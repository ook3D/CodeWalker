using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using CodeWalker.GameFiles;

namespace CodeWalker.Forms;

public partial class YwrForm : Form
{
    private string fileName;

    private YwrFile ywr;


    public YwrForm()
    {
        InitializeComponent();
    }

    public string FileName
    {
        get => fileName;
        set
        {
            fileName = value;
            UpdateFormTitle();
        }
    }

    public string FilePath { get; set; }


    private void UpdateFormTitle()
    {
        Text = fileName + " - Waypoint Records Viewer - CodeWalker by dexyfex";
    }


    public void LoadYwr(YwrFile ywr)
    {
        this.ywr = ywr;
        fileName = ywr?.Name;
        if (string.IsNullOrEmpty(fileName)) fileName = ywr?.RpfFileEntry?.Name;

        UpdateFormTitle();

        if (ywr != null && ywr.Waypoints != null && ywr.Waypoints.Entries != null)
        {
            LoadListView();
            ExportButton.Enabled = true;
            CopyClipboardButton.Enabled = true;
        }
        else
        {
            MessageBox.Show("Error", "Could not load ywr", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string GenerateText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("PositionX, PositionY, PositionZ, Unk0, Unk1, Unk2, Unk3");
        foreach (var entry in ywr.Waypoints.Entries)
        {
            sb.Append(FloatUtil.ToString(entry.Position.X));
            sb.Append(", ");
            sb.Append(FloatUtil.ToString(entry.Position.Y));
            sb.Append(", ");
            sb.Append(FloatUtil.ToString(entry.Position.Z));
            sb.Append(", ");
            sb.Append(entry.Unk0.ToString());
            sb.Append(", ");
            sb.Append(entry.Unk1.ToString());
            sb.Append(", ");
            sb.Append(entry.Unk2.ToString());
            sb.Append(", ");
            sb.Append(entry.Unk3.ToString());
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void LoadListView()
    {
        MainListView.BeginUpdate();
        MainListView.Items.Clear();
        foreach (var entry in ywr.Waypoints.Entries)
        {
            string[] row =
            {
                FloatUtil.ToString(entry.Position.X),
                FloatUtil.ToString(entry.Position.Y),
                FloatUtil.ToString(entry.Position.Z),
                entry.Unk0.ToString(),
                entry.Unk1.ToString(),
                entry.Unk2.ToString(),
                entry.Unk3.ToString()
            };
            MainListView.Items.Add(new ListViewItem(row));
        }

        MainListView.EndUpdate();
    }

    private void CloseButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CopyClipboardButton_Click(object sender, EventArgs e)
    {
        Clipboard.SetText(GenerateText());
    }

    private void ExportButton_Click(object sender, EventArgs e)
    {
        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(fileName) + ".csv";
        if (saveFileDialog.ShowDialog() == DialogResult.OK) File.WriteAllText(saveFileDialog.FileName, GenerateText());
    }
}