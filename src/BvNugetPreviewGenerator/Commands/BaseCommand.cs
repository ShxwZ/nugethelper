using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace BvNugetPreviewGenerator.Commands
{
    /// <summary>
    /// Base class for all package generation commands
    /// </summary>
    public abstract class BaseCommand
    {
        /// <summary>
        /// Command set GUID for all package generation commands
        /// </summary>
        public static readonly Guid CommandSet = new Guid("be00d81f-7240-4b92-aba6-995490d5063b");

        /// <summary>
        /// Gets the command ID
        /// </summary>
        protected abstract int CommandId { get; }

        /// <summary>
        /// Gets the package instance
        /// </summary>
        protected BvNugetPreviewGeneratorPackage Package { get; private set; }

        /// <summary>
        /// Gets the service provider
        /// </summary>
        protected System.IServiceProvider ServiceProvider => Package;

        /// <summary>
        /// Initializes the command
        /// </summary>
        public virtual async Task InitializeAsync(BvNugetPreviewGeneratorPackage package, OleMenuCommandService commandService)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var commandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, commandID);
            commandService.AddCommand(menuItem);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        private void Execute(object sender, EventArgs e)
        {
            // Execute on UI thread but allow async operations
            var task = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    HandleError(ex);
                }
            });

            // Continue with error handling if task fails
            _ = task.Task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Command execution failed: {t.Exception}");
                }
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// Executes the command asynchronously
        /// </summary>
        protected abstract Task ExecuteAsync();

        /// <summary>
        /// Handles command execution errors
        /// </summary>
        protected virtual void HandleError(Exception ex)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            
            System.Windows.Forms.MessageBox.Show(
                $"Error executing command: {ex.Message}",
                "Command Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);

            // Log to debug output
            System.Diagnostics.Debug.WriteLine($"Command Error: {ex}");
        }
    }
}