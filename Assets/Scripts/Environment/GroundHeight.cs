using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Environment
{
    // Hauteur du sol du décor (mesh sans collider) en un point (x, z) du monde.
    // Les sommets et triangles de chaque mesh sont lus une seule fois puis partagés.
    public static class GroundHeight
    {
        private sealed class MeshData
        {
            public Vector3[] Vertices;
            public int[] Triangles;
        }

        private static readonly Dictionary<Mesh, MeshData> cache = new Dictionary<Mesh, MeshData>();

        public static bool TryGet(MeshFilter ground, float x, float z, out float y)
        {
            y = 0f;
            if (ground == null || ground.sharedMesh == null) return false;

            Mesh mesh = ground.sharedMesh;
            if (!cache.TryGetValue(mesh, out MeshData data))
            {
                data = new MeshData { Vertices = mesh.vertices, Triangles = mesh.triangles };
                cache[mesh] = data;
            }

            Transform g = ground.transform;
            Vector3 local = g.InverseTransformPoint(new Vector3(x, 0f, z));
            Vector3[] vs = data.Vertices;
            int[] ts = data.Triangles;
            bool found = false;
            float best = float.MinValue;
            for (int i = 0; i < ts.Length; i += 3)
            {
                Vector3 a = vs[ts[i]];
                Vector3 b = vs[ts[i + 1]];
                Vector3 c = vs[ts[i + 2]];
                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < 1e-6f) continue;
                float u = ((b.z - c.z) * (local.x - c.x) + (c.x - b.x) * (local.z - c.z)) / det;
                float v = ((c.z - a.z) * (local.x - c.x) + (a.x - c.x) * (local.z - c.z)) / det;
                float w = 1f - u - v;
                if (u < 0f || v < 0f || w < 0f) continue;
                float h = u * a.y + v * b.y + w * c.y;
                if (h > best)
                {
                    best = h;
                    found = true;
                }
            }
            if (!found) return false;
            y = g.TransformPoint(new Vector3(local.x, best, local.z)).y;
            return true;
        }
    }
}
