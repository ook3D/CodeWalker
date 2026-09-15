using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class WeaponPlacementTests
{
    [Fact]
    public void NewWeaponAndAttachedModelShareTheSameOrigin()
    {
        // The viewer constructs weapons directly, including replacement finish models.
        // An uninitialised YmapEntityDef otherwise renders the body at Vector3.One.
        var body = new Weapon();
        var component = new Weapon();
        var parentBone = Matrix.Translation(0.1f, 0.6f, -0.05f);
        var childBone = Matrix.Translation(0, 0.15f, 0);
        var attachment = Matrix.Invert(childBone) * parentBone;
        attachment.Decompose(out var scale, out var rotation, out var position);
        component.RenderEntity.SetScale(scale);
        component.Position = position;
        component.Rotation = rotation;
        component.UpdateEntity();

        var parentAnchor = body.RenderEntity.Position + body.RenderEntity.Orientation.Multiply(parentBone.TranslationVector);
        var childAnchor = component.RenderEntity.Position + component.RenderEntity.Orientation.Multiply(childBone.TranslationVector);
        Assert.True(Vector3.Distance(parentAnchor, childAnchor) < 0.00001f);
        Assert.Equal(body.Position, body.RenderEntity.Position);
        Assert.Equal(body.Rotation, body.RenderEntity.Orientation);
        Assert.Equal(Vector3.One, body.RenderEntity.Scale);
    }
}
