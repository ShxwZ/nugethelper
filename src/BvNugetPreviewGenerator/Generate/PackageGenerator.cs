using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

            // Añadir el worker a la lista de activos
            lock (_lock)
            {
                _activeWorkers.Add(worker);
            }

            PackageGenerateResult result = null;

            worker.DoWork += (sender, e) =>
            {
                var context = new PackageGeneratorContext
                {
                    NugetPath = localRepoPath,
                    ProjectPath = projectPath,
                    ProjectFilename = Path.GetFileName(projectPath),
                    TempPath = Path.GetTempPath()
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

                    Progress(worker, 75, "Pushing Project to Nuget");
                    RunNugetPush(context, worker, e);
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
                // Quitar el worker de la lista de activos
                lock (_lock)
                {
                    _activeWorkers.Remove(worker);
                }
                CompleteEvent?.Invoke(result);
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

            var output = RunTask(context, "dotnet",
                $"build \"{projectPath}\" " +
                $"-c \"{buildConfiguration}\" " +
                $"-o:\"{outputFolder}\"", worker);
            Log(worker, $"DotNet Build Complete: {output}");
            return output;
        }

        private string RunNugetPush(PackageGeneratorContext context, BackgroundWorker worker, DoWorkEventArgs e)
        {
            var projectPath = context.ProjectPath;
            var outputFolder = context.TempPath;
            var version = context.VersionNo;
            var lastDirMarker = projectPath.LastIndexOf("\\");
            var fileName = projectPath.Substring(lastDirMarker + 1);
            var lastDot = fileName.LastIndexOf(".");
            var projectName = fileName.Substring(0, lastDot);
            var packageFileName = $"{projectName}.{version}.nupkg";
            Log(worker, $"Running DotNet Push Of {packageFileName}");
            context.PackageFilename = packageFileName;

            var fullProjName = $"{outputFolder}{packageFileName}";

            var output = RunTask(context, "dotnet",
                $"nuget push {fullProjName} -s {context.NugetPath}", worker);
            Log(worker, $"DotNet Push Complete");
            return output;
        }

        private void CleanUp(PackageGeneratorContext context, BackgroundWorker worker, DoWorkEventArgs e)
        {
            Log(worker, "Cleaning up temporary files");
            var tempFolder = context.TempPath;
            var packageFileName = context.PackageFilename;
            var fullProjName = $"{tempFolder}{packageFileName}";
            if (File.Exists(fullProjName))
                File.Delete(fullProjName);
            Log(worker, "Cleanup Complete");

            DeleteInstalledNugetPackage(context.ProjectFilename.Replace(".csproj", ""), context.VersionNo, worker);

            Log(worker, $"Deleted installed Nuget Package {packageFileName}");
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

        public Task GeneratePackageAsync(IEnumerable<string> projectPaths, string localRepoPath, string buildConfiguration, bool parallel = true)
        {
            int total = projectPaths.Count();
            int success = 0;
            int failed = 0;
            PackagesLeft?.Invoke($"Success 0/{total} - Failed 0/{total}");

            if (parallel)
            {
                var tasks = projectPaths.Select(projectPath =>
                    GeneratePackageAsync(projectPath, localRepoPath, buildConfiguration)
                        .ContinueWith(t =>
                        {
                            var result = t.Result;
                            if (result != null && result.ResultType == PreviewPackageGenerateResultType.Success)
                            {
                                int done = System.Threading.Interlocked.Increment(ref success);
                                PackagesLeft?.Invoke($"Success {done}/{total} - Failed {failed}/{total}");
                            }
                            else
                            {
                                int fail = System.Threading.Interlocked.Increment(ref failed);
                                PackagesLeft?.Invoke($"Success {success}/{total} - Failed {fail}/{total}");
                            }
                        }, TaskScheduler.Default)
                );
                return Task.WhenAll(tasks);
            }
            else
            {
                return GenerateSequentialAsync(projectPaths, localRepoPath, buildConfiguration, total, success, failed);
            }
        }

        private async Task GenerateSequentialAsync(IEnumerable<string> projectPaths, string localRepoPath, string buildConfiguration, int total, int success, int failed)
        {
            foreach (var projectPath in projectPaths)
            {
                PackagesLeft?.Invoke($"Success {success}/{total} - Failed {failed}/{total}");
                var result = await GeneratePackageAsync(projectPath, localRepoPath, buildConfiguration);
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
