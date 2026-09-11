/*
    Copyright(c) 2016 Neodymium

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

//mangled to fit


using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    // CPathRegion : public pgBase
    // Contains all path node data for a single map region (32x32 grid)
    [TypeConverter(typeof(ExpandableObjectConverter))] public class NodeDictionary : ResourceFileBase, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 112; }
        }

        public ulong NodesPointer { get; set; }            // CPathNode* aNodes
        public uint NodesCount { get; set; }                // s32 NumNodes
        public uint NodesCountVehicle { get; set; }         // s32 NumNodesCarNodes
        public uint NodesCountPed { get; set; }             // s32 NumNodesPedNodes
        public ulong LinksPtr { get; set; }                 // CPathNodeLink* aLinks
        public uint LinksCount { get; set; }                // s32 NumLinks
        public ulong JunctionsPtr { get; set; }             // CPathVirtualJunction* aVirtualJunctions
        public ulong JunctionHeightmapBytesPtr { get; set; }// u8* aHeightSamples
        public uint JunctionMapFlag { get; set; } = 1;      // JunctionMapContainer flag (always 1)
        public ulong JunctionRefsPtr { get; set; }          // atBinaryMap<s32,u32> data pointer (JunctionMap.JunctionMap)
        public ushort JunctionRefsCount0 { get; set; }      // atBinaryMap count
        public ushort JunctionRefsCount1 { get; set; }      // atBinaryMap capacity (same as Count0)
        public uint JunctionsCount { get; set; }            // s32 NumJunctions
        public uint JunctionHeightmapBytesCount { get; set; }// u32 NumHeightSamples

        public Node[] Nodes { get; set; } = [];
        public NodeLink[] Links { get; set; } = [];
        public NodeJunction[] Junctions { get; set; } = [];
        public byte[] JunctionHeightmapBytes { get; set; } = [];
        public NodeJunctionRef[] JunctionRefs { get; set; } = [];


        private ResourceSystemStructBlock<Node>? NodesBlock;
        private ResourceSystemStructBlock<NodeLink>? LinksBlock;
        private ResourceSystemStructBlock<NodeJunction>? JunctionsBlock;
        private ResourceSystemStructBlock<byte>? JunctionHeightmapBytesBlock;
        private ResourceSystemStructBlock<NodeJunctionRef>? JunctionRefsBlock;



        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            this.NodesPointer = reader.ReadUInt64();
            this.NodesCount = reader.ReadUInt32();
            this.NodesCountVehicle = reader.ReadUInt32();
            this.NodesCountPed = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.LinksPtr = reader.ReadUInt64();
            this.LinksCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.JunctionsPtr = reader.ReadUInt64();
            this.JunctionHeightmapBytesPtr = reader.ReadUInt64();
            this.JunctionMapFlag = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.JunctionRefsPtr = reader.ReadUInt64();
            this.JunctionRefsCount0 = reader.ReadUInt16();
            this.JunctionRefsCount1 = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.JunctionsCount = reader.ReadUInt32();
            this.JunctionHeightmapBytesCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            this.Nodes = reader.ReadStructsAt<Node>(this.NodesPointer, this.NodesCount) ?? [];
            this.Links = reader.ReadStructsAt<NodeLink>(this.LinksPtr, this.LinksCount) ?? [];
            this.Junctions = reader.ReadStructsAt<NodeJunction>(this.JunctionsPtr, this.JunctionsCount) ?? [];
            this.JunctionHeightmapBytes = reader.ReadBytesAt(this.JunctionHeightmapBytesPtr, this.JunctionHeightmapBytesCount) ?? [];
            this.JunctionRefs = reader.ReadStructsAt<NodeJunctionRef>(this.JunctionRefsPtr, this.JunctionRefsCount1) ?? [];



        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            NodesPointer = (ulong)(NodesBlock?.FilePosition ?? 0);
            NodesCount = (uint)(Nodes.Length); //assume NodesCountVehicle and Ped already updated..
            LinksPtr = (ulong)(LinksBlock?.FilePosition ?? 0);
            LinksCount = (uint)(Links?.Length ?? 0);
            JunctionsPtr = (ulong)(JunctionsBlock?.FilePosition ?? 0);
            JunctionHeightmapBytesPtr = (ulong)(JunctionHeightmapBytesBlock?.FilePosition ?? 0);
            JunctionRefsPtr = (ulong)(JunctionRefsBlock?.FilePosition ?? 0);
            JunctionRefsCount0 = (ushort)(JunctionRefs?.Length ?? 0);
            JunctionRefsCount1 = JunctionRefsCount0;
            JunctionsCount = (uint)(Junctions.Length);
            JunctionHeightmapBytesCount = (uint)(JunctionHeightmapBytes?.Length ?? 0);


            // write structure data
            writer.Write(this.NodesPointer);
            writer.Write(this.NodesCount);
            writer.Write(this.NodesCountVehicle);
            writer.Write(this.NodesCountPed);
            writer.Write(0u);
            writer.Write(this.LinksPtr);
            writer.Write(this.LinksCount);
            writer.Write(0u);
            writer.Write(this.JunctionsPtr);
            writer.Write(this.JunctionHeightmapBytesPtr);
            writer.Write(this.JunctionMapFlag);
            writer.Write(0u);
            writer.Write(this.JunctionRefsPtr);
            writer.Write(this.JunctionRefsCount0);
            writer.Write(this.JunctionRefsCount1);
            writer.Write(0u);
            writer.Write(this.JunctionsCount);
            writer.Write(this.JunctionHeightmapBytesCount);
            writer.Write(0u);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());

            if ((JunctionRefs != null) && (JunctionRefs.Length > 0))
            {
                JunctionRefsBlock = new ResourceSystemStructBlock<NodeJunctionRef>(JunctionRefs);
                list.Add(JunctionRefsBlock);
            }
            if ((JunctionHeightmapBytes != null) && (JunctionHeightmapBytes.Length > 0))
            {
                JunctionHeightmapBytesBlock = new ResourceSystemStructBlock<byte>(JunctionHeightmapBytes);
                list.Add(JunctionHeightmapBytesBlock);
            }
            if ((Junctions != null) && (Junctions.Length > 0))
            {
                JunctionsBlock = new ResourceSystemStructBlock<NodeJunction>(Junctions);
                list.Add(JunctionsBlock);
            }
            if ((Links != null) && (Links.Length > 0))
            {
                LinksBlock = new ResourceSystemStructBlock<NodeLink>(Links);
                list.Add(LinksBlock);
            }
            if ((Nodes != null) && (Nodes.Length > 0))
            {
                NodesBlock = new ResourceSystemStructBlock<Node>(Nodes);
                list.Add(NodesBlock);
            }


            return list.ToArray();
        }




        public void WriteXml(StringBuilder sb, int indent)
        {
            YndXml.ValueTag(sb, indent, "VehicleNodeCount", NodesCountVehicle.ToString());
            YndXml.ValueTag(sb, indent, "PedNodeCount", NodesCountPed.ToString());

            XmlNodeWrapper[]? nodes = null;
            int nodecount = Nodes.Length;
            if (nodecount > 0)
            {
                nodes = new XmlNodeWrapper[nodecount];
                for (int i = 0; i < nodecount; i++)
                {
                    nodes[i] = new XmlNodeWrapper(Nodes[i], Links);
                }
            }
            YndXml.WriteItemArray(sb, nodes, indent, "Nodes");


            XmlJunctionWrapper[]? juncs = null;
            int junccount = Junctions.Length;
            if (junccount > 0)
            {
                juncs = new XmlJunctionWrapper[junccount];
                for (int i = 0; i < junccount; i++)
                {
                    juncs[i] = new XmlJunctionWrapper(Junctions[i], JunctionHeightmapBytes);
                }
            }
            YndXml.WriteItemArray(sb, juncs, indent, "Junctions");

            YndXml.WriteItemArray(sb, JunctionRefs, indent, "JunctionRefs");

        }
        public void ReadXml(XmlNode node)
        {
            NodesCountVehicle = Xml.GetChildUIntAttribute(node, "VehicleNodeCount", "value");
            NodesCountPed = Xml.GetChildUIntAttribute(node, "PedNodeCount", "value");

            List<Node> nodelist = new();
            List<NodeLink> linklist = new();
            List<NodeJunction> junclist = new();
            List<byte> jhmblist = new();
            List<NodeJunctionRef> jreflist = new();

            var nodesnode = node.SelectSingleNode("Nodes");
            if (nodesnode != null)
            {
                var nodeitems = nodesnode.SelectNodes("Item");
                foreach (XmlNode nodeitem in nodeitems?.Cast<XmlNode>() ?? [])
                {
                    XmlNodeWrapper n = new(linklist);
                    n.ReadXml(nodeitem);
                    nodelist.Add(n.Node);
                }
            }

            var juncsnode = node.SelectSingleNode("Junctions");
            if (juncsnode != null)
            {
                var juncitems = juncsnode.SelectNodes("Item");
                foreach (XmlNode juncitem in juncitems?.Cast<XmlNode>() ?? [])
                {
                    XmlJunctionWrapper j = new(jhmblist);
                    j.ReadXml(juncitem);
                    junclist.Add(j.Junction);
                }
            }

            var jrefsnode = node.SelectSingleNode("JunctionRefs");
            if (jrefsnode != null)
            {
                var jrefitems = jrefsnode.SelectNodes("Item");
                foreach (XmlNode jrefitem in jrefitems?.Cast<XmlNode>() ?? [])
                {
                    NodeJunctionRef jref = new();
                    jref.ReadXml(jrefitem);
                    jreflist.Add(jref);
                }
            }

            NodesCount = (uint)nodelist.Count;
            Nodes = nodelist.ToArray();
            LinksCount = (uint)linklist.Count;
            Links = linklist.ToArray();
            JunctionsCount = (uint)junclist.Count;
            Junctions = junclist.ToArray();
            JunctionHeightmapBytesCount = (uint)jhmblist.Count;
            JunctionHeightmapBytes = jhmblist.ToArray();
            JunctionRefsCount0 = (ushort)jreflist.Count;
            JunctionRefsCount1 = JunctionRefsCount0;
            JunctionRefs = jreflist.ToArray();

        }


        class XmlNodeWrapper : IMetaXmlItem
        {
            public Node Node;
            private NodeLink[]? AllLinks;
            private List<NodeLink>? AllLinksList;

            public XmlNodeWrapper(Node node, NodeLink[] allLinks)
            {
                Node = node;
                AllLinks = allLinks;
            }
            public XmlNodeWrapper(List<NodeLink> allLinksList)
            {
                AllLinksList = allLinksList;
            }
            public void WriteXml(StringBuilder sb, int indent)
            {
                Node.WriteXml(sb, indent, AllLinks ?? throw new InvalidOperationException("This node wrapper was created for reading."));
            }
            public void ReadXml(XmlNode node)
            {
                Node = new Node();
                Node.ReadXml(node, AllLinksList ?? throw new InvalidOperationException("This node wrapper was created for writing."));
            }
        }
        class XmlJunctionWrapper : IMetaXmlItem
        {
            public NodeJunction Junction;
            private byte[]? AllHeightmapData;
            private List<byte>? AllHeightmapDataList;

            public XmlJunctionWrapper(NodeJunction junc, byte[] allHeightmapData)
            {
                Junction = junc;
                AllHeightmapData = allHeightmapData;
            }
            public XmlJunctionWrapper(List<byte> allHeightmapDataList)
            {
                AllHeightmapDataList = allHeightmapDataList;
            }
            public void WriteXml(StringBuilder sb, int indent)
            {
                Junction.WriteXml(sb, indent, AllHeightmapData ?? throw new InvalidOperationException("This junction wrapper was created for reading."));
            }
            public void ReadXml(XmlNode node)
            {
                Junction = new NodeJunction();
                Junction.ReadXml(node, AllHeightmapDataList ?? throw new InvalidOperationException("This junction wrapper was created for writing."));
            }
        }
    }

    // CPathNode (40 bytes on 64-bit)
    // Serialized via DeclareStruct: m_address, m_streetNameHash, m_startIndexOfLinks, m_coorsX, m_coorsY, m_iAsInteger1, m_iAsInteger2
    // m_pNext/m_pPrevious/m_distanceToTarget are runtime-only (STRUCT_IGNORE), zeroed in file
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct Node
    {
        public uint Unused0 { get; set; } // m_pNext (low 32 bits, runtime only, zero in file)
        public uint Unused1 { get; set; } // m_pNext (high 32 bits)
        public uint Unused2 { get; set; } // m_pPrevious (low 32 bits, runtime only, zero in file)
        public uint Unused3 { get; set; } // m_pPrevious (high 32 bits)
        public ushort AreaID { get; set; }      // CNodeAddress.m_region (u16)
        public ushort NodeID { get; set; }      // CNodeAddress.m_Index (u16)
        public TextHash StreetName { get; set; }// m_streetNameHash (u32)
        public ushort DistanceToTarget { get; set; } // m_distanceToTarget (s16, runtime only, zero in file)
        public ushort LinkID { get; set; }      // m_startIndexOfLinks (s16)
        public short PositionX { get; set; }    // m_coorsX (s16) - divide by 4.0 for world coords (PATHCOORD_XYSHIFT)
        public short PositionY { get; set; }    // m_coorsY (s16) - divide by 4.0 for world coords
        // m_iAsInteger1 (u32) bitfield:
        //   bits 0-2:   m_group (3)              - flood fill group
        //   bit  3:     m_Offroad (1)
        //   bit  4:     m_onPlayersRoad (1)      - runtime only
        //   bit  5:     m_noBigVehicles (1)
        //   bit  6:     m_cannotGoRight (1)
        //   bit  7:     m_cannotGoLeft (1)
        //   bit  8:     m_slipLane (1)
        //   bit  9:     m_indicateKeepLeft (1)
        //   bit  10:    m_indicateKeepRight (1)
        //   bits 11-15: m_specialFunction (5)    - see PathNodeSpecialUse enum
        //   bits 16-31: m_coorsZ (16)            - divide by 32.0 for world Z (PATHCOORD_ZSHIFT)
        public uint Flags0 { get; set; }        // m_iAsInteger1
        // m_iAsInteger2 (u32) bitfield:
        //   bit  0:     m_noGps (1)
        //   bit  1:     m_closeToCamera (1)      - runtime only
        //   bit  2:     m_slipJunction (1)
        //   bit  3:     m_alreadyFound (1)       - runtime only
        //   bit  4:     m_switchedOffOriginal (1)
        //   bit  5:     m_waterNode (1)
        //   bit  6:     m_highwayOrLowBridge (1)
        //   bit  7:     m_switchedOff (1)
        //   bit  8:     m_qualifiesAsJunction (1)
        //   bits 9-10:  m_speed (2)              - 0=slow, 1=normal, 2=fast, 3=double
        //   bits 11-15: m_numLinks (5)
        //   bit  16:    m_inTunnel (1)
        //   bits 17-23: m_distanceHash (7)       - runtime spacing
        //   bits 24-27: m_density (4)            - 0=empty, 15=normal
        //   bits 28-30: m_deadEndness (3)
        //   bit  31:    m_leftOnly (1)
        public uint Flags1 { get; set; }        // m_iAsInteger2

        public override string ToString()
        {
            return AreaID.ToString() + ", " + NodeID.ToString() + ", " + StreetName.ToString();
        }

        public void WriteXml(StringBuilder sb, int indent, NodeLink[] allLinks)
        {
            Vector3 p = new();
            p.X = PositionX / 4.0f;
            p.Y = PositionY / 4.0f;
            p.Z = ((short)(Flags0 >> 16)) / 32.0f;
            byte lcflags = (byte)((Flags1 >> 8) & 0xFF);
            int linkCount = lcflags >> 3;
            int linkCountUnk = lcflags & 7;

            YndXml.ValueTag(sb, indent, "AreaID", AreaID.ToString());
            YndXml.ValueTag(sb, indent, "NodeID", NodeID.ToString());
            YndXml.StringTag(sb, indent, "StreetName", YndXml.HashString(StreetName));
            YndXml.SelfClosingTag(sb, indent, "Position " + FloatUtil.GetVector3XmlString(p));
            YndXml.ValueTag(sb, indent, "Flags0", (Flags0 & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags1", ((Flags0 >> 8) & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags2", (Flags1 & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags3", ((Flags1 >> 16) & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags4", ((Flags1 >> 24) & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags5", linkCountUnk.ToString());

            NodeLink[]? links = null;
            if (linkCount > 0)
            {
                links = new NodeLink[linkCount];
                for (int i = 0; i < linkCount; i++)
                {
                    links[i] = allLinks[LinkID + i];
                }
            }
            YndXml.WriteItemArray(sb, links, indent, "Links");

        }
        public void ReadXml(XmlNode node, List<NodeLink> allLinksList)
        {
            AreaID = (ushort)Xml.GetChildUIntAttribute(node, "AreaID", "value");
            NodeID = (ushort)Xml.GetChildUIntAttribute(node, "NodeID", "value");
            StreetName = XmlYnd.GetTextHash(Xml.GetChildInnerText(node, "StreetName"));
            Vector3 p = Xml.GetChildVector3Attributes(node, "Position");
            PositionX = (short)(p.X * 4.0f);
            PositionY = (short)(p.Y * 4.0f);
            ushort posZ = (ushort)(short)(p.Z * 32.0f);
            byte f0 = (byte)Xml.GetChildUIntAttribute(node, "Flags0", "value");
            byte f1 = (byte)Xml.GetChildUIntAttribute(node, "Flags1", "value");
            byte f2 = (byte)Xml.GetChildUIntAttribute(node, "Flags2", "value");
            byte f3 = (byte)Xml.GetChildUIntAttribute(node, "Flags3", "value");
            byte f4 = (byte)Xml.GetChildUIntAttribute(node, "Flags4", "value");
            int linkCountUnk = (byte)Xml.GetChildUIntAttribute(node, "Flags5", "value");

            Flags0 = (uint)f0 | ((uint)f1 << 8) | ((uint)posZ << 16);

            LinkID = (ushort)allLinksList.Count;
            int linkCount = 0;
            var linksnode = node.SelectSingleNode("Links");
            if (linksnode != null)
            {
                var linkitems = linksnode.SelectNodes("Item");
                foreach (XmlNode linkitem in linkitems?.Cast<XmlNode>() ?? [])
                {
                    NodeLink link = new();
                    link.ReadXml(linkitem);
                    allLinksList.Add(link);
                    linkCount++;
                }
            }
            byte lcflags = (byte)((linkCount << 3) + (linkCountUnk & 7));
            Flags1 = (uint)f2 | ((uint)lcflags << 8) | ((uint)f3 << 16) | ((uint)f4 << 24);
        }
    }

    // CPathNodeLink (8 bytes)
    // Serialized via DeclareStruct: m_OtherNode (CNodeAddress), m_iAsInteger1 (u32 bitfield)
    // m_iAsInteger1 bitfield:
    //   bit  0:     m_bGpsCanGoBothWays (1)
    //   bit  1:     m_bBlockIfNoLanes (1)
    //   bits 2-6:   m_Tilt (5)
    //   bits 7-8:   m_TiltFalloff (2)
    //   bit  9:     m_NarrowRoad (1)
    //   bit  10:    m_LeadsToDeadEnd (1)
    //   bit  11:    m_LeadsFromDeadEnd (1)
    //   bits 12-15: m_Width (4)                  - center road width in meters
    //   bit  16:    m_bDontUseForNavigation (1)
    //   bit  17:    m_bShortCut (1)
    //   bits 18-20: m_LanesFromOtherNode (3)
    //   bits 21-23: m_LanesToOtherNode (3)
    //   bits 24-31: m_Distance (8)               - link distance in meters (u8)
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct NodeLink : IMetaXmlItem
    {
        public ushort AreaID { get; set; }          // CNodeAddress.m_region (u16) - m_OtherNode
        public ushort NodeID { get; set; }          // CNodeAddress.m_Index (u16)
        public uint Flags0 { get; set; }            // m_iAsInteger1 (u32 bitfield)

        public override string ToString()
        {
            return AreaID.ToString() + ", " + NodeID.ToString() + ", " + (Flags0 & 0xFF).ToString() + ", " + ((Flags0 >> 8) & 0xFF).ToString() + ", " + ((Flags0 >> 16) & 0xFF).ToString() + ", " + ((Flags0 >> 24) & 0xFF).ToString();
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YndXml.ValueTag(sb, indent, "ToAreaID", AreaID.ToString());
            YndXml.ValueTag(sb, indent, "ToNodeID", NodeID.ToString());
            YndXml.ValueTag(sb, indent, "Flags0", (Flags0 & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags1", ((Flags0 >> 8) & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "Flags2", ((Flags0 >> 16) & 0xFF).ToString());
            YndXml.ValueTag(sb, indent, "LinkLength", ((Flags0 >> 24) & 0xFF).ToString());
        }
        public void ReadXml(XmlNode node)
        {
            AreaID = (ushort)Xml.GetChildUIntAttribute(node, "ToAreaID", "value");
            NodeID = (ushort)Xml.GetChildUIntAttribute(node, "ToNodeID", "value");
            byte f0 = (byte)Xml.GetChildUIntAttribute(node, "Flags0", "value");
            byte f1 = (byte)Xml.GetChildUIntAttribute(node, "Flags1", "value");
            byte f2 = (byte)Xml.GetChildUIntAttribute(node, "Flags2", "value");
            byte dist = (byte)Xml.GetChildUIntAttribute(node, "LinkLength", "value");
            Flags0 = (uint)f0 | ((uint)f1 << 8) | ((uint)f2 << 16) | ((uint)dist << 24);
        }
    }

    // CPathVirtualJunction (12 bytes)
    // Serialized via DeclareStruct: m_uMaxZ, m_iMinX, m_iMinY, m_nHeightBaseWorld,
    //   m_nStartIndexOfHeightSamples, m_nXSamples, m_nYSamples
    // XY coords use PATHCOORD_XYSHIFT (4.0), Z coords use PATHCOORD_ZSHIFT (32.0)
    // Note: C++ stores MaxZ as u16 and MinZ as u16, but UINT16_TO_COORSZ casts to s16 for conversion
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct NodeJunction
    {
        public short MaxZ { get; set; }             // m_uMaxZ (u16) - max Z height of junction area
        public short PositionX { get; set; }        // m_iMinX (s16) - min X of junction area
        public short PositionY { get; set; }        // m_iMinY (s16) - min Y of junction area
        public short MinZ { get; set; }             // m_nHeightBaseWorld (u16) - minimum height base for heightmap
        public ushort HeightmapPtr { get; set; }    // m_nStartIndexOfHeightSamples (u16)
        public byte HeightmapDimX { get; set; }     // m_nXSamples (u8)
        public byte HeightmapDimY { get; set; }     // m_nYSamples (u8)

        public override string ToString()
        {
            return PositionX.ToString() + ", " + PositionY.ToString() + ": " + MinZ.ToString() + ", " + MaxZ.ToString() + ": " + HeightmapDimX.ToString() + " x " + HeightmapDimY.ToString();
        }

        public void WriteXml(StringBuilder sb, int indent, byte[] allHeightmapData)
        {
            Vector2 p = new();
            p.X = PositionX / 4.0f;
            p.Y = PositionY / 4.0f;
            float minz = MinZ / 32.0f;
            float maxz = MaxZ / 32.0f;

            YndXml.SelfClosingTag(sb, indent, "Position " + FloatUtil.GetVector2XmlString(p));
            YndXml.ValueTag(sb, indent, "MinZ", FloatUtil.ToString(minz));
            YndXml.ValueTag(sb, indent, "MaxZ", FloatUtil.ToString(maxz));
            YndXml.ValueTag(sb, indent, "SizeX", HeightmapDimX.ToString());
            YndXml.ValueTag(sb, indent, "SizeY", HeightmapDimY.ToString());

            byte[]? hmdata = null;
            int hmbcount = HeightmapDimX * HeightmapDimY;
            if (hmbcount > 0)
            {
                hmdata = new byte[hmbcount];
                Buffer.BlockCopy(allHeightmapData, HeightmapPtr, hmdata, 0, hmbcount);
            }
            YndXml.WriteRawArray(sb, hmdata, indent, "Heightmap", "", RelXml.FormatHexByte, Math.Max(HeightmapDimX, (byte)1));

        }
        public void ReadXml(XmlNode node, List<byte> allHeightmapDataList)
        {
            Vector2 p = Xml.GetChildVector2Attributes(node, "Position");
            float minz = Xml.GetChildFloatAttribute(node, "MinZ", "value");
            float maxz = Xml.GetChildFloatAttribute(node, "MaxZ", "value");
            HeightmapDimX = (byte)Xml.GetChildUIntAttribute(node, "SizeX", "value");
            HeightmapDimY = (byte)Xml.GetChildUIntAttribute(node, "SizeY", "value");
            PositionX = (short)(p.X * 4.0f);
            PositionY = (short)(p.Y * 4.0f);
            MinZ = (short)(minz * 32.0f);
            MaxZ = (short)(maxz * 32.0f);

            byte[] hmdata = Xml.GetChildRawByteArray(node, "Heightmap");
            HeightmapPtr = (ushort)allHeightmapDataList.Count;
            if (hmdata != null)
            {
                allHeightmapDataList.AddRange(hmdata);
            }

        }
    }

    // Entry in atBinaryMap<s32, u32> JunctionMap
    // Key = CNodeAddress (u32 packed as region:16 + index:16)
    // Value = junction index (u32, only lower 16 bits used since max 256 junctions per region)
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct NodeJunctionRef : IMetaXmlItem
    {
        public ushort AreaID { get; set; }          // CNodeAddress.m_region (key high 16 bits)
        public ushort NodeID { get; set; }          // CNodeAddress.m_Index (key low 16 bits)
        public ushort JunctionID { get; set; }      // Junction index (value low 16 bits)

        public override string ToString()
        {
            return AreaID.ToString() + ", " + NodeID.ToString() + ", " + JunctionID.ToString();
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YndXml.ValueTag(sb, indent, "AreaID", AreaID.ToString());
            YndXml.ValueTag(sb, indent, "NodeID", NodeID.ToString());
            YndXml.ValueTag(sb, indent, "JunctionID", JunctionID.ToString());
        }
        public void ReadXml(XmlNode node)
        {
            AreaID = (ushort)Xml.GetChildUIntAttribute(node, "AreaID", "value");
            NodeID = (ushort)Xml.GetChildUIntAttribute(node, "NodeID", "value");
            JunctionID = (ushort)Xml.GetChildUIntAttribute(node, "JunctionID", "value");
        }
    }








}
