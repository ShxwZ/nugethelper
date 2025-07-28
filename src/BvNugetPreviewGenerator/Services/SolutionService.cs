using BvNugetPreviewGenerator.Generate;
using BvNugetPreviewGenerator.Services.Interfaces;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BvNugetPreviewGenerator.Services
{
    /// <summary>
    /// Service for managing Visual Studio solution and project operations
    /// </summary>
    public class SolutionService : ISolutionService
    {
        private readonly IServiceProvider _serviceProvider;

        public SolutionService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets the current DTE instance
        /// </summary>
        public EnvDTE.DTE GetDTE()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
        }

        /// <summary>
        /// Tries to get the active solution
        /// </summary>
        public bool TryGetSolution(out Solution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = GetDTE();
            solution = dte?.Solution;

            return solution != null && solution.Projects != null;
        }

        /// <summary>
        /// Gets all NuGet-enabled project paths in the solution
        /// </summary>
        public List<string> GetNugetProjectPaths()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetSolution(out Solution solution))
                return new List<string>();

            var projectPaths = new List<string>();
            foreach (EnvDTE.Project project in solution.Projects)
            {
                AddProjectAndSubProjects(project, projectPaths);
            }
            return projectPaths;
        }

        /// <summary>
        /// Gets NuGet project paths from selected items
        /// </summary>
        public List<string> GetSelectedNugetProjectPaths()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = GetDTE();
            if (dte == null) return new List<string>();

            var projectPaths = new List<string>();
            var selectedItems = dte.SelectedItems;

            foreach (SelectedItem item in selectedItems)
            {
                var project = item.Project;
                if (project != null && !string.IsNullOrEmpty(project.FileName))
                {
                    if (IsNugetProject(project.FileName))
                        projectPaths.Add(project.FileName);
                }
            }

            return projectPaths;
        }

        /// <summary>
        /// Gets the full path of the current solution
        /// </summary>
        public string GetSolutionPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (TryGetSolution(out Solution solution))
                return solution.FullName;

            return string.Empty;
        }

        /// <summary>
        /// Recursively adds a project and its sub-projects to the list
        /// </summary>
        private void AddProjectAndSubProjects(Project project, List<string> projectPaths)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (project == null) return;

            if (!string.IsNullOrEmpty(project.FileName) && IsNugetProject(project.FileName))
            {
                projectPaths.Add(project.FileName);
            }

            if (project.ProjectItems != null)
            {
                foreach (ProjectItem item in project.ProjectItems)
                {
                    var subProject = item.SubProject;
                    if (subProject != null)
                    {
                        AddProjectAndSubProjects(subProject, projectPaths);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a project is configured for NuGet package generation
        /// </summary>
        private bool IsNugetProject(string projectFileName)
        {
            if (!projectFileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var xml = System.Xml.Linq.XDocument.Load(projectFileName);
                var ns = xml.Root.GetDefaultNamespace();
                var version = xml.Root.Element(ns + "PropertyGroup")?.Element(ns + "Version")?.Value;
                bool.TryParse(xml.Root.Element(ns + "PropertyGroup")?.Element(ns + "GeneratePackageOnBuild")?.Value, out bool generatePackageOnBuild);
                return generatePackageOnBuild && !string.IsNullOrEmpty(version);
            }
            catch
            {
                return false;
            }
        }
    }
}