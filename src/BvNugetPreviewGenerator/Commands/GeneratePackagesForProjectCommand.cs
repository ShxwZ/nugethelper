using BvNugetPreviewGenerator.Services;
using BvNugetPreviewGenerator.Services.Interfaces;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Commands
{
    /// <summary>
    /// Command for generating NuGet packages for selected projects
    /// </summary>
    internal sealed class GeneratePackagesForProjectCommand : BaseCommand
    {
        /// <summary>
        /// Command ID constant for this command
        /// </summary>
        public const int PROJECT_COMMAND_ID = 0x0101;

        /// <summary>
        /// Gets the command ID
        /// </summary>
        protected override int CommandId => PROJECT_COMMAND_ID;

        /// <summary>
        /// Executes the command to generate packages for selected projects
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionService = new SolutionService(ServiceProvider);
            var packageGenerationService = new PackageGenerationService(Package);

            // Get selected NuGet projects
            var projectPaths = solutionService.GetSelectedNugetProjectPaths();
            packageGenerationService.ShowGenerateFormForProjects(projectPaths);
        }
    }
}