using System;
using System.Collections.Generic;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ParryEverything.Editor
{
    // Construit les modèles 3D du chat et les enregistre comme assets (Prefabs/Player) :
    //  - FleshBall : le torse (chair du Gardien refermée sur l'âme), en œuf / capsule ;
    //  - CatLeg    : grande patte arrière digitigrade (cuisse, jarret, grand pied à doigts), articulée au genou
    //                et à la cheville (le pied reste à plat) ;
    //  - CatArm    : bras (patte avant du chat redressé), articulé au coude, avec griffes ;
    //  - CatHead   : tête (crâne, museau, oreilles, joues, yeux lumineux) ;
    //  - CatTail   : queue en segments (pour onduler), flamme au bout.
    // CatBody assemble ces pièces selon les membres que le chat possède, et les anime.
    //
    // Chaque membre a deux couches, lues par le shader FleshLit :
    //  - la chair cousue (couleur des sommets) ;
    //  - la fourrure par-dessus (UV1 : couleur + seuil). Abîmé, le membre la perd par plaques.
    //
    // Tous les réglages sont dans CatModelSettings (menu Parry Everything > Modèle du chat : réglages).
    // Changer une valeur reconstruit les modèles. Les assets existants sont mis à jour en place :
    // prefabs, matériaux (et leurs réglages faits à la main) et références de la scène sont conservés.
    public static class CatModelBuilder
    {
        // ---------- Fichiers produits ----------
        private const string SettingsPath = "Assets/Meshes/Player/CatModelSettings.asset";
        private const string MeshPath = "Assets/Meshes/Player/CatModels.asset";     // tous les meshes, en sous-assets
        private const string MaterialFolder = "Assets/Materials/Player";
        private const string PrefabFolder = "Assets/Prefabs/Player";

        // ---------- Patte : gabarit à la longueur 1 ----------
        // Repère : hanche à l'origine, patte de profil (X = avant, Y = haut). Tout est multiplié par legLength.
        private static readonly Vector3 KneeAt1 = new Vector3(0.07f, -0.18f, 0f);    // hanche → genou
        private static readonly Vector3 AnkleAt1 = new Vector3(0f, -0.235f, 0f);     // genou → cheville

        private static readonly Color PupilColor = new Color(0.02f, 0.02f, 0.03f);

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

        // Reconstruction différée (une seule par image même si plusieurs valeurs changent).
        // Pas de drapeau « déjà demandé » : si un autre outil plante dans le même delayCall, Unity jette
        // la file d'attente et un drapeau resterait bloqué (plus aucune reconstruction). On retire puis on remet.
        public static void RequestRebuild()
        {
            EditorApplication.delayCall -= RebuildNow;
            EditorApplication.delayCall += RebuildNow;
        }

        private static void RebuildNow()
        {
            EditorApplication.delayCall -= RebuildNow;
            BuildAll();
        }

        // =====================================================================
        //  Construction
        // =====================================================================

        private static void Build(CatModelSettings s)
        {
            EnsureFolder("Assets/Meshes", "Player");
            EnsureFolder("Assets/Materials", "Player");
            EnsureFolder("Assets/Prefabs", "Player");

            // Matériaux : chair + fourrure (FleshLit), parties lumineuses (GlowUnlit), griffes
            Material flesh = SaveMaterial("Flesh", "Custom/FleshLit", m => m.SetFloat("_Glossiness", 0.45f));
            Material leg = SaveMaterial("CatLeg", "Custom/FleshLit", m => m.SetFloat("_Glossiness", 0.3f));
            Material glow = SaveMaterial("CatGlow", "Custom/GlowUnlit", null);
            Material claw = SaveMaterial("CatClaw", "Standard", m => m.SetFloat("_Glossiness", 0.6f));
            claw.color = s.clawColor;
            EditorUtility.SetDirty(claw);

            var built = new List<Mesh>
            {
                BuildTorso(s), BuildLegSkin(s), BuildHead(s), BuildUpperArm(s), BuildForearm(s), BuildFlame(s),
            };
            for (int i = 0; i < s.tailSegments; i++) built.Add(BuildTailSegment(s, i));
            Dictionary<string, Mesh> m = SaveMeshes(built);

            // Torse : points d'attache de la tête, des bras et de la queue
            SavePrefab("FleshBall", go =>
            {
                AddRenderer(go, m["FleshBall"], flesh);
                go.AddComponent<CatTorsoRig>().SetPoints(s.neckPoint, s.shoulderPoint, s.tailPoint, s.hipPoint);
            });

            // CatLeg (hanche, os 0) → Knee (os 1, pivot du genou) → Ankle (os 2, pivot de la cheville, pied à plat)
            //                      → Skin (la patte entière, un seul maillage déformé par les 3 os)
            SavePrefab("CatLeg", go =>
            {
                go.AddComponent<CatLegRig>().SetGeometry(KneeRest(s), AnkleAt1 * s.legLength);
                Transform kneeT = AddChild(go.transform, "Knee", KneeRest(s), null);
                Transform ankleT = AddChild(kneeT, "Ankle", AnkleAt1 * s.legLength, null);
                var skin = new GameObject("Skin").AddComponent<SkinnedMeshRenderer>();
                skin.transform.SetParent(go.transform, false);
                skin.sharedMesh = m["CatLegSkin"];
                skin.sharedMaterial = leg;
                skin.bones = new[] { go.transform, kneeT, ankleT };
                skin.rootBone = go.transform;
                skin.updateWhenOffscreen = true;   // bornes recalculées : la patte pliée n'est jamais coupée
            });

            // CatArm (épaule) → Upper (bras)
            //                 → Elbow (pivot du coude) → Fore (avant-bras, main, griffes)
            SavePrefab("CatArm", go =>
            {
                AddChild(go.transform, "Upper", Vector3.zero, m["CatUpperArm"], flesh);
                Transform elbow = AddChild(go.transform, "Elbow", ElbowPoint(s), null);
                AddChild(elbow, "Fore", Vector3.zero, m["CatForearm"], flesh, claw);
            });

            // CatHead (base du cou, pivot de la tête) : crâne + yeux
            SavePrefab("CatHead", go => AddRenderer(go, m["CatHead"], flesh, glow));

            // CatTail (naissance) → Seg0 → Seg1 → ... → SegN (chaque segment est un pivot) → Flame
            SavePrefab("CatTail", go =>
            {
                Transform parent = go.transform;
                float segLength = s.tailLength / s.tailSegments;
                for (int i = 0; i < s.tailSegments; i++)
                {
                    Vector3 at = i == 0 ? Vector3.zero : new Vector3(-segLength, 0f, 0f);
                    parent = AddChild(parent, "Seg" + i, at, m["CatTailSeg" + i], flesh);
                }
                AddChild(parent, "Flame", new Vector3(-segLength, 0f, 0f), m["CatTailFlame"], glow);
            });

            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        //  Torse
        // =====================================================================

        // La chair refermée sur l'âme : œuf déformé, sans ouverture.
        // Chaque sommet part d'une direction de la sphère unité et passe par : forme d'œuf → bosses → placement.
        private static Mesh BuildTorso(CatModelSettings s)
        {
            var skin = new FleshSkin(s, s.seed, s.patchCount, s.lumpCount, s.lumpiness);
            Matrix4x4 placement = Matrix4x4.TRS(s.bodyOffset, Quaternion.Euler(s.bodyRotation), s.bodyScale);
            var d = new MeshDraft();
            d.AddSphere(s.bodyDetail, dir => placement.MultiplyPoint3x4(EggPoint(s, dir) * skin.Displace(dir)), skin.Paint);
            Mesh mesh = d.ToMesh("FleshBall");

            // Fourrure : bleu nuit, poitrail clair sur le devant
            Matrix4x4 toEgg = placement.inverse;
            ApplyFur(s, mesh, 1f, p =>
            {
                Vector3 e = toEgg.MultiplyPoint3x4(p);
                Vector3 dir = new Vector3(e.x / s.bodyRadii.x, e.y / s.bodyRadii.y, e.z / s.bodyRadii.z);
                float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f - s.chestAmount, 1.05f - s.chestAmount * 0.6f, dir.x));
                float band = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, Mathf.Abs(dir.y - 0.05f)));
                return Color.Lerp(s.furDark, s.furLight, front * band * (s.chestAmount > 0f ? 1f : 0f));
            });
            return mesh;
        }

        // Point de la surface du torse (sans bosses) dans la direction 'dir'
        private static Vector3 EggPoint(CatModelSettings s, Vector3 dir)
        {
            // Capsule : les flancs gardent leur largeur plus longtemps vers les bouts.
            // side = distance à l'axe vertical sur la sphère ; side^flank l'élargit (flank < 1)
            float side = Mathf.Sqrt(Mathf.Max(0f, 1f - dir.y * dir.y));
            float widen = side > 0.0001f ? Mathf.Pow(side, s.bodyFlank) / side : 1f;
            // Silhouette : largeur et avancée selon la hauteur (courbes de CatModelSettings)
            widen *= Curve(s.torsoWidth, dir.y, 1f);
            Vector3 p = Vector3.Scale(new Vector3(dir.x * widen, dir.y, dir.z * widen), s.bodyRadii);
            p.x += Curve(s.torsoSpine, dir.y, 0f) * s.bodyRadii.x;

            // Œuf : plus étroit en haut (dir.y = 1) qu'en bas (dir.y = −1)
            float taper = 1f - s.bodyTaper * dir.y;
            p.x *= taper;
            p.z *= taper;

            // Courbure : les bouts (|dir.y| grand) reculent, le milieu avance un peu (moyenne nulle)
            p.x -= s.bodyBend * (dir.y * dir.y - 0.33f);
            return p;
        }

        // =====================================================================
        //  Patte : un seul maillage continu (hanche → genou → talon → cheville → pied → doigts)
        // =====================================================================
        //
        // Le maillage est un tube qui suit un « os » de la hanche au bout du pied, sans raccord.
        // Il est déformé par 3 os (SkinnedMeshRenderer) : la hanche (racine), le genou et la cheville.
        // Autour du genou et de la cheville, chaque sommet suit les deux os à la fois (poids mélangés) :
        // la patte se plie en douceur au lieu de montrer des morceaux.
        // Rotule et talon sont des reliefs du tube (pas des boules collées).
        // Repère : hanche à l'origine, patte de profil (X = avant, Y = haut). Pose de repos = pose du modèle.

        private const int LegRings = 56;          // anneaux le long de la patte (plus = plus lisse)
        private const int LegSegments = 14;       // sommets autour de chaque anneau
        private const float JointBlend = 0.045f;  // largeur du mélange de poids autour d'une articulation (part de la longueur)

        // Os de la patte au repos (repère de la hanche)
        private static Vector3 KneeRest(CatModelSettings s) => KneeAt1 * s.legLength;
        private static Vector3 AnkleRest(CatModelSettings s) => KneeRest(s) + AnkleAt1 * s.legLength;

        private static Mesh BuildLegSkin(CatModelSettings s)
        {
            float L = s.legLength;
            Vector3 knee = KneeRest(s), ankle = AnkleRest(s);
            // Talon : à heelHeight entre le genou et la base des doigts, reculé de heelBack
            Vector3 heel = Vector3.Lerp(knee, ankle, s.heelHeight) + new Vector3(-s.heelBack * L, 0f, 0f);
            Vector3 P = s.pawSize;
            float footY = -0.07f + P.y;   // centre du pied : la cheville est à 0.07 au-dessus du sol (CatBody.ankleHeight)

            // L'os : points de passage (lissés en courbe). Repère des marqueurs pour savoir où sont genou et cheville.
            var bone = new List<Vector3>
            {
                new Vector3(-0.03f, 0.07f, 0f) * L,             // remonte dans le torse : la cuisse y forme la fesse
                new Vector3(0f, 0f, 0f) * L,                     // hanche
                new Vector3(0.025f, -0.05f, 0f) * L,
                new Vector3(0.055f, -0.12f, 0f) * L,
                knee,
                Vector3.Lerp(knee, heel, 0.5f) + new Vector3(-0.012f, 0f, 0f) * L,   // tibia
                heel,                                                                 // talon (digitigrade)
                Vector3.Lerp(heel, ankle, 0.5f) + new Vector3(-0.012f, 0f, 0f) * L,  // métatarse, levé du sol
                ankle,                                                                // base des doigts
                ankle + new Vector3(P.x * 0.55f, footY, 0f),     // le pied part vers l'avant, posé au sol
                ankle + new Vector3(P.x * 1.5f, footY, 0f),
                ankle + new Vector3(P.x * 2f, footY * 0.9f, 0f), // bout des doigts
            };
            float kneeAt = ArcFraction(bone, 4), heelAt = ArcFraction(bone, 6), ankleAt = ArcFraction(bone, 8);

            float thighRadius = 0.072f * s.legThickness;
            float shinRadius = 0.047f * s.legThickness;
            var points = new Vector3[LegRings];
            var radii = new float[LegRings];
            var depth = new float[LegRings];
            for (int i = 0; i < LegRings; i++)
            {
                float t = i / (LegRings - 1f);
                Vector3 p = BonePoint(bone, t);
                Vector3 tangent = (BonePoint(bone, Mathf.Min(1f, t + 0.005f)) - BonePoint(bone, Mathf.Max(0f, t - 0.005f))).normalized;
                var front = new Vector3(-tangent.y, tangent.x, 0f);   // perpendiculaire « avant » dans le plan de profil

                // Épaisseur de chaque partie (courbes réglables), fondues l'une dans l'autre aux articulations
                float thigh = thighRadius * Curve(s.thighWidth, t / kneeAt, 1f);
                float shin = shinRadius * Curve(s.shinWidth, (t - kneeAt) / (ankleAt - kneeAt), 1f);
                float foot = FootRadius(s, (t - ankleAt) / (1f - ankleAt), shin);
                float r = Mathf.Lerp(thigh, shin, Smooth01((t - kneeAt) / JointBlend + 0.5f));
                r = Mathf.Lerp(r, foot, Smooth01((t - ankleAt) / JointBlend + 0.5f));

                // Muscles (courbes « bosse ») et reliefs : rotule devant le genou, talon derrière
                float bulge = t < kneeAt
                    ? Curve(s.thighBulge, t / kneeAt, 0f) * thighRadius
                    : t < ankleAt ? Curve(s.shinBulge, (t - kneeAt) / (ankleAt - kneeAt), 0f) * shinRadius : 0f;
                float kneeBump = Bump(t, kneeAt, 0.035f) * s.kneeBump * thighRadius * 0.35f;
                float heelBump = Bump(t, heelAt, 0.04f) * s.heelBump * shinRadius * 0.45f;
                // Haut de la cuisse en dôme (quart de cercle) : se referme en rond dans le torse, pas de bout plat
                float domeEnd = s.thighDome * kneeAt;
                if (domeEnd > 0f && t < domeEnd)
                {
                    float x = 1f - t / domeEnd;
                    r *= Mathf.Sqrt(Mathf.Max(0.02f, 1f - x * x));
                }

                points[i] = p + front * (bulge + kneeBump - heelBump);
                radii[i] = Mathf.Max(0.003f, r + kneeBump * 0.5f + heelBump * 0.5f);

                // Section ovale : cuisse aplatie (large de profil, fine de face), jarret rond, pied large et plat
                float lower = Smooth01((t - kneeAt) / (JointBlend * 2f) + 0.5f);
                float footness = Smooth01((t - ankleAt) / JointBlend + 0.5f);
                depth[i] = Mathf.Lerp(Mathf.Lerp(s.thighDepth, 1f, lower), P.z / Mathf.Max(0.001f, P.y), footness);
            }

            var d = new MeshDraft();
            d.AddLoft(points, radii, LegSegments, (ring, angle) => SewnPaint(s, ring, angle), 0, depth);

            // Doigts : petits coussins ronds au bout du pied, en éventail
            Color pawColor = Opaque(s.legColor * 1.25f);
            for (int i = 0; i < s.toeCount; i++)
            {
                float u = s.toeCount == 1 ? 0f : i / (s.toeCount - 1f) * 2f - 1f;   // −1 → 1 en travers du pied
                var toe = ankle + new Vector3(P.x * (1.75f - 0.15f * u * u), -0.07f + s.toeSize, u * P.z * 0.75f);
                d.AddEllipsoid(toe, new Vector3(s.toeSize * 1.2f, s.toeSize, s.toeSize), pawColor);
            }

            Mesh mesh = d.ToMesh("CatLegSkin");

            // Poids des os et couleur de la fourrure, d'après la place de chaque sommet le long de l'os
            var samples = new Vector3[200];
            for (int i = 0; i < samples.Length; i++) samples[i] = BonePoint(bone, i / (samples.Length - 1f));
            Vector3[] v = mesh.vertices;
            var weights = new BoneWeight[v.Length];
            var along = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float t = ClosestAlong(samples, v[i]);
                along[i] = t;
                float toKnee = Smooth01((t - kneeAt) / JointBlend + 0.5f);     // 0 : hanche, 1 : genou
                float toAnkle = Smooth01((t - ankleAt) / JointBlend + 0.5f);   // 0 : genou, 1 : cheville
                float wHip = 1f - toKnee, wKnee = toKnee * (1f - toAnkle), wAnkle = toKnee * toAnkle;
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = wHip, boneIndex1 = 1, weight1 = wKnee, boneIndex2 = 2, weight2 = wAnkle };
            }
            mesh.boneWeights = weights;
            // Pose de repos des os : hanche à l'origine, genou, cheville (sans rotation)
            mesh.bindposes = new[]
            {
                Matrix4x4.identity,
                Matrix4x4.Translate(-knee),
                Matrix4x4.Translate(-ankle),
            };

            // Fourrure : bleu nuit, « chaussette » claire à partir de sockStart (entre genou et cheville) jusqu'au bout
            float sockAt = Mathf.Lerp(kneeAt, ankleAt, s.sockStart);
            ApplyFurByIndex(s, mesh, 2f, i => Color.Lerp(s.furDark, s.furLight, Smooth01((along[i] - sockAt) / 0.05f + 0.5f)));
            return mesh;
        }

        // Rayon du pied le long de sa longueur (u : 0 cheville → 1 bout des doigts) : s'élargit, puis s'arrondit au bout
        private static float FootRadius(CatModelSettings s, float u, float ankleRadius)
        {
            u = Mathf.Clamp01(u);
            float r = Mathf.Lerp(ankleRadius, s.pawSize.y, Smooth01(u * 3f));
            return r * Mathf.Lerp(1f, 0.55f, Smooth01((u - 0.8f) / 0.2f));
        }

        // Proportion de la longueur de l'os où se trouve le point de passage 'index'
        private static float ArcFraction(IList<Vector3> bone, int index)
        {
            float total = 0f, upTo = 0f;
            for (int i = 1; i < bone.Count; i++)
            {
                float len = Vector3.Distance(bone[i - 1], bone[i]);
                total += len;
                if (i <= index) upTo += len;
            }
            return total > 0f ? upTo / total : 0f;
        }

        // Proportion le long de l'os du point de l'os le plus proche de p
        private static float ClosestAlong(Vector3[] samples, Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < samples.Length; i++)
            {
                float dd = (samples[i] - p).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = i; }
            }
            return best / (samples.Length - 1f);
        }

        // Relief en cloche centré sur 'at' (1 au centre, 0 au-delà de 'width')
        private static float Bump(float t, float at, float width)
        {
            float x = (t - at) / width;
            return Mathf.Exp(-x * x * 2f);
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // =====================================================================
        //  Bras
        // =====================================================================

        // Coude : à mi-longueur, un peu en arrière (le bras plié pointe vers l'avant)
        private static Vector3 ElbowPoint(CatModelSettings s)
        {
            return new Vector3(-0.02f, -s.armLength * 0.5f, 0f);
        }

        // Bras : de l'épaule (origine) au coude, épaule ronde
        private static Mesh BuildUpperArm(CatModelSettings s)
        {
            Vector3 elbow = ElbowPoint(s);
            float r = s.armThickness;
            var d = new MeshDraft();
            ShapedLoft(d, new[] { new Vector3(0f, 0.04f, 0f), elbow * 0.5f + new Vector3(0.01f, 0f, 0f), elbow },
                       r, s.upperArmWidth, s.upperArmBulge, 8, (ring, angle) => SewnPaint(s, ring, angle));
            Mesh mesh = d.ToMesh("CatUpperArm");
            ApplyFur(s, mesh, 4f, _ => s.furDark);
            return mesh;
        }

        // Avant-bras : du coude (origine) au poignet, main ronde et 3 griffes (sous-mesh 1)
        private static Mesh BuildForearm(CatModelSettings s)
        {
            float half = s.armLength * 0.5f;
            float r = s.armThickness;
            var wrist = new Vector3(0.02f, -half, 0f);
            var d = new MeshDraft(2);
            ShapedLoft(d, new[] { Vector3.zero, wrist * 0.5f, wrist }, r, s.forearmWidth, s.forearmBulge, 8,
                       (ring, angle) => SewnPaint(s, ring + 2, angle));
            Vector3 hand = wrist + new Vector3(0.015f, -r * 0.9f, 0f);
            Color handColor = Opaque(s.legColor * 1.25f);
            d.AddEllipsoid(hand, new Vector3(r * 1.25f, r * 1.1f, r * 1.05f), handColor);
            foreach (float z in new[] { -r * 0.6f, 0f, r * 0.6f })
            {
                Vector3 root = hand + new Vector3(r * 0.9f, -r * 0.5f, z);
                d.AddCone(root, root + new Vector3(s.clawLength * 0.6f, -s.clawLength, 0f), r * 0.28f, 5, Color.white, 1);
            }
            Mesh mesh = d.ToMesh("CatForearm");

            // Fourrure : bleu nuit, main claire
            ApplyFur(s, mesh, 5f, p => Color.Lerp(s.furDark, s.furLight, Mathf.InverseLerp(-half * 0.55f, -half * 0.9f, p.y)));
            return mesh;
        }

        // =====================================================================
        //  Tête
        // =====================================================================

        // Crâne (avec museau), oreilles, touffes des joues : sous-mesh 0 (chair + fourrure).
        // Yeux, pupilles, truffe : sous-mesh 1 (lumineux, couleurs des sommets).
        // Origine = base du cou (pivot de la tête).
        private static Mesh BuildHead(CatModelSettings s)
        {
            Vector3 c = s.headOffset;
            Vector3 R = s.headRadii;
            var skin = new FleshSkin(s, s.seed + 11, 5, 0, 0f);
            Vector3 muzzleDir = new Vector3(1f, -0.45f, 0f).normalized;
            var d = new MeshDraft(2);

            // Crâne : ellipsoïde, le museau bombe vers l'avant et le bas
            d.AddSphere(3, dir =>
            {
                Vector3 p = Vector3.Scale(dir, R);
                float muzzle = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, muzzleDir)), 3f) * s.muzzleLength;
                return c + p + muzzleDir * muzzle;
            }, skin.Paint);

            // Oreilles : cônes au sommet, écartés et penchés vers l'arrière
            foreach (float side in new[] { -1f, 1f })
            {
                var basePoint = c + new Vector3(s.earPosition.x, s.earPosition.y, s.earPosition.z * side);
                // Rotation Z : penche vers l'arrière ; rotation X : écarte vers l'extérieur (côté de l'oreille)
                Vector3 up = Quaternion.Euler(s.earSpread * side, 0f, s.earBack) * Vector3.up;
                d.AddCone(basePoint, basePoint + up * s.earLength, s.earWidth, 7, Opaque(s.legColor));
            }

            // Touffes des joues : petites pointes vers l'arrière et le bas
            if (s.cheekTuft > 0f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var root = c + new Vector3(R.x * 0.2f, -R.y * 0.45f, R.z * 0.75f * side);
                    foreach (float k in new[] { 0f, 1f })
                    {
                        Vector3 dir = new Vector3(-0.6f - k * 0.3f, -0.4f + k * 0.35f, 0.5f * side).normalized;
                        d.AddCone(root + new Vector3(-k * 0.03f, k * 0.04f, 0f), root + dir * s.cheekTuft,
                                  s.cheekTuft * 0.35f, 5, Opaque(s.legColor));
                    }
                }
            }

            // Yeux lumineux, pupille fendue devant, truffe au bout du museau
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eye = c + new Vector3(s.eyePosition.x, s.eyePosition.y, s.eyePosition.z * -side);
                float e = s.eyeSize;
                d.AddEllipsoid(eye, new Vector3(e * 0.9f, e, e * 0.55f), s.eyeColor, 2, 1);
                Vector3 pupil = eye + new Vector3(e * 0.25f, 0f, e * 0.4f * Mathf.Sign(eye.z - c.z));
                d.AddEllipsoid(pupil, new Vector3(e * 0.18f, e * 0.75f, e * 0.25f), PupilColor, 2, 1);
            }
            Vector3 nose = c + muzzleDir * (R.x + s.muzzleLength * 0.9f);
            d.AddEllipsoid(nose, new Vector3(0.025f, 0.018f, 0.03f), PupilColor, 2, 1);

            Mesh mesh = d.ToMesh("CatHead");
            BakeRotation(mesh, Quaternion.Euler(s.headRotation));

            // Fourrure : bleu nuit, museau et joues clairs (devant, en bas)
            ApplyFur(s, mesh, 6f, p =>
            {
                Vector3 q = Quaternion.Inverse(Quaternion.Euler(s.headRotation)) * p - c;
                q = new Vector3(q.x / R.x, q.y / R.y, q.z / R.z);
                float light = Mathf.InverseLerp(0.35f, 0.8f, q.x) * Mathf.InverseLerp(0.25f, -0.25f, q.y);
                return Color.Lerp(s.furDark, s.furLight, Mathf.SmoothStep(0f, 1f, light));
            });
            return mesh;
        }

        // =====================================================================
        //  Queue
        // =====================================================================

        // Un segment : de son pivot (origine) vers l'arrière (−X), effilé de la base vers le bout
        private static Mesh BuildTailSegment(CatModelSettings s, int index)
        {
            int n = s.tailSegments;
            float segLength = s.tailLength / n;
            float r0 = Mathf.Lerp(s.tailBaseRadius, s.tailTipRadius, (float)index / n);
            float r1 = Mathf.Lerp(s.tailBaseRadius, s.tailTipRadius, (float)(index + 1) / n);
            var d = new MeshDraft();
            // Dépasse un peu dans le segment suivant : pas de trou quand la queue se plie
            d.AddLoft(new[] { new Vector3(segLength * 0.08f, 0f, 0f), new Vector3(-segLength * 0.5f, 0f, 0f), new Vector3(-segLength * 1.08f, 0f, 0f) },
                      new[] { r0, (r0 + r1) * 0.5f, r1 }, 8, (ring, angle) => SewnPaint(s, index == 0 ? ring : ring + 3, angle), 0);
            Mesh mesh = d.ToMesh("CatTailSeg" + index);

            // Fourrure : bleu nuit qui vire à la couleur vive vers le bout
            ApplyFur(s, mesh, 7f + index, p =>
            {
                float t = (index + Mathf.Clamp01(-p.x / segLength)) / n;
                return Color.Lerp(s.furDark, s.furAccent, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t)));
            });
            return mesh;
        }

        // Flamme au bout de la queue : 3 langues de feu vers l'arrière et le haut, cœur clair à la base
        private static Mesh BuildFlame(CatModelSettings s)
        {
            float f = Mathf.Max(0.001f, s.flameSize);
            var d = new MeshDraft();
            Vector3[] tips =
            {
                new Vector3(-1.5f, 0.55f, 0f), new Vector3(-0.9f, 0.9f, 0.25f), new Vector3(-1f, 0.2f, -0.3f),
            };
            float[] widths = { 0.5f, 0.32f, 0.3f };
            for (int i = 0; i < tips.Length; i++)
            {
                Vector3 tip = tips[i] * f;
                d.AddLoft(new[] { Vector3.zero, tip * 0.45f, tip }, new[] { widths[i] * f, widths[i] * f * 0.75f, 0.003f }, 7,
                          (ring, _) => ring == 0 ? s.flameCoreColor : Color.Lerp(s.flameColor, s.flameColor * 0.8f, ring - 1), 0);
            }
            return d.ToMesh("CatTailFlame");
        }

        // =====================================================================
        //  Peinture
        // =====================================================================

        // Membre cousu : chair bleu nuit nuancée, couture claire à l'attache (anneaux 0 et 1)
        private static Color SewnPaint(CatModelSettings s, int ring, float angle)
        {
            if (ring == 0) return s.stitchColor;
            if (ring == 1 && Mathf.Sin(angle * 5f) > 0.2f) return s.stitchColor;
            float k = 0.85f + 0.3f * Mathf.PerlinNoise((angle * 3f + ring) * 1.7f, 0.37f);
            return new Color(s.legColor.r * k, s.legColor.g * k, s.legColor.b * k, 1f);
        }

        // Couche de fourrure : UV1 = (couleur de la fourrure, seuil), UV2.x = veines.
        // Seuil = bruit à grandes plaques : la fourrure part par plaques quand le membre s'abîme.
        // furColor reçoit la position du sommet dans le repère du mesh.
        private static void ApplyFur(CatModelSettings s, Mesh mesh, float seedOffset, Func<Vector3, Color> furColor)
        {
            Vector3[] v = mesh.vertices;
            ApplyFurByIndex(s, mesh, seedOffset, i => furColor(v[i]));
        }

        // Même chose, la couleur choisie d'après le numéro du sommet
        private static void ApplyFurByIndex(CatModelSettings s, Mesh mesh, float seedOffset, Func<int, Color> furColor)
        {
            Vector3[] v = mesh.vertices;
            var fur = new List<Vector4>(v.Length);
            var veins = new List<Vector2>(v.Length);
            float o = s.seed * 3.7f + seedOffset * 17.3f;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i];
                Color c = furColor(i);
                // Légère variation : la fourrure n'est pas un aplat parfait
                float grain = 0.88f + 0.24f * Noise(p * 14f, o + 2f);
                float threshold = Mathf.Clamp01((Noise(p * s.furPatchScale, o) - 0.36f) / 0.28f);
                // Veines : crêtes du bruit (valeur proche de 1 sur une ligne fine)
                float vein = Mathf.Clamp01(1f - Mathf.Abs(Noise(p * 9f, o + 9f) - 0.5f) * 9f);
                fur.Add(new Vector4(c.r * grain, c.g * grain, c.b * grain, Mathf.Lerp(0.04f, 0.97f, threshold)));
                veins.Add(new Vector2(vein, 0f));
            }
            mesh.SetUVs(1, fur);
            mesh.SetUVs(2, veins);
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
            private readonly float lumpiness;
            private readonly float noiseOffset;

            public FleshSkin(CatModelSettings settings, int seed, int patches, int lumps, float lumpiness)
            {
                s = settings;
                this.lumpiness = lumpiness;
                var rng = new System.Random(seed);
                patches = Mathf.Max(2, patches);
                patchDirs = RandomDirs(rng, patches);
                patchColors = new Color[patches];
                Color[] palette = s.fleshPalette != null && s.fleshPalette.Length > 0 ? s.fleshPalette : new[] { Color.red };
                for (int i = 0; i < patches; i++) patchColors[i] = palette[rng.Next(palette.Length)];
                lumpDirs = RandomDirs(rng, lumps);
                lumpSizes = new float[lumps];
                for (int i = 0; i < lumps; i++) lumpSizes[i] = lumpiness * (0.5f + (float)rng.NextDouble());
                noiseOffset = seed * 13.1f;
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
                r += (Noise(dir * 2.2f, noiseOffset) - 0.5f) * lumpiness * 0.9f;
                r += (Noise(dir * 7f, noiseOffset + 3f) - 0.5f) * lumpiness * 0.25f;
                float seam = SeamDistance(dir);
                if (seam < s.seamWidth) r -= s.seamDepth * (1f - seam / s.seamWidth);
                return r;
            }

            // Couleur de la chair (couleur des sommets)
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
        }

        // =====================================================================
        //  Outils
        // =====================================================================

        // Bruit 3D approché par 3 bruits 2D (valeur entre 0 et 1, le plus souvent entre 0.3 et 0.7)
        private static float Noise(Vector3 p, float o)
        {
            return (Mathf.PerlinNoise(p.x + o, p.y + 3.1f) + Mathf.PerlinNoise(p.y + o * 0.7f, p.z + 7.7f)
                    + Mathf.PerlinNoise(p.z + o * 1.3f, p.x + 1.9f)) / 3f;
        }

        // Tube le long d'un « os » (ligne de points), modelé par deux courbes réglables :
        //  - width : épaisseur le long de l'os (0 = début, 1 = fin), multiplicateur de baseRadius ;
        //  - bulge : décalage du muscle, perpendiculaire à l'os dans le plan de profil (> 0 vers l'avant).
        // L'os lui-même ne bouge pas : les articulations restent à leur place (marche, coude).
        private static void ShapedLoft(MeshDraft d, IList<Vector3> bone, float baseRadius, AnimationCurve width,
                                       AnimationCurve bulge, int segments, Func<int, float, Color> paint)
        {
            const int Rings = 12;
            var points = new Vector3[Rings];
            var radii = new float[Rings];
            for (int i = 0; i < Rings; i++)
            {
                float t = i / (Rings - 1f);
                Vector3 p = BonePoint(bone, t);
                Vector3 tangent = (BonePoint(bone, Mathf.Min(1f, t + 0.01f)) - BonePoint(bone, Mathf.Max(0f, t - 0.01f))).normalized;
                var front = new Vector3(-tangent.y, tangent.x, 0f);   // os vers le bas → avant = +X
                points[i] = p + front * (Curve(bulge, t, 0f) * baseRadius);
                radii[i] = Mathf.Max(0.002f, Curve(width, t, 1f) * baseRadius);
            }
            d.AddLoft(points, radii, segments, paint, 0);
        }

        // Point de l'os à la proportion t (0 à 1) de sa longueur, lissé (Catmull-Rom entre les points)
        private static Vector3 BonePoint(IList<Vector3> bone, float t)
        {
            int n = bone.Count;
            float total = 0f;
            for (int i = 1; i < n; i++) total += Vector3.Distance(bone[i - 1], bone[i]);
            float target = Mathf.Clamp01(t) * total;
            for (int i = 1; i < n; i++)
            {
                float len = Vector3.Distance(bone[i - 1], bone[i]);
                if (target <= len || i == n - 1)
                {
                    float u = len > 0f ? Mathf.Clamp01(target / len) : 0f;
                    Vector3 p0 = bone[Mathf.Max(0, i - 2)], p1 = bone[i - 1], p2 = bone[i], p3 = bone[Mathf.Min(n - 1, i + 1)];
                    return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u
                                   + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u);
                }
                target -= len;
            }
            return bone[n - 1];
        }

        // Valeur d'une courbe réglable, ou valeur par défaut si la courbe est vide
        private static float Curve(AnimationCurve curve, float t, float fallback)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(t) : fallback;
        }

        private static Color Opaque(Color c)
        {
            c.a = 1f;
            return c;
        }

        // Tourne tous les sommets autour de l'origine du mesh (pivot)
        private static void BakeRotation(Mesh mesh, Quaternion rotation)
        {
            if (rotation == Quaternion.identity) return;
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = rotation * v[i];
            mesh.vertices = v;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
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

        // Réutilise le matériau s'il existe (réglages faits à la main conservés), sinon le crée avec 'setup'
        private static Material SaveMaterial(string name, string shader, Action<Material> setup)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Material fresh = MaterialFactory.FromShader(shader);
            setup?.Invoke(fresh);
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        // Le fichier contient un mesh principal vide (conteneur, porte le nom du fichier) et les vrais meshes
        // en sous-objets, retrouvés par leur nom à chaque reconstruction et mis à jour en place.
        // Renvoie les meshes enregistrés, par nom.
        private static Dictionary<string, Mesh> SaveMeshes(List<Mesh> meshes)
        {
            var result = new Dictionary<string, Mesh>();
            Mesh main = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (main == null)
            {
                AssetDatabase.CreateAsset(new Mesh { name = "CatModels" }, MeshPath);
                main = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            }
            if (main.vertexCount > 0) main.Clear();

            var existing = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
            {
                if (o is Mesh m && m != main) existing[m.name] = m;
            }
            foreach (Mesh mesh in meshes)
            {
                if (existing.TryGetValue(mesh.name, out Mesh target))
                {
                    CopyMesh(mesh, target);
                    Object.DestroyImmediate(mesh);
                    result[target.name] = target;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                    result[mesh.name] = mesh;
                }
            }
            return result;
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
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 3; channel++)
            {
                source.GetUVs(channel, uv);
                if (uv.Count > 0) target.SetUVs(channel, uv);
            }
            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
            // Maillage déformé par des os (patte) : poids et pose de repos
            target.boneWeights = source.boneWeights;
            target.bindposes = source.bindposes;
            target.RecalculateBounds();
            EditorUtility.SetDirty(target);
        }

        // Enfant vide (mesh null) ou avec un rendu
        private static Transform AddChild(Transform parent, string name, Vector3 localPosition, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (mesh != null) AddRenderer(go, mesh, materials);
            return go.transform;
        }

        // Un matériau par sous-mesh
        private static void AddRenderer(GameObject go, Mesh mesh, params Material[] materials)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        // Remplace le prefab en gardant son GUID (les références de la scène restent valides)
        private static void SavePrefab(string name, Action<GameObject> build)
        {
            var root = new GameObject(name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }
    }
}
