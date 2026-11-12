using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // Outils pour fabriquer des meshes low-poly à facettes plates (chaque triangle a ses propres sommets).
    // Les triangles sont orientés avec un vecteur 'facing' : la face visible regarde de ce côté.
    public static class LowPoly
    {
        public static void Tri(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                Vector3 t = b; b = c; c = t;
            }
            v.Add(a); v.Add(b); v.Add(c);
        }

        public static void Quad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
        {
            Tri(v, a, b, c, facing);
            Tri(v, a, c, d, facing);
        }

        public static Mesh Build(string name, List<Vector3> v)
        {
            int[] tris = new int[v.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;
            Mesh m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // Icosaèdre de rayon 0.5 (20 faces), sommets un peu décalés si jitter > 0
        public static Mesh Icosahedron(float jitter = 0f, int seed = 0)
        {
            float p = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] pts =
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            int[] t =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            System.Random rng = new System.Random(seed);
            for (int i = 0; i < pts.Length; i++)
            {
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                pts[i] = pts[i].normalized * 0.5f * k;
            }
            List<Vector3> v = new List<Vector3>();
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = pts[t[i]], b = pts[t[i + 1]], c = pts[t[i + 2]];
                Tri(v, a, b, c, a + b + c);
            }
            return Build("Icosahedron", v);
        }

        // Prisme à n côtés le long de +Y, de y=0 (rayon rBottom) à y=h (rayon rTop). rTop = 0 : pointe.
        public static Mesh Prism(int n, float rBottom, float rTop, float h, float twist = 0f)
        {
            List<Vector3> v = new List<Vector3>();
            Vector3[] lo = new Vector3[n], hi = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float b = a + twist;
                lo[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rBottom;
                hi[i] = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * rTop + Vector3.up * h;
            }
            Vector3 mid = Vector3.up * h * 0.5f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 outward = (lo[i] + lo[j] + hi[i] + hi[j]) / 4f - mid;
                if (rTop > 0f) Quad(v, lo[i], lo[j], hi[j], hi[i], outward);
                else Tri(v, lo[i], lo[j], hi[0], outward);
                Tri(v, lo[i], lo[j], Vector3.zero, Vector3.down);
                if (rTop > 0f) Tri(v, hi[i], hi[j], Vector3.up * h, Vector3.up);
            }
            return Build("Prism", v);
        }

        // Boîte de côté 1 centrée ; le dessus est réduit de 'top' (1 = cube)
        public static Mesh Box(float top = 1f)
        {
            Vector3[] p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float y = (i & 4) != 0 ? 0.5f : -0.5f;
                float k = y > 0f ? top : 1f;
                p[i] = new Vector3(((i & 1) != 0 ? 0.5f : -0.5f) * k, y, ((i & 2) != 0 ? 0.5f : -0.5f) * k);
            }
            List<Vector3> v = new List<Vector3>();
            Quad(v, p[0], p[1], p[3], p[2], Vector3.down);
            Quad(v, p[4], p[5], p[7], p[6], Vector3.up);
            Quad(v, p[0], p[1], p[5], p[4], Vector3.back);
            Quad(v, p[2], p[3], p[7], p[6], Vector3.forward);
            Quad(v, p[0], p[2], p[6], p[4], Vector3.left);
            Quad(v, p[1], p[3], p[7], p[5], Vector3.right);
            return Build("Box", v);
        }
    }
}
