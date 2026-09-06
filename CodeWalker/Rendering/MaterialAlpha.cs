namespace CodeWalker.Rendering
{
    internal static class MaterialAlpha
    {
        // Renderer.h: opaque, alpha, decal, cutout, nosplash, nowater, water, displ_alpha.
        // Texture alpha can store material data; only the render bucket makes it coverage.
        public static uint Mode(uint shader, byte bucket) => bucket switch
        {
            0 or 4 or 5 => 3,
            1 or 3 when shader == 2219447268 || shader == 3091995132 => 4,
            1 or 7 => 2,
            3 => 1,
            _ => 0 // decals and specialized/runtime buckets retain their own rules
        };
    }
}
