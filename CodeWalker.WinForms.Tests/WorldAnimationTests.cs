using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using SharpDX;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class WorldAnimationTests
{
    private static ClipMapEntry Clip(byte track, float duration) => new()
    {
        Clip = new ClipAnimation
        {
            EndTime = duration, Rate = 1,
            Animation = new Animation
            {
                Frames = 2, SequenceFrameLimit = 2, Duration = duration,
                BoneIds = new ResourceSimpleList64_s<AnimationBoneId> { data_items = [new() { BoneId = 1, Track = track }] },
                Sequences = new ResourcePointerList64<Sequence> { data_items = [new Sequence { Sequences =
                    [new AnimSequence { Channels = [new AnimChannelRawFloat { Values = [0, 10] }] }] }] }
            }
        }
    };

    [Theory]
    [InlineData(true, false, 2.5f, 7.5f)]
    [InlineData(false, false, 10f, 10f)]
    [InlineData(true, true, 5f, 5f)]
    public void WorldObjectsLoopButExplicitPlaybackKeepsItsTiming(bool loop, bool manual, float first, float second)
    {
        var bone = new crBoneData { BoneId = 1, DefaultRotation = Quaternion.Identity, DefaultScale = Vector3.One };
        var clip = Clip(0, 2);
        clip.OverridePlayTime = manual;
        clip.PlayTime = 1;
        var renderable = new Renderable
        {
            LoopWorldAnimation = loop, ClipMapEntry = clip,
            Skeleton = new crSkeletonData { BonesSorted = [bone], BonesMap = new() { [1] = bone } }
        };
        renderable.UpdateAnims(100.5);
        Assert.Equal(first, bone.AnimTranslation.X);
        renderable.UpdateAnims(101.5);
        Assert.Equal(second, bone.AnimTranslation.X);
        Assert.Equal((ClipFlags)0, clip.Clip!.Flags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void UvPlaybackHoldsThirtyHzSamplesAndStillInterpolatesAnimationData(int channelType)
    {
        var entry = Clip(17, 1.0666671f);
        var clip = (ClipAnimation)entry.Clip!;
        clip.Rate = 1.0000001f;
        clip.Animation!.Frames = 34;
        clip.Animation.SequenceFrameLimit = 64;
        // The supplied video atlas holds cells, with authored transition samples.
        var values = new float[34];
        Array.Fill(values, 0.5f, 9, 25);
        values[8] = 0.42615938f;
        AnimChannel channel = channelType switch
        {
            1 => new AnimChannelQuantizeFloat { Values = values },
            2 => new AnimChannelIndirectQuantizeFloat
            {
                Values = [0, 0.42615938f, 0.5f],
                Frames = values.Select(v => v == 0 ? 0u : v == 0.5f ? 2u : 1u).ToArray()
            },
            _ => new AnimChannelRawFloat { Values = values }
        };
        clip.Animation.Sequences!.data_items[0].Sequences[0].Channels = [channel];
        var geometry = new RenderableGeometry { ClipMapEntryUV = entry };
        var renderable = new Renderable { HDModels = [new RenderableModel { Geometries = [geometry] }] };
        renderable.UpdateAnims(0.250);
        float held = geometry.globalAnimUV0.X;
        renderable.UpdateAnims(0.265);
        Assert.Equal(held, geometry.globalAnimUV0.X);
        renderable.UpdateAnims(0.267);
        float next = geometry.globalAnimUV0.X;
        Assert.NotEqual(held, next);
        Assert.InRange(next, 0.4262f, 0.4999f); // A fractional sample must blend, not snap.
        var frame = clip.Animation.GetFramePosition(clip.GetPlaybackTime(8 * (1f / 30f)));
        Assert.Equal(clip.Animation.EvaluateVector4(frame, 0, true).X, next);
        // Game UV loops use the duration truncated to milliseconds (1066 ms).
        renderable.UpdateAnims(1.333);
        Assert.Equal(next, geometry.globalAnimUV0.X);
        entry.OverridePlayTime = true;
        entry.PlayTime = 0.255f;
        renderable.UpdateAnims(4);
        frame = clip.Animation.GetFramePosition(clip.GetPlaybackTime(entry.PlayTime));
        Assert.Equal(clip.Animation.EvaluateVector4(frame, 0, true).X, geometry.globalAnimUV0.X);
    }

    [Fact]
    public void UvClipsLoopIndependentlyAtTheirOwnDurations()
    {
        var first = new RenderableGeometry { ClipMapEntryUV = Clip(17, 2) };
        var second = new RenderableGeometry { ClipMapEntryUV = Clip(18, 4) };
        var renderable = new Renderable { HDModels = [new RenderableModel { Geometries = [first, second] }] };
        renderable.UpdateAnims(100.5);
        Assert.Equal(2.5f, first.globalAnimUV0.X);
        Assert.Equal(1.25f, second.globalAnimUV1.X);
        renderable.UpdateAnims(101.5);
        Assert.Equal(7.5f, first.globalAnimUV0.X, 5);
        Assert.Equal(3.75f, second.globalAnimUV1.X, 5);
    }
}
