using BvNugetPreviewGenerator.Services;
using BvNugetPreviewGenerator.Services.Interfaces;
using BvNugetPreviewGenerator.Generate;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator.Commands
{
    /// <summary>
    /// Command for deleting specific version packages from cache
    /// </summary>
    internal sealed class DeleteSpecificVersionCommand : BaseCommand
    {
        /// <summary>
        /// Command ID constant for this command
        /// </summary>
        public const int DELETE_VERSION_COMMAND_ID = 0x0104;

        /// <summary>
        /// Gets the command ID
        /// </summary>
        protected override int CommandId => DELETE_VERSION_COMMAND_ID;

        /// <summary>
        /// Executes the command to delete specific version packages
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionService = new SolutionService(ServiceProvider);
            var packageGenerationService = new PackageGenerationService(Package);

            if (!solutionService.TryGetSolution(out var solution))
            {
                MessageBox.Show("Undetermined solution", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Prompt user for version to delete
            string version = packageGenerationService.PromptForVersion(
                "Delete NuGet Packages", 
                "Enter the version of packages to delete:");
            
            if (string.IsNullOrWhiteSpace(version))
                return;

            ProgressForm progressForm = null;

            try
            {
                // Show progress form
                progressForm = new ProgressForm("Deleting NuGet packages", "Deleting packages of the specified version...");
                progressForm.Show();

                // Switch to background thread for the actual work
                await Task.Run(async () =>
                {
                    var fileSystemService = new FileSystemService();
                    var nugetService = new NuGetPackageService(fileSystemService);
                    var progressReporter = new SimpleProgressReporter();

                    await nugetService.DeleteSpecificVersionPackagesAsync(
                        solution.FullName,
                        version,
                        progressReporter);
                });

                // Switch back to UI thread for result display
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                progressForm?.Close();
                MessageBox.Show($"NuGet packages with version {version} deleted successfully.",
                    "Deletion Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                progressForm?.Close();
                MessageBox.Show($"Error deleting packages: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                progressForm?.Dispose();
            }
        }
    }
}