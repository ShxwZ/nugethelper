using System;
using System.Drawing;
using System.Windows.Forms;
using BvNugetPreviewGenerator.Generate;
using BvNugetPreviewGenerator.UI.Interfaces;
using BvNugetPreviewGenerator.UI.Models;
using BvNugetPreviewGenerator.UI.Presenters;

namespace BvNugetPreviewGenerator.Generate
{
    /// <summary>
    /// Form for package generation that implements MVP pattern for better separation of concerns
    /// </summary>
    public partial class GenerateForm : Form, IPackageGenerationView
    {
        #region Events

        /// <summary>
        /// Event raised when the user requests to cancel the operation
        /// </summary>
        public event EventHandler CancelRequested;

        /// <summary>
        /// Event raised when the user closes the form
        /// </summary>
        public event EventHandler CloseRequested;

        #endregion

        #region Private Fields

        private readonly IPackageGenerationPresenter _presenter;
        private readonly PackageGenerationFormModel _model;
        private bool _isClosing;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the GenerateForm class
        /// </summary>
        public GenerateForm(IPackageGenerator generator)
        {
            InitializeComponent();
            _presenter = new PackageGenerationPresenter(generator);
            _model = new PackageGenerationFormModel();
        }

        /// <summary>
        /// Initializes a new instance with custom presenter (for testing)
        /// </summary>
        public GenerateForm(IPackageGenerationPresenter presenter)
        {
            InitializeComponent();
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _model = new PackageGenerationFormModel();
        }

        #endregion

        #region Model Properties (for backward compatibility and clean API)

        /// <summary>
        /// Gets or sets the configuration model for this generation operation
        /// </summary>
        public PackageGenerationFormModel Configuration
        {
            get => _model;
            set => CopyConfigurationProperties(value, _model);
        }

        // Backward compatibility properties - delegate to model
        public System.Collections.Generic.IEnumerable<string> ProjectPaths
        {
            get => _model.ProjectPaths;
            set => _model.ProjectPaths = value;
        }

        public string LocalRepoPath
        {
            get => _model.LocalRepoPath;
            set => _model.LocalRepoPath = value;
        }

        public string SolutionPath
        {
            get => _model.SolutionPath;
            set => _model.SolutionPath = value;
        }

        public string BuildConfiguration
        {
            get => _model.BuildConfiguration;
            set => _model.BuildConfiguration = value;
        }

        public bool Parallel
        {
            get => _model.Parallel;
            set => _model.Parallel = value;
        }

        public bool AsSolution
        {
            get => _model.AsSolution;
            set => _model.AsSolution = value;
        }

        public int MaxDegreeOfParallelism
        {
            get => _model.MaxDegreeOfParallelism;
            set => _model.MaxDegreeOfParallelism = value;
        }

        public bool ClearLocalRepositoryBeforeBuild
        {
            get => _model.ClearLocalRepositoryBeforeBuild;
            set => _model.ClearLocalRepositoryBeforeBuild = value;
        }

        public bool CleanBeforeBuild
        {
            get => _model.CleanBeforeBuild;
            set => _model.CleanBeforeBuild = value;
        }

        #endregion

        #region Form Events

        /// <summary>
        /// Handles the form closing event
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Handles form shown event by initializing and starting the generation process
        /// </summary>
        private async void GenerateForm_Shown(object sender, EventArgs e)
        {
            try
            {
                // Use the model's ToConfig method for clean conversion
                var config = _model.ToConfig();

                // Initialize presenter
                _presenter.Initialize(this, config);

                // Start generation
                await _presenter.StartGenerationAsync();
            }
            catch (Exception ex)
            {
                AppendLogMessage($"Error initializing generation: {ex.Message}");
                UpdateState(new PackageGenerationState
                {
                    Result = PackageGenerationResult.UnexpectedFailure(ex),
                    CanClose = true,
                    IsInProgress = false
                });
            }
        }

        /// <summary>
        /// Handles OK button click
        /// </summary>
        private void btnOk_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles cancel button click
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region IPackageGenerationView Implementation

        /// <summary>
        /// Updates the view with new state
        /// </summary>
        public void UpdateState(PackageGenerationState state)
        {
            SafeInvoke(() =>
            {
                if (state == null) return;

                // Update progress
                if (prgProgress != null && !prgProgress.IsDisposed)
                    prgProgress.Value = Math.Min(Math.Max(state.ProgressPercentage, 0), prgProgress.Maximum);

                if (lblProgressUpdate != null && !lblProgressUpdate.IsDisposed)
                    lblProgressUpdate.Text = state.ProgressMessage;

                if (lblPackagesLeft != null && !lblPackagesLeft.IsDisposed)
                    lblPackagesLeft.Text = state.PackagesLeftMessage;

                // Update controls based on state
                if (btnOk != null && !btnOk.IsDisposed)
                    btnOk.Enabled = state.CanClose;

                if (btnCancel != null && !btnCancel.IsDisposed)
                    btnCancel.Enabled = state.CanCancel;

                // Update UI based on result
                UpdateResultUI(state);
            });
        }

        /// <summary>
        /// Appends a log message to the display
        /// </summary>
        public void AppendLogMessage(string message)
        {
            SafeInvoke(() =>
            {
                if (txtLogOutput != null && !txtLogOutput.IsDisposed && !string.IsNullOrEmpty(message))
                {
                    txtLogOutput.AppendText(message + Environment.NewLine);
                    txtLogOutput.SelectionStart = txtLogOutput.Text.Length;
                    txtLogOutput.ScrollToCaret();
                }
            });
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Copies configuration properties from source to target model
        /// </summary>
        private void CopyConfigurationProperties(PackageGenerationFormModel source, PackageGenerationFormModel target)
        {
            if (source == null || target == null) return;

            target.ProjectPaths = source.ProjectPaths;
            target.LocalRepoPath = source.LocalRepoPath;
            target.SolutionPath = source.SolutionPath;
            target.BuildConfiguration = source.BuildConfiguration;
            target.Parallel = source.Parallel;
            target.AsSolution = source.AsSolution;
            target.MaxDegreeOfParallelism = source.MaxDegreeOfParallelism;
            target.ClearLocalRepositoryBeforeBuild = source.ClearLocalRepositoryBeforeBuild;
            target.CleanBeforeBuild = source.CleanBeforeBuild;
        }

        /// <summary>
        /// Updates UI elements based on the generation result
        /// </summary>
        private void UpdateResultUI(PackageGenerationState state)
        {
            if (state.Result == null)
            {
                // In progress state
                if (picMainIcon != null && !picMainIcon.IsDisposed)
                    picMainIcon.Image = Properties.Resources.windowicon32;

                if (lblHeading != null && !lblHeading.IsDisposed)
                    lblHeading.Text = "NuGet Package is being Created";

                if (lblMainText != null && !lblMainText.IsDisposed)
                    lblMainText.Text = "The package is being created, this may take a few minutes.";

                return;
            }

            // Update based on result type
            switch (state.Result.Type)
            {
                case PackageGenerationResultType.Success:
                    if (lblHeading != null && !lblHeading.IsDisposed)
                        lblHeading.Text = "NuGet Package Created Successfully";

                    if (picMainIcon != null && !picMainIcon.IsDisposed)
                        picMainIcon.Image = Properties.Resources.windowicon32;
                    break;

                case PackageGenerationResultType.ExpectedFailure:
                    if (lblHeading != null && !lblHeading.IsDisposed)
                        lblHeading.Text = "Unable to Create NuGet Package";

                    if (picMainIcon != null && !picMainIcon.IsDisposed)
                        picMainIcon.Image = SystemIcons.Warning.ToBitmap();
                    break;

                case PackageGenerationResultType.UnexpectedFailure:
                    if (lblHeading != null && !lblHeading.IsDisposed)
                        lblHeading.Text = "NuGet Package Creation Encountered an Unexpected Error";

                    if (picMainIcon != null && !picMainIcon.IsDisposed)
                        picMainIcon.Image = SystemIcons.Error.ToBitmap();
                    break;
            }

            if (lblMainText != null && !lblMainText.IsDisposed)
                lblMainText.Text = state.Result.Message;
        }

        /// <summary>
        /// Safely executes an action on the UI thread
        /// </summary>
        private void SafeInvoke(Action action)
        {
            if (_isClosing || IsDisposed)
                return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // Ignore if controls are disposed
            }
            catch (InvalidOperationException)
            {
                // Ignore exceptions that may occur during form closing
            }
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _presenter?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}