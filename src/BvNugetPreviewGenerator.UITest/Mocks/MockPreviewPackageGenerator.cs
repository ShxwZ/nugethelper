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

        private PreviewPackageGenerateResultType _ResultType;
        private Exception _ex;

        public MockPreviewPackageGenerator(PreviewPackageGenerateResultType resulttype, Exception ex)
        {
            _ResultType = resulttype;
            _ex = ex;
        }

        public async Task GeneratePackageAsync(IEnumerable<string> projectPaths, string localRepoPath, string buildConfiguration)
        {
            int total = projectPaths.Count();
            int current = 0;

            foreach (var projectPath in projectPaths)
            {
                current++;
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
                CompleteEvent?.Invoke(result);
            }
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
