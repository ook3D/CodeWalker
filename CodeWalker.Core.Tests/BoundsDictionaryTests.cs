using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class BoundsDictionaryTests
{
    [Fact]
    public void BoundUsesNativePhBoundLayout()
    {
        var bound = new Bounds
        {
            Type = BoundsType.Box,
            Flags = BoundFlags.ForceCcd | BoundFlags.UseCurrentInstanceMatrixOnly,
            PartIndex = 0x1213,
            RadiusAroundCentroid = 1.5f,
            BoundingBoxMax = new SharpDX.Vector3(2, 3, 4),
            Margin = 5,
            BoundingBoxMin = new SharpDX.Vector3(6, 7, 8),
            ReferenceCount = 9,
            CentroidOffset = new SharpDX.Vector3(10, 11, 12),
            Material = new BoundMaterial_s { Data1 = 0x4F4E4D4C, Data2 = 0x5F5E5D5C },
            CenterOfGravityOffset = new SharpDX.Vector3(13, 14, 15),
            Inertia = new SharpDX.Vector3(16, 17, 18),
            Volume = 19,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        bound.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x70, bytes.Length);
        Assert.Equal((byte)bound.Type, bytes[0x10]);
        Assert.Equal((byte)bound.Flags, bytes[0x11]);
        Assert.Equal(bound.PartIndex, BitConverter.ToUInt16(bytes, 0x12));
        Assert.Equal(bound.RadiusAroundCentroid, BitConverter.ToSingle(bytes, 0x14));
        Assert.All(bytes[0x18..0x20], value => Assert.Equal(0, value));
        Assert.Equal(bound.Margin, BitConverter.ToSingle(bytes, 0x2C));
        Assert.Equal(bound.ReferenceCount, BitConverter.ToInt32(bytes, 0x3C));
        Assert.Equal(bound.Material.Data1, BitConverter.ToUInt32(bytes, 0x4C));
        Assert.Equal(bound.Material.Data2, BitConverter.ToUInt32(bytes, 0x5C));
        Assert.Equal(bound.Volume, BitConverter.ToSingle(bytes, 0x6C));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new Bounds();
        loaded.Read(reader);

        Assert.Equal(bound.Flags, loaded.Flags);
        Assert.Equal(bound.PartIndex, loaded.PartIndex);
        Assert.Equal(bound.BoundingBoxMax, loaded.BoundingBoxMax);
        Assert.Equal(bound.BoundingBoxMin, loaded.BoundingBoxMin);
        Assert.Equal(bound.ReferenceCount, loaded.ReferenceCount);
        Assert.Equal(bound.CentroidOffset, loaded.CentroidOffset);
        Assert.Equal(bound.Material.Data1, loaded.Material.Data1);
        Assert.Equal(bound.Material.Data2, loaded.Material.Data2);
        Assert.Equal(bound.CenterOfGravityOffset, loaded.CenterOfGravityOffset);
        Assert.Equal(bound.Inertia, loaded.Inertia);
    }

    [Fact]
    public void BoundCapsuleUsesNativeDerivedLayout()
    {
        var capsule = new BoundCapsule
        {
            Type = BoundsType.Capsule,
            CapsuleHalfHeight = 12.5f,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        capsule.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x80, bytes.Length);
        Assert.Equal(capsule.CapsuleHalfHeight, BitConverter.ToSingle(bytes, 0x70));
        Assert.All(bytes[0x74..0x80], value => Assert.Equal(0, value));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new BoundCapsule();
        loaded.Read(reader);

        Assert.Equal(capsule.CapsuleHalfHeight, loaded.CapsuleHalfHeight);
    }

    [Fact]
    public void BoundDiscUsesNativeDerivedLayout()
    {
        var disc = new BoundDisc
        {
            Type = BoundsType.Disc,
            RadiusAroundCentroid = 12.5f,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        disc.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x80, bytes.Length);
        Assert.All(bytes[0x70..0x80], value => Assert.Equal(0, value));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new BoundDisc();
        loaded.Read(reader);

        Assert.Equal(disc.RadiusAroundCentroid, loaded.RadiusAroundCentroid);
    }

    [Fact]
    public void BoundCylinderUsesNativeDerivedLayout()
    {
        var cylinder = new BoundCylinder
        {
            Type = BoundsType.Cylinder,
            BoundingBoxMax = new SharpDX.Vector3(4, 6, 4),
            BoundingBoxMin = new SharpDX.Vector3(-4, -6, -4),
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        cylinder.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x80, bytes.Length);
        Assert.All(bytes[0x70..0x80], value => Assert.Equal(0, value));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new BoundCylinder();
        loaded.Read(reader);

        Assert.Equal(cylinder.BoundingBoxMax, loaded.BoundingBoxMax);
        Assert.Equal(cylinder.BoundingBoxMin, loaded.BoundingBoxMin);
    }

    [Fact]
    public void BoundPlaneUsesNativeBaseLayout()
    {
        var plane = new BoundPlane
        {
            Type = BoundsType.Plane,
            CentroidOffset = new SharpDX.Vector3(1, 2, 3),
            Normal = SharpDX.Vector3.UnitZ,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        plane.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x70, bytes.Length);
        Assert.Equal((byte)15, bytes[0x10]);

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new BoundPlane();
        loaded.Read(reader);

        Assert.Equal(plane.CentroidOffset, loaded.CentroidOffset);
        Assert.Equal(plane.Normal, loaded.Normal);
        Assert.IsType<BoundPlane>(Bounds.Create(BoundsType.Plane));
    }

    [Fact]
    public void BoundGeometryUsesNativePolyhedronAndGeometryLayout()
    {
        var geometry = new BoundGeometry
        {
            FileVFT = 1080226408,
            Type = BoundsType.Geometry,
            BoundingBoxMin = new SharpDX.Vector3(-2),
            BoundingBoxMax = new SharpDX.Vector3(2),
            UseActiveComponents = true,
            IsFlat = true,
            ConvexHullVertexCount = 2,
            UnQuantizeFactor = new SharpDX.Vector4(0, 0, 0, 0.125f),
            BoundingBoxCenter = new SharpDX.Vector4(0, 0, 0, 0.25f),
            Vertices = [new SharpDX.Vector3(-1), new SharpDX.Vector3(1)],
            VertexColours =
            [
                new BoundMaterialColour { R = 1, G = 2, B = 3, A = 4 },
                new BoundMaterialColour { R = 5, G = 6, B = 7, A = 8 },
            ],
            SecondSurfaceVertexDisplacements = [0.25f, 0.5f],
        };

        var data = ResourceBuilder.Build(geometry, 43);
        var file = new YbnFile();
        file.Load(data);

        var loaded = Assert.IsType<BoundGeometry>(file.Bounds);
        Assert.Equal(0x130, loaded.BlockLength);
        Assert.True(loaded.UseActiveComponents);
        Assert.True(loaded.IsFlat);
        Assert.Equal(1, loaded.VertexAttributeCount);
        Assert.Equal(2, loaded.ConvexHullVertexCount);
        Assert.Equal(2, loaded.VertexCount);
        Assert.Empty(loaded.Polygons);
        Assert.Equal(0.125f, loaded.UnQuantizeFactor.W);
        Assert.Equal(0.25f, loaded.BoundingBoxCenter.W);
        Assert.Equal(geometry.VertexColours, loaded.VertexColours);
        Assert.Equal(geometry.SecondSurfaceVertexDisplacements, loaded.SecondSurfaceVertexDisplacements);
    }

    [Fact]
    public void BoundGeometryXmlUsesCompatibleGeometryCenterFields()
    {
        var geometry = new BoundGeometry
        {
            Type = BoundsType.Geometry,
            UnQuantizeFactor = new SharpDX.Vector4(1, 2, 3, 0.125f),
            BoundingBoxCenter = new SharpDX.Vector4(123, 456, 789, 0.25f),
        };

        var xml = YbnXml.GetXml(new YbnFile { Bounds = geometry });

        Assert.Contains("<GeometryCenter x=\"123\" y=\"456\" z=\"789\" />", xml);
        Assert.Contains("<UnkFloat1 value=\"0.125\" />", xml);
        Assert.Contains("<UnkFloat2 value=\"0.25\" />", xml);
        Assert.DoesNotContain("<BoundingBoxCenter", xml);

        var loaded = Assert.IsType<BoundGeometry>(XmlYbn.GetYbn(xml).Bounds);
        Assert.Equal(geometry.CenterGeom, loaded.CenterGeom);
        Assert.Equal(geometry.UnQuantizeFactor.W, loaded.UnQuantizeFactor.W);
        Assert.Equal(geometry.BoundingBoxCenter.W, loaded.BoundingBoxCenter.W);
    }

    [Fact]
    public void BoundBvhUsesNativeDerivedLayout()
    {
        var bound = new BoundBVH
        {
            Type = BoundsType.GeometryBVH,
            NumActivePolygons = 123,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        bound.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x150, bytes.Length);
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x130));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x138));
        Assert.Equal(123, BitConverter.ToUInt16(bytes, 0x140));
        Assert.All(bytes[0x142..0x150], value => Assert.Equal(0, value));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new BoundBVH();
        loaded.Read(reader);

        Assert.Equal((ushort)123, loaded.NumActivePolygons);
        Assert.Null(loaded.BVH);
    }

    [Fact]
    public void BvhUsesNativeOptimizedBvhLayout()
    {
        var bvh = new BVH
        {
            AABBMin = new SharpDX.Vector3(1, 2, 3),
            AABBMax = new SharpDX.Vector3(4, 5, 6),
            AABBCenter = new SharpDX.Vector3(7, 8, 9),
            Quantize = new SharpDX.Vector3(10, 11, 12),
            InvQuantize = new SharpDX.Vector3(13, 14, 15),
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        bvh.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x80, bytes.Length);
        Assert.All(bytes[0x10..0x20], value => Assert.Equal(0, value));
        Assert.Equal(bvh.AABBMin.X, BitConverter.ToSingle(bytes, 0x20));
        Assert.Equal(bvh.AABBMax.X, BitConverter.ToSingle(bytes, 0x30));
        Assert.Equal(bvh.AABBCenter.X, BitConverter.ToSingle(bytes, 0x40));
        Assert.Equal(bvh.Quantize.X, BitConverter.ToSingle(bytes, 0x50));
        Assert.Equal(bvh.InvQuantize.X, BitConverter.ToSingle(bytes, 0x60));
        foreach (var offset in new[] { 0x2C, 0x3C, 0x4C, 0x5C, 0x6C })
            Assert.Equal(0u, BitConverter.ToUInt32(bytes, offset));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new BVH();
        loaded.Read(reader);

        Assert.Equal(bvh.AABBMin, loaded.AABBMin);
        Assert.Equal(bvh.AABBMax, loaded.AABBMax);
        Assert.Equal(bvh.AABBCenter, loaded.AABBCenter);
        Assert.Equal(bvh.Quantize, loaded.Quantize);
        Assert.Equal(bvh.InvQuantize, loaded.InvQuantize);
    }

    [Fact]
    public void BoundCompositeUsesNativeOwnedArrayLayout()
    {
        var child = new BoundSphere
        {
            Type = BoundsType.Sphere,
            CompositeFlags1 = new BoundCompositeChildrenFlags
            {
                Flags1 = EBoundCompositeFlags.MAP_WEAPON,
                Flags2 = EBoundCompositeFlags.MAP_DYNAMIC,
            },
        };
        var composite = new BoundComposite
        {
            Type = BoundsType.Composite,
            Children = new ResourcePointerArray64<Bounds> { data_items = [child] },
        };

        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };
        composite.Write(writer);
        var block = system.ToArray();
        Assert.Equal(0xB0, block.Length);
        Assert.Equal((ushort)1, BitConverter.ToUInt16(block, 0xA0));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(block, 0xA2));
        Assert.All(block[0xA4..0xA8], value => Assert.Equal(0, value));

        var data = ResourceBuilder.Build(composite, 43);
        var file = new YbnFile();
        file.Load(data);

        var loaded = Assert.IsType<BoundComposite>(file.Bounds);
        Assert.Equal(0xB0, loaded.BlockLength);
        Assert.Equal((ushort)1, loaded.MaxNumBounds);
        Assert.Equal((ushort)1, loaded.NumBounds);
        Assert.Equal(loaded.CurrentMatricesPointer, loaded.LastMatricesPointer);
        Assert.Null(loaded.LastMatrices);
        Assert.Single(Assert.IsType<AABB_s[]>(loaded.LocalBoundingBoxes));
        Assert.Equal(loaded.TypeAndIncludeFlagsPointer, loaded.OwnedTypeAndIncludeFlagsPointer);
        Assert.Single(Assert.IsType<BoundCompositeChildrenFlags[]>(loaded.TypeAndIncludeFlags));
    }

    [Fact]
    public void BoundPolygonEncodesNativePrimitiveTypeBits()
    {
        var polygon = new BoundPolygonCapsule();

        Assert.Equal(BoundPolygonType.Capsule, BoundPolygon.DecodeType(0xFA));
        Assert.Equal(0xF8, BoundPolygon.ClearType(0xFF));
        Assert.Equal(0xFA, polygon.EncodeType(0xF8));
        Assert.Equal(0x10, BoundPolygon.SerializedSize);

        polygon.Type = (BoundPolygonType)7;
        Assert.Throws<InvalidDataException>(() => polygon.EncodeType(0));
        Assert.Throws<InvalidDataException>(() => BoundPolygon.DecodeType(7));
    }

    [Fact]
    public void BoundPolygonTriangleUsesNativePhPolygonLayout()
    {
        var triangle = new BoundPolygonTriangle
        {
            Area = BitConverter.Int32BitsToSingle(0x3F8000A9),
            VertexIndex1 = 5,
            VertexIndex2 = 6,
            VertexIndex3 = 7,
            VertexNormalFlag1 = true,
        };
        triangle.SetNeighboringPolygonIndex(0, 10);
        triangle.SetNeighboringPolygonIndex(1, -1);
        triangle.SetNeighboringPolygonIndex(2, 12);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        triangle.Write(writer);
        var data = stream.ToArray();

        Assert.Equal(BoundPolygon.SerializedSize, data.Length);
        Assert.Equal(0xA8, data[0]);
        Assert.Equal(0x8005, BitConverter.ToUInt16(data, 4));
        Assert.Equal((ushort)10, BitConverter.ToUInt16(data, 10));
        Assert.Equal(ushort.MaxValue, BitConverter.ToUInt16(data, 12));

        var loaded = new BoundPolygonTriangle();
        loaded.Read(data, 0);
        Assert.Equal(5, loaded.VertexIndex1);
        Assert.True(loaded.VertexNormalFlag1);
        Assert.Equal(-1, loaded.GetNeighboringPolygonIndex(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => loaded.GetVertexIndex(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => loaded.SetNeighboringPolygonIndex(3, 0));
    }

    [Fact]
    public void BoundPolygonSphereUsesNativePrimitiveLayout()
    {
        var sphere = new BoundPolygonSphere
        {
            CenterIndex = 123,
            Radius = 4.5f,
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        sphere.Write(writer);
        var data = stream.ToArray();

        Assert.Equal(BoundPolygon.SerializedSize, data.Length);
        Assert.Equal((byte)BoundPolygonType.Sphere, data[0]);
        Assert.Equal(0, data[1]);
        Assert.Equal((ushort)123, BitConverter.ToUInt16(data, 2));
        Assert.Equal(4.5f, BitConverter.ToSingle(data, 4));
        Assert.All(data[8..16], value => Assert.Equal(0, value));

        var loaded = new BoundPolygonSphere();
        loaded.Read(data, 0);
        Assert.Equal((ushort)123, loaded.CenterIndex);
        Assert.Equal(4.5f, loaded.Radius);

        data[0] = (byte)BoundPolygonType.Capsule;
        Assert.Throws<InvalidDataException>(() => loaded.Read(data, 0));
    }

    [Fact]
    public void BoundPolygonCapsuleUsesNativePrimitiveLayout()
    {
        var capsule = new BoundPolygonCapsule
        {
            EndIndex0 = 21,
            Radius = 2.75f,
            EndIndex1 = 34,
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        capsule.Write(writer);
        var data = stream.ToArray();

        Assert.Equal(BoundPolygon.SerializedSize, data.Length);
        Assert.Equal((byte)BoundPolygonType.Capsule, data[0]);
        Assert.Equal(0, data[1]);
        Assert.Equal((ushort)21, BitConverter.ToUInt16(data, 2));
        Assert.Equal(2.75f, BitConverter.ToSingle(data, 4));
        Assert.Equal((ushort)34, BitConverter.ToUInt16(data, 8));
        Assert.All(data[10..16], value => Assert.Equal(0, value));

        var loaded = new BoundPolygonCapsule();
        loaded.Read(data, 0);
        Assert.Equal((ushort)21, loaded.EndIndex0);
        Assert.Equal(2.75f, loaded.Radius);
        Assert.Equal((ushort)34, loaded.EndIndex1);

        data[0] = (byte)BoundPolygonType.Sphere;
        Assert.Throws<InvalidDataException>(() => loaded.Read(data, 0));
    }

    [Fact]
    public void BoundPolygonBoxUsesNativePrimitiveLayout()
    {
        var box = new BoundPolygonBox
        {
            VertexIndex0 = 1,
            VertexIndex1 = 32768,
            VertexIndex2 = 60000,
            VertexIndex3 = 65534,
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        box.Write(writer);
        var data = stream.ToArray();

        Assert.Equal(BoundPolygon.SerializedSize, data.Length);
        Assert.Equal((byte)BoundPolygonType.Box, data[0]);
        Assert.All(data[1..4], value => Assert.Equal(0, value));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(data, 4));
        Assert.Equal((ushort)32768, BitConverter.ToUInt16(data, 6));
        Assert.Equal((ushort)60000, BitConverter.ToUInt16(data, 8));
        Assert.Equal((ushort)65534, BitConverter.ToUInt16(data, 10));
        Assert.All(data[12..16], value => Assert.Equal(0, value));

        var loaded = new BoundPolygonBox();
        loaded.Read(data, 0);
        Assert.Equal(new[] { 1, 32768, 60000, 65534 }, loaded.VertexIndices);
        Assert.Equal(60000, loaded.GetVertexIndex(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => loaded.GetVertexIndex(4));

        data[0] = (byte)BoundPolygonType.Cylinder;
        Assert.Throws<InvalidDataException>(() => loaded.Read(data, 0));
    }

    [Fact]
    public void BoundPolygonCylinderUsesNativePrimitiveLayout()
    {
        var cylinder = new BoundPolygonCylinder
        {
            EndIndex0 = 32768,
            Radius = 3.25f,
            EndIndex1 = 60000,
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        cylinder.Write(writer);
        var data = stream.ToArray();

        Assert.Equal(BoundPolygon.SerializedSize, data.Length);
        Assert.Equal((byte)BoundPolygonType.Cylinder, data[0]);
        Assert.Equal(0, data[1]);
        Assert.Equal((ushort)32768, BitConverter.ToUInt16(data, 2));
        Assert.Equal(3.25f, BitConverter.ToSingle(data, 4));
        Assert.Equal((ushort)60000, BitConverter.ToUInt16(data, 8));
        Assert.All(data[10..16], value => Assert.Equal(0, value));

        var loaded = new BoundPolygonCylinder();
        loaded.Read(data, 0);
        Assert.Equal(new[] { 32768, 60000 }, loaded.VertexIndices);
        Assert.Equal(3.25f, loaded.Radius);

        data[0] = (byte)BoundPolygonType.Capsule;
        Assert.Throws<InvalidDataException>(() => loaded.Read(data, 0));
    }

    [Fact]
    public void DictionaryUsesNativePgDictionaryLayout()
    {
        var dictionary = new BoundsDictionary
        {
            ReferenceCount = 7,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        dictionary.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x40, bytes.Length);
        Assert.All(bytes[0x10..0x18], value => Assert.Equal(0, value));
        Assert.Equal(7u, BitConverter.ToUInt32(bytes, 0x18));
        Assert.All(bytes[0x1C..0x20], value => Assert.Equal(0, value));
        Assert.All(bytes[0x20..0x40], value => Assert.Equal(0, value));
    }

    [Fact]
    public void LookupUsesSortedNativeCodes()
    {
        var first = new BoundSphere();
        var second = new BoundBox();
        var dictionary = new BoundsDictionary
        {
            Codes = new ResourceSimpleList64_uint { data_items = [10, 20] },
            Entries = new ResourcePointerList64<Bounds> { data_items = [first, second] },
        };

        Assert.Same(first, dictionary.Lookup(10));
        Assert.Same(second, dictionary.Lookup(20));
        Assert.Null(dictionary.Lookup(15));
    }

    [Fact]
    public void MismatchedCodesAndEntriesAreRejected()
    {
        var dictionary = new BoundsDictionary
        {
            Codes = new ResourceSimpleList64_uint { data_items = [1] },
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        Assert.Throws<InvalidDataException>(() => dictionary.Write(writer));
    }
}
