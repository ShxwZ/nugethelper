using System;
using System.Threading.Tasks;
using BvNugetPreviewGenerator.UI.Models;

namespace BvNugetPreviewGenerator.UI.Interfaces
{
    /// <summary>
    /// Interface for package generation presenter
    /// </summary>
    public interface IPackageGenerationPresenter : IDisposable
    {
        /// <summary>
        /// Initializes the presenter with view and configuration
        /// </summary>
        void Initialize(IPackageGenerationView view, PackageGenerationConfig config);

        /// <summary>
        /// Starts the package generation process
        /// </summary>
        Task StartGenerationAsync();

        /// <summary>
        /// Cancels the ongoing generation process
        /// </summary>
        void CancelGeneration();
    }
}