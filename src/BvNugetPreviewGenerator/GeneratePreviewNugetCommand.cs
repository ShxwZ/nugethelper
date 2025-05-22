using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.Reflection.Emit;
using System.Threading;
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
    /// Command handler
    /// </summary>
    internal sealed class GeneratePreviewNugetCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandIdSolution = 0x0100;
        public const int CommandIdProject = 0x0101;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("be00d81f-7240-4b92-aba6-995490d5063b");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratePreviewNugetCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GeneratePreviewNugetCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Comando para la solución
            var menuCommandID = new CommandID(CommandSet, CommandIdSolution);
            var menuItem = new MenuCommand(this.ExecuteSolution, menuCommandID);
            commandService.AddCommand(menuItem);

            // Comando para el proyecto
            var menuCommandIDProject = new CommandID(CommandSet, CommandIdProject);
            var menuItemProject = new MenuCommand(this.ExecuteProject, menuCommandIDProject);
            commandService.AddCommand(menuItemProject);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GeneratePreviewNugetCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private System.IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in Command1's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GeneratePreviewNugetCommand(package, commandService);
        }

        /// <summary>
        /// Executes the command for the solution.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExecuteSolution(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.DTE dte = (EnvDTE.DTE)ServiceProvider.GetService(typeof(EnvDTE.DTE));
            var solution = dte?.Solution;
            if (solution == null || solution.Projects == null)
            {
                MessageBox.Show("Indeterminated solution", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Procesa todos los proyectos de la solución
            var projectsPath = GetNugetProjectPaths(solution.Projects);
            ShowGenerateFormForProjects(projectsPath);
        }
        /// <summary>
        /// Executes the command for the project.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExecuteProject(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.DTE dte = (EnvDTE.DTE)ServiceProvider.GetService(typeof(EnvDTE.DTE));
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
        /// <summary>
        /// Gets the paths of all NuGet projects in the solution.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        private List<string> GetNugetProjectPaths(Projects projects)
        {
            var projectsPath = new List<string>();
            foreach (EnvDTE.Project project in projects)
            {
                AddProjectAndSubProjects(project, projectsPath);
            }
            return projectsPath;
        }

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
        /// Checks if the project is a NuGet project.
        /// </summary>
        /// <param name="projectFileName"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Shows the form to generate the NuGet package.
        /// </summary>
        /// <param name="projectsPath"></param>
        private void ShowGenerateFormForProjects(List<string> projectsPath)
        {
            var bvPreviewPackage = this.package as BvNugetPreviewGeneratorPackage;
            var generator = new PackageGenerator();

            if (projectsPath == null || projectsPath.Count == 0)
            {
                MessageBox.Show("Not found NuGets projects (with GeneratePackageOnBuild and Version).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var form = new GenerateForm(generator))
            {
                form.BuildConfiguration = bvPreviewPackage.BuildConfiguration;
                form.LocalRepoPath = bvPreviewPackage.DestinationNugetPreviewSource;
                form.ProjectPaths = projectsPath;
                form.Parallel = bvPreviewPackage.Parallel;
                form.ShowDialog();
            }
        }



    }
}
