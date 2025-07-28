using BvNugetPreviewGenerator.Services;
using BvNugetPreviewGenerator.Services.Interfaces;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Commands
{
    /// <summary>
    /// Command for building entire solution and generating all packages
    /// </summary>
    internal sealed class GeneratePackagesAsSolutionCommand : BaseCommand
    {
        /// <summary>
        /// Command ID constant for this command
        /// </summary>
        public const int AS_SOLUTION_COMMAND_ID = 0x0102;

        /// <summary>
        /// Gets the command ID
        /// </summary>
        protected override int CommandId => AS_SOLUTION_COMMAND_ID;

        /// <summary>
        /// Executes the command to build solution and generate all packages
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

            packageGenerationService.ShowGenerateFormAsSolution();
        }
    }
}