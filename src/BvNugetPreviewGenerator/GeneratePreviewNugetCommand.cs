using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using BvNugetPreviewGenerator.Generate;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace BvNugetPreviewGenerator
{
    /// <summary>
    /// Command handler for NuGet package generation operations
    /// </summary>
    internal sealed class GeneratePreviewNugetCommand
    {
        #region Command IDs and Configuration

        /// <summary>
        /// Command IDs for menu items
        /// </summary>
        private static class CommandIds
        {
            public const int Solution = 0x0100;
            public const int Project = 0x0101;
            public const int AsSolution = 0x0102;
        }

        /// <summary>
        /// Command menu group (command set GUID)
        /// </summary>
        public static readonly Guid CommandSet = new Guid("be00d81f-7240-4b92-aba6-995490d5063b");

        #endregion

        /// <summary>
        /// VS Package that provides this command
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Gets the singleton instance of the command
        /// </summary>
        public static GeneratePreviewNugetCommand Instance { get; private set; }

        /// <summary>
        /// Gets the service provider from the owner package
        /// </summary>
        private System.IServiceProvider ServiceProvider => this.package;

        /// <summary>
        /// Initializes a new instance of the GeneratePreviewNugetCommand class
        /// </summary>
        /// <param name="package">Owner package, not null</param>
        /// <param name="commandService">Command service to add command to, not null</param>
        private GeneratePreviewNugetCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Register command handlers
            RegisterCommand(commandService, CommandIds.Solution, ExecuteSolution);
            RegisterCommand(commandService, CommandIds.Project, ExecuteProject);
            RegisterCommand(commandService, CommandIds.AsSolution, ExecuteAsSolution);
        }

        /// <summary>
        /// Registers a command with the command service
        /// </summary>
        private void RegisterCommand(OleMenuCommandService commandService, int commandId, EventHandler handler)
        {
            var commandID = new CommandID(CommandSet, commandId);
            var menuItem = new MenuCommand(handler, commandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Initializes the singleton instance of the command
        /// </summary>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand requires the UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GeneratePreviewNugetCommand(package, commandService);
        }

        #region Command Execution Methods

        /// <summary>
        /// Executes the command for the solution - processes all projects
        /// </summary>
        private void ExecuteSolution(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetSolution(out Solution solution))
                return;

            // Process all NuGet projects in the solution
            var projectsPath = GetNugetProjectPaths(solution.Projects);
            ShowGenerateFormForProjects(projectsPath);
        }

        /// <summary>
        /// Executes the command to build the entire solution as a unit
        /// </summary>
        private void ExecuteAsSolution(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetSolution(out _))
                return;

            ShowGenerateFormAsSolution();
        }

        /// <summary>
        /// Executes the command for selected projects
        /// </summary>
        private void ExecuteProject(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE.DTE dte = GetDTE();
            if (dte == null)
                return;

            var selectedItems = dte.SelectedItems;
            var projectsPath = new List<string>();

            foreach (SelectedItem item in selectedItems)
            {
                var project = item.Project;
                if (project != null && !string.IsNullOrEmpty(project.FileName))
                {
                    if (IsNugetProject(project.FileName))
                        projectsPath.Add(project.FileName);
                }
            }

            ShowGenerateFormForProjects(projectsPath);
        }

        #endregion

        #region Solution and Project Processing

        /// <summary>
        /// Gets the DTE service
        /// </summary>
        private EnvDTE.DTE GetDTE()
        {
            return (EnvDTE.DTE)ServiceProvider.GetService(typeof(EnvDTE.DTE));
        }

        /// <summary>
        /// Tries to get the active solution
        /// </summary>
        /// <param name="solution">The solution if found, otherwise null</param>
        /// <returns>True if solution was found, otherwise false</returns>
        private bool TryGetSolution(out Solution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE.DTE dte = GetDTE();
            solution = dte?.Solution;

            if (solution == null || solution.Projects == null)
            {
                MessageBox.Show("Undetermined solution", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the paths of all NuGet projects in the solution
        /// </summary>
        private List<string> GetNugetProjectPaths(Projects projects)
        {
            var projectsPath = new List<string>();
            foreach (EnvDTE.Project project in projects)
            {
                AddProjectAndSubProjects(project, projectsPath);
            }
            return projectsPath;
        }

        /// <summary>
        /// Recursively adds a project and its sub-projects to the list
        /// </summary>
        private void AddProjectAndSubProjects(Project project, List<string> projectsPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (project == null)
                return;

            if (!string.IsNullOrEmpty(project.FileName) && IsNugetProject(project.FileName))
            {
                projectsPath.Add(project.FileName);
            }

            if (project.ProjectItems != null)
            {
                foreach (ProjectItem item in project.ProjectItems)
                {
                    var subProject = item.SubProject;
                    if (subProject != null)
                    {
                        AddProjectAndSubProjects(subProject, projectsPath);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the project is a NuGet project
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

        #endregion

        #region UI Form Management

        /// <summary>
        /// Shows the form to generate NuGet packages for specific projects
        /// </summary>
        private void ShowGenerateFormForProjects(List<string> projectsPath)
        {
            var bvPreviewPackage = this.package as BvNugetPreviewGeneratorPackage;

            if (projectsPath == null || projectsPath.Count == 0)
            {
                MessageBox.Show("No NuGet projects found (with GeneratePackageOnBuild and Version).",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var generator = new PackageGenerator();

            using (var form = new GenerateForm(generator))
            {
                ConfigureGenerateForm(form, bvPreviewPackage);
                form.ProjectPaths = projectsPath;
                form.AsSolution = false;
                form.ShowDialog();
            }
        }

        /// <summary>
        /// Shows the form to generate NuGet packages for the entire solution
        /// </summary>
        private void ShowGenerateFormAsSolution()
        {
            var bvPreviewPackage = this.package as BvNugetPreviewGeneratorPackage;

            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.DTE dte = GetDTE();
            string solutionPath = dte?.Solution?.FullName;

            if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
            {
                MessageBox.Show("Active solution not found.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var generator = new PackageGenerator();

            using (var form = new GenerateForm(generator))
            {
                ConfigureGenerateForm(form, bvPreviewPackage);
                form.SolutionPath = solutionPath;
                form.AsSolution = true;
                form.ShowDialog();
            }
        }

        /// <summary>
        /// Configures common settings for the Generate Form
        /// </summary>
        private void ConfigureGenerateForm(GenerateForm form, BvNugetPreviewGeneratorPackage packageSettings)
        {
            form.BuildConfiguration = packageSettings.BuildConfiguration;
            form.LocalRepoPath = packageSettings.DestinationNugetPreviewSource;
            form.Parallel = packageSettings.Parallel;
            form.MaxDegreeOfParallelism = packageSettings.MaxDegreeOfParallelism;
            form.ClearLocalRepositoryBeforeBuild = packageSettings.ClearLocalRepositoryBeforeBuild;
            form.CleanBeforeBuild = packageSettings.CleanBeforeBuild;
        }

        #endregion
    }
}