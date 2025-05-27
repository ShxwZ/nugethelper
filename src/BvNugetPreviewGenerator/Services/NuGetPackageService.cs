using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace BvNugetPreviewGenerator.Generate
{
    public class NuGetPackageService : INuGetPackageService
    {
        private readonly IFileSystemService _fileSystem;

        public NuGetPackageService(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public async Task CopyPackageToRepoAsync(string sourcePath, string destPath, IProgressReporter reporter)
        {
            if (!await _fileSystem.FileExistsAsync(sourcePath))
            {
                reporter.LogMessage($"NuGet package not found at {sourcePath}");
                throw new PackageGenerateException("NuGet package not found after build.");
            }

            await _fileSystem.CopyFileAsync(sourcePath, destPath, true);
            reporter.LogMessage($"Copied NuGet package to {destPath}");
        }

        public async Task DeleteInstalledPackageAsync(
            string packageId,
            string version,
            IProgressReporter reporter,
            CancellationToken cancellationToken = default,
            string customPackagesPath = null)
        {
            try
            {
                string versionFolder = null;

                if (!string.IsNullOrEmpty(customPackagesPath))
                {
                    string localPackagesPath = Path.Combine(customPackagesPath, packageId);
                    if (await _fileSystem.DirectoryExistsAsync(localPackagesPath))
                    {
                        versionFolder = localPackagesPath;
                    }
                }
                else
                {
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string globalPackagesPath = Path.Combine(userProfile, ".nuget", "packages", packageId.ToLowerInvariant(), version);
                    if (await _fileSystem.DirectoryExistsAsync(globalPackagesPath))
                    {
                        versionFolder = globalPackagesPath;
                    }
                }

                if (versionFolder != null)
                {
                    await _fileSystem.DeleteDirectoryAsync(versionFolder, true);
                    reporter.LogMessage($"Eliminada la carpeta del paquete: {versionFolder}");
                }
                else
                {
                    reporter.LogMessage($"No se encontró el paquete {packageId} {version} ni en caché global ni en carpeta local.");
                }
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error al eliminar el paquete NuGet: {ex.Message}");
            }
        }

        public string GetPackageFileName(string projectName, string version)
        {
            return $"{projectName}.{version}.nupkg";
        }

        public (string PackageId, string Version) ParsePackageFileName(string fileName)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            int firstDotIndex = fileNameWithoutExtension.IndexOf('.');
            if (firstDotIndex < 0)
                throw new ArgumentException("Invalid NuGet package filename format", nameof(fileName));

            string packageId = fileNameWithoutExtension.Substring(0, firstDotIndex);
            string version = fileNameWithoutExtension.Substring(firstDotIndex + 1);

            return (packageId, version);
        }
    }
}
