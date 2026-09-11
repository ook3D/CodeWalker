using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class DrawableFragmentFormatTests
{
    [Fact]
    public void DrawableDictionaryMatchesNativePgDictionaryLayout()
    {
        var original = new DrawableDictionary { ReferenceCount = 7 };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(0x40, bytes.Length);
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal(7u, BitConverter.ToUInt32(bytes, 0x18));
        Assert.All(bytes[0x1C..0x20], value => Assert.Equal(0, value));
        Assert.All(bytes[0x20..0x40], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new DrawableDictionary();
        loaded.Read(reader);
        Assert.Equal(original.ReferenceCount, loaded.ReferenceCount);
        Assert.Empty(loaded.Codes.data_items);
        Assert.Empty(loaded.Entries.data_items);
        Assert.Equal(new long[] { 0x20, 0x30 }, loaded.GetParts().Select(part => part.Item1));
    }

    [Fact]
    public void BoneDataDofsMatchNativeMasks()
    {
        Assert.Equal(0x0007, (ushort)crBoneDataDofs.ROTATION);
        Assert.Equal(0x0070, (ushort)crBoneDataDofs.TRANSLATION);
        Assert.Equal(0x0700, (ushort)crBoneDataDofs.SCALE);
        Assert.Equal(0x1000, (ushort)crBoneDataDofs.HAS_CHILD);
        Assert.Equal(0x2000, (ushort)crBoneDataDofs.IS_SKINNED);
    }

    [Fact]
    public void BoneIdMapEntryMatchesNativeFieldLayout()
    {
        var original = new atMapEntry
        {
            Key = 0xA1B2C3D4,
            Data = -2
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(16, bytes.Length);
        Assert.Equal(original.Key, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(original.Data, BitConverter.ToInt32(bytes, 4));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 8));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new atMapEntry();
        loaded.Read(reader);
        Assert.Equal(original.Key, loaded.Key);
        Assert.Equal(original.Data, loaded.Data);
        Assert.Null(loaded.Next);
    }

    [Fact]
    public void BoneDataArrayBlockWritesNativeArrayCookie()
    {
        var original = new crBoneDataArrayBlock { Items = [new crBoneData { Name = string.Empty }] };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(96, bytes.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0));
        Assert.All(bytes[4..16], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new crBoneDataArrayBlock();
        loaded.Read(reader, 1u);
        Assert.Single(loaded.Items);
    }

    [Fact]
    public void BoneDataMatchesNativeFieldLayout()
    {
        var original = new crBoneData
        {
            DefaultRotation = new SharpDX.Quaternion(1, 2, 3, 4),
            DefaultTranslation = new SharpDX.Vector3(5, 6, 7),
            DefaultScale = new SharpDX.Vector3(8, 9, 10),
            NextIndex = -2,
            ParentIndex = -3,
            Dofs = crBoneDataDofs.ROTATION | crBoneDataDofs.IS_SKINNED,
            Index = 0xFEDC,
            BoneId = 0xABCD,
            MirrorIndex = 0xDCBA
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(80, bytes.Length);
        Assert.Equal(0.0f, BitConverter.ToSingle(bytes, 0x1C));
        Assert.Equal(1.0f, BitConverter.ToSingle(bytes, 0x2C));
        Assert.Equal(original.NextIndex, BitConverter.ToInt16(bytes, 0x30));
        Assert.Equal(original.ParentIndex, BitConverter.ToInt16(bytes, 0x32));
        Assert.All(bytes[0x34..0x38], value => Assert.Equal(0, value));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x38));
        Assert.Equal((ushort)original.Dofs, BitConverter.ToUInt16(bytes, 0x40));
        Assert.Equal(original.Index, BitConverter.ToUInt16(bytes, 0x42));
        Assert.Equal(original.BoneId, BitConverter.ToUInt16(bytes, 0x44));
        Assert.Equal(original.MirrorIndex, BitConverter.ToUInt16(bytes, 0x46));
        Assert.All(bytes[0x48..0x50], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new crBoneData();
        loaded.Read(reader);
        Assert.Equal(original.DefaultRotation, loaded.DefaultRotation);
        Assert.Equal(original.DefaultTranslation, loaded.DefaultTranslation);
        Assert.Equal(original.DefaultScale, loaded.DefaultScale);
        Assert.Equal(original.NextIndex, loaded.NextIndex);
        Assert.Equal(original.ParentIndex, loaded.ParentIndex);
        Assert.Equal(original.Dofs, loaded.Dofs);
        Assert.Equal(original.Index, loaded.Index);
        Assert.Equal(original.BoneId, loaded.BoneId);
        Assert.Equal(original.MirrorIndex, loaded.MirrorIndex);
    }

    [Fact]
    public void JointDataMatchesNativeFieldLayout()
    {
        var original = new crJointData
        {
            FirstNodePointer = 0x0102030405060708,
            RotationLimits = [new crJointRotationLimit()],
            TranslationLimits = [new crJointTranslationLimit()],
            ScaleLimits = [new crJointScaleLimit()],
            Name = "joint_data",
            RefCount = 7
        };
        Assert.Equal(4, original.GetReferences().Length);
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(64, bytes.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(original.FirstNodePointer, BitConverter.ToUInt64(bytes, 0x08));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x18));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x20));
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 0x28));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x30));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x32));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x34));
        Assert.Equal(original.RefCount, BitConverter.ToUInt16(bytes, 0x36));
        Assert.All(bytes[0x38..0x40], value => Assert.Equal(0, value));
        Assert.Equal(64, System.Runtime.InteropServices.Marshal.SizeOf<crJointScaleLimit>());
        Assert.Equal(-1, new crJointScaleLimit().BoneId);

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new crJointData();
        loaded.Read(reader);
        Assert.Equal(original.FirstNodePointer, loaded.FirstNodePointer);
        Assert.Equal(original.NumRotationLimits, loaded.NumRotationLimits);
        Assert.Equal(original.NumTranslationLimits, loaded.NumTranslationLimits);
        Assert.Equal(original.NumScaleLimits, loaded.NumScaleLimits);
        Assert.Equal(original.RefCount, loaded.RefCount);
    }

    [Fact]
    public void JointRotationLimitMatchesNativeFieldLayout()
    {
        var defaults = new crJointRotationLimit();
        Assert.Equal(-1, defaults.BoneId);
        Assert.Equal(1, defaults.NumControlPoints);
        Assert.Equal(crJointRotationLimit.JointDOFs.JOINT_3_DOF, defaults.JointDofs);
        Assert.Equal(SharpDX.Quaternion.Identity, defaults.ZeroRotation);
        Assert.Equal(SharpDX.Vector3.UnitX, defaults.TwistAxis);
        Assert.False(defaults.UseTwistLimits);
        Assert.False(defaults.UseEulerAngles);
        Assert.False(defaults.UsePerControlTwistLimits);

        var original = new crJointRotationLimit
        {
            BoneId = -123,
            NumControlPoints = 8,
            JointDofs = crJointRotationLimit.JointDOFs.JOINT_1_DOF,
            ZeroRotation = new SharpDX.Quaternion(1, 2, 3, 4),
            ZeroRotationEulers = new SharpDX.Vector3(5, 6, 7),
            TwistAxis = new SharpDX.Vector3(8, 9, 10),
            TwistLimitMin = 11,
            TwistLimitMax = 12,
            SoftLimitScale = 13,
            ControlPoint0 = new crJointRotationLimit.JointControlPoint(14, 15, 16),
            ControlPoint7 = new crJointRotationLimit.JointControlPoint(17, 18, 19),
            UseTwistLimits = true,
            UseEulerAngles = false,
            UsePerControlTwistLimits = true
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        writer.WriteStruct(original);

        byte[] bytes = data.ToArray();
        Assert.Equal(192, bytes.Length);
        Assert.All(bytes[0x00..0x08], value => Assert.Equal(0, value));
        Assert.Equal(original.BoneId, BitConverter.ToInt32(bytes, 0x08));
        Assert.Equal(original.NumControlPoints, BitConverter.ToInt32(bytes, 0x0C));
        Assert.Equal((int)original.JointDofs, BitConverter.ToInt32(bytes, 0x10));
        Assert.All(bytes[0x14..0x20], value => Assert.Equal(0, value));
        Assert.Equal(original.ZeroRotation.W, BitConverter.ToSingle(bytes, 0x2C));
        Assert.Equal(original.ZeroRotationEulers.X, BitConverter.ToSingle(bytes, 0x30));
        Assert.All(bytes[0x3C..0x40], value => Assert.Equal(0, value));
        Assert.Equal(original.TwistAxis.X, BitConverter.ToSingle(bytes, 0x40));
        Assert.All(bytes[0x4C..0x50], value => Assert.Equal(0, value));
        Assert.Equal(original.TwistLimitMin, BitConverter.ToSingle(bytes, 0x50));
        Assert.Equal(original.ControlPoint0.MaxSwing, BitConverter.ToSingle(bytes, 0x5C));
        Assert.Equal(original.ControlPoint7.MaxTwist, BitConverter.ToSingle(bytes, 0xB8));
        Assert.Equal(1, bytes[0xBC]);
        Assert.Equal(0, bytes[0xBD]);
        Assert.Equal(1, bytes[0xBE]);
        Assert.Equal(0, bytes[0xBF]);

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = reader.ReadStruct<crJointRotationLimit>();
        Assert.Equal(original.BoneId, loaded.BoneId);
        Assert.Equal(original.NumControlPoints, loaded.NumControlPoints);
        Assert.Equal(original.JointDofs, loaded.JointDofs);
        Assert.Equal(original.ZeroRotation, loaded.ZeroRotation);
        Assert.Equal(original.ControlPoint7.MaxTwist, loaded.ControlPoint7.MaxTwist);
        Assert.True(loaded.UseTwistLimits);
        Assert.False(loaded.UseEulerAngles);
        Assert.True(loaded.UsePerControlTwistLimits);
    }

    [Fact]
    public void JointTranslationLimitMatchesNativeFieldLayout()
    {
        var original = new crJointTranslationLimit
        {
            BoneId = -123,
            LimitMin = new SharpDX.Vector3(1, 2, 3),
            LimitMax = new SharpDX.Vector3(4, 5, 6)
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        writer.WriteStruct(original);

        byte[] bytes = data.ToArray();
        Assert.Equal(64, bytes.Length);
        Assert.All(bytes[0x00..0x08], value => Assert.Equal(0, value));
        Assert.Equal(original.BoneId, BitConverter.ToInt32(bytes, 0x08));
        Assert.All(bytes[0x0C..0x20], value => Assert.Equal(0, value));
        Assert.Equal(original.LimitMin.X, BitConverter.ToSingle(bytes, 0x20));
        Assert.Equal(original.LimitMin.Y, BitConverter.ToSingle(bytes, 0x24));
        Assert.Equal(original.LimitMin.Z, BitConverter.ToSingle(bytes, 0x28));
        Assert.All(bytes[0x2C..0x30], value => Assert.Equal(0, value));
        Assert.Equal(original.LimitMax.X, BitConverter.ToSingle(bytes, 0x30));
        Assert.Equal(original.LimitMax.Y, BitConverter.ToSingle(bytes, 0x34));
        Assert.Equal(original.LimitMax.Z, BitConverter.ToSingle(bytes, 0x38));
        Assert.All(bytes[0x3C..0x40], value => Assert.Equal(0, value));
        Assert.Equal(-1, new crJointTranslationLimit().BoneId);

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = reader.ReadStruct<crJointTranslationLimit>();
        Assert.Equal(original.BoneId, loaded.BoneId);
        Assert.Equal(original.LimitMin, loaded.LimitMin);
        Assert.Equal(original.LimitMax, loaded.LimitMax);
    }

    [Fact]
    public void JointScaleLimitMatchesNativeFieldLayout()
    {
        var original = new crJointScaleLimit
        {
            BoneId = -321,
            LimitMin = new SharpDX.Vector3(1, 2, 3),
            LimitMax = new SharpDX.Vector3(4, 5, 6)
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        writer.WriteStruct(original);

        byte[] bytes = data.ToArray();
        Assert.Equal(64, bytes.Length);
        Assert.All(bytes[0x00..0x08], value => Assert.Equal(0, value));
        Assert.Equal(original.BoneId, BitConverter.ToInt32(bytes, 0x08));
        Assert.All(bytes[0x0C..0x20], value => Assert.Equal(0, value));
        Assert.Equal(original.LimitMin.X, BitConverter.ToSingle(bytes, 0x20));
        Assert.Equal(original.LimitMin.Y, BitConverter.ToSingle(bytes, 0x24));
        Assert.Equal(original.LimitMin.Z, BitConverter.ToSingle(bytes, 0x28));
        Assert.All(bytes[0x2C..0x30], value => Assert.Equal(0, value));
        Assert.Equal(original.LimitMax.X, BitConverter.ToSingle(bytes, 0x30));
        Assert.Equal(original.LimitMax.Y, BitConverter.ToSingle(bytes, 0x34));
        Assert.Equal(original.LimitMax.Z, BitConverter.ToSingle(bytes, 0x38));
        Assert.All(bytes[0x3C..0x40], value => Assert.Equal(0, value));
        Assert.Equal(-1, new crJointScaleLimit().BoneId);

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = reader.ReadStruct<crJointScaleLimit>();
        Assert.Equal(original.BoneId, loaded.BoneId);
        Assert.Equal(original.LimitMin, loaded.LimitMin);
        Assert.Equal(original.LimitMax, loaded.LimitMax);
    }

    [Fact]
    public void SkeletonDataMatchesNativeFieldLayout()
    {
        var original = new crSkeletonData
        {
            FirstNodePointer = 0x0102030405060708,
            BoneIdTableAllowReCompute = 1,
            PropertiesPointer = 0x1112131415161718,
            Signature = 0x21222324,
            SignatureNonChiral = 0x31323334,
            SignatureComprehensive = 0x41424344,
            RefCount = 7
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(104, bytes.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(original.FirstNodePointer, BitConverter.ToUInt64(bytes, 0x08));
        Assert.All(bytes[0x1C..0x1F], value => Assert.Equal(0, value));
        Assert.Equal(1, bytes[0x1F]);
        Assert.Equal(original.PropertiesPointer, BitConverter.ToUInt64(bytes, 0x48));
        Assert.Equal(original.Signature, BitConverter.ToUInt32(bytes, 0x50));
        Assert.Equal(original.SignatureNonChiral, BitConverter.ToUInt32(bytes, 0x54));
        Assert.Equal(original.SignatureComprehensive, BitConverter.ToUInt32(bytes, 0x58));
        Assert.Equal(original.RefCount, BitConverter.ToUInt16(bytes, 0x5C));
        Assert.All(bytes[0x62..0x68], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new crSkeletonData();
        loaded.Read(reader);
        Assert.Equal(original.FirstNodePointer, loaded.FirstNodePointer);
        Assert.Equal(original.BoneIdTableAllowReCompute, loaded.BoneIdTableAllowReCompute);
        Assert.Equal(original.PropertiesPointer, loaded.PropertiesPointer);
        Assert.Equal(original.Signature, loaded.Signature);
        Assert.Equal(original.SignatureNonChiral, loaded.SignatureNonChiral);
        Assert.Equal(original.SignatureComprehensive, loaded.SignatureComprehensive);
        Assert.Equal(original.RefCount, loaded.RefCount);
    }

    [Fact]
    public void InstanceDataEntryMatchesNativeFieldLayout()
    {
        var original = new grcInstanceData.Entry
        {
            Count = 3,
            Register = 0xA4,
            SamplerStateSet = 5,
            SavedSamplerStateSet = 6,
            DataPointer = 0x0102030405060708
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(16, bytes.Length);
        Assert.Equal(3, bytes[0]);
        Assert.Equal(0xA4, bytes[1]);
        Assert.Equal(5, bytes[2]);
        Assert.Equal(6, bytes[3]);
        Assert.All(bytes[4..8], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grcInstanceData.Entry();
        loaded.Read(reader);
        Assert.Equal(original.Count, loaded.Count);
        Assert.Equal(original.Register, loaded.Register);
        Assert.Equal(original.SamplerStateSet, loaded.SamplerStateSet);
        Assert.Equal(original.SavedSamplerStateSet, loaded.SavedSamplerStateSet);
        Assert.Equal(original.DataPointer, loaded.DataPointer);
    }

    [Fact]
    public void InstanceDataEntriesBlockMatchesNativeAllocationSizes()
    {
        var block = new grcInstanceDataEntriesBlock
        {
            Entries =
            [
                new grcInstanceData.Entry { Count = 0 },
                new grcInstanceData.Entry { Count = 1 },
                new grcInstanceData.Entry { Count = 2 }
            ],
            NameHashes = [(MetaName)1u, (MetaName)2u, (MetaName)3u]
        };

        Assert.Equal(96, block.SpuSize);
        Assert.Equal(144, block.TotalSize);
        Assert.Equal(720, block.BlockLength);
        Assert.Equal(1, block.TextureCount);
    }

    [Fact]
    public void ShaderFxMatchesGrcInstanceDataLayout()
    {
        var original = new grcInstanceData
        {
            BasisHashCode = 0x11223344,
            DrawBucket = 3,
            PhysMtlDeprecated = 4,
            Flags = 0x81,
            SpuSize = 0x1234,
            TotalSize = 0x5678,
            MaterialHashCode = 0x99AABBCC,
            DrawBucketMask = 0x10203040,
            IsInstanced = true,
            UserFlags = 5,
            TextureCount = 6,
            SortKeyDeprecated = 0xDDEEFF00
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(48, bytes.Length);
        Assert.All(bytes[0x0C..0x10], value => Assert.Equal(0, value));
        Assert.Equal(4, bytes[0x12]);
        Assert.Equal(0x81, bytes[0x13]);
        Assert.All(bytes[0x1C..0x20], value => Assert.Equal(0, value));
        Assert.Equal(1, bytes[0x24]);
        Assert.Equal(5, bytes[0x25]);
        Assert.Equal(0, bytes[0x26]);
        Assert.Equal(6, bytes[0x27]);
        Assert.All(bytes[0x2C..0x30], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grcInstanceData();
        loaded.Read(reader);
        Assert.Equal(original.BasisHashCode, loaded.BasisHashCode);
        Assert.Equal(original.DrawBucket, loaded.DrawBucket);
        Assert.Equal(original.PhysMtlDeprecated, loaded.PhysMtlDeprecated);
        Assert.Equal(original.Flags, loaded.Flags);
        Assert.Equal(original.SpuSize, loaded.SpuSize);
        Assert.Equal(original.TotalSize, loaded.TotalSize);
        Assert.Equal(original.MaterialHashCode, loaded.MaterialHashCode);
        Assert.Equal(original.DrawBucketMask, loaded.DrawBucketMask);
        Assert.Equal(original.IsInstanced, loaded.IsInstanced);
        Assert.Equal(original.UserFlags, loaded.UserFlags);
        Assert.Equal(original.TextureCount, loaded.TextureCount);
        Assert.Equal(original.SortKeyDeprecated, loaded.SortKeyDeprecated);
    }

    [Fact]
    public void ShaderGroupMatchesNativeFieldLayout()
    {
        var original = new grmShaderGroup
        {
            ShaderGroupVarsPointer = 0x0102030405060708,
            ShaderGroupVarsCount = 2,
            ShaderGroupVarsCapacity = 3,
            HasInstancedShader = true,
            ShaderDatasPointer = 0x1112131415161718
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        Assert.Equal(64, data.Length);
        byte[] bytes = data.ToArray();
        Assert.Equal((ushort)4, BitConverter.ToUInt16(bytes, 0x30));
        Assert.Equal(1, bytes[0x32]);
        Assert.All(bytes[0x33..0x38], value => Assert.Equal(0, value));
        Assert.All(bytes[0x1C..0x20], value => Assert.Equal(0, value));
        Assert.All(bytes[0x2C..0x30], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grmShaderGroup();
        loaded.Read(reader);
        Assert.Equal(original.ShaderGroupVarsPointer, loaded.ShaderGroupVarsPointer);
        Assert.Equal(original.ShaderGroupVarsCount, loaded.ShaderGroupVarsCount);
        Assert.Equal(original.ShaderGroupVarsCapacity, loaded.ShaderGroupVarsCapacity);
        Assert.Equal((ushort)4, loaded.ContainerSizeQW);
        Assert.True(loaded.HasInstancedShader);
        Assert.Equal(original.ShaderDatasPointer, loaded.ShaderDatasPointer);
    }

    [Fact]
    public void LodMatchesNativeFieldLayout()
    {
        var original = new rmcLod
        {
            ModelsPointer = 0x0102030405060708,
            ModelsCount = 2,
            ModelsCapacity = 3
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        writer.WriteStruct(original);

        byte[] bytes = data.ToArray();
        Assert.Equal(16, bytes.Length);
        Assert.Equal(original.ModelsPointer, BitConverter.ToUInt64(bytes, 0x00));
        Assert.Equal(original.ModelsCount, BitConverter.ToUInt16(bytes, 0x08));
        Assert.Equal(original.ModelsCapacity, BitConverter.ToUInt16(bytes, 0x0A));
        Assert.All(bytes[0x0C..0x10], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = reader.ReadStruct<rmcLod>();
        Assert.Equal(original.ModelsPointer, loaded.ModelsPointer);
        Assert.Equal(original.ModelsCount, loaded.ModelsCount);
        Assert.Equal(original.ModelsCapacity, loaded.ModelsCapacity);
    }

    [Fact]
    public void ModelMatchesNativeFieldLayout()
    {
        var original = new grmModel
        {
            MatrixCount = 3,
            Flags = grmModelFlags.MODEL_RELATIVE | grmModelFlags.RESOURCED,
            Type = 4,
            MatrixIndex = 5,
            Mask = 6,
            SkinFlagAndTessellatedGeometryCount = 7,
            ShaderIndices = [9],
            AABBs = [new AABB_s()],
            Geometries = [new grmGeometryQB()]
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(0x50000038ul, BitConverter.ToUInt64(bytes, 0x08));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x10));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x12));
        Assert.All(bytes[0x14..0x18], value => Assert.Equal(0, value));
        Assert.Equal(0x50000040ul, BitConverter.ToUInt64(bytes, 0x18));
        Assert.Equal(0x50000030ul, BitConverter.ToUInt64(bytes, 0x20));
        Assert.Equal(original.MatrixCount, bytes[0x28]);
        Assert.Equal((byte)original.Flags, bytes[0x29]);
        Assert.Equal(original.Type, bytes[0x2A]);
        Assert.Equal(original.MatrixIndex, bytes[0x2B]);
        Assert.Equal(original.Mask, bytes[0x2C]);
        Assert.Equal(original.SkinFlagAndTessellatedGeometryCount, bytes[0x2D]);
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x2E));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grmModel();
        loaded.Read(reader);
        Assert.Equal(original.MatrixCount, loaded.MatrixCount);
        Assert.Equal(original.Flags, loaded.Flags);
        Assert.Equal(original.Type, loaded.Type);
        Assert.Equal(original.MatrixIndex, loaded.MatrixIndex);
        Assert.Equal(original.Mask, loaded.Mask);
        Assert.Equal(original.SkinFlagAndTessellatedGeometryCount, loaded.SkinFlagAndTessellatedGeometryCount);
        Assert.Equal((ushort)1, loaded.Count);
        Assert.Equal((ushort)9, loaded.ShaderIndices[0]);
    }

    [Fact]
    public void GeometryQbMatchesNativeFieldLayout()
    {
        var original = new grmGeometryQB
        {
            VertexDeclarationPointer = 0x0102030405060708,
            Type = 0,
            VertexBuffer = new grcVertexBuffer { Stride = 32 },
            IndexBuffer = new grcIndexBuffer { IndexCount = 6 },
            VertexData = new VertexData { VertexCount = 4 },
            PrimitiveType = grcDrawMode.drawTris,
            DoubleBuffered = 5,
            MatrixPalette = [7, 8],
            VertexDeclarationOffsetPointer = 0x1112131415161718,
            IndexOffset = 0xA1B2C3D4
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(156, bytes.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(original.VertexDeclarationPointer, BitConverter.ToUInt64(bytes, 0x08));
        Assert.Equal(original.Type, BitConverter.ToInt32(bytes, 0x10));
        Assert.All(bytes[0x14..0x18], value => Assert.Equal(0, value));
        Assert.All(bytes[0x18..0x58], value => Assert.Equal(0, value));
        Assert.Equal(6u, BitConverter.ToUInt32(bytes, 0x58));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 0x5C));
        Assert.Equal((ushort)4, BitConverter.ToUInt16(bytes, 0x60));
        Assert.Equal((byte)grcDrawMode.drawTris, bytes[0x62]);
        Assert.Equal(original.DoubleBuffered, bytes[0x63]);
        Assert.All(bytes[0x64..0x68], value => Assert.Equal(0, value));
        Assert.Equal(0x50000098ul, BitConverter.ToUInt64(bytes, 0x68));
        Assert.Equal((ushort)32, BitConverter.ToUInt16(bytes, 0x70));
        Assert.Equal((ushort)2, BitConverter.ToUInt16(bytes, 0x72));
        Assert.All(bytes[0x74..0x78], value => Assert.Equal(0, value));
        Assert.Equal(original.VertexDeclarationOffsetPointer, BitConverter.ToUInt64(bytes, 0x80));
        Assert.Equal(original.IndexOffset, BitConverter.ToUInt32(bytes, 0x90));
        Assert.All(bytes[0x94..0x98], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grmGeometryQB();
        loaded.Read(reader);
        Assert.Equal(original.VertexDeclarationPointer, loaded.VertexDeclarationPointer);
        Assert.Equal(original.Type, loaded.Type);
        Assert.Equal(6u, loaded.IndexCount);
        Assert.Equal(2u, loaded.PrimitiveCount);
        Assert.Equal((ushort)4, loaded.VertexCount);
        Assert.Equal(original.PrimitiveType, loaded.PrimitiveType);
        Assert.Equal(original.DoubleBuffered, loaded.DoubleBuffered);
        Assert.Equal((ushort)32, loaded.Stride);
        Assert.Equal(new ushort[] { 7, 8 }, loaded.MatrixPalette);
        Assert.Equal(original.IndexOffset, loaded.IndexOffset);
    }

    [Fact]
    public void VertexBufferMatchesNativeFieldLayout()
    {
        var lockData = new VertexData { FilePosition = 0x50001000, VertexCount = 3, Stride = 32 };
        var vertexData = new VertexData { FilePosition = 0x50002000, VertexCount = 3, Stride = 32 };
        var vertexFormat = new grcFvf { FilePosition = 0x50003000 };
        var original = new grcVertexBuffer
        {
            Stride = 32,
            Reserved0 = 0x12,
            Flags = grcVertexBufferFlags.DYNAMIC | grcVertexBufferFlags.READ_WRITE,
            LockData = lockData,
            VertexData = vertexData,
            VertexFormat = vertexFormat,
            D3DBufferUnusedPointer = 0x0102030405060708
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(128, bytes.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(original.Stride, BitConverter.ToUInt16(bytes, 0x08));
        Assert.Equal(original.Reserved0, bytes[0x0A]);
        Assert.Equal((byte)original.Flags, bytes[0x0B]);
        Assert.All(bytes[0x0C..0x10], value => Assert.Equal(0, value));
        Assert.Equal((ulong)lockData.FilePosition, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal((uint)lockData.VertexCount, BitConverter.ToUInt32(bytes, 0x18));
        Assert.All(bytes[0x1C..0x20], value => Assert.Equal(0, value));
        Assert.Equal((ulong)vertexData.FilePosition, BitConverter.ToUInt64(bytes, 0x20));
        Assert.All(bytes[0x28..0x30], value => Assert.Equal(0, value));
        Assert.Equal((ulong)vertexFormat.FilePosition, BitConverter.ToUInt64(bytes, 0x30));
        Assert.Equal(original.D3DBufferUnusedPointer, BitConverter.ToUInt64(bytes, 0x38));
        Assert.All(bytes[0x40..0x80], value => Assert.Equal(0, value));
    }

    [Fact]
    public void VertexDataMatchesNativeRawAllocation()
    {
        var vertexFormat = new grcFvf
        {
            Fvf = 1,
            FvfSize = 4,
            FvfChannelSizes = VertexDeclarationTypes.GTAV1
        };
        var original = new VertexData
        {
            Stride = 4,
            VertexCount = 2,
            VertexFormat = vertexFormat,
            Data = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        Assert.Equal(original.Data, data.ToArray());
        Assert.Equal(original.Data.Length, original.BlockLength);
        Assert.Equal(8, original.MemoryUsage);

        var reader = new ResourceDataReader(new MemoryStream(data.ToArray()), graphics) { Position = 0x50000000 };
        var loaded = new VertexData();
        loaded.Read(reader, 4, 2, vertexFormat);
        Assert.Equal(original.Data, loaded.Data);
        Assert.Equal(original.Stride, loaded.Stride);
        Assert.Equal(original.VertexCount, loaded.VertexCount);
        Assert.Same(vertexFormat, loaded.VertexFormat);
    }

    [Fact]
    public void GrcFvfMatchesNativeFieldLayout()
    {
        var original = new grcFvf
        {
            Fvf = 0x00004059,
            FvfSize = 36,
            Flags = grcFvfFlags.PRE_TRANSFORM,
            DynamicOrder = 1,
            ChannelCount = 5,
            FvfChannelSizes = VertexDeclarationTypes.GTAV1
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(16, bytes.Length);
        Assert.Equal(original.Fvf, BitConverter.ToUInt32(bytes, 0x00));
        Assert.Equal(original.FvfSize, bytes[0x04]);
        Assert.Equal((byte)original.Flags, bytes[0x05]);
        Assert.Equal(original.DynamicOrder, bytes[0x06]);
        Assert.Equal(original.ChannelCount, bytes[0x07]);
        Assert.Equal((ulong)original.FvfChannelSizes, BitConverter.ToUInt64(bytes, 0x08));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grcFvf();
        loaded.Read(reader);
        Assert.Equal(original.Fvf, loaded.Fvf);
        Assert.Equal(original.FvfSize, loaded.FvfSize);
        Assert.Equal(original.Flags, loaded.Flags);
        Assert.Equal(original.DynamicOrder, loaded.DynamicOrder);
        Assert.Equal(original.ChannelCount, loaded.ChannelCount);
        Assert.Equal(original.FvfChannelSizes, loaded.FvfChannelSizes);
        Assert.True(loaded.IsPreTransform);
        Assert.True(loaded.IsDynamicOrder);
    }

    [Fact]
    public void IndexBufferMatchesNativeFieldLayout()
    {
        var original = new grcIndexBuffer
        {
            Indices = [2, 1, 0],
            IsPreallocatedMemory = true,
            D3DBufferUnusedPointer = 0x0102030405060708
        };
        var indexData = Assert.IsType<ResourceSystemStructBlock<ushort>>(Assert.Single(original.GetReferences()));
        indexData.FilePosition = 0x50000060;
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);
        indexData.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(102, bytes.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(0x01000003u, BitConverter.ToUInt32(bytes, 0x08));
        Assert.All(bytes[0x0C..0x10], value => Assert.Equal(0, value));
        Assert.Equal((ulong)indexData.FilePosition, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal(original.D3DBufferUnusedPointer, BitConverter.ToUInt64(bytes, 0x18));
        Assert.All(bytes[0x20..0x60], value => Assert.Equal(0, value));
        Assert.Equal(6, original.MemoryUsage);

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new grcIndexBuffer();
        loaded.Read(reader);
        Assert.Equal(3u, loaded.IndexCount);
        Assert.True(loaded.IsPreallocatedMemory);
        Assert.Equal(original.Indices, loaded.Indices);
        Assert.Equal(original.D3DBufferUnusedPointer, loaded.D3DBufferUnusedPointer);
    }

    [Fact]
    public void LodContainerOmitsEmptyLodsAndSupportsSparseLods()
    {
        var original = new rmcLodContainer
        {
            FilePosition = 0x50000000,
            Low = [new grmModel()]
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = original.FilePosition };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(80, bytes.Length);
        Assert.Equal(0, original.GetHighPointer());
        Assert.Equal(0, original.GetMedPointer());
        Assert.Equal(original.FilePosition, original.GetLowPointer());
        Assert.Equal(0, original.GetVLowPointer());
        Assert.Equal(0x50000010ul, BitConverter.ToUInt64(bytes, 0x00));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x08));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(bytes, 0x0A));
        Assert.Equal(0x50000020ul, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal(0, new rmcLodContainer().BlockLength);
    }

    [Fact]
    public void DrawableBuildsSourceCompatibleBucketMasks()
    {
        var defaultMaskShader = new grcInstanceData { DrawBucketMask = 0xFF04 };
        var explicitMaskShader = new grcInstanceData { DrawBucketMask = 0x12000008 };
        var drawable = new gtaDrawable
        {
            ShaderGroup = new grmShaderGroup
            {
                Shaders = new ResourcePointerArray64<grcInstanceData>
                {
                    data_items = [defaultMaskShader, explicitMaskShader]
                }
            },
            DrawableModels = new rmcLodContainer
            {
                High =
                [
                    new grmModel { Mask = 5, ShaderIndices = [0] },
                    new grmModel { Mask = 2, ShaderIndices = [1] }
                ]
            }
        };

        drawable.BuildRenderMasks();

        Assert.Equal(0x1200050Cu, drawable.BucketMaskHigh);
        Assert.Equal(0u, drawable.BucketMaskMed);
        Assert.Equal(0u, drawable.BucketMaskLow);
        Assert.Equal(0u, drawable.BucketMaskVlow);
    }

    [Fact]
    public void FragmentPreservesRootChildAndDamagedDrawableIndex()
    {
        var original = new YftFile
        {
            Fragment = new FragType
            {
                RootChild = new FragPhysTypeChild { PristineMass = 12.5f },
                DrawableArray = new ResourcePointerArray64<FragDrawable>
                {
                    data_items = [new FragDrawable(), new FragDrawable()]
                },
                DamagedDrawableIndex = 1
            }
        };

        var loaded = new YftFile();
        loaded.Load(original.Save());

        Assert.NotNull(loaded.Fragment?.RootChild);
        Assert.Equal(12.5f, loaded.Fragment.RootChild.PristineMass);
        Assert.Equal(1, loaded.Fragment.DamagedDrawableIndex);
        Assert.Same(loaded.Fragment.DrawableArray!.data_items[1], loaded.Fragment.DamagedDrawable);

        var xmlLoaded = XmlYft.GetYft(YftXml.GetXml(original));
        Assert.Equal(1, xmlLoaded.Fragment!.DamagedDrawableIndex);
    }

    [Fact]
    public void DrawableMatchesNativeFieldLayout()
    {
        var original = new rmcDrawable
        {
            FileVFT = 0x11223344,
            ShaderGroup = new grmShaderGroup { FilePosition = 0x50001000 },
            SkeletonData = new crSkeletonData { FilePosition = 0x50002000 },
            CullSphereCenter = new SharpDX.Vector3(1, 2, 3),
            CullSphereRadius = 4,
            BoundingBoxMin = new SharpDX.Vector3(5, 6, 7),
            BoundingBoxUserData1 = 0x7F800001,
            BoundingBoxMax = new SharpDX.Vector3(8, 9, 10),
            BoundingBoxUserData2 = 0x7F800002,
            LodThresholdHigh = 11,
            LodThresholdMed = 12,
            LodThresholdLow = 13,
            LodThresholdVlow = 14,
            BucketMaskHigh = 0x01020304,
            BucketMaskMed = 0x11121314,
            BucketMaskLow = 0x21222324,
            BucketMaskVlow = 0x31323334,
            JointData = new crJointData { FilePosition = 0x50003000 },
            HandleIndex = 0x4567
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.IsAssignableFrom<rmcDrawableBase>(original);
        Assert.Equal(176, bytes.Length);
        Assert.Equal(0x50001000UL, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal(0x50002000UL, BitConverter.ToUInt64(bytes, 0x18));
        Assert.Equal(1, BitConverter.ToSingle(bytes, 0x20));
        Assert.Equal(4, BitConverter.ToSingle(bytes, 0x2C));
        Assert.Equal(original.BoundingBoxUserData1, BitConverter.ToUInt32(bytes, 0x3C));
        Assert.Equal(original.BoundingBoxUserData2, BitConverter.ToUInt32(bytes, 0x4C));
        Assert.All(bytes[0x50..0x70], value => Assert.Equal(0, value));
        Assert.Equal(11, BitConverter.ToSingle(bytes, 0x70));
        Assert.Equal(original.BucketMaskHigh, BitConverter.ToUInt32(bytes, 0x80));
        Assert.Equal(0x50003000UL, BitConverter.ToUInt64(bytes, 0x90));
        Assert.Equal(original.HandleIndex, BitConverter.ToUInt16(bytes, 0x98));
        Assert.All(bytes[0x9A..0xA0], value => Assert.Equal(0, value));
        Assert.Equal(0UL, BitConverter.ToUInt64(bytes, 0xA0));
        Assert.Equal(0UL, BitConverter.ToUInt64(bytes, 0xA8));
    }

    [Fact]
    public void GtaDrawableMatchesNativeFieldLayout()
    {
        var original = new gtaDrawable
        {
            Lights = new atArray<CLightAttr>(),
            TintData = new ResourceSimpleList64_byte { FilePosition = 0x50004000 }
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(208, bytes.Length);
        Assert.All(bytes[0xB0..0xC0], value => Assert.Equal(0, value));
        Assert.Equal(0x50004000UL, BitConverter.ToUInt64(bytes, 0xC0));
        Assert.Equal(0UL, BitConverter.ToUInt64(bytes, 0xC8));
    }

    [Fact]
    public void LightAttrMatchesNativeFieldLayout()
    {
        var original = new CLightAttr
        {
            VFT = 0x0102030405060708,
            Position = new SharpDX.Vector3(1, 2, 3),
            BoneTag = unchecked((short)0xABCD),
            Type = LightType.Capsule,
            ShadowBlur = 9,
            ExtraFlags = 4,
            ExtraFlagsBank = 128,
            Extents = new SharpDX.Vector3(4, 5, 6),
            ProjectedTextureKey = 0xDEADBEEF
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        original.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(168, bytes.Length);
        Assert.Equal(original.VFT, BitConverter.ToUInt64(bytes, 0x00));
        Assert.All(bytes[0x14..0x18], value => Assert.Equal(0, value));
        Assert.Equal(original.BoneTag, BitConverter.ToInt16(bytes, 0x24));
        Assert.Equal((byte)original.Type, bytes[0x26]);
        Assert.Equal(original.ShadowBlur, bytes[0x44]);
        Assert.Equal(original.ExtraFlags, bytes[0x45]);
        Assert.All(bytes[0x46..0x48], value => Assert.Equal(0, value));
        Assert.Equal(original.ExtraFlagsBank, BitConverter.ToUInt32(bytes, 0x48));
        Assert.Equal(original.ProjectedTextureKey.Hash, BitConverter.ToUInt32(bytes, 0xA0));
        Assert.All(bytes[0xA4..0xA8], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new CLightAttr();
        loaded.Read(reader);
        Assert.Equal(original.VFT, loaded.VFT);
        Assert.Equal(original.BoneTag, loaded.BoneTag);
        Assert.Equal(original.ExtraFlags, loaded.ExtraFlags);
        Assert.Equal(original.ExtraFlagsBank, loaded.ExtraFlagsBank);
        Assert.Equal(original.Extents, loaded.Extents);
        Assert.Equal(original.ProjectedTextureKey, loaded.ProjectedTextureKey);
    }
}
