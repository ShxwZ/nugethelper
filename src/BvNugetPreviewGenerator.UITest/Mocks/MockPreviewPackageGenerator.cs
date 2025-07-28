using BvNugetPreviewGenerator.Generate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.UITest.Mocks
{
    class MockPreviewPackageGenerator : IPackageGenerator
    {
        public event Action<string> LogEvent;
        public event Action<int, string> ProgressEvent;
        public event Action<PackageGenerateResult> CompleteEvent;
        public event Action<string> PackagesLeft;

        private PreviewPackageGenerateResultType _ResultType;
        private Exception _ex;

        public MockPreviewPackageGenerator(PreviewPackageGenerateResultType resulttype, Exception ex)
        {
            _ResultType = resulttype;
            _ex = ex;
        }

        public async Task<PackageGenerateResult> GeneratePackageAsync(
            IEnumerable<string> projectPaths, 
            string localRepoPath, 
            string buildConfiguration, 
            bool cleanBeforeBuild, 
            bool clearLocalRepoBeforeBuild, 
            bool parallel = true, 
            int maxDegreeOfParallelism = 4)
        {
            int total = projectPaths.Count();
            int current = 0;

            PackageGenerateResult lastResult = null;

            foreach (var projectPath in projectPaths)
            {
                current++;
                PackagesLeft?.Invoke($"Success {current-1}/{total} - Failed 0/{total}");
                Log($"[MOCK] Starting Test for {projectPath} (Config: {buildConfiguration}, Repo: {localRepoPath})");
                Progress((current * 100) / total, $"Processing {System.IO.Path.GetFileName(projectPath)} ({current}/{total})");

                // Simula pasos de generación
                Log("[MOCK] Running Process 1");
                await Task.Delay(100);
                Log("[MOCK] Finished Process 1");
                Progress(30, "Second Stuff");
                await Task.Delay(100);
                Log("[MOCK] Running Process 2");
                await Task.Delay(100);
                Log("[MOCK] Finished Process 2");
                Progress(80, "Last Stuff");
                Log("[MOCK] Running Process 3");
                await Task.Delay(200);
                Log("[MOCK] Process 3 Stage 1");
                await Task.Delay(200);
                Log("[MOCK] Process 3 Stage 2");
                await Task.Delay(200);
                Log("[MOCK] Process 3 Stage 3");
                Log("[MOCK] Finished Process 3");
                Progress(100, "Done");
                await Task.Delay(100);

                // Crea el contexto y resultado simulado
                var context = new PackageGeneratorContext
                {
                    ProjectPath = projectPath,
                    NugetPath = localRepoPath
                };

                PackageGenerateResult result;
                if (_ResultType == PreviewPackageGenerateResultType.Success)
                {
                    result = PackageGenerateResult.CreateSuccessResult(context);
                }
                else if (_ResultType == PreviewPackageGenerateResultType.ExpectedFailure)
                {
                    result = PackageGenerateResult.CreateExpectedFailureResult(context, _ex);
                }
                else
                {
                    result = PackageGenerateResult.CreateUnexpectedFailureResult(context, _ex);
                }
                
                lastResult = result;
            }

            PackagesLeft?.Invoke($"Success {total}/{total} - Failed 0/{total}");
            CompleteEvent?.Invoke(lastResult);
            return lastResult;
        }

        public async Task BuildSolutionAndCopyNupkgsAsync(
            string solutionPath, 
            string buildConfiguration, 
            string localRepoPath, 
            bool cleanBeforeBuild, 
            bool clearLocalRepoBeforeBuild)
        {
            Log($"[MOCK] Building solution: {solutionPath}");
            Log($"[MOCK] Configuration: {buildConfiguration}");
            Log($"[MOCK] Target repo: {localRepoPath}");
            
            Progress(25, "Building solution...");
            await Task.Delay(500);
            
            Progress(75, "Copying packages...");
            await Task.Delay(300);
            
            Progress(100, "Complete");
            
            var context = new PackageGeneratorContext
            {
                ProjectPath = solutionPath,
                NugetPath = localRepoPath
            };

            PackageGenerateResult result;
            if (_ResultType == PreviewPackageGenerateResultType.Success)
            {
                result = PackageGenerateResult.CreateSuccessResult(context);
            }
            else if (_ResultType == PreviewPackageGenerateResultType.ExpectedFailure)
            {
                result = PackageGenerateResult.CreateExpectedFailureResult(context, _ex);
            }
            else
            {
                result = PackageGenerateResult.CreateUnexpectedFailureResult(context, _ex);
            }
            
            CompleteEvent?.Invoke(result);
            await Task.CompletedTask;
        }

        public void Cancel()
        {
            Log("[MOCK] Cancel requested");
        }

        private void Progress(int progress, string message)
        {
            ProgressEvent?.Invoke(progress, message);
        }

        private void Log(string message)
        {
            LogEvent?.Invoke(message);
        }
    }
}
