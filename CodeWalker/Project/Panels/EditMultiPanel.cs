using System;
using SharpDX;

namespace CodeWalker.Project.Panels;

public partial class EditMultiPanel : ProjectPanel
{
    public MapSelection MultiItem;

    private bool populatingui;
    public ProjectForm ProjectForm;

    public EditMultiPanel(ProjectForm owner)
    {
        ProjectForm = owner;
        InitializeComponent();
    }

    public MapSelection[] Items { get; set; }

    public void SetItems(MapSelection[] items)
    {
        Items = items;
        Tag = items;
        LoadItems();
        UpdateFormTitle();
    }

    private void UpdateFormTitle()
    {
        Text = (Items?.Length ?? 0) + " item" + (Items?.Length == 1 ? "" : "s");
    }


    private void LoadItems()
    {
        MultiItem = new MapSelection();
        MultiItem.WorldForm = ProjectForm.WorldForm;
        MultiItem.Clear();
        MultiItem.SetMultipleSelectionItems(Items);

        if (Items == null)
        {
            PositionTextBox.Text = string.Empty;
            RotationQuatBox.Value = Quaternion.Identity;
            ScaleTextBox.Text = string.Empty;
            ItemsListBox.Items.Clear();
        }
        else
        {
            populatingui = true;


            PositionTextBox.Text = FloatUtil.GetVector3String(MultiItem.MultipleSelectionCenter);
            RotationQuatBox.Value = MultiItem.MultipleSelectionRotation;
            ScaleTextBox.Text = FloatUtil.GetVector3String(MultiItem.MultipleSelectionScale);
            ItemsListBox.Items.Clear();
            foreach (var item in Items) ItemsListBox.Items.Add(item);

            populatingui = false;
        }
    }

    private void PositionTextBox_TextChanged(object sender, EventArgs e)
    {
        if (Items == null) return;
        if (populatingui) return;
        var v = FloatUtil.ParseVector3String(PositionTextBox.Text);

        var wf = ProjectForm.WorldForm;
        if (wf != null)
            wf.BeginInvoke(() => { wf.ChangeMultiPosition(Items, v); });
    }

    private void RotationQuatBox_ValueChanged(object sender, EventArgs e)
    {
        if (Items == null) return;
        if (populatingui) return;
        var q = RotationQuatBox.Value;

        var wf = ProjectForm.WorldForm;
        if (wf != null)
            wf.BeginInvoke(() => { wf.ChangeMultiRotation(Items, q); });
    }

    private void ScaleTextBox_TextChanged(object sender, EventArgs e)
    {
        if (Items == null) return;
        if (populatingui) return;
        var v = FloatUtil.ParseVector3String(ScaleTextBox.Text);

        var wf = ProjectForm.WorldForm;
        if (wf != null)
            wf.BeginInvoke(() => { wf.ChangeMultiScale(Items, v); });
    }

    private void ItemsListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
    }
}