using System;
using System.Windows.Forms;
using CodeWalker.Properties;

namespace CodeWalker.Utils;

public partial class SelectFolderForm : Form
{
    public SelectFolderForm()
    {
        InitializeComponent();
        SelectedFolder = GTAFolder.CurrentGTAFolder;
    }

    public string SelectedFolder { get; set; }
    public DialogResult Result { get; set; } = DialogResult.Cancel;

    private void SelectFolderForm_Load(object sender, EventArgs e)
    {
        FolderTextBox.Text = SelectedFolder;
        RememberFolderCheckbox.Checked = Settings.Default.RememberGTAFolder;
    }

    private void FolderBrowseButton_Click(object sender, EventArgs e)
    {
        FolderBrowserDialog.SelectedPath = FolderTextBox.Text;
        var res = FolderBrowserDialog.ShowDialog();
        if (res == DialogResult.OK) FolderTextBox.Text = FolderBrowserDialog.SelectedPath;
    }

    private void FolderTextBox_TextChanged(object sender, EventArgs e)
    {
        SelectedFolder = FolderTextBox.Text;
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void OkButton_Click(object sender, EventArgs e)
    {
        if (!GTAFolder.ValidateGTAFolder(SelectedFolder, out var failReason))
        {
            MessageBox.Show("The selected folder could not be used:\n\n" + failReason, "Invalid GTA Folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = DialogResult.OK;
        Close();
    }

    private void RememberFolderCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        Settings.Default.RememberGTAFolder = RememberFolderCheckbox.Checked;
    }
}