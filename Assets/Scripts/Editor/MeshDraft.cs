using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Brouillon de mesh pour les modèles générés : sphères déformées, tubes (pattes, queue), cônes.
    // Sommets partagés dans chaque forme (normales lissées), couleur par sommet, plusieurs sous-meshes (matériaux).
    public sealed class MeshDraft
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<List<int>> submeshes = new List<List<int>>();

        public MeshDraft(int submeshCount = 1)
        {
            for (int i = 0; i < submeshCount; i++) submeshes.Add(new List<int>());
        }

        // Sphère subdivisée, chaque sommet placé par 'shape' (direction unitaire → position) et coloré par 'paint'
        public void AddSphere(int subdivisions, Func<Vector3, Vector3> shape, Func<Vector3, Color> paint, int submesh = 0)
        {
            IcoSphere(subdivisions, out List<Vector3> dirs, out List<int> tris);
            int start = vertices.Count;
            foreach (Vector3 d in dirs)
            {
                vertices.Add(shape(d));
                colors.Add(paint(d));
            }
            int first = submeshes[submesh].Count;
            foreach (int t in tris) submeshes[submesh].Add(start + t);
            // Faces vers l'extérieur : la normale du 1er triangle doit suivre la direction de son sommet
            Vector3 center = shape(Vector3.zero);   // la forme appliquée au centre donne le centre de la sphère
            OrientOutward(submeshes[submesh], first, idx => vertices[idx] - center);
        }

        // Ellipsoïde simple, d'une seule couleur
        public void AddEllipsoid(Vector3 center, Vector3 radii, Color color, int subdivisions = 2, int submesh = 0)
        {
            AddSphere(subdivisions, d => center + Vector3.Scale(d, radii), _ => color, submesh);
        }

        // Tube le long d'une ligne de points (rayon par point), fermé aux deux bouts.
        // depthScale (facultatif) : par point, multiplie le rayon en profondeur (Z) → section ovale (pied large et plat)
        public void AddLoft(IList<Vector3> points, IList<float> radii, int segments, Func<int, float, Color> paint, int submesh = 0,
                            IList<float> depthScale = null)
        {
            int n = points.Count;
            int start = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = points[Mathf.Max(0, i - 1)];
                Vector3 next = points[Mathf.Min(n - 1, i + 1)];
                Vector3 t = (next - prev).normalized;
                Vector3 side = Vector3.Cross(t, Vector3.forward);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(t, Vector3.up);
                side.Normalize();
                Vector3 depth = Vector3.Cross(side, t).normalized;
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    float dz = depthScale != null ? depthScale[i] : 1f;
                    vertices.Add(points[i] + (side * Mathf.Cos(a) + depth * (Mathf.Sin(a) * dz)) * radii[i]);
                    colors.Add(paint(i, a));
                }
            }
            List<int> tri = submeshes[submesh];
            int firstTri = tri.Count;
            for (int i = 0; i < n - 1; i++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = start + i * segments + s;
                    int b = start + i * segments + (s + 1) % segments;
                    int c = a + segments;
                    int d = b + segments;
                    tri.Add(a); tri.Add(c); tri.Add(b);
                    tri.Add(b); tri.Add(c); tri.Add(d);
                }
            }
            // Faces vers l'extérieur du tube
            Vector3 axisPoint = points[0];
            OrientOutward(tri, firstTri, idx => vertices[idx] - axisPoint, tri.Count);

            // Bouchons
            int capStart = vertices.Count;
            vertices.Add(points[0]);
            colors.Add(paint(0, 0f));
            vertices.Add(points[n - 1]);
            colors.Add(paint(n - 1, 0f));
            int capFirst = tri.Count;
            for (int s = 0; s < segments; s++)
            {
                int s1 = (s + 1) % segments;
                tri.Add(capStart); tri.Add(start + s1); tri.Add(start + s);
            }
            Vector3 startOut = points[0] - points[1];
            OrientOutward(tri, capFirst, _ => startOut, tri.Count);
            capFirst = tri.Count;
            for (int s = 0; s < segments; s++)
            {
                int s1 = (s + 1) % segments;
                int last = start + (n - 1) * segments;
                tri.Add(capStart + 1); tri.Add(last + s); tri.Add(last + s1);
            }
            Vector3 endOut = points[n - 1] - points[n - 2];
            OrientOutward(tri, capFirst, _ => endOut, tri.Count);
        }

        // Retourne les triangles [from, to) si la plupart regardent vers l'intérieur.
        // outward(i) : direction « dehors » au sommet i. Unity : face avant = normale cross(b - a, c - a).
        private void OrientOutward(List<int> tri, int from, Func<int, Vector3> outward, int to = -1)
        {
            if (to < 0) to = tri.Count;
            int score = 0;
            for (int i = from; i + 2 < to; i += 3)
            {
                Vector3 a = vertices[tri[i]], b = vertices[tri[i + 1]], c = vertices[tri[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                score += Vector3.Dot(normal, outward(tri[i])) >= 0f ? 1 : -1;
            }
            if (score >= 0) return;
            for (int i = from; i + 2 < to; i += 3)
            {
                int tmp = tri[i + 1];
                tri[i + 1] = tri[i + 2];
                tri[i + 2] = tmp;
            }
        }

        // Cône (oreille) de la base vers la pointe
        public void AddCone(Vector3 baseCenter, Vector3 tip, float radius, int segments, Color color, int submesh = 0)
        {
            AddLoft(new[] { baseCenter, Vector3.Lerp(baseCenter, tip, 0.5f), tip },
                    new[] { radius, radius * 0.6f, 0.002f }, segments, (_, __) => color, submesh);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // ---------- Icosphère ----------

        private static void IcoSphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(verts, cache, a, b), bc = Mid(verts, cache, b, c), ca = Mid(verts, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
        }

        private static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
