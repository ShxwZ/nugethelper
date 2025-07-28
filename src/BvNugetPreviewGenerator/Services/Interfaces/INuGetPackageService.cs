using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public interface INuGetPackageService
    {
        Task CopyPackageToRepoAsync(string sourcePath, string destPath, IProgressReporter reporter);
        Task DeleteInstalledPackageAsync(string packageId, string version, IProgressReporter reporter, CancellationToken cancellationToken = default, string customPackagesPath = null);
        string GetPackageFileName(string projectName, string version);
        Task<Dictionary<string, string>> GetProjectVersionsAsync(string solutionDirectory, IProgressReporter reporter, CancellationToken cancellationToken = default);
        Task UpdateProjectVersionAsync(string projectPath, string newVersion, IProgressReporter reporter, CancellationToken cancellationToken = default);
        Task UpdateAllProjectVersionsAsync(string solutionDirectory, string newVersion, IProgressReporter reporter, CancellationToken cancellationToken = default);
        Task DeleteSpecificVersionPackagesAsync(string localRepoPath, string version, IProgressReporter reporter, CancellationToken cancellationToken = default);
    }
}
