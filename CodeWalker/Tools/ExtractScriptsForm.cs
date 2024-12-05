using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Properties;

namespace CodeWalker.Tools;

public partial class ExtractScriptsForm : Form
{
    private volatile bool AbortOperation;
    private volatile bool InProgress;
    private volatile bool KeysLoaded;


    public ExtractScriptsForm()
    {
        InitializeComponent();
    }

    private void ExtractForm_Load(object sender, EventArgs e)
    {
        DumpTextBox.Text = Settings.Default.GTAExeDumpFile;
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
        OutputFolderTextBox.Text = Settings.Default.CompiledScriptFolder;

        try
        {
            GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, Settings.Default.Key);
            KeysLoaded = true;
            UpdateDumpStatus("Ready.");
            UpdateExtractStatus("Ready to extract.");
        }
        catch
        {
            UpdateDumpStatus("Keys not found! This shouldn't happen.");
        }
    }

    private void DumpTextBox_TextChanged(object sender, EventArgs e)
    {
        Settings.Default.GTAExeDumpFile = DumpTextBox.Text;
    }

    private void OutputFolderTextBox_TextChanged(object sender, EventArgs e)
    {
        Settings.Default.CompiledScriptFolder = OutputFolderTextBox.Text;
    }

    private void FolderBrowseButton_Click(object sender, EventArgs e)
    {
        GTAFolder.UpdateGTAFolder();
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
    }

    private void OutputFolderBrowseButton_Click(object sender, EventArgs e)
    {
        FolderBrowserDialog.SelectedPath = OutputFolderTextBox.Text;
        var res = FolderBrowserDialog.ShowDialog();
        if (res == DialogResult.OK) OutputFolderTextBox.Text = FolderBrowserDialog.SelectedPath;
    }

    private void DumpBrowseButton_Click(object sender, EventArgs e)
    {
        var res = OpenFileDialog.ShowDialog();
        if (res == DialogResult.OK) DumpTextBox.Text = OpenFileDialog.FileName;
    }

    private void FindKeysButton_Click(object sender, EventArgs e)
    {
        if (InProgress) return;
        if (KeysLoaded)
            if (MessageBox.Show("Keys are already loaded. Do you wish to scan the exe dump anyway?",
                    "Keys already loaded", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;


        InProgress = true;
        AbortOperation = false;

        var dmppath = DumpTextBox.Text;

        Task.Run(() =>
        {
            try
            {
                if (AbortOperation)
                {
                    UpdateDumpStatus("Dump scan aborted.");
                    return;
                }

                var dmpfi = new FileInfo(dmppath);

                UpdateDumpStatus(string.Format("Scanning {0} for keys...", dmpfi.Name));


                var exedat = File.ReadAllBytes(dmppath);
                GTA5Keys.Generate(exedat, UpdateDumpStatus);


                UpdateDumpStatus("Saving found keys...");

                GTA5Keys.SaveToPath();

                UpdateDumpStatus("Keys loaded.");
                UpdateExtractStatus("Keys loaded, ready to extract.");
                KeysLoaded = true;
                InProgress = false;
            }
            catch (Exception ex)
            {
                UpdateDumpStatus("Error - " + ex);

                InProgress = false;
            }
        });
    }

    private void ExtractScriptsButton_Click(object sender, EventArgs e)
    {
        if (InProgress) return;

        if (!KeysLoaded)
        {
            MessageBox.Show("Please scan a GTA 5 exe dump for keys first, or include key files in this app's folder!");
            return;
        }

        if (!Directory.Exists(FolderTextBox.Text))
        {
            MessageBox.Show("Folder doesn't exist: " + FolderTextBox.Text);
            return;
        }

        if (!Directory.Exists(OutputFolderTextBox.Text))
        {
            MessageBox.Show("Folder doesn't exist: " + OutputFolderTextBox.Text);
            return;
        }

        if (Directory.GetFiles(OutputFolderTextBox.Text, "*.ysc", SearchOption.AllDirectories).Length > 0)
            if (MessageBox.Show("Output folder already contains .ysc files. Are you sure you want to continue?",
                    "Output folder already contains .ysc files", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

        InProgress = true;
        AbortOperation = false;

        var searchpath = FolderTextBox.Text;
        var outputpath = OutputFolderTextBox.Text;
        var replpath = searchpath + "\\";

        Task.Run(() =>
        {
            UpdateExtractStatus("Keys loaded.");

            var allfiles = Directory.GetFiles(searchpath, "*.rpf", SearchOption.AllDirectories);
            foreach (var rpfpath in allfiles)
            {
                var rf = new RpfFile(rpfpath, rpfpath.Replace(replpath, ""));
                UpdateExtractStatus("Searching " + rf.Name + "...");
                rf.ExtractScripts(outputpath, UpdateExtractStatus);
            }

            UpdateExtractStatus("Complete.");
            InProgress = false;
        });
    }


    private void UpdateDumpStatus(string text)
    {
        try
        {
            if (InvokeRequired)
                Invoke(() => { UpdateDumpStatus(text); });
            else
                DumpStatusLabel.Text = text;
        }
        catch
        {
        }
    }

    private void UpdateExtractStatus(string text)
    {
        try
        {
            if (InvokeRequired)
                Invoke(() => { UpdateExtractStatus(text); });
            else
                ExtractStatusLabel.Text = text;
        }
        catch
        {
        }
    }
}