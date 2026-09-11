using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ClipFormatTests
{
    [Fact]
    public void AnimationInterpolatesAcrossSequenceBlockBoundaries()
    {
        static Sequence Block(float value) => new()
        {
            Sequences = [new AnimSequence { Channels = [new AnimChannelStaticFloat { Value = value }] }],
        };

        var animation = new Animation
        {
            Frames = 128,
            FramesPerChunk = 64,
            Duration = 127.0f,
            Sequences = new ResourcePointerList64<Sequence> { data_items = [Block(1.0f), Block(3.0f)] },
        };

        var frame = animation.GetFramePosition(63.5f);
        var result = animation.EvaluateVector4(frame, 0, true);

        Assert.Equal(2.0f, result.X, 5);
    }

    [Fact]
    public void LinearChannelRoundTripsWithoutChangingItsEncoding()
    {
        const int frameCount = 70;
        var values = Enumerable.Range(0, frameCount)
            .Select(i => 2.0f + (i * 0.125f) + ((i % 7) * 0.01f))
            .ToArray();
        var channel = new AnimChannelLinearFloat
        {
            Quantum = 0.001f,
            Offset = 2.0f,
            Values = values,
        };
        channel.Associate(0, 0);

        var block = new Sequence
        {
            NumFrames = frameCount,
            Sequences = [new AnimSequence { Channels = [channel] }],
        };
        block.BuildData();

        var parsed = new Sequence
        {
            NumFrames = block.NumFrames,
            SegmentSize = block.SegmentSize,
            ConstantSize = block.ConstantSize,
            FrameSize = block.FrameSize,
            Data = block.Data,
        };
        parsed.ParseData();

        var result = Assert.IsType<AnimChannelLinearFloat>(Assert.Single(Assert.Single(parsed.Sequences).Channels));
        Assert.Equal(values.Length, result.Values.Length);
        for (var i = 0; i < values.Length; i++)
            Assert.True(Math.Abs(result.Values[i] - values[i]) <= channel.Quantum,
                $"Frame {i}: expected {values[i]}, got {result.Values[i]}.");
    }

    [Fact]
    public void IndirectChannelPreservesEveryAddressableValue()
    {
        var channel = new AnimChannelIndirectQuantizeFloat
        {
            Quantum = 0.25f,
            Offset = -1.0f,
            Values = [-1.0f, -0.75f, -0.5f, -0.25f],
            Frames = [0, 1, 2, 3, 2, 1],
        };
        channel.Associate(0, 0);
        var block = new Sequence
        {
            NumFrames = 6,
            Sequences = [new AnimSequence { Channels = [channel] }],
        };
        block.BuildData();

        var parsed = new Sequence
        {
            NumFrames = block.NumFrames,
            SegmentSize = block.SegmentSize,
            ConstantSize = block.ConstantSize,
            FrameSize = block.FrameSize,
            Data = block.Data,
        };
        parsed.ParseData();

        var result = Assert.IsType<AnimChannelIndirectQuantizeFloat>(Assert.Single(Assert.Single(parsed.Sequences).Channels));
        Assert.Equal(channel.Frames, result.Frames);
        Assert.Equal(channel.Values, result.Values);
    }

    [Fact]
    public void NormalizeChannelNormalizesAllFourComponents()
    {
        var sequence = new AnimSequence
        {
            Channels =
            [
                new AnimChannelStaticFloat { Value = 1.0f },
                new AnimChannelStaticFloat { Value = 2.0f },
                new AnimChannelStaticFloat { Value = 3.0f },
                new AnimChannelStaticFloat { Value = 4.0f },
                new AnimChannelCachedQuaternion(AnimChannelType.NormalizeQuaternion),
            ],
            NormalizeQuaternion = true,
        };

        var value = sequence.EvaluateQuaternion(0);

        Assert.InRange(Math.Abs(value.Length() - 1.0f), 0.0f, 0.00001f);
        Assert.True(value.W > value.Z);
    }

    [Theory]
    [InlineData(ClipPropertyAttributeType.Float, typeof(ClipPropertyAttributeFloat), 48)]
    [InlineData(ClipPropertyAttributeType.Int, typeof(ClipPropertyAttributeInt), 48)]
    [InlineData(ClipPropertyAttributeType.Bool, typeof(ClipPropertyAttributeBool), 48)]
    [InlineData(ClipPropertyAttributeType.String, typeof(ClipPropertyAttributeString), 48)]
    [InlineData(ClipPropertyAttributeType.BitSet, typeof(ClipPropertyAttributeBitSet), 48)]
    [InlineData(ClipPropertyAttributeType.Vector3, typeof(ClipPropertyAttributeVector3), 48)]
    [InlineData(ClipPropertyAttributeType.Vector4, typeof(ClipPropertyAttributeVector4), 48)]
    [InlineData(ClipPropertyAttributeType.Quaternion, typeof(ClipPropertyAttributeQuaternion), 48)]
    [InlineData(ClipPropertyAttributeType.Matrix34, typeof(ClipPropertyAttributeMatrix34), 96)]
    [InlineData(ClipPropertyAttributeType.Situation, typeof(ClipPropertyAttributeSituation), 64)]
    [InlineData(ClipPropertyAttributeType.Data, typeof(ClipPropertyAttributeData), 48)]
    [InlineData(ClipPropertyAttributeType.HashString, typeof(ClipPropertyAttributeHashString), 48)]
    public void AllNativePropertyAttributeTypesAreConstructible(
        ClipPropertyAttributeType type,
        Type expectedType,
        long expectedSize)
    {
        var attribute = ClipPropertyAttribute.ConstructItem(type);

        Assert.IsType(expectedType, attribute);
        Assert.Equal(type, attribute.Type);
        Assert.Equal(expectedSize, attribute.BlockLength);
    }

    [Fact]
    public void NativePropertyAttributeIdsMatchCrMetadata()
    {
        Assert.Equal(7, (byte)ClipPropertyAttributeType.Vector4);
        Assert.Equal(8, (byte)ClipPropertyAttributeType.Quaternion);
        Assert.Equal(9, (byte)ClipPropertyAttributeType.Matrix34);
        Assert.Equal(10, (byte)ClipPropertyAttributeType.Situation);
        Assert.Equal(11, (byte)ClipPropertyAttributeType.Data);
        Assert.Equal(12, (byte)ClipPropertyAttributeType.HashString);
    }

    [Fact]
    public void ExpressionClipUsesTheNativeDerivedLayout()
    {
        var clip = ClipBase.ConstructClip(ClipType.AnimationExpression);

        Assert.IsType<ClipAnimationExpression>(clip);
        Assert.Equal(112, clip.BlockLength);
    }

    [Fact]
    public void BitSetUsesNativeWordAndBitCounts()
    {
        var bits = new atBitSet(65);
        bits[0] = true;
        bits[64] = true;

        Assert.Equal(3, bits.Words.Length);
        Assert.True(bits[0]);
        Assert.True(bits[64]);
        Assert.False(bits[63]);
    }
}
