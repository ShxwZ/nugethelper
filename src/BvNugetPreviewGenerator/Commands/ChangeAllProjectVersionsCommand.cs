using BvNugetPreviewGenerator.Services;
using BvNugetPreviewGenerator.Services.Interfaces;
using BvNugetPreviewGenerator.Generate;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator.Commands
{
    /// <summary>
    /// Command for updating all project versions in the solution
    /// </summary>
    internal sealed class ChangeAllProjectVersionsCommand : BaseCommand
    {
        /// <summary>
        /// Command ID constant for this command
        /// </summary>
        public const int CHANGE_VERSION_COMMAND_ID = 0x0103;

        /// <summary>
        /// Gets the command ID
        /// </summary>
        protected override int CommandId => CHANGE_VERSION_COMMAND_ID;

        /// <summary>
        /// Executes the command to change all project versions
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

            // Prompt user for new version
            string newVersion = packageGenerationService.PromptForVersion(
                "Change Project Versions", 
                "Enter the new version for all projects:");
            
            if (string.IsNullOrWhiteSpace(newVersion))
                return;

            ProgressForm progressForm = null;

            try
            {
                // Show progress form
                progressForm = new ProgressForm("Updating versions", "Updating version of all projects...");
                progressForm.Show();

                // Switch to background thread for the actual work
                await Task.Run(async () =>
                {
                    var fileSystemService = new FileSystemService();
                    var nugetService = new NuGetPackageService(fileSystemService);
                    var progressReporter = new SimpleProgressReporter();

                    string solutionDirectory = Path.GetDirectoryName(solution.FullName);
                    await nugetService.UpdateAllProjectVersionsAsync(
                        solutionDirectory,
                        newVersion,
                        progressReporter);
                });

                // Switch back to UI thread for result display
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                progressForm?.Close();
                MessageBox.Show($"All projects updated to version {newVersion}.",
                    "Update Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                progressForm?.Close();
                MessageBox.Show($"Error updating versions: {ex.Message}",
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