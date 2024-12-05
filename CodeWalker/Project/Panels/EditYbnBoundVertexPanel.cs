using System;
using CodeWalker.GameFiles;

namespace CodeWalker.Project.Panels;

public partial class EditYbnBoundVertexPanel : ProjectPanel
{
    private bool populatingui;
    public ProjectForm ProjectForm;
    private bool waschanged;

    public EditYbnBoundVertexPanel(ProjectForm projectForm)
    {
        ProjectForm = projectForm;
        InitializeComponent();
    }

    public BoundVertex CollisionVertex { get; set; }

    public void SetCollisionVertex(BoundVertex v)
    {
        CollisionVertex = v;
        Tag = v;
        UpdateFormTitle();
        UpdateUI();
        waschanged = v?.Owner?.HasChanged ?? false;
    }

    public void UpdateFormTitleYbnChanged()
    {
        var changed = CollisionVertex?.Owner?.HasChanged ?? false;
        if (!waschanged && changed)
        {
            UpdateFormTitle();
            waschanged = true;
        }
        else if (waschanged && !changed)
        {
            UpdateFormTitle();
            waschanged = false;
        }
    }

    private void UpdateFormTitle()
    {
        var fn = CollisionVertex?.Title ?? "untitled";
        Text = fn + (CollisionVertex?.Owner?.HasChanged ?? false ? "*" : "");
    }


    public void UpdateUI()
    {
        if (CollisionVertex == null)
        {
            AddToProjectButton.Enabled = false;
            DeleteButton.Enabled = false;
            PositionTextBox.Text = string.Empty;
            ColourTextBox.Text = string.Empty;
        }
        else
        {
            populatingui = true;

            PositionTextBox.Text = FloatUtil.GetVector3String(CollisionVertex.Position);
            ColourTextBox.Text = CollisionVertex.Colour.ToString();

            var ybn = CollisionVertex.Owner?.GetRootYbn();
            AddToProjectButton.Enabled = ybn != null ? !ProjectForm.YbnExistsInProject(ybn) : false;
            DeleteButton.Enabled = !AddToProjectButton.Enabled;

            populatingui = false;
        }
    }

    private void PositionTextBox_TextChanged(object sender, EventArgs e)
    {
        if (CollisionVertex == null) return;
        if (populatingui) return;
        var v = FloatUtil.ParseVector3String(PositionTextBox.Text);
        lock (ProjectForm.ProjectSyncRoot)
        {
            if (CollisionVertex.Position != v)
            {
                CollisionVertex.Position = v;
                ProjectForm.SetYbnHasChanged(true);
            }
        }
    }

    private void ColourTextBox_TextChanged(object sender, EventArgs e)
    {
        //TODO!!
    }

    private void AddToProjectButton_Click(object sender, EventArgs e)
    {
        ProjectForm.SetProjectItem(CollisionVertex);
        ProjectForm.AddCollisionVertexToProject();
    }

    private void DeleteButton_Click(object sender, EventArgs e)
    {
        ProjectForm.SetProjectItem(CollisionVertex);
        ProjectForm.DeleteCollisionVertex();
    }
}