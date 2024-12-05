using System;
using System.Windows.Forms;
using CodeWalker.GameFiles;

namespace CodeWalker.Tools;

public partial class JenkGenForm : Form
{
    public JenkGenForm()
    {
        InitializeComponent();
    }

    private void InputTextBox_TextChanged(object sender, EventArgs e)
    {
        GenerateHash();
    }

    private void GenerateHash()
    {
        var encoding = JenkHashInputEncoding.UTF8;
        if (ASCIIRadioButton.Checked) encoding = JenkHashInputEncoding.ASCII;

        var h = new JenkHash(InputTextBox.Text, encoding);

        HashHexTextBox.Text = h.HashHex;
        HashSignedTextBox.Text = h.HashInt.ToString();
        HashUnsignedTextBox.Text = h.HashUint.ToString();
    }
}