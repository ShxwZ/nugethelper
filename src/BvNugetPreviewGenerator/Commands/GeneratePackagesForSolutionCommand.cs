using BvNugetPreviewGenerator.Services;
using BvNugetPreviewGenerator.Services.Interfaces;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Commands
{
    /// <summary>
    /// Command for generating NuGet packages for all projects in solution
    /// </summary>
    internal sealed class GeneratePackagesForSolutionCommand : BaseCommand
    {
        /// <summary>
        /// Command ID constant for this command
        /// </summary>
        public const int SOLUTION_COMMAND_ID = 0x0100;

        /// <summary>
        /// Gets the command ID
        /// </summary>
        protected override int CommandId => SOLUTION_COMMAND_ID;

        /// <summary>
        /// Executes the command to generate packages for all solution projects
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionService = new SolutionService(ServiceProvider);
            var packageGenerationService = new PackageGenerationService(Package);

            if (!solutionService.TryGetSolution(out var solution))
            {
                System.Windows.Forms.MessageBox.Show("Undetermined solution", "Error", 
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // Get all NuGet projects in the solution
            var projectPaths = solutionService.GetNugetProjectPaths();
            packageGenerationService.ShowGenerateFormForProjects(projectPaths);
        }
    }
}