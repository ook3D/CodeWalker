using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class CutsceneCameraPersistenceTests
{
    [Fact]
    public void AuthoredCameraPreservesEditorValuesUsedBySettings()
    {
        var position=new Vector3(12,34,56);
        var orientation=Quaternion.RotationAxis(Vector3.UnitZ,0.7f);
        var orbit=new Vector3(0.3f,0.2f,0);
        var entity=new Entity { Position=position, Orientation=orientation, OrientationInv=Quaternion.Invert(orientation) };
        var camera=new Camera(2,1,1) { FollowEntity=entity, CurrentRotation=orbit, TargetRotation=orbit,
            CurrentDistance=10, TargetDistance=10 };
        var authored=new Vector3(100,200,300);
        camera.SetAuthoredPose(authored,Quaternion.Identity);
        camera.Update(0);
        Assert.Equal(authored,camera.Position);
        Assert.Equal(position,entity.Position);
        Assert.Equal(orientation,entity.Orientation);
        Assert.Equal(orbit,camera.CurrentRotation);
        Assert.Equal(10f,camera.TargetDistance);
        camera.SetAuthoredPose(authored,Quaternion.Identity);
        camera.ClearAuthoredPose(); // Closing before the queued render consumes the pose.
        camera.Update(0);
        Assert.NotEqual(authored,camera.Position);
        Assert.Equal(orientation,entity.Orientation);
    }
}
