using System;
using System.Windows.Forms;

namespace CodeWalker.Tools;

public partial class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, EventArgs e)
    {
        Close();
    }
}