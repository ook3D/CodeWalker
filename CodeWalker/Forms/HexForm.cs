using System;
using System.Text;
using System.Windows.Forms;

namespace CodeWalker.Forms;

public partial class HexForm : Form
{
    private byte[] data;

    private string fileName;


    public HexForm()
    {
        InitializeComponent();

        LineSizeDropDown.Text = "16";
    }

    public byte[] Data
    {
        get => data;
        set
        {
            data = value;
            UpdateTextBoxFromData();
        }
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


    public void LoadData(string filename, string filepath, byte[] data)
    {
        FileName = filename;
        FilePath = filepath;
        Data = data;
    }

    private void UpdateFormTitle()
    {
        Text = fileName + " - Hex Viewer - CodeWalker by dexyfex";
    }

    private void UpdateTextBoxFromData()
    {
        if (data == null)
        {
            HexTextBox.Text = "";
            return;
        }

        if (data.Length > 1048576 * 5)
        {
            HexTextBox.Text =
                "[File size > 5MB - Not shown due to performance limitations - Please use an external viewer for this file.]";
            return;
        }


        Cursor = Cursors.WaitCursor;

        //int selline = -1;
        //int selstartc = -1;
        //int selendc = -1;

        var ishex = LineSizeDropDown.Text != "Text";


        if (ishex)
        {
            var charsperln = int.Parse(LineSizeDropDown.Text);
            var lines = data.Length / charsperln + (data.Length % charsperln > 0 ? 1 : 0);
            var hexb = new StringBuilder();
            var texb = new StringBuilder();
            var finb = new StringBuilder();

            //if (offset > 0)
            //{
            //    selline = offset / charsperln;
            //}
            for (var i = 0; i < lines; i++)
            {
                var pos = i * charsperln;
                var poslim = pos + charsperln;
                hexb.Clear();
                texb.Clear();
                hexb.AppendFormat("{0:X4}: ", pos);
                for (var c = pos; c < poslim; c++)
                    if (c < data.Length)
                    {
                        var b = data[c];
                        hexb.AppendFormat("{0:X2} ", b);
                        if (char.IsControl((char)b))
                            texb.Append(".");
                        else
                            texb.Append(Encoding.ASCII.GetString(data, c, 1));
                    }
                    else
                    {
                        hexb.Append("   ");
                        texb.Append(" ");
                    }

                //if (i == selline) selstartc = finb.Length;
                finb.AppendLine(hexb + "| " + texb);
                //if (i == selline) selendc = finb.Length - 1;
            }

            HexTextBox.Text = finb.ToString();
        }
        else
        {
            var text = Encoding.UTF8.GetString(data);


            HexTextBox.Text = text;

            //if (offset > 0)
            //{
            //    selstartc = offset;
            //    selendc = offset + length;
            //}
        }

        Cursor = Cursors.Default;
    }

    private void LineSizeDropDown_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateTextBoxFromData();
    }
}