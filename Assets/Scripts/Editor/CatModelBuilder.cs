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
    //  - FleshBall : le torse (chair du Gardien refermée sur l'âme), en œuf / capsule, collerette de poils ;
    //  - CatLeg    : grande patte arrière digitigrade (cuisse, jarret, grand pied à doigts), articulée au genou
    //                et à la cheville (le pied reste à plat) ;
    //  - CatArm    : bras fin (patte avant du chat redressé), articulé au coude, touffes au coude et au poignet,
    //                main à doigts griffus ;
    //  - CatHead   : tête (crâne, museau court, grandes oreilles à l'intérieur clair, collerette des joues,
    //                yeux lumineux en amande) ;
    //  - CatTail   : grosse queue touffue en segments (pour onduler).
    // La fourrure en relief est faite de touffes (pointes aplaties, MeshDraft.AddTuft).
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
        internal const string SettingsPath = "Assets/Meshes/Player/CatModelSettings.asset";
        private const string MeshPath = "Assets/Meshes/Player/CatModels.asset";     // tous les meshes, en sous-assets
        private const string MaterialFolder = "Assets/Materials/Player";
        private const string PrefabFolder = "Assets/Prefabs/Player";

        // ---------- Patte : gabarit à la longueur 1 ----------
        // Repère : hanche à l'origine, patte de profil (X = avant, Y = haut). Tout est multiplié par legLength.
        private static readonly Vector3 KneeAt1 = new Vector3(0.07f, -0.18f, 0f);    // hanche → genou
        private static readonly Vector3 AnkleAt1 = new Vector3(0f, -0.235f, 0f);     // genou → cheville

        private static readonly Color PupilColor = new Color(0.02f, 0.02f, 0.03f);
        internal const int HipAnchorBone = 3;   // os du haut de la cuisse (prefab CatLeg : HipAnchor)

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

        // ---------- Pour l'aller-retour avec Blender (CatBlenderExport) ----------

        internal static CatModelSettings Settings => LoadOrCreateSettings();

        // Pièces fraîchement construites, non enregistrées, sommets partagés (pas de facettes séparées) :
        // plus faciles à modifier dans Blender. Clé = nom du mesh (FleshBall, CatLegSkin, CatHead, ...).
        internal static Dictionary<string, Mesh> BuildEditableMeshes(CatModelSettings s)
        {
            var list = new List<Mesh> { BuildTorso(s), BuildLegSkin(s), BuildHead(s, false), BuildUpperArm(s), BuildForearm(s) };
            for (int i = 0; i < s.tailSegments; i++) list.Add(BuildTailSegment(s, i));
            var result = new Dictionary<string, Mesh>();
            foreach (Mesh mesh in list) result[mesh.name] = mesh;
            return result;
        }

        // Articulations et points d'attache du modèle généré
        internal static CatRig GeneratedRig(CatModelSettings s)
        {
            var rig = new CatRig
            {
                Neck = s.neckPoint,
                Shoulder = s.shoulderPoint,
                TailBase = s.tailPoint,
                Hip = new Vector3(s.hipPoint.x, s.hipPoint.y, -(Mathf.Abs(s.hipPoint.z) + s.hipSpread)),
                Knee = KneeRest(s),
                AnkleFromKnee = AnkleAt1 * s.legLength,
                Elbow = ElbowPoint(s),
                Eye = DefaultEye(s),
                Nose = DefaultNose(s),
                TailSegments = s.tailSegments,
                TailSegmentLength = s.tailLength / s.tailSegments,
            };
            rig.SetStraightTail();
            rig.SetPoseFrom(s);
            return rig;
        }

        internal static Vector3 LegKnee(CatModelSettings s) => KneeRest(s);      // repère de la hanche
        internal static Vector3 LegAnkle(CatModelSettings s) => AnkleRest(s);    // repère de la hanche
        internal static Vector3 ArmElbow(CatModelSettings s) => ElbowPoint(s);   // repère de l'épaule

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
            // Style : même matériau (réglages gardés), shader changé
            ApplyStyle(s, flesh);
            ApplyStyle(s, leg);
            Material claw = SaveMaterial("CatClaw", "Standard", m => m.SetFloat("_Glossiness", 0.6f));
            claw.color = s.clawColor;
            EditorUtility.SetDirty(claw);

            // Pièces : modèle Blender si choisi (CatBlenderImport), sinon générées d'après les réglages
            if (!CatBlenderImport.TryBuild(s, out List<Mesh> built, out CatRig rig))
            {
                built = new List<Mesh>
                {
                    BuildTorso(s), BuildLegSkin(s), BuildHead(s), BuildUpperArm(s), BuildForearm(s),
                };
                for (int i = 0; i < s.tailSegments; i++) built.Add(BuildTailSegment(s, i));
                rig = GeneratedRig(s);
            }
            // Gameplay : torse en poitrine / ventre / bassin, patte en cuisse / jarret / pied (une vie chacun)
            SplitTorso(built, s);
            SplitLeg(built);
            // Queue : segments recollés en un maillage continu à os (CatTailBuilder)
            CatTailBuilder.Chain tailChain = CatTailBuilder.Merge(built, rig);
            // Modèle généré : la main reste dans l'avant-bras (main vide)
            if (!built.Exists(x => x.name == "CatHand")) built.Add(EmptyMesh("CatHand", 2));
            if (IsLowPoly(s))
            {
                foreach (Mesh mesh in built) FlattenFaces(mesh);
            }
            // UV3 = position de repos de chaque sommet (repère du maillage, avant déformation par les os) :
            // fissures et plaies y sont calculées, elles restent collées à la peau quand le membre se plie
            foreach (Mesh mesh in built)
            {
                if (mesh.vertexCount > 0) mesh.SetUVs(3, mesh.vertices);
            }
            Dictionary<string, Mesh> m = SaveMeshes(built);

            // Torse : 3 parties (Chest, Belly, Pelvis), points d'attache de la tête, des bras et de la queue
            SavePrefab("FleshBall", go =>
            {
                AddChild(go.transform, "Chest", Vector3.zero, m["FleshChest"], flesh);
                AddChild(go.transform, "Belly", Vector3.zero, m["FleshBelly"], flesh);
                AddChild(go.transform, "Pelvis", Vector3.zero, m["FleshPelvis"], flesh);
                var torsoRig = go.AddComponent<CatTorsoRig>();
                torsoRig.SetPoints(rig.Neck, rig.Shoulder, rig.TailBase, rig.Hip);
                torsoRig.SetPose(rig.StandingPosition, rig.StandingTilt);
            });

            // CatLeg (hanche, os 0) → Knee (os 1, pivot du genou) → Ankle (os 2, pivot de la cheville, pied à plat)
            //                      → HipAnchor (os 3, haut de la cuisse : CatBody annule la rotation de la cuisse,
            //                        la fesse reste collée au corps et le pli se fait à la hanche)
            //                      → Thigh, Shin, Foot : les 3 parties de la patte, déformées par les mêmes os
            //                        (elles restent jointes en se pliant ; chacune peut disparaître seule)
            SavePrefab("CatLeg", go =>
            {
                var legRig = go.AddComponent<CatLegRig>();
                legRig.SetGeometry(rig.Knee, rig.AnkleFromKnee);
                legRig.SetStance(rig.LegSplay, rig.FootForward, rig.AnkleHeight);
                Transform kneeT = AddChild(go.transform, "Knee", rig.Knee, null);
                Transform ankleT = AddChild(kneeT, "Ankle", rig.AnkleFromKnee, null);
                Transform anchorT = AddChild(go.transform, "HipAnchor", Vector3.zero, null);
                foreach (string part in new[] { "Thigh", "Shin", "Foot" })
                {
                    var skin = new GameObject(part).AddComponent<SkinnedMeshRenderer>();
                    skin.transform.SetParent(go.transform, false);
                    skin.sharedMesh = m["CatLeg" + part];
                    skin.sharedMaterial = leg;
                    skin.bones = new[] { go.transform, kneeT, ankleT, anchorT };
                    skin.rootBone = go.transform;
                    skin.updateWhenOffscreen = true;   // bornes recalculées : la patte pliée n'est jamais coupée
                }
            });

            // Rotations des pièces (racine, coude, segments) = pose de repos ; CatBody y ajoute les mouvements
            // CatArm (épaule) → Upper (bras)
            //                 → Elbow (pivot du coude) → Fore (avant-bras)
            //                                          → Hand (main, griffes) : membres à part (PartOf)
            SavePrefab("CatArm", go =>
            {
                go.transform.localRotation = rig.ArmRest;
                AddChild(go.transform, "Upper", Vector3.zero, m["CatUpperArm"], flesh);
                Transform elbow = AddChild(go.transform, "Elbow", rig.Elbow, null);
                elbow.localRotation = rig.ElbowRest;
                AddChild(elbow, "Fore", Vector3.zero, m["CatForearm"], flesh, claw);
                AddChild(elbow, "Hand", Vector3.zero, m["CatHand"], flesh, claw);
            });

            // CatHead (base du cou, pivot de la tête) : crâne + yeux
            SavePrefab("CatHead", go =>
            {
                go.transform.localRotation = rig.HeadRest;
                AddRenderer(go, m["CatHead"], flesh, glow);
            });

            // CatTail (naissance) → Bone0 → Bone1 → ... → BoneN : chaîne régulière d'os (pose de repos du fichier),
            //                     → Tail : un seul maillage continu déformé par ces os (CatBody les anime)
            // Sans chaîne (segments manquants) : ancienne queue en segments rigides Seg0 → Seg1 → ...
            SavePrefab("CatTail", go =>
            {
                Transform parent = go.transform;
                if (tailChain != null)
                {
                    var bones = new Transform[CatTailBuilder.BoneCount];
                    for (int k = 0; k < bones.Length; k++)
                    {
                        parent = AddChild(parent, "Bone" + k, tailChain.LocalPositions[k], null);
                        parent.localRotation = Quaternion.Euler(0f, 0f, tailChain.LocalAngles[k]);
                        bones[k] = parent;
                    }
                    var skin = new GameObject("Tail").AddComponent<SkinnedMeshRenderer>();
                    skin.transform.SetParent(go.transform, false);
                    skin.sharedMesh = m[CatTailBuilder.MeshName];
                    skin.sharedMaterial = flesh;
                    skin.bones = bones;
                    skin.rootBone = bones[0];
                    skin.updateWhenOffscreen = true;
                    return;
                }
                for (int i = 0; i < rig.TailSegments; i++)
                {
                    parent = AddChild(parent, "Seg" + i, i == 0 ? Vector3.zero : rig.TailPositions[i], m["CatTailSeg" + i], flesh);
                    parent.localRotation = rig.TailRotations[i];
                }
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
            var d = NewDraft(s);
            d.AddSphere(s.bodyDetail, dir => placement.MultiplyPoint3x4(EggPoint(s, dir) * skin.Displace(dir)), skin.Paint);
            var marks = new FurMarks();
            AddCollar(s, d, marks, placement, skin);
            Mesh mesh = d.ToMesh("FleshBall");

            // Fourrure : bleu nuit, poitrail clair sur le devant, collerette claire
            Matrix4x4 toEgg = placement.inverse;
            Vector3[] v = mesh.vertices;
            ApplyFurByIndex(s, mesh, 1f, i => marks.Get(i, () =>
            {
                Vector3 e = toEgg.MultiplyPoint3x4(v[i]);
                Vector3 dir = new Vector3(e.x / s.bodyRadii.x, e.y / s.bodyRadii.y, e.z / s.bodyRadii.z);
                float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f - s.chestAmount, 1.05f - s.chestAmount * 0.6f, dir.x));
                float band = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, Mathf.Abs(dir.y - 0.05f)));
                return Color.Lerp(s.furDark, s.furLight, front * band * (s.chestAmount > 0f ? 1f : 0f));
            }));
            return mesh;
        }

        // Collerette : deux rangs de touffes claires autour du haut du torse (devant et côtés),
        // qui retombent vers le bas et l'extérieur. Le 2e rang, plus bas, est décalé et plus long.
        private static void AddCollar(CatModelSettings s, MeshDraft d, FurMarks marks, Matrix4x4 placement, FleshSkin skin)
        {
            if (s.collarLength <= 0f || s.collarTufts <= 0) return;
            int from = d.VertexCount;
            for (int row = 0; row < 2; row++)
            {
                float h = s.collarHeight - row * 0.17f;          // hauteur sur la sphère unité (−1 bas, 1 haut)
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - h * h));
                int count = s.collarTufts + row;
                for (int k = 0; k < count; k++)
                {
                    float u = count == 1 ? 0.5f : (k + row * 0.5f) / (count - 1f + row);
                    float phi = Mathf.Lerp(-s.collarSpread, s.collarSpread, u) * Mathf.Deg2Rad;   // 0 = devant (+X)
                    var dir = new Vector3(Mathf.Cos(phi) * ring, h, Mathf.Sin(phi) * ring);
                    Vector3 root = placement.MultiplyPoint3x4(EggPoint(s, dir) * 0.97f);
                    var outward = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    float jitter = 0.8f + 0.4f * Hash01(k, row + 31);
                    float length = s.collarLength * (row == 0 ? 0.85f : 1.1f) * jitter;
                    // Mèches larges et plates qui retombent en se recourbant : de la fourrure, pas des éclats
                    Vector3 tuftDir = outward * 0.6f + Vector3.down;
                    d.AddTuft(root, tuftDir, length, length * 0.55f, outward * length * 0.35f + Vector3.down * length * 0.1f,
                              skin.Paint(dir), 0.4f, 6);
                }
            }
            marks.Add(from, d.VertexCount, s.furLight);
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

        private static int LegRings(CatModelSettings s) => IsLowPoly(s) ? 26 : 56;   // anneaux le long de la patte (plus = plus lisse)
        private const int LegSegments = 14;       // sommets autour de chaque anneau (moins en low-poly : MeshDraft.LowPoly)
        internal const float JointBlend = 0.045f;  // largeur du mélange de poids autour d'une articulation (part de la longueur)

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
            float footY = -0.07f + P.y;   // centre du pied : la cheville est à 0.07 au-dessus du sol (CatLegRig.AnkleHeight)

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
            int legRings = LegRings(s);
            var points = new Vector3[legRings];
            var radii = new float[legRings];
            var depth = new float[legRings];
            for (int i = 0; i < legRings; i++)
            {
                float t = i / (legRings - 1f);
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

            var d = NewDraft(s);
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
                Matrix4x4.identity,   // HipAnchor (haut de cuisse) : inutilisé par la patte générée
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

        internal static float Smooth01(float x)
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
            var d = NewDraft(s);
            ShapedLoft(d, new[] { new Vector3(0f, 0.04f, 0f), elbow * 0.5f + new Vector3(0.01f, 0f, 0f), elbow },
                       r, s.upperArmWidth, s.upperArmBulge, 8, (ring, angle) => SewnPaint(s, ring, angle));

            // Touffe derrière le coude : deux mèches qui pointent vers l'arrière et le bas
            if (s.elbowTuft > 0f)
            {
                Color c = SewnPaint(s, 3, 0f);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 root = elbow + new Vector3(-r * 0.6f, r * (1.2f - k * 0.9f), 0f);
                    float length = s.elbowTuft * (1f - k * 0.3f);
                    d.AddTuft(root, new Vector3(-0.8f, -0.55f - k * 0.3f, 0f), length, r * 0.65f, new Vector3(0f, -length * 0.2f, 0f), c);
                }
            }
            Mesh mesh = d.ToMesh("CatUpperArm");
            ApplyFur(s, mesh, 4f, _ => s.furDark);
            return mesh;
        }

        // Avant-bras : du coude (origine) au poignet, manchette de poils, main (paume + doigts) et griffes (sous-mesh 1)
        private static Mesh BuildForearm(CatModelSettings s)
        {
            float half = s.armLength * 0.5f;
            float r = s.armThickness;
            var wrist = new Vector3(0.02f, -half, 0f);
            var d = NewDraft(s, 2);
            ShapedLoft(d, new[] { Vector3.zero, wrist * 0.5f, wrist }, r, s.forearmWidth, s.forearmBulge, 8,
                       (ring, angle) => SewnPaint(s, ring + 2, angle));

            // Manchette : couronne de mèches au-dessus du poignet, qui retombent sur la main
            if (s.wristTuft > 0f)
            {
                Color c = SewnPaint(s, 4, 0f);
                const int Count = 6;
                for (int k = 0; k < Count; k++)
                {
                    float a = (k + 0.5f) / Count * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 root = wrist + new Vector3(0f, half * 0.18f, 0f) + radial * r * 0.55f;
                    float length = s.wristTuft * (0.8f + 0.4f * Hash01(k, 7));
                    d.AddTuft(root, radial * 0.5f + Vector3.down, length, r * 0.5f, radial * length * 0.2f, c);
                }
            }

            // Main : paume ronde, doigts en rang devant (légèrement repliés), une griffe courte au bout de chacun
            Color handColor = Opaque(s.legColor * 1.25f);
            Vector3 palm = wrist + new Vector3(0.01f, -r * 1.05f, 0f);
            d.AddEllipsoid(palm, new Vector3(r * 1.15f, r * 1.25f, r * 1.05f), handColor);
            int fingers = Mathf.Max(0, s.fingerCount);
            for (int k = 0; k < fingers; k++)
            {
                float u = fingers == 1 ? 0f : k / (fingers - 1f) * 2f - 1f;   // −1 → 1 en travers de la main
                Vector3 finger = palm + new Vector3(r * (0.75f - 0.12f * u * u), -r * 1.05f, u * r * 0.72f);
                d.AddEllipsoid(finger, new Vector3(r * 0.38f, r * 0.55f, r * 0.3f), handColor);
                if (s.clawLength > 0f)
                {
                    Vector3 root = finger + new Vector3(r * 0.2f, -r * 0.4f, 0f);
                    d.AddCone(root, root + new Vector3(s.clawLength * 0.35f, -s.clawLength * 0.6f, 0f), r * 0.16f, 5, Color.white, 1);
                }
            }
            Mesh mesh = d.ToMesh("CatForearm");

            // Fourrure : bleu nuit, main claire
            ApplyFur(s, mesh, 5f, p => Color.Lerp(s.furDark, s.furLight, Mathf.InverseLerp(-half * 0.55f, -half * 0.9f, p.y)));
            return mesh;
        }

        // =====================================================================
        //  Tête
        // =====================================================================

        // Crâne (avec museau court), grandes oreilles, collerette des joues, mèches : sous-mesh 0 (chair + fourrure).
        // Yeux en amande, pupilles, truffe : sous-mesh 1 (lumineux, couleurs des sommets).
        // Origine = base du cou (pivot de la tête).
        // withFace = false : sans yeux ni truffe (export vers Blender : Unity les rajoute au retour, à la place des repères Eye et Nose).
        private static Mesh BuildHead(CatModelSettings s, bool withFace = true)
        {
            Vector3 c = s.headOffset;
            Vector3 R = s.headRadii;
            var skin = new FleshSkin(s, s.seed + 11, 5, 0, 0f);
            Vector3 muzzleDir = new Vector3(1f, -0.45f, 0f).normalized;
            Color under = Opaque(s.legColor);
            var d = NewDraft(s, 2);
            var marks = new FurMarks();

            // Crâne : ellipsoïde, le museau bombe vers l'avant et le bas
            d.AddSphere(s.headDetail, dir =>
            {
                Vector3 p = Vector3.Scale(dir, R);
                float muzzle = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, muzzleDir)), 3f) * s.muzzleLength;
                return c + p + muzzleDir * muzzle;
            }, skin.Paint);

            foreach (float side in new[] { -1f, 1f })
            {
                AddEar(s, d, marks, c, side, under);
                AddCheekRuff(s, d, c, R, side, under);
            }

            // Mèches du front, entre les oreilles : vers le haut et l'arrière
            if (s.foreheadTuft > 0f)
            {
                for (int k = 0; k < 3; k++)
                {
                    float z = (k - 1) * R.z * 0.22f;
                    Vector3 root = c + new Vector3(R.x * 0.25f, R.y * 0.88f, z);
                    float length = s.foreheadTuft * (k == 1 ? 1.15f : 0.85f);
                    d.AddTuft(root, new Vector3(-0.5f, 1f, z * 2f), length, length * 0.4f, new Vector3(-length * 0.35f, 0f, 0f), under);
                }
            }

            if (withFace) AddFace(s, d, c + s.eyePosition, c + muzzleDir * (R.x + s.muzzleLength * 0.9f));

            Mesh mesh = d.ToMesh("CatHead");
            BakeRotation(mesh, Quaternion.Euler(s.headRotation));

            // Fourrure : bleu nuit, museau et bas des joues clairs ; intérieur des oreilles clair
            Vector3[] v = mesh.vertices;
            Quaternion unturn = Quaternion.Inverse(Quaternion.Euler(s.headRotation));
            ApplyFurByIndex(s, mesh, 6f, i => marks.Get(i, () =>
            {
                Vector3 q = unturn * v[i] - c;
                q = new Vector3(q.x / R.x, q.y / R.y, q.z / R.z);
                float light = Mathf.InverseLerp(0.35f, 0.8f, q.x) * Mathf.InverseLerp(0.25f, -0.25f, q.y);
                return Color.Lerp(s.furDark, s.furLight, Mathf.SmoothStep(0f, 1f, light));
            }));
            return mesh;
        }

        // Yeux lumineux en amande (coin arrière relevé), pupille fendue devant, truffe : sous-mesh 1.
        // eye = œil côté caméra (z < 0), l'autre est symétrique (z opposé) ; repère de la tête.
        // eyes = false : truffe seule (yeux modelés dans Blender).
        internal static void AddFace(CatModelSettings s, MeshDraft d, Vector3 eye, Vector3 nose, bool eyes = true)
        {
            Quaternion eyeTurn = Quaternion.Euler(0f, 0f, -s.eyeTilt);
            float e = s.eyeSize;
            foreach (float side in eyes ? new[] { 1f, -1f } : new float[0])
            {
                var at = new Vector3(eye.x, eye.y, eye.z * side);
                d.AddSphere(2, dir => at + eyeTurn * Vector3.Scale(dir, new Vector3(e * 1.15f, e * 0.72f, e * 0.5f)),
                            _ => s.eyeColor, 1);
                // Pupille un peu devant l'œil, vers l'extérieur de la tête
                Vector3 pupil = at + new Vector3(e * 0.2f, 0f, e * 0.4f * Mathf.Sign(at.z == 0f ? -1f : at.z));
                d.AddEllipsoid(pupil, new Vector3(e * 0.16f, e * 0.6f, e * 0.25f), PupilColor, 2, 1);
            }
            d.AddEllipsoid(nose, new Vector3(0.025f, 0.018f, 0.03f), PupilColor, 2, 1);
        }

        // Places par défaut de l'œil côté caméra et de la truffe, dans le repère de la tête (rotation de la tête comprise)
        internal static Vector3 DefaultEye(CatModelSettings s) => Quaternion.Euler(s.headRotation) * (s.headOffset + s.eyePosition);

        internal static Vector3 DefaultNose(CatModelSettings s)
        {
            Vector3 muzzleDir = new Vector3(1f, -0.45f, 0f).normalized;
            return Quaternion.Euler(s.headRotation) * (s.headOffset + muzzleDir * (s.headRadii.x + s.muzzleLength * 0.9f));
        }

        // Grande oreille : lame triangulaire fine (d'avant en arrière), large à la base, penchée vers l'arrière
        // et écartée ; intérieur clair (lame plus petite, devant) et touffes qui en sortent
        private static void AddEar(CatModelSettings s, MeshDraft d, FurMarks marks, Vector3 c, float side, Color under)
        {
            var basePoint = c + new Vector3(s.earPosition.x, s.earPosition.y, s.earPosition.z * side);
            // Rotation Z : penche vers l'arrière ; rotation X : écarte vers l'extérieur (côté de l'oreille)
            Quaternion turn = Quaternion.Euler(s.earSpread * side, 0f, s.earBack);
            Vector3 up = turn * Vector3.up;
            Vector3 front = turn * Vector3.right;
            // Section : moins épaisse sur X (0.6 × largeur) que large sur Z ; la pointe se resserre.
            // Pas trop fine sur X : vue de profil (caméra du jeu), l'oreille doit rester un triangle bien lisible
            float thick = s.earWidth * 0.6f;
            float flat = 1f / 0.6f;
            AddBlade(d, basePoint - up * s.earWidth * 0.3f, up, s.earLength * 1.1f, thick, flat, under);

            if (s.earInner > 0f)
            {
                int from = d.VertexCount;
                float k = s.earInner;
                Vector3 innerBase = basePoint + front * thick * 0.55f + up * s.earLength * 0.05f;
                AddBlade(d, innerBase, up, s.earLength * k, thick * 0.6f * k, flat, Opaque(s.fleshPalette.Length > 5 ? s.fleshPalette[5] : under));
                marks.Add(from, d.VertexCount, s.furLight);
            }

            if (s.earTuft > 0f)
            {
                int from = d.VertexCount;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 root = basePoint + front * thick * 0.7f + up * s.earLength * (0.08f + k * 0.12f);
                    var dir = front * 0.8f + up * (0.6f + k * 0.25f) + new Vector3(0f, 0f, side * 0.3f);
                    d.AddTuft(root, dir, s.earTuft * (1f - k * 0.2f), s.earTuft * 0.3f, up * s.earTuft * 0.3f, under);
                }
                marks.Add(from, d.VertexCount, s.furLight);
            }
        }

        // Lame : tube aplati qui s'effile de la base vers la pointe (oreille)
        private static void AddBlade(MeshDraft d, Vector3 basePoint, Vector3 up, float length, float thick, float flat, Color color)
        {
            var points = new[] { basePoint, basePoint + up * length * 0.4f, basePoint + up * length * 0.75f, basePoint + up * length };
            var radii = new[] { thick, thick * 0.8f, thick * 0.45f, 0.002f };
            var depth = new[] { flat, flat, flat, flat };
            d.AddLoft(points, radii, 7, (_, __) => color, 0, depth);
        }

        // Collerette des joues : éventail de touffes autour de l'arrière et du bas de la tête,
        // du dessous de l'oreille jusque sous le menton, qui partent vers l'arrière et l'extérieur
        private static void AddCheekRuff(CatModelSettings s, MeshDraft d, Vector3 c, Vector3 R, float side, Color under)
        {
            int n = s.cheekTuftCount;
            if (s.cheekTuft <= 0f || n <= 0) return;
            for (int k = 0; k < n; k++)
            {
                float u = n == 1 ? 0.5f : k / (n - 1f);
                float theta = Mathf.Lerp(150f, 290f, u) * Mathf.Deg2Rad;   // dans le profil : 180 = arrière, 270 = bas
                var dir = new Vector3(Mathf.Cos(theta) * 0.8f, Mathf.Sin(theta) * 0.85f, side * 0.45f).normalized;
                Vector3 root = c + Vector3.Scale(dir, R) * 0.88f;
                // Plus longues sur la joue (milieu de l'éventail), plus courtes en haut et sous le menton
                float length = s.cheekTuft * (0.65f + 0.45f * Mathf.Sin(u * Mathf.PI)) * (0.85f + 0.3f * Hash01(k, side > 0f ? 3 : 5));
                // Couchées dans le plan du profil (peu de Z) : elles dépassent de la silhouette vue par la caméra du jeu
                var flat = new Vector3(dir.x, dir.y, 0f).normalized;
                Vector3 tuftDir = flat + new Vector3(-0.6f, -0.2f, side * 0.15f);
                d.AddTuft(root, tuftDir, length, length * 0.45f, new Vector3(-length * 0.15f, -length * 0.2f, 0f), under, 0.5f);
            }
        }

        // =====================================================================
        //  Queue
        // =====================================================================

        // Rayon de la queue à la proportion t de sa longueur (0 naissance, 1 bout)
        private static float TailRadius(CatModelSettings s, float t)
        {
            return Mathf.Max(0.004f, s.tailRadius * Curve(s.tailWidth, t, 1f));
        }

        // Un segment : de son pivot (origine) vers l'arrière (−X). Gros fuseau touffu :
        //  - une boule au pivot comble le creux quand la queue se plie ;
        //  - des touffes en couronne, couchées vers l'arrière, hérissent la silhouette (décalées d'un segment à l'autre) ;
        //  - le dernier segment finit par une mèche en pointe.
        private static Mesh BuildTailSegment(CatModelSettings s, int index)
        {
            int n = s.tailSegments;
            float segLength = s.tailLength / n;
            float t0 = (float)index / n, t1 = (index + 1f) / n;
            var d = NewDraft(s);

            // Fuseau : dépasse un peu dans le segment suivant (pas de trou quand la queue se plie)
            int rings = d.LowPoly ? 3 : 5;
            var points = new Vector3[rings];
            var radii = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float u = i / (rings - 1f);
                points[i] = new Vector3(Mathf.Lerp(segLength * 0.08f, -segLength * 1.08f, u), 0f, 0f);
                radii[i] = TailRadius(s, Mathf.Lerp(t0, t1, u));
            }
            d.AddLoft(points, radii, 10, (ring, angle) => SewnPaint(s, index == 0 ? ring : ring + 3, angle), 0);
            if (index > 0) d.AddSphere(d.LowPoly ? 1 : 2, dir => Vector3.Scale(dir, Vector3.one * TailRadius(s, t0) * 0.97f), _ => SewnPaint(s, 3, 0f));

            // Touffes : couronne au milieu du segment, inclinées vers l'arrière et vers l'extérieur
            Color tuftColor = SewnPaint(s, 4, 0f);
            float fullness = TailRadius(s, (t0 + t1) * 0.5f) / Mathf.Max(0.001f, s.tailRadius);
            for (int k = 0; k < s.tailTufts; k++)
            {
                float a = (k + (index % 2) * 0.5f) / s.tailTufts * Mathf.PI * 2f;
                var radial = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                float x = -segLength * (0.35f + 0.3f * Hash01(k, index + 11));
                float r = TailRadius(s, Mathf.Lerp(t0, t1, -x / segLength));
                Vector3 root = new Vector3(x, 0f, 0f) + radial * r * 0.8f;
                float length = s.tailTuftLength * fullness * (0.75f + 0.5f * Hash01(k, index + 23));
                d.AddTuft(root, radial * 0.55f + Vector3.left, length, length * 0.42f, radial * length * 0.15f, tuftColor);
            }
            // Bout : longue mèche en pointe
            if (index == n - 1 && s.tailTuftLength > 0f)
            {
                float length = s.tailTuftLength * 1.4f;
                d.AddTuft(new Vector3(-segLength * 0.8f, 0f, 0f), Vector3.left, length, TailRadius(s, 0.9f) * 0.9f,
                          new Vector3(0f, length * 0.15f, 0f), tuftColor);
            }
            Mesh mesh = d.ToMesh("CatTailSeg" + index);

            // Fourrure : bleu nuit qui vire à la couleur vive vers le bout
            ApplyFur(s, mesh, 7f + index, p =>
            {
                float t = (index + Mathf.Clamp01(-p.x / segLength)) / n;
                return Color.Lerp(s.furDark, s.furAccent, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.05f, t)));
            });
            return mesh;
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
        internal static void ApplyFurByIndex(CatModelSettings s, Mesh mesh, float seedOffset, Func<int, Color> furColor)
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
        internal sealed class FleshSkin
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

        // Couleurs de fourrure imposées à des plages de sommets (intérieur des oreilles, collerette…).
        // Les autres sommets gardent la couleur calculée d'après leur position.
        private sealed class FurMarks
        {
            private readonly List<(int from, int to, Color color)> ranges = new List<(int, int, Color)>();

            public void Add(int from, int to, Color color) => ranges.Add((from, to, color));

            public Color Get(int vertex, Func<Color> fallback)
            {
                foreach ((int from, int to, Color color) in ranges)
                {
                    if (vertex >= from && vertex < to) return color;
                }
                return fallback();
            }
        }

        // Valeur pseudo-aléatoire stable entre 0 et 1 (variation des touffes, identique à chaque reconstruction)
        private static float Hash01(int a, int b)
        {
            float x = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.547f;
            return x - Mathf.Floor(x);
        }

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
            int rings = d.LowPoly ? 6 : 12;
            var points = new Vector3[rings];
            var radii = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float t = i / (rings - 1f);
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

        internal static Color Opaque(Color c)
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

        // Style low-poly : maillages grossiers (facettes visibles), rendus par FleshFacet
        private static bool IsLowPoly(CatModelSettings s) => s.style == CatModelSettings.Style.Facets;

        private static MeshDraft NewDraft(CatModelSettings s, int submeshCount = 1)
        {
            return new MeshDraft(submeshCount) { LowPoly = IsLowPoly(s) };
        }

        // ---------- Découpe en parties (gameplay : une vie par partie) ----------

        // Torse « FleshBall » → FleshChest (au-dessus de chestSplit), FleshBelly, FleshPelvis (sous pelvisSplit),
        // d'après la hauteur du centre de chaque triangle
        internal static void SplitTorso(List<Mesh> meshes, CatModelSettings s)
        {
            Mesh torso = meshes.Find(mesh => mesh.name == "FleshBall");
            if (torso == null) return;
            meshes.Remove(torso);
            meshes.Add(Subset(torso, "FleshChest", c => c.y >= s.chestSplit));
            meshes.Add(Subset(torso, "FleshBelly", c => c.y < s.chestSplit && c.y > s.pelvisSplit));
            meshes.Add(Subset(torso, "FleshPelvis", c => c.y <= s.pelvisSplit));
            Object.DestroyImmediate(torso);
        }

        // Maillage sans sommets, avec ses sous-meshes (un par matériau du prefab)
        private static Mesh EmptyMesh(string name, int submeshes)
        {
            var mesh = new Mesh { name = name, subMeshCount = submeshes };
            return mesh;
        }

        // Patte « CatLegSkin » → CatLegThigh, CatLegShin, CatLegFoot : chaque triangle va à l'os qui le porte le plus
        // (hanche, genou, cheville). Les poids des os sont gardés : les 3 parties se plient ensemble.
        internal static void SplitLeg(List<Mesh> meshes)
        {
            Mesh leg = meshes.Find(mesh => mesh.name == "CatLegSkin");
            if (leg == null) return;
            BoneWeight[] w = leg.boneWeights;
            meshes.Remove(leg);
            string[] names = { "CatLegThigh", "CatLegShin", "CatLegFoot" };
            for (int bone = 0; bone < 3; bone++)
            {
                int b = bone;
                meshes.Add(Subset(leg, names[bone], null, (i0, i1, i2) => DominantBone(w, i0, i1, i2) == b));
            }
            Object.DestroyImmediate(leg);
        }

        private static int DominantBone(BoneWeight[] w, int i0, int i1, int i2)
        {
            if (w.Length == 0) return 0;
            var sum = new float[3];
            foreach (int i in new[] { i0, i1, i2 })
            {
                AddWeight(sum, w[i].boneIndex0, w[i].weight0);
                AddWeight(sum, w[i].boneIndex1, w[i].weight1);
                AddWeight(sum, w[i].boneIndex2, w[i].weight2);
            }
            return sum[0] >= sum[1] && sum[0] >= sum[2] ? 0 : sum[1] >= sum[2] ? 1 : 2;
        }

        private static void AddWeight(float[] sum, int bone, float weight)
        {
            if (bone == HipAnchorBone) bone = 0;   // haut de cuisse : compté avec la hanche
            if (bone >= 0 && bone < sum.Length) sum[bone] += weight;
        }

        // Nouveau mesh avec les triangles choisis (par le centre du triangle, ou par ses 3 sommets).
        // Garde couleurs, fourrure (UV1), veines (UV2), poids et poses des os. Tout dans le sous-mesh 0.
        private static Mesh Subset(Mesh source, string name, Func<Vector3, bool> byCenter,
                                   Func<int, int, int, bool> byCorners = null)
        {
            Vector3[] v = source.vertices;
            Color[] colors = source.colors;
            var fur = new List<Vector4>();
            var veins = new List<Vector4>();
            source.GetUVs(1, fur);
            source.GetUVs(2, veins);
            BoneWeight[] weights = source.boneWeights;

            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>();
            var nc = new List<Color>();
            var nf = new List<Vector4>();
            var nvein = new List<Vector4>();
            var nw = new List<BoneWeight>();
            var tris = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] t = source.GetTriangles(sub);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    bool keep = byCorners != null
                        ? byCorners(t[i], t[i + 1], t[i + 2])
                        : byCenter((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f);
                    if (!keep) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int old = t[i + k];
                        if (!map.TryGetValue(old, out int idx))
                        {
                            idx = nv.Count;
                            map[old] = idx;
                            nv.Add(v[old]);
                            if (colors.Length == v.Length) nc.Add(colors[old]);
                            if (fur.Count == v.Length) nf.Add(fur[old]);
                            if (veins.Count == v.Length) nvein.Add(veins[old]);
                            if (weights.Length == v.Length) nw.Add(weights[old]);
                        }
                        tris.Add(idx);
                    }
                }
            }

            var mesh = new Mesh { name = name };
            if (nv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(nv);
            if (nc.Count > 0) mesh.SetColors(nc);
            if (nf.Count > 0) mesh.SetUVs(1, nf);
            if (nvein.Count > 0) mesh.SetUVs(2, nvein);
            mesh.SetTriangles(tris, 0);
            if (nw.Count > 0)
            {
                mesh.boneWeights = nw.ToArray();
                mesh.bindposes = source.bindposes;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Low-poly : chaque triangle a ses propres sommets, d'une seule couleur (moyenne de ses coins) et d'une
        // seule normale → facettes unies et nettes. Les poids des os (patte) restent ceux de chaque coin.
        internal static void FlattenFaces(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Color[] colors = mesh.colors;
            var fur = new List<Vector4>();
            var veins = new List<Vector4>();
            mesh.GetUVs(1, fur);
            mesh.GetUVs(2, veins);
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;

            int subCount = mesh.subMeshCount;
            var tris = new int[subCount][];
            int total = 0;
            for (int s = 0; s < subCount; s++)
            {
                tris[s] = mesh.GetTriangles(s);
                total += tris[s].Length;
            }

            var nv = new Vector3[total];
            var nc = new Color[colors.Length > 0 ? total : 0];
            var nf = new List<Vector4>(fur.Count > 0 ? total : 0);
            var nvein = new List<Vector4>(veins.Count > 0 ? total : 0);
            var nw = new BoneWeight[weights.Length > 0 ? total : 0];
            var newTris = new int[subCount][];
            int n = 0;
            for (int s = 0; s < subCount; s++)
            {
                newTris[s] = new int[tris[s].Length];
                for (int i = 0; i + 2 < tris[s].Length; i += 3)
                {
                    int a = tris[s][i], b = tris[s][i + 1], c = tris[s][i + 2];
                    Color faceColor = nc.Length > 0 ? (colors[a] + colors[b] + colors[c]) / 3f : Color.white;
                    Vector4 faceFur = fur.Count > 0 ? (fur[a] + fur[b] + fur[c]) / 3f : Vector4.zero;
                    Vector4 faceVein = veins.Count > 0 ? (veins[a] + veins[b] + veins[c]) / 3f : Vector4.zero;
                    for (int k = 0; k < 3; k++)
                    {
                        int idx = tris[s][i + k];
                        nv[n] = v[idx];
                        if (nc.Length > 0) nc[n] = faceColor;
                        if (fur.Count > 0) nf.Add(faceFur);
                        if (veins.Count > 0) nvein.Add(faceVein);
                        if (nw.Length > 0) nw[n] = weights[idx];
                        newTris[s][i + k] = n;
                        n++;
                    }
                }
            }

            mesh.Clear();
            mesh.indexFormat = total > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = nv;
            if (nc.Length > 0) mesh.colors = nc;
            if (nf.Count > 0) mesh.SetUVs(1, nf);
            if (nvein.Count > 0) mesh.SetUVs(2, nvein);
            mesh.subMeshCount = subCount;
            for (int s = 0; s < subCount; s++) mesh.SetTriangles(newTris[s], s);
            if (nw.Length > 0)
            {
                mesh.boneWeights = nw;
                mesh.bindposes = bindposes;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // Shader du corps selon le style choisi (tous lisent les mêmes données de chair et de fourrure)
        private static void ApplyStyle(CatModelSettings s, Material material)
        {
            string name;
            switch (s.style)
            {
                case CatModelSettings.Style.Fur: name = "Custom/FleshFur"; break;
                case CatModelSettings.Style.Toon: name = "Custom/FleshToon"; break;
                case CatModelSettings.Style.Facets: name = "Custom/FleshFacet"; break;
                default: name = "Custom/FleshLit"; break;
            }
            Shader shader = Shader.Find(name);
            if (shader == null || material.shader == shader) return;
            material.shader = shader;
            EditorUtility.SetDirty(material);
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
            for (int channel = 0; channel < 4; channel++)
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
