using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.Tools;

public partial class ImportFbxForm : Form
{
    public ImportFbxForm()
    {
        InitializeComponent();
        DialogResult = DialogResult.Cancel;
        OutputTypeCombo.Text = "YDR";
    }

    private Dictionary<string, byte[]> InputFiles { get; set; }
    private Dictionary<string, byte[]> OutputFiles { get; set; }


    public void SetInputFiles(Dictionary<string, byte[]> fdict)
    {
        InputFiles = fdict;

        FbxFilesListBox.Items.Clear();
        foreach (var kvp in fdict) FbxFilesListBox.Items.Add(kvp.Key);
    }

    public Dictionary<string, byte[]> GetOutputFiles()
    {
        return OutputFiles;
    }


    private void ConvertFiles()
    {
        if (InputFiles == null) return;

        Cursor = Cursors.WaitCursor;


        Task.Run(() =>
        {
            OutputFiles = new Dictionary<string, byte[]>();

            foreach (var kvp in InputFiles)
            {
                var fname = kvp.Key;
                var idata = kvp.Value;

                UpdateStatus("Converting " + fname + "...");

                var fc = new FbxConverter();

                var ydr = fc.ConvertToYdr(fname, idata);


                if (ydr == null)
                {
                    UpdateStatus("Converting " + fname + " failed!"); //TODO: error message

                    continue; //something went wrong..
                }

                var odata = ydr.Save();

                OutputFiles.Add(fname + ".ydr", odata);
            }

            UpdateStatus("Process complete.");

            ConvertComplete();
        });
    }

    private void ConvertComplete()
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { ConvertComplete(); });
            }
            else
            {
                Cursor = Cursors.Default;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch
        {
        }
    }

    public void UpdateStatus(string text)
    {
        try
        {
            if (InvokeRequired)
                BeginInvoke(() => { UpdateStatus(text); });
            else
                StatusLabel.Text = text;
        }
        catch
        {
        }
    }


    private void CancelThisButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void ImportButton_Click(object sender, EventArgs e)
    {
        ConvertFiles();
    }
}