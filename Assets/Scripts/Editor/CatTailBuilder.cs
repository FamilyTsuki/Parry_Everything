using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Queue continue : les segments TailSeg0…N (fichier Blender ou modèle généré) sont recollés en un seul maillage,
    // déformé par une chaîne régulière de BoneCount os, de la naissance au bout (prefab CatTail : Bone0 → Bone1 → …).
    // Chaque sommet suit les deux os les plus proches, poids fondus d'un os à l'autre : la queue plie en courbe lisse,
    // sans cassure aux anciennes jonctions. La pose de repos (courbe du fichier) est gardée : elle est dans les os.
    // Les os ne tournent qu'autour de Z (plan de profil) ; CatBody les anime.
    internal static class CatTailBuilder
    {
        internal const int BoneCount = 40;
        // Subdivisions du maillage (chaque passe coupe chaque triangle en 4) : sans sommets intermédiaires le long de la
        // queue, le maillage ne peut pas se courber en douceur, quel que soit le nombre d'os
        private const int Subdivisions = 2;
        internal const string MeshName = "CatTail";

        // Chaîne d'os au repos, dans le repère de la racine du prefab (naissance de la queue)
        internal sealed class Chain
        {
            public Vector3[] LocalPositions;    // os k dans le repère de l'os k−1 (le 1er : dans la racine)
            public float[] LocalAngles;         // rotation Z de l'os k par rapport à l'os k−1 (degrés)
        }

        // Remplace les maillages CatTailSeg0…N de la liste par un seul maillage CatTail à os. Null si pas de segments.
        internal static Chain Merge(List<Mesh> meshes, CatRig rig)
        {
            int n = rig.TailSegments;
            if (n <= 0 || rig.TailRotations == null || rig.TailPositions == null) return null;
            var segments = new Mesh[n];
            for (int i = 0; i < n; i++)
            {
                string name = "CatTailSeg" + i;
                segments[i] = meshes.Find(m => m.name == name);
                if (segments[i] == null) return null;
            }

            // Repère de chaque segment dans celui de la racine (même chaîne que l'ancien prefab)
            var frames = new Matrix4x4[n];
            Matrix4x4 frame = Matrix4x4.identity;
            for (int i = 0; i < n; i++)
            {
                frame *= Matrix4x4.TRS(i == 0 ? Vector3.zero : rig.TailPositions[i], rig.TailRotations[i], Vector3.one);
                frames[i] = frame;
            }

            // Ligne centrale : origine de chaque segment, puis le bout du dernier (segments orientés vers −X)
            var line = new List<Vector3>();
            for (int i = 0; i < n; i++) line.Add(frames[i].MultiplyPoint3x4(Vector3.zero));
            line.Add(frames[n - 1].MultiplyPoint3x4(new Vector3(Mathf.Min(-0.01f, segments[n - 1].bounds.min.x), 0f, 0f)));

            // Os régulièrement espacés le long de cette ligne, arrondie (sinon les os gardent les angles des anciennes jonctions)
            Vector3[] points = Resample(Smooth(line, 12), BoneCount + 1);
            var angles = new float[BoneCount];
            for (int k = 0; k < BoneCount; k++)
            {
                Vector3 d = points[k + 1] - points[k];
                angles[k] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 180f;   // l'os pointe vers son −X
            }
            var bindposes = new Matrix4x4[BoneCount];
            var chain = new Chain { LocalPositions = new Vector3[BoneCount], LocalAngles = new float[BoneCount] };
            for (int k = 0; k < BoneCount; k++)
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, angles[k]);
                bindposes[k] = Matrix4x4.TRS(points[k], rot, Vector3.one).inverse;
                if (k == 0)
                {
                    chain.LocalPositions[0] = points[0];
                    chain.LocalAngles[0] = angles[0];
                }
                else
                {
                    chain.LocalPositions[k] = Quaternion.Inverse(Quaternion.Euler(0f, 0f, angles[k - 1])) * (points[k] - points[k - 1]);
                    chain.LocalAngles[k] = Mathf.DeltaAngle(angles[k - 1], angles[k]);
                }
            }

            Mesh tail = Combine(segments, frames);
            for (int pass = 0; pass < Subdivisions; pass++) Subdivide(tail);
            tail.boneWeights = Weights(tail.vertices, points);
            tail.bindposes = bindposes;
            tail.RecalculateBounds();

            foreach (Mesh segment in segments)
            {
                meshes.Remove(segment);
                Object.DestroyImmediate(segment);
            }
            meshes.Add(tail);
            return chain;
        }

        // Un seul maillage dans le repère de la racine : couleurs, fourrure (UV1), veines et sutures (UV2), sous-mesh 0
        private static Mesh Combine(Mesh[] segments, Matrix4x4[] frames)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var fur = new List<Vector4>();
            var veins = new List<Vector4>();
            var tris = new List<int>();
            var tmp = new List<Vector4>();
            for (int i = 0; i < segments.Length; i++)
            {
                Mesh m = segments[i];
                int offset = v.Count;
                foreach (Vector3 p in m.vertices) v.Add(frames[i].MultiplyPoint3x4(p));
                Color[] colors = m.colors;
                for (int j = 0; j < m.vertexCount; j++) c.Add(colors.Length == m.vertexCount ? colors[j] : Color.white);
                m.GetUVs(1, tmp);
                for (int j = 0; j < m.vertexCount; j++) fur.Add(tmp.Count == m.vertexCount ? tmp[j] : Vector4.zero);
                m.GetUVs(2, tmp);
                for (int j = 0; j < m.vertexCount; j++) veins.Add(tmp.Count == m.vertexCount ? tmp[j] : Vector4.zero);
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    foreach (int t in m.GetTriangles(sub)) tris.Add(t + offset);
                }
            }
            var mesh = new Mesh { name = MeshName };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetUVs(1, fur);
            mesh.SetUVs(2, veins);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Chaque triangle coupé en 4 par le milieu de ses côtés. Sommets non partagés entre triangles (le style
        // low-poly les sépare de toute façon) ; couleur, fourrure (UV1), veines et sutures (UV2) interpolées.
        private static void Subdivide(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Color[] c = mesh.colors;
            var fur = new List<Vector4>();
            var veins = new List<Vector4>();
            mesh.GetUVs(1, fur);
            mesh.GetUVs(2, veins);
            int[] t = mesh.triangles;

            var nv = new List<Vector3>(t.Length * 4);
            var nc = new List<Color>(t.Length * 4);
            var nf = new List<Vector4>(t.Length * 4);
            var nvein = new List<Vector4>(t.Length * 4);
            var nt = new List<int>(t.Length * 4);
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int a = t[i], b = t[i + 1], d = t[i + 2];
                // Coins puis milieux : 0 = a, 1 = b, 2 = d, 3 = ab, 4 = bd, 5 = da
                int start = nv.Count;
                foreach ((int p, int q) in new[] { (a, a), (b, b), (d, d), (a, b), (b, d), (d, a) })
                {
                    nv.Add((v[p] + v[q]) * 0.5f);
                    nc.Add(c.Length == v.Length ? (c[p] + c[q]) * 0.5f : Color.white);
                    nf.Add(fur.Count == v.Length ? (fur[p] + fur[q]) * 0.5f : Vector4.zero);
                    nvein.Add(veins.Count == v.Length ? (veins[p] + veins[q]) * 0.5f : Vector4.zero);
                }
                nt.AddRange(new[]
                {
                    start, start + 3, start + 5,
                    start + 3, start + 1, start + 4,
                    start + 5, start + 4, start + 2,
                    start + 3, start + 4, start + 5,
                });
            }

            mesh.Clear();
            mesh.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(nv);
            mesh.SetColors(nc);
            mesh.SetUVs(1, nf);
            mesh.SetUVs(2, nvein);
            mesh.SetTriangles(nt, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // Poids : place du sommet le long de la ligne des os (point le plus proche), fondue entre les deux os voisins.
        // Os k centré au milieu de son tronçon : au milieu, il porte seul ; vers ses bouts, il partage avec le voisin.
        private static BoneWeight[] Weights(Vector3[] vertices, Vector3[] points)
        {
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float along = ClosestAlong(points, vertices[i]);   // 0 à BoneCount, en longueurs d'os
                float b = along - 0.5f;
                int k0 = Mathf.FloorToInt(b);
                float f = b - k0;
                if (k0 < 0) weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                else if (k0 >= BoneCount - 1) weights[i] = new BoneWeight { boneIndex0 = BoneCount - 1, weight0 = 1f };
                else
                {
                    // Lissé (smoothstep) : pas de cassure de la déformation au milieu des os
                    float s = f * f * (3f - 2f * f);
                    weights[i] = new BoneWeight { boneIndex0 = k0, weight0 = 1f - s, boneIndex1 = k0 + 1, weight1 = s };
                }
            }
            return weights;
        }

        // Position le long de la ligne (en nombre de tronçons, 0 à points.Length − 1) du point le plus proche de p
        private static float ClosestAlong(Vector3[] points, Vector3 p)
        {
            float best = float.MaxValue, result = 0f;
            for (int k = 0; k + 1 < points.Length; k++)
            {
                Vector3 a = points[k], ab = points[k + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    result = k + t;
                }
            }
            // Avant la naissance ou après le bout : prolongé (le sommet garde l'os du bout)
            if (result <= 0f) result = Mathf.Min(0f, Vector3.Dot(p - points[0], points[1] - points[0]));
            return result;
        }

        // Courbe passant par les points de la ligne brisée (Catmull-Rom), steps points par tronçon
        private static List<Vector3> Smooth(List<Vector3> line, int steps)
        {
            var result = new List<Vector3>();
            int n = line.Count;
            for (int i = 0; i + 1 < n; i++)
            {
                Vector3 p0 = line[Mathf.Max(0, i - 1)], p1 = line[i], p2 = line[i + 1], p3 = line[Mathf.Min(n - 1, i + 2)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                                       + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(line[n - 1]);
            return result;
        }

        // count points régulièrement espacés le long de la ligne brisée
        private static Vector3[] Resample(List<Vector3> line, int count)
        {
            var lengths = new float[line.Count];
            for (int i = 1; i < line.Count; i++) lengths[i] = lengths[i - 1] + (line[i] - line[i - 1]).magnitude;
            float total = lengths[line.Count - 1];
            var result = new Vector3[count];
            int seg = 1;
            for (int k = 0; k < count; k++)
            {
                float at = total * k / (count - 1f);
                while (seg < line.Count - 1 && lengths[seg] < at) seg++;
                float span = Mathf.Max(1e-8f, lengths[seg] - lengths[seg - 1]);
                result[k] = Vector3.Lerp(line[seg - 1], line[seg], Mathf.Clamp01((at - lengths[seg - 1]) / span));
            }
            return result;
        }
    }
}
