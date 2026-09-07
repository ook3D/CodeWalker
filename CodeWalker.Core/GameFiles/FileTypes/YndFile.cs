using CodeWalker.World;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;
using System.Diagnostics.CodeAnalysis;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))] public class YndFile : GameFile, PackedFile, BasePathData
    {

        public NodeDictionary NodeDictionary { get; set; } = new();

        public YndNode[] Nodes { get; set; } = [];
        public YndLink[] Links { get; set; } = [];
        public YndJunction[] Junctions { get; set; } = [];

        public EditorVertex[] LinkedVerts { get; set; } = [];//populated by the space (needs to use grid of all ynd's!)
        public EditorVertex[] TriangleVerts { get; set; } = []; //used for junctions display
        public Vector4[] NodePositions { get; set; } = [];

        public Vector3 BBMin { get; set; }
        public Vector3 BBMax { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }

        public bool ShouldRecalculateIndices { get; set; }

        public int AreaID
        {
            get
            {
                return CellY * 32 + CellX;
            }
            set
            {
                CellX = value % 32;
                CellY = value / 32;
                UpdateBoundingBox();
            }
        }

        public PathBVH? BVH { get; set; }



        //fields used by the editor:
        public bool HasChanged { get; set; } = false;
        public List<string>? SaveWarnings;

        public bool BuildStructsOnSave { get; set; } = true;


        public YndFile() : base(null, GameFileType.Ynd)
        {
        }
        public YndFile(RpfFileEntry entry) : base(entry, GameFileType.Ynd)
        {
        }



        public void Load(byte[] data)
        {
            //direct load from a raw, compressed ynd file (openIV-compatible format)

            RpfFile.LoadResourceFile(this, data, 1);

            Loaded = true;
            LoadQueued = true;
        }

        public void Load(byte[] data, RpfFileEntry entry)
        {
            Name = entry.Name;
            RpfFileEntry = entry;

            RpfResourceFileEntry? resentry = entry as RpfResourceFileEntry;
            if (resentry == null)
            {
                throw new Exception("File entry wasn't a resource! (is it binary data?)");
            }

            ResourceDataReader rd = new(resentry, data);


            NodeDictionary = rd.ReadRequiredBlock<NodeDictionary>();

            InitNodesFromDictionary();

            UpdateAllNodePositions();

            //links will be populated by the space... maybe move that code here?



            string areaidstr = Name.ToLowerInvariant().Replace("nodes", "").Replace(".ynd", "");
            int areaid = 0;
            int.TryParse(areaidstr, out areaid);
            AreaID = areaid;

            UpdateBoundingBox();


            BuildBVH();


            Loaded = true;
            LoadQueued = true;
        }


        public byte[] Save()
        {
            if (BuildStructsOnSave)
            {
                RecalculateNodeIndices();
                BuildStructs();
            }

            byte[] data = ResourceBuilder.Build(NodeDictionary, 1); //ynd is version 1...


            return data;

        }

        public void BuildStructs()
        {

            List<NodeLink> newlinks = new();
            List<NodeJunction> newjuncs = new();
            List<NodeJunctionRef> newjuncrefs = new();
            List<byte> newjuncheightmaps = new();

            if (Nodes != null)
            {
                int count = Nodes.Length;
                var nodes = new Node[count];
                for (int i = 0; i < count; i++)
                {
                    var node = Nodes[i];
                    if (node.Links != null)
                    {
                        node.LinkCount = node.Links.Length;
                        node.LinkID = (ushort)newlinks.Count;
                        for (int l = 0; l < node.Links.Length; l++)
                        {
                            var nlink = node.Links[l];
                            var rlink = nlink.RawData;
                            if (nlink.Node2 != null)
                            {
                                rlink.AreaID = nlink.Node2.AreaID;
                                rlink.NodeID = nlink.Node2.NodeID;
                            }
                            newlinks.Add(rlink);
                        }
                    }
                    else
                    {
                        node.LinkCount = 0;
                    }

                    byte lcflags = (byte)((node.LinkCount << 3) | (node.LinkCountUnk & 6) | (node.QualifiesAsJunction ? 1 : 0));
                    node._RawData.Flags1 = (node._RawData.Flags1 & 0xFFFF00FF) | ((uint)lcflags << 8);

                    nodes[i] = node.RawData;

                    if ((node.HasJunction) && (node.Junction != null))
                    {
                        var nj = node.Junction;
                        var heightmapoff = newjuncheightmaps.Count;
                        var heightmap = nj.Heightmap?.GetBytes() ?? [];
                        nj._RawData.HeightmapPtr = (ushort)heightmapoff;
                        var jref = nj.RefData;
                        jref.AreaID = node.AreaID;
                        jref.NodeID = node.NodeID;
                        jref.JunctionID = (ushort)newjuncs.Count;
                        //jref.Padding0 = 0; // upper 16 bits of junction map value, always 0
                        nj.RefData = jref;//move this somewhere else..??
                        newjuncs.Add(nj.RawData);
                        newjuncrefs.Add(jref);
                        newjuncheightmaps.AddRange(heightmap);
                    }
                }
                NodeDictionary.Nodes = nodes;
                NodeDictionary.NodesCount = (uint)count;
                NodeDictionary.Links = newlinks.ToArray();
                NodeDictionary.LinksCount = (uint)newlinks.Count;
                NodeDictionary.Junctions = newjuncs.ToArray();
                NodeDictionary.JunctionsCount = (uint)newjuncs.Count;
                NodeDictionary.JunctionRefs = newjuncrefs.ToArray();
                NodeDictionary.JunctionRefsCount0 = (ushort)newjuncrefs.Count;
                NodeDictionary.JunctionRefsCount1 = (ushort)newjuncrefs.Count;
                NodeDictionary.JunctionHeightmapBytes = newjuncheightmaps.ToArray();
                NodeDictionary.JunctionHeightmapBytesCount = (uint)newjuncheightmaps.Count;
            }
            else
            {
                NodeDictionary.Nodes = [];
                NodeDictionary.NodesCount = 0;
                NodeDictionary.Links = [];
                NodeDictionary.LinksCount = 0;
                NodeDictionary.Junctions = [];
                NodeDictionary.JunctionsCount = 0;
                NodeDictionary.JunctionRefs = [];
                NodeDictionary.JunctionRefsCount0 = 0;
                NodeDictionary.JunctionRefsCount1 = 0;
                NodeDictionary.JunctionHeightmapBytes = [];
                NodeDictionary.JunctionHeightmapBytesCount = 0;
            }
        }




        public void InitNodesFromDictionary()
        {
            if (NodeDictionary != null)
            {
                if (NodeDictionary.Nodes != null)
                {
                    var nodes = NodeDictionary.Nodes;
                    Nodes = new YndNode[nodes.Length];
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var n = new YndNode();
                        n.Init(this, nodes[i]);
                        Nodes[i] = n;
                        if (n.NodeID != i)
                        { } //never hit here - nodeid's have to match the index!
                    }
                }
                if ((NodeDictionary.JunctionRefs != null) && (NodeDictionary.Junctions != null))
                {
                    var juncrefs = NodeDictionary.JunctionRefs;
                    var juncs = NodeDictionary.Junctions;
                    Junctions = new YndJunction[juncrefs.Length];
                    for (int i = 0; i < juncrefs.Length; i++)
                    {
                        var juncref = juncrefs[i];
                        if (juncref.JunctionID >= juncs.Length)
                        { continue; }

                        var j = new YndJunction();
                        j.Init(this, juncs[juncref.JunctionID], juncref);
                        j.Heightmap = new YndJunctionHeightmap(NodeDictionary.JunctionHeightmapBytes, j);
                        Junctions[i] = j;
                    }
                }
            }
        }

        public YndNode AddNode()
        {
            int cnt = Nodes.Length;
            YndNode yn = new();
            Node n = new();
            n.AreaID = (ushort)AreaID;
            n.NodeID = (ushort)(Nodes.Length);
            yn.Init(this, n);

            int ncnt = cnt + 1;
            YndNode[] nnodes = new YndNode[ncnt];
            for (int i = 0; i < cnt; i++)
            {
                nnodes[i] = Nodes[i];
            }
            nnodes[cnt] = yn;
            Nodes = nnodes;
            NodeDictionary.NodesCount = (uint)ncnt;

            ShouldRecalculateIndices = true;

            return yn;
        }

        public void MigrateNode(YndNode node)
        {
            int cnt = Nodes.Length;
            node.Ynd = this;
            node.AreaID = (ushort)AreaID;
            node.NodeID = (ushort)(Nodes.Length);

            int ncnt = cnt + 1;
            YndNode[] nnodes = new YndNode[ncnt];
            for (int i = 0; i < cnt; i++)
            {
                nnodes[i] = Nodes[i];
            }
            nnodes[cnt] = node;
            Nodes = nnodes;
            NodeDictionary.NodesCount = (uint)ncnt;

            var links = new List<YndLink>();
            if (Links != null)
            {
                links.AddRange(Links);
            }

            if (node.Links != null)
            {
                foreach (var nodeLink in node.Links)
                {
                    links.Add(nodeLink);
                }
            }

            Links = links.ToArray();

            ShouldRecalculateIndices = true;
        }

        public bool RemoveNode(YndNode node, bool removeLinks)
        {

            var nodes = Nodes.Where(n => n.AreaID != node.AreaID || n.NodeID != node.NodeID).ToArray();
            Nodes = nodes;
            NodeDictionary.NodesCount = (uint)nodes.Count();

            if (removeLinks)
            {
                RemoveLinksForNode(node);
            }

            ShouldRecalculateIndices = true;

            return true;
        }

        public void RecalculateNodeIndices()
        {
            if (!ShouldRecalculateIndices)
            {
                return;
            }

            if (Nodes == null)
            {
                return;
            }
            // Sort nodes so ped nodes are at the end - single pass partitioning
            var vehicleNodesList = new List<YndNode>();
            var pedNodesList = new List<YndNode>();
            
            foreach (var n in Nodes)
            {
                if (n.IsPedNode)
                    pedNodesList.Add(n);
                else
                    vehicleNodesList.Add(n);
            }
            
            vehicleNodesList.Sort((a, b) => a.NodeID.CompareTo(b.NodeID));
            pedNodesList.Sort((a, b) => a.NodeID.CompareTo(b.NodeID));
            
            var nodes = new List<YndNode>(Nodes.Length);
            var affectedNodesList = new List<YndNode>();
            nodes.AddRange(vehicleNodesList);
            nodes.AddRange(pedNodesList);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                if (node.NodeID != i)
                {
                    node.NodeID = (ushort)i;
                    affectedNodesList.Add(node);
                }
            }

            NodeDictionary.NodesCountVehicle = (uint)vehicleNodesList.Count;
            NodeDictionary.NodesCountPed = (uint)pedNodesList.Count;
            NodeDictionary.NodesCount = NodeDictionary.NodesCountVehicle + NodeDictionary.NodesCountPed;
            Nodes = nodes.ToArray();

            UpdateAllNodePositions();
            ShouldRecalculateIndices = false;
        }

        /// <summary>
        /// Removes links for node.
        /// </summary>
        /// <param name="node">node to check against.</param>
        /// <returns><see cref="bool"/> indicating whether this file has been affected</returns>
        public bool RemoveLinksForNode(YndNode node)
        {
            // Delete links that target this node
            var rmLinks = new List<YndLink>();

            foreach (var n in Nodes)
            {
                // Consolidated: materialize once and reuse
                var nodeRmLinks = new List<YndLink>();
                foreach (var l in n.Links)
                {
                    if (l.Node1 == node || l.Node2 == node)
                        nodeRmLinks.Add(l);
                }

                foreach (var rl in nodeRmLinks)
                {
                    n.RemoveLink(rl);
                }

                rmLinks.AddRange(nodeRmLinks);
            }

            if (rmLinks.Count > 0)
            {
                // Consolidated: use loop instead of LINQ for filtering
                var remainingLinks = new List<YndLink>(Links.Length);
                foreach (var l in Links)
                {
                    if (!rmLinks.Contains(l))
                        remainingLinks.Add(l);
                }
                Links = remainingLinks.ToArray();
                return true;
            }

            return false;
        }

        public bool HasAnyLinksForNode(YndNode node)
        {
            return Links.Any(l => l.Node1 == node || l.Node2 == node);
        }

        public void UpdateBoundingBox()
        {
            Vector3 corner = new(-8192, -8192, -2048);
            Vector3 cellsize = new(512, 512, 4096);

            BBMin = corner + (cellsize * new Vector3(CellX, CellY, 0));
            BBMax = BBMin + cellsize;
        }

        public void UpdateAllNodePositions()
        {
            int cnt = Nodes.Length;
            if (cnt <= 0)
            {
                NodePositions = [];
                return;
            }
            var np = new Vector4[cnt];
            for (int i = 0; i < cnt; i++)
            {
                np[i] = new Vector4(Nodes[i].Position, 1.0f);
            }
            NodePositions = np;
        }

        public void UpdateTriangleVertices(YndNode[]? selectedNodes)
        {
            //note: called from space.BuildYndVerts()

            UpdateLinkTriangleVertices();
            UpdateJunctionTriangleVertices(selectedNodes);
        }

        private void UpdateLinkTriangleVertices()
        {
            //build triangles for the path links display
            int vc = 0;
            if (Links != null)
            {
                vc = Links.Length * 6;
            }

            List<EditorVertex> verts = new(vc);
            EditorVertex v0 = new();
            EditorVertex v1 = new();
            EditorVertex v2 = new();
            EditorVertex v3 = new();
            if ((Links != null) && (Nodes != null))
            {
                foreach (var node in Nodes)
                {
                    if (node.Links is null)
                    {
                        continue;
                    }

                    foreach (var link in node.Links)
                    {
                        var p0 = link.Node1?.Position ?? Vector3.Zero;
                        var p1 = link.Node2?.Position ?? Vector3.Zero;
                        var dir = link.GetDirection();
                        var ax = Vector3.Cross(dir, Vector3.UnitZ);

                        float lanestot = link.LaneCountForward + link.LaneCountBackward;
                        //float backfrac = Math.Min(Math.Max(link.LaneCountBackward / lanestot, 0.1f), 0.9f);
                        //float lanewidth = 7.0f;
                        //float inner = totwidth*(backfrac-0.5f);
                        //float outer = totwidth*0.5f;

                        float lanewidth = link.GetLaneWidth();

                        float inner = link.LaneOffset * lanewidth;// 0.0f;
                        float outer = inner + lanewidth * link.LaneCountForward;

                        float totwidth = lanestot * lanewidth;
                        float halfwidth = totwidth * 0.5f;
                        if (link.LaneCountBackward == 0)
                        {
                            inner -= halfwidth;
                            outer -= halfwidth;
                        }
                        if (link.LaneCountForward == 0)
                        {
                            inner += halfwidth;
                            outer += halfwidth;
                        }


                        v0.Position = p1 + ax * inner;
                        v1.Position = p0 + ax * inner;
                        v2.Position = p1 + ax * outer;
                        v3.Position = p0 + ax * outer;
                        var c = (uint)link.GetColour().ToRgba();
                        v0.Colour = c;
                        v1.Colour = c;
                        v2.Colour = c;
                        v3.Colour = c;
                        verts.Add(v0);
                        verts.Add(v1);
                        verts.Add(v2);
                        verts.Add(v2);
                        verts.Add(v1);
                        verts.Add(v3);
                    }
                }
            }


            if (verts.Count > 0)
            {
                TriangleVerts = verts.ToArray();
            }
            else
            {
                TriangleVerts = [];
            }
        }

        private void UpdateJunctionTriangleVertices(YndNode[]? selectedNodes)
        {
            if (selectedNodes == null)
            {
                return;
            }

            //build triangles for the junctions bytes display....
            List<EditorVertex> verts = new();
            EditorVertex v0 = new();
            EditorVertex v1 = new();
            EditorVertex v2 = new();
            EditorVertex v3 = new();

            foreach (var node in selectedNodes)
            {
                if (node.Ynd != this) continue;
                if (node.Junction == null) continue;
                var j = node.Junction;
                var d = j.Heightmap;
                if (d == null) continue;

                float maxz = j.MaxZ / 32.0f;
                float minz = j.MinZ / 32.0f;
                float rngz = maxz - minz;
                float posx = j.PositionX / 4.0f;
                float posy = j.PositionY / 4.0f;

                Vector3 pos = new(posx, posy, 0.0f);
                Vector3 siz = new Vector3(d.CountX, d.CountY, 0.0f) * 2.0f;
                //Vector3 siz = new(jx, jy, 0.0f);
                Vector3 cnr = pos;// - siz * 0.5f;
                                  //Vector3 inc = new(1.0f/jx)

                cnr.Z = minz;// + 2.0f;

                for (int y = 1; y < d.CountY; y++) //rows progress up the Y axis.
                {
                    var row0 = d.Rows[y - 1];
                    var row1 = d.Rows[y];
                    float offy = y * 2.0f;

                    for (int x = 1; x < d.CountX; x++) //values progress along the X axis.
                    {
                        var val0 = row0.Values[x - 1] / 255.0f;
                        var val1 = row0.Values[x] / 255.0f;
                        var val2 = row1.Values[x - 1] / 255.0f;
                        var val3 = row1.Values[x] / 255.0f;
                        float offx = x * 2.0f;
                        v0.Position = cnr + new Vector3(offx - 2.0f, offy - 2.0f, val0 * rngz);
                        v1.Position = cnr + new Vector3(offx + 0.0f, offy - 2.0f, val1 * rngz);
                        v2.Position = cnr + new Vector3(offx - 2.0f, offy + 0.0f, val2 * rngz);
                        v3.Position = cnr + new Vector3(offx + 0.0f, offy + 0.0f, val3 * rngz);
                        v0.Colour = (uint)new Color4(val0, 1.0f - val0, 0.0f, 0.3f).ToRgba();
                        v1.Colour = (uint)new Color4(val1, 1.0f - val1, 0.0f, 0.3f).ToRgba();
                        v2.Colour = (uint)new Color4(val2, 1.0f - val2, 0.0f, 0.3f).ToRgba();
                        v3.Colour = (uint)new Color4(val3, 1.0f - val3, 0.0f, 0.3f).ToRgba();
                        verts.Add(v0);
                        verts.Add(v1);
                        verts.Add(v2);
                        verts.Add(v2);
                        verts.Add(v1);
                        verts.Add(v3);
                    }
                }
            }
            
            if (verts.Count > 0)
            {
                var vertsarr = verts.ToArray();
                if (TriangleVerts != null)
                {
                    var nvc = vertsarr.Length;
                    var tvc = TriangleVerts.Length;
                    var newTriangles = new EditorVertex[tvc + nvc];
                    Array.Copy(TriangleVerts, newTriangles, tvc);
                    Array.Copy(vertsarr, 0, newTriangles, tvc, nvc);
                    TriangleVerts = newTriangles;
                }
                else
                {
                    TriangleVerts = vertsarr;
                }
            }
        }


        public void UpdateBvhForNode(YndNode node)
        {
            //this needs to be called when a node's position changes...
            //if it changes a lot, need to recalc the BVH for mouse intersection optimisation purposes.

            //if (BVH == null) return;
            //BVH.UpdateForNode(node);

            BuildBVH();

            //also updates the NodePositions for the visible vertex
            if (Nodes != null)
            {
                for (int i = 0; i < Nodes.Length; i++)
                {
                    if (Nodes[i] == node)
                    {
                        NodePositions[i] = new Vector4(node.Position, 1.0f);
                        break;
                    }
                }
            }

        }

        public void BuildBVH()
        {
            BVH = new PathBVH(Nodes, 10, 10);
        }




        public EditorVertex[] GetPathVertices()
        {
            return LinkedVerts;
        }
        public EditorVertex[] GetTriangleVertices()
        {
            return TriangleVerts;
        }
        public Vector4[] GetNodePositions()
        {
            return NodePositions;
        }




        public YndNode AddYndNode(Space space, out HashSet<YndFile> affectedFiles)
        {
            var n = AddNode();
            affectedFiles = space.GetYndFilesThatDependOnYndFile(this);
            return n;
        }


        public bool RemoveYndNode(Space space, YndNode node, bool removeLinks, out YndFile[] affectedFiles)
        {
            var totalAffectedFiles = new List<YndFile>();
            if (RemoveNode(node, removeLinks))
            {
                if (removeLinks)
                {
                    node.RemoveYndLinksForNode(space, out var affectedFilesFromLinkChanges);
                    totalAffectedFiles.AddRange(affectedFilesFromLinkChanges);

                }

                totalAffectedFiles.AddRange(space.GetYndFilesThatDependOnYndFile(this));
                affectedFiles = totalAffectedFiles.Distinct().ToArray();
                return true;
            }

            affectedFiles = Array.Empty<YndFile>();
            return false;
        }





        public override string ToString()
        {
            return RpfFileEntry?.ToString() ?? string.Empty;
        }
    }

    public enum YndNodeSpeed
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
        Faster = 3
    }

    public enum YndNodeSpecialType
    {
        None = 0,
        ParkingParallel = 1,
        ParkingPerpendicular = 2,
        DropoffGoods = 3,
        DriveThrough = 4,
        DriveThroughWindow = 5,
        DropoffGoodsUnload = 6,
        HidingNode = 7,
        SmallWorkVehicles = 8,
        PetrolStation = 9,
        PedCrossing = 10,
        DropoffPassengers = 11,
        DropoffPassengersUnload = 12,
        OpenSpace = 13,
        PedAssistedMovement = 14,
        TrafficLight = 15,
        GiveWay = 16,
        ForceJunction = 17,
        PedDrivewayCrossing = 18,
        RestrictedArea = 19,
        FalseJunction = 20,
        DisableVehicleCreation = 21
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class YndNode : BasePathNode
    {
        public Node _RawData;

        public YndFile? Ynd { get; set; }
        public Node RawData { get { return _RawData; } set { _RawData = value; } }
        public Vector3 Position { get; set; }
        public int LinkCount { get; set; }
        public int LinkCountUnk { get; set; }
        public YndLink[] Links { get; set; } = [];

        public ushort AreaID { get { return _RawData.AreaID; } set { _RawData.AreaID = value; } }
        public ushort NodeID { get { return _RawData.NodeID; } set { _RawData.NodeID = value; } }
        public ushort LinkID { get { return _RawData.LinkID; } set { _RawData.LinkID = value; } }
        // m_iAsInteger1 and m_iAsInteger2 - direct u32 bitfield access matching C++ CPathNode
        public uint Flags0 { get { return _RawData.Flags0; } set { _RawData.Flags0 = value; } }       // m_iAsInteger1
        public uint Flags1 { get { return _RawData.Flags1; } set { _RawData.Flags1 = value; } }       // m_iAsInteger2
        public TextHash StreetName { get { return _RawData.StreetName; } set { _RawData.StreetName = value; } }

        public Color4 Colour { get; set; }

        public YndJunction? Junction { get; set; }
        public bool HasJunction;


        public YndNodeSpeed Speed
        {
            get
            {
                return (YndNodeSpeed)((LinkCountUnk >> 1) & 3);
            }
            set
            {
                LinkCountUnk = (LinkCountUnk &~ 6) | (((int)value & 3) << 1);
            }
        }

        // m_iAsInteger1 (Flags0) bit properties
        public int FloodGroup                                        // bits 0-2: m_group (3 bits, 0-7)
        {
            get => (int)(Flags0 & 0x7u);
            set => Flags0 = (Flags0 & ~0x7u) | ((uint)(value & 0x7));
        }
        public bool OffRoad                                          // bit 3: m_Offroad
        {
            get => (Flags0 & 0x8u) != 0;
            set => Flags0 = value ? (Flags0 | 0x8u) : (Flags0 & ~0x8u);
        }
        public bool NoBigVehicles                                    // bit 5: m_noBigVehicles
        {
            get => (Flags0 & 0x20u) != 0;
            set => Flags0 = value ? (Flags0 | 0x20u) : (Flags0 & ~0x20u);
        }
        public bool CannotGoRight                                    // bit 6: m_cannotGoRight
        {
            get => (Flags0 & 0x40u) != 0;
            set => Flags0 = value ? (Flags0 | 0x40u) : (Flags0 & ~0x40u);
        }
        public bool CannotGoLeft                                     // bit 7: m_cannotGoLeft
        {
            get => (Flags0 & 0x80u) != 0;
            set => Flags0 = value ? (Flags0 | 0x80u) : (Flags0 & ~0x80u);
        }
        public bool SlipRoad                                         // bit 8: m_slipLane
        {
            get => (Flags0 & 0x100u) != 0;
            set => Flags0 = value ? (Flags0 | 0x100u) : (Flags0 & ~0x100u);
        }
        public bool IndicateKeepLeft                                 // bit 9: m_indicateKeepLeft
        {
            get => (Flags0 & 0x200u) != 0;
            set => Flags0 = value ? (Flags0 | 0x200u) : (Flags0 & ~0x200u);
        }
        public bool IndicateKeepRight                                // bit 10: m_indicateKeepRight
        {
            get => (Flags0 & 0x400u) != 0;
            set => Flags0 = value ? (Flags0 | 0x400u) : (Flags0 & ~0x400u);
        }
        public YndNodeSpecialType Special                            // bits 11-15: m_specialFunction (5 bits)
        {
            get => (YndNodeSpecialType)((Flags0 >> 11) & 0x1F);
            set => Flags0 = (Flags0 & ~(0x1Fu << 11)) | ((uint)(byte)value << 11);
        }
        public bool LeftTurnsOnly                                    // bit 15: m_specialFunction MSB
        {
            get => (Flags0 & 0x8000u) != 0;
            set => Flags0 = value ? (Flags0 | 0x8000u) : (Flags0 & ~0x8000u);
        }

        // m_iAsInteger2 (Flags1) bit properties
        public bool NoGps                                            // bit 0: m_noGps
        {
            get => (Flags1 & 0x1u) != 0;
            set => Flags1 = value ? (Flags1 | 0x1u) : (Flags1 & ~0x1u);
        }
        public bool IsJunction                                       // bit 2: m_slipJunction
        {
            get => (Flags1 & 0x4u) != 0;
            set => Flags1 = value ? (Flags1 | 0x4u) : (Flags1 & ~0x4u);
        }
        public bool IsDisabledUnk1                                   // bit 4: m_switchedOffOriginal
        {
            get => (Flags1 & 0x10u) != 0;
            set => Flags1 = value ? (Flags1 | 0x10u) : (Flags1 & ~0x10u);
        }
        public bool WaterNode                                        // bit 5: m_waterNode
        {
            get => (Flags1 & 0x20u) != 0;
            set => Flags1 = value ? (Flags1 | 0x20u) : (Flags1 & ~0x20u);
        }
        public bool Highway                                          // bit 6: m_highwayOrLowBridge
        {
            get => (Flags1 & 0x40u) != 0;
            set => Flags1 = value ? (Flags1 | 0x40u) : (Flags1 & ~0x40u);
        }
        public bool IsDisabledUnk0                                   // bit 7: m_switchedOff
        {
            get => (Flags1 & 0x80u) != 0;
            set => Flags1 = value ? (Flags1 | 0x80u) : (Flags1 & ~0x80u);
        }
        public bool QualifiesAsJunction                              // bit 8: m_qualifiesAsJunction
        {
            get => (Flags1 & 0x100u) != 0;
            set => Flags1 = value ? (Flags1 | 0x100u) : (Flags1 & ~0x100u);
        }
        public bool Tunnel                                           // bit 16: m_inTunnel
        {
            get => (Flags1 & 0x10000u) != 0;
            set => Flags1 = value ? (Flags1 | 0x10000u) : (Flags1 & ~0x10000u);
        }
        public int HeuristicValue                                    // bits 17-23: m_distanceHash (7 bits, 0-127)
        {
            get => (int)((Flags1 >> 17) & 0x7F);
            set => Flags1 = (Flags1 & ~(0x7Fu << 17)) | ((uint)(value & 0x7F) << 17);
        }
        public int Density                                           // bits 24-27: m_density (4 bits, 0-15)
        {
            get => (int)((Flags1 >> 24) & 0xF);
            set => Flags1 = (Flags1 & ~(0xFu << 24)) | ((uint)(value & 0xF) << 24);
        }
        public int DeadEndness                                       // bits 28-30: m_deadEndness (3 bits)
        {
            get => (int)((Flags1 >> 28) & 0x7);
            set => Flags1 = (Flags1 & ~(0x7u << 28)) | ((uint)(value & 0x7) << 28);
        }
        public bool LeftOnly                                         // bit 31: m_leftOnly
        {
            get => (Flags1 & 0x80000000u) != 0;
            set => Flags1 = value ? (Flags1 | 0x80000000u) : (Flags1 & ~0x80000000u);
        }

        public static bool IsSpecialTypeAPedNode(YndNodeSpecialType specialType)
            => specialType == YndNodeSpecialType.PedCrossing
               || specialType == YndNodeSpecialType.PedAssistedMovement
               || specialType == YndNodeSpecialType.PedDrivewayCrossing;
        public bool IsPedNode => IsSpecialTypeAPedNode(Special);// If Special is 10, 14 or 18 this is a ped node.


        public void Init(YndFile ynd, Node node)
        {
            Ynd = ynd;
            RawData = node;
            Vector3 p = new();
            p.X = node.PositionX / 4.0f;
            p.Y = node.PositionY / 4.0f;
            p.Z = ((short)(node.Flags0 >> 16)) / 32.0f;
            Position = p;

            byte lcflags = (byte)((node.Flags1 >> 8) & 0xFF);
            LinkCount = lcflags >> 3;
            LinkCountUnk = lcflags & 6; // bits 1-2 only; bit 0 is QualifiesAsJunction in Flags1

            Colour = GetColour();

        }

        public Color4 GetColour()
        {
            if (IsDisabledUnk0 || IsDisabledUnk1)
            {
                return new Color4(1.0f, 0.0f, 0.0f, 0.5f);
            }

            if (IsPedNode)
            {
                return new Color4(1.0f, 0.0f, 1.0f, 0.5f);
            }

            if (Tunnel)
            {
                return new Color4(0.3f, 0.3f, 0.3f, 0.5f);
            }

            return new Color4(LinkCountUnk / 7.0f, (Flags0 & 0xFF) / 255.0f, ((Flags0 >> 8) & 0xFF) / 255.0f, 0.5f);
        }


        public void SetPosition(Vector3 pos)
        {
            _RawData.PositionX = (short)(pos.X * 4.0f);
            _RawData.PositionY = (short)(pos.Y * 4.0f);
            ushort posZ = (ushort)(short)(pos.Z * 32.0f);
            _RawData.Flags0 = (_RawData.Flags0 & 0xFFFF) | ((uint)posZ << 16);

            Vector3 newpos = pos;
            Position = newpos;

            UpdateLinkLengths();
            RecalculateHeuristic();
        }


        public void UpdateLinkLengths()
        {
            if (Links == null) return;
            for (int i = 0; i < Links.Length; i++)
            {
                var link = Links[i];
                link.UpdateLength(); //update this node's links

                var n2 = link.Node2;
                if ((n2 == null) || (n2.Links == null)) continue;

                for (int j = 0; j < n2.Links.Length; j++)
                {
                    var n2l = n2.Links[j];
                    if (n2l.Node2 == this)
                    {
                        n2l.UpdateLength(); //update back links
                    }
                }
            }
        }

        public void RecalculateHeuristic()
        {
            var link = Links?.FirstOrDefault(l => l.LaneCountBackward > 0);
            if (link is null)
            {
                HeuristicValue = 0;
                return;
            }

            var partner = link.Node1 == this
                ? link.Node2
                : link.Node1;

            var length = link.Distance;

            HeuristicValue = (partner?.HeuristicValue ?? 0) + length;
        }

        public void CheckIfJunction()
        {
            if (Links == null)
            {
                IsJunction = false;
                return;
            }

            // If this is a 3 node junction (4 including itself)
            IsJunction = Links
                .Where(l => !l.Shortcut)
                .SelectMany(l => new[] { l.Node1, l.Node2 }).Distinct().Count() > 3;

            if (!IsJunction && Special == YndNodeSpecialType.FalseJunction)
            {
                Special = YndNodeSpecialType.None;
            }

            if (IsJunction && Special == YndNodeSpecialType.None || Special == YndNodeSpecialType.FalseJunction)
            {
                var hasOffroadLink = Links.Any(l => l.Node2?.OffRoad == true);
                Special = hasOffroadLink ? YndNodeSpecialType.FalseJunction : YndNodeSpecialType.None;
            }
        }


        public YndLink AddLink(YndNode? tonode = null, bool bidirectional = true)
        {
            if (Links == null)
            {
                Links = Array.Empty<YndLink>();
            }

            var existing = Links.FirstOrDefault(el => el.Node2 == tonode);
            if (existing != null)
            {
                return existing;
            }

            YndLink l = new();
            l._RawData.AreaID = AreaID;
            l.Node1 = this;
            if (tonode != null)
            {
                l.Node2 = tonode;
                l._RawData.AreaID = tonode.AreaID;
                l._RawData.NodeID = tonode.NodeID;
            }
            else if ((Ynd?.Nodes != null) && (Ynd.Nodes.Length > 0))
            {
                l.Node2 = Ynd.Nodes[0];
            }
            else
            {
                l.Node2 = this;
                l._RawData.NodeID = NodeID;
            }
            l.UpdateLength();

            int cnt = Links.Length;
            int ncnt = cnt + 1;
            YndLink[] nlinks = new YndLink[ncnt];
            for (int i = 0; i < cnt; i++)
            {
                nlinks[i] = Links[i];
            }
            nlinks[cnt] = l;
            Links = nlinks;
            LinkCount = ncnt;

            if (bidirectional)
            {
                tonode?.AddLink(this, false);
            }

            RecalculateHeuristic();
            CheckIfJunction();

            return l;
        }

        public bool TryGetLinkForNode(YndNode node, [NotNullWhen(true)] out YndLink? link)
        {
            for (int i = 0; i < Links.Length; i++)
            {
                if (Links[i].Node2 == node)
                {
                    link = Links[i];
                    return true;
                }
            }

            link = null;
            return false;
        }

        public bool RemoveLink(YndLink l)
        {
            List<YndLink> newlinks = new();
            int cnt = Links.Length;
            bool r = false;
            for (int i = 0; i < cnt; i++)
            {
                var tl = Links[i];
                if (tl != l)
                {
                    newlinks.Add(tl);
                }
                else
                {
                    r = true;
                }
            }
            Links = newlinks.ToArray();
            LinkCount = newlinks.Count;

            RecalculateHeuristic();
            CheckIfJunction();
            return r;
        }

        public void FloodCopyFlags(out YndFile[] affectedFiles)
        {
            FloodCopyFlags(this, new List<YndNode>(), out affectedFiles);
        }

        private void FloodCopyFlags(YndNode basis, List<YndNode> seenNodes, out YndFile[] affectedFiles)
        {
            var affectedFilesList = new List<YndFile>();
            if (Links == null || !Links.Any())
            {
                affectedFiles = Array.Empty<YndFile>();
                return;
            }

            if (seenNodes.Contains(this))
            {
                affectedFiles = Array.Empty<YndFile>();
                return;
            }

            seenNodes.Add(this);
            if (basis != this && !IsJunction)
            {
                // Copy flag bits from m_iAsInteger1 (bits 0-15 only, preserve coorsZ in bits 16-31)
                Flags0 = (Flags0 & 0xFFFF0000u) | (basis.Flags0 & 0x0000FFFFu);
                Flags1 = basis.Flags1;
                LinkCountUnk = (LinkCountUnk &~ 7) | (basis.LinkCountUnk & 7);

                if (Ynd != null) affectedFilesList.Add(Ynd);
                RecalculateHeuristic();
            }

            CheckIfJunction();

            if (!IsJunction)
            {
                foreach (var yndLink in Links)
                {
                    if (yndLink.Shortcut)
                    {
                        continue;
                    }

                    if (yndLink.Node1 != null)
                    {
                        yndLink.Node1.FloodCopyFlags(basis, seenNodes, out var node1Files);
                        affectedFilesList.AddRange(node1Files);
                    }
                    if (yndLink.Node2 != null)
                    {
                        yndLink.Node2.FloodCopyFlags(basis, seenNodes, out var node2Files);
                        affectedFilesList.AddRange(node2Files);
                    }


                }
            }

            affectedFiles = affectedFilesList.Distinct().ToArray();
        }





        public void SetYndNodePosition(Space space, Vector3 newPosition, out YndFile[] affectedFiles)
        {
            var totalAffectedFiles = new List<YndFile>();

            if (Links != null)
            {
                totalAffectedFiles.AddRange(Links.SelectMany(l => new[] { l.Node1?.Ynd, l.Node2?.Ynd }).OfType<YndFile>().Distinct());
            }

            var oldPosition = Position;
            var expectedArea = space.NodeGrid?.GetCellForPosition(newPosition);
            if (expectedArea == null)
            {
                affectedFiles = [];
                return;
            }
            SetPosition(newPosition);

            if (AreaID != expectedArea.ID)
            {
                var nodeYnd = space.NodeGrid?.GetCell(AreaID)?.Ynd;
                var newYnd = expectedArea.Ynd;
                if (newYnd == null)
                {
                    SetPosition(oldPosition);
                    affectedFiles = Array.Empty<YndFile>();
                    return;
                }

                if ((nodeYnd == null) ||
                    nodeYnd.RemoveYndNode(space, this, false, out var affectedFilesFromDelete))
                {
                    if (nodeYnd != null) totalAffectedFiles.Add(nodeYnd);
                    newYnd.MigrateNode(this);
                    totalAffectedFiles.AddRange(space.GetYndFilesThatDependOnYndFile(nodeYnd));
                    totalAffectedFiles.AddRange(space.GetYndFilesThatDependOnYndFile(Ynd));
                }
            }

            affectedFiles = totalAffectedFiles.Distinct().ToArray();
        }

        public void RemoveYndLinksForNode(Space space, out YndFile[] affectedFiles)
        {
            List<YndFile> files = new();

            foreach (var yndFile in space.GetYndFilesThatDependOnYndFile(Ynd))
            {
                if (yndFile.RemoveLinksForNode(this))
                {
                    files.Add(yndFile);
                }
            }

            affectedFiles = files.ToArray();
        }

        public void GenerateYndNodeJunctionHeightMap(Space space)
        {
            if (Junction == null)
            {
                Junction = new YndJunction();
            }

            var junc = Junction;
            var sizeX = junc._RawData.HeightmapDimX;
            var sizeY = junc._RawData.HeightmapDimY;

            if (sizeX <= 0 || sizeY <= 0) return;

            var maxZ = junc.MaxZ / 32f;
            var minZ = junc.MinZ / 32f;
            var maxDist = maxZ - minZ;

            if (maxDist <= 0) return; // avoid division by zero

            var xStart = junc.PositionX / 4f;
            var yStart = junc.PositionY / 4f;

            var start = new Vector3(xStart, yStart, maxZ);
            var layers = new[] { true, false, false };

            // Build heightmap byte array directly
            var heightmapBytes = new byte[sizeX * sizeY];

            for (int y = 0; y < sizeY; y++)
            {
                var offy = y * 2.0f;

                for (int x = 0; x < sizeX; x++)
                {
                    var offx = x * 2.0f;
                    var result = space.RayIntersect(new Ray(start + new Vector3(offx, offy, 0f), new Vector3(0f, 0f, -1f)), maxDist, layers);

                    if (!result.Hit)
                    {
                        heightmapBytes[y * sizeX + x] = 0;
                        continue;
                    }

                    var height = Math.Min(Math.Max(result.Position.Z, minZ), maxZ);
                    heightmapBytes[y * sizeX + x] = (byte)((height - minZ) / maxDist * 255);
                }
            }

            // Ensure Heightmap object exists with correct dimensions
            if (junc.Heightmap == null || junc.Heightmap.CountX != sizeX || junc.Heightmap.CountY != sizeY)
            {
                junc.Heightmap = new YndJunctionHeightmap(heightmapBytes, junc);
            }
            else
            {
                // Update existing heightmap rows directly
                for (int y = 0; y < sizeY; y++)
                {
                    var rowBytes = new byte[sizeX];
                    Buffer.BlockCopy(heightmapBytes, y * sizeX, rowBytes, 0, sizeX);
                    junc.Heightmap.Rows[y].Values = rowBytes;
                }
            }

            HasJunction = true;
        }




        public override string ToString()
        {
            //return AreaID.ToString() + "." + NodeID.ToString();
            return StreetName.ToString() + ", " + Position.X.ToString() + ", " + Position.Y.ToString() + ", " + Position.Z.ToString() + ", " + NodeID.ToString();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class YndLink
    {
        public YndFile? Ynd { get; set; }
        public YndNode? Node1 { get; set; }

        private YndNode? _node2;
        public YndNode? Node2
        {
            get => _node2;
            set
            {
                _node2 = value;
                UpdateTargetIndex();
            }
        }

        public NodeLink _RawData;
        public NodeLink RawData { get { return _RawData; } set { _RawData = value; } }
        // m_iAsInteger1 - direct u32 bitfield access matching C++ CPathNodeLink
        public uint Flags0 { get { return _RawData.Flags0; } set { _RawData.Flags0 = value; } }       // m_iAsInteger1
        public byte Distance                                         // bits 24-31: m_Distance
        {
            get => (byte)((Flags0 >> 24) & 0xFF);
            set => Flags0 = (Flags0 & 0x00FFFFFFu) | ((uint)value << 24);
        }

        public int LaneCountForward                                  // bits 21-23: m_LanesToOtherNode
        {
            get => (int)((Flags0 >> 21) & 7);
            set => Flags0 = (Flags0 & ~(7u << 21)) | ((uint)(value & 7) << 21);
        }

        public int LaneCountBackward                                 // bits 18-20: m_LanesFromOtherNode
        {
            get => (int)((Flags0 >> 18) & 7);
            set => Flags0 = (Flags0 & ~(7u << 18)) | ((uint)(value & 7) << 18);
        }

        public int OffsetValue                                       // bits 12-14: m_Width (lower 3 bits)
        {
            get => (int)((Flags0 >> 12) & 7);
            set => Flags0 = (Flags0 & ~(7u << 12)) | ((uint)(value & 7) << 12);
        }

        public bool NegativeOffset { get { return (Flags0 & 0x8000u) != 0; } }  // bit 15: m_Width MSB
        public float LaneOffset { get { return (OffsetValue / 7.0f) * (NegativeOffset ? -0.5f : 0.5f); } }

        public bool GpsBothWays { get { return (Flags0 & 1u) != 0; } }            // bit 0: m_bGpsCanGoBothWays
        public bool NarrowRoad { get { return (Flags0 & 0x200u) != 0; } }         // bit 9: m_NarrowRoad
        public bool DontUseForNavigation { get { return (Flags0 & 0x10000u) != 0; } } // bit 16: m_bDontUseForNavigation

        public bool Shortcut                                         // bit 17: m_bShortCut
        {
            get { return (Flags0 & 0x20000u) != 0; }
            set => Flags0 = value ? (Flags0 | 0x20000u) : (Flags0 & ~0x20000u);
        }


        public void Init(YndFile ynd, YndNode node1, YndNode node2, NodeLink link)
        {
            Ynd = ynd;
            Node1 = node1;
            Node2 = node2;
            RawData = link;
        }


        public void UpdateLength()
        {
            if (Node1 == null) return;
            if (Node2 == null) return;

            Distance = (byte)Math.Min(255, (Node2.Position - Node1.Position).Length());
        }


        public void CopyFlags(YndLink? link)
        {
            if (link == null) return;
            // Copy flag bits (bits 0-23), preserve Distance (bits 24-31)
            Flags0 = (Flags0 & 0xFF000000u) | (link.Flags0 & 0x00FFFFFFu);

            CheckIfJunction();
        }

        public bool IsTwoWay()
        {
            return LaneCountForward > 0 && LaneCountBackward > 0;
        }

        public void SetForwardLanesBidirectionally(int value)
        {
            LaneCountForward = value;

            if (Node1 != null && Node2 != null && Node2.TryGetLinkForNode(Node1, out var node2Link))
            {
                node2Link.LaneCountBackward = value;
            }

            CheckIfJunction();
        }

        public void SetBackwardLanesBidirectionally(int value)
        {
            LaneCountBackward = value;

            if (Node1 != null && Node2 != null && Node2.TryGetLinkForNode(Node1, out var node2Link))
            {
                node2Link.LaneCountForward = value;
            }

            CheckIfJunction();
        }

        public void CheckIfJunction()
        {
            Node1?.CheckIfJunction();
            Node2?.CheckIfJunction();
        }

        public Color4 GetColour()
        {
            //float f0 = Flags0.Value / 255.0f;
            //float f1 = Flags1.Value / 255.0f;
            //float f2 = Flags2.Value / 255.0f;
            //var c = new Color4(f0, f1, f2, 1.0f);

            var c = new Color4(0.0f, 0.0f, 0.0f, 0.5f);

            if (Shortcut)
            {
                c.Blue = 0.2f;
                c.Green = 0.2f;
                return c;
            }

            if (Node1 == null || Node2 == null) return c;

            if (Node1.IsDisabledUnk0 
                || Node1.IsDisabledUnk1 
                || Node2.IsDisabledUnk0 
                || Node2.IsDisabledUnk1)
            {
                if (Node1.OffRoad || Node2.OffRoad)
                {
                    c.Red = 0.0196f;
                    c.Green = 0.0156f;
                    c.Blue = 0.0043f;
                }
                c.Red = 0.02f;
                return c;
            }

            if (Node1.IsPedNode || Node2.IsPedNode)
            {
                c.Red = 0.2f;
                c.Green = 0.15f;
                return c;
            }

            if (Node1.OffRoad || Node2.OffRoad)
            {
                c.Red = 0.196f;
                c.Green = 0.156f;
                c.Blue = 0.043f;
                return c;
            }

            if (DontUseForNavigation)
            {
                c.Blue = 0.2f;
                c.Red = 0.2f;
                return c;
            }

            if (LaneCountForward == 0)
            {
                c.Red = 0.5f;
                return c;
            }

            c.Green = 0.2f;
            

            return c;
        }

        public float GetLaneWidth()
        {
            if (Shortcut || Node1?.IsPedNode == true || Node2?.IsPedNode == true)
            {
                return 0.5f;
            }

            if (DontUseForNavigation)
            {
                return 2.5f;
            }

            if (NarrowRoad)
            {
                return 4.0f;
            }

            return 5.5f;
        }

        public Vector3 GetDirection()
        {
            var p0 = Node1?.Position ?? Vector3.Zero;
            var p1 = Node2?.Position ?? Vector3.Zero;
            var diff = p1 - p0;
            return Vector3.Normalize(diff);
        }

        public void UpdateTargetIndex()
        {
            if (Node2 == null) return;
            _RawData.AreaID = Node2.AreaID;
            _RawData.NodeID = Node2.NodeID;
        }

        public override string ToString()
        {
            return Node2?._RawData.ToString() ?? _RawData.ToString();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class YndJunction
    {
        public YndFile? Ynd { get; set; }
        public NodeJunction _RawData;
        public NodeJunction RawData { get { return _RawData; } set { _RawData = value; } }
        public NodeJunctionRef RefData { get; set; }
        public YndJunctionHeightmap? Heightmap { get; set; }
        public short MaxZ { get; set; }
        public short MinZ { get; set; }
        public short PositionX { get; set; }
        public short PositionY { get; set; }

        public void Init(YndFile ynd, NodeJunction junc, NodeJunctionRef reff)
        {
            Ynd = ynd;
            RawData = junc;
            RefData = reff;
            MaxZ = junc.MaxZ;
            MinZ = junc.MinZ;
            PositionX = junc.PositionX;
            PositionY = junc.PositionY;
        }


        public void ResizeHeightmap()
        {
            Heightmap ??= new YndJunctionHeightmap([], this);
            Heightmap.Resize(_RawData.HeightmapDimX, _RawData.HeightmapDimY);
        }

        public void SetHeightmap(string text)
        {
            Heightmap ??= new YndJunctionHeightmap([], this);
            Heightmap.SetData(text);
        }


        public override string ToString()
        {
            return RefData.ToString();
        }


    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class YndJunctionHeightmap
    {
        public YndJunctionHeightmapRow[] Rows { get; set; } = [];
        public int CountX { get; set; }
        public int CountY { get; set; }

        public YndJunctionHeightmap(byte[] data, YndJunction junc)
        {
            if (data == null)
            { return; }

            var d = junc.RawData;
            int s = d.HeightmapPtr;
            CountX = d.HeightmapDimX;
            CountY = d.HeightmapDimY;

            if ((s + CountX * CountY) > data.Length)
            { return; }

            Rows = new YndJunctionHeightmapRow[CountY];


            for (int y = 0; y < CountY; y++)
            {
                int o = s + y * CountX;
                byte[] vals = new byte[CountX];
                Buffer.BlockCopy(data, o, vals, 0, CountX);
                Rows[y] = new YndJunctionHeightmapRow(vals);
            }
        }


        public byte[] GetBytes()
        {
            int cnt = CountX * CountY;
            var bytes = new byte[cnt];
            for (int y = 0; y < CountY; y++)
            {
                int o = y * CountX;
                var rowvals = Rows[y].Values;
                Buffer.BlockCopy(rowvals, 0, bytes, o, CountX);
            }
            return bytes;
        }


        public void Resize(int cx, int cy)
        {
            var nrows = new YndJunctionHeightmapRow[cy];
            for (int y = 0; y < cy; y++)
            {
                byte[] nvals = new byte[cx];
                int minx = Math.Min(cx, CountX);
                if ((Rows != null) && (y < Rows.Length))
                {
                    Buffer.BlockCopy(Rows[y].Values, 0, nvals, 0, minx);
                }
                nrows[y] = new YndJunctionHeightmapRow(nvals);
            }
            Rows = nrows;

            CountX = cx;
            CountY = cy;
        }

        public void SetData(string text)
        {
            var rsplit = new[] { '\n' };
            var csplit = new[] { ' ' };
            string[] rowstrs = text.Split(rsplit, StringSplitOptions.RemoveEmptyEntries);
            for (int y = 0; y < rowstrs.Length; y++)
            {
                string[] colstrs = rowstrs[y].Trim().Split(csplit, StringSplitOptions.RemoveEmptyEntries);
                int cx = colstrs.Length;
                byte[] vals = new byte[cx];
                for (int x = 0; x < cx; x++)
                {
                    byte.TryParse(colstrs[x], out vals[x]);
                }
                int minx = Math.Min(cx, CountX);
                if ((Rows != null) && (y < Rows.Length))
                {
                    var nrow = new byte[CountX];
                    Buffer.BlockCopy(vals, 0, nrow, 0, minx);
                    Rows[y].Values = nrow;
                }
            }
        }

        public string GetDataString()
        {
            StringBuilder sb = new();
            if (Rows != null)
            {
                foreach (var row in Rows)
                {
                    sb.AppendLine(row.ToString());
                }
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return CountX.ToString() + " x " + CountY.ToString();
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class YndJunctionHeightmapRow
    {
        public byte[] Values { get; set; } = [];

        public YndJunctionHeightmapRow(byte[] vals)
        {
            Values = vals;
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            for (int i = 0; i < Values.Length; i++)
            {
                if (i > 0) sb.Append(" ");
                sb.Append(Values[i].ToString().PadLeft(3, '0'));
                //sb.Append(Convert.ToString(Values[i], 16).ToUpper().PadLeft(2, '0'));
            }
            return sb.ToString();
        }
    }




    public class PathBVHNode
    {
        public int Depth;
        public int MaxDepth;
        public int Threshold;
        public List<BasePathNode> Nodes = [];
        public BoundingBox Box;
        public BoundingSphere Sphere;
        public PathBVHNode? Node1;
        public PathBVHNode? Node2;


        public void CalcBounds()
        {
            if ((Nodes == null) || (Nodes.Count <= 0)) return;

            Box.Minimum = new Vector3(float.MaxValue);
            Box.Maximum = new Vector3(float.MinValue);
            foreach (var node in Nodes)
            {
                Box.Minimum = Vector3.Min(Box.Minimum, node.Position);
                Box.Maximum = Vector3.Max(Box.Maximum, node.Position);
            }
            Sphere.Center = (Box.Minimum + Box.Maximum) * 0.5f;
            Sphere.Radius = (Box.Maximum - Box.Minimum).Length() * 0.5f;
        }


        public void Build()
        {
            if ((Nodes == null) || (Nodes.Count <= Threshold) || (Depth >= MaxDepth)) return;

            Vector3 avgsum = Vector3.Zero;
            foreach (var node in Nodes)
            {
                avgsum += node.Position;
            }
            Vector3 avg = avgsum * (1.0f / Nodes.Count);

            int countx = 0, county = 0, countz = 0;
            foreach (var node in Nodes)
            {
                if (node.Position.X < avg.X) countx++;
                if (node.Position.Y < avg.Y) county++;
                if (node.Position.Z < avg.Z) countz++;
            }

            int target = Nodes.Count / 2;
            int dx = Math.Abs(target - countx);
            int dy = Math.Abs(target - county);
            int dz = Math.Abs(target - countz);

            int axis = -1;
            if ((dx <= dy) && (dx <= dz)) axis = 0; //x seems best
            else if (dy <= dz) axis = 1; //y seems best
            else axis = 2; //z seems best


            List<BasePathNode> l1 = new();
            List<BasePathNode> l2 = new();
            foreach (var node in Nodes)
            {
                bool s = false;
                switch (axis)
                {
                    default:
                    case 0: s = (node.Position.X > avg.X); break;
                    case 1: s = (node.Position.Y > avg.Y); break;
                    case 2: s = (node.Position.Z > avg.Z); break;
                }
                if (s) l1.Add(node);
                else l2.Add(node);
            }

            var cdepth = Depth + 1;

            Node1 = new PathBVHNode();
            Node1.Depth = cdepth;
            Node1.MaxDepth = MaxDepth;
            Node1.Threshold = Threshold;
            Node1.Nodes = new List<BasePathNode>(l1);
            Node1.CalcBounds();
            Node1.Build();

            Node2 = new PathBVHNode();
            Node2.Depth = cdepth;
            Node2.MaxDepth = MaxDepth;
            Node2.Threshold = Threshold;
            Node2.Nodes = new List<BasePathNode>(l2);
            Node2.CalcBounds();
            Node2.Build();
        }


        public void UpdateForNode(BasePathNode node)
        {
            if (!Nodes.Contains(node)) return;
            Box.Minimum = Vector3.Min(Box.Minimum, node.Position);
            Box.Maximum = Vector3.Max(Box.Maximum, node.Position);

            if (Node1 != null) Node1.UpdateForNode(node);
            if (Node2 != null) Node2.UpdateForNode(node);
        }

    }

    public class PathBVH : PathBVHNode
    {

        public PathBVH(IEnumerable<BasePathNode> nodes, int threshold, int maxdepth)
        {
            Threshold = threshold;
            MaxDepth = maxdepth;
            Nodes = (nodes != null) ? new List<BasePathNode>(nodes) : new List<BasePathNode>();
            CalcBounds();
            Build();
        }

    }




    public interface BasePathNode
    {
        Vector3 Position { get; set; }
    }

    public interface BasePathData
    {
        //reuse this interface for file types that need to get paths rendered...

        EditorVertex[] GetPathVertices();
        EditorVertex[] GetTriangleVertices();
        Vector4[] GetNodePositions();
    }










    public class YndXml : MetaXmlBase
    {

        public static string GetXml(YndFile ynd)
        {
            StringBuilder sb = new();
            sb.AppendLine(XmlHeader);

            if ((ynd != null) && (ynd.NodeDictionary != null))
            {
                var name = "NodeDictionary";

                OpenTag(sb, 0, name);

                ynd.NodeDictionary.WriteXml(sb, 1);

                CloseTag(sb, 0, name);
            }

            return sb.ToString();
        }

    }


    public class XmlYnd
    {

        public static YndFile GetYnd(string xml)
        {
            XmlDocument doc = new();
            doc.LoadXml(xml);
            return GetYnd(doc);
        }

        public static YndFile GetYnd(XmlDocument doc)
        {
            YndFile ynd = new();
            ynd.NodeDictionary = new NodeDictionary();
            ynd.NodeDictionary.ReadXml(doc.DocumentElement ?? throw new XmlException("The path node document must have a root element."));
            ynd.InitNodesFromDictionary();
            ynd.BuildStructsOnSave = false; //structs don't need to be rebuilt here!
            return ynd;
        }





        public static TextHash GetTextHash(string? str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return 0;
            }
            if (str.StartsWith("hash_"))
            {
                return Convert.ToUInt32(str.Substring(5), 16);
            }
            else
            {
                uint h = GlobalText.TryFindHash(str);
                if (h != 0)
                {
                    return h;
                }

                return JenkHash.GenHash(str);
            }
        }

    }


}
