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
    /// Service for package discovery operations following single responsibility principle
    /// </summary>
    public class PackageDiscoveryService : IPackageDiscoveryService
    {
        private readonly IFileSystemService _fileSystem;

        public PackageDiscoveryService(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Finds generated packages in solution bin folders
        /// </summary>
        public async Task<IEnumerable<string>> FindGeneratedPackagesAsync(
            string solutionDirectory, 
            string buildConfiguration, 
            Dictionary<string, string> projectVersions,
            IProgressReporter reporter)
        {
            reporter.LogMessage("Searching for NuGet packages in project bin folders");
            
            var foundPackages = new List<string>();
            var allFiles = await _fileSystem.FindFilesAsync(solutionDirectory, "*.nupkg", true);

            foreach (var file in allFiles)
            {
                if (IsValidPackageForBuildConfiguration(file, buildConfiguration) &&
                    MatchesKnownProjectVersion(file, projectVersions, reporter))
                {
                    foundPackages.Add(file);
                }
            }

            reporter.LogMessage($"Found {foundPackages.Count} newly generated NuGet packages");
            return foundPackages.Distinct();
        }

        /// <summary>
        /// Locates a specific package in project bin folder
        /// </summary>
        public async Task<string> FindProjectPackageAsync(
            string projectPath, 
            string buildConfiguration, 
            string version,
            IProgressReporter reporter)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectDirectory = Path.GetDirectoryName(projectPath);
            var binPath = Path.Combine(projectDirectory, "bin", buildConfiguration);

            var files = await _fileSystem.FindFilesAsync(binPath, $"{projectName}*.{version}.nupkg", true);

            if (files.Any())
            {
                var sourcePath = files.First();
                reporter.LogMessage($"Found package in bin folder: {sourcePath}");
                return sourcePath;
            }

            throw new PackageGenerateException(
                $"NuGet package {projectName}.{version}.nupkg not found in bin folder. " +
                "Ensure the project is built and the package is generated correctly.");
        }

        /// <summary>
        /// Checks if package is in correct bin folder for build configuration
        /// </summary>
        private bool IsValidPackageForBuildConfiguration(string filePath, string buildConfiguration)
        {
            var lowerPath = filePath.ToLower();
            var lowerConfig = buildConfiguration.ToLower();

            return lowerPath.Contains($"\\bin\\{lowerConfig}\\") ||
                   (lowerPath.Contains("\\bin\\") && lowerPath.Contains($"\\{lowerConfig}\\"));
        }

        /// <summary>
        /// Checks if package matches known project versions
        /// </summary>
        private bool MatchesKnownProjectVersion(string filePath, Dictionary<string, string> projectVersions, IProgressReporter reporter)
        {
            var fileName = Path.GetFileName(filePath);

            foreach (var projectVersion in projectVersions)
            {
                if (fileName.StartsWith(projectVersion.Key, StringComparison.OrdinalIgnoreCase) &&
                    fileName.Contains(projectVersion.Value))
                {
                    reporter.LogMessage($"Found NuGet package for {projectVersion.Key} v{projectVersion.Value}: {fileName}");
                    return true;
                }
            }

            // Log ignored packages for transparency
            if (IsValidPackageForBuildConfiguration(filePath, ""))
            {
                reporter.LogMessage($"Ignoring package that doesn't match known versions: {fileName}");
            }

            return false;
        }
    }
}