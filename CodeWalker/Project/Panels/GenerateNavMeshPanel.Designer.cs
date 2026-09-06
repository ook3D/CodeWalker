namespace CodeWalker.Project.Panels
{
    partial class GenerateNavMeshPanel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GenerateNavMeshPanel));
            this.MinTextBox = new System.Windows.Forms.TextBox();
            this.label81 = new System.Windows.Forms.Label();
            this.MaxTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.GenerateButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.StatusLabel = new System.Windows.Forms.Label();
            this.SamplingDensityNumeric = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.HeightThresholdNumeric = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.MaxSlopeAngleNumeric = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.ProgressBar = new System.Windows.Forms.ProgressBar();
            this.CancelButton = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.SamplingDensityNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.HeightThresholdNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxSlopeAngleNumeric)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // MinTextBox
            // 
            this.MinTextBox.Location = new System.Drawing.Point(70, 19);
            this.MinTextBox.Name = "MinTextBox";
            this.MinTextBox.Size = new System.Drawing.Size(177, 20);
            this.MinTextBox.TabIndex = 46;
            this.MinTextBox.Text = "0, 0";
            // 
            // label81
            // 
            this.label81.AutoSize = true;
            this.label81.Location = new System.Drawing.Point(5, 22);
            this.label81.Name = "label81";
            this.label81.Size = new System.Drawing.Size(56, 13);
            this.label81.TabIndex = 47;
            this.label81.Text = "Min: (X, Y)";
            // 
            // MaxTextBox
            // 
            this.MaxTextBox.Location = new System.Drawing.Point(70, 45);
            this.MaxTextBox.Name = "MaxTextBox";
            this.MaxTextBox.Size = new System.Drawing.Size(177, 20);
            this.MaxTextBox.TabIndex = 48;
            this.MaxTextBox.Text = "50, 50";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 48);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 49;
            this.label1.Text = "Max: (X, Y)";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(598, 80);
            this.label2.TabIndex = 50;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // GenerateButton
            // 
            this.GenerateButton.Location = new System.Drawing.Point(15, 340);
            this.GenerateButton.Name = "GenerateButton";
            this.GenerateButton.Size = new System.Drawing.Size(90, 28);
            this.GenerateButton.TabIndex = 51;
            this.GenerateButton.Text = "Generate";
            this.GenerateButton.UseVisualStyleBackColor = true;
            this.GenerateButton.Click += new System.EventHandler(this.GenerateButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(253, 34);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(235, 13);
            this.label3.TabIndex = 52;
            this.label3.Text = "(Nav meshes will only be generated for this area)";
            // 
            // StatusLabel
            // 
            this.StatusLabel.AutoSize = true;
            this.StatusLabel.Location = new System.Drawing.Point(12, 380);
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(95, 13);
            this.StatusLabel.TabIndex = 53;
            this.StatusLabel.Text = "Ready to generate";
            // 
            // SamplingDensityNumeric
            // 
            this.SamplingDensityNumeric.DecimalPlaces = 2;
            this.SamplingDensityNumeric.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.SamplingDensityNumeric.Location = new System.Drawing.Point(150, 19);
            this.SamplingDensityNumeric.Maximum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.SamplingDensityNumeric.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.SamplingDensityNumeric.Name = "SamplingDensityNumeric";
            this.SamplingDensityNumeric.Size = new System.Drawing.Size(80, 20);
            this.SamplingDensityNumeric.TabIndex = 54;
            this.SamplingDensityNumeric.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(5, 21);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(139, 13);
            this.label4.TabIndex = 55;
            this.label4.Text = "Sampling Density (meters):";
            // 
            // HeightThresholdNumeric
            // 
            this.HeightThresholdNumeric.DecimalPlaces = 2;
            this.HeightThresholdNumeric.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.HeightThresholdNumeric.Location = new System.Drawing.Point(150, 45);
            this.HeightThresholdNumeric.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.HeightThresholdNumeric.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.HeightThresholdNumeric.Name = "HeightThresholdNumeric";
            this.HeightThresholdNumeric.Size = new System.Drawing.Size(80, 20);
            this.HeightThresholdNumeric.TabIndex = 56;
            this.HeightThresholdNumeric.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 47);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(139, 13);
            this.label5.TabIndex = 57;
            this.label5.Text = "Height Threshold (meters):";
            // 
            // MaxSlopeAngleNumeric
            // 
            this.MaxSlopeAngleNumeric.DecimalPlaces = 1;
            this.MaxSlopeAngleNumeric.Location = new System.Drawing.Point(150, 71);
            this.MaxSlopeAngleNumeric.Maximum = new decimal(new int[] {
            90,
            0,
            0,
            0});
            this.MaxSlopeAngleNumeric.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.MaxSlopeAngleNumeric.Name = "MaxSlopeAngleNumeric";
            this.MaxSlopeAngleNumeric.Size = new System.Drawing.Size(80, 20);
            this.MaxSlopeAngleNumeric.TabIndex = 58;
            this.MaxSlopeAngleNumeric.Value = new decimal(new int[] {
            44,
            0,
            0,
            0});
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(5, 73);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(139, 13);
            this.label6.TabIndex = 59;
            this.label6.Text = "Max Slope Angle (degrees):";
            // 
            // ProgressBar
            // 
            this.ProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ProgressBar.Location = new System.Drawing.Point(15, 310);
            this.ProgressBar.Name = "ProgressBar";
            this.ProgressBar.Size = new System.Drawing.Size(595, 23);
            this.ProgressBar.TabIndex = 60;
            // 
            // CancelButton
            // 
            this.CancelButton.Enabled = false;
            this.CancelButton.Location = new System.Drawing.Point(111, 340);
            this.CancelButton.Name = "CancelButton";
            this.CancelButton.Size = new System.Drawing.Size(90, 28);
            this.CancelButton.TabIndex = 61;
            this.CancelButton.Text = "Cancel";
            this.CancelButton.UseVisualStyleBackColor = true;
            this.CancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label81);
            this.groupBox1.Controls.Add(this.MinTextBox);
            this.groupBox1.Controls.Add(this.MaxTextBox);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(15, 92);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(595, 75);
            this.groupBox1.TabIndex = 62;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Generation Area";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.SamplingDensityNumeric);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.HeightThresholdNumeric);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.MaxSlopeAngleNumeric);
            this.groupBox2.Location = new System.Drawing.Point(15, 173);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(595, 105);
            this.groupBox2.TabIndex = 63;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Generation Parameters";
            // 
            // GenerateNavMeshPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(622, 420);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.CancelButton);
            this.Controls.Add(this.ProgressBar);
            this.Controls.Add(this.StatusLabel);
            this.Controls.Add(this.GenerateButton);
            this.Controls.Add(this.label2);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "GenerateNavMeshPanel";
            this.Text = "Generate Nav Meshes";
            ((System.ComponentModel.ISupportInitialize)(this.SamplingDensityNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.HeightThresholdNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MaxSlopeAngleNumeric)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox MinTextBox;
        private System.Windows.Forms.Label label81;
        private System.Windows.Forms.TextBox MaxTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button GenerateButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label StatusLabel;
        private System.Windows.Forms.NumericUpDown SamplingDensityNumeric;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown HeightThresholdNumeric;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown MaxSlopeAngleNumeric;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ProgressBar ProgressBar;
        private new System.Windows.Forms.Button CancelButton;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
    }
}