namespace CodeWalker.Project.Panels
{
    partial class EditYnvPanel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditYnvPanel));
            YnvAABBSizeTextBox = new System.Windows.Forms.TextBox();
            label91 = new System.Windows.Forms.Label();
            label89 = new System.Windows.Forms.Label();
            YnvAreaIDYUpDown = new System.Windows.Forms.NumericUpDown();
            label90 = new System.Windows.Forms.Label();
            YnvAreaIDXUpDown = new System.Windows.Forms.NumericUpDown();
            YnvAreaIDInfoLabel = new System.Windows.Forms.Label();
            label92 = new System.Windows.Forms.Label();
            YnvVertexCountLabel = new System.Windows.Forms.Label();
            YnvPolyCountLabel = new System.Windows.Forms.Label();
            YnvPortalCountLabel = new System.Windows.Forms.Label();
            YnvPortalLinkCountLabel = new System.Windows.Forms.Label();
            YnvPointCountLabel = new System.Windows.Forms.Label();
            YnvByteCountLabel = new System.Windows.Forms.Label();
            YnvFlagsGroupBox = new System.Windows.Forms.GroupBox();
            YnvFlagsUnknown16CheckBox = new System.Windows.Forms.CheckBox();
            YnvFlagsUnknown8CheckBox = new System.Windows.Forms.CheckBox();
            YnvFlagsVehicleCheckBox = new System.Windows.Forms.CheckBox();
            YnvFlagsPortalsCheckBox = new System.Windows.Forms.CheckBox();
            YnvFlagsPolygonsCheckBox = new System.Windows.Forms.CheckBox();
            YnvVersionUnkHashTextBox = new System.Windows.Forms.TextBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label78 = new System.Windows.Forms.Label();
            YnvAdjAreaIDsTextBox = new WinForms.TextBoxFix();
            label48 = new System.Windows.Forms.Label();
            YnvProjectPathTextBox = new System.Windows.Forms.TextBox();
            label47 = new System.Windows.Forms.Label();
            YnvRpfPathTextBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)YnvAreaIDYUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)YnvAreaIDXUpDown).BeginInit();
            YnvFlagsGroupBox.SuspendLayout();
            SuspendLayout();
            // 
            // YnvAABBSizeTextBox
            // 
            YnvAABBSizeTextBox.Location = new System.Drawing.Point(106, 55);
            YnvAABBSizeTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvAABBSizeTextBox.Name = "YnvAABBSizeTextBox";
            YnvAABBSizeTextBox.Size = new System.Drawing.Size(160, 23);
            YnvAABBSizeTextBox.TabIndex = 37;
            YnvAABBSizeTextBox.TextChanged += YnvAABBSizeTextBox_TextChanged;
            // 
            // label91
            // 
            label91.AutoSize = true;
            label91.Location = new System.Drawing.Point(28, 59);
            label91.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label91.Name = "label91";
            label91.Size = new System.Drawing.Size(63, 15);
            label91.TabIndex = 38;
            label91.Text = "AABB Size:";
            // 
            // label89
            // 
            label89.AutoSize = true;
            label89.Location = new System.Drawing.Point(175, 16);
            label89.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label89.Name = "label89";
            label89.Size = new System.Drawing.Size(17, 15);
            label89.TabIndex = 36;
            label89.Text = "Y:";
            // 
            // YnvAreaIDYUpDown
            // 
            YnvAreaIDYUpDown.Location = new System.Drawing.Point(202, 14);
            YnvAreaIDYUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvAreaIDYUpDown.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
            YnvAreaIDYUpDown.Name = "YnvAreaIDYUpDown";
            YnvAreaIDYUpDown.Size = new System.Drawing.Size(56, 23);
            YnvAreaIDYUpDown.TabIndex = 35;
            YnvAreaIDYUpDown.ValueChanged += YnvAreaIDYUpDown_ValueChanged;
            // 
            // label90
            // 
            label90.AutoSize = true;
            label90.Location = new System.Drawing.Point(79, 16);
            label90.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label90.Name = "label90";
            label90.Size = new System.Drawing.Size(17, 15);
            label90.TabIndex = 34;
            label90.Text = "X:";
            // 
            // YnvAreaIDXUpDown
            // 
            YnvAreaIDXUpDown.Location = new System.Drawing.Point(106, 14);
            YnvAreaIDXUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvAreaIDXUpDown.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
            YnvAreaIDXUpDown.Name = "YnvAreaIDXUpDown";
            YnvAreaIDXUpDown.Size = new System.Drawing.Size(56, 23);
            YnvAreaIDXUpDown.TabIndex = 33;
            YnvAreaIDXUpDown.ValueChanged += YnvAreaIDXUpDown_ValueChanged;
            // 
            // YnvAreaIDInfoLabel
            // 
            YnvAreaIDInfoLabel.AutoSize = true;
            YnvAreaIDInfoLabel.Location = new System.Drawing.Point(278, 16);
            YnvAreaIDInfoLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvAreaIDInfoLabel.Name = "YnvAreaIDInfoLabel";
            YnvAreaIDInfoLabel.Size = new System.Drawing.Size(30, 15);
            YnvAreaIDInfoLabel.TabIndex = 32;
            YnvAreaIDInfoLabel.Text = "ID: 0";
            // 
            // label92
            // 
            label92.AutoSize = true;
            label92.Location = new System.Drawing.Point(19, 16);
            label92.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label92.Name = "label92";
            label92.Size = new System.Drawing.Size(48, 15);
            label92.TabIndex = 31;
            label92.Text = "Area ID:";
            // 
            // YnvVertexCountLabel
            // 
            YnvVertexCountLabel.AutoSize = true;
            YnvVertexCountLabel.Location = new System.Drawing.Point(418, 63);
            YnvVertexCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvVertexCountLabel.Name = "YnvVertexCountLabel";
            YnvVertexCountLabel.Size = new System.Drawing.Size(85, 15);
            YnvVertexCountLabel.TabIndex = 39;
            YnvVertexCountLabel.Text = "Vertex count: 0";
            // 
            // YnvPolyCountLabel
            // 
            YnvPolyCountLabel.AutoSize = true;
            YnvPolyCountLabel.Location = new System.Drawing.Point(418, 84);
            YnvPolyCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvPolyCountLabel.Name = "YnvPolyCountLabel";
            YnvPolyCountLabel.Size = new System.Drawing.Size(76, 15);
            YnvPolyCountLabel.TabIndex = 40;
            YnvPolyCountLabel.Text = "Poly count: 0";
            // 
            // YnvPortalCountLabel
            // 
            YnvPortalCountLabel.AutoSize = true;
            YnvPortalCountLabel.Location = new System.Drawing.Point(418, 105);
            YnvPortalCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvPortalCountLabel.Name = "YnvPortalCountLabel";
            YnvPortalCountLabel.Size = new System.Drawing.Size(84, 15);
            YnvPortalCountLabel.TabIndex = 41;
            YnvPortalCountLabel.Text = "Portal count: 0";
            // 
            // YnvPortalLinkCountLabel
            // 
            YnvPortalLinkCountLabel.AutoSize = true;
            YnvPortalLinkCountLabel.Location = new System.Drawing.Point(418, 126);
            YnvPortalLinkCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvPortalLinkCountLabel.Name = "YnvPortalLinkCountLabel";
            YnvPortalLinkCountLabel.Size = new System.Drawing.Size(106, 15);
            YnvPortalLinkCountLabel.TabIndex = 42;
            YnvPortalLinkCountLabel.Text = "Portal link count: 0";
            // 
            // YnvPointCountLabel
            // 
            YnvPointCountLabel.AutoSize = true;
            YnvPointCountLabel.Location = new System.Drawing.Point(418, 147);
            YnvPointCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvPointCountLabel.Name = "YnvPointCountLabel";
            YnvPointCountLabel.Size = new System.Drawing.Size(81, 15);
            YnvPointCountLabel.TabIndex = 43;
            YnvPointCountLabel.Text = "Point count: 0";
            // 
            // YnvByteCountLabel
            // 
            YnvByteCountLabel.AutoSize = true;
            YnvByteCountLabel.Location = new System.Drawing.Point(418, 167);
            YnvByteCountLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            YnvByteCountLabel.Name = "YnvByteCountLabel";
            YnvByteCountLabel.Size = new System.Drawing.Size(76, 15);
            YnvByteCountLabel.TabIndex = 44;
            YnvByteCountLabel.Text = "Byte count: 0";
            // 
            // YnvFlagsGroupBox
            // 
            YnvFlagsGroupBox.Controls.Add(YnvFlagsUnknown16CheckBox);
            YnvFlagsGroupBox.Controls.Add(YnvFlagsUnknown8CheckBox);
            YnvFlagsGroupBox.Controls.Add(YnvFlagsVehicleCheckBox);
            YnvFlagsGroupBox.Controls.Add(YnvFlagsPortalsCheckBox);
            YnvFlagsGroupBox.Controls.Add(YnvFlagsPolygonsCheckBox);
            YnvFlagsGroupBox.Location = new System.Drawing.Point(288, 55);
            YnvFlagsGroupBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsGroupBox.Name = "YnvFlagsGroupBox";
            YnvFlagsGroupBox.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsGroupBox.Size = new System.Drawing.Size(120, 133);
            YnvFlagsGroupBox.TabIndex = 45;
            YnvFlagsGroupBox.TabStop = false;
            YnvFlagsGroupBox.Text = "Content flags";
            // 
            // YnvFlagsUnknown16CheckBox
            // 
            YnvFlagsUnknown16CheckBox.AutoSize = true;
            YnvFlagsUnknown16CheckBox.Location = new System.Drawing.Point(14, 110);
            YnvFlagsUnknown16CheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsUnknown16CheckBox.Name = "YnvFlagsUnknown16CheckBox";
            YnvFlagsUnknown16CheckBox.Size = new System.Drawing.Size(48, 19);
            YnvFlagsUnknown16CheckBox.TabIndex = 4;
            YnvFlagsUnknown16CheckBox.Text = "DLC";
            YnvFlagsUnknown16CheckBox.UseVisualStyleBackColor = true;
            YnvFlagsUnknown16CheckBox.CheckedChanged += YnvFlagsUnknown16CheckBox_CheckedChanged;
            // 
            // YnvFlagsUnknown8CheckBox
            // 
            YnvFlagsUnknown8CheckBox.AutoSize = true;
            YnvFlagsUnknown8CheckBox.Location = new System.Drawing.Point(14, 88);
            YnvFlagsUnknown8CheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsUnknown8CheckBox.Name = "YnvFlagsUnknown8CheckBox";
            YnvFlagsUnknown8CheckBox.Size = new System.Drawing.Size(57, 19);
            YnvFlagsUnknown8CheckBox.TabIndex = 3;
            YnvFlagsUnknown8CheckBox.Text = "Water";
            YnvFlagsUnknown8CheckBox.UseVisualStyleBackColor = true;
            YnvFlagsUnknown8CheckBox.CheckedChanged += YnvFlagsUnknown8CheckBox_CheckedChanged;
            // 
            // YnvFlagsVehicleCheckBox
            // 
            YnvFlagsVehicleCheckBox.AutoSize = true;
            YnvFlagsVehicleCheckBox.Location = new System.Drawing.Point(14, 66);
            YnvFlagsVehicleCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsVehicleCheckBox.Name = "YnvFlagsVehicleCheckBox";
            YnvFlagsVehicleCheckBox.Size = new System.Drawing.Size(63, 19);
            YnvFlagsVehicleCheckBox.TabIndex = 2;
            YnvFlagsVehicleCheckBox.Text = "Vehicle";
            YnvFlagsVehicleCheckBox.UseVisualStyleBackColor = true;
            YnvFlagsVehicleCheckBox.CheckedChanged += YnvFlagsVehicleCheckBox_CheckedChanged;
            // 
            // YnvFlagsPortalsCheckBox
            // 
            YnvFlagsPortalsCheckBox.AutoSize = true;
            YnvFlagsPortalsCheckBox.Location = new System.Drawing.Point(14, 44);
            YnvFlagsPortalsCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsPortalsCheckBox.Name = "YnvFlagsPortalsCheckBox";
            YnvFlagsPortalsCheckBox.Size = new System.Drawing.Size(62, 19);
            YnvFlagsPortalsCheckBox.TabIndex = 1;
            YnvFlagsPortalsCheckBox.Text = "Portals";
            YnvFlagsPortalsCheckBox.UseVisualStyleBackColor = true;
            YnvFlagsPortalsCheckBox.CheckedChanged += YnvFlagsPortalsCheckBox_CheckedChanged;
            // 
            // YnvFlagsPolygonsCheckBox
            // 
            YnvFlagsPolygonsCheckBox.AutoSize = true;
            YnvFlagsPolygonsCheckBox.Location = new System.Drawing.Point(14, 22);
            YnvFlagsPolygonsCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvFlagsPolygonsCheckBox.Name = "YnvFlagsPolygonsCheckBox";
            YnvFlagsPolygonsCheckBox.Size = new System.Drawing.Size(75, 19);
            YnvFlagsPolygonsCheckBox.TabIndex = 0;
            YnvFlagsPolygonsCheckBox.Text = "Polygons";
            YnvFlagsPolygonsCheckBox.UseVisualStyleBackColor = true;
            YnvFlagsPolygonsCheckBox.CheckedChanged += YnvFlagsVerticesCheckBox_CheckedChanged;
            // 
            // YnvVersionUnkHashTextBox
            // 
            YnvVersionUnkHashTextBox.Location = new System.Drawing.Point(106, 208);
            YnvVersionUnkHashTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvVersionUnkHashTextBox.Name = "YnvVersionUnkHashTextBox";
            YnvVersionUnkHashTextBox.Size = new System.Drawing.Size(160, 23);
            YnvVersionUnkHashTextBox.TabIndex = 46;
            YnvVersionUnkHashTextBox.TextChanged += YnvVersionUnkHashTextBox_TextChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(27, 211);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(64, 15);
            label1.TabIndex = 47;
            label1.Text = "Unk hash?:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(274, 211);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(199, 15);
            label2.TabIndex = 48;
            label2.Text = "(2244687201 for global, 0 for vehicle)";
            // 
            // label78
            // 
            label78.AutoSize = true;
            label78.Location = new System.Drawing.Point(19, 250);
            label78.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label78.Name = "label78";
            label78.Size = new System.Drawing.Size(74, 15);
            label78.TabIndex = 53;
            label78.Text = "Adj Area IDs:";
            // 
            // YnvAdjAreaIDsTextBox
            // 
            YnvAdjAreaIDsTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            YnvAdjAreaIDsTextBox.Location = new System.Drawing.Point(106, 247);
            YnvAdjAreaIDsTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvAdjAreaIDsTextBox.Multiline = true;
            YnvAdjAreaIDsTextBox.Name = "YnvAdjAreaIDsTextBox";
            YnvAdjAreaIDsTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            YnvAdjAreaIDsTextBox.Size = new System.Drawing.Size(249, 160);
            YnvAdjAreaIDsTextBox.TabIndex = 52;
            YnvAdjAreaIDsTextBox.WordWrap = false;
            YnvAdjAreaIDsTextBox.TextChanged += YnvAdjAreaIDsTextBox_TextChanged;
            // 
            // label48
            // 
            label48.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            label48.AutoSize = true;
            label48.Location = new System.Drawing.Point(20, 459);
            label48.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label48.Name = "label48";
            label48.Size = new System.Drawing.Size(74, 15);
            label48.TabIndex = 57;
            label48.Text = "Project Path:";
            // 
            // YnvProjectPathTextBox
            // 
            YnvProjectPathTextBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            YnvProjectPathTextBox.Location = new System.Drawing.Point(106, 456);
            YnvProjectPathTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvProjectPathTextBox.Name = "YnvProjectPathTextBox";
            YnvProjectPathTextBox.ReadOnly = true;
            YnvProjectPathTextBox.Size = new System.Drawing.Size(548, 23);
            YnvProjectPathTextBox.TabIndex = 56;
            // 
            // label47
            // 
            label47.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            label47.AutoSize = true;
            label47.Location = new System.Drawing.Point(38, 429);
            label47.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label47.Name = "label47";
            label47.Size = new System.Drawing.Size(55, 15);
            label47.TabIndex = 55;
            label47.Text = "Rpf Path:";
            // 
            // YnvRpfPathTextBox
            // 
            YnvRpfPathTextBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            YnvRpfPathTextBox.Location = new System.Drawing.Point(106, 426);
            YnvRpfPathTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            YnvRpfPathTextBox.Name = "YnvRpfPathTextBox";
            YnvRpfPathTextBox.ReadOnly = true;
            YnvRpfPathTextBox.Size = new System.Drawing.Size(548, 23);
            YnvRpfPathTextBox.TabIndex = 54;
            // 
            // EditYnvPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(678, 495);
            Controls.Add(label48);
            Controls.Add(YnvProjectPathTextBox);
            Controls.Add(label47);
            Controls.Add(YnvRpfPathTextBox);
            Controls.Add(label78);
            Controls.Add(YnvAdjAreaIDsTextBox);
            Controls.Add(label2);
            Controls.Add(YnvVersionUnkHashTextBox);
            Controls.Add(label1);
            Controls.Add(YnvFlagsGroupBox);
            Controls.Add(YnvByteCountLabel);
            Controls.Add(YnvPointCountLabel);
            Controls.Add(YnvPortalLinkCountLabel);
            Controls.Add(YnvPortalCountLabel);
            Controls.Add(YnvPolyCountLabel);
            Controls.Add(YnvVertexCountLabel);
            Controls.Add(YnvAABBSizeTextBox);
            Controls.Add(label91);
            Controls.Add(label89);
            Controls.Add(YnvAreaIDYUpDown);
            Controls.Add(label90);
            Controls.Add(YnvAreaIDXUpDown);
            Controls.Add(YnvAreaIDInfoLabel);
            Controls.Add(label92);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "EditYnvPanel";
            Text = "Edit Ynv";
            ((System.ComponentModel.ISupportInitialize)YnvAreaIDYUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)YnvAreaIDXUpDown).EndInit();
            YnvFlagsGroupBox.ResumeLayout(false);
            YnvFlagsGroupBox.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox YnvAABBSizeTextBox;
        private System.Windows.Forms.Label label91;
        private System.Windows.Forms.Label label89;
        private System.Windows.Forms.NumericUpDown YnvAreaIDYUpDown;
        private System.Windows.Forms.Label label90;
        private System.Windows.Forms.NumericUpDown YnvAreaIDXUpDown;
        private System.Windows.Forms.Label YnvAreaIDInfoLabel;
        private System.Windows.Forms.Label label92;
        private System.Windows.Forms.Label YnvVertexCountLabel;
        private System.Windows.Forms.Label YnvPolyCountLabel;
        private System.Windows.Forms.Label YnvPortalCountLabel;
        private System.Windows.Forms.Label YnvPortalLinkCountLabel;
        private System.Windows.Forms.Label YnvPointCountLabel;
        private System.Windows.Forms.Label YnvByteCountLabel;
        private System.Windows.Forms.GroupBox YnvFlagsGroupBox;
        private System.Windows.Forms.CheckBox YnvFlagsUnknown8CheckBox;
        private System.Windows.Forms.CheckBox YnvFlagsVehicleCheckBox;
        private System.Windows.Forms.CheckBox YnvFlagsPortalsCheckBox;
        private System.Windows.Forms.CheckBox YnvFlagsPolygonsCheckBox;
        private System.Windows.Forms.TextBox YnvVersionUnkHashTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label78;
        private WinForms.TextBoxFix YnvAdjAreaIDsTextBox;
        private System.Windows.Forms.Label label48;
        private System.Windows.Forms.TextBox YnvProjectPathTextBox;
        private System.Windows.Forms.Label label47;
        private System.Windows.Forms.TextBox YnvRpfPathTextBox;
        private System.Windows.Forms.CheckBox YnvFlagsUnknown16CheckBox;
    }
}