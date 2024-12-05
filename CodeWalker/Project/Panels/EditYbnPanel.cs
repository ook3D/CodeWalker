using CodeWalker.GameFiles;

namespace CodeWalker.Project.Panels;

public partial class EditYbnPanel : ProjectPanel
{
    public ProjectForm ProjectForm;

    //private bool populatingui = false;
    private bool waschanged;

    public EditYbnPanel(ProjectForm projectForm)
    {
        ProjectForm = projectForm;
        InitializeComponent();
    }

    public YbnFile Ybn { get; set; }

    public void SetYbn(YbnFile ybn)
    {
        Ybn = ybn;
        Tag = ybn;
        UpdateFormTitle();
        UpdateUI();
        waschanged = ybn?.HasChanged ?? false;
    }

    public void UpdateFormTitleYbnChanged()
    {
        var changed = Ybn?.HasChanged ?? false;
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
        var fn = Ybn?.RpfFileEntry?.Name ?? Ybn?.Name;
        if (string.IsNullOrEmpty(fn)) fn = "untitled.ybn";
        Text = fn + (Ybn?.HasChanged ?? false ? "*" : "");
    }


    public void UpdateUI()
    {
        if (Ybn?.Bounds == null)
        {
        }
        else
        {
            var b = Ybn.Bounds;
            //populatingui = true;


            //populatingui = false;
        }
    }
}