using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public interface IDotNetBuildService
    {
        Task<string> RestorePackagesAsync(string projectOrSolutionPath, IProgressReporter reporter, CancellationToken cancellationToken = default);
        Task<string> CleanAsync(string projectOrSolutionPath, string buildConfiguration, IProgressReporter reporter, CancellationToken cancellationToken = default);
        Task<string> BuildAsync(string projectOrSolutionPath, string buildConfiguration, string outputFolder, int parallelism, IProgressReporter reporter, CancellationToken cancellationToken = default);
    }
}
