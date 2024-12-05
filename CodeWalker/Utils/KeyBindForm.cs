using System;
using System.Windows.Forms;

namespace CodeWalker.Utils;

public partial class KeyBindForm : Form
{
    private Keys _SelectedKey = Keys.None;


    public KeyBindForm()
    {
        InitializeComponent();
        KeyLabel.Text = "";
        DialogResult = DialogResult.Cancel;
    }

    public Keys SelectedKey
    {
        get => _SelectedKey;
        set
        {
            _SelectedKey = value;
            KeyLabel.Text = _SelectedKey.ToString();
        }
    }

    private void KeyBindForm_KeyDown(object sender, KeyEventArgs e)
    {
        SelectedKey = e.KeyCode;
        OkButton.Enabled = true;
        e.Handled = true;
    }

    private void OkButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancellButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void KeyBindForm_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        e.IsInputKey = true;
    }
}