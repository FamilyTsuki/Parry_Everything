using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Reprend le chat modelé dans Blender (CatModelSettings.source = Blender, fichier « blenderModel »).
    // Voir CatBlenderExport pour la hiérarchie attendue. Une pièce peut être faite de plusieurs objets
    // (crâne + oreilles, avant-bras + main, ou tout morceau ajouté dessous) : ils sont recollés.
    // Torse en Chest / Belly / Pelvis et patte en Thigh / Shin / Foot : gardés tels quels (sinon découpés par Unity).
    // Pour chaque pièce :
    //  - les sommets sont remis en mètres, dans le repère de son articulation (origine de l'objet) ;
    //  - couleur des sommets de Blender = couleur de la fourrure ;
    //  - Unity refait dessous la chair cousue, les plaques de fourrure qui tombent et les veines ;
    //  - tête : yeux lumineux et truffe ajoutés (repères Eye et Nose s'ils existent, sinon places des réglages) ;
    //  - patte : poids des os calculés autour des repères Knee et Ankle (pas d'armature à faire dans Blender).
    // Repère manquant : valeur des réglages. Pièce manquante : le modèle généré est gardé (message dans la console).
    public static class CatBlenderImport
    {
        internal static bool TryBuild(CatModelSettings s, out List<Mesh> meshes, out CatRig rig)
        {
            meshes = null;
            rig = null;
            if (s.source != CatModelSettings.Source.Blender || s.blenderModel == null) return false;

            // Les copies du côté loin (Far_…) ne servent qu'à l'aperçu dans Blender : le jeu recopie le côté caméra
            var nodes = new Dictionary<string, Transform>();
            bool posed = false;   // fichier posé (exporté avec le sol « Ground ») : la pose debout vient du fichier
            foreach (Transform t in s.blenderModel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Ground") posed = true;
                if (!IsPreviewOnly(t.name) && !nodes.ContainsKey(t.name)) nodes[t.name] = t;
            }
            var missing = new List<string>();
            Transform torso = Need(nodes, "Torso", missing);
            Transform head = Need(nodes, "Head", missing);
            Transform upper = Need(nodes, "UpperArm", missing);
            Transform fore = Need(nodes, "Forearm", missing);
            Transform leg = Need(nodes, "Leg", missing);
            var tail = new List<Transform>();
            while (nodes.TryGetValue("TailSeg" + tail.Count, out Transform seg) && Collect(seg).Count > 0) tail.Add(seg);
            if (tail.Count == 0) missing.Add("TailSeg0");
            if (missing.Count > 0)
            {
                Debug.LogWarning("Chat Blender : objets manquants (" + string.Join(", ", missing) + ") dans " + s.blenderModel.name
                                 + ". Le modèle généré est gardé.");
                return false;
            }

            // Sinon (ancien fichier, pièces droites) : pose des réglages
            var origin = new Frame(nodes.TryGetValue("Cat", out Transform cat) ? cat : s.blenderModel.transform);
            rig = CatModelBuilder.GeneratedRig(s);
            rig.TailSegments = tail.Count;
            rig.TailSegmentLength = tail.Count > 1 ? new Frame(tail[0]).Local(tail[1].position).magnitude : s.tailLength / tail.Count;

            // Torse : repère = sa place et son inclinaison (rotation autour de la profondeur)
            Frame torsoFrame = new Frame(torso);
            if (posed)
            {
                float tilt = Mathf.DeltaAngle(0f, origin.Rel(torso.rotation).eulerAngles.z);
                torsoFrame = new Frame(torso.position, origin.rot * Quaternion.Euler(0f, 0f, tilt));
                Vector3 at = origin.Local(torso.position);
                rig.StandingPosition = new Vector2(at.x, at.y);
                rig.StandingTilt = -tilt;
            }
            else rig.SetPoseFrom(s);

            // Attaches : origine de chaque pièce (cou = tête, épaule = bras, hanche = patte, coude = avant-bras)
            rig.Neck = torsoFrame.Local(head.position);
            rig.Shoulder = torsoFrame.Local(upper.position);
            rig.TailBase = torsoFrame.Local(tail[0].position);
            rig.Hip = torsoFrame.Local(leg.position);
            if (!posed) rig.Hip.z = -(Mathf.Abs(rig.Hip.z) + s.hipSpread);
            var upperFrame = new Frame(upper);
            rig.Elbow = upperFrame.Local(fore.position);
            var headFrame = new Frame(head);
            rig.Eye = Point(nodes, "Eye", headFrame, rig.Eye);
            rig.Nose = Point(nodes, "Nose", headFrame, rig.Nose);

            // Queue : place de chaque segment sur le précédent ; courbe de repos si le fichier est posé
            rig.TailPositions = new Vector3[tail.Count];
            for (int i = 1; i < tail.Count; i++) rig.TailPositions[i] = new Frame(tail[i - 1]).Local(tail[i].position);
            if (posed)
            {
                rig.HeadRest = torsoFrame.Rel(head.rotation);
                rig.ArmRest = torsoFrame.Rel(upper.rotation);
                rig.ElbowRest = upperFrame.Rel(fore.rotation);
                rig.TailRotations = new Quaternion[tail.Count];
                for (int i = 0; i < tail.Count; i++)
                {
                    rig.TailRotations[i] = (i == 0 ? torsoFrame : new Frame(tail[i - 1])).Rel(tail[i].rotation);
                }
            }

            // Patte : repère = hanche, écartée (rotation autour de X) ; sa rotation dans le profil est gardée
            // dans la forme (le jeu la retrouve en pliant la patte vers la cheville du fichier)
            Frame legFrame = new Frame(leg);
            if (posed)
            {
                float splay = Mathf.DeltaAngle(0f, origin.Rel(leg.rotation).eulerAngles.x);
                legFrame = new Frame(leg.position, origin.rot * Quaternion.Euler(splay, 0f, 0f));
                rig.LegSplay = splay;
            }
            rig.Knee = Point(nodes, "Knee", legFrame, rig.Knee);
            rig.AnkleFromKnee = Point(nodes, "Ankle", legFrame, rig.Ankle) - rig.Knee;
            if (posed && nodes.TryGetValue("Ankle", out Transform ankle))
            {
                Vector3 ankleAt = origin.Local(ankle.position);
                rig.FootForward = ankleAt.x - origin.Local(leg.position).x;
                rig.AnkleHeight = ankleAt.y + ParryEverything.Player.CatBody.FeetBelowCenter;
            }
            meshes = new List<Mesh>();

            // Torse : en 3 objets (Chest, Belly, Pelvis) s'ils existent, sinon un seul (découpé ensuite par hauteur)
            if (Child(torso, "Belly") != null)
            {
                foreach (string part in TorsoParts)
                {
                    Transform piece = Child(torso, part);
                    if (piece == null) continue;
                    Mesh mesh = Convert(torsoFrame, Collect(piece), "Flesh" + part);
                    Prepare(s, mesh, 1, s.patchCount);
                    meshes.Add(mesh);
                }
            }
            else
            {
                Mesh torsoMesh = Convert(torsoFrame, Collect(torso), "FleshBall");
                Prepare(s, torsoMesh, 1, s.patchCount);
                meshes.Add(torsoMesh);
            }

            // Tête : tous ses morceaux (crâne, oreilles...), puis yeux et truffe (matériau lumineux).
            // Yeux modelés dans Blender (Eye_R, Eye_L...) : repris tels quels avec leurs couleurs, sinon générés.
            Mesh headMesh = Convert(headFrame, Collect(head), "CatHead");
            Prepare(s, headMesh, 11, 5);
            List<MeshFilter> eyePieces = CollectEyes(head);
            var face = new MeshDraft(2);
            CatModelBuilder.AddFace(s, face, rig.Eye, rig.Nose, eyePieces.Count == 0);
            Mesh faceMesh = face.ToMesh("Face");
            AppendSubmesh(headMesh, faceMesh);
            Object.DestroyImmediate(faceMesh);
            if (eyePieces.Count > 0)
            {
                Mesh eyesMesh = Convert(headFrame, eyePieces, "Eyes");
                AppendSubmesh(headMesh, eyesMesh);
                Object.DestroyImmediate(eyesMesh);
            }
            meshes.Add(headMesh);

            Mesh upperMesh = Convert(upperFrame, Collect(upper), "CatUpperArm");
            // Pivot de l'épaule recalé sur le bras : l'origine de UpperArm peut être au milieu du bras
            // (le bras tournerait par son milieu et ressortirait du torse). Bras rigide, la pose ne change pas.
            Vector3 joint = ShoulderJoint(upperMesh, rig.Elbow);
            MoveOrigin(upperMesh, joint);
            rig.Elbow -= joint;
            rig.Shoulder += torsoFrame.Rel(upper.rotation) * joint;
            Prepare(s, upperMesh, 4, 3);
            meshes.Add(upperMesh);
            // Avant-bras et main : deux membres (deux vies), maillages séparés dans le repère du coude.
            // Main = l'objet Hand rangé sous Forearm (et ses morceaux) ; sans lui, main vide (elle reste dans l'avant-bras)
            Mesh foreMesh = Convert(new Frame(fore), Collect(fore), "CatForearm");
            Prepare(s, foreMesh, 5, 3);
            EnsureSubmeshes(foreMesh, 2);   // 2e matériau du prefab (griffes) : sous-mesh vide, les griffes sont dans le modèle
            meshes.Add(foreMesh);
            Transform hand = Child(fore, "Hand");
            Mesh handMesh = hand != null ? Convert(new Frame(fore), Collect(hand), "CatHand") : new Mesh { name = "CatHand" };
            if (hand != null) Prepare(s, handMesh, 6, 2);
            EnsureSubmeshes(handMesh, 2);
            meshes.Add(handMesh);

            // Patte : en 3 objets (Thigh, Shin, Foot) s'ils existent, sinon un seul (découpé ensuite par os)
            if (Child(leg, "Thigh") != null)
            {
                foreach (string part in LegParts)
                {
                    Transform piece = Child(leg, part);
                    if (piece == null) continue;
                    Mesh mesh = Convert(legFrame, Collect(piece), "CatLeg" + part);
                    Prepare(s, mesh, 2, 4);
                    SkinLeg(mesh, rig.Knee, rig.Ankle);
                    meshes.Add(mesh);
                }
            }
            else
            {
                Mesh legMesh = Convert(legFrame, Collect(leg), "CatLegSkin");
                Prepare(s, legMesh, 2, 4);
                SkinLeg(legMesh, rig.Knee, rig.Ankle);
                meshes.Add(legMesh);
            }

            for (int i = 0; i < tail.Count; i++)
            {
                Mesh segMesh = Convert(new Frame(tail[i]), Collect(tail[i]), "CatTailSeg" + i);
                Prepare(s, segMesh, 7 + i, 3);
                meshes.Add(segMesh);
            }
            return true;
        }

        // Repère d'une pièce dans le fichier : position et rotation (sans l'échelle : Blender exporte en cm)
        private readonly struct Frame
        {
            public readonly Vector3 pos;
            public readonly Quaternion rot;

            public Frame(Transform t) : this(t.position, t.rotation) { }

            public Frame(Vector3 position, Quaternion rotation)
            {
                pos = position;
                rot = rotation;
            }

            // Point du monde (du fichier) → ce repère, en mètres
            public Vector3 Local(Vector3 world) => Quaternion.Inverse(rot) * (world - pos);

            // Rotation du monde → rotation par rapport à ce repère
            public Quaternion Rel(Quaternion world) => Quaternion.Inverse(rot) * world;
        }

        // Objets d'aperçu, ignorés : copies du côté loin, sol
        private static bool IsPreviewOnly(string name)
        {
            return name.StartsWith("Far_") || name == "Ground";
        }

        private static readonly string[] TorsoParts = { "Chest", "Belly", "Pelvis" };
        private static readonly string[] LegParts = { "Thigh", "Shin", "Foot" };

        // Noms des pièces : un morceau rangé sous une pièce lui appartient, sauf s'il est lui-même une pièce
        private static bool IsPartName(string name)
        {
            switch (name)
            {
                case "Torso": case "Head": case "UpperArm": case "Forearm": case "Hand": case "Leg":
                case "Chest": case "Belly": case "Pelvis": case "Thigh": case "Shin": case "Foot":
                    return true;
                default:
                    return name.StartsWith("TailSeg");
            }
        }

        // Maillages d'une pièce : le sien et ceux de ses enfants (oreilles, main...), sans descendre dans une autre pièce
        private static List<MeshFilter> Collect(Transform part)
        {
            var list = new List<MeshFilter>();
            CollectInto(part, list, true);
            return list;
        }

        // Yeux modelés : objets « Eye_… » rangés sous la pièce (et leurs morceaux)
        private static bool IsEyeName(string name) => name.StartsWith("Eye_");

        private static List<MeshFilter> CollectEyes(Transform head)
        {
            var list = new List<MeshFilter>();
            foreach (Transform child in head)
            {
                if (IsEyeName(child.name)) CollectInto(child, list, true);
            }
            return list;
        }

        private static void CollectInto(Transform node, List<MeshFilter> list, bool isRoot)
        {
            if (!isRoot && (IsPartName(node.name) || IsPreviewOnly(node.name) || IsEyeName(node.name))) return;
            MeshFilter filter = node.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) list.Add(filter);
            foreach (Transform child in node) CollectInto(child, list, false);
        }

        private static Transform Child(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private static Transform Need(Dictionary<string, Transform> nodes, string name, List<string> missing)
        {
            if (nodes.TryGetValue(name, out Transform t) && HasMeshes(t)) return t;
            missing.Add(name);
            return null;
        }

        // La pièce a au moins un maillage (le sien ou celui d'un morceau, y compris ses parties Chest, Thigh...)
        private static bool HasMeshes(Transform t)
        {
            return t.GetComponentInChildren<MeshFilter>(true) != null;
        }

        // Position d'un repère dans le repère d'une pièce (en mètres), ou valeur par défaut s'il n'existe pas
        private static Vector3 Point(Dictionary<string, Transform> nodes, string name, Frame part, Vector3 fallback)
        {
            return nodes.TryGetValue(name, out Transform t) ? part.Local(t.position) : fallback;
        }

        // Un seul maillage avec tous les morceaux, sommets dans le repère de 'frame' en mètres, sous-meshes réunis
        private static Mesh Convert(Frame frame, List<MeshFilter> pieces, string name)
        {
            var v = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            bool allColored = true;
            foreach (MeshFilter piece in pieces)
            {
                Mesh source = piece.sharedMesh;
                int offset = v.Count;
                foreach (Vector3 p in source.vertices) v.Add(frame.Local(piece.transform.TransformPoint(p)));
                Color[] c = source.colors;
                if (c.Length == source.vertexCount) colors.AddRange(c);
                else
                {
                    allColored = false;
                    for (int i = 0; i < source.vertexCount; i++) colors.Add(Color.clear);
                }
                // Pièce retournée (échelle négative, ex. oreille gauche en miroir) : triangles dans l'autre sens,
                // sinon ses faces seraient à l'envers
                bool flipped = piece.transform.localToWorldMatrix.determinant < 0f;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] st = source.GetTriangles(sub);
                    for (int i = 0; i + 2 < st.Length; i += 3)
                    {
                        tris.Add(st[i] + offset);
                        tris.Add(st[flipped ? i + 2 : i + 1] + offset);
                        tris.Add(st[flipped ? i + 1 : i + 2] + offset);
                    }
                }
            }
            // Morceau sans couleur des sommets : alpha 0, Prepare lui donne la fourrure sombre par défaut
            if (!allColored) Debug.Log("Chat Blender : un morceau de " + name + " n'a pas de couleur des sommets (fourrure sombre).");

            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Couleur des sommets (Blender) = fourrure ; Unity peint dessous la chair cousue, les plaques et les veines
        private static void Prepare(CatModelSettings s, Mesh mesh, int seedOffset, int patches)
        {
            Vector3[] v = mesh.vertices;
            Color[] painted = mesh.colors;
            var fur = new Color[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                fur[i] = painted.Length == v.Length && painted[i].a > 0f ? CatModelBuilder.Opaque(painted[i]) : s.furDark;
            }

            var skin = new CatModelBuilder.FleshSkin(s, s.seed + seedOffset, patches, 0, 0f);
            Vector3 center = mesh.bounds.center;
            var flesh = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) flesh[i] = skin.Paint((v[i] - center).normalized);
            mesh.colors = flesh;
            CatModelBuilder.ApplyFurByIndex(s, mesh, seedOffset, i => fur[i]);
        }

        // Poids des os de la patte (hanche, genou, cheville) d'après la place du sommet le long de hanche → genou → cheville,
        // mélangés autour des articulations comme pour la patte générée. Bindposes : os sans rotation.
        // Haut de la cuisse (au-dessus de la hanche : fesse) sur l'os HipAnchor, qui ne tourne pas avec la cuisse :
        // il reste collé au corps quelle que soit la place de la hanche, le pli se fait autour d'elle.
        private static void SkinLeg(Mesh mesh, Vector3 knee, Vector3 ankle)
        {
            Vector3[] v = mesh.vertices;
            float kneeAt = knee.magnitude;
            float shin = (ankle - knee).magnitude;
            float ankleAt = kneeAt + shin;
            float blend = CatModelBuilder.JointBlend * ankleAt * 1.3f;   // ≈ même largeur que la patte générée (pied compris)
            float hipBlend = kneeAt * HipBlend;
            var weights = new BoneWeight[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float along = AlongLeg(v[i], knee, ankle, kneeAt, shin);
                float toThigh = CatModelBuilder.Smooth01(along / hipBlend + 0.5f);   // 0 : fesse, 1 : cuisse
                float toKnee = CatModelBuilder.Smooth01((along - kneeAt) / blend + 0.5f);
                float toAnkle = CatModelBuilder.Smooth01((along - ankleAt) / blend + 0.5f);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = 0, weight0 = toThigh * (1f - toKnee),
                    boneIndex1 = 1, weight1 = toKnee * (1f - toAnkle),
                    boneIndex2 = 2, weight2 = toKnee * toAnkle,
                    boneIndex3 = CatModelBuilder.HipAnchorBone, weight3 = (1f - toThigh) * (1f - toKnee),
                };
            }
            mesh.boneWeights = weights;
            mesh.bindposes = new[]
            {
                Matrix4x4.identity, Matrix4x4.Translate(-knee), Matrix4x4.Translate(-ankle), Matrix4x4.identity,
            };
        }

        // Largeur du pli de la hanche (part de la longueur de la cuisse), centrée sur la hanche
        private const float HipBlend = 0.5f;

        // Distance le long de la patte (mètres) : sur la cuisse (hanche → genou) ou le jarret (genou → cheville, prolongé : le pied)
        private static float AlongLeg(Vector3 p, Vector3 knee, Vector3 ankle, float kneeAt, float shin)
        {
            float tThigh = kneeAt > 0f ? Vector3.Dot(p, knee) / (kneeAt * kneeAt) : 0f;
            Vector3 onThigh = knee * Mathf.Clamp01(tThigh);
            Vector3 shinDir = shin > 0f ? (ankle - knee) / shin : Vector3.down;
            float tShin = Vector3.Dot(p - knee, shinDir);
            Vector3 onShin = knee + shinDir * Mathf.Clamp(tShin, 0f, shin);
            bool thigh = (p - onThigh).sqrMagnitude <= (p - onShin).sqrMagnitude;
            // Au-dessus de la hanche (fesse dans le torse) : valeur négative, reste à la hanche
            return thigh ? tThigh * kneeAt : kneeAt + Mathf.Max(0f, tShin);
        }

        // Articulation de l'épaule (repère du bras, mètres) : centre du haut du bras, soit les sommets du premier
        // ShoulderPart de la longueur comprise entre le haut du bras et le coude (le long de l'axe épaule → coude)
        private static Vector3 ShoulderJoint(Mesh mesh, Vector3 elbow)
        {
            Vector3[] v = mesh.vertices;
            if (v.Length == 0 || elbow.sqrMagnitude < 1e-8f) return Vector3.zero;
            Vector3 dir = elbow.normalized;
            float top = float.MaxValue;
            foreach (Vector3 p in v) top = Mathf.Min(top, Vector3.Dot(p, dir));
            float limit = top + (Vector3.Dot(elbow, dir) - top) * ShoulderPart;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 p in v)
            {
                if (Vector3.Dot(p, dir) > limit) continue;
                sum += p;
                count++;
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        private const float ShoulderPart = 0.4f;

        // Origine du maillage déplacée en « origin » (les sommets reculent d'autant)
        private static void MoveOrigin(Mesh mesh, Vector3 origin)
        {
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] -= origin;
            mesh.vertices = v;
            mesh.RecalculateBounds();
        }

        // Ajoute « extra » au sous-mesh 1 (yeux, truffe : matériau lumineux)
        private static void AppendSubmesh(Mesh main, Mesh extra)
        {
            var v = new List<Vector3>(main.vertices);
            var c = new List<Color>(main.colors);
            var fur = new List<Vector4>();
            var veins = new List<Vector4>();
            main.GetUVs(1, fur);
            main.GetUVs(2, veins);
            int offset = v.Count;
            v.AddRange(extra.vertices);
            c.AddRange(extra.colors);
            for (int i = 0; i < extra.vertexCount; i++)
            {
                fur.Add(Vector4.zero);
                veins.Add(Vector4.zero);
            }
            int[] mainTris = main.GetTriangles(0);
            // Sous-mesh 1 déjà là (2e appel) : on y ajoute
            var extraTris = new List<int>(main.subMeshCount > 1 ? main.GetTriangles(1) : new int[0]);
            foreach (int t in extra.triangles) extraTris.Add(t + offset);

            if (v.Count > 65000) main.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            main.SetVertices(v);
            main.SetColors(c);
            main.SetUVs(1, fur);
            main.SetUVs(2, veins);
            main.subMeshCount = 2;
            main.SetTriangles(mainTris, 0);
            main.SetTriangles(extraTris, 1);
            main.RecalculateNormals();
            main.RecalculateTangents();
            main.RecalculateBounds();
        }

        private static void EnsureSubmeshes(Mesh mesh, int count)
        {
            if (mesh.subMeshCount >= count) return;
            int[][] tris = new int[mesh.subMeshCount][];
            for (int i = 0; i < tris.Length; i++) tris[i] = mesh.GetTriangles(i);
            mesh.subMeshCount = count;
            for (int i = 0; i < count; i++) mesh.SetTriangles(i < tris.Length ? tris[i] : new int[0], i);
        }
    }
}
