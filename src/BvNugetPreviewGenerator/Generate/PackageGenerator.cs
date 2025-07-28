using BvNugetPreviewGenerator.Services.Interfaces;
using BvNugetPreviewGenerator.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public class PackageGenerator : IPackageGenerator
    {
        #region Events and Fields

        public event Action<string> LogEvent;
        public event Action<int, string> ProgressEvent;
        public event Action<PackageGenerateResult> CompleteEvent;
        public event Action<string> PackagesLeft;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly IPackageWorkflowService _workflowService;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor with standard service implementations
        /// </summary>
        public PackageGenerator()
        {
            var fileSystem = new FileSystemService();
            var buildService = new DotNetBuildService();
            var nugetService = new NuGetPackageService(fileSystem);
            var cleanupService = new RepositoryCleanupService(fileSystem);
            var discoveryService = new PackageDiscoveryService(fileSystem);

            _workflowService = new PackageWorkflowService(
                buildService, nugetService, fileSystem, cleanupService, discoveryService);
        }

        /// <summary>
        /// Constructor with dependency injection for testing
        /// </summary>
        public PackageGenerator(IPackageWorkflowService workflowService)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Generates package for a single project
        /// </summary>
        public async Task<PackageGenerateResult> GeneratePackageAsync(
            string projectPath,
            string localRepoPath,
            string buildConfiguration,
            bool cleanBeforeBuild,
            bool clearLocalRepoBeforeBuild)
        {
            try
            {
                var reporter = CreateProgressReporter(_cancellationTokenSource.Token);
                var config = new PackageGenerationConfig
                {
                    ProjectPath = projectPath,
                    LocalRepoPath = localRepoPath,
                    BuildConfiguration = buildConfiguration,
                    CleanBeforeBuild = cleanBeforeBuild,
                    ClearLocalRepoBeforeBuild = clearLocalRepoBeforeBuild,
                    MaxDegreeOfParallelism = 1
                };

                var result = await _workflowService.ExecuteSingleProjectWorkflowAsync(
                    config, reporter, _cancellationTokenSource.Token);

                CompleteEvent?.Invoke(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Generates packages for multiple projects
        /// </summary>
        public async Task<PackageGenerateResult> GeneratePackageAsync(
            IEnumerable<string> projectPaths,
            string localRepoPath,
            string buildConfiguration,
            bool cleanBeforeBuild, 
            bool clearLocalRepoBeforeBuild,
            bool parallel = true,
            int maxDegreeOfParallelism = 4)
        {
            var pathsList = projectPaths.ToList();
            int total = pathsList.Count;
            
            PackagesLeft?.Invoke($"Success 0/{total} - Failed 0/{total}");

            return await ProcessMultipleProjectsAsync(
                pathsList, localRepoPath, buildConfiguration, 
                cleanBeforeBuild, clearLocalRepoBeforeBuild, 
                maxDegreeOfParallelism, total);
        }

        /// <summary>
        /// Builds solution and copies all generated packages
        /// </summary>
        public async Task BuildSolutionAndCopyNupkgsAsync(
            string solutionPath,
            string buildConfiguration,
            string localRepoPath, 
            bool cleanBeforeBuild, 
            bool clearLocalRepoBeforeBuild)
        {
            var reporter = CreateProgressReporter(_cancellationTokenSource.Token);
            PackageGenerateResult result = null;

            PackagesLeft?.Invoke("Starting...");

            try
            {
                result = await _workflowService.ExecuteSolutionWorkflowAsync(
                    solutionPath, buildConfiguration, localRepoPath,
                    cleanBeforeBuild, clearLocalRepoBeforeBuild,
                    1, // Default parallelism for solution builds
                    reporter, _cancellationTokenSource.Token);

                // Update packages left based on result
                if (result.ResultType == PreviewPackageGenerateResultType.Success)
                {
                    PackagesLeft?.Invoke("Solution build completed successfully");
                }
                else
                {
                    PackagesLeft?.Invoke("Solution build completed with errors");
                }
            }
            catch (Exception ex)
            {
                var context = new PackageGeneratorContext
                {
                    ProjectPath = solutionPath,
                    ProjectFilename = Path.GetFileName(solutionPath),
                    NugetPath = localRepoPath,
                    TempPath = Path.Combine(Path.GetTempPath(), "nugetsolutionbuild")
                };
                result = PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);
            }
            finally
            {
                CompleteEvent?.Invoke(result);
            }
        }

        /// <summary>
        /// Cancels ongoing operations
        /// </summary>
        public void Cancel()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates progress reporter for event forwarding
        /// </summary>
        private IProgressReporter CreateProgressReporter(CancellationToken cancellationToken = default)
        {
            return new PackageGeneratorProgressAdapter(
                message => LogEvent?.Invoke(message),
                (progress, message) => ProgressEvent?.Invoke(progress, message),
                cancellationToken
            );
        }

        /// <summary>
        /// Processes multiple projects sequentially with progress tracking
        /// </summary>
        private async Task<PackageGenerateResult> ProcessMultipleProjectsAsync(
            IList<string> projectPaths,
            string localRepoPath,
            string buildConfiguration,
            bool cleanBeforeBuild, 
            bool clearLocalRepoBeforeBuild,
            int maxDegreeOfParallelism,
            int total)
        {
            int success = 0, failed = 0;
            var results = new List<PackageGenerateResult>();

            foreach (var projectPath in projectPaths)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                    break;

                PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");
                
                var result = await GeneratePackageAsync(
                    projectPath, localRepoPath, buildConfiguration, 
                    cleanBeforeBuild, clearLocalRepoBeforeBuild);

                if (result != null)
                {
                    results.Add(result);
                    if (result.ResultType == PreviewPackageGenerateResultType.Success)
                        success++;
                    else
                        failed++;
                }
                else
                {
                    failed++;
                }
            }

            PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");

            var finalResult = CreateConsolidatedResult(success, failed, total, localRepoPath);
            CompleteEvent?.Invoke(finalResult);
            return finalResult;
        }

        /// <summary>
        /// Creates consolidated result for multiple project operations
        /// </summary>
        private PackageGenerateResult CreateConsolidatedResult(int success, int failed, int total, string localRepoPath)
        {
            var consolidatedContext = new PackageGeneratorContext
            {
                NugetPath = localRepoPath,
                ProjectPath = "Multiple Projects",
                ProjectFilename = $"{total} projects"
            };

            if (failed == 0)
            {
                var result = PackageGenerateResult.CreateSuccessResult(consolidatedContext);
                result.Message = $"Successfully processed {success} packages";
                return result;
            }
            else
            {
                return PackageGenerateResult.CreateExpectedFailureResult(
                    consolidatedContext,
                    new Exception($"Failed to process {failed} of {total} packages"));
            }
        }

        #endregion
    }
}
