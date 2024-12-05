using System;
using WeifenLuo.WinFormsUI.Docking;

namespace CodeWalker.Project.Panels;

public partial class DeleteGrassPanel : ProjectPanel
{
    public DeleteGrassPanel(ProjectForm projectForm)
    {
        ProjectForm = projectForm;
        InitializeComponent();

        if (ProjectForm?.WorldForm == null)
            //could happen in some other startup mode - world form is required for this..
            brushModeGroupBox.Enabled = false;

        DockStateChanged += onDockStateChanged;
        // currentYmapTextBox.DataBindings.Add("Text", ProjectForm, "CurrentYmapName", false, DataSourceUpdateMode.OnPropertyChanged);
    }

    public ProjectForm ProjectForm { get; set; }
    public ProjectFile CurrentProjectFile { get; set; }

    internal DeleteGrassMode Mode
    {
        get
        {
            if (brushDeleteBatchRadio.Checked) return DeleteGrassMode.Batch;
            if (brushDeleteYmapRadio.Checked) return DeleteGrassMode.Ymap;
            if (brushDeleteProjectRadio.Checked) return DeleteGrassMode.Project;
            if (brushDeleteAnyRadio.Checked) return DeleteGrassMode.Any;
            return DeleteGrassMode.None;
        }
    }

    internal float BrushRadius => (float)RadiusNumericUpDown.Value;

    private void onDockStateChanged(object sender, EventArgs e)
    {
        if (DockState == DockState.Hidden)
        {
            brushDisabledRadio.Checked = true;
            brushDisabledRadio.Focus();
        }
    }

    public void SetProject(ProjectFile project)
    {
        CurrentProjectFile = project;
    }

    internal enum DeleteGrassMode
    {
        None,
        Batch,
        Ymap,
        Project,
        Any
    }
}