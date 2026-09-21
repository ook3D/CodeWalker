using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace CodeWalker.Project.Panels
{
    public class EditAudioShorelinePanel : ProjectPanel
    {
        private readonly ProjectForm? projectForm;
        private readonly TableLayoutPanel fields = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        private readonly List<Action> refreshers = new();
        private readonly ErrorProvider errors;
        private readonly Timer worldRefreshTimer = new() { Interval = 100 };
        private Dat151RelData? currentRecord;
        private bool populating;

        public EditAudioShorelinePanel(ProjectForm? owner = null)
        {
            projectForm = owner;
            AutoScaleMode = AutoScaleMode.Font;
            errors = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(fields);
            Controls.Add(scroll);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            worldRefreshTimer.Tick += (_, _) =>
            {
                worldRefreshTimer.Stop();
                if (Tag is not AudioPlacement placement) return;
                Text = placement.GetNameString();
                projectForm?.ProjectExplorer?.UpdateAudioShorelineTreeNode(placement);
                RefreshFields();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                worldRefreshTimer.Dispose();
                errors.Dispose();
            }
            base.Dispose(disposing);
        }

        private void AddRow(string label, Control control)
        {
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(3, 4, 22, 4);
            fields.Controls.Add(control, 1, row);
        }

        private void AddHeading(string text)
        {
            var label = new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 12, 3, 6) };
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(label, 0, row);
            fields.SetColumnSpan(label, 2);
        }

        private void AddText(string name, string label, Func<string> read, Action<string> write)
        {
            var text = new TextBox { Name = name };
            AddRow(label, text);
            refreshers.Add(() => { if (!text.Focused || !text.Modified) text.Text = read(); });
            text.Validating += (_, e) =>
            {
                if (populating || !text.Modified) return;
                try
                {
                    string value = text.Text.Trim();
                    // Clear Modified before notification refreshes this panel.
                    text.Modified = false;
                    Commit(() => write(value));
                    errors.SetError(text, "");
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    text.Modified = true;
                    errors.SetError(text, ex.Message);
                    e.Cancel = true;
                }
            };
            text.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                ValidateChildren();
                e.SuppressKeyPress = true;
            };
        }

        private NumericUpDown AddNumber(string name, string label, Func<decimal> read, Action<decimal>? write, int decimals = 6, decimal min = -1000000000000m, decimal max = 1000000000000m)
        {
            var number = new NumericUpDown { Name = name, DecimalPlaces = decimals, Minimum = min, Maximum = max, Increment = decimals == 0 ? 1 : 0.1m, Width = 220 };
            AddRow(label, number);
            refreshers.Add(() => number.Value = Math.Clamp(read(), min, max));
            number.ValueChanged += (_, _) => { if (!populating && write != null) Commit(() => write(number.Value)); };
            return number;
        }

        private void AddSetting(string propertyName, string label)
        {
            var record = currentRecord!;
            var property = record.GetType().GetProperty(propertyName)!;
            bool integral = property.PropertyType != typeof(float);
            decimal min = property.PropertyType == typeof(byte) ? 0 : integral ? int.MinValue : -1000000000000m;
            decimal max = property.PropertyType == typeof(byte) ? byte.MaxValue : integral ? int.MaxValue : 1000000000000m;
            if (propertyName == "OceanDirection") max = 7;
            AddNumber(propertyName, label, () => Convert.ToDecimal(property.GetValue(record), CultureInfo.InvariantCulture),
                value => property.SetValue(record, Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture)), integral ? 0 : 6, min, max);
        }

        private uint ReadFlags() => ((FlagsUint)currentRecord!.GetType().GetProperty("Flags")!.GetValue(currentRecord)!).Value;
        private void WriteFlags(uint value) => currentRecord!.GetType().GetProperty("Flags")!.SetValue(currentRecord, new FlagsUint(value));

        private void AddFlag(string name, string label, int bit)
        {
            var combo = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(Enum.GetValues(typeof(TristateValue)).Cast<object>().ToArray());
            AddRow(label, combo);
            refreshers.Add(() => combo.SelectedItem = Dat151RelData.GetTristateValue(ReadFlags(), bit));
            combo.SelectedValueChanged += (_, _) =>
            {
                if (populating || combo.SelectedItem is not TristateValue value) return;
                Commit(() => { uint flags = ReadFlags(); Dat151RelData.SetTristateValue(ref flags, bit, value); WriteFlags(flags); });
            };
        }

        private static uint ParseHash(string text)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return uint.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (uint.TryParse(text, out uint hash)) return hash;
            if (string.IsNullOrEmpty(text)) return 0;
            JenkIndex.Ensure(text);
            return JenkHash.GenHash(text);
        }

        private void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new FormatException("Enter a shoreline name or hash.");
            var record = currentRecord!;
            var oldHash = record.NameHash;
            var newHash = ParseHash(name);
            record.Name = name;
            record.NameHash = newHash;
            foreach (var data in record.Rel.RelDatas)
            {
                if (data is Dat151ShoreLineList list)
                    for (int i = 0; i < list.ShoreLines.Length; i++)
                        if (list.ShoreLines[i] == oldHash) list.ShoreLines[i] = newHash;
                if (data is Dat151ShoreLineLakeAudioSettings lake && lake.NextShoreline == oldHash) lake.NextShoreline = newHash;
                if (data is Dat151ShoreLineRiverAudioSettings river && river.NextShoreline == oldHash) river.NextShoreline = newHash;
                if (data is Dat151ShoreLineOceanAudioSettings ocean && ocean.NextShoreline == oldHash) ocean.NextShoreline = newHash;
            }
        }

        private void BuildSettings()
        {
            var record = currentRecord!;
            AddHeading("Shoreline settings");
            AddText("ShorelineName", "Name / hash", () => record.GetNameString(), Rename);
            AddText("Flags", "Flags (hex or decimal)", () => "0x" + ReadFlags().ToString("X8"), value =>
            {
                uint flags = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? uint.Parse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    : uint.Parse(value, CultureInfo.InvariantCulture);
                WriteFlags(flags);
            });
            AddFlag("IsInterior", "Is interior", 0);
            if (record is Dat151ShoreLinePoolAudioSettings) AddFlag("TreatAsLake", "Treat as lake", 1);
            if (record is Dat151ShoreLineOceanAudioSettings) AddFlag("WaveDetection", "Wave detection", 1);

            AddHeading("Activation bounds");
            var bounds = record.GetType().GetProperty("ActivationBox")!;
            string[] names = { "ActivationCenterX", "ActivationCenterY", "ActivationWidth", "ActivationHeight" };
            string[] labels = { "Center X", "Center Y", "Width", "Height" };
            for (int i = 0; i < 4; i++)
            {
                int axis = i;
                AddNumber(names[i], labels[i], () => (decimal)((Vector4)bounds.GetValue(record)!)[axis], value =>
                {
                    var box = (Vector4)bounds.GetValue(record)!;
                    box[axis] = (float)value;
                    bounds.SetValue(record, box);
                }, min: i < 2 ? -1000000000000m : 0);
            }
            AddSetting("RotationAngle", "Rotation angle (degrees)");
            var padding = new NumericUpDown { Name = "BoxPadding", Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = 20 };
            AddRow("Box padding (each side)", padding);
            var calculateBox = new Button { Name = "RecalculateBox", Text = "Recalculate box", AutoSize = true };
            AddRow("", calculateBox);
            calculateBox.Click += (_, _) =>
            {
                if (Tag is not AudioPlacement placement) return;
                try
                {
                    Commit(() => placement.RecalculateShorelineBox((float)padding.Value));
                    errors.SetError(calculateBox, "");
                }
                catch (InvalidOperationException ex) { errors.SetError(calculateBox, ex.Message); }
            };
            if (record is not Dat151ShoreLinePoolAudioSettings)
            {
                var next = record.GetType().GetProperty("NextShoreline")!;
                AddText("NextShoreline", "Next shoreline name / hash", () => ((MetaHash)next.GetValue(record)!).ToCleanString(), value => next.SetValue(record, new MetaHash(ParseHash(value))));
            }
            AddHeading("Audio settings");
            switch (record)
            {
                case Dat151ShoreLineOceanAudioSettings:
                    AddSetting("OceanType", "Ocean type");
                    AddSetting("OceanDirection", "Ocean direction");
                    AddOceanDirectionCalculator((Dat151ShoreLineOceanAudioSettings)record);
                    AddSetting("WaveStartDPDistance", "Wave start DP distance");
                    AddSetting("WaveStartHeight", "Wave start height");
                    AddSetting("WaveBreaksDPDistance", "Wave breaks DP distance");
                    AddSetting("WaveBreaksHeight", "Wave breaks height");
                    AddSetting("WaveEndDPDistance", "Wave end DP distance");
                    AddSetting("WaveEndHeight", "Wave end height");
                    AddSetting("RecedeHeight", "Recede height");
                    break;
                case Dat151ShoreLineLakeAudioSettings:
                    AddSetting("LakeSize", "Lake size");
                    break;
                case Dat151ShoreLineRiverAudioSettings:
                    AddSetting("RiverType", "River type");
                    AddSetting("DefaultHeight", "Default height");
                    break;
                case Dat151ShoreLinePoolAudioSettings:
                    AddSetting("WaterLappingMinDelay", "Water lapping min delay");
                    AddSetting("WaterLappingMaxDelay", "Water lapping max delay");
                    AddSetting("WaterSplashMinDelay", "Water splash min delay");
                    AddSetting("WaterSplashMaxDelay", "Water splash max delay");
                    AddSetting("FirstQuadIndex", "First quad index");
                    AddSetting("SecondQuadIndex", "Second quad index");
                    AddSetting("ThirdQuadIndex", "Third quad index");
                    AddSetting("FourthQuadIndex", "Fourth quad index");
                    AddSetting("SmallestDistanceToPoint", "Smallest distance to point");
                    break;
            }
            var count = new Label { Name = "PointCount", AutoSize = true };
            AddRow("Point count", count);
            refreshers.Add(() => count.Text = ((AudioPlacement)Tag!).ShorelineParent is { } parent ? parent.ShorelinePoints.Length.ToString() : ((AudioPlacement)Tag!).ShorelinePoints.Length.ToString());
        }

        private void AddOceanDirectionCalculator(Dat151ShoreLineOceanAudioSettings ocean)
        {
            var buttons = new FlowLayoutPanel { AutoSize = true };
            foreach (bool left in new[] { true, false })
            {
                var button = new Button { Name = left ? "CalculateOceanLeft" : "CalculateOceanRight", Text = left ? "Water on left" : "Water on right", AutoSize = true };
                buttons.Controls.Add(button);
                button.Click += (_, _) =>
                {
                    try
                    {
                        byte direction = AudioPlacement.CalculateOceanDirection(ocean.Points, left);
                        Commit(() => ocean.OceanDirection = direction);
                        errors.SetError(buttons, "");
                    }
                    catch (InvalidOperationException ex) { errors.SetError(buttons, ex.Message); }
                };
            }
            AddRow("Calculate direction", buttons);
            AddRow("Water side", new Label { AutoSize = true, Text = "Looking from Point 0 toward the last point. The orange arrow must point into water." });
            var status = new Label { Name = "OceanDirectionStatus", AutoSize = true };
            AddRow("Direction check", status);
            string[] names = { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };
            refreshers.Add(() =>
            {
                int count = AudioPlacement.CountParallelOceanSegments(ocean.Points, ocean.OceanDirection);
                status.Text = ocean.OceanDirection > 7 ? "Invalid direction; choose a value from 0 to 7."
                    : names[ocean.OceanDirection] + " — " + count + " segments within 30° of parallel."
                    + (count > 0 ? " Consider splitting curved sections." : "");
            });
        }

        private void BuildPosition()
        {
            AddHeading("Position");
            var description = new Label { AutoSize = true };
            AddRow("Selection", description);
            refreshers.Add(() => description.Text = Tag is AudioPlacement { ShorelinePointIndex: >= 0 } point
                ? "Point " + point.ShorelinePointIndex + " — Shift-drag to duplicate."
                : "Whole shoreline — moves all points.");
            var x = AddNumber("PositionX", "X", () => (decimal)((AudioPlacement)Tag!).Position.X, null);
            var y = AddNumber("PositionY", "Y", () => (decimal)((AudioPlacement)Tag!).Position.Y, null);
            var z = AddNumber("PositionZ", "Z", () => (decimal)((AudioPlacement)Tag!).Position.Z, null);
            z.Enabled = currentRecord is Dat151ShoreLineRiverAudioSettings;
            var buttons = new FlowLayoutPanel { AutoSize = true };
            var apply = new Button { Text = "Apply position", AutoSize = true };
            var goTo = new Button { Text = "Go to", AutoSize = true };
            buttons.Controls.AddRange(new Control[] { apply, goTo });
            AddRow("", buttons);
            apply.Click += (_, _) =>
            {
                if (Tag is not AudioPlacement placement) return;
                var position = new Vector3((float)x.Value, (float)y.Value, (float)z.Value);
                if (projectForm?.WorldForm is { } world)
                {
                    world.SelectObject(placement);
                    lock (world.RenderSyncRoot) placement.SetPosition(position);
                    world.SetWidgetPosition(placement.Position, true);
                    NotifyChanged(placement);
                }
                else Commit(() => placement.SetPosition(position));
            };
            goTo.Click += (_, _) => { if (Tag is AudioPlacement placement) projectForm?.WorldForm?.GoToPosition(placement.Position, Vector3.One * 20); };
            var deleteButtons = new FlowLayoutPanel { AutoSize = true };
            var deletePoint = new Button { Name = "DeletePoint", Text = "Delete point", AutoSize = true };
            var deleteShoreline = new Button { Name = "DeleteShoreline", Text = "Delete shoreline", AutoSize = true };
            deleteButtons.Controls.AddRange(new Control[] { deletePoint, deleteShoreline });
            AddRow("", deleteButtons);
            refreshers.Add(() => deletePoint.Enabled = Tag is AudioPlacement { ShorelineParent: { } parent } && parent.ShorelinePoints.Length > 1);
            deletePoint.Click += (_, _) => { if (Tag is AudioPlacement placement) projectForm?.DeleteAudioShoreline(placement); };
            deleteShoreline.Click += (_, _) => { if (Tag is AudioPlacement placement) projectForm?.DeleteAudioShoreline(placement, true); };
        }

        public void SetItem(object item)
        {
            worldRefreshTimer.Stop();
            var record = (item as AudioPlacement)?.Shoreline;
            bool rebuild = record != currentRecord || (Tag is AudioPlacement) != (item is AudioPlacement) || Tag == null;
            Tag = item;
            currentRecord = record;
            if (rebuild || item is Dat151ShoreLineList)
            {
                populating = true;
                fields.SuspendLayout();
                errors.Clear();
                refreshers.Clear();
                foreach (Control control in fields.Controls.Cast<Control>().ToArray()) control.Dispose();
                fields.Controls.Clear();
                fields.RowStyles.Clear();
                fields.RowCount = 0;
                if (record != null) { BuildPosition(); BuildSettings(); }
                else if (item is Dat151ShoreLineList list)
                {
                    var members = new ListBox { Height = 250 };
                    foreach (var hash in list.ShoreLines) members.Items.Add(hash.ToString());
                    AddRow("Shorelines (double-click)", members);
                    members.DoubleClick += (_, _) =>
                    {
                        if (members.SelectedIndex < 0) return;
                        var data = list.Rel.RelDatas.FirstOrDefault(d => d.NameHash == list.ShoreLines[members.SelectedIndex] && AudioPlacement.IsShoreline(d));
                        if (data != null) projectForm?.ShowProjectItem(data, false);
                    };
                    Text = list.NameHash.ToString();
                }
                fields.ResumeLayout(true);
                populating = false;
            }
            if (item is AudioPlacement placement)
            {
                placement.UpdateFromShoreline();
                Text = placement.GetNameString();
                RefreshFields();
                projectForm?.WorldForm?.SelectObject(placement);
            }
        }

        private void RefreshFields()
        {
            populating = true;
            fields.SuspendLayout();
            try { foreach (var refresh in refreshers) refresh(); }
            finally { fields.ResumeLayout(true); populating = false; }
        }

        public void RefreshFromWorld()
        {
            // Coalesce drag notifications; the tick reads the latest position, including the final move.
            if (!worldRefreshTimer.Enabled) worldRefreshTimer.Start();
        }

        private void Commit(Action edit)
        {
            if (populating || Tag is not AudioPlacement placement) return;
            if (projectForm?.WorldForm is { } world) { lock (world.RenderSyncRoot) edit(); }
            else edit();
            NotifyChanged(placement);
        }

        private void NotifyChanged(AudioPlacement placement)
        {
            placement.UpdateFromShoreline();
            // Notify before setting HasChanged so the project tree receives its dirty-state transition.
            projectForm?.OnWorldSelectionModified(new MapSelection { Audio = placement });
            placement.RelFile.HasChanged = true;
            projectForm?.WorldForm?.SetWidgetPosition(placement.Position);
            Text = placement.GetNameString();
            projectForm?.ProjectExplorer?.UpdateAudioShorelineTreeNode(placement);
            RefreshFields();
        }
    }
}
