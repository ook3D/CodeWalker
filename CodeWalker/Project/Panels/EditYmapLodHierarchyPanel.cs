using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace CodeWalker.Project.Panels
{
    public partial class EditYmapLodHierarchyPanel : ProjectPanel
    {
        public ProjectForm ProjectForm;
        public YmapFile? Ymap { get; set; }

        private List<YmapFile> CurrentYmaps = new List<YmapFile>();
        private HashSet<YmapEntityDef> CurrentEntities = new HashSet<YmapEntityDef>();
        private Dictionary<YmapEntityDef, List<YmapEntityDef>> ChildMap = new Dictionary<YmapEntityDef, List<YmapEntityDef>>();
        private YmapEntityDef? MarkedParent = null;

        private List<uint> PendingYmapHashes = new List<uint>();
        private Timer? LoadTimer = null;
        private int LoadAttempts = 0;
        private long LastAutoRefreshTime = 0;
        private bool AutoRefreshPending = false;

        public EditYmapLodHierarchyPanel(ProjectForm projectForm)
        {
            ProjectForm = projectForm;
            InitializeComponent();
            Disposed += (s, e) => { LoadTimer?.Dispose(); LoadTimer = null; };
        }

        public void SetYmap(YmapFile ymap)
        {
            var ymapchanged = (ymap != Ymap);
            Ymap = ymap;
            Tag = ymap;
            UpdateFormTitle();
            if (ymapchanged) GraphView.ResetNavigation(); //allow auto-fit again for the new hierarchy
            BeginAddHierarchyToProject();
            RefreshHierarchy(!ymapchanged); //keep node positions when re-showing the same hierarchy
        }


        private void BeginAddHierarchyToProject()
        {
            //find the full ymap LOD hierarchy this ymap belongs to (via the cache.dat map hierarchy),
            //and add all of its ymaps to the project so the whole chain is visible and editable.
            PendingYmapHashes.Clear();
            LoadAttempts = 0;

            var gfc = ProjectForm.GameFileCache;
            if ((Ymap == null) || (gfc == null) || !gfc.IsInited) return;

            uint hash = Ymap.RpfFileEntry?.ShortNameHash ?? Ymap._CMapData.name.Hash;

            //walk up to the root of the hierarchy.
            var node = gfc.GetMapNode(hash);
            if ((node == null) && (Ymap._CMapData.parent != 0)) //custom ymap parented to a vanilla one
            {
                node = gfc.GetMapNode(Ymap._CMapData.parent.Hash);
            }
            while ((node != null) && (node.ParentName != 0))
            {
                var pnode = gfc.GetMapNode(node.ParentName.Hash);
                if (pnode == null) break;
                node = pnode;
            }

            //collect the whole subtree from the root down.
            var hashes = new List<uint>();
            CollectMapNodeNames(node, hashes, 0);
            if (!hashes.Contains(hash)) hashes.Add(hash);

            PendingYmapHashes.AddRange(hashes);
            ProcessPendingYmaps();
        }

        private static void CollectMapNodeNames(MapDataStoreNode? node, List<uint> hashes, int depth)
        {
            if ((node == null) || (depth > 10) || (hashes.Count > 500)) return;
            hashes.Add(node.Name.Hash);
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectMapNodeNames(child, hashes, depth + 1);
                }
            }
        }

        private void ProcessPendingYmaps()
        {
            var gfc = ProjectForm.GameFileCache;
            if ((gfc == null) || !gfc.IsInited)
            {
                PendingYmapHashes.Clear();
                return;
            }

            bool added = false;
            for (int i = PendingYmapHashes.Count - 1; i >= 0; i--)
            {
                var ymap = gfc.GetYmap(PendingYmapHashes[i]); //also (re)enqueues the load if not loaded yet
                if (ymap == null)
                {
                    PendingYmapHashes.RemoveAt(i); //no such ymap
                    continue;
                }
                if (!ymap.Loaded) continue; //still loading - check again on the next timer tick

                PendingYmapHashes.RemoveAt(i);
                if (!ProjectForm.YmapExistsInProject(ymap))
                {
                    ProjectForm.AddYmapToProject(ymap, false, false); //quiet add - no has-changed mark, no selection change
                    added = true;
                }
            }

            bool keepPolling = (PendingYmapHashes.Count > 0) && (LoadAttempts++ < 60); //keep polling for up to ~15 seconds

            if (added)
            {
                //rebuilding the graph isn't free, so while ymaps are still streaming in, refresh at most once a second.
                var now = Environment.TickCount64;
                if (!keepPolling || ((now - LastAutoRefreshTime) >= 1000))
                {
                    LastAutoRefreshTime = now;
                    AutoRefreshPending = false;
                    RefreshHierarchy(false); //fresh layout while the hierarchy is still streaming in
                }
                else
                {
                    AutoRefreshPending = true;
                }
            }
            else if (!keepPolling && AutoRefreshPending)
            {
                AutoRefreshPending = false;
                RefreshHierarchy(false);
            }

            if (keepPolling)
            {
                if (LoadTimer == null)
                {
                    LoadTimer = new Timer();
                    LoadTimer.Interval = 250;
                    LoadTimer.Tick += (s, e) => { LoadTimer.Stop(); ProcessPendingYmaps(); };
                }
                LoadTimer.Start();
            }
            else
            {
                PendingYmapHashes.Clear();
            }
        }

        private void UpdateFormTitle()
        {
            Text = "LOD Hierarchy" + ((Ymap != null) ? (" - " + Ymap.Name) : string.Empty);
        }

        private YmapEntityDef? SelectedEntity
        {
            get { return GraphView.SelectedEntity; }
        }


        public void RefreshHierarchy(bool preservePositions = true)
        {
            var selents = GraphView.SelectedEntities;

            CollectYmaps();
            RunLocked(() =>
            {
                ConnectYmaps();
                BuildChildMap();
            });

            //keep the user's pan/zoom once they've started navigating; auto-fit otherwise.
            GraphView.SetData(CurrentYmaps, CurrentEntities, ChildMap, selents, !GraphView.UserNavigated, preservePositions);

            if ((MarkedParent != null) && !CurrentEntities.Contains(MarkedParent))
            {
                MarkedParent = null;
            }
            GraphView.MarkedEntity = MarkedParent;

            UpdateDetails();
            UpdateButtonStates();
        }

        private void CollectYmaps()
        {
            CurrentYmaps.Clear();
            if (Ymap == null) return;

            void add(YmapFile? y)
            {
                if ((y != null) && y.Loaded && !CurrentYmaps.Contains(y)) CurrentYmaps.Add(y);
            }
            bool isname(YmapFile y, MetaHash hash)
            {
                if (y == null) return false;
                if (y._CMapData.name == hash) return true;
                if ((y.RpfFileEntry != null) && (y.RpfFileEntry.ShortNameHash == hash)) return true;
                return false;
            }

            add(Ymap);

            var projymaps = ProjectForm?.CurrentProjectFile?.YmapFiles;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var y in CurrentYmaps.ToArray())
                {
                    int count = CurrentYmaps.Count;
                    add(y.Parent);
                    if (y.ChildYmaps != null)
                    {
                        foreach (var c in y.ChildYmaps) add(c);
                    }
                    if (projymaps != null)
                    {
                        foreach (var p in projymaps)
                        {
                            if (p == null) continue;
                            if ((p._CMapData.parent != 0) && isname(y, p._CMapData.parent)) add(p); //p is a child ymap of y
                            if ((y._CMapData.parent != 0) && isname(p, y._CMapData.parent)) add(p); //p is the parent ymap of y
                        }
                    }
                    if (CurrentYmaps.Count != count) changed = true;
                }
            }
        }

        private void ConnectYmaps()
        {
            //make sure cross-ymap entity Parent references are resolved for all ymaps in view.
            foreach (var y in CurrentYmaps)
            {
                if (y._CMapData.parent == 0) continue;
                if (y.Parent != null) continue;
                foreach (var p in CurrentYmaps)
                {
                    if (p == y) continue;
                    if ((p._CMapData.name == y._CMapData.parent) ||
                        ((p.RpfFileEntry != null) && (p.RpfFileEntry.ShortNameHash == y._CMapData.parent)))
                    {
                        y.ConnectToParent(p);
                        break;
                    }
                }
            }
        }

        private void BuildChildMap()
        {
            CurrentEntities.Clear();
            ChildMap.Clear();

            foreach (var y in CurrentYmaps)
            {
                if (y.AllEntities == null) continue;
                foreach (var ent in y.AllEntities)
                {
                    if (ent != null) CurrentEntities.Add(ent);
                }
            }

            foreach (var ent in CurrentEntities)
            {
                var p = ent.Parent;
                if ((p == null) || !CurrentEntities.Contains(p)) continue;
                if (!ChildMap.TryGetValue(p, out var clist))
                {
                    clist = new List<YmapEntityDef>();
                    ChildMap[p] = clist;
                }
                clist.Add(ent);

                //reconcile cross-ymap links into the parent's merged children array, so that
                //subtree operations (eg. delete with children) can see them.
                if (p.Ymap != ent.Ymap)
                {
                    var merged = p.ChildrenMerged ?? p.Children;
                    bool found = false;
                    if (merged != null)
                    {
                        foreach (var c in merged)
                        {
                            if (c == ent) { found = true; break; }
                        }
                    }
                    if (!found)
                    {
                        var newmerged = new List<YmapEntityDef>();
                        if (merged != null) newmerged.AddRange(merged);
                        newmerged.Add(ent);
                        p.ChildrenMerged = newmerged.ToArray();
                    }
                }
            }
        }

        private static string LodLevelString(rage__eLodType t)
        {
            return LodHierarchyGraphControl.LodLevelString(t);
        }

        private static int LodDepth(rage__eLodType t)
        {
            return LodHierarchyGraphControl.LodDepth(t);
        }


        private void UpdateDetails()
        {
            var ents = GraphView.SelectedEntities;
            if (ents.Length > 1)
            {
                const int maxlist = 30;
                var msb = new StringBuilder();
                msb.AppendLine(ents.Length.ToString() + " entities selected:");
                for (int i = 0; (i < ents.Length) && (i < maxlist); i++)
                {
                    var e = ents[i];
                    msb.AppendLine(e.Name + " [" + LodLevelString(e._CEntityDef.lodLevel) + "] (" + (e.Ymap?.Name ?? "?") + ")");
                }
                if (ents.Length > maxlist)
                {
                    msb.AppendLine("... and " + (ents.Length - maxlist).ToString() + " more");
                }
                DetailsTextBox.Text = msb.ToString();
                return;
            }

            var ent = SelectedEntity;
            if (ent == null)
            {
                DetailsTextBox.Text = "(No entity selected)";
                return;
            }
            int actual = ChildMap.TryGetValue(ent, out var clist) ? clist.Count : 0;
            var sb = new StringBuilder();
            sb.AppendLine("Archetype: " + ent.Name);
            sb.AppendLine("Ymap: " + (ent.Ymap?.Name ?? "(none)"));
            sb.AppendLine("Index: " + ent.Index.ToString());
            sb.AppendLine("Position: " + FloatUtil.GetVector3String(ent.Position));
            sb.AppendLine("Lod level: " + LodLevelString(ent._CEntityDef.lodLevel));
            sb.AppendLine("Lod dist: " + FloatUtil.ToString(ent._CEntityDef.lodDist));
            sb.AppendLine("Child lod dist: " + FloatUtil.ToString(ent._CEntityDef.childLodDist));
            sb.AppendLine("Num children: " + ent._CEntityDef.numChildren.ToString() + " (actual: " + actual.ToString() + ")");
            sb.AppendLine("Parent index: " + ent._CEntityDef.parentIndex.ToString());
            sb.AppendLine("Parent: " + (ent.Parent?.Name ?? "(none)"));
            sb.AppendLine("Parent ymap: " + (ent.Parent?.Ymap?.Name ?? "(none)"));
            sb.AppendLine("LOD in parent ymap flag: " + ent.LodInParentYmap.ToString());
            DetailsTextBox.Text = sb.ToString();
        }

        private void UpdateButtonStates()
        {
            var ents = GraphView.SelectedEntities;
            var ent = SelectedEntity;
            bool any = (ents.Length > 0);
            bool anyparent = false;
            foreach (var e in ents)
            {
                if (e.Parent != null) { anyparent = true; break; }
            }
            DeleteEntityButton.Enabled = any;
            DeleteEntityButton.Text = (ents.Length > 1) ? ("Delete " + ents.Length.ToString() + " Entities") : "Delete Entity";
            UnlinkParentButton.Enabled = anyparent;
            MarkParentButton.Enabled = (ent != null) && (ents.Length == 1); //marking needs an unambiguous single selection
            SetParentButton.Enabled = (MarkedParent != null) && any && !((ents.Length == 1) && (ents[0] == MarkedParent));
            GotoButton.Enabled = (ent != null) && (ProjectForm?.WorldForm != null);
            MarkedParentLabel.Text = (MarkedParent != null)
                ? ("Marked parent: " + MarkedParent.Name + " (" + (MarkedParent.Ymap?.Name ?? "?") + ")")
                : "Marked parent: (none)";
        }

        private void RunLocked(Action a)
        {
            var wf = ProjectForm?.WorldForm;
            if (wf != null)
            {
                lock (wf.RenderSyncRoot)
                {
                    a();
                }
            }
            else
            {
                a();
            }
        }

        private void MarkYmapChanged(YmapFile? ymap)
        {
            if (ymap == null) return;
            ProjectForm?.ProjectExplorer?.SetYmapHasChanged(ymap, true);
        }

        private bool EnsureYmapEditable(YmapFile? ymap)
        {
            if (ymap == null) return false;
            if (ProjectForm == null) return false;
            if (ProjectForm.YmapExistsInProject(ymap)) return true;
            if (MessageBox.Show(ymap.Name + " is not in the current project, so changes to it can't be saved yet.\nAdd it to the project now?", "Add ymap to project", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                ProjectForm.AddYmapToProject(ymap);
            }
            return true;
        }


        private void DeleteSelectedEntity()
        {
            var ents = GraphView.SelectedEntities;
            if (ents.Length == 0) return;

            bool delchildren = DeleteChildrenCheckBox.Checked;

            //gather the full set that will go, for an accurate confirmation.
            var doomed = new HashSet<YmapEntityDef>();
            foreach (var ent in ents)
            {
                GatherSubtree(ent, doomed, delchildren);
            }
            int desccount = doomed.Count - new HashSet<YmapEntityDef>(ents).Count;

            var msg = new StringBuilder();
            if (ents.Length == 1)
            {
                msg.AppendLine("Delete entity " + ents[0].Name + " from " + (ents[0].Ymap?.Name ?? "?") + "?");
            }
            else
            {
                msg.AppendLine("Delete " + ents.Length.ToString() + " selected entities?");
            }
            if (delchildren && (desccount > 0))
            {
                msg.AppendLine();
                msg.AppendLine(desccount.ToString() + " LOD descendant(s) will also be DELETED.");
            }
            else if (!delchildren)
            {
                bool anychildren = false;
                foreach (var ent in ents)
                {
                    if (ChildMap.TryGetValue(ent, out var cl) && (cl.Count > 0)) { anychildren = true; break; }
                }
                if (anychildren)
                {
                    msg.AppendLine();
                    msg.AppendLine("Their direct LOD children will be orphaned (parentIndex set to -1).");
                }
            }
            msg.AppendLine();
            msg.Append("This operation cannot be undone.");
            if (MessageBox.Show(msg.ToString(), "Confirm delete", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            foreach (var ymap in DistinctYmaps(ents))
            {
                if (!EnsureYmapEditable(ymap)) return;
            }

            foreach (var ent in ents)
            {
                //an earlier subtree delete may have already removed this entity.
                if (!IsEntityInYmap(ent)) continue;
                ProjectForm.DeleteYmapEntity(ent, delchildren);
            }

            RefreshHierarchy();
        }

        private void GatherSubtree(YmapEntityDef? ent, HashSet<YmapEntityDef> result, bool includeChildren)
        {
            if ((ent == null) || !result.Add(ent)) return;
            if (!includeChildren) return;
            if (ChildMap.TryGetValue(ent, out var clist))
            {
                foreach (var child in clist)
                {
                    GatherSubtree(child, result, true);
                }
            }
        }

        private static bool IsEntityInYmap(YmapEntityDef ent)
        {
            var allents = ent.Ymap?.AllEntities;
            if (allents == null) return false;
            return (ent.Index >= 0) && (ent.Index < allents.Length) && (allents[ent.Index] == ent);
        }

        private List<YmapFile> DistinctYmaps(YmapEntityDef[] ents)
        {
            var ymaps = new List<YmapFile>();
            foreach (var ent in ents)
            {
                if ((ent?.Ymap != null) && !ymaps.Contains(ent.Ymap)) ymaps.Add(ent.Ymap);
            }
            return ymaps;
        }

        private void UnlinkSelectedEntity()
        {
            UnlinkEntities(GraphView.SelectedEntities);
        }

        private void UnlinkEntities(YmapEntityDef[] candidates)
        {
            var ents = new List<YmapEntityDef>();
            foreach (var e in candidates)
            {
                if (e?.Parent != null) ents.Add(e);
            }
            if (ents.Count == 0) return;

            string msg = (ents.Count == 1)
                ? ("Unlink " + ents[0].Name + " from its LOD parent " + ents[0].Parent?.Name + "?\nIt will become an orphan (parentIndex -1), and the parent's numChildren will be decremented.")
                : ("Unlink " + ents.Count.ToString() + " entities from their LOD parents?\nThey will become orphans (parentIndex -1), and their parents' numChildren will be decremented.");
            if (MessageBox.Show(msg, "Confirm unlink", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            foreach (var ymap in DistinctYmaps(ents.ToArray()))
            {
                if (!EnsureYmapEditable(ymap)) return;
            }

            var changedymaps = new HashSet<YmapFile>();
            RunLocked(() =>
            {
                foreach (var ent in ents)
                {
                    if (ent.Parent == null) continue;
                    if (ent.Parent.Ymap != null) changedymaps.Add(ent.Parent.Ymap);
                    if (ent.Ymap != null) changedymaps.Add(ent.Ymap);
                    ent.SetLodParent(null);
                }
            });
            foreach (var ymap in changedymaps)
            {
                MarkYmapChanged(ymap);
            }

            RefreshHierarchy();
        }

        private void SetParentOfSelectedEntity()
        {
            SetParentForEntities(GraphView.SelectedEntities, MarkedParent);
        }

        private void SetParentForEntities(YmapEntityDef[] candidates, YmapEntityDef? newp)
        {
            if (newp == null) return;

            //candidates: entities that aren't the new parent and aren't already its children,
            //excluding any that would create a cycle (the new parent being their own LOD descendant).
            var targets = new List<YmapEntityDef>();
            int cycleskips = 0;
            foreach (var ent in candidates)
            {
                if ((ent == null) || (ent == newp) || (ent.Parent == newp)) continue;
                bool cycle = false;
                var test = newp;
                while (test != null)
                {
                    if (test == ent) { cycle = true; break; }
                    test = test.Parent;
                }
                if (cycle) { cycleskips++; continue; }
                targets.Add(ent);
            }
            if (targets.Count == 0)
            {
                if (cycleskips > 0)
                {
                    MessageBox.Show("Can't set parent: " + newp.Name + " is a LOD descendant of the selected entit" + ((cycleskips == 1) ? "y" : "ies") + ".");
                }
                return;
            }

            var warnings = new StringBuilder();
            int pdepth = LodDepth(newp._CEntityDef.lodLevel);
            if (newp._CEntityDef.lodLevel == rage__eLodType.LODTYPES_DEPTH_ORPHANHD)
            {
                warnings.AppendLine("- The new parent's lod level is ORPHANHD - orphans can't have LOD children in the game.");
            }
            int levelissues = 0;
            int orderissues = 0;
            foreach (var ent in targets)
            {
                if ((newp._CEntityDef.lodLevel != rage__eLodType.LODTYPES_DEPTH_ORPHANHD) && (pdepth <= LodDepth(ent._CEntityDef.lodLevel))) levelissues++;
                if ((newp.Ymap == ent.Ymap) && (newp.Index > ent.Index)) orderissues++;
            }
            if (levelissues > 0)
            {
                warnings.AppendLine("- " + levelissues.ToString() + " entit" + ((levelissues == 1) ? "y's" : "ies'") + " lod level is not below the new parent's (" + LodLevelString(newp._CEntityDef.lodLevel) + ").");
            }
            if (orderissues > 0)
            {
                warnings.AppendLine("- " + orderissues.ToString() + " entit" + ((orderissues == 1) ? "y" : "ies") + " come(s) before the parent's index in the same ymap - the game requires parents to come before children within a ymap.");
            }
            if (cycleskips > 0)
            {
                warnings.AppendLine("- " + cycleskips.ToString() + " selected entit" + ((cycleskips == 1) ? "y" : "ies") + " skipped (would create a LOD cycle).");
            }
            if (warnings.Length > 0)
            {
                if (MessageBox.Show("Possible problems with this LOD parent assignment:\n" + warnings.ToString() + "\nContinue anyway?", "LOD chain warning", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            }

            foreach (var ymap in DistinctYmaps(targets.ToArray()))
            {
                if (!EnsureYmapEditable(ymap)) return;
            }

            //cross-ymap links need the child ymap's parent ymap set for the game to connect them.
            if (newp.Ymap != null)
            {
                var pname = newp.Ymap._CMapData.name;
                if ((pname == 0) && (newp.Ymap.RpfFileEntry != null)) pname = newp.Ymap.RpfFileEntry.ShortNameHash;
                foreach (var cymap in DistinctYmaps(targets.ToArray()))
                {
                    if ((cymap == newp.Ymap) || (cymap._CMapData.parent == pname)) continue;
                    if (MessageBox.Show("Some entities are in " + cymap.Name + ", whose parent ymap is not set to " + newp.Ymap.Name + ".\nThe game requires the child ymap's parent to be set for cross-ymap LOD linking.\n\nSet " + cymap.Name + "'s parent ymap to " + newp.Ymap.Name + "?", "Set ymap parent", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        var cmap = cymap._CMapData;
                        cmap.parent = pname;
                        cymap._CMapData = cmap;
                        cymap.HasChanged = true;
                    }
                }
            }

            var changedymaps = new HashSet<YmapFile>();
            if (newp.Ymap != null) changedymaps.Add(newp.Ymap);
            RunLocked(() =>
            {
                foreach (var ent in targets)
                {
                    if (ent.Parent?.Ymap != null) changedymaps.Add(ent.Parent.Ymap);
                    if (ent.Ymap != null) changedymaps.Add(ent.Ymap);
                    ent.SetLodParent(newp);
                }
            });
            foreach (var ymap in changedymaps)
            {
                MarkYmapChanged(ymap);
            }

            RefreshHierarchy();
        }

        private void RecalcLodValues()
        {
            if (CurrentEntities.Count == 0) return;

            if (MessageBox.Show("Recalculate numChildren and childLodDist for all " + CurrentEntities.Count.ToString() + " entities in view, from the actual LOD hierarchy?", "Confirm recalculate", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            var changedymaps = new HashSet<YmapFile>();
            RunLocked(() =>
            {
                foreach (var ent in CurrentEntities)
                {
                    uint actual = 0;
                    float maxdist = 0.0f;
                    if (ChildMap.TryGetValue(ent, out var clist))
                    {
                        actual = (uint)clist.Count;
                        foreach (var child in clist)
                        {
                            if (child._CEntityDef.lodDist > maxdist) maxdist = child._CEntityDef.lodDist;
                        }
                    }
                    bool changed = false;
                    if (ent._CEntityDef.numChildren != actual)
                    {
                        ent._CEntityDef.numChildren = actual;
                        changed = true;
                    }
                    if ((actual > 0) && (ent._CEntityDef.childLodDist != maxdist))
                    {
                        ent._CEntityDef.childLodDist = maxdist;
                        ent.ChildLodDist = (maxdist > 0) ? maxdist : (ent.LodDist * 0.5f);
                        changed = true;
                    }
                    if (changed && (ent.Ymap != null))
                    {
                        ent.Ymap.HasChanged = true;
                        ent.Ymap.LodManagerUpdate = true;
                        changedymaps.Add(ent.Ymap);
                    }
                }
            });

            foreach (var y in changedymaps)
            {
                MarkYmapChanged(y);
            }

            MessageBox.Show((changedymaps.Count > 0)
                ? ("Updated entities in " + changedymaps.Count.ToString() + " ymap(s).")
                : "All numChildren and childLodDist values were already correct.");

            RefreshHierarchy();
        }


        private void GraphView_SelectionChanged(object sender, EventArgs e)
        {
            UpdateDetails();
            UpdateButtonStates();
            SelectInWorld(GraphView.SelectedEntities);
        }

        private void SelectInWorld(YmapEntityDef[] ents)
        {
            var wf = ProjectForm?.WorldForm;
            if (wf == null) return;
            if ((ents == null) || (ents.Length == 0))
            {
                wf.SelectItem(null, false, false, false);
                return;
            }
            object obj = (ents.Length == 1) ? ents[0] : (object)ents; //object[] selects multiple
            var ms = MapSelection.FromProjectObject(wf, obj);
            //notifyProject false, so node clicks don't churn the project window's panels.
            wf.SelectItem(ms, false, false, false);
        }

        private void GraphView_EntityActivated(object sender, EventArgs e)
        {
            var ent = GraphView.SelectedEntity;
            if (ent == null) return;
            ProjectForm?.ShowProjectItem(ent, true);
        }

        private void GraphView_LinkRequested(YmapEntityDef? child, YmapEntityDef parent)
        {
            if (child == null) return;
            if (parent == null)
            {
                UnlinkEntities(new[] { child });
            }
            else
            {
                SetParentForEntities(new[] { child }, parent);
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RefreshHierarchy(false); //explicit refresh re-runs the layout
        }

        private void FitViewButton_Click(object sender, EventArgs e)
        {
            GraphView.FitView();
        }

        private void GotoButton_Click(object sender, EventArgs e)
        {
            var ent = SelectedEntity;
            if (ent == null) return;
            ProjectForm?.WorldForm?.GoToPosition(ent.Position, ent.BBExtent);
        }

        private void DeleteEntityButton_Click(object sender, EventArgs e)
        {
            DeleteSelectedEntity();
        }

        private void UnlinkParentButton_Click(object sender, EventArgs e)
        {
            UnlinkSelectedEntity();
        }

        private void MarkParentButton_Click(object sender, EventArgs e)
        {
            MarkedParent = SelectedEntity;
            GraphView.MarkedEntity = MarkedParent;
            UpdateButtonStates();
        }

        private void SetParentButton_Click(object sender, EventArgs e)
        {
            SetParentOfSelectedEntity();
        }

        private void RecalcButton_Click(object sender, EventArgs e)
        {
            RecalcLodValues();
        }
    }
}
