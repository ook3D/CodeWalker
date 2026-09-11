using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Runtime.CompilerServices;

namespace CodeWalker.World
{
    public static class RayMeshIntersect
    {
        public static bool RayIntersectDrawable(rmcDrawable drawable, ref Ray ray, float maxDist, out float hitDist)
        {
            hitDist = maxDist;
            bool anyHit = false;

            if (drawable == null) return false;

            var models = drawable.AllModels;
            if (models == null) return false;

            // Only test the highest LOD models (first set)
            // AllModels contains all LODs; we want the first grmModel entries (high detail)
            var hdModels = (drawable as gtaDrawable)?.DrawableModels?.High;
            if (hdModels == null)
            {
                hdModels = (drawable as FragDrawable)?.DrawableModels?.High;
            }
            if (hdModels == null && models.Length > 0)
            {
                // Fallback: use first model
                hdModels = new[] { models[0] };
            }
            if (hdModels == null) return false;

            for (int mi = 0; mi < hdModels.Length; mi++)
            {
                var model = hdModels[mi];
                if (model?.Geometries == null) continue;

                for (int gi = 0; gi < model.Geometries.Length; gi++)
                {
                    var geom = model.Geometries[gi];
                    if (RayIntersectGeometry(geom, ref ray, hitDist, out float geomDist))
                    {
                        hitDist = geomDist;
                        anyHit = true;
                    }
                }
            }

            return anyHit;
        }

        public static bool RayIntersectGeometry(grmGeometryQB geom, ref Ray ray, float maxDist, out float hitDist)
        {
            hitDist = maxDist;
            bool anyHit = false;

            if (geom == null) return false;

            var vdata = geom.VertexData;
            var ibuf = geom.IndexBuffer;

            if (vdata?.Data == null || ibuf?.Indices == null) return false;

            int vertexStride = vdata.Stride;
            int vertexCount = vdata.VertexCount;
            byte[] vertexBytes = vdata.Data;
            ushort[] indices = ibuf.Indices;

            if (vertexStride < 12) return false; // Need at least 3 floats for position
            if (indices.Length < 3) return false;

            int triCount = indices.Length / 3;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = indices[t * 3];
                int i1 = indices[t * 3 + 1];
                int i2 = indices[t * 3 + 2];

                if (i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
                    continue;

                Vector3 v0 = ReadPosition(vertexBytes, i0 * vertexStride);
                Vector3 v1 = ReadPosition(vertexBytes, i1 * vertexStride);
                Vector3 v2 = ReadPosition(vertexBytes, i2 * vertexStride);

                if (RayIntersectsTriangle(ref ray, ref v0, ref v1, ref v2, out float dist))
                {
                    if (dist > 0 && dist < hitDist)
                    {
                        hitDist = dist;
                        anyHit = true;
                    }
                }
            }

            return anyHit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ReadPosition(byte[] bytes, int offset)
        {
            float x = BitConverter.ToSingle(bytes, offset);
            float y = BitConverter.ToSingle(bytes, offset + 4);
            float z = BitConverter.ToSingle(bytes, offset + 8);
            return new Vector3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RayIntersectsTriangle(ref Ray ray, ref Vector3 v0, ref Vector3 v1, ref Vector3 v2, out float t)
        {
            t = 0;
            const float epsilon = 1e-8f;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;

            Vector3 h = Vector3.Cross(ray.Direction, edge2);
            float a = Vector3.Dot(edge1, h);

            if (a > -epsilon && a < epsilon)
                return false; // Ray parallel to triangle

            float f = 1.0f / a;
            Vector3 s = ray.Position - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.Direction, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            t = f * Vector3.Dot(edge2, q);
            return t > epsilon;
        }

        public static bool RayIntersectEntity(YmapEntityDef ent, rmcDrawable drawable, ref Ray worldRay, float maxDist, out float hitDist)
        {
            hitDist = maxDist;

            if (ent == null || drawable == null) return false;

            // Transform ray to entity-local space
            var eorinv = Quaternion.Invert(ent.Orientation);
            var localRay = new Ray();
            localRay.Position = eorinv.Multiply(worldRay.Position - ent.Position);
            localRay.Direction = eorinv.Multiply(worldRay.Direction);

            // Account for entity scale
            var scale = ent.Scale;
            if (scale != Vector3.One)
            {
                localRay.Position = new Vector3(
                    localRay.Position.X / scale.X,
                    localRay.Position.Y / scale.Y,
                    localRay.Position.Z / scale.Z);
                localRay.Direction = Vector3.Normalize(new Vector3(
                    localRay.Direction.X / scale.X,
                    localRay.Direction.Y / scale.Y,
                    localRay.Direction.Z / scale.Z));
            }

            return RayIntersectDrawable(drawable, ref localRay, maxDist, out hitDist);
        }
    }
}
