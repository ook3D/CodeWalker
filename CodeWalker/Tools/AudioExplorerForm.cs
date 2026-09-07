using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.Tools
{
    public partial class AudioExplorerForm : Form
    {
        private GameFileCache GameFileCache { get; set; }

        private List<string> NameComboItems = new();
        private Dictionary<string, RelData> NameComboLookup = new();


        public AudioExplorerForm(GameFileCache gfc)
        {
            GameFileCache = gfc;
            InitializeComponent();
            SetupTreeViewStyling();
            LoadDropDowns();
        }


        private void LoadDropDowns()
        {
            if (!GameFileCache.IsInited) return;

            NameComboLookup.Clear();
            NameComboBox.Items.Clear();
            NameComboBox.AutoCompleteCustomSource.Clear();
            NameComboItems = new List<string>();
            void addNameItem(RelData item, bool addToCombo = true)
            {
                if (item == null) return;
                var str = GetRelDataTitleString(item);
                var originalStr = str;
                int counter = 1;
                while (NameComboLookup.ContainsKey(str))
                {
                    str = $"{originalStr} ({counter})";
                    counter++;
                }
                
                NameComboLookup[str] = item; // Use indexer instead of Add to be safe
                if (addToCombo) NameComboItems.Add(str);
            }
            if (GameFileCache.AudioSoundsDict != null)
            {
                foreach (var kvp in GameFileCache.AudioConfigDict) addNameItem(kvp.Value, false);
                foreach (var kvp in GameFileCache.AudioSpeechDict) addNameItem(kvp.Value, false);
                foreach (var kvp in GameFileCache.AudioSynthsDict) addNameItem(kvp.Value);
                foreach (var kvp in GameFileCache.AudioMixersDict) addNameItem(kvp.Value);
                foreach (var kvp in GameFileCache.AudioCurvesDict) addNameItem(kvp.Value);
                foreach (var kvp in GameFileCache.AudioCategsDict) addNameItem(kvp.Value);
                foreach (var kvp in GameFileCache.AudioSoundsDict) addNameItem(kvp.Value);
                foreach (var kvp in GameFileCache.AudioGameDict) addNameItem(kvp.Value);
            }
            NameComboBox.AutoCompleteCustomSource.AddRange(NameComboItems.ToArray());



            TypeComboBox.Items.Clear();
            TypeComboBox.Items.Add("(All types)");
            void addTypeItem(string filetype, object item)
            {
                var str = filetype + " : " + item.ToString();
                TypeComboBox.Items.Add(str);
            }
            foreach (var e in Enum.GetValues(typeof(Dat4ConfigType))) addTypeItem("Config", e);
            foreach (var e in Enum.GetValues(typeof(Dat4SpeechType))) addTypeItem("Speech", e);
            foreach (var e in Enum.GetValues(typeof(Dat10RelType))) addTypeItem("Synths", e);
            foreach (var e in Enum.GetValues(typeof(Dat15RelType))) addTypeItem("Mixers", e);
            foreach (var e in Enum.GetValues(typeof(Dat16RelType))) addTypeItem("Curves", e);
            foreach (var e in Enum.GetValues(typeof(Dat22RelType))) addTypeItem("Categories", e);
            foreach (var e in Enum.GetValues(typeof(Dat54SoundType))) addTypeItem("Sounds", e);
            foreach (var e in Enum.GetValues(typeof(Dat151RelType))) addTypeItem("Game", e);
            TypeComboBox.SelectedIndex = 0;


        }

        private void SelectType()
        {
            var typestr = TypeComboBox.Text;
            var typespl = typestr.Split(new[] { " : " }, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<MetaHash, RelData>? dict = null;
            byte typeid = 255;
            if (typespl.Length == 2)
            {
                switch (typespl[0])
                {
                    case "Config": { dict = GameFileCache.AudioConfigDict; if (Enum.TryParse(typespl[1], out Dat4ConfigType t)) typeid = (byte)t; break; }
                    case "Speech": { dict = GameFileCache.AudioSpeechDict; if (Enum.TryParse(typespl[1], out Dat4SpeechType t)) typeid = (byte)t; break; }
                    case "Synths": { dict = GameFileCache.AudioSynthsDict; if (Enum.TryParse(typespl[1], out Dat10RelType t)) typeid = (byte)t; break; }
                    case "Mixers": { dict = GameFileCache.AudioMixersDict; if (Enum.TryParse(typespl[1], out Dat15RelType t)) typeid = (byte)t; break; }
                    case "Curves": { dict = GameFileCache.AudioCurvesDict; if (Enum.TryParse(typespl[1], out Dat16RelType t)) typeid = (byte)t; break; }
                    case "Categories": { dict = GameFileCache.AudioCategsDict; if (Enum.TryParse(typespl[1], out Dat22RelType t)) typeid = (byte)t; break; }
                    case "Sounds": { dict = GameFileCache.AudioSoundsDict; if (Enum.TryParse(typespl[1], out Dat54SoundType t)) typeid = (byte)t; break; }
                    case "Game": { dict = GameFileCache.AudioGameDict; if (Enum.TryParse(typespl[1], out Dat151RelType t)) typeid = (byte)t; break; }
                }
            }
            if ((dict != null) && (typeid != 255))
            {
                NameComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
                NameComboBox.Text = "(Select item...)";
                NameComboBox.Items.Clear();
                var list = new List<string>();
                foreach (var kvp in dict)
                {
                    var item = kvp.Value;
                    if (item.TypeID == typeid)
                    {
                        var str = GetRelDataTitleString(item);
                        list.Add(str);
                    }
                }
                list.Sort();
                NameComboBox.Items.AddRange(list.ToArray());
            }
            else
            {
                NameComboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
                NameComboBox.Text = "(Start typing to search...)";
                NameComboBox.Items.Clear();
            }


        }

        private string GetRelDataTitleString(RelData? item)
        {
            if (item == null) return "";
            var h = item.NameHash;
            var str = JenkIndex.TryGetString(h);
            if (string.IsNullOrEmpty(str)) str = GlobalText.TryGetString(h);//is this necessary?
            if (string.IsNullOrEmpty(str)) MetaNames.TryGetString(h, out str);
            if (string.IsNullOrEmpty(str)) str = h.Hex;
            
            var typeid = item.TypeID.ToString();
            var rel = item.Rel;
            if (rel != null)
            {
                switch (rel.RelType)
                {
                    case RelDatFileType.Dat54DataEntries:
                        typeid = ((Dat54SoundType)item.TypeID).ToString();
                        break;
                    case RelDatFileType.Dat149:
                    case RelDatFileType.Dat150:
                    case RelDatFileType.Dat151:
                        typeid = ((Dat151RelType)item.TypeID).ToString();
                        break;
                    case RelDatFileType.Dat4:
                        if (rel.IsAudioConfig) typeid = ((Dat4ConfigType)item.TypeID).ToString();
                        else typeid = ((Dat4SpeechType)item.TypeID).ToString();
                        break;
                    case RelDatFileType.Dat10ModularSynth:
                        typeid = ((Dat10RelType)item.TypeID).ToString();
                        break;
                    case RelDatFileType.Dat15DynamicMixer:
                        typeid = ((Dat15RelType)item.TypeID).ToString();
                        break;
                    case RelDatFileType.Dat16Curves:
                        typeid = ((Dat16RelType)item.TypeID).ToString();
                        break;
                    case RelDatFileType.Dat22Categories:
                        typeid = ((Dat22RelType)item.TypeID).ToString();
                        break;
                    default:
                        break;
                }
            }
            return $"{str}";
        }

        private IEnumerable<MetaHash> GetUniqueHashes(MetaHash[]? hashes, RelData item)
        {
            return hashes?.Distinct().Where(h => h != item.NameHash) ?? Enumerable.Empty<MetaHash>();
        }

        private Color GetItemTypeColor(RelData item)
        {
            if (item?.Rel == null) return Color.FromArgb(64, 64, 64);
            
            switch (item.Rel.RelType)
            {
                case RelDatFileType.Dat4:
                    return item.Rel.IsAudioConfig ? Color.FromArgb(70, 130, 180) : Color.FromArgb(95, 158, 160);
                case RelDatFileType.Dat10ModularSynth:
                    return Color.FromArgb(138, 43, 226);
                case RelDatFileType.Dat15DynamicMixer:
                    return Color.FromArgb(205, 133, 63);
                case RelDatFileType.Dat16Curves:
                    return Color.FromArgb(85, 107, 47);
                case RelDatFileType.Dat22Categories:
                    return Color.FromArgb(139, 69, 19);
                case RelDatFileType.Dat54DataEntries:
                    return Color.FromArgb(64, 64, 64);
                case RelDatFileType.Dat149:
                case RelDatFileType.Dat150:
                case RelDatFileType.Dat151:
                    return Color.FromArgb(106, 90, 205);
                default:
                    return Color.FromArgb(64, 64, 64);
            }
        }

        private void SetupTreeViewStyling()
        {
            HierarchyTreeView.DrawMode = TreeViewDrawMode.OwnerDrawText;
            HierarchyTreeView.DrawNode += HierarchyTreeView_DrawNode;
            HierarchyTreeView.BackColor = Color.FromArgb(252, 252, 252); // Very light gray background
            HierarchyTreeView.LineColor = Color.FromArgb(200, 200, 200); // Subtle line color
            HierarchyTreeView.BorderStyle = BorderStyle.FixedSingle;
            HierarchyTreeView.ShowLines = true;
            HierarchyTreeView.ShowPlusMinus = true;
            HierarchyTreeView.ShowRootLines = true;
            HierarchyTreeView.ItemHeight = 26;
            HierarchyTreeView.Indent = 28;
            HierarchyTreeView.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular);
            
            
            HierarchyTreeView.ShowNodeToolTips = true;
            HierarchyTreeView.NodeMouseHover += HierarchyTreeView_NodeMouseHover;
        }

        private void HierarchyTreeView_NodeMouseHover(object? sender, TreeNodeMouseHoverEventArgs e)
        {
            if (e.Node is not { } node) return;
            var item = node.Tag as RelData;
            if (item != null)
            {
                var tooltip = $"Type: {item.GetType().Name}\n" +
                             $"Hash: {item.NameHash.Hex}\n" +
                             $"TypeID: {item.TypeID}\n" +
                             $"File: {item.Rel?.RpfFileEntry?.Name ?? "Unknown"}";
                e.Node.ToolTipText = tooltip;
            }
            else if (e.Node.Tag == null && e.Node.Text.Contains("("))
            {
                e.Node.ToolTipText = "Audio component group - expand to see individual items";
            }
        }

        private void HierarchyTreeView_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (sender is not TreeView treeView || e.Node == null) return;
            var bounds = e.Bounds;
            
            var adjustedBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, Math.Max(bounds.Height, treeView.ItemHeight));
            
            Color backColor = Color.White;
            if ((e.State & TreeNodeStates.Selected) != 0)
            {
                backColor = Color.FromArgb(51, 153, 255);
            }
            else if ((e.State & TreeNodeStates.Hot) != 0)
            {
                backColor = Color.FromArgb(229, 243, 255);
            }
            else if (e.Node.Level % 2 == 1)
            {
                backColor = Color.FromArgb(248, 248, 248);
            }
            
            using (var brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, adjustedBounds);
            }
            
            Color textColor = e.Node.ForeColor;
            if ((e.State & TreeNodeStates.Selected) != 0)
            {
                textColor = Color.White;
            }
            
            var font = e.Node.NodeFont ?? treeView.Font;
            using (var brush = new SolidBrush(textColor))
            {
                var textSize = e.Graphics.MeasureString(e.Node.Text, font, PointF.Empty, StringFormat.GenericDefault);
                var textY = adjustedBounds.Y + (adjustedBounds.Height - textSize.Height) / 2;
                
                var textWidth = Math.Max(adjustedBounds.Width - 6, textSize.Width + 5);
                var textBounds = new RectangleF(adjustedBounds.X + 3, textY, textWidth, textSize.Height);
                
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                
                using (var stringFormat = new StringFormat(StringFormat.GenericDefault))
                {
                    stringFormat.LineAlignment = StringAlignment.Center;
                    stringFormat.Alignment = StringAlignment.Near;
                    stringFormat.FormatFlags = StringFormatFlags.NoWrap;
                    stringFormat.Trimming = StringTrimming.None; // Prevent automatic trimming
                    
                    e.Graphics.DrawString(e.Node.Text, font, brush, textBounds, stringFormat);
                }
            }
            
            if ((e.State & TreeNodeStates.Focused) != 0)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, adjustedBounds);
            }
        }


        private void LoadItemHierarchy(RelData item, TreeNode? parentNode = null)
        {
            TreeNode node;
            if (parentNode == null)
            {
                HierarchyTreeView.Nodes.Clear();
                if (item == null) return;
                node = HierarchyTreeView.Nodes.Add(GetRelDataTitleString(item));
                

                try
                {
                    node.NodeFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                catch
                {
                    node.NodeFont = new Font(HierarchyTreeView.Font, FontStyle.Bold);
                }
                node.ForeColor = GetItemTypeColor(item);
            }
            else
            {
                if (item == null) return;
                node = parentNode.Nodes.Add(GetRelDataTitleString(item));
                
                node.ForeColor = GetItemTypeColor(item);
                try
                {
                    node.NodeFont = new Font("Segoe UI", 8.25F, FontStyle.Bold);
                }
                catch
                {
                    node.NodeFont = new Font(HierarchyTreeView.Font, FontStyle.Bold);
                }
            }

            node.Tag = item;

            if ((item is Dat22Category) && (parentNode != null) && (!(parentNode.Tag is Dat22Category))) //don't bother expanding out categories, too spammy!
            {
                return;
            }


            var speech = GetUniqueHashes(item.GetSpeechHashes(), item);
            var synths = GetUniqueHashes(item.GetSynthHashes(), item);
            var mixers = GetUniqueHashes(item.GetMixerHashes(), item);
            var curves = GetUniqueHashes(item.GetCurveHashes(), item);
            var categs = GetUniqueHashes(item.GetCategoryHashes(), item);
            var sounds = GetUniqueHashes(item.GetSoundHashes(), item);
            var games = GetUniqueHashes(item.GetGameHashes(), item);

            AddHashGroup(node, speech, GameFileCache.AudioSpeechDict, "Speech", Color.FromArgb(95, 158, 160));
            AddHashGroup(node, synths, GameFileCache.AudioSynthsDict, "Synthesizers", Color.FromArgb(138, 43, 226));
            AddHashGroup(node, mixers, GameFileCache.AudioMixersDict, "Mixers", Color.FromArgb(205, 133, 63));
            AddHashGroup(node, curves, GameFileCache.AudioCurvesDict, "Curves", Color.FromArgb(85, 107, 47));
            AddHashGroup(node, categs, GameFileCache.AudioCategsDict, "Categories", Color.FromArgb(139, 69, 19));
            AddHashGroup(node, sounds, GameFileCache.AudioSoundsDict, "Sounds", Color.FromArgb(64, 64, 64));
            AddHashGroup(node, games, GameFileCache.AudioGameDict, "Game Audio", Color.FromArgb(106, 90, 205));

            if (parentNode == null)
            {
                var totnodes = node.GetNodeCount(true);
                
                if (totnodes > 100)
                {
                    node.Expand();
                    foreach (TreeNode cnode in node.Nodes)
                    {
                        if (cnode.Nodes.Count <= 10)
                        {
                            cnode.Expand();
                        }
                    }
                }
                else if (totnodes > 50)
                {
                    node.Expand();
                    foreach (TreeNode cnode in node.Nodes)
                    {
                        if (cnode.Nodes.Count <= 5)
                        {
                            cnode.ExpandAll();
                        }
                    }
                }
                else
                {
                    node.ExpandAll();
                }
                
                HierarchyTreeView.SelectedNode = node;
                
                node.EnsureVisible();
            }
        }

        private void AddHashGroup(TreeNode parentNode, IEnumerable<MetaHash>? hashes, Dictionary<MetaHash, RelData> dict, string groupName, Color groupColor)
        {
            if (hashes == null) return;
            
            var hashList = hashes.ToList();
            if (hashList.Count == 0) return;
            
            TreeNode groupNode = parentNode;
            if (hashList.Count > 1)
            {
                groupNode = parentNode.Nodes.Add(groupName);
                groupNode.ForeColor = groupColor;
                try
                {
                    groupNode.NodeFont = new Font("Segoe UI", 8.25F, FontStyle.Bold);
                }
                catch
                {
                    groupNode.NodeFont = new Font(HierarchyTreeView.Font, FontStyle.Bold);
                }
                groupNode.Tag = null;
            }
            
            foreach (var h in hashList)
            {
                if (dict.TryGetValue(h, out RelData? child))
                {
                    LoadItemHierarchy(child, groupNode);
                }
            }
        }


        private void SelectItem(RelData? item)
        {
            DetailsPropertyGrid.SelectedObject = item;

            if (item != null)
            {
                try
                {
                    XmlTextBox.Text = RelXml.GetXml(item);
                }
                catch (Exception ex)
                {
                    XmlTextBox.Text = $"Error generating XML: {ex.Message}";
                }
            }
            else
            {
                XmlTextBox.Text = "No data available for this item.";
            }
        }


        private void NameComboBox_TextChanged(object sender, EventArgs e)
        {
            if (NameComboLookup.TryGetValue(NameComboBox.Text, out RelData? item))
            {
                LoadItemHierarchy(item);
            }
        }

        private void TypeComboBox_TextChanged(object sender, EventArgs e)
        {
            SelectType();
        }

        private void HierarchyTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var selectedNode = HierarchyTreeView.SelectedNode;
            if (selectedNode != null)
            {
                var item = selectedNode.Tag as RelData;
                
                if (item == null && selectedNode.Tag == null && selectedNode.Nodes.Count > 0)
                {
                    DetailsPropertyGrid.SelectedObject = null;
                    XmlTextBox.Text = $"Group: {selectedNode.Text}\nContains {selectedNode.Nodes.Count} items.\n\nSelect an individual item to view its details.";
                }
                else
                {
                    SelectItem(item);
                }
            }
            else
            {
                SelectItem(null);
            }
        }
    }
}
