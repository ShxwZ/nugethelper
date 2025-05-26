using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private readonly List<BackgroundWorker> _activeWorkers = new List<BackgroundWorker>();
        private readonly object _lock = new object();

        public PackageGenerator() { }

        public Task<PackageGenerateResult> GeneratePackageAsync(
             string projectPath,
             string localRepoPath,
             string buildConfiguration)
        {
            var tcs = new TaskCompletionSource<PackageGenerateResult>();
            var worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;

            lock (_lock)
            {
                _activeWorkers.Add(worker);
            }

            PackageGenerateResult result = null;

            worker.DoWork += (sender, e) =>
            {
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                var tempRoot = Path.Combine(Path.GetTempPath(), $"{projectName}nuget");
                Directory.CreateDirectory(tempRoot);

                var context = new PackageGeneratorContext
                {
                    NugetPath = localRepoPath,
                    ProjectPath = projectPath,
                    ProjectFilename = Path.GetFileName(projectPath),
                    TempPath = tempRoot
                };

                try
                {
                    Progress(worker, 0, "Performing Initial Checks");
                    PackageGenerateException.ThrowIf(string.IsNullOrWhiteSpace(localRepoPath),
                        "No NuGet repository folder has been specified, please configure a folder " +
                        "Local Nuget Repository Folder in Nuget Package Manager > Nuget Preview Generator.");

                    PackageGenerateException.ThrowIf(!Directory.Exists(localRepoPath),
                        "The configured Local Nuget Repository Folder does not exist, please check the " +
                        "configured folder in Nuget Package Manager > Nuget Preview Generator is correct.");

                    PackageGenerateException.ThrowIf(string.IsNullOrWhiteSpace(projectPath),
                        "No project file was specified for this package.");
                    Progress(worker, 5, "Initial Checks Complete");
                    Progress(worker, 10, "Get Project Version");
                    GetProjectVersion(context, worker, e);
                    if (CheckCancellation(worker, e)) return;

                    Progress(worker, 15, "Building Project");
                    RunDotNetBuild(context, buildConfiguration, worker, e);
                    if (CheckCancellation(worker, e)) return;

                    Progress(worker, 75, "Copying Nuget Package to Local Repo");
                    CopyNugetToLocalRepo(context, worker);
                    if (CheckCancellation(worker, e)) return;

                    Progress(worker, 95, "Cleaning Up");
                    CleanUp(context, worker, e);
                    if (CheckCancellation(worker, e)) return;

                    Progress(worker, 100, "Generation Complete");
                    result = PackageGenerateResult.CreateSuccessResult(context);
                }
                catch (PackageGenerateException ex)
                {
                    result = PackageGenerateResult.CreateExpectedFailureResult(context, ex);
                }
                catch (Exception ex)
                {
                    result = PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);
                }
            };

            worker.ProgressChanged += (sender, e) =>
            {
                var progress = e.UserState as PackageGenerateProgress;
                if (progress == null) return;
                if (progress.IsUpdate)
                    ProgressEvent?.Invoke(e.ProgressPercentage, progress.LogMessage);
                else
                    LogEvent?.Invoke(progress.LogMessage);
            };

            worker.RunWorkerCompleted += (sender, e) =>
            {
                lock (_lock)
                {
                    _activeWorkers.Remove(worker);
                }
                tcs.SetResult(result);
            };

            worker.RunWorkerAsync();
            return tcs.Task;
        }

        private static bool CheckCancellation(BackgroundWorker worker, DoWorkEventArgs e)
        {
            if (worker.CancellationPending)
            {
                e.Cancel = true;
                return true;
            }
            return false;
        }

        private void Progress(BackgroundWorker worker, int progress, string message)
        {
            worker.ReportProgress(progress, new PackageGenerateProgress(message, true));
        }

        private void Log(BackgroundWorker worker, string message)
        {
            worker.ReportProgress(0, new PackageGenerateProgress(message));
        }

        private string LoadFile(string fileName)
        {
            using (var reader = new StreamReader(fileName))
            {
                return reader.ReadToEnd();
            }
        }

        public void SaveFile(string fileName, string content)
        {
            using (var writer = new StreamWriter(fileName, false))
            {
                writer.Write(content);
            }
        }

        private void GetProjectVersion(PackageGeneratorContext context, BackgroundWorker worker, DoWorkEventArgs e)
        {
            Log(worker, $"Get Project Version in {context.ProjectFilename}");
            context.OriginalProjectContent = LoadFile(context.ProjectPath);
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

        private string RunDotNetBuild(PackageGeneratorContext context, string buildConfiguration, BackgroundWorker worker, DoWorkEventArgs e)
        {
            Log(worker, $"Running DotNet Build Of {context.ProjectFilename}");
            var projectPath = context.ProjectPath;
            var outputFolder = context.TempPath;
            var version = context.VersionNo;
            var intermediateOutputFolder = Path.Combine(outputFolder, "obj");

            var output = RunTask(context, "dotnet",
                $"build \"{projectPath}\" " +
                $"-c \"{buildConfiguration}\" " +
                $"-o:\"{outputFolder}\" " +
                $"-m:{ParallelBuild}",
                worker);
            Log(worker, $"DotNet Build Complete: {output}");
            return output;
        }

        private void CopyNugetToLocalRepo(PackageGeneratorContext context, BackgroundWorker worker)
        {
            var version = context.VersionNo;
            var projectName = Path.GetFileNameWithoutExtension(context.ProjectFilename);
            var packageFileName = $"{projectName}.{version}.nupkg";
            var sourcePath = Path.Combine(context.TempPath, packageFileName);
            var destPath = Path.Combine(context.NugetPath, packageFileName);

            if (!File.Exists(sourcePath))
            {
                Log(worker, $"NuGet package not found at {sourcePath}");
                throw new PackageGenerateException("NuGet package not found after build.");
            }

            File.Copy(sourcePath, destPath, true);
            Log(worker, $"Copied NuGet package to {destPath}");
        }

        private void CleanUp(PackageGeneratorContext context, BackgroundWorker worker, DoWorkEventArgs e)
        {
            Log(worker, "Cleaning up temporary files");
            var tempFolder = context.TempPath;
            try
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                    Log(worker, $"Deleted temp folder: {tempFolder}");
                }
            }
            catch (Exception ex)
            {
                Log(worker, $"Error deleting temp folder: {ex.Message}");
            }

            DeleteInstalledNugetPackage(context.ProjectFilename.Replace(".csproj", ""), context.VersionNo, worker);
            Log(worker, $"Deleted installed Nuget Package {context.PackageFilename}");
        }

        public void DeleteInstalledNugetPackage(string packageId, string version, BackgroundWorker worker, string customPackagesPath = null)
        {
            try
            {
                string versionFolder = null;

                if (!string.IsNullOrEmpty(customPackagesPath))
                {
                    string localPackagesPath = Path.Combine(customPackagesPath, packageId);
                    if (Directory.Exists(localPackagesPath))
                    {
                        versionFolder = localPackagesPath;
                    }
                }
                else
                {
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string globalPackagesPath = Path.Combine(userProfile, ".nuget", "packages", packageId.ToLowerInvariant(), version);
                    if (Directory.Exists(globalPackagesPath))
                    {
                        versionFolder = globalPackagesPath;
                    }
                }

                if (versionFolder != null)
                {
                    Directory.Delete(versionFolder, true);
                    Log(worker, $"Eliminada la carpeta del paquete: {versionFolder}");
                }
                else
                {
                    Log(worker, $"No se encontró el paquete {packageId} {version} ni en caché global ni en carpeta local.");
                }
            }
            catch (Exception ex)
            {
                Log(worker, $"Error al eliminar el paquete NuGet: {ex.Message}");
            }
        }

        private string RunTask(PackageGeneratorContext context, string processName, string parameters, BackgroundWorker worker)
        {
            try
            {
                Log(worker, $"Attempting to Run: {processName} {parameters}");
                var procStIfo = new ProcessStartInfo(processName, parameters)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = new Process())
                {
                    proc.StartInfo = procStIfo;
                    proc.Start();

                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();

                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        Log(worker, output);
                        throw new PackageGenerateException(
                            $"ERROR BUILDING: Check the output for more details");
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                Log(worker, $"Exception Occured running: {processName} {parameters}");
                Log(worker, $"Exception Message: {ex.Message}");
                Log(worker, $"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public Task<PackageGenerateResult> GeneratePackageAsync(
            IEnumerable<string> projectPaths,
            string localRepoPath,
            string buildConfiguration,
            bool parallel = true,
            int maxDegreeOfParallelism = 4
        )
        {
            ParallelBuild = maxDegreeOfParallelism;
            int total = projectPaths.Count();
            int success = 0;
            int failed = 0;
            PackagesLeft?.Invoke($"Success 0/{total} - Failed 0/{total}");

            return GenerateSequentialAsync(projectPaths, localRepoPath, buildConfiguration, total, success, failed);
        }

        public Task BuildSolutionAndCopyNupkgsAsync(
         string solutionPath,
         string buildConfiguration,
         string localRepoPath)
        {
            var worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;

            lock (_lock)
            {
                _activeWorkers.Add(worker);
            }

            worker.DoWork += (s, e) =>
            {
                var outputFolder = Path.Combine(Path.GetTempPath(), "nugetsolutionbuild");
                Directory.CreateDirectory(outputFolder);

                int success = 0;
                int failed = 0;
                int total = 0;
                PackageGenerateResult result = null;

                PackagesLeft?.Invoke($"Starting...");

                try
                {
                    // 0. Restaurar paquetes primero
                    Progress(worker, 2, "Restoring NuGet packages...");
                    string restoreCmd = $"restore \"{solutionPath}\"";
                    try
                    {
                        string restoreOutput = RunTask(null, "dotnet", restoreCmd, worker);
                        Log(worker, restoreOutput);
                    }
                    catch (Exception ex)
                    {
                        Log(worker, $"Warning: Package restore failed but continuing: {ex.Message}");
                    }

                    if (CheckCancellation(worker, e)) return;

                    // 1. Clean
                    Progress(worker, 5, "Cleaning solution...");
                    string cleanCmd = $"clean \"{solutionPath}\" -c \"{buildConfiguration}\"";
                    try
                    {
                        string cleanOutput = RunTask(null, "dotnet", cleanCmd, worker);
                        Log(worker, cleanOutput);
                    }
                    catch (Exception ex)
                    {
                        Log(worker, $"Warning: Clean operation failed but continuing: {ex.Message}");
                    }

                    if (CheckCancellation(worker, e)) return;

                    // 2. Build
                    Progress(worker, 30, "Building solution...");
                    string buildCmd = $"build \"{solutionPath}\" -c \"{buildConfiguration}\" -o \"{outputFolder}\"";
                    string buildOutput = RunTask(null, "dotnet", buildCmd, worker);
                    Log(worker, buildOutput);

                    if (CheckCancellation(worker, e)) return;

                    // 3. Buscar y copiar nupkgs
                    Progress(worker, 70, "Copying NuGet packages...");
                    var nupkgs = Directory.GetFiles(outputFolder, "*.nupkg", SearchOption.AllDirectories);
                    total = nupkgs.Length;

                    if (!Directory.Exists(localRepoPath))
                        Directory.CreateDirectory(localRepoPath);

                    foreach (var nupkg in nupkgs)
                    {
                        try
                        {
                            var dest = Path.Combine(localRepoPath, Path.GetFileName(nupkg));
                            File.Copy(nupkg, dest, true);
                            Log(worker, $"Copied {nupkg} to {dest}");
                            success++;
                        }
                        catch (Exception ex)
                        {
                            Log(worker, $"Failed to copy {nupkg}: {ex.Message}");
                            failed++;
                        }
                        PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");
                    }

                    if (total == 0)
                        Log(worker, "No NuGet packages found to copy.");

                    if (CheckCancellation(worker, e)) return;

                    // 4. Limpieza final
                    Progress(worker, 90, "Cleaning up temporary files...");
                    try
                    {
                        Directory.Delete(outputFolder, true);
                        Log(worker, $"Deleted temp folder: {outputFolder}");
                    }
                    catch (Exception ex)
                    {
                        Log(worker, $"Error deleting temp folder: {ex.Message}");
                    }

                    // Eliminar paquetes instalados de la caché de NuGet
                    Progress(worker, 95, "Removing installed NuGet packages from cache...");
                    foreach (var nupkg in nupkgs)
                    {
                        try
                        {
                            string filename = Path.GetFileNameWithoutExtension(nupkg);
                            // El formato es [PackageId].[1.2.3.4].nupkg
                            int firstDotIndex = filename.IndexOf('.');
                            int lastDotIndex = filename.LastIndexOf('.');

                            string packageId = filename.Substring(0, firstDotIndex);
                            string version = filename.Substring(firstDotIndex + 1);
                            DeleteInstalledNugetPackage(packageId, version, worker);
                            Log(worker, $"Deleted installed NuGet package: {packageId} {version}");

                        }
                        catch (Exception ex)
                        {
                            Log(worker, $"Error removing NuGet package from cache: {ex.Message}");
                        }
                    }
                    Progress(worker, 100, $"Build and copy complete. {success} packages moved to {localRepoPath}");
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
                    // Reportar fallo inesperado
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
                    lock (_lock)
                    {
                        _activeWorkers.Remove(worker);
                    }
                    CompleteEvent?.Invoke(result);
                }

            };

            worker.ProgressChanged += (sender, e) =>
            {
                var progress = e.UserState as PackageGenerateProgress;
                if (progress == null) return;
                if (progress.IsUpdate)
                    ProgressEvent?.Invoke(e.ProgressPercentage, progress.LogMessage);
                else
                    LogEvent?.Invoke(progress.LogMessage);
            };

            worker.RunWorkerAsync();
            return Task.CompletedTask;
        }

        private async Task<PackageGenerateResult> GenerateSequentialAsync(IEnumerable<string> projectPaths, string localRepoPath, string buildConfiguration, int total, int success, int failed)
        {
            var results = new List<PackageGenerateResult>();

            foreach (var projectPath in projectPaths)
            {
                PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");
                var result = await GeneratePackageAsync(projectPath, localRepoPath, buildConfiguration);
                results.Add(result);

                if (result != null && result.ResultType == PreviewPackageGenerateResultType.Success)
                {
                    success++;
                }
                else
                {
                    failed++;
                }
            }

            PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");

            // Crear un contexto consolidado para el resultado final
            var consolidatedContext = new PackageGeneratorContext
            {
                NugetPath = localRepoPath,
                ProjectPath = "Multiple Projects",
                ProjectFilename = $"{total} projects"
            };
            PackageGenerateResult finalResult = null;
            if (failed == 0)
            {
                finalResult = PackageGenerateResult.CreateSuccessResult(consolidatedContext);
                finalResult.Message = $"Successfully processed {success} packages";
            }
            else
            {
                finalResult = PackageGenerateResult.CreateExpectedFailureResult(
                    consolidatedContext,
                    new Exception($"Failed to process {failed} of {total} packages")
                );
                ;
            }

            CompleteEvent?.Invoke(finalResult);
            return finalResult;
        }

        public void Cancel()
        {
            lock (_lock)
            {
                foreach (var worker in _activeWorkers.ToList())
                {
                    if (worker.IsBusy && worker.WorkerSupportsCancellation)
                        worker.CancelAsync();
                }
            }
        }
    }
}
