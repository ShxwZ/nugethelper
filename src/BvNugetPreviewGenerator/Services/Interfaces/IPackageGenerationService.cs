using BvNugetPreviewGenerator.Generate;
using System.Collections.Generic;

namespace BvNugetPreviewGenerator.Services.Interfaces
{
    /// <summary>
    /// Interface for NuGet package generation form operations
    /// </summary>
    public interface IPackageGenerationService
    {
        /// <summary>
        /// Shows the generate form for specific projects
        /// </summary>
        void ShowGenerateFormForProjects(List<string> projectPaths);

        /// <summary>
        /// Shows the generate form for the entire solution
        /// </summary>
        void ShowGenerateFormAsSolution();

        /// <summary>
        /// Prompts user for version input
        /// </summary>
        string PromptForVersion(string title, string prompt);
    }
}