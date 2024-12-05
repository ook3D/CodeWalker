using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Properties;

namespace CodeWalker.Tools;

public partial class ExtractShadersForm : Form
{
    private volatile bool AbortOperation;
    private volatile bool InProgress;
    private volatile bool KeysLoaded;

    public ExtractShadersForm()
    {
        InitializeComponent();
    }

    private void ExtractShadersForm_Load(object sender, EventArgs e)
    {
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
        OutputFolderTextBox.Text = Settings.Default.ExtractedShadersFolder;

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

    private void FolderTextBox_TextChanged(object sender, EventArgs e)
    {
    }

    private void OutputFolderTextBox_TextChanged(object sender, EventArgs e)
    {
        Settings.Default.ExtractedShadersFolder = OutputFolderTextBox.Text;
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
        //if (Directory.GetFiles(OutputFolderTextBox.Text, "*.ysc", SearchOption.AllDirectories).Length > 0)
        //{
        //    if (MessageBox.Show("Output folder already contains .ysc files. Are you sure you want to continue?", "Output folder already contains .ysc files", MessageBoxButtons.OKCancel) != DialogResult.OK)
        //    {
        //        return;
        //    }
        //}

        InProgress = true;
        AbortOperation = false;

        var searchpath = FolderTextBox.Text;
        var outputpath = OutputFolderTextBox.Text;
        var replpath = searchpath + "\\";

        var cso = CsoCheckBox.Checked;
        var asm = AsmCheckBox.Checked;
        var meta = MetaCheckBox.Checked;

        Task.Run(() =>
        {
            UpdateExtractStatus("Keys loaded.");


            var rpfman = new RpfManager();
            rpfman.Init(searchpath, UpdateExtractStatus, UpdateExtractStatus);


            UpdateExtractStatus("Beginning shader extraction...");
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
                    if (entry.NameLower.EndsWith(".fxc"))
                    {
                        UpdateExtractStatus(entry.Path);
                        var fxc = rpfman.GetFile<FxcFile>(entry);
                        if (fxc == null) throw new Exception("Couldn't load file.");

                        var basepath = outputpath + "\\" + rpf.Name.Replace(".rpf", "");


                        if (!Directory.Exists(basepath)) Directory.CreateDirectory(basepath);

                        var pleft = entry.Path.Substring(0, entry.Path.Length - (entry.Name.Length + 1));
                        var ppart = pleft.Substring(pleft.LastIndexOf('\\'));
                        var opath = basepath + ppart;

                        if (!Directory.Exists(opath)) Directory.CreateDirectory(opath);

                        var obase = opath + "\\" + entry.Name;

                        foreach (var shader in fxc.Shaders)
                        {
                            var filebase = obase + "_" + shader.Name;
                            if (cso)
                            {
                                var csofile = filebase + ".cso";
                                File.WriteAllBytes(csofile, shader.ByteCode);
                            }

                            if (asm)
                            {
                                var asmfile = filebase + ".hlsl";
                                FxcParser.ParseShader(shader);
                                File.WriteAllText(asmfile, shader.Disassembly);
                            }
                        }

                        if (meta)
                        {
                            var metafile = obase + ".meta.txt";
                            var metastr = fxc.GetMetaString();
                            File.WriteAllText(metafile, metastr);
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