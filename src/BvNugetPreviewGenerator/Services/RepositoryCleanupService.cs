using BvNugetPreviewGenerator.Services.Interfaces;
using BvNugetPreviewGenerator.Generate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Services
{
    /// <summary>
    /// Service for repository cleanup operations following single responsibility principle
    /// </summary>
    public class RepositoryCleanupService : IRepositoryCleanupService
    {
        private readonly IFileSystemService _fileSystem;

        public RepositoryCleanupService(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Clears existing packages from local repository for a specific project
        /// </summary>
        public async Task ClearLocalRepositoryAsync(string localRepoPath, string projectName, IProgressReporter reporter)
        {
            if (!await _fileSystem.DirectoryExistsAsync(localRepoPath))
                return;

            try
            {
                var existingPackages = await _fileSystem.FindFilesAsync(
                    localRepoPath,
                    $"{projectName}*.nupkg",
                    false);

                int deletedCount = 0;
                foreach (var package in existingPackages)
                {
                    try
                    {
                        await _fileSystem.DeleteFileAsync(package);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Warning: Could not delete package {package}: {ex.Message}");
                    }
                }

                reporter.LogMessage($"Cleared {deletedCount} existing NuGet package(s) for {projectName} from local repository");
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Warning: Failed to clear local repository: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all packages from local repository
        /// </summary>
        public async Task ClearAllPackagesFromRepositoryAsync(string localRepoPath, IProgressReporter reporter)
        {
            if (!await _fileSystem.DirectoryExistsAsync(localRepoPath))
                return;

            try
            {
                var existingPackages = await _fileSystem.FindFilesAsync(localRepoPath, "*.nupkg", false);
                int deletedCount = 0;

                foreach (var package in existingPackages)
                {
                    try
                    {
                        await _fileSystem.DeleteFileAsync(package);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Warning: Could not delete package {package}: {ex.Message}");
                    }
                }

                reporter.LogMessage($"Cleared {deletedCount} NuGet packages from local repository");
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Warning: Failed to clear local repository: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up temporary files and directories
        /// </summary>
        public async Task CleanupTemporaryFilesAsync(string tempPath, IProgressReporter reporter)
        {
            if (string.IsNullOrEmpty(tempPath))
                return;

            try
            {
                if (await _fileSystem.DirectoryExistsAsync(tempPath))
                {
                    await _fileSystem.DeleteDirectoryAsync(tempPath, true);
                    reporter.LogMessage($"Deleted temp folder: {tempPath}");
                }
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error deleting temp folder: {ex.Message}");
            }
        }
    }
}