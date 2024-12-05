using System;
using System.Windows.Forms;
using FastColoredTextBoxNS;

namespace CodeWalker.Utils;

public partial class TextInputForm : Form
{
    private string _MainText = string.Empty;

    private string _TitleText = string.Empty;

    public TextInputForm()
    {
        InitializeComponent();
        DialogResult = DialogResult.Cancel;
    }

    public string MainText
    {
        get => _MainText;
        set
        {
            _MainText = value;
            MainTextBox.Text = _MainText;
        }
    }

    public string PromptText
    {
        get => PromptLabel.Text;
        set => PromptLabel.Text = value;
    }

    public string TitleText
    {
        get => _TitleText;
        set
        {
            _TitleText = value;
            var str = "Text Input - CodeWalker by dexyfex";
            if (!string.IsNullOrEmpty(_TitleText))
                Text = _TitleText + " - " + str;
            else
                Text = str;
        }
    }


    private void OkButton_Click(object sender, EventArgs e)
    {
        _MainText = MainTextBox.Text;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelThisButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void MainTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _MainText = MainTextBox.Text;
    }
}