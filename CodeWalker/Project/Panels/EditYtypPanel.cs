using CodeWalker.GameFiles;

namespace CodeWalker.Project.Panels;

public partial class EditYtypPanel : ProjectPanel
{
    public ProjectForm ProjectForm;

    //private bool populatingui = false;
    private bool waschanged;

    public EditYtypPanel(ProjectForm projectForm)
    {
        ProjectForm = projectForm;
        InitializeComponent();
    }

    public YtypFile Ytyp { get; set; }

    public void SetYtyp(YtypFile ytyp)
    {
        Ytyp = ytyp;
        Tag = ytyp;
        UpdateFormTitle();
        UpdateYtypUI();
        waschanged = ytyp?.HasChanged ?? false;
    }

    public void UpdateFormTitleYtypChanged()
    {
        var changed = Ytyp.HasChanged;
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
        var fn = Ytyp.RpfFileEntry?.Name ?? Ytyp.Name;
        if (string.IsNullOrEmpty(fn)) fn = "untitled.ytyp";
        Text = fn + (Ytyp.HasChanged ? "*" : "");
    }


    public void UpdateYtypUI()
    {
    }
}