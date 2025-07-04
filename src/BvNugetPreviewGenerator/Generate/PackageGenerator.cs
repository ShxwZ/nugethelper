﻿using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace BvNugetPreviewGenerator.Generate
{
    public class PackageGenerator : IPackageGenerator
    {
        public event Action<string> LogEvent;
        public event Action<int, string> ProgressEvent;
        public event Action<PackageGenerateResult> CompleteEvent;
        public event Action<string> PackagesLeft;

        private int ParallelBuild = 1;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly IDotNetBuildService _buildService;
        private readonly INuGetPackageService _nuGetService;
        private readonly IFileSystemService _fileSystem;

        public PackageGenerator()
        {
            _fileSystem = new FileSystemService();
            _buildService = new DotNetBuildService();
            _nuGetService = new NuGetPackageService(_fileSystem);
        }

        public PackageGenerator(
            IDotNetBuildService buildService,
            INuGetPackageService nuGetService,
            IFileSystemService fileSystem)
        {
            _buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
            _nuGetService = nuGetService ?? throw new ArgumentNullException(nameof(nuGetService));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        private IProgressReporter CreateProgressReporter(CancellationToken cancellationToken = default)
        {
            return new PackageGeneratorProgressAdapter(
                message => LogEvent?.Invoke(message),
                (progress, message) => ProgressEvent?.Invoke(progress, message),
                cancellationToken
            );
        }

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
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                var tempRoot = Path.Combine(Path.GetTempPath(), $"{projectName}nuget");
                await _fileSystem.CreateDirectoryAsync(tempRoot);

                var context = new PackageGeneratorContext
                {
                    NugetPath = localRepoPath,
                    ProjectPath = projectPath,
                    ProjectFilename = Path.GetFileName(projectPath),
                    TempPath = tempRoot
                };

                try
                {
                    reporter.ReportProgress(0, "Performing Initial Checks");
                    await ValidateInitialRequirements(localRepoPath, projectPath);
                    reporter.ReportProgress(5, "Initial Checks Complete");

                    if (clearLocalRepoBeforeBuild && await _fileSystem.DirectoryExistsAsync(localRepoPath))
                    {
                        reporter.ReportProgress(7, "Clearing local NuGet repository...");
                        try
                        {
                            var existingPackages = await _fileSystem.FindFilesAsync(
                                localRepoPath,
                                $"{projectName}*.nupkg",
                                false);

                            int deletedCount = 0;
                            foreach (var package in existingPackages)
                            {
                                try
                                {
                                    await _fileSystem.DeleteFileAsync(package);
                                    deletedCount++;
                                }
                                catch (Exception ex)
                                {
                                    reporter.LogMessage($"Warning: Could not delete package {package}: {ex.Message}");
                                }
                            }

                            reporter.LogMessage($"Cleared {deletedCount} existing NuGet package(s) for {projectName} from local repository");
                        }
                        catch (Exception ex)
                        {
                            reporter.LogMessage($"Warning: Failed to clear local repository: {ex.Message}");
                        }
                    }

                    reporter.ReportProgress(10, "Get Project Version");
                    await GetProjectVersionAsync(context, reporter);
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    if (cleanBeforeBuild)
                    {
                        reporter.ReportProgress(12, "Cleaning project...");
                        try
                        {
                            await _buildService.CleanAsync(
                                context.ProjectPath, buildConfiguration, reporter, _cancellationTokenSource.Token);
                        }
                        catch (Exception ex)
                        {
                            reporter.LogMessage($"Warning: Clean operation failed but continuing: {ex.Message}");
                        }
                    }
                    else
                    {
                        reporter.LogMessage("Clean before build option is disabled. Skipping clean step.");
                    }

                    reporter.ReportProgress(15, "Building Project");
                    await _buildService.BuildAsync(
                        context.ProjectPath, buildConfiguration, ParallelBuild, reporter, _cancellationTokenSource.Token);

                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    reporter.ReportProgress(75, "Copying Nuget Package to Local Repo");
                    await CopyNugetToLocalRepoAsync(context, reporter, buildConfiguration);
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    reporter.ReportProgress(95, "Cleaning Up");
                    await CleanUpAsync(context, reporter);
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    reporter.ReportProgress(100, "Generation Complete");
                    var result = PackageGenerateResult.CreateSuccessResult(context);
                    CompleteEvent?.Invoke(result);
                    return result;
                }
                catch (PackageGenerateException ex)
                {
                    var result = PackageGenerateResult.CreateExpectedFailureResult(context, ex);
                    CompleteEvent?.Invoke(result);
                    return result;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    var result = PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);
                    CompleteEvent?.Invoke(result);
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private async Task ValidateInitialRequirements(string localRepoPath, string projectPath)
        {
            PackageGenerateException.ThrowIf(string.IsNullOrWhiteSpace(localRepoPath),
                "No NuGet repository folder has been specified, please configure a folder " +
                "Local Nuget Repository Folder in Nuget Package Manager > Nuget Preview Generator.");

            PackageGenerateException.ThrowIf(!await _fileSystem.DirectoryExistsAsync(localRepoPath),
                "The configured Local Nuget Repository Folder does not exist, please check the " +
                "configured folder in Nuget Package Manager > Nuget Preview Generator is correct.");

            PackageGenerateException.ThrowIf(string.IsNullOrWhiteSpace(projectPath),
                "No project file was specified for this package.");
        }

        private async Task GetProjectVersionAsync(PackageGeneratorContext context, IProgressReporter reporter)
        {
            reporter.LogMessage($"Get Project Version in {context.ProjectFilename}");
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

        private async Task CopyNugetToLocalRepoAsync(PackageGeneratorContext context, IProgressReporter reporter, string buildConfiguration)
        {
            var projectName = Path.GetFileNameWithoutExtension(context.ProjectFilename);
            var packageFileName = _nuGetService.GetPackageFileName(projectName, context.VersionNo);

            var projectDirectory = Path.GetDirectoryName(context.ProjectPath);


            var binPath = Path.Combine(projectDirectory, "bin", buildConfiguration);

            var sourcePath = string.Empty;
            var files = await _fileSystem.FindFilesAsync(binPath, $"{projectName}*.{context.VersionNo}.nupkg", true);

            if (files.Any())
            {
                sourcePath = files.First();
                reporter.LogMessage($"Found package in bin folder: {sourcePath}");
            }
            else
            {
                throw new PackageGenerateException($"NuGet package {packageFileName} not found in bin folder. " +
                    "Ensure the project is built and the package is generated correctly.");
            }

            var destPath = Path.Combine(context.NugetPath, packageFileName);

            await _nuGetService.CopyPackageToRepoAsync(sourcePath, destPath, reporter);
        }

        private async Task CleanUpAsync(PackageGeneratorContext context, IProgressReporter reporter)
        {
            // Clean up temporary files 
            reporter.LogMessage("Cleaning up temporary files");
            try
            {
                if (await _fileSystem.DirectoryExistsAsync(context.TempPath))
                {
                    await _fileSystem.DeleteDirectoryAsync(context.TempPath, true);
                    reporter.LogMessage($"Deleted temp folder: {context.TempPath}");
                }
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error deleting temp folder: {ex.Message}");
            }

            // Delete installed package from cache
            await _nuGetService.DeleteInstalledPackageAsync(
                context.ProjectFilename.Replace(".csproj", ""),
                context.VersionNo,
                reporter,
                _cancellationTokenSource.Token);

            reporter.LogMessage($"Deleted installed Nuget Package {context.PackageFilename}");
        }

        public async Task<PackageGenerateResult> GeneratePackageAsync(
            IEnumerable<string> projectPaths,
            string localRepoPath,
            string buildConfiguration,
            bool cleanBeforeBuild, bool clearLocalRepoBeforeBuild,
            bool parallel = true,
            int maxDegreeOfParallelism = 4
        )
        {
            ParallelBuild = maxDegreeOfParallelism;
            int total = projectPaths.Count();
            PackagesLeft?.Invoke($"Success 0/{total} - Failed 0/{total}");

            return await ProcessMultipleProjectsAsync(projectPaths, localRepoPath, buildConfiguration, cleanBeforeBuild, clearLocalRepoBeforeBuild,total );
        }

        private async Task<PackageGenerateResult> ProcessMultipleProjectsAsync(
            IEnumerable<string> projectPaths,
            string localRepoPath,
            string buildConfiguration,
            bool cleanBeforeBuild, bool clearLocalRepoBeforeBuild,
            int total)
        {
            int success = 0, failed = 0;
            var results = new List<PackageGenerateResult>();

            foreach (var projectPath in projectPaths)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                    break;

                PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");
                var result = await GeneratePackageAsync(projectPath, localRepoPath, buildConfiguration, cleanBeforeBuild, clearLocalRepoBeforeBuild);

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

            var consolidatedContext = new PackageGeneratorContext
            {
                NugetPath = localRepoPath,
                ProjectPath = "Multiple Projects",
                ProjectFilename = $"{total} projects"
            };

            PackageGenerateResult finalResult;
            if (failed == 0)
            {
                finalResult = PackageGenerateResult.CreateSuccessResult(consolidatedContext);
                finalResult.Message = $"Successfully processed {success} packages";
            }
            else
            {
                finalResult = PackageGenerateResult.CreateExpectedFailureResult(
                    consolidatedContext,
                    new Exception($"Failed to process {failed} of {total} packages"));
            }

            CompleteEvent?.Invoke(finalResult);
            return finalResult;
        }

        public async Task BuildSolutionAndCopyNupkgsAsync(
         string solutionPath,
         string buildConfiguration,
         string localRepoPath, bool cleanBeforeBuild, bool clearLocalRepoBeforeBuild)
        {
            var reporter = CreateProgressReporter(_cancellationTokenSource.Token);
            var outputFolder = Path.Combine(Path.GetTempPath(), "nugetsolutionbuild");
            await _fileSystem.CreateDirectoryAsync(outputFolder);

            int success = 0;
            int failed = 0;
            int total = 0;
            PackageGenerateResult result = null;

            PackagesLeft?.Invoke($"Starting...");

            try
            {

                if (clearLocalRepoBeforeBuild && await _fileSystem.DirectoryExistsAsync(localRepoPath))
                {
                    reporter.ReportProgress(1, "Clearing local NuGet repository...");
                    try
                    {
                        var existingPackages = await _fileSystem.FindFilesAsync(localRepoPath, "*.nupkg", false);
                        int deletedCount = 0;

                        foreach (var package in existingPackages)
                        {
                            try
                            {
                                await _fileSystem.DeleteFileAsync(package);
                                deletedCount++;
                            }
                            catch (Exception ex)
                            {
                                reporter.LogMessage($"Warning: Could not delete package {package}: {ex.Message}");
                            }
                        }

                        reporter.LogMessage($"Cleared {deletedCount} NuGet packages from local repository");
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Warning: Failed to clear local repository: {ex.Message}");
                    }
                }

                // Restore NuGet packages
                reporter.ReportProgress(2, "Restoring NuGet packages...");
                try
                {
                    await _buildService.RestorePackagesAsync(
                        solutionPath, reporter, _cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    reporter.LogMessage($"Warning: Package restore failed but continuing: {ex.Message}");
                }

                if (_cancellationTokenSource.IsCancellationRequested) return;


                if (cleanBeforeBuild)
                {
                    reporter.ReportProgress(5, "Cleaning solution...");
                    try
                    {
                        await _buildService.CleanAsync(
                            solutionPath, buildConfiguration, reporter, _cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Warning: Clean operation failed but continuing: {ex.Message}");
                    }

                    if (_cancellationTokenSource.IsCancellationRequested) return;
                }
                else
                {
                    reporter.LogMessage("Clean before build option is disabled. Skipping clean step.");
                }

                reporter.ReportProgress(20, "Analizando proyectos para determinar versiones...");
                var projectVersions = new Dictionary<string, string>();
                var solutionDirectory = Path.GetDirectoryName(solutionPath);
 
                var projectFiles = await _fileSystem.FindFilesAsync(solutionDirectory, "*.csproj", true);
                foreach (var projectFile in projectFiles)
                {
                    try
                    {
                        var projectName = Path.GetFileNameWithoutExtension(projectFile);
                        var projectContent = await _fileSystem.ReadAllTextAsync(projectFile);

                        var doc = new XmlDocument();
                        doc.LoadXml(projectContent);
                        var versionNode = doc.SelectSingleNode("/Project/PropertyGroup/Version");

                        if (versionNode != null && PackageVersion.TryParse(versionNode.InnerText, out var version))
                        {
                            projectVersions[projectName] = version.ToString();
                            reporter.LogMessage($"Proyecto {projectName}: versión {version}");
                        }
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Error al obtener versión del proyecto {projectFile}: {ex.Message}");
                    }
                }

                // Compile solution
                reporter.ReportProgress(30, "Building solution...");
                await _buildService.BuildAsync(
                    solutionPath, buildConfiguration, ParallelBuild, reporter, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.IsCancellationRequested) return;

                reporter.ReportProgress(70, "Searching for NuGet packages in project bin folders...");
                

                reporter.LogMessage("Buscando paquetes NuGet específicos en las carpetas bin de los proyectos");
                List<string> allNupkgs = new List<string>();

                var allFiles = await _fileSystem.FindFilesAsync(
                    solutionDirectory,
                    "*.nupkg",
                    true);


                foreach (var file in allFiles)
                {
                    var filePath = file.ToLower();
                    var fileName = Path.GetFileName(file);


                    bool isInCorrectBinFolder = filePath.Contains($"\\bin\\{buildConfiguration.ToLower()}\\") ||
                                               (filePath.Contains($"\\bin\\") && filePath.Contains($"\\{buildConfiguration.ToLower()}\\"));

                    if (!isInCorrectBinFolder)
                        continue;

                    bool matchesKnownVersion = false;
                    foreach (var projectVersion in projectVersions)
                    {
                        if (fileName.StartsWith(projectVersion.Key, StringComparison.OrdinalIgnoreCase) &&
                            fileName.Contains(projectVersion.Value))
                        {
                            matchesKnownVersion = true;
                            reporter.LogMessage($"Encontrado paquete NuGet de {projectVersion.Key} v{projectVersion.Value}: {fileName}");
                            break;
                        }
                    }

                    if (matchesKnownVersion)
                    {
                        allNupkgs.Add(file);
                    }
                    else if (isInCorrectBinFolder)
                    {
                        reporter.LogMessage($"Ignorando paquete que no coincide con versiones conocidas: {fileName}");
                    }
                }

                if (!allNupkgs.Any())
                {
                    throw new PackageGenerateException("No se encontraron paquetes NuGet recién generados en las carpetas bin de los proyectos. " +
                        "Asegúrate de que los proyectos estén configurados para generar paquetes NuGet y que la compilación se haya completado correctamente.");
                }

                var nupkgList = allNupkgs.Distinct().ToList();
                reporter.LogMessage($"Found {nupkgList.Count} newly generated NuGet packages");
                total = nupkgList.Count;

                if (!await _fileSystem.DirectoryExistsAsync(localRepoPath))
                    await _fileSystem.CreateDirectoryAsync(localRepoPath);

                foreach (var nupkg in nupkgList)
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
                    PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");
                }

                if (total == 0)
                    reporter.LogMessage("No NuGet packages found to copy.");

                if (_cancellationTokenSource.IsCancellationRequested) return;

                // Final cleanup
                reporter.ReportProgress(90, "Cleaning up temporary files...");
                try
                {
                    await _fileSystem.DeleteDirectoryAsync(outputFolder, true);
                    reporter.LogMessage($"Deleted temp folder: {outputFolder}");
                }
                catch (Exception ex)
                {
                    reporter.LogMessage($"Error deleting temp folder: {ex.Message}");
                }

                // Delete installed NuGet packages from c
                // 
                reporter.ReportProgress(95, "Removing installed NuGet packages from cache...");
                foreach (var projectVersion in projectVersions)
                {
                    try
                    {
                        var packageId = projectVersion.Key;
                        var version = projectVersion.Value;
                        await _nuGetService.DeleteInstalledPackageAsync(packageId, version, reporter, _cancellationTokenSource.Token);
                        reporter.LogMessage($"Deleted installed NuGet package: {packageId} {version}");
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Error removing NuGet package from cache: {ex.Message}");
                    }
                }

                reporter.ReportProgress(100, $"Build and copy complete. {success} packages moved to {localRepoPath}");
                PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");

                var context = new PackageGeneratorContext
                {
                    ProjectPath = solutionPath,
                    ProjectFilename = Path.GetFileName(solutionPath),
                    NugetPath = localRepoPath,
                    TempPath = outputFolder,
                    VersionNo = null,
                    PackageFilename = null
                };

                result = PackageGenerateResult.CreateSuccessResult(context);
            }
            catch (Exception ex)
            {
                var context = new PackageGeneratorContext
                {
                    ProjectPath = solutionPath,
                    ProjectFilename = Path.GetFileName(solutionPath),
                    NugetPath = localRepoPath,
                    TempPath = outputFolder
                };
                result = PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);
            }
            finally
            {
                CompleteEvent?.Invoke(result);
            }
        }

        public void Cancel()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();
        }
    }
}
