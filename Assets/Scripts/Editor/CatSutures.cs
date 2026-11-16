using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Sutures entre les membres détachables (modèle Blender) : bourrelet de chair à vif le long de la jonction
    // et gros points de fil sombre en travers, cousus à la va-vite (de travers, mal espacés, quelques croix,
    // un trou, un bout de fil qui pend).
    //
    // La ligne de jonction est calculée dans l'espace du fichier Blender : c'est là où la surface du membre
    // cousu (enfant) traverse celle de la pièce qui le porte (parent). Les points trouvés sont rangés en anneau
    // autour de leur axe (rayon et hauteur par secteur), puis la suture est construite sur cet anneau.
    // Elle est ajoutée au maillage de l'enfant : elle part avec lui quand il est arraché et suit ses os.
    //
    // Données des sommets ajoutés (lues par FleshCore.cginc) :
    //  - couleur : fil en alpha 0 (reste sombre), bourrelet en alpha 1 (chair : vire à vif, saigne) ;
    //  - UV1 = (bourrelet, numéro du point, 0, 2) : seuil de fourrure au-delà de 1 (jamais de fourrure dessus), rg lus
    //    par le shader à la place de la couleur de fourrure ;
    //  - UV2.y = 1 sur le bourrelet : il s'ouvre en fente sombre quand le membre est abîmé ;
    //  - UV2.z = numéro du point de fil (0,02 à 1) : les plus petits sautent d'abord quand le membre s'abîme.
    internal static class CatSutures
    {
        private const int Sectors = 24;          // secteurs de l'anneau (rayon et hauteur moyens par secteur)
        private const int MinSectors = 6;        // secteurs remplis nécessaires pour reconnaître une jonction
        private const float SharedBorderTolerance = 0.002f;   // bords communs : sommets à moins de 2 mm
        private const float TipInset = 0.015f;   // repli : coupe du bout de l'enfant, à 1,5 cm au moins du contact

        // Surface d'une pièce dans l'espace du fichier : sommets (soudés par position) et triangles
        internal sealed class Surface
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public Surface(IEnumerable<MeshFilter> pieces)
            {
                var welded = new Dictionary<Vector3Int, int>();
                foreach (MeshFilter piece in pieces)
                {
                    Mesh mesh = piece.sharedMesh;
                    if (mesh == null) continue;
                    Vector3[] v = mesh.vertices;
                    var index = new int[v.Length];
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 p = piece.transform.TransformPoint(v[i]);
                        Vector3Int key = Vector3Int.RoundToInt(p * 100000f);
                        if (!welded.TryGetValue(key, out int id))
                        {
                            id = Points.Count;
                            welded[key] = id;
                            Points.Add(p);
                        }
                        index[i] = id;
                    }
                    // Pièce retournée (miroir) : triangles dans l'autre sens, comme à l'import
                    bool flipped = piece.transform.localToWorldMatrix.determinant < 0f;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        int[] t = mesh.GetTriangles(sub);
                        for (int i = 0; i + 2 < t.Length; i += 3)
                        {
                            Triangles.Add(index[t[i]]);
                            Triangles.Add(index[t[flipped ? i + 2 : i + 1]]);
                            Triangles.Add(index[t[flipped ? i + 1 : i + 2]]);
                        }
                    }
                }

                // Bords ouverts : arêtes d'un seul triangle (pièce découpée dans Blender, ex. cuisse / jarret / pied)
                var count = new Dictionary<long, int>();
                for (int i = 0; i + 2 < Triangles.Count; i += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        long key = EdgeKey(Triangles[i + e], Triangles[i + (e + 1) % 3]);
                        count[key] = count.TryGetValue(key, out int c) ? c + 1 : 1;
                    }
                }
                foreach (KeyValuePair<long, int> edge in count)
                {
                    if (edge.Value != 1) continue;
                    BorderEdges.Add(((int)(edge.Key >> 32), (int)(edge.Key & 0xffffffff)));
                }
            }

            public readonly List<(int a, int b)> BorderEdges = new List<(int a, int b)>();

            public bool IsEmpty => Triangles.Count == 0;

            public Vector3 Center
            {
                get
                {
                    if (Points.Count == 0) return Vector3.zero;
                    var b = new Bounds(Points[0], Vector3.zero);
                    foreach (Vector3 p in Points) b.Encapsulate(p);
                    return b.center;
                }
            }

            // Distance signée au point le plus proche de la surface : négative dedans (côté opposé à la normale)
            public float SignedDistance(Vector3 p)
            {
                float best = float.MaxValue;
                Vector3 closest = Vector3.zero;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i + 2 < Triangles.Count; i += 3)
                {
                    Vector3 a = Points[Triangles[i]], b = Points[Triangles[i + 1]], c = Points[Triangles[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-14f) continue;
                    Vector3 q = ClosestOnTriangle(p, a, b, c);
                    float d = (p - q).sqrMagnitude;
                    // Égalité (point le plus proche sur une arête ou un coin partagé) : normales moyennées
                    if (d < best - 1e-10f)
                    {
                        best = d;
                        closest = q;
                        normal = n.normalized;
                    }
                    else if (d <= best + 1e-10f) normal += n.normalized;
                }
                if (best == float.MaxValue) return float.MaxValue;
                // Le signe se lit sur la normale : dehors si le point est du côté où elle pointe
                float dist = Mathf.Sqrt(best);
                return Vector3.Dot(p - closest, normal) >= 0f ? dist : -dist;
            }
        }

        // Ajoute la suture de la jonction enfant ↔ parent au maillage de l'enfant.
        // toLocal : point du fichier → repère du maillage de l'enfant.
        internal static void Sew(CatModelSettings s, Mesh childMesh, Surface child, Surface parent,
                                 Func<Vector3, Vector3> toLocal, int seed)
        {
            if (!s.sutures || childMesh == null || child.IsEmpty || parent.IsEmpty) return;
            // Pièces découpées dans Blender : la jonction est leur bord ouvert commun (la découpe elle-même)
            if (!FitRing(SharedBorder(child, parent), child.Center, out Ring ring))
            {
                var dist = new float[child.Points.Count];
                for (int i = 0; i < dist.Length; i++) dist[i] = parent.SignedDistance(child.Points[i]);
                // Pièces qui se traversent : ligne de croisement ; qui se touchent à peine : coupe du bout de l'enfant
                if (!FitRing(SeamPoints(child, dist), child.Center, out ring)) FitRing(TipSection(s, child, dist), child.Center, out ring);
            }
            if (ring == null)
            {
                Debug.Log("Sutures : jonction de " + childMesh.name + " introuvable (les pièces ne se touchent pas ?).");
                return;
            }

            var d = new MeshDraft { LowPoly = s.style == CatModelSettings.Style.Facets };
            var rng = new System.Random(s.seed * 7919 + seed * 104729);
            AddLip(s, d, ring, rng);
            int lipEnd = d.VertexCount;
            List<(int from, int to, float id)> stitches = AddStitches(s, d, ring, rng);
            Mesh suture = d.ToMesh("Suture");
            Append(childMesh, suture, lipEnd, stitches, toLocal);
            UnityEngine.Object.DestroyImmediate(suture);
        }

        // ---------- Ligne de jonction ----------

        // Bord ouvert de l'enfant qui coïncide avec un bord ouvert du parent (à SharedBorderTolerance près) :
        // sommets communs et milieux des arêtes communes
        private static List<Vector3> SharedBorder(Surface child, Surface parent)
        {
            var parentBorder = new HashSet<int>();
            foreach ((int a, int b) in parent.BorderEdges)
            {
                parentBorder.Add(a);
                parentBorder.Add(b);
            }
            var result = new List<Vector3>();
            if (parentBorder.Count == 0) return result;

            float tol = SharedBorderTolerance * SharedBorderTolerance;
            var onBorder = new Dictionary<int, bool>();
            bool Shared(int i)
            {
                if (onBorder.TryGetValue(i, out bool known)) return known;
                bool hit = false;
                foreach (int j in parentBorder)
                {
                    if ((child.Points[i] - parent.Points[j]).sqrMagnitude > tol) continue;
                    hit = true;
                    break;
                }
                onBorder[i] = hit;
                return hit;
            }
            foreach ((int a, int b) in child.BorderEdges)
            {
                if (!Shared(a) || !Shared(b)) continue;
                result.Add(child.Points[a]);
                result.Add((child.Points[a] + child.Points[b]) * 0.5f);
            }
            return result;
        }

        // Points où les arêtes de l'enfant traversent la surface du parent (dist : distance signée de chaque sommet)
        private static List<Vector3> SeamPoints(Surface child, float[] dist)
        {
            return Crossings(child, i => dist[i]);
        }

        // Coupe de l'enfant par un plan, un peu en retrait de sa partie la plus proche du parent
        // (perpendiculaire à la direction contact → centre de l'enfant) : le tour du bout du membre
        private static List<Vector3> TipSection(CatModelSettings s, Surface child, float[] dist)
        {
            var order = new List<int>(dist.Length);
            for (int i = 0; i < dist.Length; i++) order.Add(i);
            order.Sort((a, b) => dist[a].CompareTo(dist[b]));
            int near = Mathf.Clamp(dist.Length / 20, MinSectors, dist.Length);
            Vector3 contact = Vector3.zero;
            for (int k = 0; k < near; k++) contact += child.Points[order[k]];
            contact /= near;
            Vector3 axis = child.Center - contact;
            if (axis.sqrMagnitude < 1e-8f) return new List<Vector3>();
            axis.Normalize();
            Vector3 origin = contact + axis * Mathf.Max(s.sutureLip * 1.5f, TipInset);
            return Crossings(child, i => Vector3.Dot(child.Points[i] - origin, axis));
        }

        // Points où les arêtes de la surface passent d'une valeur négative à positive (interpolés)
        private static List<Vector3> Crossings(Surface surface, Func<int, float> value)
        {
            var result = new List<Vector3>();
            var seen = new HashSet<long>();
            List<int> t = surface.Triangles;
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = t[i + e], b = t[i + (e + 1) % 3];
                    if (!seen.Add(EdgeKey(a, b))) continue;
                    float da = value(a), db = value(b);
                    if ((da < 0f) == (db < 0f)) continue;
                    result.Add(Vector3.Lerp(surface.Points[a], surface.Points[b], da / (da - db)));
                }
            }
            return result;
        }

        // Anneau de la jonction : centre, axe (vers l'enfant), base du plan, rayon et hauteur par secteur
        private sealed class Ring
        {
            public Vector3 Center, Axis, U, W;
            public readonly float[] Radius = new float[Sectors];
            public readonly float[] Height = new float[Sectors];
            public float Circumference;

            // t de 0 à 1 autour de l'anneau
            public Vector3 Point(float t) => Center + Axis * Sample(Height, t) + Out(t) * Sample(Radius, t);

            // Direction « dehors » (radiale) en t
            public Vector3 Out(float t)
            {
                float a = t * Mathf.PI * 2f;
                return U * Mathf.Cos(a) + W * Mathf.Sin(a);
            }

            public Vector3 Tangent(float t) => (Point(t + 0.002f) - Point(t - 0.002f)).normalized;

            private static float Sample(float[] values, float t)
            {
                float f = Mathf.Repeat(t, 1f) * Sectors;
                int i0 = Mathf.FloorToInt(f) % Sectors;
                return Mathf.Lerp(values[i0], values[(i0 + 1) % Sectors], f - Mathf.Floor(f));
            }
        }

        private static bool FitRing(List<Vector3> points, Vector3 childCenter, out Ring ring)
        {
            ring = null;
            if (points.Count < MinSectors) return false;

            // Centre, en écartant les points isolés (loin de la médiane des distances)
            Vector3 center = Mean(points);
            var spread = new List<float>(points.Count);
            foreach (Vector3 p in points) spread.Add((p - center).magnitude);
            spread.Sort();
            float limit = spread[spread.Count / 2] * 2.5f + 1e-4f;
            points = points.FindAll(p => (p - center).magnitude <= limit);
            if (points.Count < MinSectors) return false;
            center = Mean(points);

            // Axe : direction où les points varient le moins (normale du plan moyen)
            Vector3 axis = PlaneNormal(points, center);
            if (Vector3.Dot(axis, childCenter - center) < 0f) axis = -axis;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(axis, u);

            var r = new float[Sectors];
            var h = new float[Sectors];
            var n = new int[Sectors];
            foreach (Vector3 p in points)
            {
                Vector3 rel = p - center;
                float height = Vector3.Dot(rel, axis);
                Vector3 flat = rel - axis * height;
                float angle = Mathf.Atan2(Vector3.Dot(flat, w), Vector3.Dot(flat, u));
                int k = Mathf.FloorToInt(Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * Sectors) % Sectors;
                r[k] += flat.magnitude;
                h[k] += height;
                n[k]++;
            }
            int filled = 0;
            for (int k = 0; k < Sectors; k++)
            {
                if (n[k] == 0) continue;
                r[k] /= n[k];
                h[k] /= n[k];
                filled++;
            }
            if (filled < MinSectors) return false;

            ring = new Ring { Center = center, Axis = axis, U = u, W = w };
            // Secteurs vides : interpolés entre les voisins remplis
            for (int k = 0; k < Sectors; k++)
            {
                if (n[k] > 0)
                {
                    ring.Radius[k] = r[k];
                    ring.Height[k] = h[k];
                    continue;
                }
                int back = 1, ahead = 1;
                while (n[(k - back + Sectors) % Sectors] == 0) back++;
                while (n[(k + ahead) % Sectors] == 0) ahead++;
                int kb = (k - back + Sectors) % Sectors, ka = (k + ahead) % Sectors;
                float f = back / (float)(back + ahead);
                ring.Radius[k] = Mathf.Lerp(r[kb], r[ka], f);
                ring.Height[k] = Mathf.Lerp(h[kb], h[ka], f);
            }
            // Léger lissage : le bruit de la découpe ne doit pas faire trembler le bourrelet
            var rs = (float[])ring.Radius.Clone();
            var hs = (float[])ring.Height.Clone();
            for (int k = 0; k < Sectors; k++)
            {
                int a = (k + Sectors - 1) % Sectors, b = (k + 1) % Sectors;
                ring.Radius[k] = rs[a] * 0.25f + rs[k] * 0.5f + rs[b] * 0.25f;
                ring.Height[k] = hs[a] * 0.25f + hs[k] * 0.5f + hs[b] * 0.25f;
            }
            for (int k = 0; k < Sectors; k++)
            {
                ring.Circumference += (ring.Point((k + 1f) / Sectors) - ring.Point(k / (float)Sectors)).magnitude;
            }
            if (ring.Circumference > 1e-3f) return true;
            ring = null;
            return false;
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ---------- Géométrie ----------

        // Bourrelet : tube fermé le long de l'anneau, un peu sorti de la peau, d'épaisseur inégale
        private static void AddLip(CatModelSettings s, MeshDraft d, Ring ring, System.Random rng)
        {
            if (s.sutureLip <= 0f) return;
            int count = Mathf.Clamp(Mathf.RoundToInt(ring.Circumference / (s.sutureLip * 2.2f)), 10, 48);
            var points = new Vector3[count + 1];
            var radii = new float[count + 1];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float bulge = 0.7f + 0.6f * (float)rng.NextDouble();
                radii[i] = s.sutureLip * bulge;
                points[i] = ring.Point(t) + ring.Out(t) * (s.sutureLip * 0.3f) + ring.Axis * ((float)rng.NextDouble() - 0.5f) * s.sutureLip * 0.6f;
            }
            // Anneau refermé : le dernier point rejoint le premier
            points[count] = points[0];
            radii[count] = radii[0];
            d.AddLoft(points, radii, 6, (_, __) => s.sutureFleshColor);
        }

        // Points de couture en travers de la jonction : arc de fil qui sort de la peau, passe sur le bourrelet
        // et y replonge. Désordre : décalés, de travers, longueurs inégales, quelques trous, des croix,
        // un bout de fil qui pend.
        // Renvoie, pour chaque point, ses sommets [from, to) et son numéro (0,02 à 1) : quand le membre s'abîme,
        // le shader fait sauter les points de plus petit numéro d'abord (coutures qui lâchent).
        private static List<(int from, int to, float id)> AddStitches(CatModelSettings s, MeshDraft d, Ring ring, System.Random rng)
        {
            var stitches = new List<(int from, int to, float id)>();
            int count = Mathf.Max(5, Mathf.RoundToInt(ring.Circumference / s.stitchSpacing));
            float mess = s.stitchMess;
            int loose = rng.Next(count);   // le point d'où pend un bout de fil
            for (int k = 0; k < count; k++)
            {
                if (k != loose && rng.NextDouble() < mess * 0.12f) continue;   // point oublié
                float t = (k + ((float)rng.NextDouble() - 0.5f) * mess * 0.8f) / count;
                float tilt = ((float)rng.NextDouble() * 2f - 1f) * mess * 45f;
                float length = s.stitchLength * (1f + ((float)rng.NextDouble() - 0.5f) * mess * 0.9f);
                int from = d.VertexCount;
                Vector3 end = AddStitch(s, d, ring, t, tilt, length);
                if (rng.NextDouble() < s.stitchCross)
                {
                    // Croix : deuxième passage de l'autre biais
                    float other = -Mathf.Sign(tilt == 0f ? 1f : tilt) * (30f + 20f * (float)rng.NextDouble());
                    AddStitch(s, d, ring, t + ((float)rng.NextDouble() - 0.5f) * 0.2f / count, other, length * 1.05f);
                }
                if (k == loose)
                {
                    // Bout de fil mal coupé : pend et se recourbe
                    Vector3 hang = (Vector3.down * 0.7f + ring.Out(t) * 0.5f).normalized;
                    float len = s.stitchLength * 0.9f;
                    d.AddTuft(end, hang, len, s.stitchThread * 0.9f, ring.Tangent(t) * len * 0.3f, s.threadColor, 1f, 4);
                }
                stitches.Add((from, d.VertexCount, 0.02f + 0.98f * (float)rng.NextDouble()));
            }
            return stitches;
        }

        // Un point de fil ; renvoie son extrémité (côté enfant)
        private static Vector3 AddStitch(CatModelSettings s, MeshDraft d, Ring ring, float t, float tilt, float length)
        {
            Vector3 center = ring.Point(t);
            Vector3 outward = ring.Out(t);
            Vector3 dir = Quaternion.AngleAxis(tilt, outward) * ring.Axis;
            float over = s.sutureLip * 1.25f + s.stitchThread;   // hauteur du fil au-dessus de la jonction
            const int steps = 6;
            var points = new Vector3[steps + 1];
            var radii = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;               // 0 → 1 d'un bout à l'autre
                float arch = Mathf.Sin(u * Mathf.PI);     // 0 aux bouts, 1 au milieu
                points[i] = center + dir * ((u - 0.5f) * length) + outward * (Mathf.Lerp(-s.stitchThread, over, arch));
                radii[i] = s.stitchThread * (i == 0 || i == steps ? 0.8f : 1f);
            }
            d.AddLoft(points, radii, 6, (_, __) => s.threadColor);
            return points[steps];
        }

        // Ajoute la suture au sous-mesh 0 de l'enfant (sommets ramenés dans son repère)
        // stitches : sommets de chaque point de fil et son numéro (UV2.z)
        private static void Append(Mesh target, Mesh suture, int lipEnd, List<(int from, int to, float id)> stitches,
                                   Func<Vector3, Vector3> toLocal)
        {
            var v = new List<Vector3>(target.vertices);
            var c = new List<Color>(target.colors);
            var fur = new List<Vector4>();
            var veins = new List<Vector4>();
            target.GetUVs(1, fur);
            target.GetUVs(2, veins);
            int offset = v.Count;
            while (c.Count < offset) c.Add(Color.white);
            while (fur.Count < offset) fur.Add(Vector4.zero);
            while (veins.Count < offset) veins.Add(Vector4.zero);

            Vector3[] sv = suture.vertices;
            Color[] sc = suture.colors;
            for (int i = 0; i < sv.Length; i++)
            {
                v.Add(toLocal(sv[i]));
                c.Add(sc[i]);
                float id = 0f;
                foreach ((int from, int to, float stitchId) in stitches)
                {
                    if (i >= from && i < to) id = stitchId;
                }
                // UV1 : seuil 2 (jamais de fourrure) ; rg = bourrelet, numéro du point (lus par le shader à la place
                // de la couleur de fourrure, inutile ici). UV2 : mêmes données, pour les outils.
                fur.Add(new Vector4(i < lipEnd ? 1f : 0f, id, 0f, 2f));
                veins.Add(new Vector4(0f, i < lipEnd ? 1f : 0f, id, 0f));
            }

            int subCount = Mathf.Max(1, target.subMeshCount);
            var tris = new List<int>[subCount];
            for (int sub = 0; sub < subCount; sub++) tris[sub] = new List<int>(target.GetTriangles(sub));
            foreach (int t in suture.triangles) tris[0].Add(t + offset);

            if (v.Count > 65000) target.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            target.SetVertices(v);
            target.SetColors(c);
            target.SetUVs(1, fur);
            target.SetUVs(2, veins);
            target.subMeshCount = subCount;
            for (int sub = 0; sub < subCount; sub++) target.SetTriangles(tris[sub], sub);
            target.RecalculateNormals();
            target.RecalculateTangents();
            target.RecalculateBounds();
        }

        // ---------- Calcul ----------

        private static Vector3 Mean(List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in points) sum += p;
            return sum / points.Count;
        }

        // Normale du plan moyen : 3e axe de la covariance (après les deux directions les plus étendues)
        private static Vector3 PlaneNormal(List<Vector3> points, Vector3 center)
        {
            Vector3 cx = Vector3.zero, cy = Vector3.zero, cz = Vector3.zero;   // lignes de la matrice de covariance
            foreach (Vector3 p in points)
            {
                Vector3 r = p - center;
                cx += r * r.x;
                cy += r * r.y;
                cz += r * r.z;
            }
            Vector3 e1 = PowerIteration(cx, cy, cz, new Vector3(1f, 0.31f, 0.17f));
            float l1 = Vector3.Dot(e1, Mul(cx, cy, cz, e1));
            // Retire la 1re direction, cherche la 2e
            cx -= e1 * (l1 * e1.x);
            cy -= e1 * (l1 * e1.y);
            cz -= e1 * (l1 * e1.z);
            Vector3 start = Vector3.Cross(e1, Mathf.Abs(e1.y) < 0.9f ? Vector3.up : Vector3.right);
            Vector3 e2 = PowerIteration(cx, cy, cz, start);
            Vector3 normal = Vector3.Cross(e1, e2);
            return normal.sqrMagnitude > 1e-10f ? normal.normalized : Vector3.up;
        }

        private static Vector3 PowerIteration(Vector3 cx, Vector3 cy, Vector3 cz, Vector3 v)
        {
            v.Normalize();
            for (int i = 0; i < 64; i++)
            {
                Vector3 next = Mul(cx, cy, cz, v);
                if (next.sqrMagnitude < 1e-20f) break;
                v = next.normalized;
            }
            return v;
        }

        private static Vector3 Mul(Vector3 cx, Vector3 cy, Vector3 cz, Vector3 v)
        {
            return new Vector3(Vector3.Dot(cx, v), Vector3.Dot(cy, v), Vector3.Dot(cz, v));
        }

        // Point du triangle abc le plus proche de p (Ericson, Real-Time Collision Detection)
        private static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
