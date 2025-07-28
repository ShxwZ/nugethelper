using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BvNugetPreviewGenerator.Generate;

namespace BvNugetPreviewGenerator.Services.Interfaces
{
    /// <summary>
    /// Configuration for package generation operations
    /// </summary>
    public class PackageGenerationConfig
    {
        public string ProjectPath { get; set; }
        public string LocalRepoPath { get; set; }
        public string BuildConfiguration { get; set; }
        public bool CleanBeforeBuild { get; set; }
        public bool ClearLocalRepoBeforeBuild { get; set; }
        public int MaxDegreeOfParallelism { get; set; } = 1;
    }

    /// <summary>
    /// Service for repository cleanup operations
    /// </summary>
    public interface IRepositoryCleanupService
    {
        /// <summary>
        /// Clears existing packages from local repository
        /// </summary>
        Task ClearLocalRepositoryAsync(string localRepoPath, string projectName, IProgressReporter reporter);

        /// <summary>
        /// Clears all packages from local repository
        /// </summary>
        Task ClearAllPackagesFromRepositoryAsync(string localRepoPath, IProgressReporter reporter);

        /// <summary>
        /// Cleans up temporary files and directories
        /// </summary>
        Task CleanupTemporaryFilesAsync(string tempPath, IProgressReporter reporter);
    }

    /// <summary>
    /// Service for package discovery operations
    /// </summary>
    public interface IPackageDiscoveryService
    {
        /// <summary>
        /// Finds generated packages in project bin folder
        /// </summary>
        Task<IEnumerable<string>> FindGeneratedPackagesAsync(
            string solutionDirectory, 
            string buildConfiguration, 
            Dictionary<string, string> projectVersions,
            IProgressReporter reporter);

        /// <summary>
        /// Locates a specific package in bin folder
        /// </summary>
        Task<string> FindProjectPackageAsync(
            string projectPath, 
            string buildConfiguration, 
            string version,
            IProgressReporter reporter);
    }

    /// <summary>
    /// Service for coordinating package generation workflow
    /// </summary>
    public interface IPackageWorkflowService
    {
        /// <summary>
        /// Executes the complete single project package generation workflow
        /// </summary>
        Task<PackageGenerateResult> ExecuteSingleProjectWorkflowAsync(
            PackageGenerationConfig config,
            IProgressReporter reporter,
            CancellationToken cancellationToken);

        /// <summary>
        /// Executes the solution build and package discovery workflow
        /// </summary>
        Task<PackageGenerateResult> ExecuteSolutionWorkflowAsync(
            string solutionPath,
            string buildConfiguration,
            string localRepoPath,
            bool cleanBeforeBuild,
            bool clearLocalRepoBeforeBuild,
            int maxDegreeOfParallelism,
            IProgressReporter reporter,
            CancellationToken cancellationToken);
    }
}