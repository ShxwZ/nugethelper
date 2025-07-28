using System;
using System.Drawing;
using System.Windows.Forms;
using BvNugetPreviewGenerator.Generate;

namespace BvNugetPreviewGenerator
{
    /// <summary>
    /// Dialog for prompting user to enter a version number
    /// </summary>
    public partial class VersionInputDialog : Form
    {
        private Label lblPrompt;
        private TextBox txtVersion;
        private Button btnOK;
        private Button btnCancel;
        private Label lblValidation;

        /// <summary>
        /// Gets or sets the prompt text displayed to the user
        /// </summary>
        public string PromptText
        {
            get => lblPrompt?.Text ?? string.Empty;
            set
            {
                if (lblPrompt != null)
                    lblPrompt.Text = value;
            }
        }

        /// <summary>
        /// Gets the version entered by the user
        /// </summary>
        public string Version => txtVersion?.Text?.Trim() ?? string.Empty;

        /// <summary>
        /// Initializes a new instance of the VersionInputDialog
        /// </summary>
        public VersionInputDialog()
        {
            InitializeComponent();
            InitializeValidation();
        }

        /// <summary>
        /// Initializes the form components
        /// </summary>
        private void InitializeComponent()
        {
            this.lblPrompt = new Label();
            this.txtVersion = new TextBox();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.lblValidation = new Label();
            this.SuspendLayout();

            // Form properties
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(400, 180);
            this.ShowInTaskbar = false;

            // lblPrompt
            this.lblPrompt.AutoSize = true;
            this.lblPrompt.Location = new Point(12, 15);
            this.lblPrompt.Size = new Size(0, 13);
            this.lblPrompt.Text = "Enter version:";

            // txtVersion
            this.txtVersion.Location = new Point(15, 35);
            this.txtVersion.Size = new Size(350, 20);
            this.txtVersion.Font = new Font("Consolas", 9F, FontStyle.Regular);
            this.txtVersion.TextChanged += TxtVersion_TextChanged;

            // lblValidation
            this.lblValidation.AutoSize = true;
            this.lblValidation.Location = new Point(15, 62);
            this.lblValidation.Size = new Size(0, 13);
            this.lblValidation.ForeColor = Color.Red;
            this.lblValidation.Text = "";

            // btnOK
            this.btnOK.DialogResult = DialogResult.OK;
            this.btnOK.Location = new Point(205, 95);
            this.btnOK.Size = new Size(75, 23);
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += BtnOK_Click;

            // btnCancel
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new Point(290, 95);
            this.btnCancel.Size = new Size(75, 23);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;

            // Add controls to form
            this.Controls.Add(this.lblPrompt);
            this.Controls.Add(this.txtVersion);
            this.Controls.Add(this.lblValidation);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);

            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        /// <summary>
        /// Initializes version validation
        /// </summary>
        private void InitializeValidation()
        {
            ValidateVersion();
        }

        /// <summary>
        /// Handles text change in version textbox
        /// </summary>
        private void TxtVersion_TextChanged(object sender, EventArgs e)
        {
            ValidateVersion();
        }

        /// <summary>
        /// Validates the entered version format using centralized validation
        /// </summary>
        private void ValidateVersion()
        {
            string version = txtVersion.Text.Trim();

            // Use centralized validation from PackageVersion
            var validationResult = PackageVersion.Validate(version);

            if (validationResult.IsValid)
            {
                lblValidation.Text = "✓ Valid format";
                lblValidation.ForeColor = Color.Green;
                btnOK.Enabled = true;
            }
            else
            {
                lblValidation.Text = validationResult.Message;
                lblValidation.ForeColor = Color.Red;
                btnOK.Enabled = false;
            }
        }

        /// <summary>
        /// Handles OK button click
        /// </summary>
        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (!btnOK.Enabled)
            {
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Override to prevent closing with invalid input
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK && !btnOK.Enabled)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }
    }
}