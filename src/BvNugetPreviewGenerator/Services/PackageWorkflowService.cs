using BvNugetPreviewGenerator.Generate;
using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace BvNugetPreviewGenerator.Services
{
    /// <summary>
    /// Service for coordinating package generation workflow operations
    /// </summary>
    public class PackageWorkflowService : IPackageWorkflowService
    {
        private readonly IDotNetBuildService _buildService;
        private readonly INuGetPackageService _nuGetService;
        private readonly IFileSystemService _fileSystem;
        private readonly IRepositoryCleanupService _cleanupService;
        private readonly IPackageDiscoveryService _discoveryService;

        public PackageWorkflowService(
            IDotNetBuildService buildService,
            INuGetPackageService nuGetService,
            IFileSystemService fileSystem,
            IRepositoryCleanupService cleanupService,
            IPackageDiscoveryService discoveryService)
        {
            _buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
            _nuGetService = nuGetService ?? throw new ArgumentNullException(nameof(nuGetService));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        }

        /// <summary>
        /// Executes the complete single project package generation workflow
        /// </summary>
        public async Task<PackageGenerateResult> ExecuteSingleProjectWorkflowAsync(
            PackageGenerationConfig config,
            IProgressReporter reporter,
            CancellationToken cancellationToken)
        {
            var context = await CreatePackageContextAsync(config, reporter);

            try
            {
                // Step 1: Validate requirements
                await ValidateRequirementsAsync(config, reporter);
                reporter.ReportProgress(5, "Initial validation complete");

                // Step 2: Clear repository if requested
                if (config.ClearLocalRepoBeforeBuild)
                {
                    reporter.ReportProgress(7, "Clearing local repository...");
                    var projectName = Path.GetFileNameWithoutExtension(config.ProjectPath);
                    await _cleanupService.ClearLocalRepositoryAsync(config.LocalRepoPath, projectName, reporter);
                }

                // Step 3: Get project version
                reporter.ReportProgress(10, "Reading project version...");
                await ReadProjectVersionAsync(context, reporter);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Clean project if requested
                if (config.CleanBeforeBuild)
                {
                    reporter.ReportProgress(15, "Cleaning project...");
                    await CleanProjectAsync(config, reporter, cancellationToken);
                }

                // Step 5: Build project
                reporter.ReportProgress(25, "Building project...");
                await BuildProjectAsync(config, reporter, cancellationToken);

                // Step 6: Copy package to repository
                reporter.ReportProgress(75, "Copying package to repository...");
                await CopyPackageToRepositoryAsync(context, config, reporter);

                // Step 7: Cleanup
                reporter.ReportProgress(95, "Cleaning up...");
                await CleanupAfterGenerationAsync(context, reporter, cancellationToken);

                reporter.ReportProgress(100, "Package generation complete");
                return PackageGenerateResult.CreateSuccessResult(context);
            }
            catch (PackageGenerateException ex)
            {
                return PackageGenerateResult.CreateExpectedFailureResult(context, ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);
            }
        }

        /// <summary>
        /// Executes the solution build and package discovery workflow
        /// </summary>
        public async Task<PackageGenerateResult> ExecuteSolutionWorkflowAsync(
            string solutionPath,
            string buildConfiguration,
            string localRepoPath,
            bool cleanBeforeBuild,
            bool clearLocalRepoBeforeBuild,
            int maxDegreeOfParallelism,
            IProgressReporter reporter,
            CancellationToken cancellationToken)
        {
            var context = CreateSolutionContext(solutionPath, localRepoPath);

            try
            {
                // Step 1: Clear repository if requested
                if (clearLocalRepoBeforeBuild)
                {
                    reporter.ReportProgress(1, "Clearing local repository...");
                    await _cleanupService.ClearAllPackagesFromRepositoryAsync(localRepoPath, reporter);
                }

                // Step 2: Restore packages
                reporter.ReportProgress(2, "Restoring NuGet packages...");
                await RestorePackagesAsync(solutionPath, reporter, cancellationToken);

                // Step 3: Clean solution if requested
                if (cleanBeforeBuild)
                {
                    reporter.ReportProgress(5, "Cleaning solution...");
                    await CleanSolutionAsync(solutionPath, buildConfiguration, reporter, cancellationToken);
                }

                // Step 4: Analyze project versions
                reporter.ReportProgress(20, "Analyzing project versions...");
                var solutionDirectory = Path.GetDirectoryName(solutionPath);
                var projectVersions = await _nuGetService.GetProjectVersionsAsync(solutionDirectory, reporter, cancellationToken);

                // Step 5: Build solution
                reporter.ReportProgress(30, "Building solution...");
                await _buildService.BuildAsync(solutionPath, buildConfiguration, maxDegreeOfParallelism, reporter, cancellationToken);

                // Step 6: Discover and copy packages
                reporter.ReportProgress(70, "Discovering generated packages...");
                var packages = await _discoveryService.FindGeneratedPackagesAsync(solutionDirectory, buildConfiguration, projectVersions, reporter);
                
                var copyResult = await CopyDiscoveredPackagesAsync(packages, localRepoPath, reporter);

                // Step 7: Clean installed packages from cache
                reporter.ReportProgress(95, "Cleaning package cache...");
                await CleanPackageCacheAsync(projectVersions, reporter, cancellationToken);

                reporter.ReportProgress(100, $"Solution build complete. {copyResult.Success} packages copied, {copyResult.Failed} failed");
                
                context.VersionNo = $"{copyResult.Success + copyResult.Failed} packages processed";
                return PackageGenerateResult.CreateSuccessResult(context);
            }
            catch (Exception ex)
            {
                return PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);
            }
        }

        #region Private Helper Methods

        private async Task<PackageGeneratorContext> CreatePackageContextAsync(PackageGenerationConfig config, IProgressReporter reporter)
        {
            var projectName = Path.GetFileNameWithoutExtension(config.ProjectPath);
            var tempRoot = Path.Combine(Path.GetTempPath(), $"{projectName}nuget");
            await _fileSystem.CreateDirectoryAsync(tempRoot);

            return new PackageGeneratorContext
            {
                NugetPath = config.LocalRepoPath,
                ProjectPath = config.ProjectPath,
                ProjectFilename = Path.GetFileName(config.ProjectPath),
                TempPath = tempRoot
            };
        }

        private PackageGeneratorContext CreateSolutionContext(string solutionPath, string localRepoPath)
        {
            return new PackageGeneratorContext
            {
                ProjectPath = solutionPath,
                ProjectFilename = Path.GetFileName(solutionPath),
                NugetPath = localRepoPath,
                TempPath = Path.Combine(Path.GetTempPath(), "nugetsolutionbuild")
            };
        }

        private async Task ValidateRequirementsAsync(PackageGenerationConfig config, IProgressReporter reporter)
        {
            PackageGenerateException.ThrowIf(string.IsNullOrWhiteSpace(config.LocalRepoPath),
                "No NuGet repository folder has been specified, please configure a folder " +
                "Local Nuget Repository Folder in Nuget Package Manager > Nuget Preview Generator.");

            PackageGenerateException.ThrowIf(!await _fileSystem.DirectoryExistsAsync(config.LocalRepoPath),
                "The configured Local Nuget Repository Folder does not exist, please check the " +
                "configured folder in Nuget Package Manager > Nuget Preview Generator is correct.");

            PackageGenerateException.ThrowIf(string.IsNullOrWhiteSpace(config.ProjectPath),
                "No project file was specified for this package.");
        }

        private async Task ReadProjectVersionAsync(PackageGeneratorContext context, IProgressReporter reporter)
        {
            reporter.LogMessage($"Reading project version from {context.ProjectFilename}");
            context.OriginalProjectContent = await _fileSystem.ReadAllTextAsync(context.ProjectPath);

            var doc = new XmlDocument();
            doc.LoadXml(context.OriginalProjectContent);
            var versionNode = doc.SelectSingleNode("/Project/PropertyGroup/Version");

            PackageGenerateException.ThrowIf(versionNode == null,
                "No version number found, project must contain a version number in the " +
                "format Major.Minor.Patch e.g. 1.5.3 or 1.5.3.4");

            if (!PackageVersion.TryParse(versionNode.InnerText, out var version))
                throw new PackageGenerateException("Version number did not match " +
                    "expected format. Project must contain a version number in the format " +
                    "Major.Minor.Patch e.g. 1.5.3 or 1.5.3.4");

            context.VersionNo = version.ToString();
        }

        private async Task CleanProjectAsync(PackageGenerationConfig config, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            try
            {
                await _buildService.CleanAsync(config.ProjectPath, config.BuildConfiguration, reporter, cancellationToken);
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Warning: Clean operation failed but continuing: {ex.Message}");
            }
        }

        private async Task BuildProjectAsync(PackageGenerationConfig config, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            await _buildService.BuildAsync(config.ProjectPath, config.BuildConfiguration, config.MaxDegreeOfParallelism, reporter, cancellationToken);
        }

        private async Task CopyPackageToRepositoryAsync(PackageGeneratorContext context, PackageGenerationConfig config, IProgressReporter reporter)
        {
            var sourcePath = await _discoveryService.FindProjectPackageAsync(context.ProjectPath, config.BuildConfiguration, context.VersionNo, reporter);
            var projectName = Path.GetFileNameWithoutExtension(context.ProjectFilename);
            var packageFileName = _nuGetService.GetPackageFileName(projectName, context.VersionNo);
            var destPath = Path.Combine(context.NugetPath, packageFileName);

            await _nuGetService.CopyPackageToRepoAsync(sourcePath, destPath, reporter);
        }

        private async Task CleanupAfterGenerationAsync(PackageGeneratorContext context, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            // Delete installed package from cache
            await _nuGetService.DeleteInstalledPackageAsync(
                context.ProjectFilename.Replace(".csproj", ""),
                context.VersionNo,
                reporter,
                cancellationToken);

            reporter.LogMessage($"Package generation cleanup completed");
        }

        private async Task RestorePackagesAsync(string solutionPath, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            try
            {
                await _buildService.RestorePackagesAsync(solutionPath, reporter, cancellationToken);
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Warning: Package restore failed but continuing: {ex.Message}");
            }
        }

        private async Task CleanSolutionAsync(string solutionPath, string buildConfiguration, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            try
            {
                await _buildService.CleanAsync(solutionPath, buildConfiguration, reporter, cancellationToken);
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Warning: Clean operation failed but continuing: {ex.Message}");
            }
        }

        private async Task<(int Success, int Failed)> CopyDiscoveredPackagesAsync(IEnumerable<string> packages, string localRepoPath, IProgressReporter reporter)
        {
            var packageList = packages.ToList();
            PackageGenerateException.ThrowIf(!packageList.Any(),
                "No NuGet packages were found in project bin folders. " +
                "Ensure projects are configured to generate NuGet packages and build completed successfully.");

            if (!await _fileSystem.DirectoryExistsAsync(localRepoPath))
                await _fileSystem.CreateDirectoryAsync(localRepoPath);

            int success = 0, failed = 0;
            foreach (var nupkg in packageList)
            {
                try
                {
                    var dest = Path.Combine(localRepoPath, Path.GetFileName(nupkg));
                    await _fileSystem.CopyFileAsync(nupkg, dest, true);
                    reporter.LogMessage($"Copied {nupkg} to {dest}");
                    success++;
                }
                catch (Exception ex)
                {
                    reporter.LogMessage($"Failed to copy {nupkg}: {ex.Message}");
                    failed++;
                }
            }

            return (success, failed);
        }

        private async Task CleanPackageCacheAsync(Dictionary<string, string> projectVersions, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            foreach (var projectVersion in projectVersions)
            {
                try
                {
                    await _nuGetService.DeleteInstalledPackageAsync(projectVersion.Key, projectVersion.Value, reporter, cancellationToken);
                    reporter.LogMessage($"Deleted installed NuGet package: {projectVersion.Key} {projectVersion.Value}");
                }
                catch (Exception ex)
                {
                    reporter.LogMessage($"Error removing NuGet package from cache: {ex.Message}");
                }
            }
        }

        #endregion
    }
}