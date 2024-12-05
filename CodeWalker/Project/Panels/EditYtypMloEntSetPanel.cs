using System;
using CodeWalker.GameFiles;

namespace CodeWalker.Project.Panels;

public partial class EditYtypMloEntSetPanel : ProjectPanel
{
    private bool populatingui;
    public ProjectForm ProjectForm;
    private bool SelectingLocation;

    public EditYtypMloEntSetPanel(ProjectForm owner)
    {
        ProjectForm = owner;
        InitializeComponent();
    }

    public MCMloEntitySet CurrentEntitySet { get; set; }

    public void SetEntitySet(MCMloEntitySet entset)
    {
        CurrentEntitySet = entset;
        Tag = entset;
        UpdateFormTitle();
        var instance = ProjectForm.TryGetMloInstance(entset?.OwnerMlo);
        //ProjectForm.WorldForm?.SelectMloEntitySet(entset, instance);
        UpdateControls();
    }

    private void UpdateControls()
    {
        if (CurrentEntitySet != null)
        {
            populatingui = true;
            EntitySetNameTextBox.Text = CurrentEntitySet.Name;
            ForceVisibleCheckBox.Checked = CurrentEntitySet.ForceVisible;
            SelectedLocationGroupBox.Visible = false;
            UpdateSelectedLocationRoomCombo();
            populatingui = false;
        }
        else
        {
            EntitySetNameTextBox.Text = string.Empty;
            ForceVisibleCheckBox.Checked = false;
            SelectedLocationGroupBox.Visible = false;
            SelectedLocationRoomCombo.Items.Clear();
        }

        UpdateLocationsListBox();
    }

    private void UpdateLocationsListBox()
    {
        LocationsListBox.Items.Clear();
        if (CurrentEntitySet?.Locations != null)
            for (var i = 0; i < CurrentEntitySet.Locations.Length; i++)
            {
                var loc = CurrentEntitySet.Locations[i];
                var ent = CurrentEntitySet.Entities != null && i < CurrentEntitySet.Entities.Length
                    ? CurrentEntitySet.Entities[i]
                    : null;
                var item = new LocationItem { Location = loc, Entity = ent, Index = i };
                LocationsListBox.Items.Add(item);
            }
    }

    private void UpdateSelectedLocationRoomCombo()
    {
        SelectedLocationRoomCombo.Items.Clear();
        if (CurrentEntitySet?.OwnerMlo?.rooms != null)
            foreach (var room in CurrentEntitySet.OwnerMlo.rooms)
                SelectedLocationRoomCombo.Items.Add(room);
    }

    private void UpdateFormTitle()
    {
        Text = CurrentEntitySet?.Name ?? "Entity Set";
    }

    private void EntitySetNameTextBox_TextChanged(object sender, EventArgs e)
    {
        if (populatingui) return;
        if (CurrentEntitySet == null) return;

        var str = EntitySetNameTextBox.Text;
        if (CurrentEntitySet.Name != str)
        {
            uint h = 0;
            if (uint.TryParse(str, out h))
            {
                CurrentEntitySet._Data.name = h;
            }
            else
            {
                JenkIndex.Ensure(str);
                CurrentEntitySet._Data.name = JenkHash.GenHash(str);
            }

            var tn = ProjectForm.ProjectExplorer?.FindMloEntitySetTreeNode(CurrentEntitySet);
            if (tn != null) tn.Text = CurrentEntitySet.Name;

            UpdateFormTitle();
            ProjectForm.SetYtypHasChanged(true);
        }
    }

    private void ForceVisibleCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        if (CurrentEntitySet == null) return;
        CurrentEntitySet.ForceVisible = ForceVisibleCheckBox.Checked;
    }

    private void LocationsListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        SelectedLocationGroupBox.Visible = false;

        if (CurrentEntitySet == null) return;

        var item = LocationsListBox.SelectedItem as LocationItem;
        if (item == null) return;

        SelectingLocation = true;
        SelectedLocationEntityLabel.Text = item.Entity != null ? item.Entity.Name : "-";
        SelectedLocationRoomCombo.SelectedIndex =
            item.Location < SelectedLocationRoomCombo.Items.Count ? (int)item.Location : 0;
        SelectingLocation = false;
        SelectedLocationGroupBox.Visible = true;
    }

    private void SelectedLocationRoomCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (SelectingLocation) return;
        if (CurrentEntitySet?.Locations == null) return;

        var item = LocationsListBox.SelectedItem as LocationItem;
        if (item == null) return;

        item.Location = SelectedLocationRoomCombo.SelectedIndex >= 0
            ? (uint)SelectedLocationRoomCombo.SelectedIndex
            : 0;

        var ind = item.Index;
        if (ind < CurrentEntitySet.Locations.Length)
        {
            CurrentEntitySet.Locations[ind] = item.Location;

            ProjectForm.SetYtypHasChanged(true);

            if (ind >= 0 && ind < LocationsListBox.Items.Count)
                LocationsListBox.Items[ind] = LocationsListBox.Items[ind]; //refresh the item text...
        }
    }

    private void AddEntityButton_Click(object sender, EventArgs e)
    {
        ProjectForm.SetProjectItem(CurrentEntitySet);
        ProjectForm.NewMloEntity();
    }

    private void DeleteButton_Click(object sender, EventArgs e)
    {
        ProjectForm.SetProjectItem(CurrentEntitySet);
        ProjectForm.DeleteMloEntitySet();
    }


    private class LocationItem
    {
        public uint Location { get; set; }
        public MCEntityDef Entity { get; set; }
        public int Index { get; set; }

        public override string ToString()
        {
            if (Entity == null) return Location.ToString();
            return Location + ": " + Entity;
        }
    }
}