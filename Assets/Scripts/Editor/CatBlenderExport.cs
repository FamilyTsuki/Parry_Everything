using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Exporte le chat vers Blender (menu Parry Everything > Exporter le chat vers Blender) :
    //  - Assets/Models/Cat/Cat.fbx : le chat assemblé ;
    //  - puis Blender (en arrière-plan, Tools/Blender/cat_blend_setup.py) en fait Assets/Models/Cat/cat.blend,
    //    prêt à modifier. L'ancien cat.blend est d'abord copié dans References/Backups.
    // Part du modèle actuel : le fichier Blender s'il est utilisé (CatModelSettings.source = Blender), sinon le modèle généré.
    //
    // Le chat est dans sa POSE DE JEU, debout immobile, regarde vers +X ; ce qu'on voit dans Blender = le jeu :
    //   Cat                             origine = centre du joueur
    //   ├ Ground                        sol (repère, pas modifiable : pieds du joueur)
    //   └ Torso                         Chest, Belly, Pelvis ; place et inclinaison = pose du torse
    //     ├ Head                        Skull, Ear_R (côté caméra), Ear_L (+ repères Eye, Nose : Unity y met yeux et truffe)
    //     ├ UpperArm → Forearm → Hand   bras côté caméra ; origine = épaule, coude
    //     ├ Leg → Knee → Ankle          Thigh, Shin, Foot ; patte côté caméra ; origine = hanche
    //     ├ TailSeg0 → TailSeg1 → ...
    //     └ Far_…                       bras et patte du côté loin : copies en miroir qui suivent (ne pas les modifier)
    // Chaque pièce a son origine sur son articulation ; place et rotation des pièces = pose de repos en jeu
    // (la marche, la course, etc. s'y ajoutent). La patte : sa cheville au repos = celle du fichier.
    // Ajouter un morceau : le ranger sous la pièce (il sera recollé avec elle au retour).
    //
    // Couleur des sommets = couleur de la FOURRURE (à repeindre dans Blender si besoin).
    // La chair cousue dessous, les plaques de fourrure qui tombent et les veines ne passent pas par le FBX :
    // Unity les recalcule au retour.
    public static class CatBlenderExport
    {
        public const string Folder = "Assets/Models/Cat";
        public const string FbxPath = Folder + "/Cat.fbx";
        public const string BlendPath = Folder + "/cat.blend";
        private const string SetupScript = "Tools/Blender/cat_blend_setup.py";
        private const string BackupFolder = "References/Backups";

        private static Material exportMaterial;   // matériau neutre le temps de l'export (Blender lit la couleur des sommets)

        [MenuItem("Parry Everything/Exporter le chat vers Blender")]
        public static void Export()
        {
            if (!ExportFbx()) return;
            MakeBlend();
        }

        internal static bool ExportFbx()
        {
            CatModelSettings s = CatModelBuilder.Settings;
            if (!CatBlenderImport.TryBuild(s, out List<Mesh> list, out CatRig rig))
            {
                list = new List<Mesh>(CatModelBuilder.BuildEditableMeshes(s).Values);
                rig = CatModelBuilder.GeneratedRig(s);
            }
            CatModelBuilder.SplitTorso(list, s);
            CatModelBuilder.SplitLeg(list);
            var m = new Dictionary<string, Mesh>();
            foreach (Mesh mesh in list)
            {
                FurToVertexColor(mesh);
                // Tête : les yeux et la truffe (sous-mesh 1) sont remis par Unity au retour ; ailleurs tout est gardé
                MergeSubmeshes(mesh, mesh.name == "CatHead" ? 1 : mesh.subMeshCount);
                m[mesh.name] = mesh;
            }
            var temp = new List<Mesh>();   // morceaux créés ici, détruits à la fin

            var root = new GameObject("Cat");
            exportMaterial = new Material(Shader.Find("Standard")) { name = "Cat" };
            try
            {
                Mesh ground = GroundMesh();
                temp.Add(ground);
                Part(root.transform, "Ground", new Vector3(0f, -ParryEverything.Player.CatBody.FeetBelowCenter, 0f), ground);

                Transform torso = Empty(root.transform, "Torso", new Vector3(rig.StandingPosition.x, rig.StandingPosition.y, 0f));
                torso.localRotation = Quaternion.Euler(0f, 0f, -rig.StandingTilt);
                foreach (string part in new[] { "Chest", "Belly", "Pelvis" }) Piece(torso, part, m, "Flesh" + part);

                // Tête : crâne et oreilles séparés
                Transform head = Empty(torso, "Head", rig.Neck);
                head.localRotation = rig.HeadRest;
                Dictionary<string, Mesh> headPieces = SplitHead(m["CatHead"]);
                Vector3 skullCenter = headPieces.TryGetValue("Skull", out Mesh skull) ? skull.bounds.center : Vector3.zero;
                foreach (KeyValuePair<string, Mesh> piece in headPieces)
                {
                    // Oreilles : origine à leur base (elles tournent autour de leur base dans Blender)
                    Vector3 pivot = piece.Key.StartsWith("Ear") ? MoveOriginToBase(piece.Value, skullCenter) : Vector3.zero;
                    Part(head, piece.Key, pivot, piece.Value);
                    temp.Add(piece.Value);
                }
                Empty(head, "Eye", rig.Eye);
                Empty(head, "Nose", rig.Nose);

                // Bras : avant-bras et main séparés (modèle généré : main dans l'avant-bras, coupée au plus gros morceau)
                Mesh forearm = m["CatForearm"], hand = m.TryGetValue("CatHand", out Mesh h) && h.vertexCount > 0 ? h : null;
                if (hand == null)
                {
                    SplitLargest(m["CatForearm"], out forearm, out hand);
                    temp.Add(forearm);
                    if (hand != null) temp.Add(hand);
                }
                Transform upper = Part(torso, "UpperArm", rig.Shoulder, m["CatUpperArm"]);
                upper.localRotation = rig.ArmRest;
                Transform fore = Part(upper, "Forearm", rig.Elbow, forearm);
                fore.localRotation = rig.ElbowRest;
                if (hand != null) Part(fore, "Hand", Vector3.zero, hand);

                // Patte : cuisse, jarret, pied, pliés comme en jeu au repos (cheville posée au sol)
                Transform leg = Empty(torso, "Leg", rig.Hip);
                leg.rotation = root.transform.rotation * Quaternion.Euler(rig.LegSplay, 0f, 0f);
                PoseLeg(rig, torso.TransformPoint(rig.Hip), out Matrix4x4[] bones);
                foreach (string part in new[] { "Thigh", "Shin", "Foot" })
                {
                    if (!m.TryGetValue("CatLeg" + part, out Mesh mesh) || mesh.vertexCount == 0) continue;
                    Mesh posed = Deform(mesh, bones);
                    temp.Add(posed);
                    Part(leg, part, Vector3.zero, posed);
                }
                Vector3 kneeAt = bones[1].MultiplyPoint3x4(Vector3.zero);
                Transform kneeT = Empty(leg, "Knee", kneeAt);
                Empty(kneeT, "Ankle", bones[2].MultiplyPoint3x4(Vector3.zero) - kneeAt);

                Transform parent = torso;
                for (int i = 0; i < rig.TailSegments; i++)
                {
                    parent = Part(parent, "TailSeg" + i, i == 0 ? rig.TailBase : rig.TailPositions[i], m["CatTailSeg" + i]);
                    parent.localRotation = rig.TailRotations[i];
                }

                // Côté loin : copies en miroir (profondeur inversée), comme le jeu les place
                MirrorCopy(upper, torso);
                MirrorCopy(leg, torso);

                Directory.CreateDirectory(Folder);
                // Binaire : Blender ne lit pas le FBX texte (ASCII), format par défaut de l'exporteur
                ModelExporter.ExportObject(FbxPath, root, new ExportModelOptions { ExportFormat = ExportFormat.Binary });
                AssetDatabase.Refresh();
                Debug.Log("Chat exporté : " + Path.GetFullPath(FbxPath));
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(exportMaterial);
                foreach (Mesh mesh in m.Values) Object.DestroyImmediate(mesh);
                foreach (Mesh mesh in temp) Object.DestroyImmediate(mesh);
            }
        }

        // Blender en arrière-plan : Cat.fbx → cat.blend (copies du côté loin reliées, sol, couleurs visibles)
        private static void MakeBlend()
        {
            string blender = File.Exists("/usr/bin/blender") ? "/usr/bin/blender" : "blender";
            if (File.Exists(BlendPath))
            {
                Directory.CreateDirectory(BackupFolder);
                string backup = BackupFolder + "/cat-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".blend";
                File.Copy(BlendPath, backup);
                Debug.Log("Ancien cat.blend copié dans " + Path.GetFullPath(backup));
            }
            var info = new System.Diagnostics.ProcessStartInfo(blender,
                "-b --factory-startup --python \"" + Path.GetFullPath(SetupScript) + "\" -- \""
                + Path.GetFullPath(FbxPath) + "\" \"" + Path.GetFullPath(BlendPath) + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0 || !output.Contains("CAT_BLEND_OK"))
                    {
                        Debug.LogError("Blender n'a pas pu créer cat.blend :\n" + output);
                        return;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Blender introuvable (" + blender + ") : " + e.Message
                               + "\nImporter Cat.fbx à la main dans Blender (File > Import > FBX).");
                return;
            }
            AssetDatabase.ImportAsset(BlendPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("cat.blend prêt : " + Path.GetFullPath(BlendPath));
        }

        // Copie en miroir d'une pièce et de tout ce qu'elle porte : noms préfixés Far_. Comme le membre du côté loin en jeu :
        // place et rotation en miroir (profondeur, Z du torse, inversée), échelle −1 en profondeur (forme retournée) ;
        // les morceaux dessous gardent les mêmes place et rotation que côté caméra.
        private static void MirrorCopy(Transform near, Transform parent)
        {
            Transform far = Object.Instantiate(near.gameObject, parent).transform;
            foreach (Transform t in far.GetComponentsInChildren<Transform>(true))
            {
                t.name = "Far_" + t.name.Replace("(Clone)", "");
            }
            Vector3 p = near.localPosition;
            far.localPosition = new Vector3(p.x, p.y, -p.z);
            Quaternion q = near.localRotation;
            far.localRotation = new Quaternion(-q.x, -q.y, q.z, q.w);
            far.localScale = new Vector3(1f, 1f, -1f);
        }

        // Sol : rectangle sous les pieds (gris)
        private static Mesh GroundMesh()
        {
            var mesh = new Mesh { name = "Ground" };
            mesh.vertices = new[]
            {
                new Vector3(-1.5f, 0f, -0.75f), new Vector3(1.5f, 0f, -0.75f),
                new Vector3(1.5f, 0f, 0.75f), new Vector3(-1.5f, 0f, 0.75f),
            };
            var grey = new Color(0.3f, 0.3f, 0.33f);
            mesh.colors = new[] { grey, grey, grey, grey };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        // Os de la patte au repos, comme CatBody (chat immobile qui regarde à droite) : cheville au sol sous la hanche,
        // genou plié vers l'avant. bones[i] : os i (hanche, genou, cheville) dans le repère de la patte écartée.
        private static void PoseLeg(CatRig rig, Vector3 hip, out Matrix4x4[] bones)
        {
            float splay = rig.LegSplay * Mathf.Deg2Rad;
            var target = new Vector2(rig.FootForward,
                (-ParryEverything.Player.CatBody.FeetBelowCenter + rig.AnkleHeight - hip.y) / Mathf.Max(0.2f, Mathf.Cos(splay)));
            Vector2 thigh = rig.Knee, shin = rig.AnkleFromKnee;
            float l1 = thigh.magnitude, l2 = shin.magnitude;
            float d = Mathf.Clamp(target.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            float a1 = Mathf.Atan2(target.y, target.x) + Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));
            Vector2 knee = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * l1;
            Vector2 dir = target.normalized * d - knee;
            float hipRot = a1 * Mathf.Rad2Deg - Mathf.Atan2(thigh.y, thigh.x) * Mathf.Rad2Deg;
            float kneeRot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - Mathf.Atan2(shin.y, shin.x) * Mathf.Rad2Deg - hipRot;
            float ankleRot = -hipRot - kneeRot;   // pied à plat

            Matrix4x4 b0 = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, hipRot));
            Matrix4x4 b1 = b0 * Matrix4x4.TRS(rig.Knee, Quaternion.Euler(0f, 0f, kneeRot), Vector3.one);
            Matrix4x4 b2 = b1 * Matrix4x4.TRS(rig.AnkleFromKnee, Quaternion.Euler(0f, 0f, ankleRot), Vector3.one);
            bones = new[] { b0, b1, b2 };
        }

        // Maillage plié par ses os (poids et bindposes de la patte)
        private static Mesh Deform(Mesh source, Matrix4x4[] bones)
        {
            Vector3[] v = source.vertices;
            BoneWeight[] w = source.boneWeights;
            Matrix4x4[] bind = source.bindposes;
            var result = Object.Instantiate(source);
            result.name = source.name;
            if (w.Length != v.Length || bind.Length < bones.Length) return result;
            var skin = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++) skin[b] = bones[b] * bind[b];
            var nv = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                BoneWeight bw = w[i];
                nv[i] = skin[bw.boneIndex0].MultiplyPoint3x4(v[i]) * bw.weight0
                        + skin[bw.boneIndex1].MultiplyPoint3x4(v[i]) * bw.weight1
                        + skin[bw.boneIndex2].MultiplyPoint3x4(v[i]) * bw.weight2
                        + skin[bw.boneIndex3].MultiplyPoint3x4(v[i]) * bw.weight3;
            }
            result.vertices = nv;
            result.boneWeights = new BoneWeight[0];
            result.bindposes = new Matrix4x4[0];
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        // Morceau déjà découpé (torse, patte), s'il existe
        private static void Piece(Transform parent, string name, Dictionary<string, Mesh> m, string meshName)
        {
            if (m.TryGetValue(meshName, out Mesh mesh) && mesh.vertexCount > 0) Part(parent, name, Vector3.zero, mesh);
        }

        // La couleur de la fourrure (UV1.rgb) devient la couleur des sommets : c'est elle qu'on voit et qu'on peint dans Blender
        private static void FurToVertexColor(Mesh mesh)
        {
            var fur = new List<Vector4>();
            mesh.GetUVs(1, fur);
            if (fur.Count != mesh.vertexCount) return;
            var colors = new Color[fur.Count];
            for (int i = 0; i < fur.Count; i++) colors[i] = new Color(fur[i].x, fur[i].y, fur[i].z, 1f);
            mesh.colors = colors;
        }

        // Garde les 'keep' premiers sous-meshes, réunis en un seul
        private static void MergeSubmeshes(Mesh mesh, int keep)
        {
            var tris = new List<int>();
            for (int sub = 0; sub < Mathf.Min(keep, mesh.subMeshCount); sub++) tris.AddRange(mesh.GetTriangles(sub));
            mesh.subMeshCount = 1;
            mesh.SetTriangles(tris, 0);
        }

        // Tête → crâne (le plus gros morceau, et ce qui n'est pas en haut) et oreilles (morceaux du haut, par côté)
        private static Dictionary<string, Mesh> SplitHead(Mesh head)
        {
            List<int[]> groups = Components(head, out int[] componentOf);
            int largest = Largest(groups);
            Bounds skull = BoundsOf(head, groups[largest]);
            float earLine = skull.center.y + skull.extents.y * 0.35f;
            var nameOf = new string[groups.Count];
            for (int g = 0; g < groups.Count; g++)
            {
                Vector3 c = BoundsOf(head, groups[g]).center;
                nameOf[g] = g == largest || c.y < earLine ? "Skull" : c.z < skull.center.z ? "Ear_R" : "Ear_L";
            }
            var result = new Dictionary<string, Mesh>();
            foreach (string name in new[] { "Skull", "Ear_R", "Ear_L" })
            {
                Mesh piece = Subset(head, name, tri => nameOf[componentOf[tri]] == name);
                if (piece.vertexCount > 0) result[name] = piece;
                else Object.DestroyImmediate(piece);
            }
            return result;
        }

        // Base d'une oreille : centre de ses sommets les plus proches du crâne (axe centre du crâne → centre de l'oreille).
        // Le maillage est décalé pour que cette base soit son origine ; renvoie la base (où placer l'objet).
        private static Vector3 MoveOriginToBase(Mesh mesh, Vector3 skullCenter)
        {
            Vector3[] v = mesh.vertices;
            if (v.Length == 0) return Vector3.zero;
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in v) center += p;
            center /= v.Length;
            Vector3 axis = (center - skullCenter).normalized;
            float low = float.MaxValue, high = float.MinValue;
            foreach (Vector3 p in v)
            {
                low = Mathf.Min(low, Vector3.Dot(p, axis));
                high = Mathf.Max(high, Vector3.Dot(p, axis));
            }
            Vector3 baseSum = Vector3.zero;
            int count = 0;
            foreach (Vector3 p in v)
            {
                if (Vector3.Dot(p, axis) > low + (high - low) * 0.15f) continue;
                baseSum += p;
                count++;
            }
            Vector3 pivot = baseSum / Mathf.Max(1, count);
            for (int i = 0; i < v.Length; i++) v[i] -= pivot;
            mesh.vertices = v;
            mesh.RecalculateBounds();
            return pivot;
        }

        // Avant-bras → le morceau le plus long (le bras) et tout le reste (main : paume, doigts, griffes)
        private static void SplitLargest(Mesh mesh, out Mesh main, out Mesh rest)
        {
            List<int[]> groups = Components(mesh, out int[] componentOf);
            int largest = 0;
            for (int g = 1; g < groups.Count; g++)
            {
                if (BoundsOf(mesh, groups[g]).size.magnitude > BoundsOf(mesh, groups[largest]).size.magnitude) largest = g;
            }
            main = Subset(mesh, "Forearm", tri => componentOf[tri] == largest);
            rest = Subset(mesh, "Hand", tri => componentOf[tri] != largest);
            if (rest.vertexCount == 0)
            {
                Object.DestroyImmediate(rest);
                rest = null;
            }
        }

        // Morceaux séparés du maillage (sommets reliés par des triangles ou à la même place).
        // componentOf : morceau de chaque premier sommet de triangle (index du triangle × 3 → morceau)
        private static List<int[]> Components(Mesh mesh, out int[] componentOfTriangle)
        {
            Vector3[] v = mesh.vertices;
            int[] tris = mesh.triangles;
            var parent = new int[v.Length];
            for (int i = 0; i < v.Length; i++) parent[i] = i;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Union(parent, tris[i], tris[i + 1]);
                Union(parent, tris[i], tris[i + 2]);
            }
            var byPosition = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 10000f);
                if (byPosition.TryGetValue(key, out int other)) Union(parent, i, other);
                else byPosition[key] = i;
            }

            var index = new Dictionary<int, int>();
            var members = new List<List<int>>();
            for (int i = 0; i < v.Length; i++)
            {
                int r = Find(parent, i);
                if (!index.TryGetValue(r, out int g))
                {
                    g = members.Count;
                    index[r] = g;
                    members.Add(new List<int>());
                }
                members[g].Add(i);
            }
            componentOfTriangle = new int[tris.Length / 3];
            for (int t = 0; t < componentOfTriangle.Length; t++) componentOfTriangle[t] = index[Find(parent, tris[t * 3])];
            var groups = new List<int[]>();
            foreach (List<int> g in members) groups.Add(g.ToArray());
            return groups;
        }

        private static int Find(int[] parent, int a)
        {
            while (parent[a] != a)
            {
                parent[a] = parent[parent[a]];
                a = parent[a];
            }
            return a;
        }

        private static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[b] = a;
        }

        private static int Largest(List<int[]> groups)
        {
            int best = 0;
            for (int g = 1; g < groups.Count; g++) if (groups[g].Length > groups[best].Length) best = g;
            return best;
        }

        private static Bounds BoundsOf(Mesh mesh, int[] vertices)
        {
            Vector3[] v = mesh.vertices;
            var b = new Bounds(v[vertices[0]], Vector3.zero);
            foreach (int i in vertices) b.Encapsulate(v[i]);
            return b;
        }

        // Nouveau maillage avec les triangles choisis (par numéro de triangle) ; garde positions et couleurs
        private static Mesh Subset(Mesh source, string name, System.Func<int, bool> keepTriangle)
        {
            Vector3[] v = source.vertices;
            Color[] colors = source.colors;
            int[] tris = source.triangles;
            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>();
            var nc = new List<Color>();
            var nt = new List<int>();
            for (int t = 0; t < tris.Length / 3; t++)
            {
                if (!keepTriangle(t)) continue;
                for (int k = 0; k < 3; k++)
                {
                    int old = tris[t * 3 + k];
                    if (!map.TryGetValue(old, out int idx))
                    {
                        idx = nv.Count;
                        map[old] = idx;
                        nv.Add(v[old]);
                        if (colors.Length == v.Length) nc.Add(colors[old]);
                    }
                    nt.Add(idx);
                }
            }
            var mesh = new Mesh { name = name };
            if (nv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(nv);
            if (nc.Count == nv.Count) mesh.SetColors(nc);
            mesh.SetTriangles(nt, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform Part(Transform parent, string name, Vector3 localPosition, Mesh mesh)
        {
            Transform t = Empty(parent, name, localPosition);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = exportMaterial;
            return t;
        }

        private static Transform Empty(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }
    }
}
