using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace BvNugetPreviewGenerator.Generate
{
    public class PackageGenerator : IPackageGenerator
    {
        public event Action<string> LogEvent;
        public event Action<int, string> ProgressEvent;
        public event Action<PackageGenerateResult> CompleteEvent;

        private string _LocalRepoPath;
        private string _ProjectPath;
        private string _BuildConfiguration;
        private int _Progress;
        private BackgroundWorker _Worker;
        private PackageGenerateResult _Result;
        public PackageGenerator()
        {
            _Worker = new BackgroundWorker();
            _Worker.WorkerReportsProgress = true;
            _Worker.DoWork += Worker_DoWork;
            _Worker.ProgressChanged += Worker_ProgressChanged;
            _Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            _Worker.WorkerSupportsCancellation = true;
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (CompleteEvent != null)
                CompleteEvent(_Result);
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            PackageGenerateProgress result = e.UserState as PackageGenerateProgress;
            if (result == null)
                return;

            if (result.IsUpdate && ProgressEvent != null)
                ProgressEvent(e.ProgressPercentage, result.LogMessage);


            if (!result.IsUpdate && LogEvent != null)
                LogEvent(result.LogMessage);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var context = new PackageGeneratorContext();
            context.NugetPath = _LocalRepoPath;
            context.ProjectPath = _ProjectPath;
            context.ProjectFilename = Path.GetFileName(_ProjectPath);
            context.TempPath = Path.GetTempPath();

            try
            {
                Progress(0, "Performing Initial Checks");
                PackageGenerateException
                    .ThrowIf(string.IsNullOrWhiteSpace(_LocalRepoPath),
                        "No NuGet repository folder has been specified, please configure a folder " +
                        "Local Nuget Repository Folder in Nuget Package Manager > Nuget Preview Generator.");

                PackageGenerateException
                    .ThrowIf(!Directory.Exists(_LocalRepoPath),
                        "The configured Local Nuget Repository Folder does not exist, please check the " +
                        "configured folder in Nuget Package Manager > Nuget Preview Generator is correct.");

                PackageGenerateException
                    .ThrowIf(string.IsNullOrWhiteSpace(_ProjectPath),
                        "No project file was specified for this package.");
                Progress(5, "Initial Checks Complete");
                Progress(10, "Get Project Version");
                GetProjectVersion(context);
                if (_Worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                Progress(15, "Building Project");
                RunDotNetBuild(context);
                if (_Worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                Progress(75, "Pushing Project to Nuget");
                RunNugetPush(context);
                if (_Worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                Progress(95, "Cleaning Up");
                CleanUp(context);
                if (_Worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                Progress(100, "Generation Complete");
                _Result = PackageGenerateResult.CreateSuccessResult(context);
            }
            catch (PackageGenerateException ex)
            {
                _Result = PackageGenerateResult.CreateExpectedFailureResult(context, ex);                
            }
            catch (Exception ex)
            {
                _Result = PackageGenerateResult.CreateUnexpectedFailureResult(context, ex);                
            }            
        }

        public void DeleteInstalledNugetPackage(string packageId, string version, string customPackagesPath = null)
        {
            try
            {
                string versionFolder = null;

                
                if (!string.IsNullOrEmpty(customPackagesPath))
                {
                    // TODO: In the future, we should check if the customPackagesPath is a valid path
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
                    string projectPackagesPath = Path.Combine(userProfile, ".nuget", "packages", packageId.ToLowerInvariant(), version);
                    if (Directory.Exists(globalPackagesPath))
                    {
                        versionFolder = globalPackagesPath;
                    }
                }

                if (versionFolder != null)
                {
                    Directory.Delete(versionFolder, true);
                    Log($"Eliminada la carpeta del paquete: {versionFolder}");
                }
                else
                {
                    Log($"No se encontró el paquete {packageId} {version} ni en caché global ni en carpeta local.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error al eliminar el paquete NuGet: {ex.Message}");
            }
        }
        public void GeneratePackage()
        {
            PackageGenerateException.ThrowIf(_Worker.IsBusy, "GeneratePackage is Already Running");
            _Worker.RunWorkerAsync();            
        }

        private string LoadFile(string fileName)
        {
            var reader = new StreamReader(fileName);
            var data = reader.ReadToEnd();
            reader.Close();
            return data;
        }
        public void SaveFile(string fileName, string content)
        {
            var writer = new StreamWriter(fileName, false);
            writer.Write(content);
            writer.Close();
        }

        private void GetProjectVersion(PackageGeneratorContext context)
        {
            Log($"Get Project Version in {context.ProjectFilename}");
            context.OriginalProjectContent = LoadFile(context.ProjectPath);
            var doc = new XmlDocument();
            doc.LoadXml(context.OriginalProjectContent);
            var versionNode = doc.SelectSingleNode("/Project/PropertyGroup/Version");
            PackageGenerateException
                .ThrowIf(versionNode == null, 
                "No version number found, project must contain a version number in the " +
                "format Major.Minor.Patch e.g. 1.5.3 or 1.5.3.4");

            if (!PackageVersion.TryParse(versionNode.InnerText, out var version))
                throw new PackageGenerateException("Version number did not match " +
                    "expected format. Project must contain a version number in the format " +
                    "Major.Minor.Patch e.g. 1.5.3 or 1.5.3.4");

            context.VersionNo = version.ToString();
        }
        private string RunDotNetBuild(PackageGeneratorContext context)
        {
            Log($"Running DotNet Build Of {context.ProjectFilename}");
            var projectPath = context.ProjectPath;
            var outputFolder = context.TempPath;
            var version = context.VersionNo;

            var output = RunTask(context, "dotnet", 
                $"build \"{projectPath}\" " +
                $"--configuration \"{this._BuildConfiguration}\" " +
                $"-o:\"{outputFolder}\"");
            Log($"DotNet Build Complete: {output}");
            return output;
        }
        private string RunNugetPush(PackageGeneratorContext context)
        {
            var projectPath = context.ProjectPath;
            var outputFolder = context.TempPath;
            var version = context.VersionNo;            
            var lastDirMarker = projectPath.LastIndexOf("\\");
            var path = projectPath.Substring(0, lastDirMarker);
            var fileName = projectPath.Substring(lastDirMarker + 1);
            var lastDot = fileName.LastIndexOf(".");
            var projectName = fileName.Substring(0, lastDot);
            var packageFileName = $"{projectName}.{version}.nupkg";
            Log($"Running DotNet Push Of {packageFileName}");
            context.PackageFilename = packageFileName;

            var fullProjName = $"{outputFolder}{packageFileName}";            
            
            var sb = new StringBuilder();
            var output = RunTask(context, "dotnet", 
                $"nuget push {fullProjName} -s {context.NugetPath}");
            Log($"DotNet Push Complete");
            return output;
        }
        private void CleanUp(PackageGeneratorContext context)
        {
            Log("Cleaning up temporary files");
            var tempFolder = context.TempPath;
            var packageFileName = context.PackageFilename;
            var fullProjName = $"{tempFolder}{packageFileName}";
            if (File.Exists(fullProjName))
                File.Delete(fullProjName);
            Log("Cleanup Complete");

            DeleteInstalledNugetPackage(context.ProjectFilename.Replace(".csproj",""), context.VersionNo);

            Log($"Deleted installed Nuget Package {packageFileName}");
        }
        private string RunTask(PackageGeneratorContext context, string processName, string parameters)
        {
            try
            {
                Log($"Attempting to Run: {processName} {parameters}");
                var procStIfo = new ProcessStartInfo(processName, parameters);
                procStIfo.RedirectStandardOutput = true;
                procStIfo.RedirectStandardError = true;
                procStIfo.UseShellExecute = false;
                procStIfo.CreateNoWindow = true;

                using (var proc = new Process())
                {
                    proc.StartInfo = procStIfo;
                    proc.Start();

                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();

                    proc.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Log($"dotnet error: {error}");
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                Log($"Exception Occured running: {processName} {parameters}");
                Log($"Exception Message: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
        public void Log(string message)
        {
            _Worker.ReportProgress(_Progress, new PackageGenerateProgress(message));
        }
        public void Progress(int progress, string message)
        {
            _Progress = progress;
            _Worker.ReportProgress(progress, new PackageGenerateProgress(message, true));
        }
        private Task<PackageGenerateResult> GeneratePackageInternalAsync()
        {
            var tcs = new TaskCompletionSource<PackageGenerateResult>();
            Action<PackageGenerateResult> handler = null;
            handler = result =>
            {
                CompleteEvent -= handler;
                tcs.SetResult(result);
            };
            CompleteEvent += handler;
            GeneratePackage();
            return tcs.Task;
        }

        public async Task GeneratePackageAsync(IEnumerable<string> projectPaths, string localRepoPath, string buildConfiguration)
        {
            int total = projectPaths.Count();
            int current = 0;

            this._LocalRepoPath = localRepoPath;
            this._BuildConfiguration = buildConfiguration;
            Log($"Using build configuration: {this._BuildConfiguration}");
            foreach (var projectPath in projectPaths)
            {
                current++;
                ProgressEvent?.Invoke((current * 100) / total, $"Procesando {Path.GetFileName(projectPath)} ({current}/{total})");
                this._ProjectPath = projectPath;

                await GeneratePackageInternalAsync(); 
            }
        }
        public void Cancel()
        {
            if (_Worker != null && _Worker.IsBusy && _Worker.WorkerSupportsCancellation)
                _Worker.CancelAsync();
        }
    }
}
