using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public class DotNetBuildService : IDotNetBuildService
    {
        public async Task<string> RestorePackagesAsync(
            string projectOrSolutionPath,
            IProgressReporter reporter,
            CancellationToken cancellationToken = default)
        {
            return await RunCommandAsync("dotnet", $"restore \"{projectOrSolutionPath}\"", reporter, cancellationToken);
        }

        public async Task<string> CleanAsync(
            string projectOrSolutionPath,
            string buildConfiguration,
            IProgressReporter reporter,
            CancellationToken cancellationToken = default)
        {
            return await RunCommandAsync("dotnet", $"clean \"{projectOrSolutionPath}\" -c \"{buildConfiguration}\"", reporter, cancellationToken);
        }

        public async Task<string> BuildAsync(
            string projectOrSolutionPath,
            string buildConfiguration,
            string outputFolder,
            int parallelism,
            IProgressReporter reporter,
            CancellationToken cancellationToken = default)
        {
            return await RunCommandAsync("dotnet",
                $"build \"{projectOrSolutionPath}\" -c \"{buildConfiguration}\" -o \"{outputFolder}\" -m:{parallelism}",
                reporter, cancellationToken);
        }

        private async Task<string> RunCommandAsync(
            string processName,
            string parameters,
            IProgressReporter reporter,
            CancellationToken cancellationToken)
        {
            reporter.LogMessage($"Executing: {processName} {parameters}");

            var startInfo = new ProcessStartInfo(processName, parameters)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        string line = args.Data;
                        outputBuilder.AppendLine(line);
                        reporter.LogMessage($"Output: {line}");
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        string line = args.Data;
                        errorBuilder.AppendLine(line);
                        reporter.LogMessage($"Error: {line}");
                    }
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var processExitTask = Task.Run(() =>
                    {
                        process.WaitForExit();
                        return true;
                    });

                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                    if (await Task.WhenAny(processExitTask, timeoutTask) == timeoutTask)
                    {
                        // Timeout
                        reporter.LogMessage("Process timed out after 5 minutes");
                        if (!process.HasExited)
                        {
                            try { process.Kill(); } catch { /* Ignore */ }
                        }
                        throw new TimeoutException($"Process {processName} timed out");
                    }
                    string output = outputBuilder.ToString();
                    string error = errorBuilder.ToString();

                    if (string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(error))
                    {
                        output = error;
                    }

                    reporter.LogMessage($"Process exited with code: {process.ExitCode}");
                    if (process.ExitCode != 0)
                    {
                        throw new PackageGenerateException($"ERROR BUILDING: Exit code {process.ExitCode}. Check the output for more details");
                    }

                    return output;
                }
                catch (Exception ex) when (!(ex is TaskCanceledException || ex is OperationCanceledException || ex is TimeoutException))
                {
                    reporter.LogMessage($"Exception occurred running: {processName} {parameters}");
                    reporter.LogMessage($"Exception Message: {ex.Message}");
                    reporter.LogMessage($"Stack Trace: {ex.StackTrace}");
                    throw;
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        try { process.Kill(); } catch { /* Ignore errors */ }
                    }
                }
            }
        }
    }
}
