using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public interface IPackageGenerator
    {
        event Action<string> LogEvent;
        event Action<int, string> ProgressEvent;
        event Action<PackageGenerateResult> CompleteEvent;
        event Action<string> PackagesLeft;
        Task GeneratePackageAsync(IEnumerable<string> projectPaths, string localRepoPath, string buildConfiguration, bool parallel = true,
            int maxDegreeOfParallelism = 4);
        void Cancel();
    }
}
