using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator.Generate
{
    public partial class GenerateForm : Form
    {
        // Properties for configuration
        public IEnumerable<string> ProjectPaths { get; set; }
        public string LocalRepoPath { get; set; }
        public string SolutionPath { get; set; }
        public string BuildConfiguration { get; set; }
        public bool Parallel { get; set; }
        public bool AsSolution { get; set; } = false;
        public int MaxDegreeOfParallelism { get; set; }

        public bool ClearLocalRepositoryBeforeBuild { get; set; } = true;

        public bool CleanBeforeBuild { get; set; } = true;

        // Private fields
        private readonly IPackageGenerator _generator;
        private PackageGenerateResult _previewPackageGenerateResult;
        private bool _isClosing;

        /// <summary>
        /// Initializes a new instance of the GenerateForm class.
        /// </summary>
        public GenerateForm(IPackageGenerator generator)
        {
            InitializeComponent();
            _generator = generator;
            _generator.ProgressEvent += Generator_ProgressEvent;
            _generator.LogEvent += Generator_LogEvent;
            _generator.CompleteEvent += Generator_CompleteEvent;
            _generator.PackagesLeft += Generator_LogPackagesLeft;
        }

        /// <summary>
        /// Handles the form closing event to prevent UI operations after closing begins.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Safely executes an action on the UI thread.
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
            catch (InvalidOperationException)
            {
                // Ignore exceptions that may occur during form closing
            }
        }

        /// <summary>
        /// Safely executes an action with parameter on the UI thread.
        /// </summary>
        private void SafeInvoke<T>(Action<T> action, T parameter)
        {
            if (_isClosing || IsDisposed)
                return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(action, parameter);
                }
                else
                {
                    action(parameter);
                }
            }
            catch (InvalidOperationException)
            {
                // Ignore exceptions that may occur during form closing
            }
        }

        /// <summary>
        /// Sets the result and updates the UI accordingly.
        /// </summary>
        private void SetResult(PackageGenerateResult result)
        {
            SafeInvoke<PackageGenerateResult>(r =>
            {
                _previewPackageGenerateResult = r;
                SetControls();
            }, result);
        }

        /// <summary>
        /// Initializes the progress UI elements.
        /// </summary>
        private void StartProgress()
        {
            SafeInvoke(() =>
            {
                _previewPackageGenerateResult = null;

                if (prgProgress != null && !prgProgress.IsDisposed)
                    prgProgress.Value = 0;

                SetControls();
            });
        }

        /// <summary>
        /// Updates UI controls based on the current result state.
        /// </summary>
        public void SetControls()
        {
            SafeInvoke(() =>
            {
                try
                {
                    var result = _previewPackageGenerateResult;
                    if (result == null)
                    {
                        if (picMainIcon != null && !picMainIcon.IsDisposed)
                            picMainIcon.Image = Properties.Resources.windowicon32;

                        if (lblHeading != null && !lblHeading.IsDisposed)
                            lblHeading.Text = "Nuget Package is being Created";

                        if (lblMainText != null && !lblMainText.IsDisposed)
                            lblMainText.Text = "The package is being created, this may take a few minutes.";

                        if (btnOk != null && !btnOk.IsDisposed)
                            btnOk.Enabled = false;

                        return;
                    }

                    if (result.ResultType == PreviewPackageGenerateResultType.Success)
                    {
                        if (lblHeading != null && !lblHeading.IsDisposed)
                            lblHeading.Text = "Nuget Package Created Successfully";
                    }
                    else if (result.ResultType == PreviewPackageGenerateResultType.ExpectedFailure)
                    {
                        if (lblHeading != null && !lblHeading.IsDisposed)
                            lblHeading.Text = "Unable to Create Nuget Package";

                        if (picMainIcon != null && !picMainIcon.IsDisposed)
                            picMainIcon.Image = SystemIcons.Warning.ToBitmap();
                    }
                    else
                    {
                        if (lblHeading != null && !lblHeading.IsDisposed)
                            lblHeading.Text = "Nuget Package Creation Encountered an Unexpected Error";

                        if (picMainIcon != null && !picMainIcon.IsDisposed)
                            picMainIcon.Image = SystemIcons.Error.ToBitmap();
                    }

                    if (lblMainText != null && !lblMainText.IsDisposed)
                        lblMainText.Text = result.Message;

                    if (btnOk != null && !btnOk.IsDisposed)
                        btnOk.Enabled = true;
                }
                catch (Exception)
                {
                    // Ignore any UI-related exceptions
                }
            });
        }

        /// <summary>
        /// Handles OK button click by closing the form.
        /// </summary>
        private void btnOk_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles form shown event by starting the package generation process.
        /// </summary>
        private async void GenerateForm_ShownAsync(object sender, EventArgs e)
        {
            StartProgress();

            try
            {
                if (AsSolution)
                {
                    await _generator.BuildSolutionAndCopyNupkgsAsync(SolutionPath, BuildConfiguration, LocalRepoPath, CleanBeforeBuild, ClearLocalRepositoryBeforeBuild);
                }
                else
                {
                    await _generator.GeneratePackageAsync(ProjectPaths, LocalRepoPath, BuildConfiguration, CleanBeforeBuild, ClearLocalRepositoryBeforeBuild, Parallel, MaxDegreeOfParallelism);
                }
            }
            catch (Exception ex)
            {
                // If an unhandled exception occurs, log it to the UI
                Generator_LogEvent($"Unhandled exception: {ex.Message}");

                // Create failure result to update UI
                var context = new PackageGeneratorContext
                {
                    ProjectPath = AsSolution ? SolutionPath : "Multiple Projects",
                    NugetPath = LocalRepoPath
                };

                SetResult(PackageGenerateResult.CreateUnexpectedFailureResult(context, ex));
            }
        }

        /// <summary>
        /// Handles the generator's completion event.
        /// </summary>
        private void Generator_CompleteEvent(PackageGenerateResult obj)
        {
            SetResult(obj);
        }

        /// <summary>
        /// Updates the packages left status label.
        /// </summary>
        private void Generator_LogPackagesLeft(string message)
        {
            SafeInvoke<string>(msg =>
            {
                try
                {
                    if (lblPackagesLeft != null && !lblPackagesLeft.IsDisposed)
                        lblPackagesLeft.Text = msg;
                }
                catch (Exception)
                {
                    // Ignore UI-related exceptions
                }
            }, message);
        }

        /// <summary>
        /// Handles log messages from the generator and updates the log output textbox.
        /// </summary>
        private void Generator_LogEvent(string message)
        {
            SafeInvoke<string>(msg =>
            {
                try
                {
                    if (txtLogOutput != null && !txtLogOutput.IsDisposed)
                    {
                        txtLogOutput.AppendText(msg + Environment.NewLine);
                        txtLogOutput.SelectionStart = txtLogOutput.Text.Length;
                        txtLogOutput.ScrollToCaret();
                    }
                }
                catch (Exception)
                {
                    // Ignore UI-related exceptions
                }
            }, message);
        }

        /// <summary>
        /// Updates the progress bar and status label.
        /// </summary>
        private void Generator_ProgressEvent(int progress, string message)
        {
            SafeInvoke(() =>
            {
                try
                {
                    if (prgProgress != null && !prgProgress.IsDisposed)
                        prgProgress.Value = Math.Min(Math.Max(progress, 0), prgProgress.Maximum);

                    if (lblProgressUpdate != null && !lblProgressUpdate.IsDisposed)
                        lblProgressUpdate.Text = message;
                }
                catch (Exception)
                {
                    // Ignore UI-related exceptions
                }
            });
        }

        /// <summary>
        /// Handles cancel button click by cancelling operations and closing the form.
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            try
            {
                _generator.Cancel();
            }
            catch (Exception)
            {
                // Ignore exceptions during cancellation
            }

            Close();
        }
    }
}