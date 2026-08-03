using System;

namespace CodeWalker.Numerics
{
    /// <summary>
    /// Smoke test for the RAGE SIMD engine. Not a unit-test framework - just one
    /// runnable check that fails loudly if a core sequence breaks.
    /// Call SelfCheck.Run() from a scratch console/LINQPad. Throws on failure.
    /// </summary>
    public static class SelfCheck
    {
        public static void Run()
        {
            // exact ops
            Exact(Vec.Dot3(new Vec3V(1, 2, 3).V, new Vec3V(4, 5, 6).V), 32f, "dot3");            // 4+10+18
            Exact(new Vec3V(3, 4, 0).MagSquared(), 25f, "magSquared");
            Near(new Vec3V(3, 4, 0).Mag(), 5f, 1e-4f, "mag(sqrt)");

            // normalize via rsqrt+1 Newton step -> within a few ULP, not exact
            var n = new Vec3V(3, 4, 0).Normalize();
            Near(n.X, 0.6f, 1e-3f, "normX");
            Near(n.Y, 0.8f, 1e-3f, "normY");
            Near(n.Mag(), 1f, 1e-3f, "normLen");

            // identity quat (0,0,0,1) -> identity rotation; transform must be exact
            var m = Mat34V.FromComponents(new Vec3V(10, 20, 30), new Vec4V(0, 0, 0, 1), new Vec3V(1, 1, 1));
            var p = m.Transform(new Vec3V(1, 2, 3));
            Exact(p.X, 11f, "identX"); Exact(p.Y, 22f, "identY"); Exact(p.Z, 33f, "identZ");

            // 90deg about Z (q = (0,0,sin45,cos45)) rotates +X -> +Y
            float s = MathF.Sin(MathF.PI / 4), c = MathF.Cos(MathF.PI / 4);
            var mz = Mat34V.FromComponents(new Vec3V(0, 0, 0), new Vec4V(0, 0, s, c), new Vec3V(1, 1, 1));
            var rx = mz.Transform(new Vec3V(1, 0, 0));
            Near(rx.X, 0f, 1e-4f, "rotZx"); Near(rx.Y, 1f, 1e-4f, "rotZy");

            // AABB through identity must be unchanged
            AabbV.Transform(m, new Vec3V(-1, -1, -1), new Vec3V(1, 1, 1), out var wmin, out var wmax);
            Exact(wmin.X, 9f, "aabbMinX"); Exact(wmax.Z, 31f, "aabbMaxZ");

            Console.WriteLine("CodeWalker.Numerics SelfCheck: OK");
        }

        private static void Exact(float actual, float expected, string what)
        {
            if (actual != expected) throw new Exception($"SelfCheck {what}: expected {expected}, got {actual}");
        }

        private static void Near(float actual, float expected, float tol, string what)
        {
            if (MathF.Abs(actual - expected) > tol) throw new Exception($"SelfCheck {what}: expected ~{expected}, got {actual}");
        }
    }
}
