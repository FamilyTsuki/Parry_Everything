using System.Collections.Generic;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEditor;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Construit les modèles 3D du corps du chat et les enregistre comme assets :
    //  - FleshBall : la chair du Gardien refermée sur l'âme (œuf / capsule, morceaux dépareillés, coutures) ;
    //  - CatLeg    : grande patte de chat digitigrade (cuisse, jarret, patte à 3 doigts), articulée au genou.
    //    Debout, le chat est ce corps porté par 2 de ces pattes (assemblage et animation : CatBody).
    //
    // Tous les réglages de forme sont dans CatModelSettings (menu Parry Everything > Modèle du chat : réglages).
    // Changer une valeur reconstruit les modèles. Les assets existants sont mis à jour en place :
    // les prefabs, les matériaux (et leurs réglages faits à la main) et les références de la scène sont conservés.
    public static class CatModelBuilder
    {
        // ---------- Fichiers produits ----------
        private const string SettingsPath = "Assets/Meshes/Player/CatModelSettings.asset";
        private const string MeshPath = "Assets/Meshes/Player/CatModels.asset";     // les 3 meshes, en sous-assets
        private const string MaterialFolder = "Assets/Materials/Player";
        private const string PrefabFolder = "Assets/Prefabs/Player";

        // ---------- Patte : gabarit à la longueur 1 ----------
        // Repère : hanche à l'origine, patte de profil (X = avant, Y = haut). Tout est multiplié par legLength.
        private static readonly Vector3 KneeAt1 = new Vector3(0.07f, -0.18f, 0f);    // hanche → genou
        private static readonly Vector3 AnkleAt1 = new Vector3(0f, -0.235f, 0f);     // genou → cheville

        private static bool rebuildQueued;

        // =====================================================================
        //  Menus
        // =====================================================================

        [MenuItem("Parry Everything/Modèle du chat : réglages")]
        public static void SelectSettings()
        {
            CatModelSettings settings = LoadOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Parry Everything/Reconstruire les modèles du chat")]
        public static void BuildAll()
        {
            Build(LoadOrCreateSettings());
        }

        // Reconstruction différée (une seule par image même si plusieurs valeurs changent)
        public static void RequestRebuild()
        {
            if (rebuildQueued) return;
            rebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                rebuildQueued = false;
                BuildAll();
            };
        }

        // =====================================================================
        //  Construction
        // =====================================================================

        private static void Build(CatModelSettings s)
        {
            EnsureFolder("Assets/Meshes", "Player");
            EnsureFolder("Assets/Materials", "Player");
            EnsureFolder("Assets/Prefabs", "Player");

            Material flesh = SaveMaterial("Flesh", 0.45f);
            Material leg = SaveMaterial("CatLeg", 0.3f);

            Mesh[] saved = SaveMeshes(BuildBody(s), BuildThigh(s), BuildShin(s));
            Mesh body = saved[0], thigh = saved[1], shin = saved[2];

            Vector3 knee = KneeAt1 * s.legLength;
            SavePrefab("FleshBall", go => AddRenderer(go, body, flesh));
            SavePrefab("CatLeg", go =>
            {
                // CatLeg (hanche) → Thigh (cuisse)
                //                 → Knee (pivot du genou) → Shin (jarret + patte)
                go.AddComponent<CatLegRig>().SetGeometry(knee, AnkleAt1 * s.legLength);
                AddChild(go.transform, "Thigh", Vector3.zero, thigh, leg);
                Transform kneeT = AddChild(go.transform, "Knee", knee, null, null);
                AddChild(kneeT, "Shin", Vector3.zero, shin, leg);
            });

            AssetDatabase.SaveAssets();
        }

        // ---------- Corps ----------

        // La chair refermée sur l'âme : œuf déformé, sans ouverture.
        // Chaque sommet part d'une direction de la sphère unité et passe par : forme d'œuf → bosses → placement.
        private static Mesh BuildBody(CatModelSettings s)
        {
            var skin = new FleshSkin(s);
            Matrix4x4 placement = Matrix4x4.TRS(s.bodyOffset, Quaternion.Euler(s.bodyRotation), s.bodyScale);
            var d = new MeshDraft();
            d.AddSphere(s.bodyDetail, dir => placement.MultiplyPoint3x4(EggPoint(s, dir) * skin.Displace(dir)), skin.Paint);
            return d.ToMesh("FleshBall");
        }

        // Point de la surface de l'œuf (sans bosses) dans la direction 'dir'
        private static Vector3 EggPoint(CatModelSettings s, Vector3 dir)
        {
            // Capsule : les flancs gardent leur largeur plus longtemps vers les bouts.
            // side = distance à l'axe vertical sur la sphère ; side^flank l'élargit (flank < 1)
            float side = Mathf.Sqrt(Mathf.Max(0f, 1f - dir.y * dir.y));
            float widen = side > 0.0001f ? Mathf.Pow(side, s.bodyFlank) / side : 1f;
            Vector3 p = Vector3.Scale(new Vector3(dir.x * widen, dir.y, dir.z * widen), s.bodyRadii);

            // Œuf : plus étroit en haut (dir.y = 1) qu'en bas (dir.y = −1)
            float taper = 1f - s.bodyTaper * dir.y;
            p.x *= taper;
            p.z *= taper;

            // Courbure : les bouts (|dir.y| grand) reculent, le milieu avance un peu (moyenne nulle)
            p.x -= s.bodyBend * (dir.y * dir.y - 0.33f);
            return p;
        }

        // ---------- Patte ----------

        // Cuisse : de la hanche (origine) au genou, musclée
        private static Mesh BuildThigh(CatModelSettings s)
        {
            float L = s.legLength;
            Vector3[] spine =
            {
                new Vector3(0f, 0.03f, 0f) * L,       // dépasse un peu au-dessus de la hanche (rentre dans le corps)
                new Vector3(0.025f, -0.05f, 0f) * L,
                new Vector3(0.055f, -0.12f, 0f) * L,
                KneeAt1 * L,
            };
            float[] radii = Thick(s, 0.07f, 0.082f, 0.066f, 0.048f);   // muscle gonflé sous la hanche
            var d = new MeshDraft();
            d.AddLoft(spine, radii, 10, (ring, angle) => LegPaint(s, ring, angle), 0);
            return d.ToMesh("CatThigh");
        }

        // Jarret et patte : du genou (origine) vers l'arrière puis vers l'avant, patte à 3 doigts
        private static Mesh BuildShin(CatModelSettings s)
        {
            float L = s.legLength;
            Vector3[] spine =
            {
                Vector3.zero,
                new Vector3(-0.03f, -0.07f, 0f) * L,
                new Vector3(-0.06f, -0.14f, 0f) * L,   // talon, en arrière (digitigrade)
                new Vector3(-0.035f, -0.2f, 0f) * L,
                AnkleAt1 * L,                          // cheville
            };
            float[] radii = Thick(s, 0.047f, 0.04f, 0.033f, 0.03f, 0.03f);
            var d = new MeshDraft();
            d.AddLoft(spine, radii, 9, (ring, angle) => LegPaint(s, ring + 4, angle), 0);

            // Coussinet et 3 doigts, un peu plus clairs
            Color paw = s.legColor * 1.25f;
            paw.a = 1f;
            float T = s.legThickness;
            d.AddEllipsoid(new Vector3(0.04f, -0.245f, 0f) * L, new Vector3(0.075f, 0.03f, 0.05f) * T, paw);
            foreach (float z in new[] { -0.03f, 0f, 0.03f })
            {
                d.AddEllipsoid(new Vector3(0.105f, -0.252f, z) * L, new Vector3(0.025f, 0.02f, 0.02f) * T, paw);
            }
            return d.ToMesh("CatShin");
        }

        private static float[] Thick(CatModelSettings s, params float[] radii)
        {
            for (int i = 0; i < radii.Length; i++) radii[i] *= s.legThickness;
            return radii;
        }

        // Couleur de la patte : bleu nuit nuancé, couture claire en haut de la cuisse (anneaux 0 et 1)
        private static Color LegPaint(CatModelSettings s, int ring, float angle)
        {
            if (ring == 0) return s.stitchColor;
            if (ring == 1 && Mathf.Sin(angle * 5f) > 0.2f) return s.stitchColor;
            float k = 0.85f + 0.3f * Mathf.PerlinNoise((angle * 3f + ring) * 1.7f, 0.37f);
            return new Color(s.legColor.r * k, s.legColor.g * k, s.legColor.b * k, 1f);
        }

        // =====================================================================
        //  Peau de chair : morceaux dépareillés, bosses, coutures
        // =====================================================================

        // La surface est découpée en morceaux : chaque point appartient au morceau dont le centre (une direction)
        // est le plus proche. Là où deux morceaux se touchent : couture (sillon + points clairs).
        private sealed class FleshSkin
        {
            private readonly CatModelSettings s;
            private readonly Vector3[] patchDirs;     // centre de chaque morceau
            private readonly Color[] patchColors;
            private readonly Vector3[] lumpDirs;      // sommet de chaque bosse
            private readonly float[] lumpSizes;
            private readonly float noiseOffset;

            public FleshSkin(CatModelSettings settings)
            {
                s = settings;
                var rng = new System.Random(s.seed);
                patchDirs = RandomDirs(rng, s.patchCount);
                patchColors = new Color[s.patchCount];
                Color[] palette = s.fleshPalette != null && s.fleshPalette.Length > 0 ? s.fleshPalette : new[] { Color.red };
                for (int i = 0; i < s.patchCount; i++) patchColors[i] = palette[rng.Next(palette.Length)];
                lumpDirs = RandomDirs(rng, s.lumpCount);
                lumpSizes = new float[s.lumpCount];
                for (int i = 0; i < s.lumpCount; i++) lumpSizes[i] = s.lumpiness * (0.5f + (float)rng.NextDouble());
                noiseOffset = s.seed * 13.1f;
            }

            // Multiplicateur du rayon dans une direction : 1 + bosses + grain − sillon des coutures
            public float Displace(Vector3 dir)
            {
                if (dir == Vector3.zero) return 0f;
                float r = 1f;
                for (int i = 0; i < lumpDirs.Length; i++)
                {
                    float k = Mathf.Max(0f, Vector3.Dot(dir, lumpDirs[i]));
                    r += lumpSizes[i] * Mathf.Pow(k, 4f);   // bosse arrondie autour de sa direction
                }
                // Grain irrégulier sur deux échelles : la chair n'est jamais lisse
                r += (Noise(dir * 2.2f, noiseOffset) - 0.5f) * s.lumpiness * 0.9f;
                r += (Noise(dir * 7f, noiseOffset + 3f) - 0.5f) * s.lumpiness * 0.25f;
                float seam = SeamDistance(dir);
                if (seam < s.seamWidth) r -= s.seamDepth * (1f - seam / s.seamWidth);
                return r;
            }

            // Couleur par sommet (lue par le shader FleshLit)
            public Color Paint(Vector3 dir)
            {
                if (dir == Vector3.zero) return Color.black;
                float seam = SeamDistance(dir);
                Color patch = patchColors[Nearest(dir)];
                if (seam < s.seamWidth * 0.6f)
                {
                    // Au fond de la couture : points grossiers, espacés, sur une croûte sombre
                    float dash = Mathf.Sin(dir.x * 31f + dir.y * 27f + dir.z * 35f);
                    return dash > 0.55f ? s.stitchColor : s.crustColor;
                }
                if (seam < s.seamWidth) return Color.Lerp(s.crustColor, patch, (seam / s.seamWidth - 0.6f) / 0.4f);

                float grain = 0.7f + 0.6f * Noise(dir * 6f, noiseOffset + 5f);
                float vein = Mathf.Clamp01((Noise(dir * 11f, noiseOffset + 9f) - 0.68f) * 4f);
                Color c = Color.Lerp(patch * grain, s.veinColor, vein * 0.6f);
                c.a = 1f;
                return c;
            }

            // Écart entre le morceau le plus proche et le suivant : petit = on est sur une couture
            private float SeamDistance(Vector3 dir)
            {
                float best = -2f, second = -2f;
                foreach (Vector3 p in patchDirs)
                {
                    float d = Vector3.Dot(dir, p);
                    if (d > best) { second = best; best = d; }
                    else if (d > second) second = d;
                }
                return best - second;
            }

            private int Nearest(Vector3 dir)
            {
                int best = 0;
                float bestDot = -2f;
                for (int i = 0; i < patchDirs.Length; i++)
                {
                    float d = Vector3.Dot(dir, patchDirs[i]);
                    if (d > bestDot) { bestDot = d; best = i; }
                }
                return best;
            }

            // Directions uniformes sur la sphère (tirage dans la boule unité, puis normalisé)
            private static Vector3[] RandomDirs(System.Random rng, int count)
            {
                var dirs = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3 v;
                    do
                    {
                        v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                    } while (v.sqrMagnitude < 0.05f || v.sqrMagnitude > 1f);
                    dirs[i] = v.normalized;
                }
                return dirs;
            }

            // Bruit 3D approché par 3 bruits 2D (valeur entre 0 et 1)
            private static float Noise(Vector3 p, float o)
            {
                return (Mathf.PerlinNoise(p.x + o, p.y + 3.1f) + Mathf.PerlinNoise(p.y + o * 0.7f, p.z + 7.7f)
                        + Mathf.PerlinNoise(p.z + o * 1.3f, p.x + 1.9f)) / 3f;
            }
        }

        // =====================================================================
        //  Assets
        // =====================================================================

        private static CatModelSettings LoadOrCreateSettings()
        {
            CatModelSettings settings = AssetDatabase.LoadAssetAtPath<CatModelSettings>(SettingsPath);
            if (settings != null) return settings;
            EnsureFolder("Assets/Meshes", "Player");
            settings = ScriptableObject.CreateInstance<CatModelSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        // Réutilise le matériau s'il existe (réglages faits à la main conservés), sinon le crée
        private static Material SaveMaterial(string name, float gloss)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Material fresh = MaterialFactory.FromShader("Custom/FleshLit");
            fresh.SetFloat("_Glossiness", gloss);
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        // Le fichier contient un mesh principal vide (conteneur, porte le nom du fichier) et les vrais meshes
        // en sous-objets, retrouvés par leur nom à chaque reconstruction et mis à jour en place
        private static Mesh[] SaveMeshes(params Mesh[] meshes)
        {
            Mesh main = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (main == null)
            {
                AssetDatabase.CreateAsset(new Mesh { name = "CatModels" }, MeshPath);
                foreach (Mesh m in meshes) AssetDatabase.AddObjectToAsset(m, MeshPath);
                return meshes;
            }
            if (main.vertexCount > 0) main.Clear();

            var existing = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
            {
                if (o is Mesh m && m != main) existing[m.name] = m;
            }
            for (int i = 0; i < meshes.Length; i++)
            {
                if (existing.TryGetValue(meshes[i].name, out Mesh target))
                {
                    CopyMesh(meshes[i], target);
                    Object.DestroyImmediate(meshes[i]);
                    meshes[i] = target;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(meshes[i], MeshPath);
                }
            }
            return meshes;
        }

        // Copie par l'API Mesh (et non CopySerialized) : la copie sérialisée ne renvoie pas les données
        // à la carte graphique, le jeu continuait d'afficher l'ancienne forme jusqu'au redémarrage d'Unity
        private static void CopyMesh(Mesh source, Mesh target)
        {
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.SetVertices(source.vertices);
            target.SetNormals(source.normals);
            if (source.tangents.Length > 0) target.SetTangents(source.tangents);
            if (source.colors.Length > 0) target.SetColors(source.colors);
            var uv = new List<Vector2>();
            source.GetUVs(0, uv);
            if (uv.Count > 0) target.SetUVs(0, uv);
            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
            target.RecalculateBounds();
            EditorUtility.SetDirty(target);
        }

        // Enfant vide (mesh null) ou avec un rendu
        private static Transform AddChild(Transform parent, string name, Vector3 localPosition, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (mesh != null) AddRenderer(go, mesh, material);
            return go.transform;
        }

        private static void AddRenderer(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // Remplace le prefab en gardant son GUID (les références de la scène restent valides)
        private static void SavePrefab(string name, System.Action<GameObject> build)
        {
            var root = new GameObject(name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }
    }
}
