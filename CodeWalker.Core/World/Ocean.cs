using System;
using System.Collections.Generic;
using SharpDX;

namespace CodeWalker.World;

public static class Ocean
{
    // Subtract each authored region separately, preserving the ocean between islands.
    public static void GetPatches(Camera camera, ReadOnlySpan<BoundingBox> waterBounds, List<BoundingBox> patches)
    {
        float perspectiveExtent = camera.ZFar * (1 + MathF.Tan(camera.FieldOfView * 0.5f) * MathF.Max(1, camera.AspectRatio));
        float extent = MathF.Max(perspectiveExtent, camera.ZFar + camera.OrthographicSize * (1 + camera.AspectRatio));
        float minX = camera.Position.X - extent;
        float maxX = camera.Position.X + extent;
        float minY = camera.Position.Y - extent;
        float maxY = camera.Position.Y + extent;
        patches.Clear();
        Add(minX, minY, maxX, maxY);
        foreach (var bounds in waterBounds)
        {
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                var patch = patches[i];
                float left = MathF.Max(bounds.Minimum.X, patch.Minimum.X);
                float right = MathF.Min(bounds.Maximum.X, patch.Maximum.X);
                float bottom = MathF.Max(bounds.Minimum.Y, patch.Minimum.Y);
                float top = MathF.Min(bounds.Maximum.Y, patch.Maximum.Y);
                if (left >= right || bottom >= top) continue;
                patches.RemoveAt(i);
                Add(patch.Minimum.X, patch.Minimum.Y, left, patch.Maximum.Y);
                Add(right, patch.Minimum.Y, patch.Maximum.X, patch.Maximum.Y);
                Add(left, patch.Minimum.Y, right, bottom);
                Add(left, top, right, patch.Maximum.Y);
            }
        }

        void Add(float x1, float y1, float x2, float y2)
        {
            if (x1 < x2 && y1 < y2)
                patches.Add(new BoundingBox(new Vector3(x1, y1, 0), new Vector3(x2, y2, 0)));
        }
    }
}
