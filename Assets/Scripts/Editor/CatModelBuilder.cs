using System.Collections.Generic;
using ParryEverything.Core;
using UnityEditor;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Construit les modèles 3D du corps du chat et les enregistre comme assets :
    //  - FleshBall : la chair du Gardien refermée sur l'âme (boule bosselée, morceaux dépareillés, coutures) ;
    //  - CatLeg   : grande patte de chat digitigrade (cuisse, jarret, patte à 3 doigts), articulée au genou.
    //    Debout, le chat est la boule de chair portée par 2 de ces pattes.
    // Menu : Parry Everything > Reconstruire les modèles du chat. Relancer après avoir changé une forme ici.
    public static class CatModelBuilder
    {
        private const string MeshPath = "Assets/Meshes/Player/CatModels.asset";
        private const string MaterialFolder = "Assets/Materials/Player";
        private const string PrefabFolder = "Assets/Prefabs/Player";

        // Taille des pattes : longueur et épaisseur (1 = première version, jugée trop petite)
        private const float LegLength = 2f;
        private const float LegThickness = 1.7f;

        // Repères de la patte (le genou est le pivot de la partie basse)
        public static readonly Vector3 KneeOffset = new Vector3(0.07f, -0.18f, 0f) * LegLength;
        // Longueur totale hanche → sol, pour placer la boule au-dessus
        public const float LegReach = 0.445f * LegLength;

        private static readonly Color[] FleshPalette =
        {
            new Color(0.36f, 0.08f, 0.09f),   // rouge sombre
            new Color(0.24f, 0.07f, 0.12f),   // lie-de-vin
            new Color(0.42f, 0.16f, 0.12f),   // brun rouge
            new Color(0.19f, 0.09f, 0.13f),   // violacé
            new Color(0.3f, 0.12f, 0.1f),     // brun
            new Color(0.5f, 0.2f, 0.17f),     // rose cuit
            new Color(0.52f, 0.4f, 0.32f),    // peau morte, pâle
            new Color(0.14f, 0.11f, 0.17f),   // contusion
            new Color(0.66f, 0.2f, 0.18f),    // à vif
        };
        private static readonly Color CrustColor = new Color(0.1f, 0.03f, 0.03f);
        private static readonly Color StitchColor = new Color(0.86f, 0.8f, 0.68f, 0f);   // alpha 0 : reste clair abîmé
        private static readonly Color LegColor = new Color(0.1f, 0.1f, 0.17f);

        [MenuItem("Parry Everything/Reconstruire les modèles du chat")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Meshes", "Player");
            EnsureFolder("Assets/Materials", "Player");
            EnsureFolder("Assets/Prefabs", "Player");

            Material flesh = SaveMaterial("Flesh", MaterialFactory.FromShader("Custom/FleshLit"), 0.45f);
            Material leg = SaveMaterial("CatLeg", MaterialFactory.FromShader("Custom/FleshLit"), 0.3f);

            // Les meshes enregistrés (mis à jour en place s'ils existaient déjà)
            Mesh[] saved = SaveMeshes(BuildBall(), BuildThigh(), BuildShin());
            Mesh ball = saved[0], thigh = saved[1], shin = saved[2];

            SavePrefab("FleshBall", go => AddRenderer(go, ball, flesh));
            SavePrefab("CatLeg", go =>
            {
                GameObject t = new GameObject("Thigh");
                t.transform.SetParent(go.transform, false);
                AddRenderer(t, thigh, leg);
                GameObject knee = new GameObject("Knee");
                knee.transform.SetParent(go.transform, false);
                knee.transform.localPosition = KneeOffset;
                GameObject s = new GameObject("Shin");
                s.transform.SetParent(knee.transform, false);
                AddRenderer(s, shin, leg);
            });

            AssetDatabase.SaveAssets();
            Debug.Log("Modèles du chat reconstruits : " + MeshPath + ", " + PrefabFolder);
        }

        // ---------- Formes ----------

        // La chair refermée sur l'âme : ovale bosselé, sans ouverture
        private static Mesh BuildBall()
        {
            var skin = new FleshSkin(3, 10, 13, 0.3f);
            var d = new MeshDraft();
            Vector3 radii = new Vector3(0.4f, 0.36f, 0.36f);   // les bosses gonflent la forme d'environ 30 %
            Vector3 center = new Vector3(0f, -0.03f, 0f);
            d.AddSphere(4, dir => center + Vector3.Scale(dir * skin.Displace(dir), radii), skin.Paint);
            return d.ToMesh("FleshBall");
        }

        // Cuisse : de la hanche (origine) au genou, musclée
        private static Mesh BuildThigh()
        {
            var d = new MeshDraft();
            Vector3[] p = { new Vector3(0f, 0.03f, 0f) * LegLength, new Vector3(0.025f, -0.05f, 0f) * LegLength, new Vector3(0.055f, -0.12f, 0f) * LegLength, KneeOffset };
            float[] r = Thick(0.07f, 0.082f, 0.066f, 0.048f);
            d.AddLoft(p, r, 10, LegPaint, 0);
            return d.ToMesh("CatThigh");
        }

        // Jarret et patte : du genou (origine) vers l'arrière puis vers l'avant, patte à 3 doigts
        private static Mesh BuildShin()
        {
            var d = new MeshDraft();
            Vector3[] p =
            {
                Vector3.zero, new Vector3(-0.03f, -0.07f, 0f) * LegLength, new Vector3(-0.06f, -0.14f, 0f) * LegLength,
                new Vector3(-0.035f, -0.2f, 0f) * LegLength, new Vector3(0f, -0.235f, 0f) * LegLength,
            };
            float[] r = Thick(0.047f, 0.04f, 0.033f, 0.03f, 0.03f);
            d.AddLoft(p, r, 9, (i, a) => LegPaint(i + 4, a), 0);
            Color paw = LegColor * 1.25f;
            paw.a = 1f;
            d.AddEllipsoid(new Vector3(0.04f, -0.245f, 0f) * LegLength, new Vector3(0.075f, 0.03f, 0.05f) * LegThickness, paw);
            foreach (float z in new[] { -0.03f, 0f, 0.03f })
            {
                d.AddEllipsoid(new Vector3(0.105f, -0.252f, z) * LegLength, new Vector3(0.025f, 0.02f, 0.02f) * LegThickness, paw);
            }
            return d.ToMesh("CatShin");
        }

        private static float[] Thick(params float[] radii)
        {
            for (int i = 0; i < radii.Length; i++) radii[i] *= LegThickness;
            return radii;
        }

        // Patte : bleu nuit, couture claire en haut de la cuisse
        private static Color LegPaint(int ring, float angle)
        {
            if (ring == 0) return StitchColor;
            if (ring == 1 && Mathf.Sin(angle * 5f) > 0.2f) return StitchColor;
            return Shade(LegColor, angle * 3f + ring);
        }

        private static Color Shade(Color c, float seed)
        {
            float k = 0.85f + 0.3f * Mathf.PerlinNoise(seed * 1.7f, 0.37f);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        // ---------- Peau de chair ----------

        // Morceaux de chair dépareillés (zones de couleur), bosses, sillons de couture avec points clairs
        private sealed class FleshSkin
        {
            private const float SeamWidth = 0.06f;
            private readonly Vector3[] patchDirs;
            private readonly Color[] patchColors;
            private readonly Vector3[] lumpDirs;
            private readonly float[] lumpSizes;
            private readonly float offset;
            private readonly float lumpiness;

            public FleshSkin(int seed, int patches, int lumps, float lumpiness)
            {
                var rng = new System.Random(seed);
                patchDirs = RandomDirs(rng, patches);
                patchColors = new Color[patches];
                for (int i = 0; i < patches; i++) patchColors[i] = FleshPalette[rng.Next(FleshPalette.Length)];
                lumpDirs = RandomDirs(rng, lumps);
                lumpSizes = new float[lumps];
                for (int i = 0; i < lumps; i++) lumpSizes[i] = lumpiness * (0.5f + (float)rng.NextDouble());
                offset = seed * 13.1f;
                this.lumpiness = lumpiness;
            }

            // Rayon relatif dans une direction : bosses, grain, sillons des coutures
            public float Displace(Vector3 dir)
            {
                if (dir == Vector3.zero) return 0f;
                float r = 1f;
                for (int i = 0; i < lumpDirs.Length; i++)
                {
                    float k = Mathf.Max(0f, Vector3.Dot(dir, lumpDirs[i]));
                    r += lumpSizes[i] * Mathf.Pow(k, 4f);
                }
                // Grain irrégulier sur deux échelles : la chair n'est jamais lisse
                r += (Noise(dir * 2.2f, offset) - 0.5f) * lumpiness * 0.9f;
                r += (Noise(dir * 7f, offset + 3f) - 0.5f) * lumpiness * 0.25f;
                float seam = SeamDistance(dir);
                if (seam < SeamWidth) r -= 0.055f * (1f - seam / SeamWidth);
                return r;
            }

            public Color Paint(Vector3 dir)
            {
                if (dir == Vector3.zero) return Color.black;
                float seam = SeamDistance(dir);
                if (seam < SeamWidth * 0.6f)
                {
                    // Points de couture grossiers, espacés, sur une croûte sombre
                    float dash = Mathf.Sin(dir.x * 31f + dir.y * 27f + dir.z * 35f);
                    return dash > 0.55f ? StitchColor : CrustColor;
                }
                if (seam < SeamWidth) return Color.Lerp(CrustColor, patchColors[Nearest(dir)], (seam / SeamWidth - 0.6f) / 0.4f);
                Color c = patchColors[Nearest(dir)];
                float grain = 0.7f + 0.6f * Noise(dir * 6f, offset + 5f);
                // Veines plus vives par endroits
                float vein = Mathf.Clamp01((Noise(dir * 11f, offset + 9f) - 0.68f) * 4f);
                c = Color.Lerp(c * grain, new Color(0.62f, 0.12f, 0.12f), vein * 0.6f);
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

            private static float Noise(Vector3 p, float o)
            {
                return (Mathf.PerlinNoise(p.x + o, p.y + 3.1f) + Mathf.PerlinNoise(p.y + o * 0.7f, p.z + 7.7f)
                        + Mathf.PerlinNoise(p.z + o * 1.3f, p.x + 1.9f)) / 3f;
            }
        }

        // ---------- Assets ----------

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        // Réutilise le matériau s'il existe (réglages faits à la main conservés), sinon le crée
        private static Material SaveMaterial(string name, Material fresh, float gloss)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Object.DestroyImmediate(fresh);
                return existing;
            }
            if (gloss >= 0f) fresh.SetFloat("_Glossiness", gloss);
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        private static Mesh[] SaveMeshes(params Mesh[] meshes)
        {
            // L'objet principal du fichier prend le nom du fichier : c'est un mesh vide qui sert de conteneur,
            // les vrais meshes sont des sous-objets (retrouvés par leur nom à chaque reconstruction)
            Mesh main = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (main == null)
            {
                AssetDatabase.CreateAsset(new Mesh { name = "CatModels" }, MeshPath);
                foreach (Mesh m in meshes) AssetDatabase.AddObjectToAsset(m, MeshPath);
                return meshes;
            }
            if (main.vertexCount > 0) main.Clear();
            // Mise à jour en place : les prefabs gardent leurs références
            var existing = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
            {
                if (o is Mesh m && m != main) existing[m.name] = m;
            }
            for (int i = 0; i < meshes.Length; i++)
            {
                if (existing.TryGetValue(meshes[i].name, out Mesh target))
                {
                    EditorUtility.CopySerialized(meshes[i], target);
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

        private static void AddRenderer(GameObject go, Mesh mesh, params Material[] materials)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private static void SavePrefab(string name, System.Action<GameObject> build)
        {
            GameObject root = new GameObject(name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }
    }
}
