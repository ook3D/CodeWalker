using System;
using System.Threading;
using System.Windows.Forms;
using CodeWalker.Project;
using CodeWalker.Properties;
using CodeWalker.Tools;

namespace CodeWalker;

public partial class MenuForm : Form
{
    private WorldForm worldForm;
    private volatile bool worldFormOpen;

    public MenuForm()
    {
        InitializeComponent();
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        Settings.Default.Save();
    }

    private void RPFExplorerButton_Click(object sender, EventArgs e)
    {
        var f = new ExploreForm();
        f.Show(this);
    }

    private void RPFBrowserButton_Click(object sender, EventArgs e)
    {
        var f = new BrowseForm();
        f.Show(this);
    }

    private void ExtractScriptsButton_Click(object sender, EventArgs e)
    {
        var f = new ExtractScriptsForm();
        f.Show(this);
    }

    private void ExtractTexturesButton_Click(object sender, EventArgs e)
    {
        var f = new ExtractTexForm();
        f.Show(this);
    }

    private void ExtractRawFilesButton_Click(object sender, EventArgs e)
    {
        var f = new ExtractRawForm();
        f.Show(this);
    }

    private void ExtractShadersButton_Click(object sender, EventArgs e)
    {
        var f = new ExtractShadersForm();
        f.Show(this);
    }

    private void BinarySearchButton_Click(object sender, EventArgs e)
    {
        var f = new BinarySearchForm();
        f.Show(this);
    }

    private void WorldButton_Click(object sender, EventArgs e)
    {
        if (worldFormOpen)
        {
            //MessageBox.Show("Can only open one world view at a time.");
            if (worldForm != null) worldForm.Invoke(() => { worldForm.Focus(); });
            return;
        }

        var thread = new Thread(() =>
        {
            try
            {
                worldFormOpen = true;
                using (var f = new WorldForm())
                {
                    worldForm = f;
                    f.ShowDialog();
                    worldForm = null;
                }

                worldFormOpen = false;
            }
            catch
            {
                worldFormOpen = false;
            }
        });
        thread.Start();
    }

    private void GCCollectButton_Click(object sender, EventArgs e)
    {
        GC.Collect();
    }

    private void AboutButton_Click(object sender, EventArgs e)
    {
        var f = new AboutForm();
        f.Show(this);
    }

    private void JenkGenButton_Click(object sender, EventArgs e)
    {
        var f = new JenkGenForm();
        f.Show(this);
    }

    private void JenkIndButton_Click(object sender, EventArgs e)
    {
        var f = new JenkIndForm();
        f.Show(this);
    }

    private void ExtractKeysButton_Click(object sender, EventArgs e)
    {
        var f = new ExtractKeysForm();
        f.Show(this);
    }

    private void ProjectButton_Click(object sender, EventArgs e)
    {
        var f = new ProjectForm();
        f.Show(this);
    }
}