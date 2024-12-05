using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Utils;

namespace CodeWalker.Tools;

public partial class ExtractTexForm : Form
{
    private volatile bool AbortOperation;
    private volatile bool InProgress;
    private volatile bool KeysLoaded;

    public ExtractTexForm()
    {
        InitializeComponent();
    }

    private void ExtractTexForm_Load(object sender, EventArgs e)
    {
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
        OutputFolderTextBox.Text = Settings.Default.ExtractedTexturesFolder;

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

    private void OutputFolderTextBox_TextChanged(object sender, EventArgs e)
    {
        Settings.Default.ExtractedTexturesFolder = OutputFolderTextBox.Text;
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
        var bytd = YtdChecBox.Checked;
        var bydr = YdrCheckBox.Checked;
        var bydd = YddCheckBox.Checked;
        var byft = YftCheckBox.Checked;

        Task.Run(() =>
        {
            UpdateExtractStatus("Keys loaded.");


            var rpfman = new RpfManager();
            rpfman.Init(searchpath, UpdateExtractStatus, UpdateExtractStatus);


            UpdateExtractStatus("Beginning texture extraction...");
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
                    if (bytd && entry.NameLower.EndsWith(".ytd"))
                    {
                        UpdateExtractStatus(entry.Path);
                        var ytd = rpfman.GetFile<YtdFile>(entry);
                        if (ytd == null) throw new Exception("Couldn't load file.");
                        if (ytd.TextureDict == null) throw new Exception("Couldn't load texture dictionary.");
                        if (ytd.TextureDict.Textures == null)
                            throw new Exception("Couldn't load texture dictionary texture array.");
                        if (ytd.TextureDict.Textures.data_items == null)
                            throw new Exception("Texture dictionary had no entries...");
                        foreach (var tex in ytd.TextureDict.Textures.data_items) SaveTexture(tex, entry, outputpath);
                    }
                    else if (bydr && entry.NameLower.EndsWith(".ydr"))
                    {
                        UpdateExtractStatus(entry.Path);
                        var ydr = rpfman.GetFile<YdrFile>(entry);
                        if (ydr == null) throw new Exception("Couldn't load file.");
                        if (ydr.Drawable == null) throw new Exception("Couldn't load drawable.");
                        if (ydr.Drawable.ShaderGroup != null)
                        {
                            var ydrtd = ydr.Drawable.ShaderGroup.TextureDictionary;
                            if (ydrtd != null && ydrtd.Textures != null && ydrtd.Textures.data_items != null)
                                foreach (var tex in ydrtd.Textures.data_items)
                                    SaveTexture(tex, entry, outputpath);
                        }
                    }
                    else if (bydd && entry.NameLower.EndsWith(".ydd"))
                    {
                        UpdateExtractStatus(entry.Path);
                        var ydd = rpfman.GetFile<YddFile>(entry);
                        if (ydd == null) throw new Exception("Couldn't load file.");
                        //if (ydd.DrawableDict == null) throw new Exception("Couldn't load drawable dictionary.");
                        //if (ydd.DrawableDict.Drawables == null) throw new Exception("Drawable dictionary had no items...");
                        //if (ydd.DrawableDict.Drawables.data_items == null) throw new Exception("Drawable dictionary had no items...");
                        if (ydd.Dict == null || ydd.Dict.Count == 0)
                            throw new Exception("Drawable dictionary had no items...");
                        foreach (var drawable in ydd.Dict.Values)
                            if (drawable.ShaderGroup != null)
                            {
                                var ydrtd = drawable.ShaderGroup.TextureDictionary;
                                if (ydrtd != null && ydrtd.Textures != null && ydrtd.Textures.data_items != null)
                                    foreach (var tex in ydrtd.Textures.data_items)
                                        SaveTexture(tex, entry, outputpath);
                            }
                    }
                    else if (byft && entry.NameLower.EndsWith(".yft"))
                    {
                        UpdateExtractStatus(entry.Path);
                        var yft = rpfman.GetFile<YftFile>(entry);
                        if (yft == null) throw new Exception("Couldn't load file.");
                        if (yft.Fragment == null) throw new Exception("Couldn't load fragment.");
                        if (yft.Fragment.Drawable != null)
                            if (yft.Fragment.Drawable.ShaderGroup != null)
                            {
                                var ydrtd = yft.Fragment.Drawable.ShaderGroup.TextureDictionary;
                                if (ydrtd != null && ydrtd.Textures != null && ydrtd.Textures.data_items != null)
                                    foreach (var tex in ydrtd.Textures.data_items)
                                        SaveTexture(tex, entry, outputpath);
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

    private void SaveTexture(Texture tex, RpfEntry entry, string folder)
    {
        //DirectXTex

        var dds = DDSIO.GetDDSFile(tex);

        var bpath = folder + "\\" + entry.Name + "_" + tex.Name;
        var fpath = bpath + ".dds";
        var c = 1;
        while (File.Exists(fpath))
        {
            fpath = bpath + "_Copy" + c + ".dds";
            c++;
        }

        File.WriteAllBytes(fpath, dds);
    }


    private void AbortButton_Click(object sender, EventArgs e)
    {
        AbortOperation = true;
    }
}