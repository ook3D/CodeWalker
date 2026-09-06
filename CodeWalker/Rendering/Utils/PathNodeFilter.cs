using System;
using System.Collections.Generic;
using SharpDX;

namespace CodeWalker.Rendering
{
    internal static class PathNodeFilter
    {
        // Return the original array when nothing is excluded, allowing the renderer to
        // reuse its existing GPU buffer. Keep order and the original distance/NaN rules.
        internal static Vector4[] Exclude(Vector4[] nodes, Vector3 position)
        {
            int first = 0;
            while (first < nodes.Length && Keep(nodes[first], position)) first++;
            if (first == nodes.Length) return nodes;

            var filtered = new List<Vector4>();
            for (int i = 0; i < first; i++) filtered.Add(nodes[i]);
            for (int i = first + 1; i < nodes.Length; i++)
                if (Keep(nodes[i], position)) filtered.Add(nodes[i]);
            return filtered.ToArray();
        }

        private static bool Keep(Vector4 node, Vector3 position) =>
            (new Vector3(node.X, node.Y, node.Z) - position).LengthSquared() > 0.01f;
    }
}
