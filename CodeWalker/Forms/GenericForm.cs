using System.Windows.Forms;
using CodeWalker.GameFiles;

namespace CodeWalker.Forms;

public partial class GenericForm : Form
{
    private object CurrentFile;

    private ExploreForm ExploreForm;

    private string fileName;


    public GenericForm(ExploreForm exploreForm)
    {
        ExploreForm = exploreForm;
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


    public void LoadFile(object file, RpfFileEntry fileEntry)
    {
        CurrentFile = file;

        DetailsPropertyGrid.SelectedObject = file;

        fileName = fileEntry?.Name;

        UpdateFormTitle();
    }


    private void UpdateFormTitle()
    {
        Text = fileName + " - File Inspector - CodeWalker by dexyfex";
    }
}