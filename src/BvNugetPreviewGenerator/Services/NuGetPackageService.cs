using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

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
                    reporter.LogMessage($"Deleted package folder: {versionFolder}");
                }
                else
                {
                    reporter.LogMessage($"Package {packageId} {version} not found in global cache or local folder.");
                }
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error deleting NuGet package: {ex.Message}");
            }
        }

        public string GetPackageFileName(string projectName, string version)
        {
            return $"{projectName}.{version}.nupkg";
        }

        public async Task<Dictionary<string, string>> GetProjectVersionsAsync(string solutionDirectory, IProgressReporter reporter, CancellationToken cancellationToken = default)
        {
            var projectVersions = new Dictionary<string, string>();

            reporter.LogMessage("Analyzing projects to determine versions...");

            var projectFiles = await _fileSystem.FindFilesAsync(solutionDirectory, "*.csproj", true);
            foreach (var projectFile in projectFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var projectName = Path.GetFileNameWithoutExtension(projectFile);
                    var projectContent = await _fileSystem.ReadAllTextAsync(projectFile, cancellationToken);

                    var doc = new XmlDocument();
                    doc.LoadXml(projectContent);
                    var versionNode = doc.SelectSingleNode("/Project/PropertyGroup/Version");

                    if (versionNode != null && PackageVersion.TryParse(versionNode.InnerText, out var version))
                    {
                        projectVersions[projectName] = version.ToString();
                        reporter.LogMessage($"Project {projectName}: version {version}");
                    }
                }
                catch (Exception ex)
                {
                    reporter.LogMessage($"Error getting version from project {projectFile}: {ex.Message}");
                }
            }

            return projectVersions;
        }

        public async Task UpdateProjectVersionAsync(string projectPath, string newVersion, IProgressReporter reporter, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var projectContent = await _fileSystem.ReadAllTextAsync(projectPath, cancellationToken);
                string oldVersion = "";
                bool versionUpdated = false;

                // First try to preserve original formatting - this is the preferred approach
                reporter.LogMessage($"Updating version for {Path.GetFileNameWithoutExtension(projectPath)} while preserving original format...");
                
                if (TryUpdateVersionPreservingFormat(projectContent, newVersion, out var updatedContent, out oldVersion))
                {
                    await _fileSystem.WriteAllTextAsync(projectPath, updatedContent, cancellationToken);
                    reporter.LogMessage($"Updated {Path.GetFileNameWithoutExtension(projectPath)} version from {oldVersion} to {newVersion} (format preserved)");
                    versionUpdated = true;
                }
                else
                {
                    // Fallback: Use XML formatting only if format-preserving approach fails
                    reporter.LogMessage("Format-preserving approach failed, trying XML formatting as fallback...");
                    if (await TryUpdateVersionWithProperFormatting(projectPath, newVersion, reporter, cancellationToken))
                    {
                        versionUpdated = true;
                    }
                }

                if (!versionUpdated)
                {
                    reporter.LogMessage($"No version node found in project {Path.GetFileNameWithoutExtension(projectPath)}");
                }
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error updating project version: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Attempts to update version while preserving original formatting completely
        /// </summary>
        private bool TryUpdateVersionPreservingFormat(string projectContent, string newVersion, out string updatedContent, out string oldVersion)
        {
            updatedContent = null;
            oldVersion = "";

            // Method 1: Try regex approach first for most precise replacement
            var versionPattern = @"(<Version\s*>)([^<]+)(<\/Version>)";
            var regex = new Regex(versionPattern, RegexOptions.IgnoreCase);
            
            var match = regex.Match(projectContent);
            if (match.Success)
            {
                oldVersion = match.Groups[2].Value.Trim();
                updatedContent = regex.Replace(projectContent, $"${{1}}{newVersion}${{3}}");
                return true;
            }

            // Method 2: Try line-by-line approach for cases where regex might miss edge cases
            var lines = projectContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.Trim();
                
                if (trimmedLine.StartsWith("<Version>") && trimmedLine.EndsWith("</Version>"))
                {
                    // Extract indentation and old version
                    var indentationEndIndex = line.IndexOf('<');
                    var indentation = indentationEndIndex >= 0 ? line.Substring(0, indentationEndIndex) : "";
                    
                    var startTag = "<Version>";
                    var endTag = "</Version>";
                    var startIndex = trimmedLine.IndexOf(startTag) + startTag.Length;
                    var endIndex = trimmedLine.IndexOf(endTag);
                    oldVersion = trimmedLine.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // Replace with new version maintaining exact indentation
                    lines[i] = $"{indentation}<Version>{newVersion}</Version>";
                    updatedContent = string.Join(Environment.NewLine, lines);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Forces proper XML formatting when updating version
        /// </summary>
        private async Task<bool> TryUpdateVersionWithProperFormatting(string projectPath, string newVersion, IProgressReporter reporter, CancellationToken cancellationToken)
        {
            try
            {
                var projectContent = await _fileSystem.ReadAllTextAsync(projectPath, cancellationToken);
                
                // Log original content characteristics for debugging
                var originalLines = projectContent.Split('\n').Length;
                var isMultiLine = originalLines > 3;
                reporter.LogMessage($"Original file has {originalLines} lines, multiline: {isMultiLine}");
                
                var doc = new XmlDocument();
                doc.LoadXml(projectContent);
                var versionNode = doc.SelectSingleNode("/Project/PropertyGroup/Version");

                if (versionNode != null)
                {
                    var oldVersion = versionNode.InnerText;
                    versionNode.InnerText = newVersion;

                    // Force proper formatting with XmlWriterSettings
                    var formattedXml = FormatXmlDocument(doc);
                    
                    // Log formatted content characteristics for debugging
                    var formattedLines = formattedXml.Split('\n').Length;
                    reporter.LogMessage($"Formatted file will have {formattedLines} lines");
                    
                    // Always write the formatted version
                    await _fileSystem.WriteAllTextAsync(projectPath, formattedXml, cancellationToken);
                    reporter.LogMessage($"Updated {Path.GetFileNameWithoutExtension(projectPath)} version from {oldVersion} to {newVersion} (XML formatting applied)");
                    return true;
                }
                else
                {
                    reporter.LogMessage("Version node not found in XML document");
                }
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error in XML formatting approach: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Formats an XmlDocument with consistent, professional formatting optimized for .csproj files
        /// </summary>
        private string FormatXmlDocument(XmlDocument doc)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ", // 2 spaces for clean indentation (MSBuild standard)
                NewLineChars = Environment.NewLine,
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = true, // .csproj files don't need XML declaration
                Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                ConformanceLevel = ConformanceLevel.Document,
                CloseOutput = false
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
            {
                // Force proper formatting by writing each node properly
                doc.Save(writer);
            }

            var result = sb.ToString();
            
            // Clean up any potential formatting issues
            result = CleanupMSBuildFormatting(result);
            
            // Ensure final newline for consistency with VS standards
            if (!result.EndsWith(Environment.NewLine))
            {
                result += Environment.NewLine;
            }

            return result;
        }

        /// <summary>
        /// Applies MSBuild-specific formatting cleanup
        /// </summary>
        private string CleanupMSBuildFormatting(string xmlContent)
        {
            // Remove any empty lines between elements for cleaner look
            var lines = xmlContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanedLines = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip completely empty lines between XML elements
                if (string.IsNullOrWhiteSpace(trimmedLine) && 
                    cleanedLines.Count > 0 && 
                    cleanedLines.Last().Trim().StartsWith("<"))
                {
                    continue;
                }
                
                cleanedLines.Add(line);
            }
            
            return string.Join(Environment.NewLine, cleanedLines);
        }

        public async Task UpdateAllProjectVersionsAsync(string solutionDirectory, string newVersion, IProgressReporter reporter, CancellationToken cancellationToken = default)
        {
            try
            {
                var projectFiles = await _fileSystem.FindFilesAsync(solutionDirectory, "*.csproj", true);
                int updatedCount = 0;

                foreach (var projectFile in projectFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await UpdateProjectVersionAsync(projectFile, newVersion, reporter, cancellationToken);
                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Failed to update {projectFile}: {ex.Message}");
                    }
                }

                reporter.LogMessage($"Updated version to {newVersion} in {updatedCount} projects");
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error updating all project versions: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteSpecificVersionPackagesAsync(string solutionPath, string version, IProgressReporter reporter, CancellationToken cancellationToken = default)
        {
            try
            {
                var solutionDirectory = Path.GetDirectoryName(solutionPath);

                // Get all project versions first
                reporter.LogMessage("Getting project versions to find packages to delete...");
                var projectVersions = await GetProjectVersionsAsync(solutionDirectory, reporter, cancellationToken);

                if (!projectVersions.Any())
                {
                    reporter.LogMessage("No projects with versions found in solution.");
                    return;
                }


                int deletedCount = 0;

                // Delete installed packages from cache for matching projects
                foreach (var project in projectVersions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var packageId = project.Key;
                        var packageVersion = version;

                        // Delete from global NuGet cache
                        await DeleteInstalledPackageAsync(packageId, packageVersion, reporter, cancellationToken);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        reporter.LogMessage($"Failed to delete installed package {project.Key} v{project.Value}: {ex.Message}");
                    }
                }

                reporter.LogMessage($"Deleted {deletedCount} installed NuGet packages with version {version} from cache");
            }
            catch (Exception ex)
            {
                reporter.LogMessage($"Error deleting specific version packages: {ex.Message}");
                throw;
            }
        }
    }
}