using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator.Generate
{
    public partial class GenerateForm: Form
    {

        public IEnumerable<string> ProjectPaths { get; set; }
        public string LocalRepoPath { get; set; }
        public string BuildConfiguration { get; set; }
        public bool Parallel { get; set; }
        public int MaxDegreeOfParallelism { get; set; }

        private IPackageGenerator _Generator;
        private PackageGenerateResult _PreviewPackageGenerateResult;

        public GenerateForm(IPackageGenerator generator)
        {
            InitializeComponent();
            _Generator = generator;
            generator.ProgressEvent += Generator_ProgressEvent;
            generator.LogEvent += Generator_LogEvent;
            generator.CompleteEvent += Generator_CompleteEvent;
            generator.PackagesLeft += Generator_LogPackagesLeft;
        }

        
        private void SetResult(PackageGenerateResult result)
        {
            _PreviewPackageGenerateResult = result;
            SetControls();
        }

        private void StartProgress()
        {
            _PreviewPackageGenerateResult = null;
            prgProgress.Value = 0;
            SetControls();
            Application.DoEvents();
        }

        public void SetControls()
        {
            var result = _PreviewPackageGenerateResult;
            if (result == null)
            {
                picMainIcon.Image = Properties.Resources.windowicon32;
                lblHeading.Text = "Nuget Package is being Created";
                lblMainText.Text = "The package is being created, this may take a few minutes.";
                btnOk.Enabled = false;
                return;
            }


            if (result.ResultType == PreviewPackageGenerateResultType.Success)
            {
                lblHeading.Text = "Nuget Package Created Successfully";
            }
            else if (result.ResultType == PreviewPackageGenerateResultType.ExpectedFailure)
            {
                lblHeading.Text = "Unable to Create Nuget Package";
                picMainIcon.Image = SystemIcons.Warning.ToBitmap();
              
            }
            else
            {
                lblHeading.Text = "Nuget Package Creation Encounted an Unexpected Error";
                picMainIcon.Image = SystemIcons.Error.ToBitmap();
            }

            lblMainText.Text = result.Message;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Close();
        }

        private async void GenerateForm_ShownAsync(object sender, EventArgs e)
        {
            StartProgress();
            await _Generator.GeneratePackageAsync(ProjectPaths, LocalRepoPath, BuildConfiguration, Parallel, MaxDegreeOfParallelism);

            btnOk.Enabled = true;
        }

        private void Generator_CompleteEvent(PackageGenerateResult obj)
        {
            SetResult(obj);
        }

        private void Generator_LogPackagesLeft(string message)
        {
            if (lblPackagesLeft.InvokeRequired)
            {
                lblPackagesLeft.Invoke(new Action(() => lblPackagesLeft.Text = message));
            }
            else
            {
                lblPackagesLeft.Text = message;
            }
        }


        private void Generator_LogEvent(string message)
        {
            txtLogOutput.Text += message + Environment.NewLine;
            txtLogOutput.SelectionStart = txtLogOutput.Text.Length;
            txtLogOutput.ScrollToCaret();
            Application.DoEvents();
        }

        private void Generator_ProgressEvent(int progress, string message)
        {
            prgProgress.Value = progress;
            lblProgressUpdate.Text = message;
            Application.DoEvents();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _Generator.Cancel();
            Close();
        }

    }
}
