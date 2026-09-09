using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using System.Linq;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class CutsceneVisualEffectsTests
{
    private static ClipMapEntry Clip(params (byte Track, float Value)[] tracks) => new()
    {
        Clip = new ClipAnimation
        {
            StartTime = 0, EndTime = 10, Rate = 1,
            Animation = new Animation
            {
                Frames = 2, SequenceFrameLimit = 2, Duration = 10,
                BoneIds = new ResourceSimpleList64_s<AnimationBoneId>
                { data_items = tracks.Select(t => new AnimationBoneId { BoneId = 0, Track = t.Track }).ToArray() },
                Sequences = new ResourcePointerList64<Sequence>
                { data_items = [new Sequence { Sequences = tracks.Select(t => new AnimSequence
                    { Channels = [new AnimChannelStaticFloat { Value = t.Value }] }).ToArray() }] }
            }
        }
    };

    [Fact]
    public void StaticLightEventsReplayAfterRewind()
    {
        var scene = new Cutscene(new CutFile { CutsceneFile2 = new CutsceneFile2() }, null!, null!, null!) { Duration = 10 };
        var obj = new CutsceneObject();
        obj.Init(new CutLightObject { iObjectId = 1, iLightType = 2, fIntensity = 3, fFallOff = 20,
            fConeAngle = 1, fInnerConeAngle = 0.4f, vColour = new Vector3(1, 0.5f, 0), vDirection = -Vector3.UnitZ }, null!, null!);
        scene.SceneObjects[1] = obj;
        scene.PlayEvents = [new CutObjectIdEvent { fTime = 1, iEventId = CutEventType.EnableLight, iObjectId = 1 },
            new CutObjectIdEvent { fTime = 3, iEventId = CutEventType.DisableLight, iObjectId = 1 }];
        scene.Update(2);
        Assert.True(obj.Light!.Visible);
        Assert.Equal(new Vector3(6, 3, 0), obj.Light.Light.Colour);
        Assert.Equal(0.4f, obj.Light.Light.ConeInnerAngle);
        scene.Update(4);
        Assert.False(obj.Light.Visible);
        scene.Update(2);
        Assert.True(obj.Light.Visible);
        scene.Update(0);
        Assert.False(obj.Light.Visible);
    }

    [Fact]
    public void AnimatedLightRequiresIntensityAndUsesReferenceExponentTrack()
    {
        var light = new CutsceneLightState(new CutLightObject { iLightType = 1, fIntensity = 100,
            fFallOff = 10, vColour = Vector3.One }, true);
        light.Update(Clip((47, 4)), 1, true);
        Assert.False(light.Visible);
        light.Update(Clip((30, 2), (31, 15), (47, 4)), 1, false);
        Assert.True(light.Visible);
        Assert.Equal(15, light.Light.Falloff);
        Assert.Equal(4, light.Light.FalloffExponent);
        light.Update(null, 2, true);
        Assert.False(light.Visible);
    }

    [Fact]
    public void DofUsesFourPlaneTrackOrderAndDisableEvent()
    {
        var scene = new Cutscene(new CutFile { CutsceneFile2 = new CutsceneFile2() }, null!, null!, null!) { Duration = 10 };
        scene.SceneObjects[1] = new CutsceneObject { Name = 1, Rotation = Quaternion.Identity };
        scene.PlayEvents = [new CutObjectIdEvent { iEventId = CutEventType.CameraCut, iObjectId = 1, EventArgs = new CutCameraCutEventArgs { vRotationQuaternion = Quaternion.Identity } },
            new CutEvent { iEventId = CutEventType.EnableCamera },
            new CutEvent { fTime = 3, iEventId = CutEventType.CameraUnk1 }];
        scene.Ycds = [new YcdFile { CutsceneMap = new() { [1] = Clip((43, 1), (44, 3), (45, 30), (46, 10), (36, 0), (49, 6), (52, 8)) } }];
        scene.Update(1);
        Assert.Equal(new Vector4(1, 3, 10, 30), scene.DepthOfField!.Planes);
        Assert.Equal(1f, scene.DepthOfField.Strength);
        Assert.Equal(8f, scene.DepthOfField.BlurRadius);
        scene.Update(4);
        Assert.Null(scene.DepthOfField);
        scene.Update(1);
        Assert.NotNull(scene.DepthOfField);
        scene.Ycds = [];
        scene.Update(2);
        Assert.Null(scene.DepthOfField);
    }

    [Theory]
    [InlineData(3, 1, 10, 20, 1)]
    [InlineData(1, 3, 2, 20, 1)]
    [InlineData(1, 3, 10, 10, 1)]
    [InlineData(1, 3, 10, 20, 0)]
    [InlineData(1, 3, 10, 20, float.NaN)]
    public void InvalidFocusPlanesDisableBlur(float a, float b, float c, float d, float strength)
        => Assert.False(new CutsceneDepthOfField(new Vector4(a, b, c, d), strength).IsValid);
    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 8)]
    public void FourPlaneDofUsesDayOrNightRadiusDespiteZeroLegacyStrength(bool useDay, float expected)
    {
        var clip=Clip((36,0), (49,6), (52,8)); // choice_int section 0 values
        Assert.Equal(expected,CutsceneAnimationTracks.EvaluateBlurRadius(clip,1.65f,useDay));
    }

    [Theory]
    [InlineData(0,1)]
    [InlineData(7.9f,7)]
    [InlineData(20,15)]
    public void BlurRadiusUsesReferenceFloorAndLimits(float input,float expected)
    {
        Assert.Equal(expected,CutsceneAnimationTracks.EvaluateBlurRadius(Clip((49,input)),0,true));
    }
}
