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
        Assert.Equal(7.5f, first.globalAnimUV0.X);
        Assert.Equal(3.75f, second.globalAnimUV1.X);
    }
}
