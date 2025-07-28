using EnvDTE;
using System.Collections.Generic;

namespace BvNugetPreviewGenerator.Services.Interfaces
{
    /// <summary>
    /// Interface for Visual Studio solution and project operations
    /// </summary>
    public interface ISolutionService
    {
        /// <summary>
        /// Gets the current DTE instance
        /// </summary>
        EnvDTE.DTE GetDTE();

        /// <summary>
        /// Tries to get the active solution
        /// </summary>
        bool TryGetSolution(out Solution solution);

        /// <summary>
        /// Gets all NuGet-enabled project paths in the solution
        /// </summary>
        List<string> GetNugetProjectPaths();

        /// <summary>
        /// Gets NuGet project paths from selected items
        /// </summary>
        List<string> GetSelectedNugetProjectPaths();

        /// <summary>
        /// Gets the full path of the current solution
        /// </summary>
        string GetSolutionPath();
    }
}