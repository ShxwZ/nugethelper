using System;
using System.Drawing;
using System.Windows.Forms;

namespace BvNugetPreviewGenerator
{
    /// <summary>
    /// Simple progress dialog form
    /// </summary>
    public partial class ProgressForm : Form
    {
        private Label lblTitle;
        private Label lblMessage;
        private ProgressBar progressBar;

        /// <summary>
        /// Gets or sets the progress message
        /// </summary>
        public string Message
        {
            get => lblMessage?.Text ?? string.Empty;
            set
            {
                if (lblMessage != null)
                    lblMessage.Text = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the ProgressForm
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Initial message</param>
        public ProgressForm(string title, string message)
        {
            InitializeComponent();
            this.Text = title;
            this.Message = message;
        }

        /// <summary>
        /// Initializes the form components
        /// </summary>
        private void InitializeComponent()
        {
            this.lblTitle = new Label();
            this.lblMessage = new Label();
            this.progressBar = new ProgressBar();
            this.SuspendLayout();

            // Form properties
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(400, 120);
            this.ShowInTaskbar = false;
            this.ControlBox = false;

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblTitle.Location = new Point(12, 15);
            this.lblTitle.Size = new Size(0, 15);

            // lblMessage
            this.lblMessage.AutoSize = true;
            this.lblMessage.Location = new Point(15, 35);
            this.lblMessage.Size = new Size(0, 13);

            // progressBar
            this.progressBar.Location = new Point(15, 55);
            this.progressBar.Size = new Size(355, 23);
            this.progressBar.Style = ProgressBarStyle.Marquee;
            this.progressBar.MarqueeAnimationSpeed = 30;

            // Add controls to form
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.progressBar);

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}