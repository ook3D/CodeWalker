using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Utils;

namespace CodeWalker.Tools;

public partial class BrowseForm : Form
{
    private volatile bool AbortOperation;

    private bool FlatStructure;
    private volatile bool InProgress;
    private volatile bool KeysLoaded;
    private readonly List<RpfFile> RootFiles = new();
    private readonly List<RpfFile> ScannedFiles = new();

    private readonly List<SearchResult> SearchResults = new();
    private RpfEntry SelectedEntry;
    private int SelectedLength;
    private int SelectedOffset = -1;

    private readonly bool TextureTabPageVisible = false;

    private int TotalFileCount;

    public BrowseForm()
    {
        InitializeComponent();
    }

    private void BrowseForm_Load(object sender, EventArgs e)
    {
        var info = DetailsPropertyGrid.GetType().GetProperty("Controls");
        var collection = info.GetValue(DetailsPropertyGrid, null) as Control.ControlCollection;
        foreach (var control in collection)
        {
            var ctyp = control.GetType();
            if (ctyp.Name == "PropertyGridView")
            {
                var prop = ctyp.GetField("labelRatio");
                var val = prop.GetValue(control);
                prop.SetValue(control, 4.0); //somehow this sets the width of the property grid's label column...
            }
        }

        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
        DataHexLineCombo.Text = "16";

        DataTextBox.SetTabStopWidth(3);

        HideTexturesTab();

        try
        {
            GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, Settings.Default.Key);
            KeysLoaded = true;
            UpdateStatus("Ready to scan...");
        }
        catch
        {
            UpdateStatus("Keys not loaded! This should not happen.");
        }
    }

    private void BrowseForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        if (!TextureTabPageVisible) TexturesTabPage.Dispose();
    }

    private void FolderBrowseButton_Click(object sender, EventArgs e)
    {
        GTAFolder.UpdateGTAFolder();
        FolderTextBox.Text = GTAFolder.CurrentGTAFolder;
    }

    private void ScanButton_Click(object sender, EventArgs e)
    {
        if (InProgress) return;

        if (!KeysLoaded) //this shouldn't ever really happen anymore
        {
            MessageBox.Show("Please scan a GTA 5 exe dump for keys first, or include key files in this app's folder!");
            return;
        }

        if (!Directory.Exists(FolderTextBox.Text))
        {
            MessageBox.Show("Folder doesn't exist: " + FolderTextBox.Text);
            return;
        }

        InProgress = true;
        AbortOperation = false;
        ScannedFiles.Clear();
        RootFiles.Clear();

        MainTreeView.Nodes.Clear();

        var searchpath = FolderTextBox.Text;
        var replpath = searchpath + "\\";

        Task.Run(() =>
        {
            UpdateStatus("Starting scan...");

            var allfiles = Directory.GetFiles(searchpath, "*.rpf", SearchOption.AllDirectories);

            uint totrpfs = 0;
            uint totfiles = 0;
            uint totfolders = 0;
            uint totresfiles = 0;
            uint totbinfiles = 0;

            foreach (var rpfpath in allfiles)
            {
                if (AbortOperation)
                {
                    UpdateStatus("Scan aborted!");
                    InProgress = false;
                    return;
                }

                var rf = new RpfFile(rpfpath, rpfpath.Replace(replpath, ""));

                UpdateStatus("Scanning " + rf.Name + "...");

                rf.ScanStructure(UpdateStatus, UpdateStatus);

                totrpfs += rf.GrandTotalRpfCount;
                totfiles += rf.GrandTotalFileCount;
                totfolders += rf.GrandTotalFolderCount;
                totresfiles += rf.GrandTotalResourceCount;
                totbinfiles += rf.GrandTotalBinaryFileCount;

                AddScannedFile(rf, null, true);

                RootFiles.Add(rf);
            }

            UpdateStatus(string.Format(
                "Scan complete. {0} RPF files, {1} total files, {2} total folders, {3} resources, {4} binary files.",
                totrpfs, totfiles, totfolders, totresfiles, totbinfiles));
            InProgress = false;
            TotalFileCount = (int)totfiles;
        });
    }


    private void UpdateStatus(string text)
    {
        try
        {
            if (InvokeRequired)
                Invoke(() => { UpdateStatus(text); });
            else
                StatusLabel.Text = text;
        }
        catch
        {
        }
    }

    private void ClearFiles()
    {
        MainTreeView.Nodes.Clear();
    }

    private void AddScannedFile(RpfFile file, TreeNode node, bool addToList = false)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(() => { AddScannedFile(file, node, addToList); });
            }
            else
            {
                var cnode = AddFileNode(file, node);

                if (FlatStructure) cnode = null;

                foreach (var cfile in file.Children) AddScannedFile(cfile, cnode, addToList);
                if (addToList) ScannedFiles.Add(file);
            }
        }
        catch
        {
        }
    }

    private TreeNode AddFileNode(RpfFile file, TreeNode n)
    {
        var nodes = n == null ? MainTreeView.Nodes : n.Nodes;
        var node = nodes.Add(file.Path);
        node.Tag = file;

        foreach (var entry in file.AllEntries)
            if (entry is RpfFileEntry)
            {
                var show = !entry.NameLower.EndsWith(".rpf"); //rpf entries get their own root node..
                if (show)
                {
                    //string text = entry.Path.Substring(file.Path.Length + 1); //includes \ on the end
                    //TreeNode cnode = node.Nodes.Add(text);
                    //cnode.Tag = entry;
                    var cnode = AddEntryNode(entry, node);
                }
            }


        //make sure it's all in jenkindex...
        JenkIndex.Ensure(file.Name);
        foreach (var entry in file.AllEntries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            JenkIndex.Ensure(entry.Name);
            JenkIndex.Ensure(entry.NameLower);
            var ind = entry.Name.LastIndexOf('.');
            if (ind > 0)
            {
                JenkIndex.Ensure(entry.Name.Substring(0, ind));
                JenkIndex.Ensure(entry.NameLower.Substring(0, ind));
            }
        }

        return node;


        //TreeNode lastNode = null;
        //string subPathAgg;
        //subPathAgg = string.Empty;
        //foreach (string subPath in file.Path.Split('\\'))
        //{
        //    subPathAgg += subPath + '\\';
        //    TreeNode[] nodes = MainTreeView.Nodes.Find(subPathAgg, true);
        //    if (nodes.Length == 0)
        //    {
        //        if (lastNode == null)
        //        {
        //            lastNode = MainTreeView.Nodes.Add(subPathAgg, subPath);
        //        }
        //        else
        //        {
        //            lastNode = lastNode.Nodes.Add(subPathAgg, subPath);
        //        }
        //    }
        //    else
        //    {
        //        lastNode = nodes[0];
        //    }
        //}
        //lastNode.Tag = file;
    }

    private TreeNode AddEntryNode(RpfEntry entry, TreeNode node)
    {
        var text = entry.Path.Substring(entry.File.Path.Length + 1); //includes \ on the end
        var cnode = node != null ? node.Nodes.Add(text) : MainTreeView.Nodes.Add(text);
        cnode.Tag = entry;
        return cnode;
    }


    private void MainTreeView_AfterSelect(object sender, TreeViewEventArgs e)
    {
        if (e.Node == null) return;
        if (!e.Node.IsSelected) return; //only do this for selected node
        if (MainTreeView.SelectedNode == null) return;

        SelectedEntry = MainTreeView.SelectedNode.Tag as RpfEntry;
        SelectedOffset = -1;
        SelectedLength = 0;

        SelectFile();
    }

    private void DataTextRadio_CheckedChanged(object sender, EventArgs e)
    {
        SelectFile();
    }

    private void DataHexLineCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        SelectFile();
    }

    private void DataHexRadio_CheckedChanged(object sender, EventArgs e)
    {
        SelectFile();
    }


    private void SelectFile()
    {
        SelectFile(SelectedEntry, SelectedOffset, SelectedLength);
    }

    private void SelectFile(RpfEntry entry, int offset, int length)
    {
        SelectedEntry = entry;
        SelectedOffset = offset;
        SelectedLength = length;

        var rfe = entry as RpfFileEntry;
        if (rfe == null)
        {
            var rde = entry as RpfDirectoryEntry;
            if (rde != null)
            {
                FileInfoLabel.Text = rde.Path + " (Directory)";
                DataTextBox.Text = "[Please select a data file]";
            }
            else
            {
                FileInfoLabel.Text = "[Nothing selected]";
                DataTextBox.Text = "[Please select a data file]";
            }

            ShowTextures(null);
            return;
        }


        Cursor = Cursors.WaitCursor;

        var typestr = "Resource";
        if (rfe is RpfBinaryFileEntry) typestr = "Binary";

        var data = rfe.File.ExtractFile(rfe);

        var datalen = data != null ? data.Length : 0;
        FileInfoLabel.Text = rfe.Path + " (" + typestr + " file)  -  " + TextUtil.GetBytesReadable(datalen);


        if (ShowLargeFileContentsCheckBox.Checked || datalen < 524287) //512K
            DisplayFileContentsText(rfe, data, length, offset);
        else
            DataTextBox.Text = "[Filesize >512KB. Select the Show large files option to view its contents]";


        var istexdict = false;


        if (rfe.NameLower.EndsWith(".ymap"))
        {
            var ymap = new YmapFile(rfe);
            ymap.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ymap;
        }
        else if (rfe.NameLower.EndsWith(".ytyp"))
        {
            var ytyp = new YtypFile();
            ytyp.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ytyp;
        }
        else if (rfe.NameLower.EndsWith(".ymf"))
        {
            var ymf = new YmfFile();
            ymf.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ymf;
        }
        else if (rfe.NameLower.EndsWith(".ymt"))
        {
            var ymt = new YmtFile();
            ymt.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ymt;
        }
        else if (rfe.NameLower.EndsWith(".ybn"))
        {
            var ybn = new YbnFile();
            ybn.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ybn;
        }
        else if (rfe.NameLower.EndsWith(".fxc"))
        {
            var fxc = new FxcFile();
            fxc.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = fxc;
        }
        else if (rfe.NameLower.EndsWith(".yft"))
        {
            var yft = new YftFile();
            yft.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = yft;

            if (yft.Fragment != null && yft.Fragment.Drawable != null && yft.Fragment.Drawable.ShaderGroup != null &&
                yft.Fragment.Drawable.ShaderGroup.TextureDictionary != null)
            {
                ShowTextures(yft.Fragment.Drawable.ShaderGroup.TextureDictionary);
                istexdict = true;
            }
        }
        else if (rfe.NameLower.EndsWith(".ydr"))
        {
            var ydr = new YdrFile();
            ydr.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ydr;

            if (ydr.Drawable != null && ydr.Drawable.ShaderGroup != null &&
                ydr.Drawable.ShaderGroup.TextureDictionary != null)
            {
                ShowTextures(ydr.Drawable.ShaderGroup.TextureDictionary);
                istexdict = true;
            }
        }
        else if (rfe.NameLower.EndsWith(".ydd"))
        {
            var ydd = new YddFile();
            ydd.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ydd;
            //todo: show embedded texdicts in ydd's? is this possible?
        }
        else if (rfe.NameLower.EndsWith(".ytd"))
        {
            var ytd = new YtdFile();
            ytd.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ytd;
            ShowTextures(ytd.TextureDict);
            istexdict = true;
        }
        else if (rfe.NameLower.EndsWith(".ycd"))
        {
            var ycd = new YcdFile();
            ycd.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ycd;
        }
        else if (rfe.NameLower.EndsWith(".ynd"))
        {
            var ynd = new YndFile();
            ynd.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ynd;
        }
        else if (rfe.NameLower.EndsWith(".ynv"))
        {
            var ynv = new YnvFile();
            ynv.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = ynv;
        }
        else if (rfe.NameLower.EndsWith("_cache_y.dat"))
        {
            var cdf = new CacheDatFile();
            cdf.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = cdf;
        }
        else if (rfe.NameLower.EndsWith(".rel"))
        {
            var rel = new RelFile(rfe);
            rel.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = rel;
        }
        else if (rfe.NameLower.EndsWith(".gxt2"))
        {
            var gxt2 = new Gxt2File();
            gxt2.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = gxt2;
        }
        else if (rfe.NameLower.EndsWith(".pso"))
        {
            var pso = new JPsoFile();
            pso.Load(data, rfe);
            DetailsPropertyGrid.SelectedObject = pso;
        }
        else
        {
            DetailsPropertyGrid.SelectedObject = null;
        }


        if (!istexdict) ShowTextures(null);


        Cursor = Cursors.Default;
    }

    private void DisplayFileContentsText(RpfFileEntry rfe, byte[] data, int length, int offset)
    {
        if (data == null)
        {
            Cursor = Cursors.Default;
            DataTextBox.Text = "[Error extracting file! " + rfe.File.LastError + "]";
            return;
        }

        var selline = -1;
        var selstartc = -1;
        var selendc = -1;

        if (DataHexRadio.Checked)
        {
            var charsperln = int.Parse(DataHexLineCombo.Text);
            var lines = data.Length / charsperln + (data.Length % charsperln > 0 ? 1 : 0);
            var hexb = new StringBuilder();
            var texb = new StringBuilder();
            var finb = new StringBuilder();

            if (offset > 0) selline = offset / charsperln;
            for (var i = 0; i < lines; i++)
            {
                var pos = i * charsperln;
                var poslim = pos + charsperln;
                hexb.Clear();
                texb.Clear();
                hexb.AppendFormat("{0:X4}: ", pos);
                for (var c = pos; c < poslim; c++)
                    if (c < data.Length)
                    {
                        var b = data[c];
                        hexb.AppendFormat("{0:X2} ", b);
                        if (char.IsControl((char)b))
                            texb.Append(".");
                        else
                            texb.Append(Encoding.ASCII.GetString(data, c, 1));
                    }
                    else
                    {
                        hexb.Append("   ");
                        texb.Append(" ");
                    }

                if (i == selline) selstartc = finb.Length;

                finb.AppendLine(hexb + "| " + texb);

                if (i == selline) selendc = finb.Length - 1;
            }

            DataTextBox.Text = finb.ToString();
        }
        else
        {
            var text = Encoding.UTF8.GetString(data);


            DataTextBox.Text = text;

            if (offset > 0)
            {
                selstartc = offset;
                selendc = offset + length;
            }
        }

        if (selstartc > 0 && selendc > 0)
        {
            DataTextBox.SelectionStart = selstartc;
            DataTextBox.SelectionLength = selendc - selstartc;
            DataTextBox.ScrollToCaret();
        }
    }

    private void ShowTextures(TextureDictionary td)
    {
        SelTexturesListView.Items.Clear();
        SelTexturePictureBox.Image = null;
        SelTextureNameTextBox.Text = string.Empty;
        SelTextureDimensionsLabel.Text = "-";
        SelTextureMipLabel.Text = "0";
        SelTextureMipTrackBar.Value = 0;
        SelTextureMipTrackBar.Maximum = 0;

        if (td == null)
        {
            HideTexturesTab();
            return;
        }

        if (!SelectionTabControl.TabPages.Contains(TexturesTabPage)) SelectionTabControl.TabPages.Add(TexturesTabPage);


        if (td.Textures == null || td.Textures.data_items == null) return;
        var texs = td.Textures.data_items;

        for (var i = 0; i < texs.Length; i++)
        {
            var tex = texs[i];
            var lvi = SelTexturesListView.Items.Add(tex.Name);
            lvi.Tag = tex;
        }
    }

    private void HideTexturesTab()
    {
        if (SelectionTabControl.TabPages.Contains(TexturesTabPage))
            SelectionTabControl.TabPages.Remove(TexturesTabPage);
    }

    private void ShowTextureMip(Texture tex, int mip, bool mipchange)
    {
        if (tex == null)
        {
            SelTexturePictureBox.Image = null;
            SelTextureNameTextBox.Text = string.Empty;
            SelTextureDimensionsLabel.Text = "-";
            SelTextureMipLabel.Text = "0";
            SelTextureMipTrackBar.Value = 0;
            SelTextureMipTrackBar.Maximum = 0;
            return;
        }


        if (mipchange)
        {
            if (mip >= tex.Levels) mip = tex.Levels - 1;
        }
        else
        {
            SelTextureMipTrackBar.Maximum = tex.Levels - 1;
        }

        SelTextureNameTextBox.Text = tex.Name;

        try
        {
            var cmip = Math.Min(Math.Max(mip, 0), tex.Levels - 1);
            var pixels = DDSIO.GetPixels(tex, cmip);
            var w = tex.Width >> cmip;
            var h = tex.Height >> cmip;
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            if (pixels != null)
            {
                var BoundsRect = new Rectangle(0, 0, w, h);
                var bmpData = bmp.LockBits(BoundsRect, ImageLockMode.WriteOnly, bmp.PixelFormat);
                var ptr = bmpData.Scan0;
                var bytes = bmpData.Stride * bmp.Height;
                Marshal.Copy(pixels, 0, ptr, bytes);
                bmp.UnlockBits(bmpData);
            }

            SelTexturePictureBox.Image = bmp;
            SelTextureDimensionsLabel.Text = w + " x " + h;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error reading texture mip:\n" + ex);
            SelTexturePictureBox.Image = null;
        }
    }


    private void AbortButton_Click(object sender, EventArgs e)
    {
        AbortOperation = true;
    }


    private void TestAllButton_Click(object sender, EventArgs e)
    {
        if (InProgress) return;
        if (ScannedFiles.Count == 0)
        {
            MessageBox.Show("Please scan the GTAV folder first.");
            return;
        }

        AbortOperation = false;
        InProgress = true;

        DataTextBox.Text = string.Empty;
        FileInfoLabel.Text = "[File test results]";

        Task.Run(() =>
        {
            UpdateStatus("Starting test...");

            var sbout = new StringBuilder();
            var errcount = 0;
            var curfile = 1;
            var totrpfs = ScannedFiles.Count;
            long totbytes = 0;

            foreach (var file in ScannedFiles)
            {
                if (AbortOperation)
                {
                    UpdateStatus("Test aborted.");
                    InProgress = false;
                    return;
                }

                UpdateStatus(curfile + "/" + totrpfs + ": Testing " + file.FilePath + "...");

                var errorstr = file.TestExtractAllFiles();

                if (!string.IsNullOrEmpty(errorstr))
                {
                    AddTestError(errorstr);
                    sbout.Append(errorstr);
                    errcount++;
                }

                totbytes += file.ExtractedByteCount;
                curfile++;
            }


            UpdateStatus(
                "Test complete. " + errcount + " problems encountered, " + totbytes + " total bytes extracted.");
            InProgress = false;
        });
    }

    private void AddTestError(string error)
    {
        try
        {
            if (InvokeRequired)
                Invoke(() => { AddTestError(error); });
            else
                DataTextBox.AppendText(error);
        }
        catch
        {
        }
    }


    private void Find()
    {
        if (InProgress) return;
        if (ScannedFiles.Count == 0)
        {
            MessageBox.Show("Please scan the GTAV folder first.");
            return;
        }


        var find = FindTextBox.Text.ToLowerInvariant();
        Cursor = Cursors.WaitCursor;
        if (string.IsNullOrEmpty(find))
        {
            ClearFiles();
            foreach (var file in RootFiles) AddScannedFile(file, null); //reset the file tree... slow :(
        }
        else
        {
            ClearFiles();
            var count = 0;
            var max = 500;
            foreach (var file in ScannedFiles)
            {
                if (file.Name.ToLowerInvariant().Contains(find))
                {
                    AddFileNode(file, null);
                    count++;
                }

                foreach (var entry in file.AllEntries)
                {
                    if (entry.NameLower.Contains(find))
                    {
                        if (entry is RpfDirectoryEntry)
                        {
                            var direntry = entry as RpfDirectoryEntry;

                            var node = AddEntryNode(entry, null);

                            foreach (var cfentry in direntry.Files)
                                //if (cfentry.Name.EndsWith(".rpf", StringComparison.InvariantCultureIgnoreCase)) continue;
                                AddEntryNode(cfentry, node);
                            count++;
                        }
                        else if (entry is RpfBinaryFileEntry)
                        {
                            if (entry.NameLower.EndsWith(".rpf", StringComparison.InvariantCultureIgnoreCase)) continue;
                            AddEntryNode(entry, null);
                            count++;
                        }
                        else if (entry is RpfResourceFileEntry)
                        {
                            AddEntryNode(entry, null);
                            count++;
                        }
                    }

                    if (count >= max)
                    {
                        MessageBox.Show("Search results limited to " + max + " entries.");
                        break;
                    }
                }

                if (count >= max) break;
            }
        }

        Cursor = Cursors.Default;
    }

    private void FindTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == 13)
        {
            Find();
            e.Handled = true;
        }
    }

    private void FindButton_Click(object sender, EventArgs e)
    {
        Find();
    }


    private void ExportButton_Click(object sender, EventArgs e)
    {
        if (InProgress) return;
        if (ScannedFiles.Count == 0)
        {
            MessageBox.Show("Please scan the GTAV folder first.");
            return;
        }

        var node = MainTreeView.SelectedNode;
        if (node == null)
        {
            MessageBox.Show("Please select a file to export.");
            return;
        }

        var rfe = node.Tag as RpfFileEntry;
        if (rfe == null)
        {
            MessageBox.Show("Please select a file to export.");
            return;
        }

        SaveFileDialog.FileName = rfe.Name;
        if (SaveFileDialog.ShowDialog() == DialogResult.OK)
        {
            var fpath = SaveFileDialog.FileName;

            var data = rfe.File.ExtractFile(rfe);


            if (ExportCompressCheckBox.Checked) data = ResourceBuilder.Compress(data);


            var rrfe = rfe as RpfResourceFileEntry;
            if (rrfe != null) //add resource header if this is a resource file.
                data = ResourceBuilder.AddResourceHeader(rrfe, data);

            if (data == null)
            {
                MessageBox.Show("Error extracting file! " + rfe.File.LastError);
                return;
            }

            try
            {
                File.WriteAllBytes(fpath, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving file! " + ex);
            }
        }
    }

    private byte LowerCaseByte(byte b)
    {
        if (b >= 65 && b <= 90) //upper case alphabet...
            b += 32;
        return b;
    }

    private void AddSearchResult(SearchResult result)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(() => { AddSearchResult(result); });
            }
            else
            {
                SearchResults.Add(result);
                SearchResultsListView.VirtualListSize = SearchResults.Count;
            }
        }
        catch
        {
        }
    }

    private void Search()
    {
        if (InProgress) return;
        if (ScannedFiles.Count == 0)
        {
            MessageBox.Show("Please scan the GTAV folder first.");
            return;
        }

        if (SearchTextBox.Text.Length == 0)
        {
            MessageBox.Show("Please enter a search term.");
            return;
        }

        var searchtxt = SearchTextBox.Text;
        var hex = SearchHexRadioButton.Checked;
        var casesen = SearchCaseSensitiveCheckBox.Checked || hex;
        var bothdirs = SearchBothDirectionsCheckBox.Checked;
        string[] ignoreexts = null;
        byte[] searchbytes1;
        byte[] searchbytes2;
        int bytelen;

        if (!casesen) searchtxt = searchtxt.ToLowerInvariant(); //case sensitive search in lower case.

        if (SearchIgnoreCheckBox.Checked)
        {
            ignoreexts = SearchIgnoreTextBox.Text.Split(',');
            for (var i = 0; i < ignoreexts.Length; i++) ignoreexts[i] = ignoreexts[i].Trim();
        }

        if (hex)
        {
            if (searchtxt.Length < 2)
            {
                MessageBox.Show("Please enter at least one byte of hex (2 characters).");
                return;
            }

            try
            {
                bytelen = searchtxt.Length / 2;
                searchbytes1 = new byte[bytelen];
                searchbytes2 = new byte[bytelen];
                for (var i = 0; i < bytelen; i++)
                {
                    searchbytes1[i] = Convert.ToByte(searchtxt.Substring(i * 2, 2), 16);
                    searchbytes2[bytelen - i - 1] = searchbytes1[i];
                }
            }
            catch
            {
                MessageBox.Show("Please enter a valid hex string.");
                return;
            }
        }
        else
        {
            bytelen = searchtxt.Length;
            searchbytes1 = new byte[bytelen];
            searchbytes2 = new byte[bytelen]; //reversed text...
            for (var i = 0; i < bytelen; i++)
            {
                searchbytes1[i] = (byte)searchtxt[i];
                searchbytes2[bytelen - i - 1] = searchbytes1[i];
            }
        }

        SearchTextBox.Enabled = false;
        SearchHexRadioButton.Enabled = false;
        SearchTextRadioButton.Enabled = false;
        SearchCaseSensitiveCheckBox.Enabled = false;
        SearchBothDirectionsCheckBox.Enabled = false;
        SearchIgnoreCheckBox.Enabled = false;
        SearchIgnoreTextBox.Enabled = false;
        SearchButton.Enabled = false;
        SearchSaveResultsButton.Enabled = false;

        InProgress = true;
        AbortOperation = false;
        SearchResultsListView.VirtualListSize = 0;
        SearchResults.Clear();
        var totfiles = TotalFileCount;
        var curfile = 0;
        Task.Run(() =>
        {
            var starttime = DateTime.Now;
            var resultcount = 0;

            for (var f = 0; f < ScannedFiles.Count; f++)
            {
                var rpffile = ScannedFiles[f];

                //UpdateStatus(string.Format("Searching {0}/{1} : {2}", f, ScannedFiles.Count, rpffile.Path));

                foreach (var entry in rpffile.AllEntries)
                {
                    var duration = DateTime.Now - starttime;
                    if (AbortOperation)
                    {
                        UpdateStatus(duration.ToString(@"hh\:mm\:ss") + " - Search aborted.");
                        InProgress = false;
                        SearchComplete();
                        return;
                    }

                    var fentry = entry as RpfFileEntry;
                    if (fentry == null) continue;

                    curfile++;

                    if (fentry.NameLower.EndsWith(".rpf")) continue;

                    if (ignoreexts != null)
                    {
                        var ignore = false;
                        for (var i = 0; i < ignoreexts.Length; i++)
                            if (fentry.NameLower.EndsWith(ignoreexts[i]))
                            {
                                ignore = true;
                                break;
                            }

                        if (ignore) continue;
                    }

                    UpdateStatus(string.Format("{0} - Searching {1}/{2} : {3}", duration.ToString(@"hh\:mm\:ss"),
                        curfile, totfiles, fentry.Path));

                    var filebytes = fentry.File.ExtractFile(fentry);
                    if (filebytes == null) continue;


                    var hitlen1 = 0;
                    var hitlen2 = 0;

                    for (var i = 0; i < filebytes.Length; i++)
                    {
                        var b = casesen ? filebytes[i] : LowerCaseByte(filebytes[i]);
                        var b1 = searchbytes1[hitlen1]; //current test byte 1
                        var b2 = searchbytes2[hitlen2];

                        if (b == b1) hitlen1++;
                        else hitlen1 = 0;
                        if (hitlen1 == bytelen)
                        {
                            AddSearchResult(new SearchResult(fentry, i - bytelen, bytelen));
                            resultcount++;
                            hitlen1 = 0;
                        }

                        if (bothdirs)
                        {
                            if (b == b2) hitlen2++;
                            else hitlen2 = 0;
                            if (hitlen2 == bytelen)
                            {
                                AddSearchResult(new SearchResult(fentry, i - bytelen, bytelen));
                                resultcount++;
                                hitlen2 = 0;
                            }
                        }
                    }
                }
            }

            var totdur = DateTime.Now - starttime;
            UpdateStatus(totdur.ToString(@"hh\:mm\:ss") + " - Search complete. " + resultcount + " results found.");
            InProgress = false;
            SearchComplete();
        });
    }

    private void SearchComplete()
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(() => { SearchComplete(); });
            }
            else
            {
                SearchTextBox.Enabled = true;
                SearchHexRadioButton.Enabled = true;
                SearchTextRadioButton.Enabled = true;
                SearchCaseSensitiveCheckBox.Enabled = SearchTextRadioButton.Checked;
                SearchBothDirectionsCheckBox.Enabled = true;
                SearchIgnoreCheckBox.Enabled = true;
                SearchIgnoreTextBox.Enabled = SearchIgnoreCheckBox.Checked;
                SearchButton.Enabled = true;
                SearchSaveResultsButton.Enabled = true;
            }
        }
        catch
        {
        }
    }

    private void SearchButton_Click(object sender, EventArgs e)
    {
        Search();
    }

    private void SearchAbortButton_Click(object sender, EventArgs e)
    {
        AbortOperation = true;
    }

    private void SearchSaveResultsButton_Click(object sender, EventArgs e)
    {
        SaveFileDialog.FileName = "SearchResults.txt";
        if (SaveFileDialog.ShowDialog() == DialogResult.OK)
        {
            var fpath = SaveFileDialog.FileName;

            var sb = new StringBuilder();
            sb.AppendLine("CodeWalker Search Results for \"" + SearchTextBox.Text + "\"");
            sb.AppendLine("[File path], [Byte offset]");
            if (SearchResults != null)
                foreach (var r in SearchResults)
                    sb.AppendLine(r.FileEntry.Path + ", " + r.Offset);

            File.WriteAllText(fpath, sb.ToString());
        }
    }

    private void SearchTextRadioButton_CheckedChanged(object sender, EventArgs e)
    {
        SearchCaseSensitiveCheckBox.Enabled = SearchTextRadioButton.Checked;
    }

    private void SearchHexRadioButton_CheckedChanged(object sender, EventArgs e)
    {
    }

    private void SearchResultsListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        var item = new ListViewItem();
        if (e.ItemIndex < SearchResults.Count)
        {
            var r = SearchResults[e.ItemIndex];
            item.Text = r.FileEntry.Name;
            item.SubItems.Add(r.Offset.ToString());
            item.Tag = r;
        }

        e.Item = item;
    }

    private void SearchResultsListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (SearchResultsListView.SelectedIndices.Count == 1)
        {
            var i = SearchResultsListView.SelectedIndices[0];
            if (i >= 0 && i < SearchResults.Count)
            {
                var r = SearchResults[i];
                SelectFile(r.FileEntry, r.Offset + 1, r.Length);
            }
            else
            {
                SelectFile(null, -1, 0);
            }
        }
        else
        {
            SelectFile(null, -1, 0);
        }
    }

    private void SearchIgnoreCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        SearchIgnoreTextBox.Enabled = SearchIgnoreCheckBox.Checked;
    }

    private void SelTexturesListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        Texture tex = null;
        if (SelTexturesListView.SelectedItems.Count == 1) tex = SelTexturesListView.SelectedItems[0].Tag as Texture;
        ShowTextureMip(tex, 0, false);
    }

    private void SelTextureMipTrackBar_Scroll(object sender, EventArgs e)
    {
        Texture tex = null;
        if (SelTexturesListView.SelectedItems.Count == 1) tex = SelTexturesListView.SelectedItems[0].Tag as Texture;
        SelTextureMipLabel.Text = SelTextureMipTrackBar.Value.ToString();
        ShowTextureMip(tex, SelTextureMipTrackBar.Value, true);
    }

    private void ShowLargeFileContentsCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        SelectFile();
    }

    private void FlattenStructureCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        FlatStructure = FlattenStructureCheckBox.Checked;

        if (InProgress) return;
        if (ScannedFiles.Count == 0) return;

        Cursor = Cursors.WaitCursor;

        SearchTextBox.Clear();

        ClearFiles();
        foreach (var file in RootFiles) AddScannedFile(file, null);

        Cursor = Cursors.Default;
    }


    private class SearchResult
    {
        public SearchResult(RpfFileEntry entry, int offset, int length)
        {
            FileEntry = entry;
            Offset = offset;
            Length = length;
        }

        public RpfFileEntry FileEntry { get; }
        public int Offset { get; }
        public int Length { get; }
    }
}