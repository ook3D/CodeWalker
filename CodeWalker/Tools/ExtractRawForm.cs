using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Properties;

namespace CodeWalker.Tools;

public partial class ExtractRawForm : Form
{
    private volatile bool AbortOperation;
    private volatile bool InProgress;
    private volatile bool KeysLoaded;

    public ExtractRawForm()
    {
        InitializeComponent();
    }

    private void ExtractRawForm_Load(object sender, EventArgs e)
    {
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
        OutputFolderTextBox.Text = Settings.Default.ExtractedRawFilesFolder;

        try
        {
            GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, Settings.Default.Key);
            KeysLoaded = true;
            UpdateExtractStatus("Ready to extract.");
        }
        catch
        {
            UpdateExtractStatus("Keys not found! This shouldn't happen.");
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

    private void OutputFolderTextBox_TextChanged(object sender, EventArgs e)
    {
        Settings.Default.ExtractedRawFilesFolder = OutputFolderTextBox.Text;
    }

    private void FolderBrowseButton_Click(object sender, EventArgs e)
    {
        GTAFolder.UpdateGTAFolder();
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
    }

    private void OutputFolderBrowseButton_Click(object sender, EventArgs e)
    {
        FolderBrowserDialog.SelectedPath = OutputFolderTextBox.Text;
        var res = FolderBrowserDialog.ShowDialogNew();
        if (res == DialogResult.OK) OutputFolderTextBox.Text = FolderBrowserDialog.SelectedPath;
    }

    private void ExtractButton_Click(object sender, EventArgs e)
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

        if (string.IsNullOrEmpty(FileMatchTextBox.Text) || FileMatchTextBox.Text.Length < 3)
        {
            MessageBox.Show("Please enter at least 3 characters to match.");
            return;
        }

        InProgress = true;
        AbortOperation = false;

        var searchpath = FolderTextBox.Text;
        var outputpath = OutputFolderTextBox.Text;
        var replpath = searchpath + "\\";
        var matchstr = FileMatchTextBox.Text;
        var endswith = MatchEndsWithRadio.Checked;
        var compress = CompressCheckBox.Checked;

        Task.Run(() =>
        {
            UpdateExtractStatus("Keys loaded.");


            var rpfman = new RpfManager();
            rpfman.Init(searchpath, UpdateExtractStatus, UpdateExtractStatus);


            UpdateExtractStatus("Beginning file extraction...");
            var errsb = new StringBuilder();
            foreach (var rpf in rpfman.AllRpfs)
            foreach (var entry in rpf.AllEntries)
            {
                if (AbortOperation)
                {
                    UpdateExtractStatus("Operation aborted");
                    InProgress = false;
                    return;
                }

                try
                {
                    var extract = false;
                    if (endswith)
                        extract = entry.NameLower.EndsWith(matchstr);
                    else
                        extract = entry.NameLower.Contains(matchstr);
                    var fentry = entry as RpfFileEntry;
                    if (fentry == null) extract = false;
                    if (extract)
                    {
                        UpdateExtractStatus(entry.Path);

                        var data = entry.File.ExtractFile(fentry);

                        if (compress) data = ResourceBuilder.Compress(data);

                        var rrfe = fentry as RpfResourceFileEntry;
                        if (rrfe != null) //add resource header if this is a resource file.
                            data = ResourceBuilder.AddResourceHeader(rrfe, data);


                        if (data != null)
                        {
                            var finf = new FileInfo(entry.Name);
                            var bpath = outputpath + "\\" +
                                        entry.Name.Substring(0, entry.Name.Length - finf.Extension.Length);
                            var fpath = bpath + finf.Extension;
                            var c = 1;
                            while (File.Exists(fpath))
                            {
                                fpath = bpath + "_" + c + finf.Extension;
                                c++;
                            }

                            File.WriteAllBytes(fpath, data);
                        }
                        else
                        {
                            throw new Exception("Couldn't extract data.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    var err = entry.Name + ": " + ex.Message;
                    UpdateExtractStatus(err);
                    errsb.AppendLine(err);
                }
            }

            File.WriteAllText(outputpath + "\\_errors.txt", errsb.ToString());

            UpdateExtractStatus("Complete.");
            InProgress = false;
        });
    }

    private void AbortButton_Click(object sender, EventArgs e)
    {
        AbortOperation = true;
    }
}