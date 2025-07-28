using System;
using BvNugetPreviewGenerator.UI.Models;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator.UI.Interfaces
{
    /// <summary>
    /// Interface for package generation view
    /// </summary>
    public interface IPackageGenerationView
    {
        /// <summary>
        /// Event raised when the user requests to cancel the operation
        /// </summary>
        event EventHandler CancelRequested;

        /// <summary>
        /// Event raised when the user closes the form
        /// </summary>
        event EventHandler CloseRequested;

        /// <summary>
        /// Updates the view with new state
        /// </summary>
        void UpdateState(PackageGenerationState state);

        /// <summary>
        /// Appends a log message to the display
        /// </summary>
        void AppendLogMessage(string message);

        /// <summary>
        /// Shows the view as a modal dialog
        /// </summary>
        DialogResult ShowDialog();

        /// <summary>
        /// Closes the view
        /// </summary>
        void Close();
    }
}