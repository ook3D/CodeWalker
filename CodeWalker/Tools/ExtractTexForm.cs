using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.Tools
{
    public partial class ExtractTexForm : Form
    {
        private volatile bool KeysLoaded = false;
        private volatile bool InProgress = false;
        private volatile bool AbortOperation = false;

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
                GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, GTAFolder.IsGen9, Settings.Default.Key);
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
            GTAFolder.UpdateGTAFolder(false);
            FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
        }

        private void OutputFolderBrowseButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog.SelectedPath = OutputFolderTextBox.Text;
            DialogResult res = FolderBrowserDialog.ShowDialogNew();
            if (res == DialogResult.OK)
            {
                OutputFolderTextBox.Text = FolderBrowserDialog.SelectedPath;
            }
        }


        private void UpdateExtractStatus(string text)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => { UpdateExtractStatus(text); }));
                }
                else
                {
                    ExtractStatusLabel.Text = text;
                }
            }
            catch { }
        }

        private async void ExtractButton_Click(object sender, EventArgs e)
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

            InProgress = true;
            AbortOperation = false;

            string searchpath = FolderTextBox.Text;
            string outputpath = OutputFolderTextBox.Text;
            bool bytd = YtdChecBox.Checked;
            bool bydr = YdrCheckBox.Checked;
            bool bydd = YddCheckBox.Checked;
            bool byft = YftCheckBox.Checked;
            bool gen9 = GTAFolder.IsGen9Folder(searchpath);

            await Task.Run(async () =>
            {
                UpdateExtractStatus("Keys loaded.");

                RpfManager rpfman = new RpfManager();
                rpfman.Init(searchpath, gen9, UpdateExtractStatus, UpdateExtractStatus);

                UpdateExtractStatus("Beginning texture extraction...");
                StringBuilder errsb = new StringBuilder();

                foreach (var rpf in rpfman.AllRpfs)
                {
                    if (AbortOperation)
                    {
                        UpdateExtractStatus("Operation aborted");
                        InProgress = false;
                        return;
                    }

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
                                await ProcessYtdFileAsync(entry, rpfman, outputpath);
                            }
                            else if (bydr && entry.NameLower.EndsWith(".ydr"))
                            {
                                await ProcessYdrFileAsync(entry, rpfman, outputpath);
                            }
                            else if (bydd && entry.NameLower.EndsWith(".ydd"))
                            {
                                await ProcessYddFileAsync(entry, rpfman, outputpath);
                            }
                            else if (byft && entry.NameLower.EndsWith(".yft"))
                            {
                                await ProcessYftFileAsync(entry, rpfman, outputpath);
                            }
                        }
                        catch (Exception ex)
                        {
                            string err = entry.Name + ": " + ex.Message;
                            UpdateExtractStatus(err);
                            lock (errsb)
                            {
                                errsb.AppendLine(err);
                            }
                        }
                    }
                }

                UpdateExtractStatus("Complete.");
                InProgress = false;
            });
        }

        private async Task ProcessYtdFileAsync(RpfEntry entry, RpfManager rpfman, string outputpath)
        {
            UpdateExtractStatus(entry.Path);
            YtdFile ytd = rpfman.GetFile<YtdFile>(entry);
            if (ytd == null || ytd.TextureDict == null || ytd.TextureDict.Textures == null || ytd.TextureDict.Textures.data_items == null)
                throw new Exception("Couldn't load texture dictionary.");

            foreach (var tex in ytd.TextureDict.Textures.data_items)
            {
                await SaveTextureAsync(tex, entry, outputpath);
            }
        }

        private async Task ProcessYdrFileAsync(RpfEntry entry, RpfManager rpfman, string outputpath)
        {
            UpdateExtractStatus(entry.Path);
            YdrFile ydr = rpfman.GetFile<YdrFile>(entry);
            if (ydr == null || ydr.Drawable == null || ydr.Drawable.ShaderGroup == null)
                throw new Exception("Couldn't load drawable or shader group.");

            var ydrtd = ydr.Drawable.ShaderGroup.TextureDictionary;
            if (ydrtd != null && ydrtd.Textures != null && ydrtd.Textures.data_items != null)
            {
                foreach (var tex in ydrtd.Textures.data_items)
                {
                    await SaveTextureAsync(tex, entry, outputpath);
                }
            }
        }

        private async Task ProcessYddFileAsync(RpfEntry entry, RpfManager rpfman, string outputpath)
        {
            UpdateExtractStatus(entry.Path);
            YddFile ydd = rpfman.GetFile<YddFile>(entry);
            if (ydd == null || ydd.Dict == null || ydd.Dict.Count == 0)
                throw new Exception("Drawable dictionary had no items.");

            foreach (var drawable in ydd.Dict.Values)
            {
                if (drawable.ShaderGroup != null)
                {
                    var ydrtd = drawable.ShaderGroup.TextureDictionary;
                    if (ydrtd != null && ydrtd.Textures != null && ydrtd.Textures.data_items != null)
                    {
                        foreach (var tex in ydrtd.Textures.data_items)
                        {
                            await SaveTextureAsync(tex, entry, outputpath);
                        }
                    }
                }
            }
        }

        private async Task ProcessYftFileAsync(RpfEntry entry, RpfManager rpfman, string outputpath)
        {
            UpdateExtractStatus(entry.Path);
            YftFile yft = rpfman.GetFile<YftFile>(entry);
            if (yft == null || yft.Fragment == null || yft.Fragment.Drawable == null || yft.Fragment.Drawable.ShaderGroup == null)
                throw new Exception("Couldn't load fragment or drawable.");

            var ydrtd = yft.Fragment.Drawable.ShaderGroup.TextureDictionary;
            if (ydrtd != null && ydrtd.Textures != null && ydrtd.Textures.data_items != null)
            {
                foreach (var tex in ydrtd.Textures.data_items)
                {
                    await SaveTextureAsync(tex, entry, outputpath);
                }
            }
        }

        private async Task SaveTextureAsync(Texture tex, RpfEntry entry, string folder)
        {
            byte[] dds = DDSIO.GetDDSFile(tex);

            string textureName = tex.Name + ".dds";
            string fpath = Path.Combine(folder, textureName);

            if (File.Exists(fpath))
            {
                return;
            }

            using (var fileStream = new FileStream(fpath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.WriteAsync(dds, 0, dds.Length);
            }
        }


        private void AbortButton_Click(object sender, EventArgs e)
        {
            AbortOperation = true;
        }

    }
}
