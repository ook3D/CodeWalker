using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodeWalker.Utils;

public partial class ColourPickerForm : Form
{
    public ColourPickerForm()
    {
        InitializeComponent();
    }

    public Color SelectedColour
    {
        get => Picker.SelectedColour;
        set => Picker.SelectedColour = value;
    }

    private void ColourPickerForm_Load(object sender, EventArgs e)
    {
        //var loc = Vector2.Max(Vector2.Zero, LocationSetting.GetVector2());
        //if (loc != Vector2.Zero)
        //{
        //    Location = new Point((int)loc.X, (int)loc.Y);
        //}
    }

    private void ColourPickerForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        //var l = Location;
        //LocationSetting.Set(new Vector2(l.X, l.Y));
    }

    private void ButtonOk_Click(object sender, EventArgs e)
    {
        Picker.SaveRecentColour();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ButtonCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}