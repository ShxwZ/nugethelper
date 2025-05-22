namespace BvNugetPreviewGenerator.Generate
{
    partial class GenerateForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnOk = new System.Windows.Forms.Button();
            this.flpLayout = new System.Windows.Forms.FlowLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblPackagesLeft = new System.Windows.Forms.Label();
            this.lblHeading = new System.Windows.Forms.Label();
            this.picMainIcon = new System.Windows.Forms.PictureBox();
            this.lblMainText = new System.Windows.Forms.Label();
            this.pnlProgress = new System.Windows.Forms.Panel();
            this.lblProgressUpdate = new System.Windows.Forms.Label();
            this.prgProgress = new System.Windows.Forms.ProgressBar();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.txtLogOutput = new System.Windows.Forms.TextBox();
            this.pnlLog = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.flpLayout.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picMainIcon)).BeginInit();
            this.pnlProgress.SuspendLayout();
            this.panel2.SuspendLayout();
            this.pnlLog.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnOk.Enabled = false;
            this.btnOk.Location = new System.Drawing.Point(398, 7);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(109, 27);
            this.btnOk.TabIndex = 6;
            this.btnOk.Text = "Ok";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // flpLayout
            // 
            this.flpLayout.AutoSize = true;
            this.flpLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpLayout.BackColor = System.Drawing.SystemColors.Control;
            this.flpLayout.Controls.Add(this.panel1);
            this.flpLayout.Controls.Add(this.pnlProgress);
            this.flpLayout.Controls.Add(this.panel2);
            this.flpLayout.Controls.Add(this.pnlLog);
            this.flpLayout.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flpLayout.Location = new System.Drawing.Point(0, 0);
            this.flpLayout.Name = "flpLayout";
            this.flpLayout.Size = new System.Drawing.Size(636, 433);
            this.flpLayout.TabIndex = 9;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.Control;
            this.panel1.Controls.Add(this.lblPackagesLeft);
            this.panel1.Controls.Add(this.lblHeading);
            this.panel1.Controls.Add(this.picMainIcon);
            this.panel1.Controls.Add(this.lblMainText);
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(630, 94);
            this.panel1.TabIndex = 10;
            // 
            // lblPackagesLeft
            // 
            this.lblPackagesLeft.Location = new System.Drawing.Point(73, 65);
            this.lblPackagesLeft.Name = "lblPackagesLeft";
            this.lblPackagesLeft.Size = new System.Drawing.Size(402, 21);
            this.lblPackagesLeft.TabIndex = 16;
            this.lblPackagesLeft.Text = "0/0";
            // 
            // lblHeading
            // 
            this.lblHeading.AutoSize = true;
            this.lblHeading.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHeading.Location = new System.Drawing.Point(73, 16);
            this.lblHeading.Name = "lblHeading";
            this.lblHeading.Size = new System.Drawing.Size(275, 17);
            this.lblHeading.TabIndex = 1;
            this.lblHeading.Text = "Nuget Package Created Successfully";
            // 
            // picMainIcon
            // 
            this.picMainIcon.BackColor = System.Drawing.Color.Transparent;
            this.picMainIcon.Image = global::BvNugetPreviewGenerator.Properties.Resources.windowicon32;
            this.picMainIcon.Location = new System.Drawing.Point(13, 16);
            this.picMainIcon.Name = "picMainIcon";
            this.picMainIcon.Size = new System.Drawing.Size(34, 34);
            this.picMainIcon.TabIndex = 0;
            this.picMainIcon.TabStop = false;
            // 
            // lblMainText
            // 
            this.lblMainText.Location = new System.Drawing.Point(73, 44);
            this.lblMainText.Name = "lblMainText";
            this.lblMainText.Size = new System.Drawing.Size(402, 21);
            this.lblMainText.TabIndex = 2;
            this.lblMainText.Text = "lblMainText";
            // 
            // pnlProgress
            // 
            this.pnlProgress.Controls.Add(this.lblProgressUpdate);
            this.pnlProgress.Controls.Add(this.prgProgress);
            this.pnlProgress.Location = new System.Drawing.Point(3, 103);
            this.pnlProgress.Name = "pnlProgress";
            this.pnlProgress.Size = new System.Drawing.Size(630, 60);
            this.pnlProgress.TabIndex = 14;
            // 
            // lblProgressUpdate
            // 
            this.lblProgressUpdate.AutoSize = true;
            this.lblProgressUpdate.Location = new System.Drawing.Point(10, 39);
            this.lblProgressUpdate.Name = "lblProgressUpdate";
            this.lblProgressUpdate.Size = new System.Drawing.Size(0, 13);
            this.lblProgressUpdate.TabIndex = 14;
            // 
            // prgProgress
            // 
            this.prgProgress.Location = new System.Drawing.Point(12, 13);
            this.prgProgress.Name = "prgProgress";
            this.prgProgress.Size = new System.Drawing.Size(598, 20);
            this.prgProgress.TabIndex = 13;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.label1);
            this.panel2.Controls.Add(this.txtLogOutput);
            this.panel2.Location = new System.Drawing.Point(3, 169);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(630, 213);
            this.panel2.TabIndex = 15;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Log Output";
            // 
            // txtLogOutput
            // 
            this.txtLogOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogOutput.BackColor = System.Drawing.SystemColors.ControlLight;
            this.txtLogOutput.Location = new System.Drawing.Point(12, 26);
            this.txtLogOutput.Multiline = true;
            this.txtLogOutput.Name = "txtLogOutput";
            this.txtLogOutput.ReadOnly = true;
            this.txtLogOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLogOutput.Size = new System.Drawing.Size(610, 184);
            this.txtLogOutput.TabIndex = 3;
            this.txtLogOutput.WordWrap = false;
            // 
            // pnlLog
            // 
            this.pnlLog.Controls.Add(this.btnOk);
            this.pnlLog.Controls.Add(this.btnCancel);
            this.pnlLog.Location = new System.Drawing.Point(3, 388);
            this.pnlLog.Name = "pnlLog";
            this.pnlLog.Size = new System.Drawing.Size(630, 42);
            this.pnlLog.TabIndex = 11;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnCancel.Location = new System.Drawing.Point(511, 7);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(109, 27);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // GenerateForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(639, 438);
            this.ControlBox = false;
            this.Controls.Add(this.flpLayout);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "GenerateForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Generate Nuget Package";
            this.Shown += new System.EventHandler(this.GenerateForm_ShownAsync);
            this.flpLayout.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picMainIcon)).EndInit();
            this.pnlProgress.ResumeLayout(false);
            this.pnlProgress.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.pnlLog.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.FlowLayoutPanel flpLayout;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblHeading;
        private System.Windows.Forms.PictureBox picMainIcon;
        private System.Windows.Forms.Label lblMainText;
        private System.Windows.Forms.Panel pnlLog;
        private System.Windows.Forms.Panel pnlProgress;
        private System.Windows.Forms.ProgressBar prgProgress;
        private System.Windows.Forms.Label lblProgressUpdate;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblPackagesLeft;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtLogOutput;
    }
}