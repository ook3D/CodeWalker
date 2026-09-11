using System.Buffers.Binary;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class BinaryWriterArrayTests
{
    public static IEnumerable<object[]> WriterCases()
    {
        for (int route = 0; route < 3; route++)
        foreach (var endian in new[] { Endianess.LittleEndian, Endianess.BigEndian })
        for (int kind = 0; kind < 9; kind++)
            yield return [route, endian, kind];
    }

    [Theory]
    [MemberData(nameof(WriterCases))]
    public void NumericWritesPreserveBytesAndRoute(int route, Endianess endian, int kind)
    {
        using var output = new MemoryStream();
        using var unused = new MemoryStream();
        DataWriter writer = route switch
        {
            1 => new ResourceDataWriter(output, unused, endian),
            2 => new ResourceDataWriter(unused, output, endian),
            _ => new DataWriter(output, endian)
        };
        long start = route == 0 ? 0 : route == 1 ? 0x50000000 : 0x60000000;
        writer.Position = start + 3;
        byte[] expected;
        switch (kind)
        {
            case 0: writer.Write((byte)0xef); expected = [0xef]; break;
            case 1: writer.Write((short)-12345); expected = BitConverter.GetBytes((short)-12345); break;
            case 2: writer.Write(-123456789); expected = BitConverter.GetBytes(-123456789); break;
            case 3: writer.Write(long.MinValue + 12345); expected = BitConverter.GetBytes(long.MinValue + 12345); break;
            case 4: writer.Write((ushort)0xabcd); expected = BitConverter.GetBytes((ushort)0xabcd); break;
            case 5: writer.Write(0x89abcdefu); expected = BitConverter.GetBytes(0x89abcdefu); break;
            case 6: writer.Write(0x123456789abcdef0ul); expected = BitConverter.GetBytes(0x123456789abcdef0ul); break;
            case 7: writer.Write(BitConverter.Int32BitsToSingle(unchecked((int)0xffc12345))); expected = BitConverter.GetBytes(0xffc12345u); break;
            default: writer.Write(-0.0d); expected = BitConverter.GetBytes(-0.0d); break;
        }
        if (endian == Endianess.BigEndian) Array.Reverse(expected);
        Assert.Equal(expected, output.ToArray()[3..]);
        Assert.Equal(start + 3 + expected.Length, writer.Position);
        Assert.Equal(0, unused.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1024)]
    public void ChunkedStringsPreserveLowBytesAndTerminator(int length)
    {
        string value = new(Enumerable.Range(0, length).Select(i => (char)(i % 3 == 0 ? 0x1234 : i)).ToArray());
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics, Endianess.BigEndian) { Position = 0x60000000 };
        writer.Write(value);
        Assert.Equal(value.Select(c => (byte)c).Append((byte)0), graphics.ToArray());
        Assert.Equal(0x60000000 + length + 1, writer.Position);
        Assert.Equal(0, system.Length);
    }

    [Fact]
    public void ByteArraysAndSpansRemainUnchangedInBigEndianMode()
    {
        byte[] bytes = [1, 2, 3, 4];
        using var output = new MemoryStream();
        var writer = new DataWriter(output, Endianess.BigEndian);
        writer.Write(bytes);
        writer.Write(bytes.AsSpan(1, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 2, 3 }, output.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
    }

    public static IEnumerable<object[]> ArrayCases()
    {
        foreach (bool graphics in new[] { false, true })
        foreach (bool cache in new[] { false, true })
        foreach (var endian in new[] { Endianess.LittleEndian, Endianess.BigEndian })
        for (int kind = 0; kind < 5; kind++)
            yield return [graphics, cache, endian, kind];
    }

    [Theory]
    [MemberData(nameof(ArrayCases))]
    public void PrimitiveArraysPreserveRawLayoutAndCache(bool graphics, bool cache, Endianess endian, int kind)
    {
        byte[] bytes = Enumerable.Range(0, 35).Select(i => (byte)i).ToArray();
        using var input = new PartialReadStream(bytes);
        using var unused = new MemoryStream();
        var reader = graphics ? new ResourceDataReader(unused, input, endian) : new ResourceDataReader(input, unused, endian);
        ulong address = (graphics ? 0x60000000ul : 0x50000000ul) + 3;
        reader.Position = 0x50000001;
        Array result = kind switch
        {
            0 => Assert.IsType<ushort[]>(reader.ReadUshortsAt(address, 4, cache)),
            1 => Assert.IsType<short[]>(reader.ReadShortsAt(address, 4, cache)),
            2 => Assert.IsType<uint[]>(reader.ReadUintsAt(address, 4, cache)),
            3 => Assert.IsType<ulong[]>(reader.ReadUlongsAt(address, 4, cache)),
            _ => Assert.IsType<float[]>(reader.ReadFloatsAt(address, 4, cache))
        };
        var actual = new byte[Buffer.ByteLength(result)];
        Buffer.BlockCopy(result, 0, actual, 0, actual.Length);
        Assert.Equal(bytes[3..(3 + actual.Length)], actual);
        Assert.Equal(0x50000001, reader.Position);
        Assert.Equal(cache, reader.arrayPool.ContainsKey((long)address));
        if (cache) Assert.Same(result, reader.arrayPool[(long)address]);
    }

    [Fact]
    public void FailedArrayReadRestoresPositionAndDoesNotCachePartialData()
    {
        using var input = new PartialReadStream([1, 2, 3]);
        using var unused = new MemoryStream();
        var reader = new ResourceDataReader(input, unused) { Position = 0x60000000 };
        Assert.Throws<EndOfStreamException>(() => reader.ReadUintsAt(0x50000000, 1));
        Assert.Equal(0x60000000, reader.Position);
        Assert.Empty(reader.arrayPool);
        Assert.Throws<OverflowException>(() => reader.ReadUintsAt(0x50000000, uint.MaxValue));
        Assert.Null(reader.ReadUintsAt(0, 1));
        Assert.Null(reader.ReadUintsAt(0x50000000, 0));
    }

    [Fact]
    public void MovementSizingPassStillCountsEveryWrite()
    {
        var movement = new MrfFile { StringTable = [1, 2, 3] };
        byte[] bytes = movement.Save();
        Assert.Equal(43, bytes.Length);
        Assert.Equal(8, movement.DefinitionLength);
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes[^3..]);
    }

    [Fact]
    public void MovementHeaderAndDefinitionBoundaryMatchNativeFormat()
    {
        var movement = new MrfFile
        {
            VersionPatch = 7,
            ExternalReferences =
            [
                new MrfExternalReference { Data = [(byte)'a', 0, 0, 0] },
            ],
            StringTable = [1, 0, 3],
        };

        byte[] bytes = movement.Save();

        Assert.Equal(51, bytes.Length);
        Assert.Equal(MrfFile.ExpectedMagic, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(32)));

        var loaded = new MrfFile();
        loaded.Load(bytes, null!);
        Assert.Equal(7, loaded.VersionPatch);
        Assert.Equal(new byte[] { 1, 0, 3 }, loaded.StringTable);
        Assert.Single(loaded.ExternalReferences);
        Assert.Empty(loaded.AllNodes);
        Assert.Null(loaded.FindMoveNetworkTriggerByName(123u));

        bytes[16] = 1;
        Assert.Throws<InvalidDataException>(() => new MrfFile().Load(bytes, null!));
    }

    [Fact]
    public void MovementNodeParameterIdsMatchNativeDefinitions()
    {
        Assert.Equal((ushort)5, (ushort)MrfNodeParameterId.Animation_Absolute);
        Assert.Equal((ushort)1, (ushort)MrfNodeParameterId.Frame_Owner);
        Assert.Equal((ushort)0, (ushort)MrfNodeParameterId.BlendN_FilterN);
        Assert.Equal((ushort)5, (ushort)MrfNodeParameterId.Clip_Property);
        Assert.Equal((ushort)4, (ushort)MrfNodeParameterId.Pm_ParameterValue);
        Assert.Equal((ushort)5, (ushort)MrfNodeParameterId.Extrapolate_DeltaTime);
        Assert.Equal((ushort)3, (ushort)MrfNodeParameterId.AddN_InputFilter);
        Assert.Equal((ushort)3, (ushort)MrfNodeParameterId.MergeN_InputFilter);
    }

    [Fact]
    public void MovementNodeEventIdsMatchNativeResolvers()
    {
        Assert.Equal((ushort)1, (ushort)MrfNodeEventId.Animation_AnimationEnded);
        Assert.Equal((ushort)4, (ushort)MrfNodeEventId.Clip_ClipTagUpdate);
        Assert.Equal((ushort)1, (ushort)MrfNodeEventId.Pm_PmEnded);
    }

    [Fact]
    public void MovementSignalTypesMatchNativeDefinition()
    {
        Assert.Equal(0u, (uint)MrfSignalType.Animation);
        Assert.Equal(3u, (uint)MrfSignalType.Filter);
        Assert.Equal(6u, (uint)MrfSignalType.ParameterizedMotion);
        Assert.Equal(7u, (uint)MrfSignalType.Real);
        Assert.Equal(12u, (uint)MrfSignalType.Network);
        Assert.Equal(4, (int)MrfWeightModifierType.Step);
    }

    [Fact]
    public void MovementNodeHeaderMatchesNativeDefinition()
    {
        var node = new MrfNodeTail { Index = 7, ID = 0x12345678 };
        using var stream = new MemoryStream();
        var writer = new DataWriter(stream);
        node.Write(writer);

        Assert.Equal(new byte[] { 2, 0, 7, 0, 0x78, 0x56, 0x34, 0x12 }, stream.ToArray());

        stream.Position = 0;
        var loaded = new MrfNodeTail();
        loaded.Read(new DataReader(stream));
        Assert.Equal(MrfNodeType.Tail, loaded.Type);
        Assert.Equal((ushort)7, loaded.Index);
        Assert.Equal((MetaHash)0x12345678, loaded.ID);
    }

    [Fact]
    public void MovementNodeFlagsMatchNativeDefinition()
    {
        var node = new MrfNodeAnimation { Index = 3, ID = 0x12345678, Flags = 0x80000000 };
        using var stream = new MemoryStream();
        var writer = new DataWriter(stream);
        node.Write(writer);

        Assert.Equal(
            new byte[] { 4, 0, 3, 0, 0x78, 0x56, 0x34, 0x12, 0, 0, 0, 0x80 },
            stream.ToArray());

        stream.Position = 0;
        var loaded = new MrfNodeAnimation();
        loaded.Read(new DataReader(stream));
        Assert.Equal(0x80000000u, loaded.Flags);
    }

    [Fact]
    public void MovementPairNodeHeaderMatchesNativeDefinition()
    {
        var node = new MrfNodeBlend
        {
            Index = 4,
            ID = 0x12345678,
            Input0Offset = 0x11223344,
            Input1Offset = 0x55667788,
        };
        using var stream = new MemoryStream();
        node.Write(new DataWriter(stream));
        byte[] bytes = stream.ToArray();

        Assert.Equal(20, bytes.Length);
        Assert.Equal(0x11223344, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(0x55667788, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)));

        stream.Position = 0;
        var loaded = new MrfNodeBlend();
        loaded.Read(new DataReader(stream));
        Assert.Equal(0x11223344, loaded.Input0Offset);
        Assert.Equal(0x55667788, loaded.Input1Offset);
    }

    [Fact]
    public void MovementWeightedPairDataMatchesNativeDefinition()
    {
        var node = new MrfNodeBlend
        {
            WeightType = MrfValueType.Literal,
            Weight = 0.25f,
            FilterType = MrfValueType.Literal,
            FilterDictionaryName = 0x10203040,
            FilterName = 0x50607080,
            Transitional = true,
            Source0Immutable = true,
            Source1Immutable = true,
            Source0InfluenceOverride = MrfInfluenceOverride.Zero,
            Source1InfluenceOverride = MrfInfluenceOverride.One,
            SynchronizerType = MrfSynchronizerType.Tag,
            SynchronizerTag = MrfSynchronizerTagFlags.LeftFootHeel,
            MergeBlend = true,
        };
        using var stream = new MemoryStream();
        node.Write(new DataWriter(stream));
        byte[] bytes = stream.ToArray();
        uint expectedFlags = 1u | (1u << 2) | (1u << 6) | (1u << 7) | (1u << 8)
            | (1u << 12) | (2u << 14) | (1u << 19) | (1u << 31);

        Assert.Equal(36, bytes.Length);
        Assert.Equal(expectedFlags, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal((uint)MrfSynchronizerTagFlags.LeftFootHeel, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(0.25f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(24)));
        Assert.Equal(0x10203040u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28)));
        Assert.Equal(0x50607080u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(32)));

        stream.Position = 0;
        var loaded = new MrfNodeBlend();
        loaded.Read(new DataReader(stream));
        Assert.True(loaded.Transitional);
        Assert.True(loaded.Source0Immutable);
        Assert.True(loaded.Source1Immutable);
        Assert.Equal(MrfInfluenceOverride.Zero, loaded.Source0InfluenceOverride);
        Assert.Equal(MrfInfluenceOverride.One, loaded.Source1InfluenceOverride);
        Assert.Equal(MrfSynchronizerTagFlags.LeftFootHeel, loaded.SynchronizerTag);
        Assert.Equal(0.25f, loaded.Weight);
        Assert.Equal((MetaHash)0x10203040, loaded.FilterDictionaryName);
        Assert.Equal((MetaHash)0x50607080, loaded.FilterName);

        using var invalidStream = new MemoryStream();
        var invalid = new MrfNodeBlend { WeightType = (MrfValueType)3 };
        Assert.Throws<InvalidDataException>(() => invalid.Write(new DataWriter(invalidStream)));
    }

    [Fact]
    public void MovementStateNodeHeaderMatchesNativeDefinition()
    {
        var node = new MrfNodeStateMachine
        {
            Index = 2,
            ID = 0x12345678,
            InitialOffset = 0x11223344,
            DeferBlockUpdate = 1,
            OnEnterEnabled = true,
            OnEnterID = 0x90ABCDEF,
            OnExitID = 0x10203040,
            TransitionsOffset = 0x55667788,
        };
        using var stream = new MemoryStream();
        node.Write(new DataWriter(stream));
        byte[] bytes = stream.ToArray();

        Assert.Equal(32, bytes.Length);
        Assert.Equal(0x11223344, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, bytes[16..20]);
        Assert.Equal(0x90ABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(0x10203040u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(0x55667788, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)));

        stream.Position = 0;
        var loaded = new MrfNodeStateMachine();
        loaded.Read(new DataReader(stream));
        Assert.Equal(1u, loaded.DeferBlockUpdate);
        Assert.True(loaded.OnEnterEnabled);
        Assert.False(loaded.OnExitEnabled);
        Assert.Equal((MetaHash)0x90ABCDEF, loaded.OnEnterID);
        Assert.Equal((MetaHash)0x10203040, loaded.OnExitID);
    }

    [Fact]
    public void MovementTransitionMatchesNativeFlagsAndPayload()
    {
        var transition = new MrfStateTransition
        {
            SynchronizerType = MrfSynchronizerType.Tag,
            SynchronizerTag = MrfSynchronizerTagFlags.RightFootHeel,
            Duration = 0.5f,
            DurationFromParameter = true,
            DurationParameterName = 0x10203040,
            HasTransitionWeightParameter = true,
            TransitionWeightParameterName = 0x50607080,
            TargetStateOffset = 0x11223344,
            BlockUpdateAfterTransition = true,
            Transitional = true,
            Immutable = true,
            Modifier = MrfWeightModifierType.SlowIn,
            ReEvaluate = true,
            HasFilter = true,
            FilterDictionaryName = 0x90ABCDEF,
            FilterName = 0x12345678,
            MergeBlend = true,
            Conditions = [new MrfConditionTimeGreaterThan { Set = 1, Value = 2.0f }],
        };
        using var stream = new MemoryStream();
        transition.Write(new DataWriter(stream));
        byte[] bytes = stream.ToArray();

        uint expectedFlags = (1u << 1) | (1u << 2) | (1u << 3) | (40u << 4)
            | (1u << 18) | (1u << 19) | (1u << 20) | (2u << 24)
            | (1u << 27) | (1u << 28) | (1u << 30) | (1u << 31);
        Assert.Equal(40, bytes.Length);
        Assert.Equal(expectedFlags, BinaryPrimitives.ReadUInt32LittleEndian(bytes));

        stream.Position = 0;
        var loaded = new MrfStateTransition(new DataReader(stream));
        Assert.True(loaded.ReEvaluate);
        Assert.True(loaded.MergeBlend);
        Assert.Equal((ushort)1, loaded.Conditions[0].Set);
        Assert.Equal((MetaHash)0x50607080, loaded.TransitionWeightParameterName);
        Assert.Equal((MetaHash)0x90ABCDEF, loaded.FilterDictionaryName);
    }

    [Fact]
    public void MovementPushValueOperatorReadsItsPacket()
    {
        using var stream = new MemoryStream();
        var writer = new DataWriter(stream);
        writer.Write((uint)MrfOperatorType.PushValue);
        writer.Write(3.25f);
        stream.Position = 0;

        var op = new MrfStateOperatorPushValue(new DataReader(stream));
        Assert.Equal(3.25f, op.Value);
    }

    [Fact]
    public void MovementClipContextsAndPackedBoolsMatchNativeWriter()
    {
        Assert.Equal(1u, (uint)MrfClipContainerType.ClipDictionary);
        Assert.Equal(2u, (uint)MrfClipContainerType.AbsoluteClipSet);
        Assert.Equal(3u, (uint)MrfClipContainerType.LocalFile);

        var clip = new MrfNodeClip
        {
            ClipType = MrfValueType.Literal,
            ClipContainerType = MrfClipContainerType.LocalFile,
            ClipName = 0x12345678,
        };
        using var clipStream = new MemoryStream();
        clip.Write(new DataWriter(clipStream));
        Assert.Equal(20, clipStream.Length);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(clipStream.ToArray().AsSpan(16)));

        var frame = new MrfNodeFrame { OwnerType = MrfValueType.Literal, Owner = true };
        using var frameStream = new MemoryStream();
        frame.Write(new DataWriter(frameStream));
        Assert.Equal(0x01000000u, BinaryPrimitives.ReadUInt32LittleEndian(frameStream.ToArray().AsSpan(12)));
    }

    [Fact]
    public void MovementInlinedStateMachineSerializesTransitions()
    {
        var node = new MrfNodeInlinedStateMachine
        {
            FallbackNodeOffset = 4,
            TransitionsOffset = 8,
            Transitions = [new MrfStateTransition { Duration = 0.25f, TargetStateOffset = 4 }],
        };
        using var stream = new MemoryStream();
        node.Write(new DataWriter(stream));
        Assert.Equal(60, stream.Length);

        stream.Position = 0;
        var loaded = new MrfNodeInlinedStateMachine();
        loaded.Read(new DataReader(stream));
        Assert.Single(loaded.Transitions);
        Assert.Equal(0.25f, loaded.Transitions[0].Duration);
    }

    [Fact]
    public void MovementPreviewFindsFirstResolvableLiteralClip()
    {
        var literal = new MrfNodeClip { ClipType = MrfValueType.Literal, ClipName = 0x12345678 };
        var parameter = new MrfNodeClip { ClipType = MrfValueType.Parameter };
        var blend = new MrfNodeBlend { Input0 = parameter, Input1 = literal };
        var state = new MrfNodeState { InitialNode = blend };

        var movement = new MrfFile();
        Assert.Same(literal, movement.FindPreviewClip(state));

        blend.Input1 = blend;
        Assert.Null(movement.FindPreviewClip(state));
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(3, buffer.Length)]);
    }
}
