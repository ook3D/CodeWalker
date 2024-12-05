using System;

namespace CodeWalker.Project.Panels;

public partial class EditProjectPanel : ProjectPanel
{
    public ProjectForm ProjectForm;

    public EditProjectPanel(ProjectForm owner)
    {
        ProjectForm = owner;
        InitializeComponent();
    }

    public ProjectFile Project { get; set; }


    public void SetProject(ProjectFile project)
    {
        Project = project;
        Tag = project;
        ProjectNameTextBox.Text = Project.Name;
        UpdateFormTitle();
    }

    private void UpdateFormTitle()
    {
        Text = Project.Filename + (Project.HasChanged ? "*" : "");
    }

    private void ProjectNameTextBox_TextChanged(object sender, EventArgs e)
    {
        if (Project != null)
            if (Project.Name != ProjectNameTextBox.Text)
            {
                Project.Name = ProjectNameTextBox.Text;
                ProjectForm?.SetProjectHasChanged(true);
                UpdateFormTitle();
            }
    }
}