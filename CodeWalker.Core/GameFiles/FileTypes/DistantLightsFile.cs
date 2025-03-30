using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TC = System.ComponentModel.TypeConverterAttribute;
using EXP = System.ComponentModel.ExpandableObjectConverter;
using System.Xml;
using System.Xml.Linq;

namespace CodeWalker.GameFiles
{

    [TC(typeof(EXP))]
    public abstract class DistantLightsBase : IMetaXmlItem
    {
        public virtual void ReadXml(XmlNode node)
        { }
        public virtual void WriteXml(StringBuilder sb, int indent)
        { }
    }
    [TC(typeof(EXP))] public class DistantLightsFile : DistantLightsBase, PackedFile
    {
        public RpfFileEntry FileEntry { get; set; }

        public bool HD { get; set; } = true;
        public uint GridSize { get; set; } = 32;
        public uint CellSize { get; set; } = 512;
        public uint CellCount { get; set; } = 1024;
        public uint NodeCount { get; set; } // PointsCount?
        public uint PathCount { get; set; }
        public uint[] PathIndices { get; set; } //CellCount
        public uint[] PathCounts1 { get; set; } //CellCount
        public uint[] PathCounts2 { get; set; } //CellCount
        public DistantLightsPoint[] Points { get; set; } //NodeCount
        public DistantLightsGroup[] Paths { get; set; } //PathCount
        public DistantLightsCell[] Cells { get; set; } //CellCount (built from loaded data)


        public DistantLightsFile()
        { }
        public DistantLightsFile(RpfFileEntry entry)
        {
            FileEntry = entry;
        }

        public void Load(byte[] data, RpfFileEntry entry)
        {
            if (entry != null)
            {
                FileEntry = entry;

                if (!entry.NameLower.EndsWith("_hd.dat"))
                {
                    HD = false;
                    GridSize = 16;
                    CellSize = 1024;
                    CellCount = 256;
                }
            }

            using (MemoryStream ms = new MemoryStream(data))
            {
                DataReader r = new DataReader(ms, Endianess.BigEndian);

                Read(r);
            };
        }
        public byte[] Save()
        {
            MemoryStream s = new MemoryStream();
            DataWriter w = new DataWriter(s, Endianess.BigEndian);

            Write(w);

            var buf = new byte[s.Length];
            s.Position = 0;
            s.Read(buf, 0, buf.Length);
            return buf;
        }


        private void Read(DataReader r)
        {
            NodeCount = r.ReadUInt32();
            PathCount = r.ReadUInt32();
            PathIndices = new uint[CellCount];
            PathCounts1 = new uint[CellCount];
            PathCounts2 = new uint[CellCount];
            Points = new DistantLightsPoint[NodeCount];
            Paths = new DistantLightsGroup[PathCount];
            for (uint i = 0; i < CellCount; i++)
            {
                PathIndices[i] = r.ReadUInt32();
            }
            for (uint i = 0; i < CellCount; i++)
            {
                PathCounts1[i] = r.ReadUInt32();
            }
            for (uint i = 0; i < CellCount; i++)
            {
                PathCounts2[i] = r.ReadUInt32();
            }
            for (uint i = 0; i < NodeCount; i++)
            {
                Points[i] = new DistantLightsPoint(r);
            }
            for (uint i = 0; i < PathCount; i++)
            {
                Paths[i] = new DistantLightsGroup(r, HD);
            }

            BuildCells();

        }
        private void Write(DataWriter w)
        {
            w.Write(NodeCount);
            w.Write(PathCount);

            for (uint i = 0; i < CellCount; i++)
            {
                w.Write(PathIndices[i]);
            }
            for (uint i = 0; i < CellCount; i++)
            {
                w.Write(PathCounts1[i]);
            }
            for (uint i = 0; i < CellCount; i++)
            {
                w.Write(PathCounts2[i]);
            }
            for (uint i = 0; i < NodeCount; i++)
            {
                Points[i].Write(w);
            }
            for (uint i = 0; i < PathCount; i++)
            {
                Paths[i].Write(w, HD);
            }

        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            if (HD)
            {
                DistantLightsXml.ValueTag(sb, indent, "HD", true.ToString());
            }
            DistantLightsXml.ValueTag(sb, indent, "GridSize", GridSize.ToString());
            DistantLightsXml.ValueTag(sb, indent, "CellSize", CellSize.ToString());
            DistantLightsXml.ValueTag(sb, indent, "CellCount", CellCount.ToString());
            DistantLightsXml.ValueTag(sb, indent, "NodeCount", NodeCount.ToString());
            DistantLightsXml.ValueTag(sb, indent, "PathCount", PathCount.ToString());
            if (PathIndices != null)
            {
                DistantLightsXml.WriteRawArray(sb, PathIndices, indent, "PathIndices", "");
            }
            if (PathCounts1 != null)
            {
                DistantLightsXml.WriteRawArray(sb, PathCounts1, indent, "PathCounts1", "");
            }
            if (PathCounts2 != null)
            {
                DistantLightsXml.WriteRawArray(sb, PathCounts2, indent, "PathCounts2", "");
            }
            DistantLightsXml.WriteItemArray(sb, Points, indent, "Nodes");
            DistantLightsXml.WriteItemArray(sb, Paths, indent, "Paths");
            DistantLightsXml.WriteItemArray(sb, Cells, indent, "Cells");
        }

        public override void ReadXml(XmlNode node)
        {
            HD = Xml.GetChildBoolAttribute(node, "HD");
            GridSize = Xml.GetChildUIntAttribute(node, "GridSize", "value");
            CellSize = Xml.GetChildUIntAttribute(node, "CellSize", "value");
            CellCount = Xml.GetChildUIntAttribute(node, "CellCount", "value");
            NodeCount = Xml.GetChildUIntAttribute(node, "NodeCount", "value");
            PathCount = Xml.GetChildUIntAttribute(node, "PathCount", "value");
            PathIndices = Xml.GetChildRawUintArrayNullable(node, "PathIndices");
            PathCounts1 = Xml.GetChildRawUintArrayNullable(node, "PathCounts1");
            PathCounts2 = Xml.GetChildRawUintArrayNullable(node, "PathCounts2");
            Points = XmlMeta.ReadItemArrayNullable<DistantLightsPoint>(node, "Nodes");
            Paths = XmlMeta.ReadItemArrayNullable<DistantLightsGroup>(node, "Paths");
            Cells = XmlMeta.ReadItemArrayNullable<DistantLightsCell>(node, "Cells");
        }

        private void BuildCells()
        {
            for (int i = 0; i < PathCount; i++)
            {
                var path = Paths[i];
                path.Nodes = new DistantLightsPoint[path.PointCount];
                for (int n = 0; n < path.PointCount; n++)
                {
                    path.Nodes[n] = Points[path.PointOffset + n];
                }
            }

            Cells = new DistantLightsCell[CellCount];
            for (uint x = 0; x < GridSize; x++)
            {
                for (uint y = 0; y < GridSize; y++)
                {
                    var i = x * GridSize + y;
                    var cell = new DistantLightsCell();
                    cell.Index = i;
                    cell.CellX = x;
                    cell.CellY = y;
                    cell.CellMin = new Vector2(x, y) * CellSize - 8192.0f;
                    cell.CellMax = cell.CellMin + CellSize;
                    var pc1 = PathCounts1[i];
                    if (pc1 > 0)
                    {
                        cell.Paths1 = new DistantLightsGroup[pc1];
                        for (uint l = 0; l < pc1; l++)
                        {
                            cell.Paths1[l] = Paths[PathIndices[i] + l];
                        }
                    }
                    var pc2 = PathCounts2[i];
                    if (pc2 > 0)
                    {
                        cell.Paths2 = new DistantLightsGroup[pc2];
                        for (uint l = 0; l < pc2; l++)
                        {
                            cell.Paths2[l] = Paths[PathIndices[i] + l + pc1];
                        }
                    }
                    Cells[i] = cell;
                }
            }

        }

    }

    [TC(typeof(EXP))] public class DistantLightsPoint : DistantLightsBase
    {
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }

        public DistantLightsPoint()
        { }
        public DistantLightsPoint(DataReader r)
        {
            Read(r);
        }

        public void Read(DataReader r)
        {
            X = r.ReadInt16();
            Y = r.ReadInt16();
            Z = r.ReadInt16();
        }
        public void Write(DataWriter w)
        {
            w.Write(X);
            w.Write(Y);
            w.Write(Z);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            DistantLightsXml.ValueTag(sb, indent, "X", X.ToString());
            DistantLightsXml.ValueTag(sb, indent, "Y", Y.ToString());
            DistantLightsXml.ValueTag(sb, indent, "Z", Z.ToString());
        }
        public override void ReadXml(XmlNode node)
        {
            X = (short)Xml.GetChildUIntAttribute(node, "X", "value");
            Y = (short)Xml.GetChildUIntAttribute(node, "Y", "value");
            Z = (short)Xml.GetChildUIntAttribute(node, "Z", "value");
        }

        public Vector3 Vector
        {
            get { return new Vector3(X, Y, Z); }
            set { X = (short)Math.Round(value.X); Y = (short)Math.Round(value.Y); Z = (short)Math.Round(value.Z); }
        }

        public override string ToString()
        {
            return Vector.ToString();
        }
    }

    [TC(typeof(EXP))] public class DistantLightsGroup : DistantLightsBase
    {
        public short CenterX { get; set; }
        public short CenterY { get; set; }
        public short CenterZ { get; set; }
        public short Radius { get; set; }
        public ushort PointOffset { get; set; }
        public ushort PointCount { get; set; }
        public ushort Flags { get; set; }
        public ushort DisplayProperties { get; set; }
        public float DistanceOffset { get; set; }
        public byte RandomSeed1 { get; set; }
        public byte RandomSeed2 { get; set; }
        public byte RandomSeed3 { get; set; }
        public byte RandomSeed4 { get; set; }

        public DistantLightsPoint[] Nodes { get; set; }

        public DistantLightsGroup()
        { }
        public DistantLightsGroup(DataReader r, bool hd)
        {
            Read(r, hd);
        }

        public void Read(DataReader r, bool hd)
        {
            CenterX = r.ReadInt16();
            CenterY = r.ReadInt16();
            CenterZ = r.ReadInt16();
            Radius = r.ReadInt16();
            PointOffset = r.ReadUInt16();
            PointCount = r.ReadUInt16();
            if (hd)
            {
                Flags = r.ReadUInt16();
                DisplayProperties = r.ReadUInt16();
                DistanceOffset = r.ReadSingle();
                RandomSeed1 = r.ReadByte();
                RandomSeed2 = r.ReadByte();
                RandomSeed3 = r.ReadByte();
                RandomSeed4 = r.ReadByte();
            }
            else
            {
                RandomSeed1 = r.ReadByte();
                RandomSeed2 = r.ReadByte();
            }
        }
        public void Write(DataWriter w, bool hd)
        {
            w.Write(CenterX);
            w.Write(CenterY);
            w.Write(CenterZ);
            w.Write(Radius);
            w.Write(PointOffset);
            w.Write(PointCount);
            if (hd)
            {
                w.Write(Flags);
                w.Write(DisplayProperties);
                w.Write(DistanceOffset);
                w.Write(RandomSeed1);
                w.Write(RandomSeed2);
                w.Write(RandomSeed3);
                w.Write(RandomSeed4);
            }
            else
            {
                w.Write(RandomSeed1);
                w.Write(RandomSeed2);
            }
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            DistantLightsXml.ValueTag(sb, indent, "CenterX", CenterX.ToString());
            DistantLightsXml.ValueTag(sb, indent, "CenterY", CenterY.ToString());
            DistantLightsXml.ValueTag(sb, indent, "CenterZ", CenterZ.ToString());
            DistantLightsXml.ValueTag(sb, indent, "Radius", Radius.ToString());
            DistantLightsXml.ValueTag(sb, indent, "PointOffset", PointOffset.ToString());
            DistantLightsXml.ValueTag(sb, indent, "PointCount", PointCount.ToString());
            DistantLightsXml.ValueTag(sb, indent, "Flags", Flags.ToString());
            DistantLightsXml.ValueTag(sb, indent, "DisplayProperties", DisplayProperties.ToString());
            DistantLightsXml.ValueTag(sb, indent, "DistanceOffset", FloatUtil.ToString(DistanceOffset));
            DistantLightsXml.ValueTag(sb, indent, "RandomSeed1", RandomSeed1.ToString());
            DistantLightsXml.ValueTag(sb, indent, "RandomSeed2", RandomSeed2.ToString());
            DistantLightsXml.ValueTag(sb, indent, "RandomSeed3", RandomSeed3.ToString());
            DistantLightsXml.ValueTag(sb, indent, "RandomSeed4", RandomSeed4.ToString());
        }

        public override void ReadXml(XmlNode node)
        {
            CenterX = (short)Xml.GetChildUIntAttribute(node, "CenterX", "value");
            CenterY = (short)Xml.GetChildUIntAttribute(node, "CenterY", "value");
            CenterZ = (short)Xml.GetChildUIntAttribute(node, "CenterZ", "value");
            Radius = (short)Xml.GetChildUIntAttribute(node, "Radius", "value");
            PointOffset = (ushort)Xml.GetChildUIntAttribute(node, "PointOffset", "value");
            PointCount = (ushort)Xml.GetChildUIntAttribute(node, "PointCount", "value");
            Flags = (ushort)Xml.GetChildUIntAttribute(node, "Flags", "value");
            DisplayProperties = (ushort)Xml.GetChildUIntAttribute(node, "DisplayProperties", "value");
            DistanceOffset = Xml.GetChildFloatAttribute(node, "DistanceOffset", "value");
            RandomSeed1 = (byte)Xml.GetChildUIntAttribute(node, "RandomSeed1", "value");
            RandomSeed2 = (byte)Xml.GetChildUIntAttribute(node, "RandomSeed2", "value");
            RandomSeed3 = (byte)Xml.GetChildUIntAttribute(node, "RandomSeed3", "value");
            RandomSeed4 = (byte)Xml.GetChildUIntAttribute(node, "RandomSeed4", "value");

        }

        public override string ToString()
        {
            return CenterX.ToString() + ", " + CenterY.ToString() + ", " + CenterZ.ToString() + ", " + Radius.ToString() + ", " +
                PointOffset.ToString() + ", " + PointCount.ToString() + ", " + Flags.ToString() + ", " + DisplayProperties.ToString() + ", " +
                FloatUtil.ToString(DistanceOffset) + ", " + RandomSeed1.ToString() + ", " + RandomSeed2.ToString() + ", " + RandomSeed3.ToString() + ", " + RandomSeed4.ToString();
        }
    }

    [TC(typeof(EXP))] public class DistantLightsCell : DistantLightsBase
    {
        public uint Index { get; set; }
        public uint CellX { get; set; }
        public uint CellY { get; set; }
        public Vector2 CellMin { get; set; }
        public Vector2 CellMax { get; set; }
        public DistantLightsGroup[] Paths1 { get; set; }
        public DistantLightsGroup[] Paths2 { get; set; }

        public override string ToString()
        {
            return Index.ToString() + " (" + CellX.ToString() + ", " + CellY.ToString() + ") - " +
                (Paths1?.Length ?? 0).ToString() + ", " + (Paths2?.Length ?? 0).ToString() + " - (" +
                CellMin.ToString() + " - " + CellMax.ToString() + ")";
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            DistantLightsXml.ValueTag(sb, indent, "Index", Index.ToString());
            DistantLightsXml.ValueTag(sb, indent, "CellX", CellX.ToString());
            DistantLightsXml.ValueTag(sb, indent, "CellY", CellY.ToString());
            DistantLightsXml.SelfClosingTag(sb, indent, "CellMin " + FloatUtil.GetVector2XmlString(CellMin));
            DistantLightsXml.SelfClosingTag(sb, indent, "CellMax " + FloatUtil.GetVector2XmlString(CellMax));
            DistantLightsXml.WriteItemArray(sb, Paths1, indent, "Paths1");
            DistantLightsXml.WriteItemArray(sb, Paths2, indent, "Paths2");
        }
        public override void ReadXml(XmlNode node)
        {
            Index = Xml.GetChildUIntAttribute(node, "Index", "value");
            CellX = Xml.GetChildUIntAttribute(node, "CellX", "value");
            CellY = Xml.GetChildUIntAttribute(node, "CellY", "value");
            CellMin = Xml.GetChildVector2Attributes(node, "CellMin", "value");
            CellMax = Xml.GetChildVector2Attributes(node, "CellMax", "value");
            Paths1 = XmlMeta.ReadItemArrayNullable<DistantLightsGroup>(node, "Paths1");
            Paths2 = XmlMeta.ReadItemArrayNullable<DistantLightsGroup>(node, "Paths2");
        }
    }

    public class DistantLightsXml : MetaXmlBase
    {

        public static string GetXml(DistantLightsFile dlf)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(XmlHeader);

            if (dlf != null)
            {
                var name = "DistantLights";

                OpenTag(sb, 0, name);

                dlf.WriteXml(sb, 1);

                CloseTag(sb, 0, name);
            }

            return sb.ToString();
        }
    }

    public class XmlDistantLights
    {

        public static DistantLightsFile GetDistantLights(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            return GetDistantLights(doc);
        }

        public static DistantLightsFile GetDistantLights(XmlDocument doc)
        {
            DistantLightsFile dlf = new DistantLightsFile();
            dlf.ReadXml(doc.DocumentElement);
            return dlf;
        }


    }

}
