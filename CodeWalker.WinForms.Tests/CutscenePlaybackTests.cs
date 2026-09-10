using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class CutscenePlaybackTests
{
    [Fact]
    public void LargeStepsApplyLoadAndPlaybackEventsChronologically()
    {
        var scene = Create();
        scene.LoadEvents = [new CutEvent { fTime = 5, iEventId = CutEventType.LoadScene, EventArgs = new CutLoadSceneEventArgs { vOffset = new Vector3(100) } }];
        scene.PlayEvents = [new CutEvent { fTime = 1, iEventId = CutEventType.LoadScene, EventArgs = new CutLoadSceneEventArgs { vOffset = new Vector3(50) } }];
        scene.Update(7);
        Assert.Equal(new Vector3(100), scene.Position);
        scene.Update(2);
        Assert.Equal(new Vector3(50), scene.Position);
        scene.Update(7);
        Assert.Equal(new Vector3(100), scene.Position);
    }

    [Fact]
    public void HiddenObjectsHandleListsAndSinglePlaybackEvents()
    {
        var scene = Create();
        scene.SceneObjects[1] = new CutsceneObject();
        scene.SceneObjects[2] = new CutsceneObject();
        scene.LoadEvents = [new CutEvent { iEventId = CutEventType.EnableHideObject, EventArgs = new CutObjectIdListEventArgs { iObjectIdList = [1, 2, 999] } }];
        scene.PlayEvents = [new CutObjectIdEvent { fTime = 2, iObjectId = 1, iEventId = CutEventType.ShowHiddenObject }, new CutObjectIdEvent { fTime = 3, iObjectId = 1, iEventId = CutEventType.HideHiddenObject }];
        scene.Update(1);
        Assert.True(scene.SceneObjects[1].Enabled);
        Assert.True(scene.SceneObjects[2].Enabled);
        scene.Update(2);
        Assert.False(scene.SceneObjects[1].Enabled);
        Assert.True(scene.SceneObjects[2].Enabled);
        scene.Update(3);
        Assert.True(scene.SceneObjects[1].Enabled);
        scene.Update(2);
        Assert.False(scene.SceneObjects[1].Enabled);
    }

    private static Cutscene Create() => new(new CutFile { CutsceneFile2 = new CutsceneFile2() }, null!, null!, null!) { Duration = 10 };

    [Fact]
    public void RewindClearsFutureVisibilityAndSceneOffsets()
    {
        var scene = Create();
        scene.SceneObjects[1] = new CutsceneObject();
        scene.SceneObjects[2] = new CutsceneObject();
        scene.PlayEvents =
        [
            new CutEvent { fTime = 1, iEventId = CutEventType.LoadModels, EventArgs = new CutObjectIdListEventArgs { iObjectIdList = [1] } },
            new CutEvent { fTime = 5, iEventId = CutEventType.LoadModels, EventArgs = new CutObjectIdListEventArgs { iObjectIdList = [2] } },
            new CutEvent { fTime = 6, iEventId = CutEventType.LoadScene, EventArgs = new CutLoadSceneEventArgs { vOffset = new Vector3(100, 200, 300) } }
        ];
        scene.Update(7);
        Assert.True(scene.SceneObjects[2].Enabled);
        scene.Update(2);
        Assert.True(scene.SceneObjects[1].Enabled);
        Assert.False(scene.SceneObjects[2].Enabled);
        Assert.Equal(Vector3.Zero, scene.Position);
        Assert.Equal(1, scene.NextPlayEvent);
        scene.Update(7);
        Assert.True(scene.SceneObjects[2].Enabled);
        scene.Update(11);
        Assert.Equal(0, scene.PlaybackTime);
        Assert.False(scene.SceneObjects[1].Enabled);
        Assert.False(scene.SceneObjects[2].Enabled);
    }

    [Fact]
    public void InvalidTimesDoNotPoisonPlayback()
    {
        var scene = Create();
        scene.Update(2);
        scene.Update(float.NaN);
        scene.Update(float.PositiveInfinity);
        Assert.Equal(2, scene.PlaybackTime);
        scene.Update(-1);
        Assert.Equal(0, scene.PlaybackTime);
    }

    [Fact]
    public void MissingEffectsAreReportedIncludingUnknownEventIds()
    {
        var scene = Create();
        scene.LoadEvents = [new CutEvent { iEventId = CutEventType.LoadParticles }];
        scene.PlayEvents = [new CutEvent { iEventId = CutEventType.CameraCut }, new CutEvent { iEventId = CutEventType.LoadParticles }, new CutEvent { iEventId = (CutEventType)999 }];
        Assert.Equal(new[] { CutEventType.LoadParticles, (CutEventType)999 }, scene.UnsupportedEventTypes);
    }

    [Fact]
    public void CameraFovTrackIsReadWithoutAccumulatingWorldTransform()
    {
        var scene = Create();
        scene.Position = new Vector3(10, 20, 30);
        scene.Rotation = Quaternion.Identity;
        scene.CameraObject = new CutsceneObject { Name = 1, Rotation = Quaternion.Identity };
        var animation = new Animation
        {
            Frames = 2, SequenceFrameLimit = 2, Duration = 10,
            BoneIds = new ResourceSimpleList64_s<AnimationBoneId> { data_items = [new AnimationBoneId { BoneId = 0, Track = 27 }] },
            Sequences = new ResourcePointerList64<Sequence>
            {
                data_items = [new Sequence { Sequences = [new AnimSequence { Channels = [new AnimChannelStaticFloat { Value = 60 }] }] }]
            }
        };
        scene.Ycds = [new YcdFile { CutsceneMap = new() { [1] = new ClipMapEntry { Clip = new ClipAnimation { Animation = animation, StartTime = 0, EndTime = 10, Rate = 1 } } } }];
        scene.Update(1);
        var position = scene.CameraObject.Position;
        var rotation = scene.CameraObject.Rotation;
        Assert.Equal(60f, scene.CameraFieldOfViewDegrees);
        scene.Update(2);
        Assert.Equal(position, scene.CameraObject.Position);
        Assert.Equal(rotation, scene.CameraObject.Rotation);
        scene.Ycds = [];
        scene.Update(3);
        Assert.Null(scene.CameraFieldOfViewDegrees);
    }
}
