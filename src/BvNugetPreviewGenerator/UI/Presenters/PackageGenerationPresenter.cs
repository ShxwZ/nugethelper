using System;
using System.Threading.Tasks;
using BvNugetPreviewGenerator.Generate;
using BvNugetPreviewGenerator.UI.Interfaces;
using BvNugetPreviewGenerator.UI.Models;

namespace BvNugetPreviewGenerator.UI.Presenters
{
    /// <summary>
    /// Presenter for package generation operations following MVP pattern
    /// </summary>
    public class PackageGenerationPresenter : IPackageGenerationPresenter
    {
        private readonly IPackageGenerator _packageGenerator;
        private IPackageGenerationView _view;
        private PackageGenerationConfig _config;
        private PackageGenerationState _state;
        private bool _disposed;

        public PackageGenerationPresenter(IPackageGenerator packageGenerator)
        {
            _packageGenerator = packageGenerator ?? throw new ArgumentNullException(nameof(packageGenerator));
            _state = new PackageGenerationState();
        }

        /// <summary>
        /// Initializes the presenter with view and configuration
        /// </summary>
        public void Initialize(IPackageGenerationView view, PackageGenerationConfig config)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Subscribe to view events
            _view.CancelRequested += OnCancelRequested;
            _view.CloseRequested += OnCloseRequested;

            // Subscribe to generator events
            _packageGenerator.ProgressEvent += OnProgressChanged;
            _packageGenerator.LogEvent += OnLogMessageReceived;
            _packageGenerator.CompleteEvent += OnGenerationCompleted;
            _packageGenerator.PackagesLeft += OnPackagesLeftChanged;

            // Initialize view state
            _state.IsInProgress = false;
            _state.CanCancel = true;
            _state.CanClose = false;
            UpdateView();
        }

        /// <summary>
        /// Starts the package generation process
        /// </summary>
        public async Task StartGenerationAsync()
        {
            if (_state.IsInProgress) return;

            try
            {
                _state.IsInProgress = true;
                _state.CanClose = false;
                _state.ProgressPercentage = 0;
                _state.ProgressMessage = "Starting package generation...";
                _state.LogContent = string.Empty;
                UpdateView();

                if (_config.AsSolution)
                {
                    await _packageGenerator.BuildSolutionAndCopyNupkgsAsync(
                        _config.SolutionPath,
                        _config.BuildConfiguration,
                        _config.LocalRepoPath,
                        _config.CleanBeforeBuild,
                        _config.ClearLocalRepositoryBeforeBuild);
                }
                else
                {
                    await _packageGenerator.GeneratePackageAsync(
                        _config.ProjectPaths,
                        _config.LocalRepoPath,
                        _config.BuildConfiguration,
                        _config.CleanBeforeBuild,
                        _config.ClearLocalRepositoryBeforeBuild,
                        _config.Parallel,
                        _config.MaxDegreeOfParallelism);
                }
            }
            catch (Exception ex)
            {
                OnGenerationCompleted(PackageGenerateResult.CreateUnexpectedFailureResult(
                    new PackageGeneratorContext { ProjectPath = _config.AsSolution ? _config.SolutionPath : "Multiple Projects" }, 
                    ex));
            }
        }

        /// <summary>
        /// Cancels the ongoing generation process
        /// </summary>
        public void CancelGeneration()
        {
            try
            {
                _packageGenerator.Cancel();
                _state.IsInProgress = false;
                _state.CanClose = true;
                _state.Result = PackageGenerationResult.ExpectedFailure("Operation cancelled by user");
                UpdateView();
            }
            catch (Exception ex)
            {
                // Log cancellation error but don't throw
                _view?.AppendLogMessage($"Error during cancellation: {ex.Message}");
            }
        }

        #region Event Handlers

        private void OnCancelRequested(object sender, EventArgs e)
        {
            CancelGeneration();
        }

        private void OnCloseRequested(object sender, EventArgs e)
        {
            if (_state.IsInProgress)
            {
                CancelGeneration();
            }
        }

        private void OnProgressChanged(int percentage, string message)
        {
            _state.ProgressPercentage = Math.Min(Math.Max(percentage, 0), 100);
            _state.ProgressMessage = message ?? string.Empty;
            UpdateView();
        }

        private void OnLogMessageReceived(string message)
        {
            _view?.AppendLogMessage(message);
        }

        private void OnPackagesLeftChanged(string message)
        {
            _state.PackagesLeftMessage = message ?? string.Empty;
            UpdateView();
        }

        private void OnGenerationCompleted(PackageGenerateResult result)
        {
            _state.IsInProgress = false;
            _state.CanClose = true;
            _state.CanCancel = false;
            _state.ProgressPercentage = 100;

            // Convert from PackageGenerateResult to our model
            switch (result.ResultType)
            {
                case PreviewPackageGenerateResultType.Success:
                    _state.Result = PackageGenerationResult.Success(result.Message);
                    _state.ProgressMessage = "Generation completed successfully";
                    break;
                case PreviewPackageGenerateResultType.ExpectedFailure:
                    _state.Result = PackageGenerationResult.ExpectedFailure(result.Message);
                    _state.ProgressMessage = "Generation failed";
                    break;
                case PreviewPackageGenerateResultType.UnexpectedFailure:
                    _state.Result = PackageGenerationResult.UnexpectedFailure(new Exception(result.Message));
                    _state.ProgressMessage = "Generation encountered an error";
                    break;
            }

            UpdateView();
        }

        #endregion

        private void UpdateView()
        {
            _view?.UpdateState(_state);
        }

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            // Unsubscribe from events
            if (_view != null)
            {
                _view.CancelRequested -= OnCancelRequested;
                _view.CloseRequested -= OnCloseRequested;
            }

            if (_packageGenerator != null)
            {
                _packageGenerator.ProgressEvent -= OnProgressChanged;
                _packageGenerator.LogEvent -= OnLogMessageReceived;
                _packageGenerator.CompleteEvent -= OnGenerationCompleted;
                _packageGenerator.PackagesLeft -= OnPackagesLeftChanged;
            }

            _disposed = true;
        }

        #endregion
    }
}