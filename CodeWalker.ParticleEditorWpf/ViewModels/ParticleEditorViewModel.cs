using CodeWalker.Forms;
using CodeWalker.GameFiles;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace CodeWalker.ParticleEditorWpf.ViewModels
{
    public class ParticleEditorViewModel : ViewModelBase
    {
        private ParticleViewportHost host;

        public YptFile Ypt { get; private set; }
        public ObservableCollection<TreeNodeVM> Effects { get; } = new ObservableCollection<TreeNodeVM>();
        public ObservableCollection<BehaviourToggleVM> BehaviourToggles { get; } = new ObservableCollection<BehaviourToggleVM>();

        private string effectsFilter = "";
        public string EffectsFilter
        {
            get => effectsFilter;
            set { if (SetField(ref effectsFilter, value)) effectsView?.Refresh(); }
        }
        private ICollectionView effectsView;

        private TreeNodeVM selectedNode;
        public TreeNodeVM SelectedNode
        {
            get => selectedNode;
            set { if (SetField(ref selectedNode, value)) OnNodeSelected(value); }
        }

        private object selectedObject;
        public object SelectedObject { get => selectedObject; private set => SetField(ref selectedObject, value); }

        private string fileName = "(no file)";
        public string FileName { get => fileName; private set => SetField(ref fileName, value); }
        private string filePath;

        private string playPauseText = "Pause";
        public string PlayPauseText { get => playPauseText; private set { if (SetField(ref playPauseText, value)) OnPropertyChanged(nameof(PlayGlyph)); } }
        // play button glyph: pause icon while playing (click to pause), play icon while paused
        public string PlayGlyph => playPauseText == "Pause" ? "⏸" : "▶";

        private double rate = 1.0;
        public double Rate
        {
            get => rate;
            set { if (SetField(ref rate, value) && host != null) host.TimeScale = (float)value; }
        }

        private string timeText = "0.00 / 0.00";
        public string TimeText { get => timeText; private set => SetField(ref timeText, value); }

        private string statusText = "Ready";
        public string StatusText { get => statusText; private set => SetField(ref statusText, value); }

        //commands
        public ICommand NewFileCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand ImportXmlCommand { get; }
        public ICommand ExportXmlCommand { get; }
        public ICommand NewEffectCommand { get; }
        public ICommand DuplicateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddEmitterCommand { get; }
        public ICommand AddBehaviourCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }

        public ParticleEditorViewModel()
        {
            NewFileCommand = new RelayCommand(NewFile);
            OpenCommand = new RelayCommand(Open);
            SaveCommand = new RelayCommand(() => Save(false));
            SaveAsCommand = new RelayCommand(() => Save(true));
            ImportXmlCommand = new RelayCommand(ImportXml);
            ExportXmlCommand = new RelayCommand(ExportXml);
            NewEffectCommand = new RelayCommand(NewEffect);
            DuplicateCommand = new RelayCommand(DuplicateSelected);
            DeleteCommand = new RelayCommand(DeleteSelected);
            AddEmitterCommand = new RelayCommand(AddEmitter);
            AddBehaviourCommand = new RelayCommand(p => AddBehaviour(p));
            PlayPauseCommand = new RelayCommand(TogglePlay);
            StopCommand = new RelayCommand(Stop);
            RestartCommand = new RelayCommand(Restart);

            // the TreeView binds to Effects via its default collection view; filter top-level effects by the search text
            effectsView = CollectionViewSource.GetDefaultView(Effects);
            effectsView.Filter = EffectFilterPredicate;
        }

        private bool EffectFilterPredicate(object o)
        {
            if (string.IsNullOrWhiteSpace(effectsFilter)) return true;
            var n = o as TreeNodeVM;
            return (n?.DisplayName?.IndexOf(effectsFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        public void AttachHost(ParticleViewportHost h) { host = h; }

        private readonly System.Threading.Lock fallbackLock = new();
        // Use the same Lock primitive as the render thread.
        private System.Threading.Lock RenderLock => (host != null) ? host.RenderSyncRoot : fallbackLock;


        #region selection

        private ParticleEffectRule previewedEffect;

        private void OnNodeSelected(TreeNodeVM node)
        {
            SelectedObject = node?.Data;
            UpdateBehaviourToggles(node);
            // Only (re)start the preview when the OWNING effect changes. Clicking between an effect's sub-nodes
            // (emitter / emitter rule / particle rule / domain / behaviour) keeps the same effect playing from
            // where it is, instead of resetting playback to the start.
            if ((node?.OwnerEffect != null) && (host != null) && !ReferenceEquals(node.OwnerEffect, previewedEffect))
            {
                previewedEffect = node.OwnerEffect;
                host.SetPreviewEffect(node.OwnerEffect, Ypt);
            }
        }

        // called by the view when a property grid value changes
        public void OnPropertyEdited()
        {
            host?.MarkDirty();
        }

        // tree emitter "eye" checkbox -> show/hide that emitter in the preview
        private void OnEmitterVisibilityToggled(TreeNodeVM node, bool visible)
        {
            if (node?.OwnerEmitter != null) host?.SetEmitterVisible(node.OwnerEmitter, visible);
        }

        // normalized playhead position (0..1) of the running preview - used by the keyframe timeline
        public float CurrentTimeRatio
        {
            get
            {
                if (host == null) return 0f;
                float d = host.Duration;
                return (d > 0.0001f) ? Math.Clamp(host.CurrentTime / d, 0f, 1f) : 0f;
            }
        }

        // run an edit while holding the render lock so it can't race the simulation thread reading the same data.
        // rebuildPreview rebuilds the ParticleEffectInst (needed for changes the sim caches at construction, e.g.
        // most scalar/struct properties). Keyframe edits pass false: the sim re-reads KFP Values live every frame,
        // so they take effect immediately WITHOUT a rebuild - and skipping the rebuild keeps playback from
        // restarting at time 0.
        public void EditUnderLock(Action edit, bool rebuildPreview = true)
        {
            if (edit == null) return;
            lock (RenderLock) { edit(); }
            if (rebuildPreview) host?.MarkDirty();
        }

        #endregion


        #region tree

        private void RebuildTree()
        {
            Effects.Clear();
            var effs = Ypt?.AllEffects;
            if (effs == null) return;

            foreach (var eff in effs)
            {
                var en = new TreeNodeVM(eff?.Name?.Value ?? "(effect)", NodeKind.Effect, eff, eff);
                var emitters = eff?.EventEmitters?.data_items;
                int count = Math.Min(eff?.EventEmittersCount ?? 0, emitters?.Length ?? 0);
                for (int i = 0; i < count; i++)
                {
                    var em = emitters[i];
                    if (em == null) continue;
                    var emn = new TreeNodeVM("Emitter: " + (em.EmitterRuleName?.Value ?? em.ParticleRuleName?.Value ?? i.ToString()),
                        NodeKind.Emitter, em, eff, null, em);
                    emn.VisibilityToggle = OnEmitterVisibilityToggled;
                    if (em.EmitterRule != null)
                    {
                        var ern = new TreeNodeVM("Emitter Rule: " + (em.EmitterRule.Name?.Value ?? ""), NodeKind.EmitterRule, em.EmitterRule, eff, null, em);
                        if (em.EmitterRule.CreationDomainObj != null)
                        {
                            ern.Children.Add(new TreeNodeVM("Creation Domain: " + em.EmitterRule.CreationDomainObj.DomainType, NodeKind.Domain, em.EmitterRule.CreationDomainObj, eff, null, em));
                        }
                        emn.Children.Add(ern);
                    }
                    if (em.ParticleRule != null)
                    {
                        var prn = new TreeNodeVM("Particle Rule: " + (em.ParticleRule.Name?.Value ?? ""), NodeKind.ParticleRule, em.ParticleRule, eff, em.ParticleRule, em);
                        var behs = em.ParticleRule.AllBehaviours?.data_items;
                        if (behs != null)
                        {
                            foreach (var b in behs)
                            {
                                if (b == null) continue;
                                prn.Children.Add(new TreeNodeVM("Behaviour: " + b.Type, NodeKind.Behaviour, b, eff, em.ParticleRule, em));
                            }
                        }
                        emn.Children.Add(prn);
                    }
                    en.Children.Add(emn);
                }
                Effects.Add(en);
            }
        }

        private void SelectEffect(ParticleEffectRule eff)
        {
            var node = Effects.FirstOrDefault(n => n.Data == eff);
            if (node != null) { node.IsSelected = true; SelectedNode = node; }
            else if (Effects.Count > 0) { Effects[0].IsSelected = true; SelectedNode = Effects[0]; }
        }

        #endregion


        #region behaviour toggles

        private static readonly ParticleBehaviourType[] ToggleTypes =
        {
            ParticleBehaviourType.Age, ParticleBehaviourType.Velocity, ParticleBehaviourType.Acceleration,
            ParticleBehaviourType.Dampening, ParticleBehaviourType.Size, ParticleBehaviourType.Colour,
            ParticleBehaviourType.Rotation, ParticleBehaviourType.AnimateTexture, ParticleBehaviourType.Sprite,
            ParticleBehaviourType.Wind, ParticleBehaviourType.Noise,
        };

        private ParticleRule toggleParticleRule;

        private void UpdateBehaviourToggles(TreeNodeVM node)
        {
            BehaviourToggles.Clear();
            toggleParticleRule = node?.OwnerParticleRule;
            if (toggleParticleRule == null) return;

            var existing = (toggleParticleRule.AllBehaviours?.data_items ?? Array.Empty<ParticleBehaviour>())
                .Where(b => b != null).Select(b => b.Type).ToHashSet();

            foreach (var t in ToggleTypes)
            {
                BehaviourToggles.Add(new BehaviourToggleVM(t, existing.Contains(t), OnBehaviourToggled));
            }
        }

        private void OnBehaviourToggled(BehaviourToggleVM vm, bool present)
        {
            //NOTE: don't rebuild the tree/toggles here - this fires mid-checkbox-toggle and the toggle list
            //is the collection the checkbox belongs to. Just edit the data + refresh the preview; the tree's
            //behaviour nodes refresh on the next selection.
            var prule = toggleParticleRule;
            if (prule == null) return;
            lock (RenderLock)
            {
                if (present)
                {
                    YptEditUtil.AddBehaviour(prule, vm.Type);
                }
                else
                {
                    var existing = (prule.AllBehaviours?.data_items ?? Array.Empty<ParticleBehaviour>()).FirstOrDefault(b => (b != null) && (b.Type == vm.Type));
                    if (existing != null) YptEditUtil.RemoveBehaviour(prule, existing);
                }
            }
            host?.MarkDirty();
            StatusText = (present ? "Added " : "Removed ") + vm.Type + " behaviour";
        }

        #endregion


        #region file ops

        private void LoadYpt(YptFile ypt, string name, string path)
        {
            lock (RenderLock)
            {
                host?.SetPreviewEffect(null, null);
                previewedEffect = null;
                Ypt = ypt;
            }
            FileName = name;
            filePath = path;
            RebuildTree();
            //don't auto-select/play an effect on open - wait for the user to pick one from the tree
            SelectedNode = null;
            StatusText = "Loaded " + name + " - select an effect to preview (" + Effects.Count + " effects)";
        }

        private void NewFile()
        {
            var ypt = new YptFile();
            ypt.PtfxList = new ParticleEffectsList
            {
                EffectRuleDictionary = new ParticleEffectRuleDictionary(),
                EmitterRuleDictionary = new ParticleEmitterRuleDictionary(),
                ParticleRuleDictionary = new ParticleRuleDictionary(),
            };
            ypt.RebuildDicts();
            LoadYpt(ypt, "new.ypt", null);
            StatusText = "New empty file. Open a vanilla .ypt and use New Effect to start from a working template.";
        }

        private void Open()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Particle files|*.ypt;*.ypt.xml|YPT files|*.ypt|XML files|*.xml|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var path = dlg.FileName;
                YptFile ypt;
                if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    ypt = XmlYpt.GetYpt(File.ReadAllText(path), Path.GetDirectoryName(path));
                    ypt.RebuildDicts();
                    LoadYpt(ypt, Path.GetFileName(path), null);
                }
                else
                {
                    ypt = new YptFile();
                    ypt.Load(File.ReadAllBytes(path));
                    LoadYpt(ypt, Path.GetFileName(path), path);
                }
            }
            catch (Exception ex) { Error("Error opening file:\n" + ex.Message); }
        }

        private void Save(bool saveAs)
        {
            if (Ypt?.PtfxList == null) { Error("Nothing to save."); return; }
            var path = filePath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "YPT files|*.ypt|All files|*.*", FileName = FileName };
                if (dlg.ShowDialog() != true) return;
                path = dlg.FileName;
            }
            try
            {
                byte[] data;
                lock (RenderLock) { data = Ypt.Save(); }
                if (data == null) { Error("Error building file."); return; }
                File.WriteAllBytes(path, data);
                filePath = path; FileName = Path.GetFileName(path);
                StatusText = "Saved " + FileName;
            }
            catch (Exception ex) { Error("Error saving file:\n" + ex.Message); }
        }

        private void ImportXml()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "XML files|*.xml|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var path = dlg.FileName;
                var ypt = XmlYpt.GetYpt(File.ReadAllText(path), Path.GetDirectoryName(path));
                ypt.RebuildDicts();
                LoadYpt(ypt, Path.GetFileNameWithoutExtension(path), null);
            }
            catch (Exception ex) { Error("Error importing XML:\n" + ex.Message); }
        }

        private void ExportXml()
        {
            if (Ypt?.PtfxList == null) { Error("Nothing to export."); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "XML files|*.xml|All files|*.*", FileName = (FileName ?? "new.ypt") + ".xml" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var path = dlg.FileName;
                File.WriteAllText(path, YptXml.GetXml(Ypt, Path.GetDirectoryName(path)));
                StatusText = "Exported " + Path.GetFileName(path);
            }
            catch (Exception ex) { Error("Error exporting XML:\n" + ex.Message); }
        }

        #endregion


        #region structural ops

        private ParticleEffectRule SelectedEffect => SelectedNode?.OwnerEffect ?? Ypt?.AllEffects?.FirstOrDefault();

        private void NewEffect()
        {
            if (Ypt?.PtfxList?.EffectRuleDictionary == null) { Error("Open or create a file first."); return; }
            var src = SelectedEffect;
            if (src == null) { Error("New Effect duplicates an existing effect as a template.\nOpen a vanilla .ypt with at least one effect first."); return; }
            ParticleEffectRule clone;
            lock (RenderLock) { clone = YptEditUtil.NewEffectFromTemplate(Ypt, src, "new_effect"); Ypt.RebuildDicts(); }
            RebuildTree();
            SelectEffect(clone);
            StatusText = "Created effect '" + (clone.Name?.Value ?? "") + "'";
        }

        private void DuplicateSelected()
        {
            var src = SelectedNode?.OwnerEffect;
            if (src == null) { Error("Select an effect to duplicate."); return; }
            ParticleEffectRule clone;
            lock (RenderLock) { clone = YptEditUtil.NewEffectFromTemplate(Ypt, src, null); Ypt.RebuildDicts(); }
            RebuildTree();
            SelectEffect(clone);
        }

        private void DeleteSelected()
        {
            var node = SelectedNode;
            if (node?.Data == null) return;

            if (node.Kind == NodeKind.Effect && node.Data is ParticleEffectRule eff)
            {
                lock (RenderLock)
                {
                    host?.SetPreviewEffect(null, null);
                    YptEditUtil.RemoveEffect(Ypt, eff);
                    Ypt.RebuildDicts();
                }
                RebuildTree();
                if (Effects.Count > 0) { Effects[0].IsSelected = true; SelectedNode = Effects[0]; }
            }
            else if (node.Kind == NodeKind.Emitter && node.Data is ParticleEventEmitter em)
            {
                lock (RenderLock) { YptEditUtil.RemoveEmitter(node.OwnerEffect, em); }
                host?.MarkDirty();
                RebuildTree();
                SelectEffect(node.OwnerEffect);
            }
            else if (node.Kind == NodeKind.Behaviour && node.Data is ParticleBehaviour beh && node.OwnerParticleRule != null)
            {
                lock (RenderLock) { YptEditUtil.RemoveBehaviour(node.OwnerParticleRule, beh); }
                host?.MarkDirty();
                RebuildTree();
                SelectEffect(node.OwnerEffect);
            }
        }

        private void AddEmitter()
        {
            var eff = SelectedNode?.OwnerEffect;
            if (eff == null) { Error("Select an effect first."); return; }
            ParticleEventEmitter added;
            lock (RenderLock) { added = YptEditUtil.AddEmitter(eff); }
            if (added == null) { Error("This effect has no emitter to clone (blank emitters not supported yet) or it's at the 32-emitter limit."); return; }
            host?.MarkDirty();
            RebuildTree();
            SelectEffect(eff);
        }

        private void AddBehaviour(object param)
        {
            var prule = SelectedNode?.OwnerParticleRule;
            if (prule == null) { Error("Select a Particle Rule (or one of its behaviours) first."); return; }
            if (param is not ParticleBehaviourType type) return;
            lock (RenderLock) { YptEditUtil.AddBehaviour(prule, type); }
            host?.MarkDirty();
            RebuildTree();
            SelectEffect(SelectedNode?.OwnerEffect);
        }

        public ParticleBehaviourType[] BehaviourTypes => ToggleTypes;

        #endregion


        #region transport

        private void TogglePlay()
        {
            if (host == null) return;
            host.Playing = !host.Playing;
            PlayPauseText = host.Playing ? "Pause" : "Play";
        }
        private void Stop()
        {
            if (host == null) return;
            host.Playing = false;
            host.Restart();
            PlayPauseText = "Play";
        }
        private void Restart()
        {
            if (host == null) return;
            host.Restart();
            host.Playing = true;
            PlayPauseText = "Pause";
        }

        // scrub the preview to a normalized time (0..1) - called by the timeline's playhead. Pauses playback.
        public void SeekToRatio(float ratio)
        {
            host?.SeekToRatio(ratio);
            PlayPauseText = "Play"; // seeking pauses the preview
        }

        // called by the view's timer to refresh the transport readout
        public void UpdateTransportReadout()
        {
            if (host == null) return;
            TimeText = host.CurrentTime.ToString("0.00") + " / " + host.Duration.ToString("0.00") + "   (" + host.ParticleCount + " particles)";
            // keep the play/pause label in sync with the host (e.g. scrubbing pauses it)
            PlayPauseText = host.Playing ? "Pause" : "Play";
            if (!string.IsNullOrEmpty(host.lastRenderError)) StatusText = "Render error: " + host.lastRenderError;
            else if (!string.IsNullOrEmpty(host.RenderStats)) StatusText = host.RenderStats;
        }

        #endregion


        private void Error(string msg) { System.Windows.MessageBox.Show(msg); StatusText = msg; }
    }
}
