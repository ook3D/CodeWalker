/*
    Copyright(c) 2015 Neodymium

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

//shamelessly stolen and mangled


using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using System.Text;
using System.Threading.Tasks;
using CodeWalker.World;

using TC = System.ComponentModel.TypeConverterAttribute;
using EXP = System.ComponentModel.ExpandableObjectConverter;

namespace CodeWalker.GameFiles
{


    [TC(typeof(EXP))] public class BoundsDictionary : ResourceFileBase
    {
        // pgDictionary<phBound>
        public override long BlockLength => 0x40;
        public int ReferenceCount { get; set; } = 1;
        public ResourceSimpleList64_uint Codes { get; set; } = new();
        public ResourcePointerList64<Bounds> Entries { get; set; } = new();

        [Browsable(false)] public ResourceSimpleList64_uint BoundNameHashes
        {
            get => Codes;
            set => Codes = value ?? new();
        }

        [Browsable(false)] public ResourcePointerList64<Bounds> Bounds
        {
            get => Entries;
            set => Entries = value ?? new();
        }

        /// <summary>
        /// Reads the data-block from a stream.
        /// </summary>
        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            _ = reader.ReadUInt64(); // m_Parent is ignored in resources.
            ReferenceCount = reader.ReadInt32();
            _ = reader.ReadUInt32();
            Codes = reader.ReadRequiredBlock<ResourceSimpleList64_uint>();
            Entries = reader.ReadRequiredBlock<ResourcePointerList64<Bounds>>();
            ValidateCounts();
        }

        /// <summary>
        /// Writes the data-block to a stream.
        /// </summary>
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            ValidateCounts();
            base.Write(writer, parameters);
            writer.Write(0ul);
            writer.Write(ReferenceCount);
            writer.Write(0u);
            writer.WriteBlock(Codes);
            writer.WriteBlock(Entries);
        }

        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            return
            [
                new Tuple<long, IResourceBlock>(0x20, Codes),
                new Tuple<long, IResourceBlock>(0x30, Entries),
            ];
        }

        public Bounds? Lookup(uint hash)
        {
            var codes = Codes.data_items;
            var entries = Entries.data_items;
            var index = Array.BinarySearch(codes, hash);
            return index >= 0 && index < entries.Length ? entries[index] : null;
        }

        private void ValidateCounts()
        {
            if (Codes.data_items.Length != Entries.data_items.Length)
                throw new InvalidDataException("Bounds dictionary code and entry counts do not match.");
        }
    }

    public enum BoundsType : byte
    {
        None = 255, //not contained in files, but used as a placeholder in XML conversion
        Sphere = 0,
        Capsule = 1,
        Box = 3,
        Geometry = 4,
        GeometryBVH = 8,
        Composite = 10,
        Disc = 12,
        Cylinder = 13,
        Plane = 15,
        [Obsolete("Use Plane. Resource bound type 15 is phBoundPlane.")]
        Cloth = Plane,
    }

    [Flags]
    public enum BoundFlags : byte
    {
        None = 0,
        OctantMapIndexIsUInt16 = 1 << 0,
        HasOctantMap = 1 << 1,
        ForceCcd = 1 << 2,
        UseNewBackFaceCull = 1 << 3,
        UseProjectionEdgeFiltering = 1 << 4,
        UseCurrentInstanceMatrixOnly = 1 << 5,
    }

    [TC(typeof(EXP))] public class Bounds : ResourceFileBase, IResourceXXSystemBlock
    {
        // phBound
        public override long BlockLength => 0x70;
        public BoundsType Type { get; set; }
        public BoundFlags Flags { get; set; }
        public ushort PartIndex { get; set; }
        public float RadiusAroundCentroid { get; set; }
        public Vector3 BoundingBoxMax { get; set; }
        public float Margin { get; set; }
        public Vector3 BoundingBoxMin { get; set; }
        public int ReferenceCount { get; set; } = 1;
        public Vector3 CentroidOffset { get; set; }
        public BoundMaterial_s Material { get; set; }
        public Vector3 CenterOfGravityOffset { get; set; }
        public Vector3 Inertia { get; set; }
        public float Volume { get; set; }

        public byte MaterialIndex
        {
            get => Material.Type;
            set { var material = Material; material.Type = value; Material = material; }
        }
        public byte ProceduralId
        {
            get => Material.ProceduralId;
            set { var material = Material; material.ProceduralId = value; Material = material; }
        }
        public byte RoomId
        {
            get => Material.RoomId;
            set { var material = Material; material.RoomId = value; Material = material; }
        }
        public byte PedDensity
        {
            get => Material.PedDensity;
            set { var material = Material; material.PedDensity = value; Material = material; }
        }
        public EBoundMaterialFlags MaterialFlags
        {
            get => Material.Flags;
            set { var material = Material; material.Flags = value; Material = material; }
        }
        public byte MaterialColorIndex
        {
            get => Material.MaterialColorIndex;
            set { var material = Material; material.MaterialColorIndex = value; Material = material; }
        }
        public ushort MaterialUnknown
        {
            get => Material.Unk4;
            set { var material = Material; material.Unk4 = value; Material = material; }
        }

        [Browsable(false)] public float SphereRadius { get => RadiusAroundCentroid; set => RadiusAroundCentroid = value; }
        [Browsable(false)] public Vector3 BoxMax { get => BoundingBoxMax; set => BoundingBoxMax = value; }
        [Browsable(false)] public Vector3 BoxMin { get => BoundingBoxMin; set => BoundingBoxMin = value; }
        [Browsable(false)] public Vector3 BoxCenter { get => CentroidOffset; set => CentroidOffset = value; }
        [Browsable(false)] public Vector3 SphereCenter { get => CenterOfGravityOffset; set => CenterOfGravityOffset = value; }
        [Browsable(false)] public byte UnkFlags
        {
            get => (byte)MaterialFlags;
            set => MaterialFlags = (EBoundMaterialFlags)(((ushort)MaterialFlags & 0xFF00) | value);
        }
        [Browsable(false)] public byte PolyFlags
        {
            get => (byte)((ushort)MaterialFlags >> 8);
            set => MaterialFlags = (EBoundMaterialFlags)(((ushort)MaterialFlags & 0x00FF) | (value << 8));
        }

        public bool HasChanged { get; set; } = false;
        public BoundComposite? Parent { get; set; }
        public YbnFile? OwnerYbn { get; set; }
        public object? Owner { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public bool OwnerIsFragment
        {
            get
            {
                return ((Owner is FragPhysicsLOD) || (Owner is FragPhysArchetype) || (Owner is FragDrawable));
            }
        }
        public string GetName()
        {
            string n = OwnerName;
            var p = Parent;
            while (p != null)
            {
                n = p.OwnerName;
                p = p.Parent;
            }
            return n;
        }
        public string GetTitle()
        {
            var n = GetName();
            var t = Type.ToString();
            return t + ": " + n;
        }
        public YbnFile? GetRootYbn()
        {
            var r = OwnerYbn;
            var p = Parent;
            while ((p != null) && (r == null))
            {
                r = p.OwnerYbn;
                p = p.Parent;
            }
            return r;
        }
        public object? GetRootOwner()
        {
            var r = Owner;
            var p = Parent;
            while ((p != null) && (r == null))
            {
                r = p.Owner;
                p = p.Parent;
            }
            return r;
        }

        public Matrix Transform { get; set; } = Matrix.Identity; //when it's the child of a bound composite
        public Matrix TransformInv { get; set; } = Matrix.Identity;

        public BoundCompositeChildrenFlags CompositeFlags1 { get; set; }
        public BoundCompositeChildrenFlags CompositeFlags2 { get; set; }


        public virtual Vector3 Scale
        {
            get
            {
                return Transform.ScaleVector;
            }
            set
            {
                var m = Transform;
                m.ScaleVector = value;
                Transform = m;
                TransformInv = Matrix.Invert(m);
            }
        }
        public virtual Vector3 Position
        {
            get
            {
                return Transform.TranslationVector;
            }
            set
            {
                var m = Transform;
                m.TranslationVector = value;
                Transform = m;
                TransformInv = Matrix.Invert(m);
            }
        }
        public virtual Quaternion Orientation
        {
            get
            {
                return Transform.ToQuaternion();
            }
            set
            {
                var m = value.ToMatrix();
                m.TranslationVector = Transform.TranslationVector;
                m.ScaleVector = Transform.ScaleVector;
                Transform = m;
                TransformInv = Matrix.Invert(m);
            }
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            Type = (BoundsType)reader.ReadByte();
            Flags = (BoundFlags)reader.ReadByte();
            PartIndex = reader.ReadUInt16();
            RadiusAroundCentroid = reader.ReadSingle();
            _ = reader.ReadUInt64();
            BoundingBoxMax = reader.ReadVector3();
            Margin = reader.ReadSingle();
            BoundingBoxMin = reader.ReadVector3();
            ReferenceCount = reader.ReadInt32();
            CentroidOffset = reader.ReadVector3();
            Material = new BoundMaterial_s { Data1 = reader.ReadUInt32() };
            CenterOfGravityOffset = reader.ReadVector3();
            var material = Material;
            material.Data2 = reader.ReadUInt32();
            Material = material;
            Inertia = reader.ReadVector3();
            Volume = reader.ReadSingle();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            writer.Write((byte)Type);
            writer.Write((byte)Flags);
            writer.Write(PartIndex);
            writer.Write(RadiusAroundCentroid);
            writer.Write(0ul);
            writer.Write(BoundingBoxMax);
            writer.Write(Margin);
            writer.Write(BoundingBoxMin);
            writer.Write(ReferenceCount);
            writer.Write(CentroidOffset);
            writer.Write(Material.Data1);
            writer.Write(CenterOfGravityOffset);
            writer.Write(Material.Data2);
            writer.Write(Inertia);
            writer.Write(Volume);
        }
        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            YbnXml.SelfClosingTag(sb, indent, "BoxMin " + FloatUtil.GetVector3XmlString(BoxMin));
            YbnXml.SelfClosingTag(sb, indent, "BoxMax " + FloatUtil.GetVector3XmlString(BoxMax));
            YbnXml.SelfClosingTag(sb, indent, "BoxCenter " + FloatUtil.GetVector3XmlString(BoxCenter));
            YbnXml.SelfClosingTag(sb, indent, "SphereCenter " + FloatUtil.GetVector3XmlString(SphereCenter));
            YbnXml.ValueTag(sb, indent, "SphereRadius", FloatUtil.ToString(SphereRadius));
            YbnXml.ValueTag(sb, indent, "Margin", FloatUtil.ToString(Margin));
            YbnXml.ValueTag(sb, indent, "Volume", FloatUtil.ToString(Volume));
            YbnXml.SelfClosingTag(sb, indent, "Inertia " + FloatUtil.GetVector3XmlString(Inertia));
            YbnXml.StringTag(sb, indent, "Flags", Flags.ToString());
            YbnXml.ValueTag(sb, indent, "PartIndex", PartIndex.ToString());
            YbnXml.ValueTag(sb, indent, "MaterialIndex", MaterialIndex.ToString());
            YbnXml.ValueTag(sb, indent, "MaterialColourIndex", MaterialColorIndex.ToString());
            YbnXml.ValueTag(sb, indent, "ProceduralID", ProceduralId.ToString());
            YbnXml.ValueTag(sb, indent, "RoomID", RoomId.ToString());
            YbnXml.ValueTag(sb, indent, "PedDensity", PedDensity.ToString());
            YbnXml.ValueTag(sb, indent, "UnkFlags", UnkFlags.ToString());
            YbnXml.ValueTag(sb, indent, "PolyFlags", PolyFlags.ToString());
            YbnXml.ValueTag(sb, indent, "MaterialUnknown", MaterialUnknown.ToString());
            YbnXml.ValueTag(sb, indent, "ReferenceCount", ReferenceCount.ToString());
            if (Parent != null)
            {
                YbnXml.WriteRawArray(sb, Transform.ToArray(), indent, "CompositeTransform", "", FloatUtil.ToString, 4);
                if (!Parent.OwnerIsFragment)
                {
                    YbnXml.StringTag(sb, indent, "CompositeFlags1", CompositeFlags1.Flags1.ToString());
                    YbnXml.StringTag(sb, indent, "CompositeFlags2", CompositeFlags1.Flags2.ToString());
                }
            }
        }
        public virtual void ReadXml(XmlNode node)
        {
            BoxMin = Xml.GetChildVector3Attributes(node, "BoxMin");
            BoxMax = Xml.GetChildVector3Attributes(node, "BoxMax");
            BoxCenter = Xml.GetChildVector3Attributes(node, "BoxCenter");
            SphereCenter = Xml.GetChildVector3Attributes(node, "SphereCenter");
            SphereRadius = Xml.GetChildFloatAttribute(node, "SphereRadius", "value");
            Margin = Xml.GetChildFloatAttribute(node, "Margin", "value");
            Volume = Xml.GetChildFloatAttribute(node, "Volume", "value");
            Inertia = Xml.GetChildVector3Attributes(node, "Inertia");
            Flags = Xml.GetChildEnumInnerText<BoundFlags>(node, "Flags");
            PartIndex = (ushort)Xml.GetChildUIntAttribute(node, "PartIndex", "value");
            MaterialIndex = (byte)Xml.GetChildUIntAttribute(node, "MaterialIndex", "value");
            MaterialColorIndex = (byte)Xml.GetChildUIntAttribute(node, "MaterialColourIndex", "value");
            ProceduralId = (byte)Xml.GetChildUIntAttribute(node, "ProceduralID", "value");
            RoomId = (byte)Xml.GetChildUIntAttribute(node, "RoomID", "value");
            PedDensity = (byte)Xml.GetChildUIntAttribute(node, "PedDensity", "value");
            UnkFlags = (byte)Xml.GetChildUIntAttribute(node, "UnkFlags", "value");
            PolyFlags = (byte)Xml.GetChildUIntAttribute(node, "PolyFlags", "value");
            MaterialUnknown = (ushort)Xml.GetChildUIntAttribute(node, "MaterialUnknown", "value");
            var referenceCountNode = node.SelectSingleNode("ReferenceCount");
            var legacyReferenceCountNode = node.SelectSingleNode("UnkType");
            if (referenceCountNode != null)
                ReferenceCount = Xml.GetIntAttribute(referenceCountNode, "value");
            else if (legacyReferenceCountNode != null)
                ReferenceCount = Xml.GetIntAttribute(legacyReferenceCountNode, "value");
            if (Parent != null)
            {
                Transform = new Matrix(Xml.GetChildRawFloatArray(node, "CompositeTransform"));
                TransformInv = Matrix.Invert(Transform);
                if (!Parent.OwnerIsFragment)
                {
                    var f = new BoundCompositeChildrenFlags();
                    f.Flags1 = Xml.GetChildEnumInnerText<EBoundCompositeFlags>(node, "CompositeFlags1");
                    f.Flags2 = Xml.GetChildEnumInnerText<EBoundCompositeFlags>(node, "CompositeFlags2");
                    CompositeFlags1 = f;
                    CompositeFlags2 = f;
                }
            }
        }
        public static void WriteXmlNode(Bounds b, StringBuilder sb, int indent, string name = "Bounds")
        {
            if (b == null)
            {
                YbnXml.SelfClosingTag(sb, indent, name + " type=\"" + BoundsType.None.ToString() + "\"");
            }
            else
            {
                YbnXml.OpenTag(sb, indent, name + " type=\"" + b.Type.ToString() + "\"");
                b.WriteXml(sb, indent + 1);
                YbnXml.CloseTag(sb, indent, name);
            }
        }
        public static Bounds? ReadXmlNode(XmlNode? node, object? owner = null, BoundComposite? parent = null)
        {
            if (node == null) return null;
            var typestr = Xml.GetStringAttribute(node, "type");
            var type = Xml.GetEnumValue<BoundsType>(typestr);
            var b = Create(type);
            if (b != null)
            {
                b.Type = type;
                b.Owner = owner;
                b.Parent = parent;
                b.ReadXml(node);
            }
            return b;
        }

        public IResourceSystemBlock GetType(ResourceDataReader reader, params object[] parameters)
        {
            reader.Position += 16;
            var type = (BoundsType)reader.ReadByte();
            reader.Position -= 17;
            return Create(type) ?? throw new InvalidDataException($"Unsupported bounds type: {type}.");
        }
        public static Bounds? Create(BoundsType type)
        {
            switch (type)
            {
                case BoundsType.None: return null;
                case BoundsType.Sphere: return new BoundSphere();
                case BoundsType.Capsule: return new BoundCapsule();
                case BoundsType.Box: return new BoundBox();
                case BoundsType.Geometry: return new BoundGeometry();
                case BoundsType.GeometryBVH: return new BoundBVH();
                case BoundsType.Composite: return new BoundComposite();
                case BoundsType.Disc: return new BoundDisc();
                case BoundsType.Cylinder: return new BoundCylinder();
                case BoundsType.Plane: return new BoundPlane();
                default: return null; // throw new Exception("Unknown bound type");
            }
        }

        public virtual void CopyFrom(Bounds other)
        {
            if (other == null) return;
            Flags = other.Flags;
            PartIndex = other.PartIndex;
            RadiusAroundCentroid = other.RadiusAroundCentroid;
            CenterOfGravityOffset = other.CenterOfGravityOffset;
            CentroidOffset = other.CentroidOffset;
            BoundingBoxMin = other.BoundingBoxMin;
            BoundingBoxMax = other.BoundingBoxMax;
            Margin = other.Margin;
            ReferenceCount = other.ReferenceCount;
            Material = other.Material;
            Inertia = other.Inertia;
            Volume = other.Volume;
        }

        public virtual SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            return new SpaceSphereIntersectResult();
        }
        public virtual SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            return new SpaceRayIntersectResult();
        }

    }
    [TC(typeof(EXP))] public class BoundSphere : Bounds
    {
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            FileVFT = 1080221960;
        }
        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult();
            var bsph = new BoundingSphere();
            bsph.Center = SphereCenter;
            bsph.Radius = SphereRadius;
            if (sph.Intersects(ref bsph))
            {
                res.Hit = true;
                res.Normal = Vector3.Normalize(sph.Center - SphereCenter);
            }
            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var bsph = new BoundingSphere();
            bsph.Center = SphereCenter;
            bsph.Radius = SphereRadius;
            float testdist;
            if (ray.Intersects(ref bsph, out testdist) && (testdist < maxdist))
            {
                res.Hit = true;
                res.HitDist = testdist;
                res.HitBounds = this;
                res.Position = ray.Position + ray.Direction * testdist;
                res.Normal = Vector3.Normalize(res.Position - SphereCenter);
                res.Material.Type = MaterialIndex;
            }
            return res;
        }
    }
    [TC(typeof(EXP))] public class BoundCapsule : Bounds
    {
        // phBoundCapsule
        public override long BlockLength => 0x80;
        public float CapsuleHalfHeight { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            CapsuleHalfHeight = reader.ReadSingle();
            _ = reader.ReadBytes(12);
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(CapsuleHalfHeight);
            writer.Write(new byte[12]);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YbnXml.ValueTag(sb, indent, "CapsuleHalfHeight", FloatUtil.ToString(CapsuleHalfHeight));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            CapsuleHalfHeight = Xml.GetChildFloatAttribute(node, "CapsuleHalfHeight", "value");
            FileVFT = 1080213112;
        }

        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult();
            var bcap = new BoundingCapsule();
            var extent = new Vector3(0, SphereRadius - Margin, 0);
            bcap.PointA = SphereCenter - extent;
            bcap.PointB = SphereCenter + extent;
            bcap.Radius = Margin;
            if (sph.Intersects(ref bcap, out res.Normal))
            {
                res.Hit = true;
            }
            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var bcap = new BoundingCapsule();
            var extent = new Vector3(0, SphereRadius - Margin, 0);
            bcap.PointA = SphereCenter - extent;
            bcap.PointB = SphereCenter + extent;
            bcap.Radius = Margin;
            float testdist;
            if (ray.Intersects(ref bcap, out testdist) && (testdist < maxdist))
            {
                res.Hit = true;
                res.HitDist = testdist;
                res.HitBounds = this;
                res.Position = ray.Position + ray.Direction * testdist;
                res.Normal = bcap.Normal(ref res.Position);
                res.Material.Type = MaterialIndex;
            }
            return res;
        }
    }
    [TC(typeof(EXP))] public class BoundBox : Bounds
    {
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            FileVFT = 1080221016;
        }
        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult();
            var bbox = new BoundingBox(BoxMin, BoxMax);
            if (sph.Intersects(ref bbox))
            {
                var sphmin = sph.Center - sph.Radius;
                var sphmax = sph.Center + sph.Radius;
                var n = Vector3.Zero;
                float eps = sph.Radius * 0.8f;
                if (Math.Abs(sphmax.X - BoxMin.X) < eps) n -= Vector3.UnitX;
                else if (Math.Abs(sphmin.X - BoxMax.X) < eps) n += Vector3.UnitX;
                else if (Math.Abs(sphmax.Y - BoxMin.Y) < eps) n -= Vector3.UnitY;
                else if (Math.Abs(sphmin.Y - BoxMax.Y) < eps) n += Vector3.UnitY;
                else if (Math.Abs(sphmax.Z - BoxMin.Z) < eps) n -= Vector3.UnitZ;
                else if (Math.Abs(sphmin.Z - BoxMax.Z) < eps) n += Vector3.UnitZ;
                else
                { n = Vector3.UnitZ; } //ray starts inside the box...
                res.Normal = Vector3.Normalize(n);
                res.Hit = true;
            }
            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var bbox = new BoundingBox(BoxMin, BoxMax);
            float testdist;
            if (ray.Intersects(ref bbox, out testdist) && (testdist < maxdist))
            {
                const float eps = 0.002f;
                var n = Vector3.Zero;
                var hpt = ray.Position + ray.Direction * testdist;
                if (Math.Abs(hpt.X - BoxMin.X) < eps) n = -Vector3.UnitX;
                else if (Math.Abs(hpt.X - BoxMax.X) < eps) n = Vector3.UnitX;
                else if (Math.Abs(hpt.Y - BoxMin.Y) < eps) n = -Vector3.UnitY;
                else if (Math.Abs(hpt.Y - BoxMax.Y) < eps) n = Vector3.UnitY;
                else if (Math.Abs(hpt.Z - BoxMin.Z) < eps) n = -Vector3.UnitZ;
                else if (Math.Abs(hpt.Z - BoxMax.Z) < eps) n = Vector3.UnitZ;
                else
                { n = Vector3.UnitZ; } //ray starts inside the box...
                res.Hit = true;
                res.HitDist = testdist;
                res.HitBounds = this;
                res.Position = hpt;
                res.Normal = n;
                res.Material.Type = MaterialIndex;
            }
            return res;
        }
    }
    [TC(typeof(EXP))] public class BoundDisc : Bounds
    {
        public override long BlockLength => 0x80;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            _ = reader.ReadBytes(16); // phBoundDisc::m_Pad
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(new byte[16]);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            FileVFT = 1080229960;
        }

        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            //as a temporary hack, just use the sphere-sphere intersection //TODO: sphere-disc intersection
            var res = new SpaceSphereIntersectResult();
            var bsph = new BoundingSphere();
            bsph.Center = SphereCenter;
            bsph.Radius = SphereRadius;
            if (sph.Intersects(ref bsph))
            {
                res.Hit = true;
                res.Normal = Vector3.Normalize(sph.Center - SphereCenter);
            }
            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var bcyl = new BoundingCylinder();
            var size = new Vector3(Margin, 0, 0);
            bcyl.PointA = SphereCenter - size;
            bcyl.PointB = SphereCenter + size;
            bcyl.Radius = SphereRadius;
            Vector3 n;
            float testdist;
            if (ray.Intersects(ref bcyl, out testdist, out n) && (testdist < maxdist))
            {
                res.Hit = true;
                res.HitDist = testdist;
                res.HitBounds = this;
                res.Position = ray.Position + ray.Direction * testdist;
                res.Normal = n;
                res.Material.Type = MaterialIndex;
            }
            return res;
        }
    }
    [TC(typeof(EXP))] public class BoundCylinder : Bounds
    {
        public override long BlockLength => 0x80;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            _ = reader.ReadBytes(16); // phBoundCylinder::m_Pad
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(new byte[16]);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            FileVFT = 1080202872;
        }

        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            //as a temporary hack, just use the sphere-capsule intersection //TODO: sphere-cylinder intersection
            var res = new SpaceSphereIntersectResult();
            var bcap = new BoundingCapsule();
            var extent = (BoxMax - BoxMin).Abs();
            var size = new Vector3(0, extent.Y * 0.5f, 0);
            bcap.PointA = SphereCenter - size;
            bcap.PointB = SphereCenter + size;
            bcap.Radius = extent.X * 0.5f;
            if (sph.Intersects(ref bcap, out res.Normal))
            {
                res.Hit = true;
            }
            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var bcyl = new BoundingCylinder();
            var extent = (BoxMax - BoxMin).Abs();
            var size = new Vector3(0, extent.Y * 0.5f, 0);
            bcyl.PointA = SphereCenter - size;
            bcyl.PointB = SphereCenter + size;
            bcyl.Radius = extent.X * 0.5f;
            Vector3 n;
            float testdist;
            if (ray.Intersects(ref bcyl, out testdist, out n) && (testdist < maxdist))
            {
                res.Hit = true;
                res.HitDist = testdist;
                res.HitBounds = this;
                res.Position = ray.Position + ray.Direction * testdist;
                res.Normal = n;
                res.Material.Type = MaterialIndex;
            }
            return res;
        }
    }
    [TC(typeof(EXP))] public class BoundPlane : Bounds
    {
        // phBoundPlane stores its position in CentroidOffset and its normal in CenterOfGravityOffset.
        public Vector3 Normal
        {
            get => CenterOfGravityOffset;
            set => CenterOfGravityOffset = value;
        }

        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult(); //TODO...
            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult(); //TODO...
            return res;
        }
    }

    [Obsolete("Use BoundPlane. Resource bound type 15 is phBoundPlane.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BoundCloth : BoundPlane
    {
    }

    [TC(typeof(EXP))] public class BoundGeometry : Bounds
    {
        // phBoundGeometry (including phBoundPolyhedron)
        public override long BlockLength => 0x130;
        public ulong CompressedShrunkVerticesPointer { get; set; }
        public bool UseActiveComponents { get; set; }
        public bool IsFlat { get; set; }
        public byte VertexAttributeCount { get; set; }
        public ushort ConvexHullVertexCount { get; set; }
        public ulong PolygonsPointer { get; set; }
        public Vector4 UnQuantizeFactor { get; set; }
        public Vector4 BoundingBoxCenter { get; set; }
        public ulong CompressedVerticesPointer { get; set; }
        public ulong VertexAttributesPointer { get; set; }
        public ulong OctantVertexCountsPointer { get; set; }
        public ulong OctantVerticesPointer { get; set; }
        public int VertexCount { get; set; }
        public int PolygonCount { get; set; }
        public ulong MaterialIdsPointer { get; set; }
        public ulong MaterialColorsPointer { get; set; }
        public ulong SecondSurfaceVertexDisplacementsPointer { get; set; }
        public int SecondSurfaceVertexDisplacementCount { get; set; }
        public ulong PolygonMaterialIndicesPointer { get; set; }
        public byte MaterialCount { get; set; }
        public byte MaterialColorCount { get; set; }


        public Vector3[]? VerticesShrunk { get; set; } // Vertices but shrunk by margin along normal
        public BoundPolygon[] Polygons { get; set; } = [];
        public Vector3[] Vertices { get; set; } = [];
        public BoundMaterialColour[]? VertexColours { get; set; }//not sure, it seems like colours anyway, see eg. prologue03_10.ybn
        public BoundGeomOctants? Octants { get; set; }
        public BoundMaterial_s[] Materials { get; set; } = [];
        public BoundMaterialColour[]? MaterialColours { get; set; }
        public byte[] PolygonMaterialIndices { get; set; } = [];
        public float[]? SecondSurfaceVertexDisplacements { get; set; }

        [Browsable(false)] public Vector3 Quantum
        {
            get => UnQuantizeFactor.XYZ();
            set => UnQuantizeFactor = new Vector4(value, UnQuantizeFactor.W);
        }
        [Browsable(false)] public Vector3 CenterGeom
        {
            get => BoundingBoxCenter.XYZ();
            set => BoundingBoxCenter = new Vector4(value, BoundingBoxCenter.W);
        }
        private ResourceSystemStructBlock<BoundVertex_s>? VerticesShrunkBlock;
        private ResourceSystemDataBlock? PolygonsBlock;
        private ResourceSystemStructBlock<BoundVertex_s>? VerticesBlock;
        private ResourceSystemStructBlock<BoundMaterialColour>? VertexColoursBlock;
        private ResourceSystemStructBlock<BoundMaterial_s>? MaterialsBlock;
        private ResourceSystemStructBlock<BoundMaterialColour>? MaterialColoursBlock;
        private ResourceSystemStructBlock<byte>? PolygonMaterialIndicesBlock;
        private ResourceSystemStructBlock<float>? SecondSurfaceVertexDisplacementsBlock;

        private BoundVertex?[]? VertexObjects; //for use by the editor, created as needed by GetVertexObject()


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            _ = reader.ReadUInt64(); // m_VerticesPad and alignment padding.
            CompressedShrunkVerticesPointer = reader.ReadUInt64();
            UseActiveComponents = reader.ReadByte() != 0;
            IsFlat = reader.ReadByte() != 0;
            VertexAttributeCount = reader.ReadByte();
            _ = reader.ReadByte();
            ConvexHullVertexCount = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            PolygonsPointer = reader.ReadUInt64();
            UnQuantizeFactor = reader.ReadVector4();
            BoundingBoxCenter = reader.ReadVector4();
            CompressedVerticesPointer = reader.ReadUInt64();
            VertexAttributesPointer = reader.ReadUInt64();
            OctantVertexCountsPointer = reader.ReadUInt64();
            OctantVerticesPointer = reader.ReadUInt64();
            VertexCount = reader.ReadInt32();
            PolygonCount = reader.ReadInt32();
            _ = reader.ReadBytes(24); // phBoundPolyhedron::m_Pad0
            MaterialIdsPointer = reader.ReadUInt64();
            MaterialColorsPointer = reader.ReadUInt64();
            _ = reader.ReadUInt64(); // phBoundGeometry::pad1 and alignment padding.
            SecondSurfaceVertexDisplacementsPointer = reader.ReadUInt64();
            SecondSurfaceVertexDisplacementCount = reader.ReadInt32();
            _ = reader.ReadUInt32();
            PolygonMaterialIndicesPointer = reader.ReadUInt64();
            MaterialCount = reader.ReadByte();
            MaterialColorCount = reader.ReadByte();
            _ = reader.ReadBytes(14); // phBoundGeometry::pad

            ValidateGeometryCounts();
            var vertsShrunk = reader.ReadStructsAt<BoundVertex_s>(CompressedShrunkVerticesPointer, (uint)VertexCount);
            if (vertsShrunk != null) //seems to be in YFT's
            {
                VerticesShrunk = new Vector3[vertsShrunk.Length];
                for (int i = 0; i < vertsShrunk.Length; i++)
                {
                    var bv = vertsShrunk[i];
                    VerticesShrunk[i] = bv.Vector * Quantum;
                }
            }

            ReadPolygons(reader);

            var verts = reader.ReadStructsAt<BoundVertex_s>(CompressedVerticesPointer, (uint)VertexCount);
            if (verts != null)
            {
                Vertices = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    var bv = verts[i];
                    Vertices[i] = bv.Vector * Quantum;
                }
            }

            VertexColours = reader.ReadStructsAt<BoundMaterialColour>(VertexAttributesPointer, (uint)(VertexCount * VertexAttributeCount));

            Octants = reader.ReadBlockAt<BoundGeomOctants>(OctantVertexCountsPointer, OctantVerticesPointer);

            Materials = reader.ReadStructsAt<BoundMaterial_s>(MaterialIdsPointer, (MaterialCount < 4) ? 4u : MaterialCount) ?? [];

            MaterialColours = reader.ReadStructsAt<BoundMaterialColour>(MaterialColorsPointer, MaterialColorCount);

            SecondSurfaceVertexDisplacements = reader.ReadStructsAt<float>(SecondSurfaceVertexDisplacementsPointer, (uint)SecondSurfaceVertexDisplacementCount);

            PolygonMaterialIndices = reader.ReadBytesAt(PolygonMaterialIndicesPointer, (uint)PolygonCount) ?? [];

            if ((MaterialIdsPointer != 0) && (MaterialCount < 4))
            {
                //the read array was padded, so remove the padding from this array. will re-add padding in BuildMaterials...
                var mats = new BoundMaterial_s[MaterialCount];
                for (int i = 0; i < MaterialCount; i++)
                {
                    mats[i] = Materials[i];
                }
                Materials = mats;

            }


        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            CompressedShrunkVerticesPointer = (ulong)(VerticesShrunkBlock?.FilePosition ?? 0);
            PolygonsPointer = (ulong)(PolygonsBlock?.FilePosition ?? 0);
            CompressedVerticesPointer = (ulong)(VerticesBlock?.FilePosition ?? 0);
            VertexAttributesPointer = (ulong)(VertexColoursBlock?.FilePosition ?? 0);
            OctantVertexCountsPointer = (ulong)(Octants?.FilePosition ?? 0);
            OctantVerticesPointer = OctantVertexCountsPointer != 0 ? OctantVertexCountsPointer + 32 : 0;
            VertexCount = VerticesBlock?.ItemCount ?? 0;
            PolygonCount = Polygons?.Length ?? 0;
            MaterialIdsPointer = (ulong)(MaterialsBlock?.FilePosition ?? 0);
            MaterialColorsPointer = (ulong)(MaterialColoursBlock?.FilePosition ?? 0);
            SecondSurfaceVertexDisplacementsPointer = (ulong)(SecondSurfaceVertexDisplacementsBlock?.FilePosition ?? 0);
            SecondSurfaceVertexDisplacementCount = SecondSurfaceVertexDisplacementsBlock?.ItemCount ?? 0;
            PolygonMaterialIndicesPointer = (ulong)(PolygonMaterialIndicesBlock?.FilePosition ?? 0);
            MaterialColorCount = checked((byte)(MaterialColoursBlock?.ItemCount ?? 0));
            VertexAttributeCount = checked((byte)(VertexColours == null || VertexCount == 0 ? 0 : VertexColours.Length / VertexCount));
            ValidateGeometryCounts();


            // write structure data
            writer.Write(0ul);
            writer.Write(CompressedShrunkVerticesPointer);
            writer.Write((byte)(UseActiveComponents ? 1 : 0));
            writer.Write((byte)(IsFlat ? 1 : 0));
            writer.Write(VertexAttributeCount);
            writer.Write((byte)0);
            writer.Write(ConvexHullVertexCount);
            writer.Write((ushort)0);
            writer.Write(PolygonsPointer);
            writer.Write(UnQuantizeFactor);
            writer.Write(BoundingBoxCenter);
            writer.Write(CompressedVerticesPointer);
            writer.Write(VertexAttributesPointer);
            writer.Write(OctantVertexCountsPointer);
            writer.Write(OctantVerticesPointer);
            writer.Write(VertexCount);
            writer.Write(PolygonCount);
            writer.Write(new byte[24]);
            writer.Write(MaterialIdsPointer);
            writer.Write(MaterialColorsPointer);
            writer.Write(0ul);
            writer.Write(SecondSurfaceVertexDisplacementsPointer);
            writer.Write(SecondSurfaceVertexDisplacementCount);
            writer.Write(0u);
            writer.Write(PolygonMaterialIndicesPointer);
            writer.Write(MaterialCount);
            writer.Write(MaterialColorCount);
            writer.Write(new byte[14]);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            // Keep the established YBN XML schema used by Sollumz. The native fields
            // are Vector4 values, but their W components have historically been
            // represented by the two separate unknown-float elements.
            YbnXml.SelfClosingTag(sb, indent, "GeometryCenter " + FloatUtil.GetVector3XmlString(CenterGeom));
            YbnXml.ValueTag(sb, indent, "UnkFloat1", FloatUtil.ToString(UnQuantizeFactor.W));
            YbnXml.ValueTag(sb, indent, "UnkFloat2", FloatUtil.ToString(BoundingBoxCenter.W));
            YbnXml.ValueTag(sb, indent, "UseActiveComponents", UseActiveComponents.ToString().ToLowerInvariant());
            YbnXml.ValueTag(sb, indent, "IsFlat", IsFlat.ToString().ToLowerInvariant());
            YbnXml.ValueTag(sb, indent, "ConvexHullVertexCount", ConvexHullVertexCount.ToString());

            if (Materials != null)
            {
                YbnXml.WriteItemArray(sb, Materials, indent, "Materials");
            }
            if (MaterialColours != null)
            {
                YbnXml.WriteRawArray(sb, MaterialColours, indent, "MaterialColours", "", YbnXml.FormatBoundMaterialColour, 1);
            }
            if (Vertices != null)
            {
                YbnXml.WriteRawArray(sb, Vertices, indent, "Vertices", "", YbnXml.FormatVector3, 1);
            }
            if (VertexColours != null)
            {
                YbnXml.WriteRawArray(sb, VertexColours, indent, "VertexColours", "", YbnXml.FormatBoundMaterialColour, 1);
            }
            if (SecondSurfaceVertexDisplacements != null)
            {
                YbnXml.WriteRawArray(sb, SecondSurfaceVertexDisplacements, indent, "SecondSurfaceVertexDisplacements", "", FloatUtil.ToString, 8);
            }
            if (Polygons != null)
            {
                YbnXml.WriteCustomItemArray(sb, Polygons, indent, "Polygons");
            }
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            var unQuantizeFactorNode = node.SelectSingleNode("UnQuantizeFactor");
            var boundingBoxCenterNode = node.SelectSingleNode("BoundingBoxCenter");
            if (unQuantizeFactorNode != null)
                UnQuantizeFactor = Xml.GetChildVector4Attributes(node, "UnQuantizeFactor");
            else
                UnQuantizeFactor = new Vector4(Quantum, Xml.GetChildFloatAttribute(node, "UnkFloat1", "value"));
            if (boundingBoxCenterNode != null)
                BoundingBoxCenter = Xml.GetChildVector4Attributes(node, "BoundingBoxCenter");
            else
                BoundingBoxCenter = new Vector4(Xml.GetChildVector3Attributes(node, "GeometryCenter"), Xml.GetChildFloatAttribute(node, "UnkFloat2", "value"));
            UseActiveComponents = Xml.GetChildBoolAttribute(node, "UseActiveComponents", "value");
            IsFlat = Xml.GetChildBoolAttribute(node, "IsFlat", "value");
            ConvexHullVertexCount = (ushort)Xml.GetChildUIntAttribute(node, "ConvexHullVertexCount", "value");

            Materials = XmlMeta.ReadItemArray<BoundMaterial_s>(node, "Materials");
            MaterialColours = XmlYbn.GetChildRawBoundMaterialColourArray(node, "MaterialColours");
            Vertices = Xml.GetChildRawVector3ArrayNullable(node, "Vertices") ?? [];
            VertexColours = XmlYbn.GetChildRawBoundMaterialColourArray(node, "VertexColours");
            SecondSurfaceVertexDisplacements = Xml.GetChildRawFloatArrayNullable(node, "SecondSurfaceVertexDisplacements");

            var pnode = node.SelectSingleNode("Polygons");
            if (pnode != null)
            {
                var inodes = pnode.ChildNodes;
                if (inodes?.Count > 0)
                {
                    var polylist = new List<BoundPolygon>();
                    foreach (XmlNode inode in inodes)
                    {
                        if (inode.NodeType != XmlNodeType.Element) continue;
                        var typestr = inode.Name;
                        var type = Xml.GetEnumValue<BoundPolygonType>(typestr);
                        var poly = CreatePolygon(type);
                        if (poly != null)
                        {
                            poly.ReadXml(inode);
                            polylist.Add(poly);
                        }
                    }
                    Polygons = polylist.ToArray();
                }
            }

            BuildMaterials();
            if (unQuantizeFactorNode == null)
                CalculateQuantum();
            UpdateEdgeIndices();
            UpdateTriangleAreas();
            CalculateVertsShrunk();
            CalculateOctants();

            FileVFT = 1080226408;
        }

        public override IResourceBlock[] GetReferences()
        {
            BuildMaterials();
            CalculateQuantum();
            UpdateEdgeIndices();
            UpdateTriangleAreas();
            VertexCount = Vertices?.Length ?? 0;
            PolygonCount = Polygons?.Length ?? 0;
            if (ConvexHullVertexCount == 0 && VertexCount <= ushort.MaxValue)
                ConvexHullVertexCount = (ushort)VertexCount;

            var list = new List<IResourceBlock>(base.GetReferences());
            if (VerticesShrunk != null)
            {
                var verts = new List<BoundVertex_s>();
                foreach (var v in VerticesShrunk)
                {
                    var vq = v / Quantum;
                    var vs = new BoundVertex_s(vq);
                    verts.Add(vs);
                }
                VerticesShrunkBlock = new ResourceSystemStructBlock<BoundVertex_s>(verts.ToArray());
                list.Add(VerticesShrunkBlock);
            }
            if (Polygons != null)
            {
                using MemoryStream ms = new();
                using BinaryWriter bw = new(ms);
                foreach (var poly in Polygons)
                {
                    var start = ms.Position;
                    poly.Write(bw);
                    if (ms.Position - start != BoundPolygon.SerializedSize)
                        throw new InvalidDataException($"{poly.GetType().Name} wrote an invalid primitive size.");
                }
                var polydata = ms.ToArray();
                for (int i = 0; i < Polygons.Length; i++)
                {
                    var offset = i * BoundPolygon.SerializedSize;
                    polydata[offset] = Polygons[i].EncodeType(polydata[offset]);
                }

                PolygonsBlock = new ResourceSystemDataBlock(polydata);
                list.Add(PolygonsBlock);
            }
            if (Vertices != null)
            {
                var verts = new List<BoundVertex_s>();
                foreach (var v in Vertices)
                {
                    var vq = v / Quantum;
                    var vs = new BoundVertex_s(vq);
                    verts.Add(vs);
                }
                VerticesBlock = new ResourceSystemStructBlock<BoundVertex_s>(verts.ToArray());
                list.Add(VerticesBlock);
            }
            if (VertexColours != null)
            {
                VertexColoursBlock = new ResourceSystemStructBlock<BoundMaterialColour>(VertexColours);
                list.Add(VertexColoursBlock);
            }
            if (Octants != null)
            {
                list.Add(Octants);//this one is already a resource block!
            }
            if (Materials != null)
            {
                var mats = Materials;
                if (mats.Length < 4) //add padding to the array for writing if necessary
                {
                    mats = new BoundMaterial_s[4];
                    for (int i = 0; i < Materials.Length; i++)
                    {
                        mats[i] = Materials[i];
                    }
                }

                MaterialsBlock = new ResourceSystemStructBlock<BoundMaterial_s>(mats);
                list.Add(MaterialsBlock);
            }
            if (MaterialColours != null)
            {
                MaterialColoursBlock = new ResourceSystemStructBlock<BoundMaterialColour>(MaterialColours);
                list.Add(MaterialColoursBlock);
            }
            if (PolygonMaterialIndices != null)
            {
                PolygonMaterialIndicesBlock = new ResourceSystemStructBlock<byte>(PolygonMaterialIndices);
                list.Add(PolygonMaterialIndicesBlock);
            }
            if (SecondSurfaceVertexDisplacements != null)
            {
                SecondSurfaceVertexDisplacementsBlock = new ResourceSystemStructBlock<float>(SecondSurfaceVertexDisplacements);
                list.Add(SecondSurfaceVertexDisplacementsBlock);
            }
            return list.ToArray();
        }



        private void ReadPolygons(ResourceDataReader reader)
        {
            if (PolygonCount == 0)
            { return; }

            Polygons = new BoundPolygon[PolygonCount];
            uint polybytecount = checked((uint)PolygonCount * BoundPolygon.SerializedSize);
            var polygonData = reader.ReadBytesAt(PolygonsPointer, polybytecount) ?? [];
            for (int i = 0; i < PolygonCount; i++)
            {
                var offset = i * BoundPolygon.SerializedSize;
                var type = BoundPolygon.DecodeType(polygonData[offset]);
                BoundPolygon p = CreatePolygon(type) ?? throw new InvalidDataException($"Unsupported polygon type: {type}.");
                p.Index = i;
                p.Read(polygonData, offset);
                Polygons[i] = p;
            }
        }

        private void ValidateGeometryCounts()
        {
            if ((uint)VertexCount > 0xFFFE)
                throw new InvalidDataException($"Invalid geometry vertex count: {VertexCount}.");
            if ((uint)PolygonCount > 0xFFFE)
                throw new InvalidDataException($"Invalid geometry polygon count: {PolygonCount}.");
            if (ConvexHullVertexCount > VertexCount)
                throw new InvalidDataException("Convex-hull vertex count exceeds the geometry vertex count.");
            if (SecondSurfaceVertexDisplacementCount != 0 && SecondSurfaceVertexDisplacementCount != VertexCount)
                throw new InvalidDataException("Second-surface displacement count must match the geometry vertex count.");
            if (VertexColours != null && VertexCount != 0 && VertexColours.Length % VertexCount != 0)
                throw new InvalidDataException("Vertex attribute count does not evenly divide the vertex attribute array.");
        }

        public BoundVertex? GetVertexObject(int index)
        {
            //gets a cached object which references a single vertex in this geometry
            if (Vertices == null) return null;
            if ((index < 0) || (index >= Vertices.Length)) return null;
            if ((VertexObjects == null) || (VertexObjects.Length != Vertices.Length))
            {
                VertexObjects = new BoundVertex[Vertices.Length];
            }
            if (index >= VertexObjects.Length) return null;
            var r = VertexObjects[index];
            if (r == null)
            {
                r = new BoundVertex(this, index);
                VertexObjects[index] = r;
            }
            return r;
        }
        public Vector3 GetVertex(int index)
        {
            return ((index >= 0) && (index < Vertices.Length)) ? Vertices[index] : Vector3.Zero;
        }
        public Vector3 GetVertexPos(int index)
        {
            var v = GetVertex(index) + CenterGeom;
            return Vector3.Transform(v, Transform).XYZ();
        }
        public void SetVertexPos(int index, Vector3 v)
        {
            if ((index >= 0) && (index < Vertices.Length))
            {
                var t = Vector3.Transform(v, TransformInv).XYZ() - CenterGeom;
                Vertices[index] = t;
            }
        }
        public BoundMaterialColour GetVertexColour(int index)
        {
            return ((VertexColours != null) && (index >= 0) && (index < VertexColours.Length)) ? VertexColours[index] : new BoundMaterialColour();
        }
        public void SetVertexColour(int index, BoundMaterialColour c)
        {
            if ((VertexColours != null) && (index >= 0) && (index < VertexColours.Length))
            {
                VertexColours[index] = c;
            }
        }




        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult();
            var box = new BoundingBox();

            if (Polygons == null)
            { return res; }

            box.Minimum = BoxMin;
            box.Maximum = BoxMax;
            if (!sph.Intersects(ref box))
            { return res; }

            SphereIntersectPolygons(ref sph, ref res, 0, Polygons.Length);

            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var box = new BoundingBox();

            if (Polygons == null)
            { return res; }

            box.Minimum = BoxMin;
            box.Maximum = BoxMax;
            float bvhboxhittest;
            if (!ray.Intersects(ref box, out bvhboxhittest))
            { return res; }
            if (bvhboxhittest > maxdist)
            { return res; } //already a closer hit.

            res.HitDist = maxdist;

            RayIntersectPolygons(ref ray, ref res, 0, Polygons.Length);

            return res;
        }


        protected void SphereIntersectPolygons(ref BoundingSphere sph, ref SpaceSphereIntersectResult res, int startIndex, int endIndex)
        {
            var box = new BoundingBox();
            var tsph = new BoundingSphere();
            var spht = new BoundingSphere();
            var sp = sph.Center;
            var sr = sph.Radius;
            Vector3 p1, p2, p3, p4, a1, a2, a3;
            Vector3 n1 = Vector3.Zero;

            for (int p = startIndex; p < endIndex; p++)
            {
                var polygon = Polygons[p];
                bool polyhit = false;
                switch (polygon.Type)
                {
                    case BoundPolygonType.Triangle:
                        var ptri = (BoundPolygonTriangle)polygon;
                        p1 = GetVertexPos(ptri.VertexIndex1);
                        p2 = GetVertexPos(ptri.VertexIndex2);
                        p3 = GetVertexPos(ptri.VertexIndex3);
                        polyhit = sph.Intersects(ref p1, ref p2, ref p3);
                        if (polyhit) n1 = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
                        break;
                    case BoundPolygonType.Sphere:
                        var psph = (BoundPolygonSphere)polygon;
                        tsph.Center = GetVertexPos(psph.CenterIndex);
                        tsph.Radius = psph.Radius;
                        polyhit = sph.Intersects(ref tsph);
                        if (polyhit) n1 = Vector3.Normalize(sph.Center - tsph.Center);
                        break;
                    case BoundPolygonType.Capsule:
                        var pcap = (BoundPolygonCapsule)polygon;
                        var tcap = new BoundingCapsule();
                        tcap.PointA = GetVertexPos(pcap.EndIndex0);
                        tcap.PointB = GetVertexPos(pcap.EndIndex1);
                        tcap.Radius = pcap.Radius;
                        polyhit = sph.Intersects(ref tcap, out n1);
                        break;
                    case BoundPolygonType.Box:
                        var pbox = (BoundPolygonBox)polygon;
                        p1 = GetVertexPos(pbox.VertexIndex0);//corner
                        p2 = GetVertexPos(pbox.VertexIndex1);
                        p3 = GetVertexPos(pbox.VertexIndex2);
                        p4 = GetVertexPos(pbox.VertexIndex3);
                        a1 = ((p3 + p4) - (p1 + p2)) * 0.5f;
                        a2 = p3 - (p1 + a1);
                        a3 = p4 - (p1 + a1);
                        Vector3 bs = new(a1.Length(), a2.Length(), a3.Length());
                        Vector3 m1 = a1 / bs.X;
                        Vector3 m2 = a2 / bs.Y;
                        Vector3 m3 = a3 / bs.Z;
                        if ((bs.X < bs.Y) && (bs.X < bs.Z)) m1 = Vector3.Cross(m2, m3);
                        else if (bs.Y < bs.Z) m2 = Vector3.Cross(m3, m1);
                        else m3 = Vector3.Cross(m1, m2);
                        Vector3 tp = sp - (p1);//+cg
                        spht.Center = new Vector3(Vector3.Dot(tp, m1), Vector3.Dot(tp, m2), Vector3.Dot(tp, m3));
                        spht.Radius = sph.Radius;
                        box.Minimum = Vector3.Zero;
                        box.Maximum = bs;
                        polyhit = spht.Intersects(ref box);
                        if (polyhit)
                        {
                            Vector3 smin = spht.Center - spht.Radius;
                            Vector3 smax = spht.Center + spht.Radius;
                            float eps = spht.Radius * 0.8f;
                            n1 = Vector3.Zero;
                            if (Math.Abs(smax.X) < eps) n1 -= m1;
                            else if (Math.Abs(smin.X - bs.X) < eps) n1 += m1;
                            if (Math.Abs(smax.Y) < eps) n1 -= m2;
                            else if (Math.Abs(smin.Y - bs.Y) < eps) n1 += m2;
                            if (Math.Abs(smax.Z) < eps) n1 -= m3;
                            else if (Math.Abs(smin.Z - bs.Z) < eps) n1 += m3;
                            float n1l = n1.Length();
                            if (n1l > 0.0f) n1 = n1 / n1l;
                            else n1 = Vector3.UnitZ;
                        }
                        break;
                    case BoundPolygonType.Cylinder:
                        var pcyl = (BoundPolygonCylinder)polygon;
                        var ttcap = new BoundingCapsule();//just use the capsule intersection for now...
                        ttcap.PointA = GetVertexPos(pcyl.EndIndex0);
                        ttcap.PointB = GetVertexPos(pcyl.EndIndex1);
                        ttcap.Radius = pcyl.Radius;
                        polyhit = sph.Intersects(ref ttcap, out n1);
                        break;
                    default:
                        break;
                }
                if (polyhit)
                {
                    res.HitPolygon = polygon;
                    res.Hit = true;
                    res.Normal = n1;
                }
                res.TestedPolyCount++;
            }
        }
        protected void RayIntersectPolygons(ref Ray ray, ref SpaceRayIntersectResult res, int startIndex, int endIndex)
        {
            if (Polygons == null || Polygons.Length == 0) return;
            if (startIndex < 0) startIndex = 0;
            if (endIndex > Polygons.Length) endIndex = Polygons.Length;
            if (startIndex >= endIndex) return;

            var box = new BoundingBox();
            var tsph = new BoundingSphere();
            var rayt = new Ray();
            var rp = ray.Position;
            var rd = ray.Direction;
            Vector3 p1, p2, p3, p4, a1, a2, a3;
            Vector3 n1 = Vector3.Zero;

            for (int p = startIndex; p < endIndex; p++)
            {
                var polygon = Polygons[p];
                if (polygon == null) continue;
                float polyhittestdist = float.MaxValue;
                bool polyhit = false;
                switch (polygon.Type)
                {
                    case BoundPolygonType.Triangle:
                        var ptri = (BoundPolygonTriangle)polygon;
                        p1 = GetVertexPos(ptri.VertexIndex1);
                        p2 = GetVertexPos(ptri.VertexIndex2);
                        p3 = GetVertexPos(ptri.VertexIndex3);
                        polyhit = ray.Intersects(ref p1, ref p2, ref p3, out polyhittestdist);
                        if (polyhit) n1 = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
                        break;
                    case BoundPolygonType.Sphere:
                        var psph = (BoundPolygonSphere)polygon;
                        tsph.Center = GetVertexPos(psph.CenterIndex);
                        tsph.Radius = psph.Radius;
                        polyhit = ray.Intersects(ref tsph, out polyhittestdist);
                        if (polyhit) n1 = Vector3.Normalize((ray.Position + ray.Direction * polyhittestdist) - tsph.Center);
                        break;
                    case BoundPolygonType.Capsule:
                        var pcap = (BoundPolygonCapsule)polygon;
                        var tcap = new BoundingCapsule();
                        tcap.PointA = GetVertexPos(pcap.EndIndex0);
                        tcap.PointB = GetVertexPos(pcap.EndIndex1);
                        tcap.Radius = pcap.Radius;
                        polyhit = ray.Intersects(ref tcap, out polyhittestdist);
                        res.Position = (ray.Position + ray.Direction * polyhittestdist);
                        if (polyhit) n1 = tcap.Normal(ref res.Position);
                        break;
                    case BoundPolygonType.Box:
                        var pbox = (BoundPolygonBox)polygon;
                        p1 = GetVertexPos(pbox.VertexIndex0);//corner
                        p2 = GetVertexPos(pbox.VertexIndex1);
                        p3 = GetVertexPos(pbox.VertexIndex2);
                        p4 = GetVertexPos(pbox.VertexIndex3);
                        a1 = ((p3 + p4) - (p1 + p2)) * 0.5f;
                        a2 = p3 - (p1 + a1);
                        a3 = p4 - (p1 + a1);
                        Vector3 bs = new(a1.Length(), a2.Length(), a3.Length());
                        Vector3 m1 = a1 / bs.X;
                        Vector3 m2 = a2 / bs.Y;
                        Vector3 m3 = a3 / bs.Z;
                        if ((bs.X < bs.Y) && (bs.X < bs.Z)) m1 = Vector3.Cross(m2, m3);
                        else if (bs.Y < bs.Z) m2 = Vector3.Cross(m3, m1);
                        else m3 = Vector3.Cross(m1, m2);
                        Vector3 tp = rp - (p1);//+cg
                        rayt.Position = new Vector3(Vector3.Dot(tp, m1), Vector3.Dot(tp, m2), Vector3.Dot(tp, m3));
                        rayt.Direction = new Vector3(Vector3.Dot(rd, m1), Vector3.Dot(rd, m2), Vector3.Dot(rd, m3));
                        box.Minimum = Vector3.Zero;
                        box.Maximum = bs;
                        polyhit = rayt.Intersects(ref box, out polyhittestdist);
                        if (polyhit)
                        {
                            Vector3 hpt = rayt.Position + rayt.Direction * polyhittestdist;
                            const float eps = 0.002f;
                            if (Math.Abs(hpt.X) < eps) n1 = -m1;
                            else if (Math.Abs(hpt.X - bs.X) < eps) n1 = m1;
                            else if (Math.Abs(hpt.Y) < eps) n1 = -m2;
                            else if (Math.Abs(hpt.Y - bs.Y) < eps) n1 = m2;
                            else if (Math.Abs(hpt.Z) < eps) n1 = -m3;
                            else if (Math.Abs(hpt.Z - bs.Z) < eps) n1 = m3;
                            else
                            { n1 = Vector3.UnitZ; } //ray starts inside the box...
                        }
                        break;
                    case BoundPolygonType.Cylinder:
                        var pcyl = (BoundPolygonCylinder)polygon;
                        var tcyl = new BoundingCylinder();
                        tcyl.PointA = GetVertexPos(pcyl.EndIndex0);
                        tcyl.PointB = GetVertexPos(pcyl.EndIndex1);
                        tcyl.Radius = pcyl.Radius;
                        polyhit = ray.Intersects(ref tcyl, out polyhittestdist, out n1);
                        break;
                    default:
                        break;
                }
                if (polyhit && (polyhittestdist < res.HitDist))
                {
                    res.HitDist = polyhittestdist;
                    res.Hit = true;
                    res.Position = (ray.Position + ray.Direction * polyhittestdist);
                    res.Normal = n1;
                    res.HitVertex = polygon.NearestVertex(res.Position);
                    res.HitPolygon = polygon;
                    res.HitBounds = this;
                    res.Material = polygon.Material;
                }
                res.TestedPolyCount++;
            }
        }



        public int GetMaterialIndex(int polyIndex)
        {
            var matind = 0;
            if ((PolygonMaterialIndices != null) && (polyIndex < PolygonMaterialIndices.Length))
            {
                matind = PolygonMaterialIndices[polyIndex];
            }
            return matind;
        }
        public BoundMaterial_s GetMaterialByIndex(int matIndex)
        {
            if ((Materials != null) && (matIndex < Materials.Length))
            {
                return Materials[matIndex];
            }
            return new BoundMaterial_s();
        }
        public BoundMaterial_s GetMaterial(int polyIndex)
        {
            var matind = GetMaterialIndex(polyIndex);
            return GetMaterialByIndex(matind);
        }
        public void SetMaterial(int polyIndex, BoundMaterial_s mat)
        {
            //updates the shared material for the given poly.
            var matind = 0;
            if ((PolygonMaterialIndices != null) && (polyIndex < PolygonMaterialIndices.Length))
            {
                matind = PolygonMaterialIndices[polyIndex];
            }
            if ((Materials != null) && (matind < Materials.Length))
            {
                Materials[matind] = mat;
            }
        }

        public void CalculateOctants()
        {
            if (Type != BoundsType.Geometry)
            {
                Octants = null;//don't use octants for BoundBVH
                return;
            }

            Octants = new BoundGeomOctants();

            Vector3[] flipDirection = new Vector3[8]
                {
                    new Vector3(1.0f, 1.0f, 1.0f),
                    new Vector3(-1.0f, 1.0f, 1.0f),
                    new Vector3(1.0f, -1.0f, 1.0f),
                    new Vector3(-1.0f, -1.0f, 1.0f),
                    new Vector3(1.0f, 1.0f, -1.0f),
                    new Vector3(-1.0f, 1.0f, -1.0f),
                    new Vector3(1.0f, -1.0f, -1.0f),
                    new Vector3(-1.0f, -1.0f, -1.0f)
                };

            bool isShadowed(Vector3 v1, Vector3 v2, int octant)
            {
                Vector3 direction = v2 - v1;
                Vector3 flip = flipDirection[octant];
                direction *= flip;

                return direction.X >= 0.0 && direction.Y >= 0.0 && direction.Z >= 0.0;
            }

            uint[] getVerticesInOctant(int octant)
            {
                List<uint> octantIndices = new();
                if (VerticesShrunk == null) return [];

                for (uint ind1 = 0; ind1 < VerticesShrunk.Length; ind1++)
                {
                    Vector3 vertex = VerticesShrunk[ind1];

                    bool shouldAdd = true;
                    List<uint> octantIndices2 = new();

                    foreach (uint ind2 in octantIndices)
                    {
                        Vector3 vertex2 = VerticesShrunk[ind2];

                        if (isShadowed(vertex, vertex2, octant))
                        {
                            shouldAdd = false;
                            octantIndices2 = octantIndices;
                            break;
                        }

                        if (!isShadowed(vertex2, vertex, octant))
                        {
                            octantIndices2.Add(ind2);
                        }

                    }

                    if (shouldAdd)
                    {
                        octantIndices2.Add(ind1);
                    }

                    octantIndices = octantIndices2;
                }

                return octantIndices.ToArray();
            }


            for (int i = 0; i < 8; i++)
            {
                Octants.Items[i] = getVerticesInOctant(i);
                Octants.UpdateCounts();
            }
        }

        public void CalculateVertsShrunk()
        {
            //this will also adjust the Margin if it can't successfully shrink!
            //thanks to ranstar74
            //https://github.com/ranstar74/rageAm/blob/master/projects/app/src/rage/physics/bounds/boundgeometry.cpp

            VerticesShrunk = null;
            if (Type != BoundsType.Geometry) return;//don't use VerticesShrunk for BoundBVH
            if (Vertices == null) return;//must have existing vertices for this!
            var size = Vector3.Abs(BoxMax - BoxMin) * 0.5f;
            var margin = Math.Min(Math.Min(Math.Min(Margin, size.X), size.Y), size.Z);
            while (margin > 1e-6f)
            {
                var verts = ShrinkPolysByMargin(margin); //try shrink by this margin, if successful, break out
                var check = CheckShrunkPolys(verts);
                if (check)
                {
                    VerticesShrunk = verts;
                    break;
                }
                margin *= 0.5f;
            }
            if (VerticesShrunk == null)
            {
                CalculateVertsShrunkByNormals();//fallback case, just shrink by normals
                return;
            }
            margin = Math.Max(margin, 0.025f);
            //Margin = margin;//should we actually update this here?

            var shrunkMin = BoxMin + margin - CenterGeom;
            var shrunkMax = BoxMax - margin - CenterGeom;
            for (int i = 0; i < VerticesShrunk.Length; i++)//make sure the shrunk vertices fit in the box. (usually shouldn't do anything)
            {
                var vertex = VerticesShrunk[i];
                vertex = Vector3.Min(vertex, shrunkMax);
                vertex = Vector3.Max(vertex, shrunkMin);
                if (VerticesShrunk[i] != vertex)
                {
                    VerticesShrunk[i] = vertex;
                }
            }

        }
        private Vector3[] ShrinkPolysByMargin(float margin)
        {
            var verts = new Vector3[Vertices.Length];
            Array.Copy(Vertices, verts, Vertices.Length);

            var vc = verts.Length;
            var polyNormals = new Vector3[Polygons.Length];//precompute all the poly normals.
            for (int polyIndex = 0; polyIndex < Polygons.Length; polyIndex++)
            {
                var tri = Polygons[polyIndex] as BoundPolygonTriangle;
                if (tri == null) { continue; }//can only compute poly normals for triangles!
                var vi1 = tri.VertexIndex1;
                var vi2 = tri.VertexIndex2;
                var vi3 = tri.VertexIndex3;
                if ((vi1 >= vc) || (vi2 >= vc) || (vi3 >= vc)) continue;//vertex index out of range!?
                var v1 = Vertices[vi1];//test against non-shrunk triangle
                var v2 = Vertices[vi2];
                var v3 = Vertices[vi3];
                polyNormals[polyIndex] = Vector3.Normalize(Vector3.Cross(v3 - v2, v1 - v2));
            }

            var normals = new Vector3[64];//first is current poly normal, and remaining are neighbours (assumes 63 neighbours is enough!) 
            var processedVertices = new uint[2048];//2048*32 = 65536 vertices max
            var negMargin = -margin;
            for (int polyIndex = 0; polyIndex < Polygons.Length; polyIndex++)
            {
                var tri = Polygons[polyIndex] as BoundPolygonTriangle;
                if (tri == null) { continue; }
                for (int polyVertexIndex = 0; polyVertexIndex < 3; polyVertexIndex++)
                {
                    var vertexIndex = tri.GetVertexIndex(polyVertexIndex);
                    var bucket = vertexIndex >> 5;
                    var mask = 1u << (vertexIndex & 0x1F);
                    if ((processedVertices[bucket] & mask) != 0) continue;//already processed this vertex
                    processedVertices[bucket] |= mask;

                    var vertex = verts[vertexIndex];
                    var normal = polyNormals[polyIndex];
                    normals[0] = normal;//used for weighted normal computation
                    var averageNormal = normal;//compute average normal from surrounding polygons
                    var prevNeighbour = polyIndex;
                    var normalCount = 1;
                    var neighbourCount = 0;
                    var polyNeighbourIndex = (polyVertexIndex + 2) % 3;//find starting neighbour index
                    var neighbour = tri.GetNeighboringPolygonIndex(polyNeighbourIndex);
                    if (neighbour < 0)
                    {
                        neighbour = tri.GetNeighboringPolygonIndex(polyVertexIndex);
                        polyNeighbourIndex = polyVertexIndex;//is this needed?
                    }
                    while (neighbour >= 0)
                    {
                        var neighbourPoly = Polygons[neighbour] as BoundPolygonTriangle;
                        var neighbourNormal = polyNormals[neighbour];
                        averageNormal += neighbourNormal;
                        normals[neighbourCount + 1] = neighbourNormal;
                        normalCount++;
                        neighbourCount++;
                        var newNeighbour = -1;
                        if (neighbourPoly != null)
                        {
                            for (var j = 0; j < 3; j++)
                            {
                                var nextIndex = (j + 1) % 3;
                                var n = neighbourPoly.GetVertexIndex(nextIndex);
                                if (n == vertexIndex)
                                {
                                    newNeighbour = neighbourPoly.GetNeighboringPolygonIndex(j);
                                    if (newNeighbour == prevNeighbour)
                                    {
                                        newNeighbour = neighbourPoly.GetNeighboringPolygonIndex(nextIndex);
                                    }
                                    prevNeighbour = neighbour;
                                    neighbour = newNeighbour;
                                    break;
                                }
                            }
                        }
                        if (newNeighbour == polyIndex) break; //check the circle is closed and iterated all neighbours
                        if (neighbourCount >= 63) break;//too many neighbours! possibly a mesh error
                    }
                    averageNormal = Vector3.Normalize(averageNormal);

                    if (normalCount == 1)
                    {
                        verts[vertexIndex] = vertex + (normal * negMargin);
                    }
                    else if (normalCount == 2)
                    {
                        var cross = Vector3.Cross(normal, normals[1]);
                        var crossMagSq = cross.LengthSquared();
                        if (crossMagSq < 0.1f)//small angle between normals, just shrink by base normal
                        {
                            verts[vertexIndex] = vertex + (normal * negMargin);
                            continue;
                        }
                        var lengthInv = 1.0f / (float)Math.Sqrt(crossMagSq);//insert new normal to weighted set
                        normals[2] = cross * lengthInv;
                        normalCount = 3;
                    }
                    if (normalCount < 3) continue;

                    var neighbourNormalCount = normalCount - 1;
                    var shrunk = vertex + (averageNormal * negMargin);
                    for (var i = 0; i < neighbourNormalCount - 1; i++)//traverse and compute weighted normals
                    {
                        for (var j = 0; j < neighbourNormalCount - i - 1; j++)
                        {
                            for (var k = 0; k < neighbourNormalCount - j - i - 1; k++)
                            {
                                var normal1 = normals[i];
                                var normal2 = normals[i + j + 1];
                                var normal3 = normals[i + j + k + 2];
                                var cross23 = Vector3.Cross(normal2, normal3);
                                var dot = Vector3.Dot(normal1, cross23);
                                if (Math.Abs(dot) > 0.25f)//check only neighbours with large angle between neighbour normals and poly normal
                                {
                                    var dotinv = 1.0f / dot;//normals with angle closer to 0.25 will contribute more to weighted normal
                                    var cross31 = Vector3.Cross(normal3, normal1);
                                    var cross12 = Vector3.Cross(normal1, normal2);
                                    var newNormal = (cross23 + cross31 + cross12) * dotinv;//compute weighted normal
                                    var newShrink = vertex + (newNormal * negMargin);
                                    var toOld = shrunk - vertex;//choose shrunk vertex that is furthest from original vertex
                                    var toNew = newShrink - vertex;
                                    if (toNew.LengthSquared() > toOld.LengthSquared())
                                    {
                                        shrunk = newShrink;
                                    }
                                }
                            }
                        }
                    }
                    verts[vertexIndex] = shrunk;
                }
            }

            return verts;
        }
        private bool CheckShrunkPolys(Vector3[] verts)
        {
            bool rayIntersects(in Ray ray, ref Vector3 v1, ref Vector3 v2, ref Vector3 v3, float maxDist)
            {
                var hit = ray.Intersects(ref v1, ref v2, ref v3, out float dist);
                return hit && (dist <= maxDist);
            }
            var vc = verts.Length;
            if (vc != Vertices.Length) return false;//vertex count mismatch!?
            for (var i = 0; i < vc; i++)
            {
                var vertex = Vertices[i];
                var shrunkVertex = verts[i];
                var segmentPos = shrunkVertex;
                var segmentDir = vertex - shrunkVertex;
                var segmentLength = segmentDir.Length();
                if (segmentLength == 0) continue;//vertex wasn't shrunk!?
                segmentDir *= (1.0f / segmentLength);
                var segmentRay = new Ray(segmentPos, segmentDir);
                for (int p = 0; p < Polygons.Length; p++)
                {
                    var tri = Polygons[p] as BoundPolygonTriangle;
                    if (tri == null) { continue; }
                    var vi1 = tri.VertexIndex1;
                    var vi2 = tri.VertexIndex2;
                    var vi3 = tri.VertexIndex3;
                    if ((vi1 >= vc) || (vi2 >= vc) || (vi3 >= vc)) return false;//vertex index out of range!?
                    if ((vi1 == i) || (vi2 == i) || (vi3 == i)) continue;//only test polys not using this vertex
                    var v1 = Vertices[vi1];//test against non-shrunk triangle
                    var v2 = Vertices[vi2];
                    var v3 = Vertices[vi3];
                    if (rayIntersects(segmentRay, ref v1, ref v2, ref v3, segmentLength)) return false;
                    var vs1 = verts[vi1];//test against shrunk triangle
                    var vs2 = verts[vi2];
                    var vs3 = verts[vi3];
                    if (rayIntersects(segmentRay, ref vs1, ref vs2, ref vs3, segmentLength)) return false;
                }
            }
            return true;
        }


        public void CalculateVertsShrunkByNormals()
        {
            if (Type != BoundsType.Geometry)
            {
                VerticesShrunk = null;//don't use VerticesShrunk for BoundBVH
                return;
            }

            Vector3[] vertNormals = CalculateVertNormals();
            VerticesShrunk = new Vector3[Vertices.Length];

            for (int i = 0; i < Vertices.Length; i++)
            {
                Vector3 normalShrunk = vertNormals[i] * -Margin;
                VerticesShrunk[i] = Vertices[i] + normalShrunk;
            }
        }
        public Vector3[] CalculateVertNormals()
        {
            Vector3[] vertNormals = new Vector3[Vertices.Length];

            for (int i = 0; i < Polygons.Length; i++)
            {
                var tri = Polygons[i] as BoundPolygonTriangle;
                if (tri == null) { continue; }

                var p1 = tri.Vertex1;
                var p2 = tri.Vertex2;
                var p3 = tri.Vertex3;
                var p1Local = p1 - p2;
                var p3Local = p3 - p2;
                var normal = Vector3.Cross(p1Local, p3Local);
                normal.Normalize();

                vertNormals[tri.VertexIndex1] += normal;
                vertNormals[tri.VertexIndex2] += normal;
                vertNormals[tri.VertexIndex3] += normal;
            }

            for (int i = 0; i < vertNormals.Length; i++)
            {
                if (vertNormals[i].IsZero) { continue; }

                vertNormals[i].Normalize();
            }

            return vertNormals;
        }


        public void CalculateMinMax()
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            if (Vertices != null)
            {
                foreach (var v in Vertices)
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            if (VerticesShrunk != null)//this shouldn't be needed
            {
                foreach (var v in VerticesShrunk)
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            if (min.X == float.MaxValue) min = Vector3.Zero;
            if (max.X == float.MinValue) max = Vector3.Zero;
            BoxMin = min - Margin;
            BoxMax = max + Margin;
        }

        public void CalculateQuantum()
        {
            Quantum = (BoxMax - BoxMin) * 0.5f / 32767.0f;
        }

        public void BuildMaterials()
        {
            //update Materials and PolygonMaterialIndices arrays, using custom materials from polys and existing materials

            var matdict = new Dictionary<BoundMaterial_s, byte>();
            var matlist = new List<BoundMaterial_s>();
            var polymats = new List<byte>();

            if (Polygons != null)
            {
                foreach (var poly in Polygons)
                {
                    var mat = poly.Material;
                    if (matdict.TryGetValue(mat, out byte matidx))
                    {
                        polymats.Add(matidx);
                    }
                    else
                    {
                        matidx = (byte)matlist.Count;
                        matdict.Add(mat, matidx);
                        matlist.Add(mat);
                        polymats.Add(matidx);
                    }
                }
            }

            MaterialCount = (byte)matlist.Count;
            Materials = matlist.ToArray();
            PolygonMaterialIndices = polymats.ToArray();
        }

        public void UpdateEdgeIndices()
        {
            //update all triangle edge indices, based on shared vertex indices

            if (Polygons == null)
            { return; }

            for (int i = 0; i < Polygons.Length; i++)
            {
                var poly = Polygons[i];
                if (poly != null)
                {
                    poly.Index = i;
                }
            }

            var edgedict = new Dictionary<BoundEdgeRef, BoundEdge>();
            foreach (var poly in Polygons)
            {
                if (poly is BoundPolygonTriangle btri)
                {
                    var e1 = new BoundEdgeRef(btri.VertexIndex1, btri.VertexIndex2);
                    var e2 = new BoundEdgeRef(btri.VertexIndex2, btri.VertexIndex3);
                    var e3 = new BoundEdgeRef(btri.VertexIndex3, btri.VertexIndex1);

                    if (edgedict.TryGetValue(e1, out BoundEdge? edge1))
                    {
                        if (edge1.Triangle2 != null)
                        {
                            btri.SetNeighboringPolygonIndex(0, edge1.Triangle1.Index);
                        }
                        else
                        {
                            edge1.Triangle2 = btri;
                            edge1.EdgeID2 = 0;
                        }
                    }
                    else
                    {
                        edgedict[e1] = new BoundEdge(btri, 0);
                    }
                    if (edgedict.TryGetValue(e2, out BoundEdge? edge2))
                    {
                        if (edge2.Triangle2 != null)
                        {
                            btri.SetNeighboringPolygonIndex(1, edge2.Triangle1.Index);
                        }
                        else
                        {
                            edge2.Triangle2 = btri;
                            edge2.EdgeID2 = 1;
                        }
                    }
                    else
                    {
                        edgedict[e2] = new BoundEdge(btri, 1);
                    }
                    if (edgedict.TryGetValue(e3, out BoundEdge? edge3))
                    {
                        if (edge3.Triangle2 != null)
                        {
                            btri.SetNeighboringPolygonIndex(2, edge3.Triangle1.Index);
                        }
                        else
                        {
                            edge3.Triangle2 = btri;
                            edge3.EdgeID2 = 2;
                        }
                    }
                    else
                    {
                        edgedict[e3] = new BoundEdge(btri, 2);
                    }

                }
            }

            foreach (var kvp in edgedict)
            {
                var eref = kvp.Key;
                var edge = kvp.Value;

                if (edge.Triangle1 == null)
                { continue; }


                if (edge.Triangle2 == null)
                {
                    edge.Triangle1.SetNeighboringPolygonIndex(edge.EdgeID1, -1);
                }
                else
                {
                    edge.Triangle1.SetNeighboringPolygonIndex(edge.EdgeID1, edge.Triangle2.Index);
                    edge.Triangle2.SetNeighboringPolygonIndex(edge.EdgeID2, edge.Triangle1.Index);
                }
            }
        }

        public void UpdateTriangleAreas()
        {
            //update all triangle areas, based on vertex positions

            if (Polygons == null)
            { return; }

            var edgedict = new Dictionary<BoundEdgeRef, BoundEdge>();
            foreach (var poly in Polygons)
            {
                if (poly is BoundPolygonTriangle btri)
                {
                    var v1 = btri.Vertex1;
                    var v2 = btri.Vertex2;
                    var v3 = btri.Vertex3;
                    var area = TriangleMath.Area(ref v1, ref v2, ref v3);
                    btri.Area = area;
                }
            }
        }

        public bool DeletePolygon(BoundPolygon p)
        {
            if (Polygons != null)
            {
                var polys = Polygons.ToList();
                var polymats = PolygonMaterialIndices.ToList();
                var idx = polys.IndexOf(p);
                if (idx >= 0)
                {
                    polys.RemoveAt(idx);
                    polymats.RemoveAt(idx);
                    Polygons = polys.ToArray();
                    PolygonMaterialIndices = polymats.ToArray();
                    PolygonCount = polys.Count;

                    for (int i = 0; i < Polygons.Length; i++)
                    {
                        var poly = Polygons[i];
                        if (poly is BoundPolygonTriangle btri)
                        {
                            if (btri.NeighboringPolygonIndex1 == idx) btri.NeighboringPolygonIndex1 = ushort.MaxValue;
                            if ((btri.NeighboringPolygonIndex1 > idx) && (btri.NeighboringPolygonIndex1 != ushort.MaxValue)) btri.NeighboringPolygonIndex1--;
                            if (btri.NeighboringPolygonIndex2 == idx) btri.NeighboringPolygonIndex2 = ushort.MaxValue;
                            if ((btri.NeighboringPolygonIndex2 > idx) && (btri.NeighboringPolygonIndex2 != ushort.MaxValue)) btri.NeighboringPolygonIndex2--;
                            if (btri.NeighboringPolygonIndex3 == idx) btri.NeighboringPolygonIndex3 = ushort.MaxValue;
                            if ((btri.NeighboringPolygonIndex3 > idx) && (btri.NeighboringPolygonIndex3 != ushort.MaxValue)) btri.NeighboringPolygonIndex3--;
                        }
                        poly.Index = i;
                    }

                    var verts = p.VertexIndices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        if (DeleteVertex(verts[i], false)) //delete any orphaned vertices
                        {
                            verts = p.VertexIndices;//vertex indices will have changed, need to continue with the new ones!
                        }
                    }

                    return true;
                }

            }
            return false;
        }

        public bool DeleteVertex(int index, bool deletePolys = true)
        {
            if (Vertices != null)
            {
                if (!deletePolys)
                {
                    //if not deleting polys, make sure this vertex isn't used by any
                    foreach (var poly in Polygons)
                    {
                        var pverts = poly.VertexIndices;
                        for (int i = 0; i < pverts.Length; i++)
                        {
                            if (pverts[i] == index)
                            {
                                return false;
                            }
                        }
                    }
                }

                var verts = Vertices.ToList();
                var verts2 = VerticesShrunk?.ToList();
                var vertcols = VertexColours?.ToList();
                var vertobjs = VertexObjects?.ToList();
                verts.RemoveAt(index);
                verts2?.RemoveAt(index);
                vertcols?.RemoveAt(index);
                vertobjs?.RemoveAt(index);
                Vertices = verts.ToArray();
                VerticesShrunk = verts2?.ToArray();
                VertexColours = vertcols?.ToArray();
                VertexObjects = vertobjs?.ToArray();
                VertexCount = verts.Count;
                ConvexHullVertexCount = checked((ushort)VertexCount);

                if (VertexObjects != null)
                {
                    for (int i = 0; i < VertexObjects.Length; i++)
                    {
                        if (VertexObjects[i] is { } vertexObject) vertexObject.Index = i;
                    }
                }

                if (Polygons != null)
                {
                    var delpolys = new List<BoundPolygon>();

                    foreach (var poly in Polygons)
                    {
                        var pverts = poly.VertexIndices;
                        for (int i = 0; i < pverts.Length; i++)
                        {
                            if (pverts[i] == index)
                            {
                                delpolys.Add(poly);
                            }
                            if (pverts[i] > index)
                            {
                                pverts[i]--;
                            }
                        }
                        poly.VertexIndices = pverts;
                    }

                    if (deletePolys)
                    {
                        foreach (var delpoly in delpolys)
                        {
                            DeletePolygon(delpoly);
                        }
                    }
                    else
                    {
                        if (delpolys.Count > 0)
                        { } //this shouldn't happen! shouldn't have deleted the vertex if it is used by polys
                    }
                }

                return true;
            }
            return false;
        }


        public int AddVertex()
        {
            var verts = Vertices?.ToList() ?? new List<Vector3>();
            var verts2 = VerticesShrunk?.ToList();
            var vertcols = VertexColours?.ToList();
            var vertobjs = VertexObjects?.ToList();
            var index = verts.Count;

            verts.Add(Vector3.Zero);
            verts2?.Add(Vector3.Zero);
            vertcols?.Add(new BoundMaterialColour());
            vertobjs?.Add(null);

            Vertices = verts.ToArray();
            VerticesShrunk = verts2?.ToArray();
            VertexColours = vertcols?.ToArray();
            VertexObjects = vertobjs?.ToArray();
            VertexCount = verts.Count;
            ConvexHullVertexCount = checked((ushort)VertexCount);

            return index;
        }

        public BoundPolygon? AddPolygon(BoundPolygonType type)
        {
            var p = CreatePolygon(type);
            if (p == null) return null;

            var polys = Polygons?.ToList() ?? new List<BoundPolygon>();
            var polymats = PolygonMaterialIndices?.ToList() ?? new List<byte>();

            p.Index = polys.Count;
            polys.Add(p);
            polymats.Add(0);

            Polygons = polys.ToArray();
            PolygonMaterialIndices = polymats.ToArray();
            PolygonCount = polys.Count;

            var vinds = p.VertexIndices; //just get the required array size
            if (vinds != null)
            {
                for (int i = 0; i < vinds.Length; i++)
                {
                    vinds[i] = AddVertex();
                }
                p.VertexIndices = vinds;
            }

            return p;
        }

        private BoundPolygon? CreatePolygon(BoundPolygonType type)
        {
            BoundPolygon? p = null;
            switch (type)
            {
                case BoundPolygonType.Triangle:
                    p = new BoundPolygonTriangle();
                    break;
                case BoundPolygonType.Sphere:
                    p = new BoundPolygonSphere();
                    break;
                case BoundPolygonType.Capsule:
                    p = new BoundPolygonCapsule();
                    break;
                case BoundPolygonType.Box:
                    p = new BoundPolygonBox();
                    break;
                case BoundPolygonType.Cylinder:
                    p = new BoundPolygonCylinder();
                    break;
                default:
                    break;
            }
            if (p != null)
            {
                p.Owner = this;
            }
            return p;
        }
    }
    [TC(typeof(EXP))] public class BoundBVH : BoundGeometry
    {
        public override long BlockLength => 0x150;

        // structure data
        public ulong BVHPointer { get; set; }
        public ushort NumActivePolygons { get; set; } = ushort.MaxValue;

        // reference data
        public BVH? BVH { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            BVHPointer = reader.ReadUInt64();
            _ = reader.ReadUInt64(); // m_ActivePolygonIndices is runtime-only.
            NumActivePolygons = reader.ReadUInt16();
            _ = reader.ReadBytes(14); // m_Pad

            if (BVHPointer > ushort.MaxValue)
            {
                BVH = reader.ReadBlockAt<BVH>(BVHPointer);
            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            BVHPointer = (ulong)(BVH?.FilePosition ?? 0);

            writer.Write(BVHPointer);
            writer.Write(0ul); // m_ActivePolygonIndices is runtime-only.
            writer.Write(NumActivePolygons);
            writer.Write(new byte[14]);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            FileVFT = 1080228536;
        }

        public override IResourceBlock[] GetReferences()
        {
            BuildBVH(false);

            var list = new List<IResourceBlock>(base.GetReferences());
            if (BVH != null) list.Add(BVH);
            return list.ToArray();
        }




        public void BuildBVH(bool updateParent = true)
        {
            if (Polygons.Length == 0) //in some des_ drawables?
            {
                if (BVH != null)
                { }
                BVH = null;
                return;
            }
            if (BVH != null)
            {
                //var tnodes = BVHBuilder.Unbuild(BVH);
            }

            var items = new List<BVHBuilderItem?>();
            for (int i = 0; i < Polygons.Length; i++)
            {
                var poly = Polygons[i];
                if (poly != null)
                {
                    var it = new BVHBuilderItem();
                    it.Min = poly.BoxMin;
                    it.Max = poly.BoxMax;
                    it.Index = i;
                    it.Polygon = poly;
                    items.Add(it);
                }
            }

            var bvh = BVHBuilder.Build(items, 4); //geometries have BVH item threshold of 4

            var newpolys = new BoundPolygon[items.Count];
            var newpolymats = new byte[items.Count];
            var itemlookup = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var poly = items[i]?.Polygon;
                if (poly != null)
                {
                    if (poly.Index < itemlookup.Length)
                    {
                        itemlookup[poly.Index] = i;
                    }
                    else
                    { }//shouldn't happen
                }
                else
                { }//shouldn't happen
            }
            for (int i = 0; i < items.Count; i++)
            {
                var poly = items[i]?.Polygon;
                if (poly != null)
                {
                    newpolys[i] = poly;
                    newpolymats[i] = (poly.Index < PolygonMaterialIndices.Length) ? PolygonMaterialIndices[poly.Index] : (byte)0;//is this necessary?
                    poly.Index = i;
                    if (poly is BoundPolygonTriangle ptri)
                    {
                        var neighbor1 = ptri.NeighboringPolygonIndex1 < itemlookup.Length ? itemlookup[ptri.NeighboringPolygonIndex1] : -1;
                        var neighbor2 = ptri.NeighboringPolygonIndex2 < itemlookup.Length ? itemlookup[ptri.NeighboringPolygonIndex2] : -1;
                        var neighbor3 = ptri.NeighboringPolygonIndex3 < itemlookup.Length ? itemlookup[ptri.NeighboringPolygonIndex3] : -1;
                        ptri.SetNeighboringPolygonIndex(0, neighbor1);
                        ptri.SetNeighboringPolygonIndex(1, neighbor2);
                        ptri.SetNeighboringPolygonIndex(2, neighbor3);
                    }
                }
            }
            Polygons = newpolys;
            PolygonMaterialIndices = newpolymats;

            BoxMin = bvh.AABBMin;
            BoxMax = bvh.AABBMax;
            BoxCenter = bvh.AABBCenter;
            SphereCenter = BoxCenter;
            SphereRadius = (BoxMax - BoxCenter).Length();

            BVH = bvh;

            if (updateParent && (Parent != null)) //only update parent when live editing in world view!
            {
                Parent.BuildBVH();
            }
        }



        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult();
            var box = new BoundingBox();

            if (Polygons == null)
            { return res; }
            if ((BVH?.ContiguousNodes?.data_items == null) || (BVH?.SubtreeHeaders?.data_items == null))
            { return res; }

            box.Minimum = BoxMin;
            box.Maximum = BoxMax;
            if (!sph.Intersects(ref box))
            { return res; }

            var q = BVH.InvQuantize;
            var c = BVH.AABBCenter;
            for (int t = 0; t < BVH.SubtreeHeaders.data_items.Length; t++)
            {
                var tree = BVH.SubtreeHeaders.data_items[t];
                box.Minimum = tree.Min * q + c;
                box.Maximum = tree.Max * q + c;
                if (!sph.Intersects(ref box))
                { continue; }

                int nodeind = tree.NodeIndex1;
                int lastind = tree.NodeIndex2;
                while (nodeind < lastind)
                {
                    var node = BVH.ContiguousNodes.data_items[nodeind];
                    box.Minimum = node.Min * q + c;
                    box.Maximum = node.Max * q + c;
                    bool nodehit = sph.Intersects(ref box);
                    bool nodeskip = !nodehit;
                    if (node.ItemCount <= 0) //intermediate node with child nodes
                    {
                        if (nodeskip)
                        {
                            nodeind += node.ItemId; //(child node count)
                        }
                        else
                        {
                            nodeind++;
                        }
                    }
                    else //leaf node, with polygons
                    {
                        if (!nodeskip)
                        {
                            var lastp = Math.Min(node.ItemId + node.ItemCount, PolygonCount);

                            SphereIntersectPolygons(ref sph, ref res, node.ItemId, lastp);

                        }
                        nodeind++;
                    }
                    res.TestedNodeCount++;
                }
            }

            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            var box = new BoundingBox();

            if (Polygons == null)
            { return res; }
            if ((BVH?.ContiguousNodes?.data_items == null) || (BVH?.SubtreeHeaders?.data_items == null))
            { return res; }

            box.Minimum = BoxMin;
            box.Maximum = BoxMax;
            float bvhboxhittest;
            if (!ray.Intersects(ref box, out bvhboxhittest))
            { return res; }
            if (bvhboxhittest > maxdist)
            { return res; } //already a closer hit.

            res.HitDist = maxdist;

            var q = BVH.InvQuantize;
            var c = BVH.AABBCenter;
            for (int t = 0; t < BVH.SubtreeHeaders.data_items.Length; t++)
            {
                var tree = BVH.SubtreeHeaders.data_items[t];
                box.Minimum = tree.Min * q + c;
                box.Maximum = tree.Max * q + c;
                if (!ray.Intersects(ref box, out bvhboxhittest))
                { continue; }
                if (bvhboxhittest > res.HitDist)
                { continue; } //already a closer hit.

                int nodeind = tree.NodeIndex1;
                int lastind = tree.NodeIndex2;
                while (nodeind < lastind)
                {
                    var node = BVH.ContiguousNodes.data_items[nodeind];
                    box.Minimum = node.Min * q + c;
                    box.Maximum = node.Max * q + c;
                    bool nodehit = ray.Intersects(ref box, out bvhboxhittest);
                    bool nodeskip = !nodehit || (bvhboxhittest > res.HitDist);
                    if (node.ItemCount <= 0) //intermediate node with child nodes
                    {
                        if (nodeskip)
                        {
                            nodeind += node.ItemId; //(child node count)
                        }
                        else
                        {
                            nodeind++;
                        }
                    }
                    else //leaf node, with polygons
                    {
                        if (!nodeskip)
                        {
                            var lastp = Math.Min(node.ItemId + node.ItemCount, PolygonCount);

                            RayIntersectPolygons(ref ray, ref res, node.ItemId, lastp);

                        }
                        nodeind++;
                    }
                    res.TestedNodeCount++;
                }
            }

            return res;
        }

    }
    [TC(typeof(EXP))] public class BoundComposite : Bounds
    {
        public override long BlockLength => 0xB0;

        // structure data
        public ulong ChildrenPointer { get; set; }
        public ulong CurrentMatricesPointer { get; set; }
        public ulong LastMatricesPointer { get; set; }
        public ulong LocalBoundingBoxesPointer { get; set; }
        public ulong TypeAndIncludeFlagsPointer { get; set; }
        public ulong OwnedTypeAndIncludeFlagsPointer { get; set; }
        public ushort MaxNumBounds { get; set; }
        public ushort NumBounds { get; set; }
        public ulong BVHStructurePointer { get; set; }

        // reference data
        public ResourcePointerArray64<Bounds>? Children { get; set; }
        public Matrix4F_s[]? CurrentMatrices { get; set; }
        public Matrix4F_s[]? LastMatrices { get; set; }
        public AABB_s[]? LocalBoundingBoxes { get; set; }
        public BoundCompositeChildrenFlags[]? TypeAndIncludeFlags { get; set; }

        public BVH? BVHStructure { get; set; }


        private ResourceSystemStructBlock<Matrix4F_s>? CurrentMatricesBlock;
        private ResourceSystemStructBlock<Matrix4F_s>? LastMatricesBlock;
        private ResourceSystemStructBlock<AABB_s>? LocalBoundingBoxesBlock;
        private ResourceSystemStructBlock<BoundCompositeChildrenFlags>? OwnedTypeAndIncludeFlagsBlock;


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            ChildrenPointer = reader.ReadUInt64();
            CurrentMatricesPointer = reader.ReadUInt64();
            LastMatricesPointer = reader.ReadUInt64();
            LocalBoundingBoxesPointer = reader.ReadUInt64();
            TypeAndIncludeFlagsPointer = reader.ReadUInt64();
            OwnedTypeAndIncludeFlagsPointer = reader.ReadUInt64();
            MaxNumBounds = reader.ReadUInt16();
            NumBounds = reader.ReadUInt16();
            _ = reader.ReadUInt32(); // Alignment padding.
            BVHStructurePointer = reader.ReadUInt64();

            ValidateChildCounts();
            Children = reader.ReadBlockAt<ResourcePointerArray64<Bounds>>(ChildrenPointer, MaxNumBounds);
            CurrentMatrices = reader.ReadStructsAt<Matrix4F_s>(CurrentMatricesPointer, MaxNumBounds);
            LastMatrices = LastMatricesPointer == CurrentMatricesPointer
                ? null
                : reader.ReadStructsAt<Matrix4F_s>(LastMatricesPointer, MaxNumBounds);
            LocalBoundingBoxes = reader.ReadStructsAt<AABB_s>(LocalBoundingBoxesPointer, NumBounds);
            var flagsPointer = TypeAndIncludeFlagsPointer != 0 ? TypeAndIncludeFlagsPointer : OwnedTypeAndIncludeFlagsPointer;
            TypeAndIncludeFlags = reader.ReadStructsAt<BoundCompositeChildrenFlags>(flagsPointer, MaxNumBounds);
            BVHStructure = reader.ReadBlockAt<BVH>(BVHStructurePointer);


            var childTransforms = CurrentMatrices ?? LastMatrices;
            if ((Children != null) && (Children.data_items != null))
            {
                var count = Math.Min(NumBounds, Children.data_items.Length);
                for (int i = 0; i < count; i++)
                {
                    var child = Children.data_items[i];
                    if (child != null)
                    {
                        child.Parent = this;

                        var xform = ((childTransforms != null) && (i < childTransforms.Length)) ? childTransforms[i].ToMatrix() : Matrix.Identity;
                        child.Transform = xform;
                        child.TransformInv = Matrix.Invert(xform);
                        var flags = ((TypeAndIncludeFlags != null) && (i < TypeAndIncludeFlags.Length)) ? TypeAndIncludeFlags[i] : new BoundCompositeChildrenFlags();
                        child.CompositeFlags1 = flags;
                        child.CompositeFlags2 = flags;
                    }
                }
            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            SynchronizeChildCounts();
            ChildrenPointer = (ulong)(Children?.FilePosition ?? 0);
            CurrentMatricesPointer = (ulong)(CurrentMatricesBlock?.FilePosition ?? 0);
            LastMatricesPointer = (ulong)(LastMatricesBlock?.FilePosition ?? (long)CurrentMatricesPointer);
            LocalBoundingBoxesPointer = (ulong)(LocalBoundingBoxesBlock?.FilePosition ?? 0);
            OwnedTypeAndIncludeFlagsPointer = (ulong)(OwnedTypeAndIncludeFlagsBlock?.FilePosition ?? 0);
            TypeAndIncludeFlagsPointer = OwnedTypeAndIncludeFlagsPointer;
            BVHStructurePointer = (ulong)(BVHStructure?.FilePosition ?? 0);

            writer.Write(ChildrenPointer);
            writer.Write(CurrentMatricesPointer);
            writer.Write(LastMatricesPointer);
            writer.Write(LocalBoundingBoxesPointer);
            writer.Write(TypeAndIncludeFlagsPointer);
            writer.Write(OwnedTypeAndIncludeFlagsPointer);
            writer.Write(MaxNumBounds);
            writer.Write(NumBounds);
            writer.Write(0u);
            writer.Write(BVHStructurePointer);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            var c = Children?.data_items;
            if ((c == null) || (c.Length == 0))
            {
                YbnXml.SelfClosingTag(sb, indent, "Children");
            }
            else
            {
                var cind = indent + 1;
                YbnXml.OpenTag(sb, indent, "Children");
                foreach (var child in c)
                {
                    Bounds.WriteXmlNode(child, sb, cind, "Item");
                }
                YbnXml.CloseTag(sb, indent, "Children");
            }
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            var cnode = node.SelectSingleNode("Children");
            if (cnode != null)
            {
                var cnodes = cnode.SelectNodes("Item");
                if (cnodes?.Count > 0)
                {
                    var arr = new Bounds[cnodes.Count];
                    var childIndex = 0;
                    foreach (XmlNode inode in cnodes)
                    {
                        var b = Bounds.ReadXmlNode(inode, Owner, this);
                        if (b != null) arr[childIndex] = b;
                        childIndex++;
                    }
                    Children = new ResourcePointerArray64<Bounds>();
                    Children.data_items = arr;
                    MaxNumBounds = NumBounds = checked((ushort)arr.Length);

                    BuildBVH();
                    UpdateChildrenFlags();
                    UpdateChildrenBounds();
                    UpdateChildrenTransformations();
                }
            }

            FileVFT = 1080212136;
        }

        public override IResourceBlock[] GetReferences()
        {
            SynchronizeChildCounts();
            BuildBVH();
            UpdateChildrenFlags();
            UpdateChildrenBounds();
            UpdateChildrenTransformations();

            var list = new List<IResourceBlock>(base.GetReferences());
            if (Children != null) list.Add(Children);
            CurrentMatricesBlock = CurrentMatrices != null ? new ResourceSystemStructBlock<Matrix4F_s>(CurrentMatrices) : null;
            if (CurrentMatricesBlock != null)
            {
                list.Add(CurrentMatricesBlock);
            }
            LastMatricesBlock = LastMatrices != null ? new ResourceSystemStructBlock<Matrix4F_s>(LastMatrices) : null;
            if (LastMatricesBlock != null)
            {
                list.Add(LastMatricesBlock);
            }
            LocalBoundingBoxesBlock = LocalBoundingBoxes != null ? new ResourceSystemStructBlock<AABB_s>(LocalBoundingBoxes) : null;
            if (LocalBoundingBoxesBlock != null)
            {
                list.Add(LocalBoundingBoxesBlock);
            }
            OwnedTypeAndIncludeFlagsBlock = TypeAndIncludeFlags != null ? new ResourceSystemStructBlock<BoundCompositeChildrenFlags>(TypeAndIncludeFlags) : null;
            if (OwnedTypeAndIncludeFlagsBlock != null)
            {
                list.Add(OwnedTypeAndIncludeFlagsBlock);
            }
            if (BVHStructure != null) list.Add(BVHStructure);
            return list.ToArray();
        }

        private void SynchronizeChildCounts()
        {
            var capacity = Children?.Count ?? 0;
            if (capacity > ushort.MaxValue)
                throw new InvalidDataException($"Composite bound capacity exceeds {ushort.MaxValue}.");

            if (MaxNumBounds != capacity)
            {
                MaxNumBounds = NumBounds = (ushort)capacity;
            }
            ValidateChildCounts();
        }

        private void ValidateChildCounts()
        {
            if (NumBounds > MaxNumBounds)
                throw new InvalidDataException("Composite bound count exceeds its capacity.");
        }




        public void BuildBVH()
        {
            SynchronizeChildCounts();
            if (Children?.data_items == null)
            {
                BVHStructure = null;
                return;
            }
            var count = Math.Min(NumBounds, Children.data_items.Length);
            if (count <= 5) //composites only get BVHs if they have 6 or more children.
            {
                if (BVHStructure != null)
                { }
                BVHStructure = null;
                return;
            }
            if (BVHStructure != null)
            {
                //var tnodes = BVHBuilder.Unbuild(BVHStructure);
            }
            else
            {
                //why are we here? yft's hit this... (and when loading XML!)
                if (!(Owner is FragPhysicsLOD) && !(Owner is FragPhysArchetype) && !(Owner is VerletCloth))
                { }
            }
            if (Owner is FragPhysArchetype fpa)
            {
                if (fpa == fpa.Owner?.Archetype2) //for destroyed yft archetype, don't use a BVH.
                {
                    BVHStructure = null;
                    return;
                }
            }

            var items = new List<BVHBuilderItem?>();
            for (int i = 0; i < count; i++)
            {
                var child = Children.data_items[i];
                if (child != null)
                {
                    var cbox = new BoundingBox(child.BoxMin, child.BoxMax);
                    var tcbox = cbox.Transform(child.Transform);
                    var it = new BVHBuilderItem();
                    it.Min = tcbox.Minimum;
                    it.Max = tcbox.Maximum;
                    it.Index = i;
                    it.Bounds = child;
                    items.Add(it);
                }
                else
                {
                    items.Add(null);//items need to have correct count to set the correct capacity for the BVH!
                }
            }

            BVHStructure = BVHBuilder.Build(items, 1); //composites have BVH item threshold of 1
        }

        public void UpdateChildrenFlags()
        {
            if (Children?.data_items == null)
            {
                TypeAndIncludeFlags = null;
                return;
            }
            if (OwnerIsFragment)//don't use flags in fragments
            {
                TypeAndIncludeFlags = null;
                return;
            }

            var flags = new BoundCompositeChildrenFlags[Children.data_items.Length];
            for (int i = 0; i < flags.Length; i++)
            {
                var child = Children.data_items[i];
                if (child != null)
                {
                    flags[i] = child.CompositeFlags1;
                }
            }

            TypeAndIncludeFlags = flags;
        }

        public void UpdateChildrenBounds()
        {
            if (Children?.data_items == null)
            {
                LocalBoundingBoxes = null;
                return;
            }

            var count = Math.Min(NumBounds, Children.data_items.Length);
            var boxes = new AABB_s[count];
            for (int i = 0; i < count; i++)
            {
                var child = Children.data_items[i];
                if (child != null)
                {
                    boxes[i].Min = new Vector4(child.BoxMin, float.Epsilon);
                    boxes[i].Max = new Vector4(child.BoxMax, child.Margin);
                }
            }

            LocalBoundingBoxes = boxes;
        }

        public void UpdateChildrenTransformations()
        {
            if (Children?.data_items == null)
            {
                CurrentMatrices = null;
                LastMatrices = null;
                return;
            }

            var matrices = new Matrix4F_s[Children.data_items.Length];
            for (int i = 0; i < matrices.Length; i++)
            {
                var child = Children.data_items[i];
                var m = Matrix4F_s.Identity;
                if (child != null)
                {
                    m = new Matrix4F_s(child.Transform);
                }

                if (OwnerIsFragment)
                {
                    m.Flags1 = 0x7f800001;
                    m.Flags2 = 0x7f800001;
                    m.Flags3 = 0x7f800001;
                    m.Flags4 = 0x7f800001;
                }
                else
                {
                    //m.Column4 = new Vector4(0.0f, float.Epsilon, float.Epsilon, 0.0f);//is this right? TODO: check!
                    m.Flags1 = 0;
                    m.Flags2 = 1;
                    m.Flags3 = 1;
                    m.Flags4 = 0;
                }

                matrices[i] = m;
            }

            CurrentMatrices = matrices;
        }


        public override SpaceSphereIntersectResult SphereIntersect(ref BoundingSphere sph)
        {
            var res = new SpaceSphereIntersectResult();
            var tsph = sph;

            var compchilds = Children?.data_items;
            if (compchilds == null)
            { return res; }

            var count = Math.Min(NumBounds, compchilds.Length);
            for (int i = 0; i < count; i++)
            {
                var c = compchilds[i];
                if (c == null) continue;

                tsph.Center = c.TransformInv.Multiply(sph.Center);

                var chit = c.SphereIntersect(ref tsph);

                chit.Normal = c.Transform.MultiplyRot(chit.Normal);

                res.TryUpdate(ref chit);
            }

            return res;
        }
        public override SpaceRayIntersectResult RayIntersect(ref Ray ray, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            res.HitDist = maxdist;

            var tray = ray;

            var compchilds = Children?.data_items;
            if (compchilds == null)
            { return res; }

            var count = Math.Min(NumBounds, compchilds.Length);
            for (int i = 0; i < count; i++)
            {
                var c = compchilds[i];
                if (c == null) continue;

                tray.Position = c.TransformInv.Multiply(ray.Position);
                tray.Direction = c.TransformInv.MultiplyRot(ray.Direction);

                var chit = c.RayIntersect(ref tray, res.HitDist);

                chit.Position = c.Transform.Multiply(chit.Position);
                chit.Normal = c.Transform.MultiplyRot(chit.Normal);

                res.TryUpdate(ref chit);
            }

            return res;
        }




        public bool DeleteChild(Bounds child)
        {
            if (Children?.data_items != null)
            {
                var children = Children.data_items.ToList();
                var currentMatrices = CurrentMatrices?.ToList();
                var lastMatrices = LastMatrices?.ToList();
                var boxes = LocalBoundingBoxes?.ToList();
                var flags = TypeAndIncludeFlags?.ToList();
                var idx = children.IndexOf(child);
                if (idx >= 0)
                {
                    children.RemoveAt(idx);
                    currentMatrices?.RemoveAt(idx);
                    lastMatrices?.RemoveAt(idx);
                    boxes?.RemoveAt(idx);
                    flags?.RemoveAt(idx);
                    Children.data_items = children.ToArray();
                    CurrentMatrices = currentMatrices?.ToArray();
                    LastMatrices = lastMatrices?.ToArray();
                    LocalBoundingBoxes = boxes?.ToArray();
                    TypeAndIncludeFlags = flags?.ToArray();
                    MaxNumBounds = NumBounds = checked((ushort)children.Count);
                    BuildBVH();
                    return true;
                }
            }
            return false;
        }

        public void AddChild(Bounds child)
        {
            Children ??= new ResourcePointerArray64<Bounds>();

            var children = Children.data_items?.ToList() ?? new List<Bounds>();
            var currentMatrices = CurrentMatrices?.ToList() ?? new List<Matrix4F_s>();
            var lastMatrices = LastMatrices?.ToList();
            var boxes = LocalBoundingBoxes?.ToList() ?? new List<AABB_s>();
            var flags = TypeAndIncludeFlags?.ToList();

            child.Parent = this;

            children.Add(child);
            currentMatrices.Add(Matrix4F_s.Identity);
            lastMatrices?.Add(Matrix4F_s.Identity);
            boxes.Add(new AABB_s());//will get updated later
            flags?.Add(new BoundCompositeChildrenFlags());
            Children.data_items = children.ToArray();
            CurrentMatrices = currentMatrices.ToArray();
            LastMatrices = lastMatrices?.ToArray();
            LocalBoundingBoxes = boxes.ToArray();
            TypeAndIncludeFlags = flags?.ToArray();
            MaxNumBounds = NumBounds = checked((ushort)children.Count);
            BuildBVH();
            UpdateChildrenBounds();
        }
    }


    public enum BoundPolygonType : byte
    {
        Triangle = 0,
        Sphere = 1,
        Capsule = 2,
        Box = 3,
        Cylinder = 4,
    }
    [TC(typeof(EXP))] public abstract class BoundPolygon : IMetaXmlItem
    {
        public const int SerializedSize = 0x10;
        private const byte PrimitiveTypeMask = 0x07;

        public BoundPolygonType Type { get; set; }
        public BoundGeometry? Owner { get; set; } //for browsing/editing convenience
        public BoundMaterial_s Material
        {
            get
            {
                if (MaterialCustom.HasValue) return MaterialCustom.Value;
                return Owner?.GetMaterial(Index) ?? new BoundMaterial_s();
            }
            set
            {
                MaterialCustom = value;
            }
        }
        public BoundMaterial_s? MaterialCustom; //for editing, when assigning a new material.
        public int MaterialIndex
        {
            get { return Owner?.GetMaterialIndex(Index) ?? -1; }
        }
        public Vector3[] VertexPositions
        {
            get
            {
                var inds = VertexIndices;
                var va = new Vector3[inds.Length];
                if (Owner != null)
                {
                    for (int i = 0; i < inds.Length; i++)
                    {
                        va[i] = Owner.GetVertexPos(inds[i]);
                    }
                }
                return va;
            }
            set
            {
                if (value == null) return;
                var inds = VertexIndices;
                if (Owner != null)
                {
                    var imax = Math.Min(inds.Length, value.Length);
                    for (int i = 0; i < imax; i++)
                    {
                        Owner.SetVertexPos(inds[i], value[i]);
                    }
                }
            }
        }
        public int Index { get; set; } //for editing convenience, not stored
        public abstract Vector3 BoxMin { get; }
        public abstract Vector3 BoxMax { get; }
        public abstract Vector3 Scale { get; set; }
        public abstract Vector3 Position { get; set; }
        public abstract Quaternion Orientation { get; set; }
        public abstract int[] VertexIndices { get; set; }
        public abstract BoundVertexRef NearestVertex(Vector3 p);
        public abstract void GatherVertices(Dictionary<BoundVertex, int> verts);
        public abstract void Read(byte[] bytes, int offset);
        public abstract void Write(BinaryWriter bw);
        public abstract void WriteXml(StringBuilder sb, int indent);
        public abstract void ReadXml(XmlNode node);

        public static BoundPolygonType DecodeType(byte areaLowByte)
        {
            var type = (BoundPolygonType)(areaLowByte & PrimitiveTypeMask);
            if (type > BoundPolygonType.Cylinder)
                throw new InvalidDataException($"Unsupported primitive type: {(byte)type}.");
            return type;
        }

        public static byte ClearType(byte areaLowByte) => (byte)(areaLowByte & ~PrimitiveTypeMask);

        public byte EncodeType(byte areaLowByte)
        {
            if (Type > BoundPolygonType.Cylinder)
                throw new InvalidDataException($"Unsupported primitive type: {(byte)Type}.");
            return (byte)(ClearType(areaLowByte) | (byte)Type);
        }

        public virtual string Title => Type + " " + Index;
        public override string ToString() => Type.ToString();
    }
    [TC(typeof(EXP))] public class BoundPolygonTriangle : BoundPolygon
    {
        private const ushort VertexIndexMask = 0x7FFF;
        private const ushort VertexNormalMask = 0x8000;

        private float area;
        public float Area
        {
            get => area;
            set
            {
                var bits = BitConverter.SingleToInt32Bits(value);
                var firstByte = EncodeType((byte)bits);
                area = BitConverter.Int32BitsToSingle((bits & ~0xFF) | firstByte);
            }
        }
        public ushort PackedVertexIndex1 { get; set; }
        public ushort PackedVertexIndex2 { get; set; }
        public ushort PackedVertexIndex3 { get; set; }
        public ushort NeighboringPolygonIndex1 { get; set; } = ushort.MaxValue;
        public ushort NeighboringPolygonIndex2 { get; set; } = ushort.MaxValue;
        public ushort NeighboringPolygonIndex3 { get; set; } = ushort.MaxValue;

        public int VertexIndex1 { get => PackedVertexIndex1 & VertexIndexMask; set => PackedVertexIndex1 = PackVertexIndex(value, VertexNormalFlag1); }
        public int VertexIndex2 { get => PackedVertexIndex2 & VertexIndexMask; set => PackedVertexIndex2 = PackVertexIndex(value, VertexNormalFlag2); }
        public int VertexIndex3 { get => PackedVertexIndex3 & VertexIndexMask; set => PackedVertexIndex3 = PackVertexIndex(value, VertexNormalFlag3); }
        public bool VertexNormalFlag1 { get => (PackedVertexIndex1 & VertexNormalMask) != 0; set => PackedVertexIndex1 = PackVertexIndex(VertexIndex1, value); }
        public bool VertexNormalFlag2 { get => (PackedVertexIndex2 & VertexNormalMask) != 0; set => PackedVertexIndex2 = PackVertexIndex(VertexIndex2, value); }
        public bool VertexNormalFlag3 { get => (PackedVertexIndex3 & VertexNormalMask) != 0; set => PackedVertexIndex3 = PackVertexIndex(VertexIndex3, value); }

        public Vector3 Vertex1
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex1) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex1, value); }
        }
        public Vector3 Vertex2
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex2) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex2, value); }
        }
        public Vector3 Vertex3
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex3) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex3, value); }
        }

        public override Vector3 BoxMin
        {
            get
            {
                return Vector3.Min(Vector3.Min(Vertex1, Vertex2), Vertex3);
            }
        }
        public override Vector3 BoxMax
        {
            get
            {
                return Vector3.Max(Vector3.Max(Vertex1, Vertex2), Vertex3);
            }
        }
        public override Vector3 Scale
        {
            get
            {
                if (ScaleCached.HasValue) return ScaleCached.Value;
                ScaleCached = Vector3.One;
                return Vector3.One;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var v3 = Vertex3;
                var cen = (v1 + v2 + v3) * (1.0f / 3.0f);
                var trans = value / Scale;
                var ori = Orientation;
                var orinv = Quaternion.Invert(ori);
                Vertex1 = cen + ori.Multiply(trans * orinv.Multiply(v1 - cen));
                Vertex2 = cen + ori.Multiply(trans * orinv.Multiply(v2 - cen));
                Vertex3 = cen + ori.Multiply(trans * orinv.Multiply(v3 - cen));
                ScaleCached = value;
            }
        }
        public override Vector3 Position
        {
            get
            {
                return (Vertex1 + Vertex2 + Vertex3) * (1.0f / 3.0f);
            }
            set
            {
                var offset = value - Position;
                Vertex1 += offset;
                Vertex2 += offset;
                Vertex3 += offset;
            }
        }
        public override Quaternion Orientation
        {
            get
            {
                if (OrientationCached.HasValue) return OrientationCached.Value;
                var v1 = Vertex1;
                var v2 = Vertex2;
                var v3 = Vertex3;
                var dir = v2 - v1;
                var side = Vector3.Cross((v3 - v1), dir);
                var up = Vector3.Normalize(Vector3.Cross(dir, side));
                var ori = Quaternion.Invert(Quaternion.LookAtRH(Vector3.Zero, side, up));
                OrientationCached = ori;
                return ori;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var v3 = Vertex3;
                var cen = (v1 + v2 + v3) * (1.0f / 3.0f);
                var trans = value * Quaternion.Invert(Orientation);
                Vertex1 = cen + trans.Multiply(v1 - cen);
                Vertex2 = cen + trans.Multiply(v2 - cen);
                Vertex3 = cen + trans.Multiply(v3 - cen);
                OrientationCached = value;
            }
        }
        private Quaternion? OrientationCached;
        private Vector3? ScaleCached;

        public override int[] VertexIndices
        {
            get
            {
                return new[] { VertexIndex1, VertexIndex2, VertexIndex3 };
            }
            set
            {
                if (value?.Length >= 3)
                {
                    VertexIndex1 = value[0];
                    VertexIndex2 = value[1];
                    VertexIndex3 = value[2];
                }
            }
        }
        public override BoundVertexRef NearestVertex(Vector3 p)
        {
            var d1 = (p - Vertex1).Length();
            var d2 = (p - Vertex2).Length();
            var d3 = (p - Vertex3).Length();
            if ((d1 <= d2) && (d1 <= d3)) return new BoundVertexRef(VertexIndex1, d1);
            if (d2 <= d3) return new BoundVertexRef(VertexIndex2, d2);
            return new BoundVertexRef(VertexIndex3, d3);
        }
        public override void GatherVertices(Dictionary<BoundVertex, int> verts)
        {
            if (Owner != null)
            {
                if (Owner.GetVertexObject(VertexIndex1) is { } vertex1) verts[vertex1] = VertexIndex1;
                if (Owner.GetVertexObject(VertexIndex2) is { } vertex2) verts[vertex2] = VertexIndex2;
                if (Owner.GetVertexObject(VertexIndex3) is { } vertex3) verts[vertex3] = VertexIndex3;
            }
        }

        public int GetVertexIndex(int i)
        {
            return i switch
            {
                0 => VertexIndex1,
                1 => VertexIndex2,
                2 => VertexIndex3,
                _ => throw new ArgumentOutOfRangeException(nameof(i)),
            };
        }
        public int GetNeighboringPolygonIndex(int i)
        {
            return i switch
            {
                0 => UnpackNeighboringPolygonIndex(NeighboringPolygonIndex1),
                1 => UnpackNeighboringPolygonIndex(NeighboringPolygonIndex2),
                2 => UnpackNeighboringPolygonIndex(NeighboringPolygonIndex3),
                _ => throw new ArgumentOutOfRangeException(nameof(i)),
            };
        }
        public void SetNeighboringPolygonIndex(int i, int polygonIndex)
        {
            var value = PackNeighboringPolygonIndex(polygonIndex);
            switch (i)
            {
                case 0: NeighboringPolygonIndex1 = value; break;
                case 1: NeighboringPolygonIndex2 = value; break;
                case 2: NeighboringPolygonIndex3 = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(i));
            }
        }
        private static ushort PackVertexIndex(int vertexIndex, bool normalFlag) =>
            (ushort)((vertexIndex & VertexIndexMask) | (normalFlag ? VertexNormalMask : 0));

        public static ushort PackNeighboringPolygonIndex(int polygonIndex)
        {
            return (uint)polygonIndex < ushort.MaxValue ? (ushort)polygonIndex : ushort.MaxValue;
        }
        public static int UnpackNeighboringPolygonIndex(ushort polygonIndex)
        {
            return polygonIndex == ushort.MaxValue ? -1 : polygonIndex;
        }

        public BoundPolygonTriangle()
        {
            Type = BoundPolygonType.Triangle;
        }
        public override void Read(byte[] bytes, int offset)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (offset < 0 || offset > bytes.Length - SerializedSize)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (DecodeType(bytes[offset]) != BoundPolygonType.Triangle)
                throw new InvalidDataException("Primitive data does not contain a polygon triangle.");

            Area = BitConverter.ToSingle(bytes, offset);
            PackedVertexIndex1 = BitConverter.ToUInt16(bytes, offset + 4);
            PackedVertexIndex2 = BitConverter.ToUInt16(bytes, offset + 6);
            PackedVertexIndex3 = BitConverter.ToUInt16(bytes, offset + 8);
            NeighboringPolygonIndex1 = BitConverter.ToUInt16(bytes, offset + 10);
            NeighboringPolygonIndex2 = BitConverter.ToUInt16(bytes, offset + 12);
            NeighboringPolygonIndex3 = BitConverter.ToUInt16(bytes, offset + 14);
        }
        public override void Write(BinaryWriter bw)
        {
            var areaBits = BitConverter.SingleToInt32Bits(Area);
            bw.Write((areaBits & ~0xFF) | EncodeType((byte)areaBits));
            bw.Write(PackedVertexIndex1);
            bw.Write(PackedVertexIndex2);
            bw.Write(PackedVertexIndex3);
            bw.Write(NeighboringPolygonIndex1);
            bw.Write(NeighboringPolygonIndex2);
            bw.Write(NeighboringPolygonIndex3);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            var s = $"{Type} m=\"{MaterialIndex}\" v1=\"{VertexIndex1}\" v2=\"{VertexIndex2}\" v3=\"{VertexIndex3}\" f1=\"{(VertexNormalFlag1 ? 1 : 0)}\" f2=\"{(VertexNormalFlag2 ? 1 : 0)}\" f3=\"{(VertexNormalFlag3 ? 1 : 0)}\"";
            YbnXml.SelfClosingTag(sb, indent, s);
        }
        public override void ReadXml(XmlNode node)
        {
            Material = Owner?.GetMaterialByIndex(Xml.GetIntAttribute(node, "m")) ?? new BoundMaterial_s();
            VertexIndex1 = Xml.GetIntAttribute(node, "v1");
            VertexIndex2 = Xml.GetIntAttribute(node, "v2");
            VertexIndex3 = Xml.GetIntAttribute(node, "v3");
            VertexNormalFlag1 = Xml.GetIntAttribute(node, "f1") != 0;
            VertexNormalFlag2 = Xml.GetIntAttribute(node, "f2") != 0;
            VertexNormalFlag3 = Xml.GetIntAttribute(node, "f3") != 0;
        }
        public override string ToString()
        {
            return base.ToString() + ": " + VertexIndex1 + ", " + VertexIndex2 + ", " + VertexIndex3;
        }
    }
    [TC(typeof(EXP))] public class BoundPolygonSphere : BoundPolygon
    {
        public ushort CenterIndex { get; set; }
        public float Radius { get; set; }

        public override Vector3 BoxMin
        {
            get
            {
                return Position - Radius;
            }
        }
        public override Vector3 BoxMax
        {
            get
            {
                return Position + Radius;
            }
        }
        public override Vector3 Scale
        {
            get
            {
                return new Vector3(Radius);
            }
            set
            {
                Radius = value.X;
            }
        }
        public override Vector3 Position
        {
            get { return (Owner != null) ? Owner.GetVertexPos(CenterIndex) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(CenterIndex, value); }
        }
        public override Quaternion Orientation
        {
            get
            {
                return Quaternion.Identity;
            }
            set
            {
            }
        }

        public override int[] VertexIndices
        {
            get
            {
                return new[] { (int)CenterIndex };
            }
            set
            {
                if (value?.Length >= 1)
                {
                    CenterIndex = checked((ushort)value[0]);
                }
            }
        }
        public override BoundVertexRef NearestVertex(Vector3 p)
        {
            return new BoundVertexRef(CenterIndex, Radius);
        }
        public override void GatherVertices(Dictionary<BoundVertex, int> verts)
        {
            if (Owner != null)
            {
                if (Owner.GetVertexObject(CenterIndex) is { } vertex) verts[vertex] = CenterIndex;
            }
        }

        public BoundPolygonSphere()
        {
            Type = BoundPolygonType.Sphere;
        }
        public override void Read(byte[] bytes, int offset)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (offset < 0 || offset > bytes.Length - SerializedSize)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (DecodeType(bytes[offset]) != BoundPolygonType.Sphere)
                throw new InvalidDataException("Primitive data does not contain a sphere.");

            CenterIndex = BitConverter.ToUInt16(bytes, offset + 2);
            Radius = BitConverter.ToSingle(bytes, offset + 4);
        }
        public override void Write(BinaryWriter bw)
        {
            bw.Write(EncodeType(0));
            bw.Write((byte)0);
            bw.Write(CenterIndex);
            bw.Write(Radius);
            bw.Write(0ul);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            var s = $"{Type} m=\"{MaterialIndex}\" v=\"{CenterIndex}\" radius=\"{FloatUtil.ToString(Radius)}\"";
            YbnXml.SelfClosingTag(sb, indent, s);
        }
        public override void ReadXml(XmlNode node)
        {
            Material = Owner?.GetMaterialByIndex(Xml.GetIntAttribute(node, "m")) ?? new BoundMaterial_s();
            CenterIndex = checked((ushort)Xml.GetUIntAttribute(node, "v"));
            Radius = Xml.GetFloatAttribute(node, "radius");
        }
        public override string ToString()
        {
            return base.ToString() + ": " + CenterIndex + ", " + Radius;
        }
    }
    [TC(typeof(EXP))] public class BoundPolygonCapsule : BoundPolygon
    {
        public ushort EndIndex0 { get; set; }
        public float Radius { get; set; }
        public ushort EndIndex1 { get; set; }

        public Vector3 Vertex1
        {
            get { return (Owner != null) ? Owner.GetVertexPos(EndIndex0) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(EndIndex0, value); }
        }
        public Vector3 Vertex2
        {
            get { return (Owner != null) ? Owner.GetVertexPos(EndIndex1) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(EndIndex1, value); }
        }

        public override Vector3 BoxMin
        {
            get
            {
                return Vector3.Min(Vertex1, Vertex2) - Radius;
            }
        }
        public override Vector3 BoxMax
        {
            get
            {
                return Vector3.Max(Vertex1, Vertex2) + Radius;
            }
        }
        public override Vector3 Scale
        {
            get
            {
                if (ScaleCached.HasValue) return ScaleCached.Value;
                ScaleCached = Vector3.One;
                return Vector3.One;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var cen = (v1 + v2) * 0.5f;
                var trans = value / Scale;
                var ori = Orientation;
                var orinv = Quaternion.Invert(ori);
                Vertex1 = cen + ori.Multiply(trans * orinv.Multiply(v1 - cen));
                Vertex2 = cen + ori.Multiply(trans * orinv.Multiply(v2 - cen));
                Radius = trans.X * Radius;
                ScaleCached = value;
            }
        }
        public override Vector3 Position
        {
            get
            {
                return (Vertex1 + Vertex2) * 0.5f;
            }
            set
            {
                var offset = value - Position;
                Vertex1 += offset;
                Vertex2 += offset;
            }
        }
        public override Quaternion Orientation
        {
            get
            {
                if (OrientationCached.HasValue) return OrientationCached.Value;
                var v1 = Vertex1;
                var v2 = Vertex2;
                var dir = v2 - v1;
                var up = Vector3.Normalize(dir.GetPerpVec());
                var ori = Quaternion.Invert(Quaternion.LookAtRH(Vector3.Zero, dir, up));
                OrientationCached = ori;
                return ori;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var cen = (v1 + v2) * 0.5f;
                var trans = value * Quaternion.Invert(Orientation);
                Vertex1 = cen + trans.Multiply(v1 - cen);
                Vertex2 = cen + trans.Multiply(v2 - cen);
                OrientationCached = value;
            }
        }
        private Quaternion? OrientationCached;
        private Vector3? ScaleCached;

        public override int[] VertexIndices
        {
            get
            {
                return new[] { (int)EndIndex0, (int)EndIndex1 };
            }
            set
            {
                if (value?.Length >= 2)
                {
                    EndIndex0 = checked((ushort)value[0]);
                    EndIndex1 = checked((ushort)value[1]);
                }
            }
        }
        public override BoundVertexRef NearestVertex(Vector3 p)
        {
            var d1 = (p - Vertex1).Length();
            var d2 = (p - Vertex2).Length();
            if (d1 <= d2) return new BoundVertexRef(EndIndex0, d1);
            return new BoundVertexRef(EndIndex1, d2);
        }
        public override void GatherVertices(Dictionary<BoundVertex, int> verts)
        {
            if (Owner != null)
            {
                if (Owner.GetVertexObject(EndIndex0) is { } vertex0) verts[vertex0] = EndIndex0;
                if (Owner.GetVertexObject(EndIndex1) is { } vertex1) verts[vertex1] = EndIndex1;
            }
        }

        public BoundPolygonCapsule()
        {
            Type = BoundPolygonType.Capsule;
        }
        public override void Read(byte[] bytes, int offset)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (offset < 0 || offset > bytes.Length - SerializedSize)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (DecodeType(bytes[offset]) != BoundPolygonType.Capsule)
                throw new InvalidDataException("Primitive data does not contain a capsule.");

            EndIndex0 = BitConverter.ToUInt16(bytes, offset + 2);
            Radius = BitConverter.ToSingle(bytes, offset + 4);
            EndIndex1 = BitConverter.ToUInt16(bytes, offset + 8);
        }
        public override void Write(BinaryWriter bw)
        {
            bw.Write(EncodeType(0));
            bw.Write((byte)0);
            bw.Write(EndIndex0);
            bw.Write(Radius);
            bw.Write(EndIndex1);
            bw.Write((ushort)0);
            bw.Write(0u);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            var s = $"{Type} m=\"{MaterialIndex}\" v1=\"{EndIndex0}\" v2=\"{EndIndex1}\" radius=\"{FloatUtil.ToString(Radius)}\"";
            YbnXml.SelfClosingTag(sb, indent, s);
        }
        public override void ReadXml(XmlNode node)
        {
            Material = Owner?.GetMaterialByIndex(Xml.GetIntAttribute(node, "m")) ?? new BoundMaterial_s();
            EndIndex0 = checked((ushort)Xml.GetUIntAttribute(node, "v1"));
            EndIndex1 = checked((ushort)Xml.GetUIntAttribute(node, "v2"));
            Radius = Xml.GetFloatAttribute(node, "radius");
        }
        public override string ToString()
        {
            return base.ToString() + ": " + EndIndex0 + ", " + EndIndex1 + ", " + Radius;
        }
    }
    [TC(typeof(EXP))] public class BoundPolygonBox : BoundPolygon
    {
        public ushort VertexIndex0 { get; set; }
        public ushort VertexIndex1 { get; set; }
        public ushort VertexIndex2 { get; set; }
        public ushort VertexIndex3 { get; set; }

        public Vector3 Vertex1
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex0) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex0, value); }
        }
        public Vector3 Vertex2
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex1) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex1, value); }
        }
        public Vector3 Vertex3
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex2) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex2, value); }
        }
        public Vector3 Vertex4
        {
            get { return (Owner != null) ? Owner.GetVertexPos(VertexIndex3) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(VertexIndex3, value); }
        }

        public override Vector3 BoxMin
        {
            get
            {
                return Vector3.Min(Vector3.Min(Vector3.Min(Vertex1, Vertex2), Vertex3), Vertex4);
            }
        }
        public override Vector3 BoxMax
        {
            get
            {
                return Vector3.Max(Vector3.Max(Vector3.Max(Vertex1, Vertex2), Vertex3), Vertex4);
            }
        }
        public override Vector3 Scale
        {
            get
            {
                if (ScaleCached.HasValue) return ScaleCached.Value;
                ScaleCached = Vector3.One;
                return Vector3.One;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var v3 = Vertex3;
                var v4 = Vertex4;
                var cen = (v1 + v2 + v3 + v4) * 0.25f;
                var trans = value / Scale;
                var ori = Orientation;
                var orinv = Quaternion.Invert(ori);
                Vertex1 = cen + ori.Multiply(trans * orinv.Multiply(v1 - cen));
                Vertex2 = cen + ori.Multiply(trans * orinv.Multiply(v2 - cen));
                Vertex3 = cen + ori.Multiply(trans * orinv.Multiply(v3 - cen));
                Vertex4 = cen + ori.Multiply(trans * orinv.Multiply(v4 - cen));
                ScaleCached = value;
            }
        }
        public override Vector3 Position
        {
            get
            {
                return (Vertex1 + Vertex2 + Vertex3 + Vertex4) * 0.25f;
            }
            set
            {
                var offset = value - Position;
                Vertex1 += offset;
                Vertex2 += offset;
                Vertex3 += offset;
                Vertex4 += offset;
            }
        }
        public override Quaternion Orientation
        {
            get
            {
                if (OrientationCached.HasValue) return OrientationCached.Value;
                var v1 = Vertex1;
                var v2 = Vertex2;
                var v3 = Vertex3;
                var v4 = Vertex4;
                var dir = (v1+v4) - (v2+v3);
                var up = Vector3.Normalize((v3+v4) - (v1+v2));
                var ori = Quaternion.Invert(Quaternion.LookAtRH(Vector3.Zero, dir, up));
                OrientationCached = ori;
                return ori;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var v3 = Vertex3;
                var v4 = Vertex4;
                var cen = (v1 + v2 + v3 + v4) * 0.25f;
                var trans = value * Quaternion.Invert(Orientation);
                Vertex1 = cen + trans.Multiply(v1 - cen);
                Vertex2 = cen + trans.Multiply(v2 - cen);
                Vertex3 = cen + trans.Multiply(v3 - cen);
                Vertex4 = cen + trans.Multiply(v4 - cen);
                OrientationCached = value;
            }
        }
        private Quaternion? OrientationCached;
        private Vector3? ScaleCached;

        public override int[] VertexIndices
        {
            get
            {
                return new[] { (int)VertexIndex0, (int)VertexIndex1, (int)VertexIndex2, (int)VertexIndex3 };
            }
            set
            {
                if (value?.Length >= 4)
                {
                    VertexIndex0 = checked((ushort)value[0]);
                    VertexIndex1 = checked((ushort)value[1]);
                    VertexIndex2 = checked((ushort)value[2]);
                    VertexIndex3 = checked((ushort)value[3]);
                }
            }
        }
        public int GetVertexIndex(int index)
        {
            return index switch
            {
                0 => VertexIndex0,
                1 => VertexIndex1,
                2 => VertexIndex2,
                3 => VertexIndex3,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
        }
        public override BoundVertexRef NearestVertex(Vector3 p)
        {
            var d1 = (p - Vertex1).Length();
            var d2 = (p - Vertex2).Length();
            var d3 = (p - Vertex3).Length();
            var d4 = (p - Vertex4).Length();
            if ((d1 <= d2) && (d1 <= d3) && (d1 <= d4)) return new BoundVertexRef(VertexIndex0, d1);
            if ((d2 <= d3) && (d2 <= d4)) return new BoundVertexRef(VertexIndex1, d2);
            if (d3 <= d4) return new BoundVertexRef(VertexIndex2, d3);
            return new BoundVertexRef(VertexIndex3, d4);
        }
        public override void GatherVertices(Dictionary<BoundVertex, int> verts)
        {
            if (Owner != null)
            {
                if (Owner.GetVertexObject(VertexIndex0) is { } vertex0) verts[vertex0] = VertexIndex0;
                if (Owner.GetVertexObject(VertexIndex1) is { } vertex1) verts[vertex1] = VertexIndex1;
                if (Owner.GetVertexObject(VertexIndex2) is { } vertex2) verts[vertex2] = VertexIndex2;
                if (Owner.GetVertexObject(VertexIndex3) is { } vertex3) verts[vertex3] = VertexIndex3;
            }
        }

        public BoundPolygonBox()
        {
            Type = BoundPolygonType.Box;
        }
        public override void Read(byte[] bytes, int offset)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if ((offset < 0) || (offset > bytes.Length - SerializedSize))
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (DecodeType(bytes[offset]) != BoundPolygonType.Box)
                throw new InvalidDataException("Primitive data does not contain a box.");

            VertexIndex0 = BitConverter.ToUInt16(bytes, offset + 4);
            VertexIndex1 = BitConverter.ToUInt16(bytes, offset + 6);
            VertexIndex2 = BitConverter.ToUInt16(bytes, offset + 8);
            VertexIndex3 = BitConverter.ToUInt16(bytes, offset + 10);
        }
        public override void Write(BinaryWriter bw)
        {
            bw.Write(EncodeType(0));
            bw.Write((byte)0);
            bw.Write((ushort)0);
            bw.Write(VertexIndex0);
            bw.Write(VertexIndex1);
            bw.Write(VertexIndex2);
            bw.Write(VertexIndex3);
            bw.Write(0u);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            var s = $"{Type} m=\"{MaterialIndex}\" v1=\"{VertexIndex0}\" v2=\"{VertexIndex1}\" v3=\"{VertexIndex2}\" v4=\"{VertexIndex3}\"";
            YbnXml.SelfClosingTag(sb, indent, s);
        }
        public override void ReadXml(XmlNode node)
        {
            Material = Owner?.GetMaterialByIndex(Xml.GetIntAttribute(node, "m")) ?? new BoundMaterial_s();
            VertexIndex0 = checked((ushort)Xml.GetUIntAttribute(node, "v1"));
            VertexIndex1 = checked((ushort)Xml.GetUIntAttribute(node, "v2"));
            VertexIndex2 = checked((ushort)Xml.GetUIntAttribute(node, "v3"));
            VertexIndex3 = checked((ushort)Xml.GetUIntAttribute(node, "v4"));
        }
        public override string ToString()
        {
            return base.ToString() + ": " + VertexIndex0 + ", " + VertexIndex1 + ", " + VertexIndex2 + ", " + VertexIndex3;
        }
    }
    [TC(typeof(EXP))] public class BoundPolygonCylinder : BoundPolygon
    {
        public ushort EndIndex0 { get; set; }
        public float Radius { get; set; }
        public ushort EndIndex1 { get; set; }

        public Vector3 Vertex1
        {
            get { return (Owner != null) ? Owner.GetVertexPos(EndIndex0) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(EndIndex0, value); }
        }
        public Vector3 Vertex2
        {
            get { return (Owner != null) ? Owner.GetVertexPos(EndIndex1) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(EndIndex1, value); }
        }

        public override Vector3 BoxMin
        {
            get
            {
                return Vector3.Min(Vertex1, Vertex2) - Radius;//not perfect but meh
            }
        }
        public override Vector3 BoxMax
        {
            get
            {
                return Vector3.Max(Vertex1, Vertex2) + Radius;//not perfect but meh
            }
        }
        public override Vector3 Scale
        {
            get
            {
                if (ScaleCached.HasValue) return ScaleCached.Value;
                ScaleCached = Vector3.One;
                return Vector3.One;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var cen = (v1 + v2) * 0.5f;
                var trans = value / Scale;
                var ori = Orientation;
                var orinv = Quaternion.Invert(ori);
                Vertex1 = cen + ori.Multiply(trans * orinv.Multiply(v1 - cen));
                Vertex2 = cen + ori.Multiply(trans * orinv.Multiply(v2 - cen));
                Radius = trans.X * Radius;
                ScaleCached = value;
            }
        }
        public override Vector3 Position
        {
            get
            {
                return (Vertex1 + Vertex2) * 0.5f;
            }
            set
            {
                var offset = value - Position;
                Vertex1 += offset;
                Vertex2 += offset;
            }
        }
        public override Quaternion Orientation
        {
            get
            {
                if (OrientationCached.HasValue) return OrientationCached.Value;
                var v1 = Vertex1;
                var v2 = Vertex2;
                var dir = v2 - v1;
                var up = Vector3.Normalize(dir.GetPerpVec());
                var ori = Quaternion.Invert(Quaternion.LookAtRH(Vector3.Zero, dir, up));
                OrientationCached = ori;
                return ori;
            }
            set
            {
                var v1 = Vertex1;
                var v2 = Vertex2;
                var cen = (v1 + v2) * 0.5f;
                var trans = value * Quaternion.Invert(Orientation);
                Vertex1 = cen + trans.Multiply(v1 - cen);
                Vertex2 = cen + trans.Multiply(v2 - cen);
                OrientationCached = value;
            }
        }
        private Quaternion? OrientationCached;
        private Vector3? ScaleCached;

        public override int[] VertexIndices
        {
            get
            {
                return new[] { (int)EndIndex0, (int)EndIndex1 };
            }
            set
            {
                if (value?.Length >= 2)
                {
                    EndIndex0 = checked((ushort)value[0]);
                    EndIndex1 = checked((ushort)value[1]);
                }
            }
        }
        public override BoundVertexRef NearestVertex(Vector3 p)
        {
            var d1 = (p - Vertex1).Length();
            var d2 = (p - Vertex2).Length();
            if (d1 <= d2) return new BoundVertexRef(EndIndex0, d1);
            return new BoundVertexRef(EndIndex1, d2);
        }
        public override void GatherVertices(Dictionary<BoundVertex, int> verts)
        {
            if (Owner != null)
            {
                if (Owner.GetVertexObject(EndIndex0) is { } vertex0) verts[vertex0] = EndIndex0;
                if (Owner.GetVertexObject(EndIndex1) is { } vertex1) verts[vertex1] = EndIndex1;
            }
        }

        public BoundPolygonCylinder()
        {
            Type = BoundPolygonType.Cylinder;
        }
        public override void Read(byte[] bytes, int offset)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if ((offset < 0) || (offset > bytes.Length - SerializedSize))
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (DecodeType(bytes[offset]) != BoundPolygonType.Cylinder)
                throw new InvalidDataException("Primitive data does not contain a cylinder.");

            EndIndex0 = BitConverter.ToUInt16(bytes, offset + 2);
            Radius = BitConverter.ToSingle(bytes, offset + 4);
            EndIndex1 = BitConverter.ToUInt16(bytes, offset + 8);
        }
        public override void Write(BinaryWriter bw)
        {
            bw.Write(EncodeType(0));
            bw.Write((byte)0);
            bw.Write(EndIndex0);
            bw.Write(Radius);
            bw.Write(EndIndex1);
            bw.Write((ushort)0);
            bw.Write(0u);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            var s = $"{Type} m=\"{MaterialIndex}\" v1=\"{EndIndex0}\" v2=\"{EndIndex1}\" radius=\"{FloatUtil.ToString(Radius)}\"";
            YbnXml.SelfClosingTag(sb, indent, s);
        }
        public override void ReadXml(XmlNode node)
        {
            Material = Owner?.GetMaterialByIndex(Xml.GetIntAttribute(node, "m")) ?? new BoundMaterial_s();
            EndIndex0 = checked((ushort)Xml.GetUIntAttribute(node, "v1"));
            EndIndex1 = checked((ushort)Xml.GetUIntAttribute(node, "v2"));
            Radius = Xml.GetFloatAttribute(node, "radius");
        }
        public override string ToString()
        {
            return base.ToString() + ": " + EndIndex0 + ", " + EndIndex1 + ", " + Radius;
        }
    }


    [TC(typeof(EXP))] public struct BoundEdgeRef //convenience struct for updating edge indices
    {
        public int Vertex1 { get; set; }
        public int Vertex2 { get; set; }

        public BoundEdgeRef(int i1, int i2)
        {
            Vertex1 = Math.Min(i1, i2);
            Vertex2 = Math.Max(i1, i2);
        }
    }
    [TC(typeof(EXP))] public class BoundEdge //convenience class for updating edge indices
    {
        public BoundPolygonTriangle Triangle1 { get; set; }
        public BoundPolygonTriangle? Triangle2 { get; set; }
        public int EdgeID1 { get; set; }
        public int EdgeID2 { get; set; }

        public BoundEdge(BoundPolygonTriangle t1, int e1)
        {
            Triangle1 = t1;
            EdgeID1 = e1;
        }
    }

    [TC(typeof(EXP))] public struct BoundVertexRef //convenience struct for BoundPolygon.NearestVertex and SpaceRayIntersectResult
    {
        public int Index { get; set; }
        public float Distance { get; set; }

        public BoundVertexRef(int index, float dist)
        {
            Index = index;
            Distance = dist;
        }
    }
    [TC(typeof(EXP))] public class BoundVertex //class for editing convenience, to hold a reference to a BoundGeometry vertex
    {
        public BoundGeometry? Owner { get; set; }
        public int Index { get; set; }

        public Vector3 Position
        {
            get { return (Owner != null) ? Owner.GetVertexPos(Index) : Vector3.Zero; }
            set { if (Owner != null) Owner.SetVertexPos(Index, value); }
        }
        public BoundMaterialColour Colour
        {
            get { return (Owner != null) ? Owner.GetVertexColour(Index) : new BoundMaterialColour(); }
            set { if (Owner != null) Owner.SetVertexColour(Index, value); }
        }

        public BoundVertex(BoundGeometry owner, int index)
        {
            Owner = owner;
            Index = index;
        }

        public virtual string Title
        {
            get
            {
                return "Vertex " + Index.ToString();
            }
        }
    }

    [TC(typeof(EXP))] public struct BoundVertex_s
    {
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }

        public BoundVertex_s(Vector3 v)
        {
            X = (short)Math.Min(Math.Max(v.X, -32767f), 32767f);
            Y = (short)Math.Min(Math.Max(v.Y, -32767f), 32767f);
            Z = (short)Math.Min(Math.Max(v.Z, -32767f), 32767f);
        }

        public Vector3 Vector
        {
            get { return new Vector3(X, Y, Z); }
            set
            {
                X = (short)Math.Min(Math.Max(value.X, -32767f), 32767f);
                Y = (short)Math.Min(Math.Max(value.Y, -32767f), 32767f);
                Z = (short)Math.Min(Math.Max(value.Z, -32767f), 32767f);
            }
        }
    }

    [TC(typeof(EXP))] public class BoundGeomOctants : ResourceSystemBlock
    {
        public uint[] Counts { get; set; } = new uint[8];
        public uint[][] Items { get; private set; } = new uint[8][];


        public override long BlockLength
        {
            get
            {
                long len = 128; // (8*(4 + 8)) + 32
                for (int i = 0; i < 8; i++)
                {
                    len += (Counts[i] * 4);
                }
                return len;
            }
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            if ((parameters?.Length ?? 0) < 1)
            { return; } //shouldn't happen!

            ulong ptr = (ulong)(parameters?[0] ?? throw new ArgumentException("An octant pointer is required.", nameof(parameters))); //pointer array pointer

            for (int i = 0; i < 8; i++)
            {
                Counts[i] = reader.ReadUInt32();
            }

            ulong[] ptrlist = reader.ReadUlongsAt(ptr, 8, false) ?? throw new InvalidDataException("The octant pointer table is missing.");

            for (int i = 0; i < 8; i++)
            {
                Items[i] = reader.ReadUintsAt(ptrlist[i], Counts[i], false) ?? [];
            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            var ptr = writer.Position + 96;
            for (int i = 0; i < 8; i++)
            {
                writer.Write(Counts[i]);
            }
            for (int i = 0; i < 8; i++)
            {
                writer.Write((ulong)ptr);
                ptr += (Counts[i] * 4);
            }
            for (int i = 0; i < 8; i++)
            {
                var items = (i < Items.Length) ? Items[i] : null;
                if (items == null)
                { continue; }
                var c = Counts[i];
                for (int n = 0; n < c; n++)
                {
                    var v = (n < items.Length) ? items[n] : 0;
                    writer.Write(v);
                }
            }
            writer.Write(new byte[32]);
        }

        public void UpdateCounts()
        {
            for (int i = 0; i < 8; i++)
            {
                Counts[i] = (Items != null && i < Items.Length) ? (uint)(Items[i]?.Length ?? 0) : 0;
            }
        }


    }


    [Flags] public enum EBoundCompositeFlags : uint
    {
        NONE = 0u,
        UNKNOWN = 1u,
        MAP_WEAPON = 1u << 1,
        MAP_DYNAMIC = 1u << 2,
        MAP_ANIMAL = 1u << 3,
        MAP_COVER = 1u << 4,
        MAP_VEHICLE = 1u << 5,
        VEHICLE_NOT_BVH = 1u << 6,
        VEHICLE_BVH = 1u << 7,
        VEHICLE_BOX = 1u << 8,
        PED = 1u << 9,
        RAGDOLL = 1u << 10,
        ANIMAL = 1u << 11,
        ANIMAL_RAGDOLL = 1u << 12,
        OBJECT = 1u << 13,
        OBJECT_ENV_CLOTH = 1u << 14,
        PLANT = 1u << 15,
        PROJECTILE = 1u << 16,
        EXPLOSION = 1u << 17,
        PICKUP = 1u << 18,
        FOLIAGE = 1u << 19,
        FORKLIFT_FORKS = 1u << 20,
        TEST_WEAPON = 1u << 21,
        TEST_CAMERA = 1u << 22,
        TEST_AI = 1u << 23,
        TEST_SCRIPT = 1u << 24,
        TEST_VEHICLE_WHEEL = 1u << 25,
        GLASS = 1u << 26,
        MAP_RIVER = 1u << 27,
        SMOKE = 1u << 28,
        UNSMASHED = 1u << 29,
        MAP_STAIRS = 1u << 30,
        MAP_DEEP_SURFACE = 1u << 31,
    }
    [TC(typeof(EXP))] public struct BoundCompositeChildrenFlags
    {
        public EBoundCompositeFlags Flags1 { get; set; }
        public EBoundCompositeFlags Flags2 { get; set; }
        public override string ToString()
        {
            return Flags1.ToString() + ", " + Flags2.ToString();
        }
    }



    [TC(typeof(EXP))] public class BVH : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 128; }
        }

        // structure data
        public ResourceSimpleList64b_s<BVHNode_s> ContiguousNodes { get; set; } = new();
        public Vector3 AABBMin { get; set; }
        public Vector3 AABBMax { get; set; }
        public Vector3 AABBCenter { get; set; }
        public Vector3 Quantize { get; set; }
        public Vector3 InvQuantize { get; set; }
        public ResourceSimpleList64_s<BVHTreeInfo_s> SubtreeHeaders { get; set; } = new();

        /// <summary>
        /// Reads the data-block from a stream.
        /// </summary>
        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            ContiguousNodes = reader.ReadRequiredBlock<ResourceSimpleList64b_s<BVHNode_s>>();
            reader.Position += 16;
            AABBMin = reader.ReadVector3();
            reader.Position += 4;
            AABBMax = reader.ReadVector3();
            reader.Position += 4;
            AABBCenter = reader.ReadVector3();
            reader.Position += 4;
            Quantize = reader.ReadVector3();
            reader.Position += 4;
            InvQuantize = reader.ReadVector3();
            reader.Position += 4;
            SubtreeHeaders = reader.ReadRequiredBlock<ResourceSimpleList64_s<BVHTreeInfo_s>>();
        }

        /// <summary>
        /// Writes the data-block to a stream.
        /// </summary>
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {

            // write structure data
            writer.WriteBlock(ContiguousNodes);
            writer.Write(new byte[16]);
            writer.Write(AABBMin);
            writer.Write(0u);
            writer.Write(AABBMax);
            writer.Write(0u);
            writer.Write(AABBCenter);
            writer.Write(0u);
            writer.Write(Quantize);
            writer.Write(0u);
            writer.Write(InvQuantize);
            writer.Write(0u);
            writer.WriteBlock(SubtreeHeaders);
        }

        /// <summary>
        /// Returns a list of data blocks which are referenced by this block.
        /// </summary>
        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            //if (Nodes != null) list.Add(Nodes);
            //if (Trees != null) list.Add(Trees);
            return list.ToArray();
        }

        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            return new Tuple<long, IResourceBlock>[] {
                new Tuple<long, IResourceBlock>(0x0, ContiguousNodes),
                new Tuple<long, IResourceBlock>(0x70, SubtreeHeaders)
            };
        }
    }
    [TC(typeof(EXP))] public struct BVHTreeInfo_s
    {
        public short MinX { get; set; }
        public short MinY { get; set; }
        public short MinZ { get; set; }
        public short MaxX { get; set; }
        public short MaxY { get; set; }
        public short MaxZ { get; set; }
        public short NodeIndex1 { get; set; }
        public short NodeIndex2 { get; set; }

        public Vector3 Min
        {
            get { return new Vector3(MinX, MinY, MinZ); }
            set { MinX = (short)value.X; MinY = (short)value.Y; MinZ = (short)value.Z; }
        }
        public Vector3 Max
        {
            get { return new Vector3(MaxX, MaxY, MaxZ); }
            set { MaxX = (short)value.X; MaxY = (short)value.Y; MaxZ = (short)value.Z; }
        }

        public override string ToString()
        {
            return NodeIndex1.ToString() + ", " + NodeIndex2.ToString() + "  (" + (NodeIndex2 - NodeIndex1).ToString() + " nodes)";
        }
    }
    [TC(typeof(EXP))] public struct BVHNode_s
    {
        public short MinX { get; set; }
        public short MinY { get; set; }
        public short MinZ { get; set; }
        public short MaxX { get; set; }
        public short MaxY { get; set; }
        public short MaxZ { get; set; }
        public short ItemId { get; set; }
        public short ItemCount { get; set; }

        public Vector3 Min
        {
            get { return new Vector3(MinX, MinY, MinZ); }
            set { MinX = (short)value.X; MinY = (short)value.Y; MinZ = (short)value.Z; }
        }
        public Vector3 Max
        {
            get { return new Vector3(MaxX, MaxY, MaxZ); }
            set { MaxX = (short)value.X; MaxY = (short)value.Y; MaxZ = (short)value.Z; }
        }

        public override string ToString()
        {
            return ItemId.ToString() + ": " + ItemCount.ToString();
        }
    }


    public class BVHBuilder
    {
        public static int MaxNodeItemCount = 4; //item threshold: 1 for composites, 4 for geometries
        public static int MaxTreeNodeCount = 127; //max number of nodes found in any tree


        [return: NotNullIfNotNull(nameof(items))]
        public static BVH? Build(List<BVHBuilderItem?>? items, int itemThreshold)
        {
            if (items == null) return null;
            var bvh = new BVH();
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            var nodes = new List<BVHBuilderNode>();
            var trees = new List<BVHBuilderNode>();
            var iteml = new List<BVHBuilderItem>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                iteml.Add(item);
                min = Vector3.Min(min, item.Min);
                max = Vector3.Max(max, item.Max);
            }
            var cen = (min + max) * 0.5f;
            bvh.AABBMin = min;
            bvh.AABBMax = max;
            bvh.AABBCenter = cen;
            bvh.InvQuantize = Vector3.Max((min - cen).Abs(), (max - cen).Abs()) / 32767.0f;
            bvh.Quantize = 1.0f / bvh.InvQuantize;

            var root = new BVHBuilderNode();
            root.Items = iteml.ToList();
            root.Build(itemThreshold);
            root.GatherNodes(nodes);
            root.GatherTrees(trees);


            if (itemThreshold > 1) //need to reorder items, since they need to be grouped by node for the node's item index
            {
                items.Clear();
                foreach (var node in nodes)
                {
                    if (node.Items != null)
                    {
                        foreach (var item in node.Items)
                        {
                            item.Index = items.Count;
                            items.Add(item);
                        }
                    }
                }
            }
            else //don't need to reorder items, since nodes only have one item and one item index
            { }

            var bvhtrees = new List<BVHTreeInfo_s>();
            var bvhnodes = new List<BVHNode_s>();
            var qi = bvh.Quantize;
            var c = bvh.AABBCenter;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var id = (node.Items is { Count: > 0 }) ? node.Items[0].Index : 0;
                var tn = node.TotalNodes;
                var bn = new BVHNode_s();
                bn.Min = (node.Min - c) * qi;
                bn.Max = (node.Max - c) * qi;
                bn.ItemCount = (short)((tn <= 1) ? node.TotalItems : 0);
                bn.ItemId = (short)((tn <= 1) ? id : node.TotalNodes);
                bvhnodes.Add(bn);
            }

            for (int i = 0; i < trees.Count; i++)
            {
                var tree = trees[i];
                var bt = new BVHTreeInfo_s();
                bt.Min = (tree.Min - c) * qi;
                bt.Max = (tree.Max - c) * qi;
                bt.NodeIndex1 = (short)tree.Index;
                bt.NodeIndex2 = (short)(tree.Index + tree.TotalNodes);
                bvhtrees.Add(bt);
            }


            var nodecount = bvhnodes.Count;
            if (itemThreshold <= 1) //for composites, capacity needs to be (numchildren*2)+1, with empty nodes filling up the space..
            {
                var capacity = (items.Count * 2) + 1;
                var emptynode = new BVHNode_s();
                emptynode.ItemId = 1;
                while (bvhnodes.Count < capacity)
                {
                    bvhnodes.Add(emptynode);
                }
            }

            bvh.ContiguousNodes = new ResourceSimpleList64b_s<BVHNode_s>();
            bvh.ContiguousNodes.data_items = bvhnodes.ToArray();
            bvh.ContiguousNodes.EntriesCount = (uint)nodecount;

            bvh.SubtreeHeaders = new ResourceSimpleList64_s<BVHTreeInfo_s>();
            bvh.SubtreeHeaders.data_items = bvhtrees.ToArray();

            return bvh;
        }

        public static BVHBuilderNode[] Unbuild(BVH? bvh)
        {
            if ((bvh?.SubtreeHeaders?.data_items == null) || (bvh?.ContiguousNodes?.data_items == null)) return [];

            var nodes = new List<BVHBuilderNode>();
            foreach (var tree in bvh.SubtreeHeaders.data_items)
            {
                var bnode = new BVHBuilderNode();
                bnode.Unbuild(bvh, tree.NodeIndex1, tree.NodeIndex2);
                nodes.Add(bnode);
                //MaxTreeNodeCount = Math.Max(MaxTreeNodeCount, tree.NodeIndex2 - tree.NodeIndex1);
            }
            return nodes.ToArray();
        }

    }
    public class BVHBuilderNode
    {
        public List<BVHBuilderNode>? Children;
        public List<BVHBuilderItem>? Items;
        public Vector3 Min;
        public Vector3 Max;
        public int Index;

        public int TotalNodes
        {
            get
            {
                int c = 1;
                if (Children != null)
                {
                    foreach (var child in Children)
                    {
                        c += child.TotalNodes;
                    }
                }
                return c;
            }
        }
        public int TotalItems
        {
            get
            {
                int c = Items?.Count ?? 0;
                if (Children != null)
                {
                    foreach (var child in Children)
                    {
                        c += child.TotalItems;
                    }
                }
                return c;
            }
        }

        public void Build(int itemThreshold)
        {
            UpdateMinMax();
            if (Items == null) return;
            if (Items.Count <= itemThreshold) return;

            var avgsum = Vector3.Zero;
            foreach (var item in Items)
            {
                avgsum += item.Min;
                avgsum += item.Max;
            }
            var avg = avgsum * (0.5f / Items.Count);
            int countx = 0, county = 0, countz = 0;
            foreach (var item in Items)
            {
                var icen = (item.Min + item.Max) * 0.5f;
                if (icen.X < avg.X) countx++;
                if (icen.Y < avg.Y) county++;
                if (icen.Z < avg.Z) countz++;
            }
            var target = Items.Count / 2.0f;
            var dx = Math.Abs(target - countx);
            var dy = Math.Abs(target - county);
            var dz = Math.Abs(target - countz);
            int axis = -1;
            if ((dx <= dy) && (dx <= dz)) axis = 0; //x seems best
            else if (dy <= dz) axis = 1; //y seems best
            else axis = 2; //z seems best

            var l1 = new List<BVHBuilderItem>();
            var l2 = new List<BVHBuilderItem>();
            foreach (var item in Items)
            {
                var icen = (item.Min + item.Max) * 0.5f;
                bool s = false;
                switch (axis)
                {
                    default:
                    case 0: s = (icen.X > avg.X); break;
                    case 1: s = (icen.Y > avg.Y); break;
                    case 2: s = (icen.Z > avg.Z); break;
                }
                if (s) l1.Add(item);
                else l2.Add(item);
            }

            if ((l1.Count == 0) || (l2.Count == 0)) //don't get stuck in a stack overflow...
            {
                var l3 = new List<BVHBuilderItem>();//we can recover from this...
                l3.AddRange(l1);
                l3.AddRange(l2);
                if (l3.Count > 0)
                {
                    l3.Sort((a, b) =>
                    {
                        var c = a.Min.CompareTo(b.Min); if (c != 0) return c;
                        return a.Max.CompareTo(b.Max);
                    });
                    l1.Clear();
                    l2.Clear();
                    var hidx = l3.Count / 2;
                    for (int i = 0; i < hidx; i++) l1.Add(l3[i]);
                    for (int i = hidx; i < l3.Count; i++) l2.Add(l3[i]);
                }
                else
                { return; }//nothing to see here?
            }

            Items = null;
            Children = new List<BVHBuilderNode>();

            var n1 = new BVHBuilderNode();
            n1.Items = l1;
            n1.Build(itemThreshold);
            Children.Add(n1);

            var n2 = new BVHBuilderNode();
            n2.Items = l2;
            n2.Build(itemThreshold);
            Children.Add(n2);

            Children.Sort((a, b) =>
            {
                return b.TotalItems.CompareTo(a.TotalItems);
            }); //is this necessary?

        }
        public void UpdateMinMax()
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    min = Vector3.Min(min, item.Min);
                    max = Vector3.Max(max, item.Max);
                }
            }
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    child.UpdateMinMax();
                    min = Vector3.Min(min, child.Min);
                    max = Vector3.Max(max, child.Max);
                }
            }
            Min = min;
            Max = max;
        }
        public void GatherNodes(List<BVHBuilderNode> nodes)
        {
            Index = nodes.Count;
            nodes.Add(this);
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    child.GatherNodes(nodes);
                }
            }
        }
        public void GatherTrees(List<BVHBuilderNode> trees)
        {
            if ((TotalNodes > BVHBuilder.MaxTreeNodeCount) && (Children is { Count: > 0 }))
            {
                foreach (var child in Children)
                {
                    child.GatherTrees(trees);
                }
            }
            else
            {
                trees.Add(this);
            }
        }

        public void Unbuild(BVH bvh, int nodeIndex1, int nodeIndex2)
        {
            var q = bvh.InvQuantize;
            var c = bvh.AABBCenter;
            int nodeind = nodeIndex1;
            int lastind = nodeIndex2;
            while (nodeind < lastind)
            {
                var node = bvh.ContiguousNodes.data_items[nodeind];
                if (node.ItemCount <= 0) //intermediate node with child nodes
                {
                    Children = new List<BVHBuilderNode>();
                    var cind1 = nodeind + 1;
                    var lcind = nodeind + node.ItemId; //(child node count)
                    while (cind1 < lcind)
                    {
                        var cnode = bvh.ContiguousNodes.data_items[cind1];
                        var ccount = (cnode.ItemCount <= 0) ? cnode.ItemId : 1;
                        var cind2 = cind1 + ccount;
                        var chi = new BVHBuilderNode();
                        chi.Unbuild(bvh, cind1, cind2);
                        Children.Add(chi);
                        cind1 = cind2;
                    }
                    nodeind += node.ItemId;
                }
                else //leaf node, with polygons
                {
                    Items = new List<BVHBuilderItem>();
                    for (int i = 0; i < node.ItemCount; i++)
                    {
                        var item = new BVHBuilderItem();
                        item.Index = node.ItemId + i;
                        Items.Add(item);
                    }
                    //BVHBuilder.MaxNodeItemCount = Math.Max(BVHBuilder.MaxNodeItemCount, node.ItemCount);
                    nodeind++;
                }
                Min = node.Min * q + c;
                Max = node.Max * q + c;
            }
        }

        public override string ToString()
        {
            var fstr = (Children != null) ? (TotalNodes.ToString() + ", 0 - ") : (Items != null) ? ("i, " + TotalItems.ToString() + " - ") : "error!";
            var cstr = (Children != null) ? (Children.Count.ToString() + " children") : "";
            var istr = (Items != null) ? (Items.Count.ToString() + " items") : "";
            if (string.IsNullOrEmpty(cstr)) return fstr + istr;
            if (string.IsNullOrEmpty(istr)) return fstr + cstr;
            return cstr + ", " + istr;
        }
    }
    public class BVHBuilderItem
    {
        public Vector3 Min;
        public Vector3 Max;
        public int Index;
        public Bounds? Bounds;
        public BoundPolygon? Polygon;
    }



    [Flags] public enum EBoundMaterialFlags : ushort
    {
        NONE = 0,
        FLAG_STAIRS = 1,
        FLAG_NOT_CLIMBABLE = 1 << 1,
        FLAG_SEE_THROUGH = 1 << 2,
        FLAG_SHOOT_THROUGH = 1 << 3,
        FLAG_NOT_COVER = 1 << 4,
        FLAG_WALKABLE_PATH = 1 << 5,
        FLAG_NO_CAM_COLLISION = 1 << 6,
        FLAG_SHOOT_THROUGH_FX = 1 << 7,
        FLAG_NO_DECAL = 1 << 8,
        FLAG_NO_NAVMESH = 1 << 9,
        FLAG_NO_RAGDOLL = 1 << 10,
        FLAG_VEHICLE_WHEEL = 1 << 11,
        FLAG_NO_PTFX = 1 << 12,
        FLAG_TOO_STEEP_FOR_PLAYER = 1 << 13,
        FLAG_NO_NETWORK_SPAWN = 1 << 14,
        FLAG_NO_CAM_COLLISION_ALLOW_CLIPPING = 1 << 15,
    }
    [TC(typeof(EXP))] public struct BoundMaterial_s : IMetaXmlItem
    {

        public uint Data1;
        public uint Data2;

        public BoundsMaterialType Type
        {
            get => (BoundsMaterialType)(Data1 & 0xFFu);
            set => Data1 = ((Data1 & 0xFFFFFF00u) | ((byte)value & 0xFFu));
        }

        public byte ProceduralId
        {
            get => (byte)((Data1 >> 8) & 0xFFu);
            set => Data1 = ((Data1 & 0xFFFF00FFu) | ((value & 0xFFu) << 8));
        }

        public byte RoomId
        {
            get => (byte)((Data1 >> 16) & 0x1Fu);
            set => Data1 = ((Data1 & 0xFFE0FFFFu) | ((value & 0x1Fu) << 16));
        }

        public byte PedDensity
        {
            get => (byte)((Data1 >> 21) & 0x7u);
            set => Data1 = ((Data1 & 0xFF1FFFFFu) | ((value & 0x7u) << 21));
        }

        public EBoundMaterialFlags Flags
        {
            get => (EBoundMaterialFlags)(((Data1 >> 24) & 0xFFu) | ((Data2 & 0xFFu) << 8));
            set
            {
                Data1 = (Data1 & 0x00FFFFFFu) | (((ushort)value & 0x00FFu) << 24);
                Data2 = (Data2 & 0xFFFFFF00u) | (((ushort)value & 0xFF00u) >> 8);
            }
        }

        public byte MaterialColorIndex
        {
            get => (byte)((Data2 >> 8) & 0xFFu);
            set => Data2 = ((Data2 & 0xFFFF00FFu) | ((value & 0xFFu) << 8));
        }

        public ushort Unk4
        {
            get => (ushort)((Data2 >> 16) & 0xFFFFu);
            set => Data2 = ((Data2 & 0x0000FFFFu) | ((value & 0xFFFFu) << 16));
        }


        public void WriteXml(StringBuilder sb, int indent)
        {
            YbnXml.ValueTag(sb, indent, "Type", Type.Index.ToString());
            YbnXml.ValueTag(sb, indent, "ProceduralID", ProceduralId.ToString());
            YbnXml.ValueTag(sb, indent, "RoomID", RoomId.ToString());
            YbnXml.ValueTag(sb, indent, "PedDensity", PedDensity.ToString());
            YbnXml.StringTag(sb, indent, "Flags", Flags.ToString());
            YbnXml.ValueTag(sb, indent, "MaterialColourIndex", MaterialColorIndex.ToString());
            YbnXml.ValueTag(sb, indent, "Unk", Unk4.ToString());
        }
        public void ReadXml(XmlNode node)
        {
            Type = (byte)Xml.GetChildUIntAttribute(node, "Type", "value");
            ProceduralId = (byte)Xml.GetChildUIntAttribute(node, "ProceduralID", "value");
            RoomId = (byte)Xml.GetChildUIntAttribute(node, "RoomID", "value");
            PedDensity = (byte)Xml.GetChildUIntAttribute(node, "PedDensity", "value");
            Flags = Xml.GetChildEnumInnerText<EBoundMaterialFlags>(node, "Flags");
            MaterialColorIndex = (byte)Xml.GetChildUIntAttribute(node, "MaterialColourIndex", "value");
            Unk4 = (ushort)Xml.GetChildUIntAttribute(node, "Unk", "value");
        }

        public override string ToString()
        {
            return Data1.ToString() + ", " + Data2.ToString() + ", "
                + Type.ToString() + ", " + ProceduralId.ToString() + ", " + RoomId.ToString() + ", " + PedDensity.ToString() + ", "
                + Flags.ToString() + ", " + MaterialColorIndex.ToString() + ", " + Unk4.ToString();
        }

    }
    [TC(typeof(EXP))] public struct BoundMaterialColour
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; } //GIMS EVO saves this as "opacity" 0-100
        public override string ToString()
        {
            //return Type.ToString() + ", " + Unk0.ToString() + ", " + Unk1.ToString() + ", " + Unk2.ToString();
            return R.ToString() + ", " + G.ToString() + ", " + B.ToString() + ", " + A.ToString();
        }
    }
    [TC(typeof(EXP))] public struct BoundsMaterialType
    {
        public byte Index { get; set; }

        public BoundsMaterialData? MaterialData
        {
            get
            {
                return BoundsMaterialTypes.GetMaterial(this);
            }
        }

        public override string ToString()
        {
            return BoundsMaterialTypes.GetMaterialName(this);
        }

        public static implicit operator byte(BoundsMaterialType matType)
        {
            return matType.Index;  //implicit conversion
        }

        public static implicit operator BoundsMaterialType(byte b)
        {
            return new BoundsMaterialType() { Index = b };
        }
    }
    [TC(typeof(EXP))] public class BoundsMaterialData
    {
        public string Name { get; set; } = string.Empty;
        public string Filter { get; set; } = string.Empty;
        public string FXGroup { get; set; } = string.Empty;
        public string VFXDisturbanceType { get; set; } = string.Empty;
        public string RumbleProfile { get; set; } = string.Empty;
        public string ReactWeaponType { get; set; } = string.Empty;
        public string Friction { get; set; } = string.Empty;
        public string Elasticity { get; set; } = string.Empty;
        public string Density { get; set; } = string.Empty;
        public string TyreGrip { get; set; } = string.Empty;
        public string WetGrip { get; set; } = string.Empty;
        public string TyreDrag { get; set; } = string.Empty;
        public string TopSpeedMult { get; set; } = string.Empty;
        public string Softness { get; set; } = string.Empty;
        public string Noisiness { get; set; } = string.Empty;
        public string PenetrationResistance { get; set; } = string.Empty;
        public string SeeThru { get; set; } = string.Empty;
        public string ShootThru { get; set; } = string.Empty;
        public string ShootThruFX { get; set; } = string.Empty;
        public string NoDecal { get; set; } = string.Empty;
        public string Porous { get; set; } = string.Empty;
        public string HeatsTyre { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;

        public Color Colour { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public static class BoundsMaterialTypes
    {
        private static Dictionary<string, Color> ColourDict = new();
        public static List<BoundsMaterialData> Materials = [];

        public static void Init(GameFileCache gameFileCache)
        {
            var rpfman = gameFileCache.RpfMan ?? throw new InvalidOperationException("The game archive manager has not been initialized.");

            var dic = new Dictionary<string,Color>();
            string filename2 = "common.rpf\\data\\effects\\materialfx.dat";
            string txt2 = rpfman.GetFileUTF8Text(filename2);
            AddMaterialfxDat(txt2, dic);

            ColourDict = dic;

            var list = new List<BoundsMaterialData>();
            string filename = "common.rpf\\data\\materials\\materials.dat";
            if (gameFileCache.EnableDlc)
            {
                filename = "update\\update.rpf\\common\\data\\materials\\materials.dat";
            }
            string txt = rpfman.GetFileUTF8Text(filename);
            AddMaterialsDat(txt, list);

            Materials = list;
        }

        //Only gets the colors
        private static void AddMaterialfxDat(string txt, Dictionary<string, Color> dic)
        {
            dic.Clear();
            if (txt == null) return;

            string[] lines = txt.Split('\n');
            string startLine = "MTLFX_TABLE_START";
            string endLine = "MTLFX_TABLE_END";

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line[0] == '#') continue;
                if (line.StartsWith(startLine)) continue;
                if (line.StartsWith(endLine)) break;

                string[] parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 5) continue; // FXGroup R G B ...

                int cp = 0;
                Color c = new();
                c.A = 0xFF;
                string fxgroup = string.Empty;
                for (int p = 0; p < parts.Length; p++)
                {
                    string part = parts[p].Trim();
                    if (string.IsNullOrWhiteSpace(part)) continue;
                    switch (cp)
                    {
                        case 0: fxgroup = part; break;
                        case 1: c.R = byte.Parse(part); break;
                        case 2: c.G = byte.Parse(part); break;
                        case 3: c.B = byte.Parse(part); break;
                    }
                    cp++;
                }
                dic.Add(fxgroup, c);
            }
        }

        private static void AddMaterialsDat(string txt, List<BoundsMaterialData> list)
        {
            list.Clear();
            if (txt == null) return;
            string[] lines = txt.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length < 20) continue;
                if (line[0] == '#') continue;
                string[] parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10) continue;
                int cp = 0;
                BoundsMaterialData d = new();
                for (int p = 0; p < parts.Length; p++)
                {
                    string part = parts[p].Trim();
                    if (string.IsNullOrWhiteSpace(part)) continue;
                    switch (cp)
                    {
                        case 0: d.Name = part; break;
                        case 1: d.Filter = part; break;
                        case 2: d.FXGroup = part; break;
                        case 3: d.VFXDisturbanceType = part; break;
                        case 4: d.RumbleProfile = part; break;
                        case 5: d.ReactWeaponType = part; break;
                        case 6: d.Friction = part; break;
                        case 7: d.Elasticity = part; break;
                        case 8: d.Density = part; break;
                        case 9: d.TyreGrip = part; break;
                        case 10: d.WetGrip = part; break;
                        case 11: d.TyreDrag = part; break;
                        case 12: d.TopSpeedMult = part; break;
                        case 13: d.Softness = part; break;
                        case 14: d.Noisiness = part; break;
                        case 15: d.PenetrationResistance = part; break;
                        case 16: d.SeeThru = part; break;
                        case 17: d.ShootThru = part; break;
                        case 18: d.ShootThruFX = part; break;
                        case 19: d.NoDecal = part; break;
                        case 20: d.Porous = part; break;
                        case 21: d.HeatsTyre = part; break;
                        case 22: d.Material = part; break;
                    }
                    cp++;
                }
                if (cp != 23)
                { }

                Color c;
                if ((ColourDict != null) && (ColourDict.TryGetValue(d.FXGroup, out c)))
                {
                    d.Colour = c;
                }
                else
                {
                    d.Colour = new Color(0xFFCCCCCC);
                }


                list.Add(d);
            }
        }


        public static BoundsMaterialData? GetMaterial(BoundsMaterialType type)
        {
            if (Materials == null) return null;
            if (type.Index >= Materials.Count) return null;
            return Materials[type.Index];
        }

        public static BoundsMaterialData? GetMaterial(byte index)
        {
            if (Materials == null) return null;
            if ((int)index >= Materials.Count) return null;
            return Materials[index];
        }

        public static string GetMaterialName(BoundsMaterialType type)
        {
            var m = GetMaterial(type);
            if (m == null) return string.Empty;
            return m.Name;
        }

        public static Color GetMaterialColour(BoundsMaterialType type)
        {
            var m = GetMaterial(type);
            if (m == null) return new Color(0xFFCCCCCC);
            return m.Colour;
        }
    }


}
