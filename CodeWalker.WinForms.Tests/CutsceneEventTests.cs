using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class CutsceneEventTests
{
    [Theory]
    [InlineData(0.001f)]
    [InlineData(0.016f)]
    [InlineData(0.1f)]
    public void AuthoredCameraCutsDoNotInterpolateOldOrbitOffsets(float elapsed)
    {
        var camera = new Camera(2, 1, 1) { CurrentDistance = 400, TargetDistance = 400,
            CurrentRotation = new Vector3(1, 0.5f, 0), TargetRotation = new Vector3(1, 0.5f, 0), VOffset = 2 };
        var rotation = Quaternion.RotationYawPitchRoll(1, 0.4f, 0.2f);
        camera.SetAuthoredPose(new Vector3(10, 20, 30), rotation);
        camera.Update(elapsed);
        Assert.Equal(new Vector3(10, 20, 30), camera.Position);
        Assert.True(Vector3.Distance(rotation.Multiply(Vector3.UnitZ), camera.ViewDirection) < 0.00001f);
        camera.SetAuthoredPose(new Vector3(-100, 200, 300), Quaternion.Identity);
        camera.Update(elapsed);
        Assert.Equal(new Vector3(-100, 200, 300), camera.Position);
        Assert.Equal(Vector3.UnitZ, camera.ViewDirection);
        camera.Update(elapsed);
        Assert.NotEqual(new Vector3(-100, 200, 300), camera.Position); // Override is consumed, not sticky.
    }

    private static Cutscene Scene() => new(new CutFile { CutsceneFile2 = new CutsceneFile2() }, null!, null!, null!) { Duration = 10 };
    private static CutEvent AnimationEvent(float time, CutEventType type) => new CutObjectIdEvent
    {
        fTime = time, iEventId = type, iObjectId = 999, // Manager ID, not animated object ID.
        EventArgs = new CutObjectIdEventArgs { iObjectId = 1 }
    };

    [Fact]
    public void SetClearAnimationUsesArgumentTargetAndReferenceCountingOnSeek()
    {
        var scene = Scene();
        var obj = new CutsceneObject();
        scene.SceneObjects[1] = obj;
        scene.PlayEvents = [AnimationEvent(1, CutEventType.EnableAnimation), AnimationEvent(2, CutEventType.EnableAnimation),
            AnimationEvent(3, CutEventType.DisableAnimation), AnimationEvent(4, CutEventType.DisableAnimation)];
        scene.Update(3);
        Assert.True(obj.AnimationControlled);
        Assert.Equal(1, obj.AnimationReferences);
        scene.Update(4);
        Assert.Equal(0, obj.AnimationReferences);
        scene.Update(2);
        Assert.Equal(2, obj.AnimationReferences);
        scene.Update(0);
        Assert.False(obj.AnimationControlled);
        Assert.Equal(0, obj.AnimationReferences);
    }

    [Fact]
    public void AudioEventsReconstructDesiredStateWithoutPlayingDuringSeek()
    {
        var scene = Scene();
        scene.PlayEvents = [new CutEvent { fTime = 2, iEventId = CutEventType.EnableAudio },
            new CutEvent { fTime = 5, iEventId = CutEventType.DisableAudio }];
        scene.Update(1);
        Assert.False(scene.AudioActive);
        scene.Update(3);
        Assert.True(scene.AudioActive);
        scene.Update(6);
        Assert.False(scene.AudioActive);
        scene.Update(3);
        Assert.True(scene.AudioActive);
        scene.Update(0);
        Assert.False(scene.AudioActive);
    }

    [Fact]
    public void SectionHashesContinueUnfinalizedAuthoredHash()
    {
        uint partial = 0;
        foreach (char c in "camera") { partial += (byte)c; partial += partial << 10; partial ^= partial >> 6; }
        Assert.Equal(JenkHash.GenHash("camera-12"), CutsceneAnimationTracks.SectionHash(partial, 12));
        Assert.Equal(JenkHash.GenHash("camera_dual-2"), CutsceneAnimationTracks.SectionHash(partial, 2, true));
    }
}
