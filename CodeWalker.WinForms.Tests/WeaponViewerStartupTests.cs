using CodeWalker.Forms;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class WeaponViewerStartupTests
{
    [Fact]
    public void WeaponViewerInitializesItsAnimationDropdown() => StaThread.Run(() =>
    {
        using var viewer = new ModelForm();
        try
        {
            // Exercise the same initialization called by the Explorer menu, without loading game assets.
            viewer.EnableWeaponViewer();
            var dictionary = Assert.Single(Descendants(viewer).OfType<ComboBox>(),
                x => x.AccessibleName == "Animation dictionary");
            Assert.Equal(ComboBoxStyle.DropDownList, dictionary.DropDownStyle);
            Assert.Equal(AutoCompleteSource.ListItems, dictionary.AutoCompleteSource);
            Assert.Equal(AutoCompleteMode.SuggestAppend, dictionary.AutoCompleteMode);
            var attachments = Assert.Single(Descendants(viewer).OfType<CheckedListBox>(),
                x => x.AccessibleName == "Attachments");
            var layout = dictionary.Parent!;
            var tab = layout.Parent!;
            tab.Size = new System.Drawing.Size(350, 800);
            layout.PerformLayout();
            var initialWidth = dictionary.Width;
            var initialHeight = attachments.Height;
            tab.Size = new System.Drawing.Size(750, 1100);
            layout.PerformLayout();
            Assert.True(dictionary.Width >= initialWidth + 390);
            Assert.Equal(dictionary.Width, attachments.Width);
            Assert.True(attachments.Height > initialHeight + 200);
            tab.Size = new System.Drawing.Size(350, 800);
            layout.PerformLayout();
            Assert.Equal(initialWidth, dictionary.Width);
        }
        finally { viewer.Close(); }
    });

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
