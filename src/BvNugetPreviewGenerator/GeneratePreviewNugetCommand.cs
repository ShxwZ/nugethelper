using BvNugetPreviewGenerator.Commands;
using BvNugetPreviewGenerator.Services.Interfaces;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator
{
    /// <summary>
    /// Command manager for NuGet package generation operations
    /// Coordinates all individual commands and provides unified initialization
    /// </summary>
    internal sealed class GeneratePreviewNugetCommand
    {
        /// <summary>
        /// Gets the singleton instance of the command manager
        /// </summary>
        public static GeneratePreviewNugetCommand Instance { get; private set; }

        /// <summary>
        /// Collection of all individual commands
        /// </summary>
        private readonly BaseCommand[] _commands;

        /// <summary>
        /// Initializes a new instance of the GeneratePreviewNugetCommand class
        /// </summary>
        private GeneratePreviewNugetCommand()
        {
            // Initialize all individual commands
            _commands = new BaseCommand[]
            {
                new GeneratePackagesForSolutionCommand(),
                new GeneratePackagesForProjectCommand(),
                new GeneratePackagesAsSolutionCommand(),
                new ChangeAllProjectVersionsCommand(),
                new DeleteSpecificVersionCommand()
            };
        }

        /// <summary>
        /// Initializes the singleton instance and all commands
        /// </summary>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand requires the UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            
            if (commandService == null)
            {
                throw new InvalidOperationException("Could not get OleMenuCommandService");
            }

            Instance = new GeneratePreviewNugetCommand();

            // Initialize all individual commands
            var bvPackage = package as BvNugetPreviewGeneratorPackage;
            if (bvPackage == null)
            {
                throw new InvalidOperationException("Package must be of type BvNugetPreviewGeneratorPackage");
            }

            foreach (var command in Instance._commands)
            {
                await command.InitializeAsync(bvPackage, commandService);
            }
        }
    }

    /// <summary>
    /// Simple implementation of IProgressReporter for command operations
    /// </summary>
    internal class SimpleProgressReporter : IProgressReporter
    {
        public void LogMessage(string message)
        {
            // Log to debug console for now - visible in Debug Output window
            System.Diagnostics.Debug.WriteLine($"[NuGet]: {message}");
            
            // Also try to write to console if available
            try
            {
                Console.WriteLine($"[NuGet]: {message}");
            }
            catch
            {
                // Ignore if console is not available
            }
        }

        public void ReportProgress(int percentage, string message)
        {
            var progressMessage = $"[NuGet Progress {percentage}%]: {message}";
            System.Diagnostics.Debug.WriteLine(progressMessage);
            
            try
            {
                Console.WriteLine(progressMessage);
            }
            catch
            {
                // Ignore if console is not available
            }
        }
    }
}