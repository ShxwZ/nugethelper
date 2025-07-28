using BvNugetPreviewGenerator.Generate;
using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator.Services
{
    /// <summary>
    /// Service for managing NuGet package generation UI operations
    /// </summary>
    public class PackageGenerationService : IPackageGenerationService
    {
        private readonly BvNugetPreviewGeneratorPackage _package;

        public PackageGenerationService(BvNugetPreviewGeneratorPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        /// <summary>
        /// Shows the generate form for specific projects
        /// </summary>
        public void ShowGenerateFormForProjects(List<string> projectPaths)
        {
            if (projectPaths == null || projectPaths.Count == 0)
            {
                MessageBox.Show("No NuGet projects found (with GeneratePackageOnBuild and Version).",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var generator = new PackageGenerator();

            using (var form = new GenerateForm(generator))
            {
                ConfigureGenerateForm(form);
                form.ProjectPaths = projectPaths;
                form.AsSolution = false;
                form.ShowDialog();
            }
        }

        /// <summary>
        /// Shows the generate form for the entire solution
        /// </summary>
        public void ShowGenerateFormAsSolution()
        {
            var solutionService = new SolutionService(_package);
            string solutionPath = solutionService.GetSolutionPath();

            if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
            {
                MessageBox.Show("Active solution not found.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var generator = new PackageGenerator();

            using (var form = new GenerateForm(generator))
            {
                ConfigureGenerateForm(form);
                form.SolutionPath = solutionPath;
                form.AsSolution = true;
                form.ShowDialog();
            }
        }

        /// <summary>
        /// Prompts user for version input
        /// </summary>
        public string PromptForVersion(string title, string prompt)
        {
            using (var dialog = new VersionInputDialog())
            {
                dialog.Text = title;
                dialog.PromptText = prompt;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.Version;
                }
            }

            return null;
        }

        /// <summary>
        /// Configures common settings for the Generate Form
        /// </summary>
        private void ConfigureGenerateForm(GenerateForm form)
        {
            form.BuildConfiguration = _package.BuildConfiguration;
            form.LocalRepoPath = _package.DestinationNugetPreviewSource;
            form.Parallel = _package.Parallel;
            form.MaxDegreeOfParallelism = _package.MaxDegreeOfParallelism;
            form.ClearLocalRepositoryBeforeBuild = _package.ClearLocalRepositoryBeforeBuild;
            form.CleanBeforeBuild = _package.CleanBeforeBuild;
        }
    }
}