using System;
using CodeWalker.World;

namespace CodeWalker.Project.Panels;

public partial class EditTrainNodePanel : ProjectPanel
{
    private bool populatingui;
    public ProjectForm ProjectForm;

    public EditTrainNodePanel(ProjectForm projectForm)
    {
        ProjectForm = projectForm;
        InitializeComponent();
    }

    public TrainTrackNode TrainNode { get; set; }

    public void SetTrainNode(TrainTrackNode node)
    {
        TrainNode = node;
        Tag = node;
        UpdateFormTitle();
        UpdateTrainTrackNodeUI();
    }

    private void UpdateFormTitle()
    {
        Text = "Train Node " + TrainNode.Index;
    }

    public void UpdateTrainTrackNodeUI()
    {
        if (TrainNode == null)
        {
            //TrainNodePanel.Enabled = false;
            TrainNodeDeleteButton.Enabled = false;
            TrainNodeAddToProjectButton.Enabled = false;
            TrainNodePositionTextBox.Text = string.Empty;
            TrainNodeTypeComboBox.SelectedIndex = -1;
        }
        else
        {
            populatingui = true;
            //TrainNodePanel.Enabled = true;
            TrainNodeDeleteButton.Enabled = ProjectForm.TrainTrackExistsInProject(TrainNode.Track);
            TrainNodeAddToProjectButton.Enabled = !TrainNodeDeleteButton.Enabled;
            TrainNodePositionTextBox.Text = FloatUtil.GetVector3String(TrainNode.Position);
            TrainNodeTypeComboBox.SelectedIndex = TrainNode.NodeType;
            populatingui = false;

            if (ProjectForm.WorldForm != null) ProjectForm.WorldForm.SelectObject(TrainNode);
        }
    }

    private void TrainNodePositionTextBox_TextChanged(object sender, EventArgs e)
    {
        if (populatingui) return;
        if (TrainNode == null) return;
        var v = FloatUtil.ParseVector3String(TrainNodePositionTextBox.Text);
        var change = false;
        lock (ProjectForm.ProjectSyncRoot)
        {
            if (TrainNode.Position != v)
            {
                TrainNode.SetPosition(v);
                ProjectForm.SetTrainTrackHasChanged(true);
                change = true;
            }
        }

        if (change)
            if (ProjectForm.WorldForm != null)
            {
                ProjectForm.WorldForm.SetWidgetPosition(TrainNode.Position);
                ProjectForm.WorldForm.UpdateTrainTrackNodeGraphics(TrainNode, false);
            }
        //TrainNodePositionTextBox.Text = FloatUtil.GetVector3String(CurrentTrainNode.Position);
    }

    private void TrainNodeGoToButton_Click(object sender, EventArgs e)
    {
        if (TrainNode == null) return;
        if (ProjectForm.WorldForm == null) return;
        ProjectForm.WorldForm.GoToPosition(TrainNode.Position);
    }

    private void TrainNodeTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (populatingui) return;
        if (TrainNode == null) return;
        var type = TrainNodeTypeComboBox.SelectedIndex;
        var change = false;
        lock (ProjectForm.ProjectSyncRoot)
        {
            if (TrainNode.NodeType != type)
            {
                TrainNode.NodeType = type;
                ProjectForm.SetTrainTrackHasChanged(true);
                change = true;
            }
        }

        if (change)
            if (ProjectForm.WorldForm != null)
                ProjectForm.WorldForm.UpdateTrainTrackNodeGraphics(TrainNode, false); //change the colour...
        ProjectForm.ProjectExplorer?.UpdateTrainNodeTreeNode(TrainNode);
    }

    private void TrainNodeAddToProjectButton_Click(object sender, EventArgs e)
    {
        ProjectForm.SetProjectItem(TrainNode);
        ProjectForm.AddTrainTrackToProject(TrainNode.Track);
    }

    private void TrainNodeDeleteButton_Click(object sender, EventArgs e)
    {
        ProjectForm.SetProjectItem(TrainNode);
        ProjectForm.DeleteTrainNode();
    }
}