using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.World;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.Forms
{
    public partial class ModelForm
    {
        private WeaponCatalog? weaponCatalog;
        private TabPage? weaponTab;
        private ComboBox weaponList = null!;
        private CheckedListBox weaponAttachments = null!;
        private ComboBox weaponTints = null!;
        private ComboBox weaponDictionaries = null!;
        private ComboBox weaponClips = null!;
        private Label weaponStatus = null!;
        private readonly CancellationTokenSource weaponCancellation = new();
        private readonly List<WeaponPreviewComponent> weaponComponents = [];
        private Weapon? previewWeapon;
        private ClipMapEntry? weaponClip;
        private bool weaponUpdating;
        private bool weaponPlaying = true;
        private float weaponTime;
        private float weaponRate = 1;
        private int weaponLoadVersion;
        private int weaponClipLoadVersion;
        private int weaponTintCount = -1;
        private WeaponTint[] weaponTintSpecs = [];

        private sealed class WeaponPreviewComponent
        {
            public required WeaponAttachment Attachment;
            public required WeaponComponentDefinition Definition;
            public required Weapon Model;
            public bool Enabled;
            public Dictionary<string, Weapon> ExtraModels { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public void EnableWeaponViewer()
        {
            if (weaponTab != null) return;
            weaponList = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Weapon" };
            weaponAttachments = new CheckedListBox { Width = 270, Height = 210, CheckOnClick = true, HorizontalScrollbar = true, AccessibleName = "Attachments" };
            weaponTints = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Tint" };
            weaponDictionaries = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 550, AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AccessibleName = "Animation dictionary" };
            weaponClips = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 400, AccessibleName = "Animation" };
            weaponStatus = new Label { AutoSize = true, Text = "Loading weapons..." };
            FileName = "Weapon Viewer";
            weaponTab = new TabPage("Weapon");
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Padding = new Padding(6) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            weaponTab.Controls.Add(panel);
            void AddControl(Control control)
            {
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                var row = panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(control == weaponAttachments ? SizeType.Percent : SizeType.AutoSize, 100));
                panel.Controls.Add(control, 0, row);
            }
            void Add(string label, Control control)
            {
                AddControl(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 9, 3, 3) });
                AddControl(control);
            }
            var search = new TextBox { Width = 270, PlaceholderText = "Filter weapons..." };
            Add("Weapon", search);
            AddControl(weaponList);
            var highDetail = new CheckBox { Text = "High detail models", Checked = true, AutoSize = true };
            AddControl(highDetail);
            Add("Attachments (one per attachment point)", weaponAttachments);
            weaponAttachments.MinimumSize = new System.Drawing.Size(0, 210);
            weaponAttachments.Dock = DockStyle.Fill;
            Add("Tint", weaponTints);
            Add("Animation dictionary", weaponDictionaries);
            Add("Animation", weaponClips);
            var playback = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var play = new Button { Text = "Pause", Width = 75 };
            var restart = new Button { Text = "Restart", Width = 75 };
            var speed = new NumericUpDown { Minimum = 0.1m, Maximum = 4, Increment = 0.1m, DecimalPlaces = 1, Value = 1, Width = 65, AccessibleName = "Playback speed" };
            playback.Controls.AddRange([play, restart, speed]);
            AddControl(playback);
            AddControl(weaponStatus);
            ToolsTabControl.TabPages.Remove(ToolsModelsTabPage);
            ToolsTabControl.TabPages.Remove(ToolsMaterialsTabPage);
            ToolsTabControl.TabPages.Remove(ToolsDetailsTabPage);
            MainToolbarPanel.Visible = false;
            ToolsTabControl.TabPages.Insert(0, weaponTab);
            ToolsTabControl.SelectedTab = weaponTab;
            ToolsPanel.Visible = true;
            ToolsPanel.Width = Math.Max(ToolsPanel.Width, 310);

            search.TextChanged += (_, _) =>
            {
                if (weaponCatalog == null) return;
                var selected = weaponList.SelectedItem;
                weaponUpdating = true;
                weaponList.Items.Clear();
                weaponList.Items.AddRange(weaponCatalog.Weapons.Values.Where(x => x.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Name).Cast<object>().ToArray());
                if (selected != null && weaponList.Items.Contains(selected)) weaponList.SelectedItem = selected;
                weaponUpdating = false;
            };
            weaponList.SelectedIndexChanged += async (_, _) =>
            {
                if (!weaponUpdating) await LoadPreviewWeaponAsync(highDetail.Checked);
            };
            highDetail.CheckedChanged += async (_, _) => await LoadPreviewWeaponAsync(highDetail.Checked);
            weaponAttachments.ItemCheck += WeaponAttachmentChecked;
            weaponTints.SelectedIndexChanged += (_, _) =>
            {
                lock (Renderer.RenderSyncRoot)
                {
                    if (previewWeapon == null || weaponTints.SelectedIndex < 0) return;
                    var tint = (uint)weaponTints.SelectedIndex;
                    previewWeapon.RenderEntity._CEntityDef.tintValue = tint;
                    foreach (var component in weaponComponents)
                    {
                        component.Model.RenderEntity._CEntityDef.tintValue = component.Definition.Variant || component.Definition.ApplyTint ? tint : 0;
                        foreach (var extra in component.ExtraModels)
                            extra.Value.RenderEntity._CEntityDef.tintValue = weaponCatalog?.Components.GetValueOrDefault(extra.Key)?.ApplyTint == true ? tint : 0;
                    }
                }
            };
            weaponDictionaries.SelectedIndexChanged += async (_, _) => await LoadWeaponClipsAsync();
            weaponClips.SelectedIndexChanged += (_, _) =>
            {
                lock (Renderer.RenderSyncRoot)
                {
                    var entry = weaponClips.SelectedItem as WeaponClipItem;
                    weaponClip = entry == null ? null : new ClipMapEntry { Hash = entry.Entry.Hash, Clip = entry.Entry.Clip, OverridePlayTime = true };
                    weaponTime = 0;
                }
            };
            play.Click += (_, _) => { weaponPlaying = !weaponPlaying; play.Text = weaponPlaying ? "Pause" : "Play"; };
            restart.Click += (_, _) => { lock (Renderer.RenderSyncRoot) weaponTime = 0; };
            speed.ValueChanged += (_, _) => weaponRate = (float)speed.Value;
            FormClosed += (_, _) =>
            {
                weaponCancellation.Cancel();
                ToolsModelsTabPage.Dispose();
                ToolsMaterialsTabPage.Dispose();
                ToolsDetailsTabPage.Dispose();
            };
            Shown += async (_, _) => await LoadWeaponCatalogAsync();
        }

        private async Task LoadWeaponCatalogAsync()
        {
            var token = weaponCancellation.Token;
            try
            {
                if (exploreForm == null)
                {
                    await Task.Run(() =>
                    {
                        GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, GTAFolder.IsGen9, Settings.Default.Key);
                        gameFileCache.EnableDlc = true;
                        gameFileCache.EnableMods = true;
                        gameFileCache.LoadPeds = false;
                        gameFileCache.LoadVehicles = false;
                        gameFileCache.LoadArchetypes = false;
                        gameFileCache.Init(UpdateStatus, LogError);
                    }, token);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!token.IsCancellationRequested)
                            {
                                gameFileCache.BeginFrame();
                                if (!gameFileCache.ContentThreadProc()) await Task.Delay(10, token);
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex) { UpdateStatus("Weapon asset loading failed: " + ex.Message); }
                    });
                }
                for (int i = 0; !gameFileCache.IsInited && i < 6000; i++) await Task.Delay(10, token);
                if (!gameFileCache.IsInited) throw new InvalidOperationException("The game file cache did not finish loading.");
                var catalog = await Task.Run(() =>
                {
                    Scenarios.EnsureScenarioTypes(gameFileCache);
                    return WeaponCatalog.Load(gameFileCache, LogError);
                }, token);
                token.ThrowIfCancellationRequested();
                weaponCatalog = catalog;
                weaponList.Items.AddRange(catalog.Weapons.Values.OrderBy(x => x.Name).Cast<object>().ToArray());
                weaponStatus.Text = $"{weaponList.Items.Count} weapons available.";
                if (weaponList.Items.Count > 0)
                    weaponList.SelectedItem = catalog.Weapons.GetValueOrDefault("WEAPON_PISTOL") ?? weaponList.Items[0];
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!IsDisposed) weaponStatus.Text = "Unable to load weapons: " + ex.Message; }
        }

        private async Task<Weapon?> LoadWeaponModelAsync(string name, bool highDetail)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;
            var hash = JenkHash.GenHashLowerInvariant(name);
            var ydr = highDetail ? gameFileCache.GetYdr(JenkHash.GenHashLowerInvariant(name + "_hi")) : null;
            ydr ??= gameFileCache.GetYdr(hash);
            if (ydr == null) return null;
            for (int i = 0; !ydr.Loaded && i < 1500; i++) await Task.Delay(10, weaponCancellation.Token);
            weaponCancellation.Token.ThrowIfCancellationRequested();
            if (!ydr.Loaded || ydr.Drawable == null) return null;
            return new Weapon { Name = name, NameHash = hash, ModelHash = ydr.RpfFileEntry?.ShortNameHash ?? hash, Ydr = ydr, Drawable = ydr.Drawable.ShallowCopy() as gtaDrawable };
        }

        private async Task LoadPreviewWeaponAsync(bool highDetail)
        {
            if (weaponList.SelectedItem is not WeaponDefinition definition || weaponCatalog == null) return;
            int version = ++weaponLoadVersion;
            ++weaponClipLoadVersion;
            weaponStatus.Text = "Loading " + definition.Name + "...";
            weaponUpdating = true;
            weaponAttachments.Items.Clear();
            weaponTints.Items.Clear();
            weaponDictionaries.Items.Clear();
            weaponClips.Items.Clear();
            weaponUpdating = false;
            lock (Renderer.RenderSyncRoot)
            {
                previewWeapon = null;
                weaponComponents.Clear();
                weaponClip = null;
            }
            try
            {
                var weapon = await LoadWeaponModelAsync(definition.Model, highDetail);
                if (version != weaponLoadVersion || IsDisposed) return;
                if (weapon?.Drawable == null) { weaponStatus.Text = "Weapon model is unavailable: " + definition.Model; return; }
                var components = new List<WeaponPreviewComponent>();
                var missing = new List<string>();
                foreach (var attachment in definition.Attachments)
                {
                    if (!weaponCatalog.Components.TryGetValue(attachment.Name, out var component)) { missing.Add(attachment.Name); continue; }
                    var model = await LoadWeaponModelAsync(component.Model, highDetail);
                    if (version != weaponLoadVersion || IsDisposed) return;
                    if (model == null) { missing.Add(attachment.Name); continue; }
                    if (FindWeaponBone(weapon, attachment.Bone) == null) { missing.Add(attachment.Name + " (missing bone)"); continue; }
                    var preview = new WeaponPreviewComponent { Attachment = attachment, Definition = component, Model = model, Enabled = attachment.Default };
                    foreach (var extra in component.ExtraModels)
                    {
                        var extraModel = await LoadWeaponModelAsync(extra.Value, highDetail);
                        if (version != weaponLoadVersion || IsDisposed) return;
                        if (extraModel != null) preview.ExtraModels[extra.Key] = extraModel;
                        else missing.Add(extra.Value);
                    }
                    components.Add(preview);
                }
                lock (Renderer.RenderSyncRoot)
                {
                    previewWeapon = weapon;
                    weaponComponents.AddRange(components);
                    weaponTintCount = -1;
                    weaponTintSpecs = weaponCatalog.TintSets.GetValueOrDefault(definition.TintSet) ?? [];
                    weaponTime = 0;
                    Skeleton = weapon.Drawable.SkeletonData;
                }
                MoveCameraToView(weapon.Drawable.CullSphereCenter, Math.Max(0.2f, weapon.Drawable.CullSphereRadius));
                FileName = definition.Name + " - Weapon Viewer";
                weaponUpdating = true;
                foreach (var component in components) weaponAttachments.Items.Add(component.Attachment, component.Enabled);
                weaponUpdating = false;
                var dictionaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (weaponCatalog.AnimationSets.TryGetValue(definition.Name, out var sets))
                {
                    foreach (var set in sets)
                    {
                        dictionaries.Add(set.ToLowerInvariant());
                        foreach (var dictionary in Scenarios.ScenarioTypes?.GetClipSetDictionaries(JenkHash.GenHashLowerInvariant(set)) ?? []) dictionaries.Add(dictionary);
                    }
                }
                // Keep the installed weapon dictionaries available for custom weapons and alternate animation sets.
                var installed = gameFileCache.YcdDict.Values.Select(x => x.GetShortName()).Distinct().ToArray();
                var preferred = installed.Where(dictionaries.Contains).OrderBy(x => x).ToArray();
                var remaining = installed.Where(x => x.StartsWith("weapons@", StringComparison.OrdinalIgnoreCase) && !dictionaries.Contains(x)).OrderBy(x => x);
                weaponDictionaries.Items.AddRange(preferred.Concat(remaining).Cast<object>().ToArray());
                weaponStatus.Text = missing.Count == 0 ? "Ready. Select a dictionary and animation to preview." : $"{missing.Count} attachments unavailable; see Console. Select an animation to preview.";
                foreach (var name in missing) LogError(definition.Name + ": unavailable attachment " + name);
                if (weaponDictionaries.Items.Count > 0) weaponDictionaries.SelectedIndex = 0;
                else weaponStatus.Text = "Model loaded. No installed weapon animation dictionaries were found.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!IsDisposed && version == weaponLoadVersion) weaponStatus.Text = "Unable to load weapon: " + ex.Message; }
        }

        private void WeaponAttachmentChecked(object? sender, ItemCheckEventArgs e)
        {
            if (weaponUpdating || e.Index >= weaponComponents.Count) return;
            lock (Renderer.RenderSyncRoot)
            {
                var selected = weaponComponents[e.Index];
                selected.Enabled = e.NewValue == CheckState.Checked;
                if (selected.Definition.Variant)
                {
                    weaponTintCount = -1;
                    if (selected.Enabled && selected.Definition.TintOverride >= 0)
                    {
                        var tint = selected.Definition.TintOverride;
                        if (tint < weaponTints.Items.Count) weaponTints.SelectedIndex = tint;
                    }
                }
                if (!selected.Enabled) return;
                weaponUpdating = true;
                for (int i = 0; i < weaponComponents.Count; i++)
                {
                    var other = weaponComponents[i];
                    if (i == e.Index || !other.Attachment.Bone.Equals(selected.Attachment.Bone, StringComparison.OrdinalIgnoreCase)) continue;
                    other.Enabled = false;
                    weaponAttachments.SetItemChecked(i, false);
                }
                weaponUpdating = false;
            }
        }

        private sealed class WeaponClipItem(ClipMapEntry entry)
        {
            public ClipMapEntry Entry { get; } = entry;
            public override string ToString() => Entry.Clip?.ShortName ?? Entry.Hash.ToString();
        }

        private async Task LoadWeaponClipsAsync()
        {
            var version = ++weaponClipLoadVersion;
            lock (Renderer.RenderSyncRoot) weaponClip = null;
            weaponClips.Items.Clear();
            if (weaponUpdating || weaponDictionaries.SelectedItem is not string name) return;
            try
            {
                var ycd = gameFileCache.GetYcd(JenkHash.GenHashLowerInvariant(name));
                if (ycd == null) return;
                for (int i = 0; !ycd.Loaded && i < 1500; i++) await Task.Delay(10, weaponCancellation.Token);
                if (version != weaponClipLoadVersion || IsDisposed) return;
                if (!ycd.Loaded) { weaponStatus.Text = "Timed out loading " + name; return; }
                weaponClips.Items.Add("(None)");
                foreach (var entry in ycd.ClipMapEntries.Where(x => x.Clip != null).OrderBy(x => x.Clip!.ShortName))
                    weaponClips.Items.Add(new WeaponClipItem(entry));
                weaponClips.SelectedIndex = 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!IsDisposed && version == weaponClipLoadVersion) weaponStatus.Text = "Unable to load animations: " + ex.Message; }
        }

        private static crBoneData? FindWeaponBone(Weapon weapon, string name) =>
            weapon.Drawable?.SkeletonData?.Bones?.Items?.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        private void RenderWeaponPreview()
        {
            if (previewWeapon?.Drawable == null) return;
            var variant = weaponComponents.FirstOrDefault(x => x.Enabled && x.Definition.Variant);
            var body = variant?.Model ?? previewWeapon;
            ApplyWeaponTintSpec(body);
            Renderer.RenderWeapon(body, weaponClip);
            foreach (var component in weaponComponents)
            {
                if (!component.Enabled || component.Definition.Variant) continue;
                var parent = FindWeaponBone(body, component.Attachment.Bone);
                if (parent == null) continue;
                var model = variant?.ExtraModels.GetValueOrDefault(component.Attachment.Name) ?? component.Model;
                // CPedEquippedWeapon::AttachObjects aligns the child's attachment bone with the parent's bone.
                var child = FindWeaponBone(model, component.Definition.Bone);
                var transform = Matrix.Invert(child?.AnimTransform ?? Matrix.Identity) * parent.AnimTransform;
                transform.Decompose(out var scale, out var rotation, out var position);
                model.RenderEntity.SetScale(scale);
                model.Position = position;
                model.Rotation = rotation;
                model.UpdateEntity();
                ApplyWeaponTintSpec(model);
                Renderer.RenderWeapon(model);
            }
            var renderable = Renderer.TryGetRenderable(null, body.Drawable, body.NameHash);
            if (renderable?.AllTexturesLoaded != true) return;
            int tintCount = 0;
            foreach (var geometry in renderable.AllModels.SelectMany(x => x.Geometries))
            {
                for (int i = 0; i < geometry.TextureParamHashes.Length; i++)
                {
                    if (geometry.TextureParamHashes[i] is not (ShaderParamNames.TintPaletteSampler or ShaderParamNames.TextureSamplerDiffPal)) continue;
                    var texture = geometry.RenderableTextures[i]?.Key;
                    if (texture != null) tintCount = Math.Max(tintCount, texture.Height);
                }
            }
            if (weaponTintSpecs.Length > 0) tintCount = Math.Min(tintCount, weaponTintSpecs.Length);
            if (tintCount == weaponTintCount) return;
            weaponTintCount = tintCount;
            int version = weaponLoadVersion;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || version != weaponLoadVersion) return;
                var selected = (int)(previewWeapon?.RenderEntity._CEntityDef.tintValue ?? 0);
                weaponTints.Items.Clear();
                for (int i = 0; i < tintCount; i++) weaponTints.Items.Add("Tint " + i);
                weaponTints.Enabled = tintCount > 0;
                if (tintCount > 0) weaponTints.SelectedIndex = Math.Min(selected, tintCount - 1);
                else
                {
                    weaponTints.Items.Add("No tint palette");
                    weaponTints.SelectedIndex = 0;
                }
            }));
        }

        private void ApplyWeaponTintSpec(Weapon weapon)
        {
            var index = weapon.RenderEntity._CEntityDef.tintValue;
            if (index >= weaponTintSpecs.Length) return;
            var renderable = Renderer.TryGetRenderable(null, weapon.Drawable, weapon.NameHash);
            if (renderable?.IsLoaded != true) return;
            var tint = weaponTintSpecs[index];
            foreach (var model in renderable.AllModels)
            {
                foreach (var geometry in model.Geometries)
                {
                    geometry.specularIntensityMult = tint.SpecularIntensity;
                    geometry.specularFalloffMult = tint.SpecularFalloff;
                    geometry.specularFresnel = tint.SpecularFresnel;
                    geometry.specular2Factor = tint.SecondaryFalloff;
                    geometry.WeaponSpecularColour = new Vector4(tint.SecondaryColour, tint.SecondaryIntensity);
                }
            }
        }
    }
}
