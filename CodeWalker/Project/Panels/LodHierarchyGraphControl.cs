using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeWalker.Project.Panels
{
    public class LodHierarchyGraphControl : Control
    {
        private class GraphNode
        {
            public required YmapEntityDef Entity;
            public GraphNode? Parent;
            public List<GraphNode> Children = new List<GraphNode>();
            public float X; //layout space, centre of node
            public float Y; //layout space, top of node
            public float SubtreeWidth;
            public string Line1 = string.Empty;
            public string Line2 = string.Empty;
            public string Line3 = string.Empty;
            public string? WarningText;
            public Color FillColor;

            public RectangleF Bounds
            {
                get { return new RectangleF(X - (NodeWidth * 0.5f), Y, NodeWidth, NodeHeight); }
            }
        }

        private const float NodeWidth = 200.0f;
        private const float NodeHeight = 66.0f;
        private const float SiblingGap = 18.0f;
        private const float RootGap = 60.0f;
        private const float RowGap = 80.0f;
        private const float BandGap = 140.0f;
        private const float MinZoom = 0.05f;
        private const float MaxZoom = 3.0f;

        private List<GraphNode> Nodes = new List<GraphNode>();
        private Dictionary<YmapEntityDef, GraphNode> NodeLookup = new Dictionary<YmapEntityDef, GraphNode>();

        private float Zoom = 1.0f;
        private PointF Pan = new PointF(0, 0);
        private bool Panning = false;
        private GraphNode? DragNode = null;
        private Point LastMousePos;
        private PointF DragNodeOffset;
        private bool MouseMoved = false;

        private GraphNode? SelectedNode = null; //primary selection (last clicked)
        private HashSet<GraphNode> SelectedNodes = new HashSet<GraphNode>();
        private GraphNode? MarkedNode = null;
        private bool BoxSelecting = false;
        private PointF BoxStart; //graph space
        private PointF BoxEnd;
        private GraphNode? LinkDragNode = null; //node a link is being dragged from
        private bool LinkDragFromTop = false;  //true = dragging the node's parent end, false = dragging its children end
        private PointF LinkDragPos;            //graph space position of the dangling link end
        private GraphNode? LinkDropTarget = null;

        private const float SocketRadius = 6.0f;

        private Font NodeFont;
        private Font NodeBoldFont;
        private Dictionary<int, SolidBrush> BrushCache = new Dictionary<int, SolidBrush>();

        public bool UserNavigated { get; private set; } = false; //true once the user has panned/zoomed manually

        public delegate void LinkEventHandler(YmapEntityDef child, YmapEntityDef? parent);

        public event EventHandler? SelectionChanged;
        public event EventHandler? EntityActivated;
        public event LinkEventHandler? LinkRequested; //parent == null means an unlink (orphan) request

        public YmapEntityDef? SelectedEntity
        {
            get { return SelectedNode?.Entity; }
        }

        public YmapEntityDef[] SelectedEntities
        {
            get
            {
                var ents = new List<YmapEntityDef>(SelectedNodes.Count);
                foreach (var node in SelectedNodes)
                {
                    ents.Add(node.Entity);
                }
                return ents.ToArray();
            }
        }

        private GraphNode? FirstSelected()
        {
            foreach (var node in SelectedNodes) return node;
            return null;
        }

        public YmapEntityDef? MarkedEntity
        {
            get { return MarkedNode?.Entity; }
            set
            {
                MarkedNode = ((value != null) && NodeLookup.TryGetValue(value, out var n)) ? n : null;
                Invalidate();
            }
        }


        public LodHierarchyGraphControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(42, 42, 46);
            NodeFont = new Font("Segoe UI", 8.0f);
            NodeBoldFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NodeFont?.Dispose();
                NodeBoldFont?.Dispose();
                foreach (var brush in BrushCache.Values)
                {
                    brush.Dispose();
                }
                BrushCache.Clear();
            }
            base.Dispose(disposing);
        }

        private SolidBrush GetBrush(Color c)
        {
            int key = c.ToArgb();
            if (!BrushCache.TryGetValue(key, out var brush))
            {
                brush = new SolidBrush(c);
                BrushCache[key] = brush;
            }
            return brush;
        }

        public void ResetNavigation()
        {
            UserNavigated = false;
        }


        public void SetData(List<YmapFile> ymaps, HashSet<YmapEntityDef> entities, Dictionary<YmapEntityDef, List<YmapEntityDef>> childMap, YmapEntityDef[] selEntities, bool fitView = true, bool preservePositions = false)
        {
            //when preserving (eg. after link edits), nodes that already exist keep their current
            //positions, so editing a link doesn't shuffle the layout under the user.
            Dictionary<YmapEntityDef, PointF>? oldpositions = null;
            if (preservePositions && (NodeLookup.Count > 0))
            {
                oldpositions = new Dictionary<YmapEntityDef, PointF>(NodeLookup.Count);
                foreach (var kvp in NodeLookup)
                {
                    oldpositions[kvp.Key] = new PointF(kvp.Value.X, kvp.Value.Y);
                }
            }

            Nodes.Clear();
            NodeLookup.Clear();
            SelectedNode = null;
            SelectedNodes.Clear();
            MarkedNode = null;

            if ((ymaps != null) && (entities != null))
            {
                //create nodes for all entities, in ymap order.
                foreach (var ymap in ymaps)
                {
                    if (ymap.AllEntities == null) continue;
                    foreach (var ent in ymap.AllEntities)
                    {
                        if ((ent == null) || NodeLookup.ContainsKey(ent)) continue;
                        var node = new GraphNode { Entity = ent };
                        BuildNodeText(node, childMap, entities);
                        Nodes.Add(node);
                        NodeLookup[ent] = node;
                    }
                }

                //link nodes.
                foreach (var node in Nodes)
                {
                    var p = node.Entity.Parent;
                    if ((p != null) && NodeLookup.TryGetValue(p, out var pnode) && (pnode != node))
                    {
                        node.Parent = pnode;
                        pnode.Children.Add(node);
                    }
                }

                LayoutNodes();

                if (oldpositions != null)
                {
                    foreach (var node in Nodes)
                    {
                        if (oldpositions.TryGetValue(node.Entity, out var pos))
                        {
                            node.X = pos.X;
                            node.Y = pos.Y;
                        }
                    }
                }
            }

            if (selEntities != null)
            {
                foreach (var selent in selEntities)
                {
                    if ((selent != null) && NodeLookup.TryGetValue(selent, out var seln))
                    {
                        SelectedNodes.Add(seln);
                        SelectedNode = seln;
                    }
                }
            }

            if (fitView)
            {
                FitView();
            }
            else
            {
                Invalidate();
            }
        }

        private void BuildNodeText(GraphNode node, Dictionary<YmapEntityDef, List<YmapEntityDef>> childMap, HashSet<YmapEntityDef> entities)
        {
            var ent = node.Entity;
            int actual = ((childMap != null) && childMap.TryGetValue(ent, out var clist)) ? clist.Count : 0;

            node.Line1 = ent.Name;
            node.Line2 = LodLevelString(ent._CEntityDef.lodLevel) + "   dist " + ent._CEntityDef.lodDist.ToString("0.#") + "   children " + ent._CEntityDef.numChildren.ToString();
            node.Line3 = (ent.Ymap?.Name ?? "?") + "  [" + ent.Index.ToString() + "]";
            node.FillColor = LodLevelColor(ent._CEntityDef.lodLevel);

            string? warning = null;
            if (ent._CEntityDef.numChildren != actual)
            {
                warning = "numChildren=" + ent._CEntityDef.numChildren.ToString() + ", actual=" + actual.ToString();
            }
            if ((ent.Parent != null) && (entities != null) && entities.Contains(ent.Parent) && (ent._CEntityDef.parentIndex != ent.Parent.Index))
            {
                warning = (warning != null) ? (warning + "; ") : string.Empty;
                warning += "parentIndex=" + ent._CEntityDef.parentIndex.ToString() + ", expected=" + ent.Parent.Index.ToString();
            }
            node.WarningText = warning;
        }


        private void LayoutNodes()
        {
            //x: tidy-tree layout per root (subtree width packing).
            //y: row determined by LOD depth, so the same LOD levels align across chains in a band (SLODs top, HD bottom).
            //chain trees are wrapped into multiple horizontal bands targeting a roughly 16:9 overall shape,
            //instead of one very long strip; each band is only as tall as the LOD levels it actually uses.
            //roots with no children (orphans/standalones) go in a compact grid below the bands.

            var chainRoots = new List<GraphNode>();
            var loneRoots = new List<GraphNode>();
            foreach (var node in Nodes)
            {
                if (node.Parent != null) continue;
                if (node.Children.Count > 0) chainRoots.Add(node);
                else loneRoots.Add(node);
            }

            float rowh = NodeHeight + RowGap;
            float totalw = 0.0f;
            float maxtreew = 0.0f;
            foreach (var root in chainRoots)
            {
                ComputeSubtreeWidth(root, new HashSet<GraphNode>());
                totalw += root.SubtreeWidth + RootGap;
                maxtreew = Math.Max(maxtreew, root.SubtreeWidth);
            }

            //band wrap width for a ~16:9 block, assuming ~3 LOD rows per band.
            float targetw = Math.Max(maxtreew, (float)Math.Sqrt(totalw * rowh * 3.0 * (16.0 / 9.0)));

            float bandx = 0.0f;
            float bandy = 0.0f;
            float chainsWidth = 0.0f;
            var bandnodes = new List<GraphNode>();

            void flushBand()
            {
                if (bandnodes.Count == 0) return;
                float miny = float.MaxValue;
                float maxy = float.MinValue;
                foreach (var n in bandnodes)
                {
                    miny = Math.Min(miny, n.Y);
                    maxy = Math.Max(maxy, n.Y);
                }
                foreach (var n in bandnodes) //shift the band's trees down to the current band position
                {
                    n.Y += bandy - miny;
                }
                bandy += (maxy - miny) + NodeHeight + BandGap;
                chainsWidth = Math.Max(chainsWidth, bandx - RootGap);
                bandx = 0.0f;
                bandnodes.Clear();
            }

            foreach (var root in chainRoots)
            {
                if ((bandnodes.Count > 0) && ((bandx + root.SubtreeWidth) > targetw))
                {
                    flushBand();
                }
                PlaceSubtree(root, bandx, new HashSet<GraphNode>(), bandnodes);
                bandx += root.SubtreeWidth + RootGap;
            }
            flushBand();

            if (loneRoots.Count == 0) return;

            //grid of lone roots: match the chains' width when present, otherwise aim for a roughly 16:9 block.
            float cellw = NodeWidth + SiblingGap;
            float cellh = NodeHeight + 28.0f;
            int cols;
            if (chainsWidth > cellw)
            {
                cols = Math.Max(1, (int)(chainsWidth / cellw));
            }
            else
            {
                cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(loneRoots.Count * (cellh / cellw) * (16.0 / 9.0))));
            }

            for (int i = 0; i < loneRoots.Count; i++)
            {
                var node = loneRoots[i];
                node.SubtreeWidth = NodeWidth;
                node.X = (i % cols) * cellw + (NodeWidth * 0.5f);
                node.Y = bandy + (i / cols) * cellh;
            }
        }

        private void ComputeSubtreeWidth(GraphNode node, HashSet<GraphNode> visited)
        {
            if (!visited.Add(node)) { node.SubtreeWidth = NodeWidth; return; } //cycle guard
            if (node.Children.Count == 0)
            {
                node.SubtreeWidth = NodeWidth;
                return;
            }
            float w = 0.0f;
            foreach (var child in node.Children)
            {
                ComputeSubtreeWidth(child, visited);
                w += child.SubtreeWidth;
            }
            w += SiblingGap * (node.Children.Count - 1);
            node.SubtreeWidth = Math.Max(w, NodeWidth);
        }

        private void PlaceSubtree(GraphNode node, float x, HashSet<GraphNode> visited, List<GraphNode> placed)
        {
            if (!visited.Add(node)) return; //cycle guard

            node.Y = (5 - LodDepth(node.Entity._CEntityDef.lodLevel)) * (NodeHeight + RowGap);
            placed.Add(node);

            if (node.Children.Count == 0)
            {
                node.X = x + (node.SubtreeWidth * 0.5f);
                return;
            }

            float cx = x;
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var child in node.Children)
            {
                PlaceSubtree(child, cx, visited, placed);
                min = Math.Min(min, child.X);
                max = Math.Max(max, child.X);
                cx += child.SubtreeWidth + SiblingGap;
            }
            node.X = (min + max) * 0.5f;
        }


        public void FitView()
        {
            if (Nodes.Count == 0)
            {
                Zoom = 1.0f;
                Pan = new PointF(0, 0);
                Invalidate();
                return;
            }

            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            foreach (var node in Nodes)
            {
                var b = node.Bounds;
                minx = Math.Min(minx, b.Left);
                miny = Math.Min(miny, b.Top);
                maxx = Math.Max(maxx, b.Right);
                maxy = Math.Max(maxy, b.Bottom);
            }

            float gw = Math.Max(maxx - minx, 1.0f);
            float gh = Math.Max(maxy - miny, 1.0f);
            float margin = 40.0f;
            float zx = (Width - margin * 2) / gw;
            float zy = (Height - margin * 2) / gh;
            Zoom = Math.Max(MinZoom, Math.Min(Math.Min(zx, zy), 1.0f));
            Pan = new PointF(
                (Width * 0.5f) - ((minx + maxx) * 0.5f * Zoom),
                (Height * 0.5f) - ((miny + maxy) * 0.5f * Zoom));
            Invalidate();
        }

        public void FocusEntity(YmapEntityDef? ent)
        {
            if ((ent == null) || !NodeLookup.TryGetValue(ent, out var node)) return;
            if (Zoom < 0.5f) Zoom = 1.0f;
            Pan = new PointF(
                (Width * 0.5f) - (node.X * Zoom),
                (Height * 0.5f) - ((node.Y + NodeHeight * 0.5f) * Zoom));
            Invalidate();
        }


        private PointF ScreenToGraph(Point p)
        {
            return new PointF((p.X - Pan.X) / Zoom, (p.Y - Pan.Y) / Zoom);
        }

        private GraphNode? HitTest(Point p)
        {
            var gp = ScreenToGraph(p);
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                if (Nodes[i].Bounds.Contains(gp)) return Nodes[i];
            }
            return null;
        }

        private static PointF TopSocket(GraphNode n)
        {
            return new PointF(n.X, n.Y);
        }
        private static PointF BottomSocket(GraphNode n)
        {
            return new PointF(n.X, n.Y + NodeHeight);
        }

        private GraphNode? HitTestSocket(Point p, out bool top)
        {
            var gp = ScreenToGraph(p);
            float r = Math.Max(SocketRadius * 1.5f, 8.0f / Zoom); //keep sockets grabbable when zoomed out
            float r2 = r * r;
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                var n = Nodes[i];
                var ts = TopSocket(n);
                float dx = gp.X - ts.X;
                float dy = gp.Y - ts.Y;
                if ((dx * dx + dy * dy) <= r2) { top = true; return n; }
                var bs = BottomSocket(n);
                dx = gp.X - bs.X;
                dy = gp.Y - bs.Y;
                if ((dx * dx + dy * dy) <= r2) { top = false; return n; }
            }
            top = false;
            return null;
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            //detail level by zoom: 2 = full text, 1 = title only, 0 = plain boxes.
            int detail = (Zoom >= 0.45f) ? 2 : ((Zoom >= 0.22f) ? 1 : 0);
            g.SmoothingMode = (detail > 0) ? SmoothingMode.AntiAlias : SmoothingMode.HighSpeed;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            //visible area in graph space, for culling.
            var vis = new RectangleF(-Pan.X / Zoom, -Pan.Y / Zoom, Width / Zoom, Height / Zoom);
            vis.Inflate(NodeWidth, NodeHeight);

            var state = g.Save();
            g.TranslateTransform(Pan.X, Pan.Y);
            g.ScaleTransform(Zoom, Zoom);

            //edges first.
            using (var edgePen = new Pen(Color.FromArgb(140, 160, 160, 170), 2.0f / Zoom))
            using (var warnPen = new Pen(Color.FromArgb(200, 220, 120, 60), 2.0f / Zoom))
            {
                warnPen.DashStyle = (detail > 0) ? DashStyle.Dash : DashStyle.Solid; //dashes are expensive when zoomed way out
                foreach (var node in Nodes)
                {
                    if (node.Parent == null) continue;
                    var pb = node.Parent.Bounds;
                    var cb = node.Bounds;
                    var p0 = new PointF(pb.Left + pb.Width * 0.5f, pb.Bottom);
                    var p3 = new PointF(cb.Left + cb.Width * 0.5f, cb.Top);

                    var ebounds = new RectangleF(Math.Min(p0.X, p3.X), Math.Min(p0.Y, p3.Y), Math.Abs(p3.X - p0.X) + 1, Math.Abs(p3.Y - p0.Y) + 1);
                    if (!vis.IntersectsWith(ebounds)) continue;

                    var pen = (node.Entity.Ymap != node.Parent.Entity.Ymap) ? warnPen : edgePen;
                    if (detail > 0)
                    {
                        float dy = Math.Max((p3.Y - p0.Y) * 0.5f, 20.0f);
                        var p1 = new PointF(p0.X, p0.Y + dy);
                        var p2 = new PointF(p3.X, p3.Y - dy);
                        g.DrawBezier(pen, p0, p1, p2, p3);
                    }
                    else
                    {
                        g.DrawLine(pen, p0, p3);
                    }
                }
            }

            //nodes.
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                foreach (var node in Nodes)
                {
                    if (!vis.IntersectsWith(node.Bounds)) continue;
                    DrawNode(g, node, detail, sf);
                }
            }

            //link sockets (parent end on top, children end on bottom).
            if (detail > 0)
            {
                var socketbrush = GetBrush(Color.FromArgb(190, 195, 205));
                using (var socketpen = new Pen(Color.FromArgb(60, 60, 70), 1.5f))
                {
                    foreach (var node in Nodes)
                    {
                        if (!vis.IntersectsWith(node.Bounds)) continue;
                        var ts = TopSocket(node);
                        var bs = BottomSocket(node);
                        g.FillEllipse(socketbrush, ts.X - SocketRadius, ts.Y - SocketRadius, SocketRadius * 2, SocketRadius * 2);
                        g.DrawEllipse(socketpen, ts.X - SocketRadius, ts.Y - SocketRadius, SocketRadius * 2, SocketRadius * 2);
                        g.FillEllipse(socketbrush, bs.X - SocketRadius, bs.Y - SocketRadius, SocketRadius * 2, SocketRadius * 2);
                        g.DrawEllipse(socketpen, bs.X - SocketRadius, bs.Y - SocketRadius, SocketRadius * 2, SocketRadius * 2);
                    }
                }
            }

            //dangling link while connecting/disconnecting.
            if ((LinkDragNode != null) && MouseMoved)
            {
                var p0 = LinkDragFromTop ? TopSocket(LinkDragNode) : BottomSocket(LinkDragNode);
                var p3 = LinkDragPos;
                float dy = Math.Max(Math.Abs(p3.Y - p0.Y) * 0.5f, 30.0f);
                var p1 = new PointF(p0.X, p0.Y + (LinkDragFromTop ? -dy : dy));
                var p2 = new PointF(p3.X, p3.Y + (LinkDragFromTop ? dy : -dy));
                using (var linkpen = new Pen(Color.FromArgb(230, 255, 220, 100), 2.5f / Zoom))
                {
                    linkpen.DashStyle = DashStyle.Dash;
                    g.DrawBezier(linkpen, p0, p1, p2, p3);
                }
            }

            //box selection rubber band.
            if (BoxSelecting && MouseMoved)
            {
                var br = BoxRect();
                g.FillRectangle(GetBrush(Color.FromArgb(40, 120, 170, 255)), br.X, br.Y, br.Width, br.Height);
                using (var bp = new Pen(Color.FromArgb(200, 140, 190, 255), 1.5f / Zoom))
                {
                    bp.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(bp, br.X, br.Y, br.Width, br.Height);
                }
            }

            g.Restore(state);

            DrawLegend(g);

            base.OnPaint(e);
        }

        private void DrawNode(Graphics g, GraphNode node, int detail, StringFormat sf)
        {
            var b = node.Bounds;

            bool selected = SelectedNodes.Contains(node);
            bool marked = (node == MarkedNode);
            bool droptarget = (node == LinkDropTarget);

            if (detail == 0)
            {
                //zoomed way out: plain rectangles, borders only for highlights/warnings.
                g.FillRectangle(GetBrush(node.FillColor), b.X, b.Y, b.Width, b.Height);
                if (droptarget || selected || marked || (node.WarningText != null))
                {
                    var bordercol = droptarget ? Color.FromArgb(255, 220, 100) : (selected ? Color.White : (marked ? Color.Cyan : Color.FromArgb(230, 150, 60)));
                    using (var border = new Pen(bordercol, 3.0f / Zoom))
                    {
                        g.DrawRectangle(border, b.X, b.Y, b.Width, b.Height);
                    }
                }
                return;
            }

            using (var path = RoundedRect(b, 6.0f))
            {
                g.FillPath(GetBrush(node.FillColor), path);

                Color bordercol = Color.FromArgb(90, 90, 100);
                float borderw = 1.5f;
                if (droptarget) { bordercol = Color.FromArgb(255, 220, 100); borderw = 3.5f; }
                else if (selected) { bordercol = Color.White; borderw = 3.0f; }
                else if (marked) { bordercol = Color.Cyan; borderw = 2.5f; }
                else if (node.WarningText != null) { bordercol = Color.FromArgb(230, 150, 60); borderw = 2.0f; }
                using (var border = new Pen(bordercol, borderw))
                {
                    if (marked && !selected && !droptarget) border.DashStyle = DashStyle.Dash;
                    g.DrawPath(border, path);
                }
            }

            var tb = new RectangleF(b.X + 7, b.Y + 5, b.Width - 14, b.Height - 10);
            g.DrawString(node.Line1, NodeBoldFont, GetBrush(Color.White), new RectangleF(tb.X, tb.Y, tb.Width, 15), sf);
            if (detail >= 2)
            {
                var txt2 = GetBrush(Color.FromArgb(225, 230, 230, 235));
                g.DrawString(node.Line2, NodeFont, txt2, new RectangleF(tb.X, tb.Y + 16, tb.Width, 14), sf);
                g.DrawString(node.Line3, NodeFont, txt2, new RectangleF(tb.X, tb.Y + 30, tb.Width, 14), sf);
                if (node.WarningText != null)
                {
                    g.DrawString("(!) " + node.WarningText, NodeFont, GetBrush(Color.FromArgb(255, 255, 190, 90)), new RectangleF(tb.X, tb.Y + 44, tb.Width, 14), sf);
                }
            }
        }

        private void DrawLegend(Graphics g)
        {
            var levels = new[]
            {
                rage__eLodType.LODTYPES_DEPTH_SLOD4,
                rage__eLodType.LODTYPES_DEPTH_SLOD3,
                rage__eLodType.LODTYPES_DEPTH_SLOD2,
                rage__eLodType.LODTYPES_DEPTH_SLOD1,
                rage__eLodType.LODTYPES_DEPTH_LOD,
                rage__eLodType.LODTYPES_DEPTH_HD,
                rage__eLodType.LODTYPES_DEPTH_ORPHANHD,
            };
            float y = 8.0f;
            using (var txt = new SolidBrush(Color.FromArgb(220, 220, 220, 225)))
            {
                foreach (var level in levels)
                {
                    using (var fill = new SolidBrush(LodLevelColor(level)))
                    {
                        g.FillRectangle(fill, 8, y, 14, 12);
                    }
                    g.DrawString(LodLevelString(level), NodeFont, txt, 26, y - 1);
                    y += 16.0f;
                }
                g.DrawString("wheel: zoom   right-drag: pan   left-drag: box select / move", NodeFont, txt, 8, y + 4);
            }
        }


        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            LastMousePos = e.Location;
            MouseMoved = false;

            if (e.Button == MouseButtons.Left)
            {
                var socketnode = HitTestSocket(e.Location, out var sockettop);
                if (socketnode != null) //start dragging a link from this socket
                {
                    LinkDragNode = socketnode;
                    LinkDragFromTop = sockettop;
                    LinkDragPos = ScreenToGraph(e.Location);
                    LinkDropTarget = null;
                    base.OnMouseDown(e);
                    return;
                }

                var node = HitTest(e.Location);
                bool ctrl = (ModifierKeys & Keys.Control) != 0;
                if (node != null)
                {
                    if (ctrl) //ctrl+click toggles membership in the selection
                    {
                        if (SelectedNodes.Add(node))
                        {
                            SelectedNode = node;
                        }
                        else
                        {
                            SelectedNodes.Remove(node);
                            if (SelectedNode == node) SelectedNode = FirstSelected();
                        }
                        Invalidate();
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        bool changed = false;
                        if (!SelectedNodes.Contains(node)) //click outside selection replaces it
                        {
                            SelectedNodes.Clear();
                            SelectedNodes.Add(node);
                            changed = true;
                        }
                        if (SelectedNode != node)
                        {
                            SelectedNode = node;
                            changed = true;
                        }
                        if (changed)
                        {
                            Invalidate();
                            SelectionChanged?.Invoke(this, EventArgs.Empty);
                        }
                        DragNode = node; //drag moves the whole selection
                        var gp = ScreenToGraph(e.Location);
                        DragNodeOffset = new PointF(gp.X - node.X, gp.Y - node.Y);
                    }
                }
                else //left-drag on empty space: box select
                {
                    BoxSelecting = true;
                    BoxStart = BoxEnd = ScreenToGraph(e.Location);
                }
            }
            else if ((e.Button == MouseButtons.Middle) || (e.Button == MouseButtons.Right))
            {
                Panning = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Panning)
            {
                Pan = new PointF(Pan.X + (e.X - LastMousePos.X), Pan.Y + (e.Y - LastMousePos.Y));
                LastMousePos = e.Location;
                MouseMoved = true;
                UserNavigated = true;
                Invalidate();
            }
            else if (DragNode != null)
            {
                var gp = ScreenToGraph(e.Location);
                float dx = (gp.X - DragNodeOffset.X) - DragNode.X;
                float dy = (gp.Y - DragNodeOffset.Y) - DragNode.Y;
                if ((dx != 0.0f) || (dy != 0.0f))
                {
                    foreach (var node in SelectedNodes)
                    {
                        node.X += dx;
                        node.Y += dy;
                    }
                    if (!SelectedNodes.Contains(DragNode))
                    {
                        DragNode.X += dx;
                        DragNode.Y += dy;
                    }
                }
                MouseMoved = true;
                Invalidate();
            }
            else if (LinkDragNode != null)
            {
                LinkDragPos = ScreenToGraph(e.Location);
                var target = HitTestSocket(e.Location, out _) ?? HitTest(e.Location);
                LinkDropTarget = (target != LinkDragNode) ? target : null;
                MouseMoved = true;
                Invalidate();
            }
            else if (BoxSelecting)
            {
                BoxEnd = ScreenToGraph(e.Location);
                MouseMoved = true;
                Invalidate();
            }
            else
            {
                //cursor feedback over link sockets.
                Cursor = (HitTestSocket(e.Location, out _) != null) ? Cursors.Cross : Cursors.Default;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if ((LinkDragNode != null) && (e.Button == MouseButtons.Left))
            {
                var dragnode = LinkDragNode;
                var target = LinkDropTarget;
                LinkDragNode = null;
                LinkDropTarget = null;
                Invalidate();
                if (MouseMoved)
                {
                    if (LinkDragFromTop) //dragging this node's parent end
                    {
                        if ((target != null) && (target != dragnode))
                        {
                            LinkRequested?.Invoke(dragnode.Entity, target.Entity); //connect: target becomes the parent
                        }
                        else if ((target == null) && (dragnode.Entity.Parent != null))
                        {
                            LinkRequested?.Invoke(dragnode.Entity, null); //dropped in space: disconnect from parent
                        }
                    }
                    else //dragging this node's children end
                    {
                        if ((target != null) && (target != dragnode))
                        {
                            LinkRequested?.Invoke(target.Entity, dragnode.Entity); //connect: target becomes a child
                        }
                    }
                }
                Panning = false;
                DragNode = null;
                base.OnMouseUp(e);
                return;
            }

            if (BoxSelecting && (e.Button == MouseButtons.Left))
            {
                BoxSelecting = false;
                bool ctrl = (ModifierKeys & Keys.Control) != 0;
                bool changed = false;
                if (MouseMoved)
                {
                    var rect = BoxRect();
                    if (!ctrl && (SelectedNodes.Count > 0))
                    {
                        SelectedNodes.Clear();
                        changed = true;
                    }
                    foreach (var node in Nodes)
                    {
                        if (rect.IntersectsWith(node.Bounds) && SelectedNodes.Add(node))
                        {
                            SelectedNode = node;
                            changed = true;
                        }
                    }
                    if ((SelectedNode == null) || !SelectedNodes.Contains(SelectedNode))
                    {
                        SelectedNode = FirstSelected();
                    }
                }
                else if (!ctrl && (SelectedNodes.Count > 0)) //plain click on empty space deselects
                {
                    SelectedNodes.Clear();
                    SelectedNode = null;
                    changed = true;
                }
                Invalidate();
                if (changed)
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            Panning = false;
            DragNode = null;
            base.OnMouseUp(e);
        }

        private RectangleF BoxRect()
        {
            return new RectangleF(
                Math.Min(BoxStart.X, BoxEnd.X),
                Math.Min(BoxStart.Y, BoxEnd.Y),
                Math.Abs(BoxEnd.X - BoxStart.X),
                Math.Abs(BoxEnd.Y - BoxStart.Y));
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && !MouseMoved)
            {
                var node = HitTest(e.Location);
                if (node != null)
                {
                    SelectedNode = node;
                    Invalidate();
                    EntityActivated?.Invoke(this, EventArgs.Empty);
                }
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            float oldzoom = Zoom;
            float factor = (e.Delta > 0) ? 1.15f : (1.0f / 1.15f);
            Zoom = Math.Max(MinZoom, Math.Min(Zoom * factor, MaxZoom));
            UserNavigated = true;
            //zoom around the cursor position.
            Pan = new PointF(
                e.X - ((e.X - Pan.X) / oldzoom) * Zoom,
                e.Y - ((e.Y - Pan.Y) / oldzoom) * Zoom);
            Invalidate();
            base.OnMouseWheel(e);
        }


        private static GraphicsPath RoundedRect(RectangleF b, float r)
        {
            var path = new GraphicsPath();
            float d = r * 2;
            path.AddArc(b.X, b.Y, d, d, 180, 90);
            path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
            path.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            path.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static string LodLevelString(rage__eLodType t)
        {
            return t.ToString().Replace("LODTYPES_DEPTH_", "");
        }

        internal static int LodDepth(rage__eLodType t)
        {
            switch (t)
            {
                case rage__eLodType.LODTYPES_DEPTH_HD:
                case rage__eLodType.LODTYPES_DEPTH_ORPHANHD: return 0;
                case rage__eLodType.LODTYPES_DEPTH_LOD: return 1;
                case rage__eLodType.LODTYPES_DEPTH_SLOD1: return 2;
                case rage__eLodType.LODTYPES_DEPTH_SLOD2: return 3;
                case rage__eLodType.LODTYPES_DEPTH_SLOD3: return 4;
                case rage__eLodType.LODTYPES_DEPTH_SLOD4: return 5;
                default: return 0;
            }
        }

        internal static Color LodLevelColor(rage__eLodType t)
        {
            switch (t)
            {
                case rage__eLodType.LODTYPES_DEPTH_HD: return Color.FromArgb(46, 110, 58);       //green
                case rage__eLodType.LODTYPES_DEPTH_ORPHANHD: return Color.FromArgb(85, 88, 94);  //grey
                case rage__eLodType.LODTYPES_DEPTH_LOD: return Color.FromArgb(160, 116, 36);     //amber
                case rage__eLodType.LODTYPES_DEPTH_SLOD1: return Color.FromArgb(38, 102, 140);   //light blue
                case rage__eLodType.LODTYPES_DEPTH_SLOD2: return Color.FromArgb(48, 72, 150);    //blue
                case rage__eLodType.LODTYPES_DEPTH_SLOD3: return Color.FromArgb(102, 56, 140);   //purple
                case rage__eLodType.LODTYPES_DEPTH_SLOD4: return Color.FromArgb(140, 46, 60);    //red
                default: return Color.FromArgb(85, 88, 94);
            }
        }
    }
}
