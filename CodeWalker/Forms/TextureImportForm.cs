using CodeWalker.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodeWalker.Forms
{
    public partial class TextureImportForm : Form
    {
        private string _imagePath = string.Empty;
        private int _imageWidth;
        private int _imageHeight;
        private bool _hasAlpha;
        public TextureCompressionFormat SelectedFormat { get; private set; } = TextureCompressionFormat.DXT5;
        public TextureCompressionQuality SelectedQuality { get; private set; } = TextureCompressionQuality.Normal;
        public bool GenerateMipmaps { get; private set; } = true;
        public bool UseCuda { get; private set; } = true;
        public int MinMipmapSize { get; private set; } = 0;

        public TextureImportForm()
        {
            InitializeComponent();
        }

        public TextureImportForm(string imagePath) : this()
        {
            _imagePath = imagePath;
            LoadImagePreview();
        }

        private void LoadImagePreview()
        {
            if (string.IsNullOrEmpty(_imagePath) || !File.Exists(_imagePath))
            {
                return;
            }

            try
            {
                // Load image info using ImageSharp
                using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(_imagePath);
                _imageWidth = image.Width;
                _imageHeight = image.Height;

                // Check for alpha
                _hasAlpha = false;
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height && !_hasAlpha; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            if (row[x].A < 255)
                            {
                                _hasAlpha = true;
                                break;
                            }
                        }
                    }
                });

                // Create preview bitmap
                var bitmap = new Bitmap(_imageWidth, _imageHeight, PixelFormat.Format32bppArgb);
                image.ProcessPixelRows(accessor =>
                {
                    var bmpData = bitmap.LockBits(
                        new System.Drawing.Rectangle(0, 0, _imageWidth, _imageHeight),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb);

                    try
                    {
                        for (int y = 0; y < accessor.Height; y++)
                        {
                            var srcRow = accessor.GetRowSpan(y);
                            IntPtr destPtr = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);

                            // Convert BGRA to ARGB for GDI+
                            byte[] rowData = new byte[_imageWidth * 4];
                            for (int x = 0; x < _imageWidth; x++)
                            {
                                rowData[x * 4 + 0] = srcRow[x].B;
                                rowData[x * 4 + 1] = srcRow[x].G;
                                rowData[x * 4 + 2] = srcRow[x].R;
                                rowData[x * 4 + 3] = srcRow[x].A;
                            }
                            Marshal.Copy(rowData, 0, destPtr, rowData.Length);
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bmpData);
                    }
                });

                PreviewPictureBox.Image = bitmap;

                // Update labels
                FileNameLabel.Text = Path.GetFileName(_imagePath);
                DimensionsLabel.Text = $"{_imageWidth} x {_imageHeight}";
                HasAlphaLabel.Text = _hasAlpha ? "Yes" : "No";

                // Suggest format based on image content
                var suggestedFormat = TextureCompressor.SuggestFormat(_imagePath);
                FormatComboBox.SelectedItem = GetFormatDisplayName(suggestedFormat);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image preview: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(PreviewPictureBox), nameof(FileNameLabel), nameof(DimensionsLabel), nameof(HasAlphaLabel), nameof(FormatComboBox), nameof(QualityComboBox), nameof(GenerateMipmapsCheckBox), nameof(MinMipmapSizeCheckBox), nameof(UseCudaCheckBox), nameof(ImportButton), nameof(CancelImportButton), nameof(label1), nameof(label2), nameof(label3), nameof(label4), nameof(label5), nameof(label6))]
        private void InitializeComponent()
        {
            this.PreviewPictureBox = new PictureBox();
            this.FileNameLabel = new Label();
            this.DimensionsLabel = new Label();
            this.HasAlphaLabel = new Label();
            this.FormatComboBox = new ComboBox();
            this.QualityComboBox = new ComboBox();
            this.GenerateMipmapsCheckBox = new CheckBox();
            this.MinMipmapSizeCheckBox = new CheckBox();
            this.UseCudaCheckBox = new CheckBox();
            this.ImportButton = new Button();
            this.CancelImportButton = new Button();
            this.label1 = new Label();
            this.label2 = new Label();
            this.label3 = new Label();
            this.label4 = new Label();
            this.label5 = new Label();
            this.label6 = new Label();

            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).BeginInit();
            this.SuspendLayout();

            // PreviewPictureBox
            this.PreviewPictureBox.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.PreviewPictureBox.BorderStyle = BorderStyle.FixedSingle;
            this.PreviewPictureBox.Location = new System.Drawing.Point(12, 12);
            this.PreviewPictureBox.Name = "PreviewPictureBox";
            this.PreviewPictureBox.Size = new System.Drawing.Size(200, 200);
            this.PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            this.PreviewPictureBox.TabIndex = 0;
            this.PreviewPictureBox.TabStop = false;

            // label1 - Filename label
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(230, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 15);
            this.label1.Text = "Filename:";

            // FileNameLabel
            this.FileNameLabel.AutoSize = true;
            this.FileNameLabel.Location = new System.Drawing.Point(300, 12);
            this.FileNameLabel.Name = "FileNameLabel";
            this.FileNameLabel.Size = new System.Drawing.Size(40, 15);
            this.FileNameLabel.Text = "-";

            // label2 - Dimensions label
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(230, 32);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(70, 15);
            this.label2.Text = "Dimensions:";

            // DimensionsLabel
            this.DimensionsLabel.AutoSize = true;
            this.DimensionsLabel.Location = new System.Drawing.Point(300, 32);
            this.DimensionsLabel.Name = "DimensionsLabel";
            this.DimensionsLabel.Size = new System.Drawing.Size(40, 15);
            this.DimensionsLabel.Text = "-";

            // label3 - Has Alpha label
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(230, 52);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(60, 15);
            this.label3.Text = "Has Alpha:";

            // HasAlphaLabel
            this.HasAlphaLabel.AutoSize = true;
            this.HasAlphaLabel.Location = new System.Drawing.Point(300, 52);
            this.HasAlphaLabel.Name = "HasAlphaLabel";
            this.HasAlphaLabel.Size = new System.Drawing.Size(40, 15);
            this.HasAlphaLabel.Text = "-";

            // label4 - Format label
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(230, 90);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(120, 15);
            this.label4.Text = "Compression Format:";

            // FormatComboBox
            this.FormatComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.FormatComboBox.Location = new System.Drawing.Point(230, 108);
            this.FormatComboBox.Name = "FormatComboBox";
            this.FormatComboBox.Size = new System.Drawing.Size(200, 23);
            this.FormatComboBox.Items.AddRange(new object[] {
                "DXT1 (BC1) - RGB, no alpha",
                "DXT1a (BC1) - RGB, 1-bit alpha",
                "DXT3 (BC2) - RGBA, explicit alpha",
                "DXT5 (BC3) - RGBA, smooth alpha",
                "BC4 - Single channel (grayscale)",
                "BC5 - Two channels (normal maps)",
                "BC7 - High quality RGBA",
                "Uncompressed - A8R8G8B8"
            });
            this.FormatComboBox.SelectedIndex = 3; // DXT5 default

            // label5 - Quality label
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(230, 140);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(50, 15);
            this.label5.Text = "Quality:";

            // QualityComboBox
            this.QualityComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.QualityComboBox.Location = new System.Drawing.Point(230, 158);
            this.QualityComboBox.Name = "QualityComboBox";
            this.QualityComboBox.Size = new System.Drawing.Size(200, 23);
            this.QualityComboBox.Items.AddRange(new object[] {
                "Fastest",
                "Normal",
                "Production",
                "Highest"
            });
            this.QualityComboBox.SelectedIndex = 1; // Normal default

            // GenerateMipmapsCheckBox
            this.GenerateMipmapsCheckBox.AutoSize = true;
            this.GenerateMipmapsCheckBox.Checked = true;
            this.GenerateMipmapsCheckBox.CheckState = CheckState.Checked;
            this.GenerateMipmapsCheckBox.Location = new System.Drawing.Point(230, 190);
            this.GenerateMipmapsCheckBox.Name = "GenerateMipmapsCheckBox";
            this.GenerateMipmapsCheckBox.Size = new System.Drawing.Size(120, 19);
            this.GenerateMipmapsCheckBox.Text = "Generate Mipmaps";

            // MinMipmapSizeCheckBox
            this.MinMipmapSizeCheckBox.AutoSize = true;
            this.MinMipmapSizeCheckBox.Checked = true;
            this.MinMipmapSizeCheckBox.CheckState = CheckState.Checked;
            this.MinMipmapSizeCheckBox.Location = new System.Drawing.Point(230, 212);
            this.MinMipmapSizeCheckBox.Name = "MinMipmapSizeCheckBox";
            this.MinMipmapSizeCheckBox.Size = new System.Drawing.Size(183, 19);
            this.MinMipmapSizeCheckBox.Text = "Minimum mipmap size: 4x4";

            // UseCudaCheckBox
            this.UseCudaCheckBox.AutoSize = true;
            this.UseCudaCheckBox.Checked = true;
            this.UseCudaCheckBox.CheckState = CheckState.Checked;
            this.UseCudaCheckBox.Location = new System.Drawing.Point(230, 234);
            this.UseCudaCheckBox.Name = "UseCudaCheckBox";
            this.UseCudaCheckBox.Size = new System.Drawing.Size(170, 19);
            this.UseCudaCheckBox.Text = "Use CUDA Acceleration";

            // CancelImportButton
            this.CancelImportButton.DialogResult = DialogResult.Cancel;
            this.CancelImportButton.Location = new System.Drawing.Point(230, 267);
            this.CancelImportButton.Name = "CancelImportButton";
            this.CancelImportButton.Size = new System.Drawing.Size(90, 30);
            this.CancelImportButton.Text = "Cancel";
            this.CancelImportButton.UseVisualStyleBackColor = true;

            // ImportButton
            this.ImportButton.Location = new System.Drawing.Point(340, 267);
            this.ImportButton.Name = "ImportButton";
            this.ImportButton.Size = new System.Drawing.Size(90, 30);
            this.ImportButton.Text = "Import";
            this.ImportButton.UseVisualStyleBackColor = true;
            this.ImportButton.Click += new EventHandler(this.ImportButton_Click);

            // TextureImportForm
            this.AcceptButton = this.ImportButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.CancelImportButton;
            this.ClientSize = new System.Drawing.Size(450, 312);
            this.Controls.Add(this.ImportButton);
            this.Controls.Add(this.CancelImportButton);
            this.Controls.Add(this.UseCudaCheckBox);
            this.Controls.Add(this.MinMipmapSizeCheckBox);
            this.Controls.Add(this.GenerateMipmapsCheckBox);
            this.Controls.Add(this.QualityComboBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.FormatComboBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.HasAlphaLabel);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.DimensionsLabel);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.FileNameLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.PreviewPictureBox);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TextureImportForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Import Texture";

            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ImportButton_Click(object? sender, EventArgs e)
        {
            // Get selected format
            SelectedFormat = GetFormatFromIndex(FormatComboBox.SelectedIndex);

            // Get selected quality
            SelectedQuality = (TextureCompressionQuality)QualityComboBox.SelectedIndex;

            // Get mipmap setting
            GenerateMipmaps = GenerateMipmapsCheckBox.Checked;

            // Get minimum mipmap size setting
            MinMipmapSize = MinMipmapSizeCheckBox.Checked ? 4 : 0;

            // Get CUDA setting
            UseCuda = UseCudaCheckBox.Checked;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private TextureCompressionFormat GetFormatFromIndex(int index)
        {
            return index switch
            {
                0 => TextureCompressionFormat.DXT1,
                1 => TextureCompressionFormat.DXT1a,
                2 => TextureCompressionFormat.DXT3,
                3 => TextureCompressionFormat.DXT5,
                4 => TextureCompressionFormat.BC4,
                5 => TextureCompressionFormat.BC5,
                6 => TextureCompressionFormat.BC7,
                7 => TextureCompressionFormat.Uncompressed,
                _ => TextureCompressionFormat.DXT5
            };
        }

        private string GetFormatDisplayName(TextureCompressionFormat format)
        {
            return format switch
            {
                TextureCompressionFormat.DXT1 => "DXT1 (BC1) - RGB, no alpha",
                TextureCompressionFormat.DXT1a => "DXT1a (BC1) - RGB, 1-bit alpha",
                TextureCompressionFormat.DXT3 => "DXT3 (BC2) - RGBA, explicit alpha",
                TextureCompressionFormat.DXT5 => "DXT5 (BC3) - RGBA, smooth alpha",
                TextureCompressionFormat.BC4 => "BC4 - Single channel (grayscale)",
                TextureCompressionFormat.BC5 => "BC5 - Two channels (normal maps)",
                TextureCompressionFormat.BC7 => "BC7 - High quality RGBA",
                TextureCompressionFormat.Uncompressed => "Uncompressed - A8R8G8B8",
                _ => "DXT5 (BC3) - RGBA, smooth alpha"
            };
        }

        private PictureBox PreviewPictureBox;
        private Label FileNameLabel;
        private Label DimensionsLabel;
        private Label HasAlphaLabel;
        private ComboBox FormatComboBox;
        private ComboBox QualityComboBox;
        private CheckBox GenerateMipmapsCheckBox;
        private CheckBox MinMipmapSizeCheckBox;
        private CheckBox UseCudaCheckBox;
        private Button ImportButton;
        private Button CancelImportButton;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label label6;
    }
}
