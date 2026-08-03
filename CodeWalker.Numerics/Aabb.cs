namespace CodeWalker.Numerics
{
    /// <summary>
    /// Axis-aligned bounding box transform used by LOD-light hashing.
    /// Reproduces the game's center + abs(rotation)*extent method (same as
    /// CodeWalker's BoundingBox.Transform, but through the RAGE SSE engine so the
    /// x10-truncated min/max fed into the Jenkins hash match the runtime bit-for-bit).
    /// </summary>
    public static class AabbV
    {
        public static void Transform(in Mat34V mat, Vec3V localMin, Vec3V localMax, out Vec3V worldMin, out Vec3V worldMax)
        {
            var half = new Vec3V(0.5f, 0.5f, 0.5f);
            var center = (localMin + localMax) * half;
            var extent = (localMax - localMin) * half;

            var newCenter = mat.Transform(center);
            // extent transforms by |rotation| (no translation)
            var absMat = new Mat34V(
                Vec.Abs(mat.Col0), Vec.Abs(mat.Col1), Vec.Abs(mat.Col2), default);
            var newExtent = absMat.Transform3x3(extent);

            worldMin = newCenter - newExtent;
            worldMax = newCenter + newExtent;
        }
    }
}
