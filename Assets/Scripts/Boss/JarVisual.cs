using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Visuel du boss « la Jarre aux âmes » : instance du modèle Assets/Models/Jar/jar.blend.
    //   Phase I       : le bol entier (Bowl).
    //   Phase II et + : le bol est remplacé par ses morceaux (Frag_P<n>_<k>), légèrement disjoints : des fentes
    //                   laissent voir la chair. Chaque morceau se détache à la phase n (3, 4 ou 5), tombe en
    //                   tournoyant, rebondit et reste posé à plat sur le sol (détaché du boss) ; une anse tombe
    //                   avec le morceau le plus proche. Le boss peut ramasser un morceau posé (FindShard / TakeShard).
    // Bras (os UpperArm, Forearm, Hand) : cinématique inverse à deux os vers une cible.
    //   - Par défaut, les mains restent plantées au sol ; quand le corps s'éloigne trop d'une main, elle se replante.
    //   - Reptation (Crawl) : tractions pénibles, une à la fois. La main de devant s'étire loin et agrippe le sol,
    //     le bras force (le bol tremble), puis le bol est traîné d'un coup ; un temps de souffle, et la main de
    //     derrière rattrape. Crawl renvoie le déplacement du corps : c'est la traction qui fait avancer le boss.
    //   - Une attaque peut prendre la main sur un bras (Override) : la main suit alors la cible donnée.
    // Calculs de rotation dans l'espace local des parents : reste juste quand le boss est retourné en miroir.
    // Repère : le modèle est tourné d'un demi-tour pour que son avant (+X dans Blender) soit l'avant du boss (+X).
    public sealed class JarVisual
    {
        public const int Front = 0;                     // bras côté joueur
        public const int Back = 1;

        private const float GapScale = 0.94f;           // morceaux disjoints (phase II+)
        private const float StepDuration = 0.24f;       // durée d'un pas de main
        private const float StepThreshold = 0.28f;      // × portée du bras : distance qui déclenche un pas
        private const float PalmLift = 0.07f;           // poignet au-dessus du sol (unités du modèle)
        private const float MaxExtend = 0.97f;          // le bras ne se tend jamais tout à fait (pas de bras raide)

        // Reptation : durées de base d'une traction (adaptées à la vitesse demandée)
        private const float CrawlReach = 0.34f;         // la main s'étire et agrippe (le bol glisse un peu, poussé)
        private const float CrawlPull = 0.5f;           // effort puis traction
        private const float CrawlRest = 0.18f;          // souffle
        private const float CrawlCreep = 0.12f;         // part de la distance faite pendant que la main s'étire
        private const float CrawlStride = 0.55f;        // × portée : distance d'une traction
        private const float CrawlMin = 0.25f;           // en dessous, il ne bouge pas

        private sealed class Fragment
        {
            public Transform T;
            public int BreakPhase;                      // phase (0 = I) à laquelle il se détache
            public Vector3 RestPos;
            public Quaternion RestRot;
            public Mesh Solid;                          // copie pleine (main, projectiles)
            public Vector3 Radial;                      // sens de l'épaisseur, repère du morceau
            public bool Fallen;
            public bool Loose;                          // détaché du boss (dans le monde)
            public bool Tracked;                        // inscrit dans GeneratedObjects (détruit avec le boss)
            public bool Resting;                        // posé à plat au sol
            public bool Taken;                          // ramassé par le boss
            public bool Instant;                        // à poser directement au sol (labo)
            public int Bounces;
            public float Settle;
            public Quaternion FlatFrom, FlatTo;         // se couche à plat
            public Vector3 Velocity;                    // monde
            public Vector3 Spin;                        // degrés/s
            public Renderer[] Renderers;
            public readonly List<Transform> Handles = new List<Transform>();
        }

        private sealed class Handle
        {
            public Transform T;
            public Transform Parent;
            public Vector3 RestPos;
            public Quaternion RestRot;
        }

        private sealed class Arm
        {
            public Transform Root, Upper, Fore, Hand;
            public Quaternion UpperRest, ForeRest, HandRest;
            public Quaternion HandRestModel;            // orientation de la main (paume au sol) dans le repère du modèle
            public Vector3 PlantModel;                  // poignet au repos, repère du modèle
            public Vector3 PalmLocal;                   // centre de la paume, repère de l'os Hand
            public float Seed;
            // État
            public Vector3 Planted;                     // monde
            public bool Stepping;
            public Vector3 StepFrom, StepTo;
            public float StepT;
            public float StepDur = StepDuration;
            public float StepArc = 0.18f;               // × portée : hauteur de l'arc du pas
            public bool CrawlStep;                      // pas de reptation (agrippe en arrivant)
            public float FlatBlend = 1f;                // FlatHand lissé (pas de saut du poignet)
            public bool Override;
            public Vector3 OverrideTarget;
            public float OverrideBlend;
            public float FlatHand = 1f;                 // 1 : paume à plat (au sol, frappe) ; 0 : suit l'avant-bras
            public Vector3 Current;                     // cible finale du poignet (monde)
            public bool Initialized;
            public GameObject Held;
        }

        private readonly GameObject root;
        private readonly Transform model;
        private readonly GameObject bowl;
        private readonly Transform flesh;
        private readonly Vector3 fleshRestScale;
        private readonly List<Fragment> fragments = new List<Fragment>();
        private readonly List<Handle> handles = new List<Handle>();
        private readonly Arm[] arms = new Arm[2];
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Mesh> shardMeshes = new List<Mesh>();
        private readonly HashSet<Renderer> loose = new HashSet<Renderer>();     // morceaux tombés : pas de flash
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly GeneratedObjects generated;
        private Material potteryMaterial;
        private int shownPhase = -1;
        private float gap = 1f;
        private Vector3 fleshLocal;                     // centre du bol (repère du modèle)
        private float heave;                            // grand souffle après un effort (0..1)

        // Reptation
        private enum CrawlStage { Idle, Reach, Pull, Rest }
        private CrawlStage crawl;
        private float crawlT, crawlDur, crawlScale = 1f;
        private int crawlLead;
        private float crawlDir, crawlDist, crawlDone;
        private bool crawlGrip, crawlScrape;
        private Vector3 crawlGripPos;

        // Inclinaison voulue par la reptation (monde : + = vers +X) et tremblement d'effort (0..1)
        public float CrawlLean { get; private set; }
        public float CrawlStrain { get; private set; }
        public bool Crawling => crawl != CrawlStage.Idle;

        public Transform Root => root.transform;
        // Centre du bol (cœur de chair)
        public Transform Core => flesh != null ? flesh : root.transform;
        public Material PotteryMaterial => potteryMaterial;

        public JarVisual(GameObject source, Transform parent, GeneratedObjects generated)
        {
            this.generated = generated;
            root = new GameObject("Jar");
            generated.Track(root);
            root.transform.SetParent(parent, false);
            GameObject inst = Object.Instantiate(source, root.transform, false);
            inst.name = "Model";
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model = inst.transform;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            Transform ground = model.Find("Ground");
            if (ground != null) GeneratedObjects.SafeDestroy(ground.gameObject);

            Transform bowlT = model.Find("Bowl");
            bowl = bowlT != null ? bowlT.gameObject : null;
            if (bowl != null)
            {
                Renderer br = bowl.GetComponent<Renderer>();
                if (br != null) potteryMaterial = br.sharedMaterial;
            }
            flesh = model.Find("FleshCore");
            fleshRestScale = flesh != null ? flesh.localScale : Vector3.one;
            fleshLocal = flesh != null ? flesh.localPosition : Vector3.zero;

            foreach (Transform child in model)
            {
                string n = child.name;
                if (n.StartsWith("Frag_P") && n.Length > 6 && char.IsDigit(n[6]))
                {
                    Fragment f = new Fragment
                    {
                        T = child,
                        BreakPhase = (n[6] - '0') - 1,
                        RestPos = child.localPosition,
                        RestRot = child.localRotation,
                    };
                    // Épaisseur : à peu près du centre du bol vers le morceau (paroi d'un bol presque rond)
                    Vector3 radial = child.localPosition - fleshLocal;
                    f.Radial = Quaternion.Inverse(child.localRotation) * (radial.sqrMagnitude > 1e-6f ? radial.normalized : Vector3.up);
                    MeshFilter mf = child.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        f.Solid = BuildSolid(mf.sharedMesh);
                        if (f.Solid != null) shardMeshes.Add(f.Solid);
                    }
                    fragments.Add(f);
                }
                else if (n.StartsWith("Handle_"))
                {
                    handles.Add(new Handle { T = child, Parent = child.parent, RestPos = child.localPosition, RestRot = child.localRotation });
                }
            }
            // Chaque anse appartient au morceau le plus proche de son centre
            foreach (Handle h in handles)
            {
                Vector3 c = Center(h.T);
                Fragment best = null;
                float bestD = float.MaxValue;
                foreach (Fragment f in fragments)
                {
                    float d = (Center(f.T) - c).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = f; }
                }
                if (best != null) best.Handles.Add(h.T);
            }

            arms[Front] = BuildArm("Arm_Front", 0f);
            arms[Back] = BuildArm("Arm_Back", 2.3f);
            renderers.AddRange(inst.GetComponentsInChildren<Renderer>(true));
        }

        // Copie d'un morceau en un seul sous-maillage, rendu avec le seul matériau de la poterie :
        // face peinte = UV d'origine ; intérieur et tranches = UV (-1, -1), que FacetTextured rend sans texture
        // (couleur des sommets seule). Sans ça, un projectile à un matériau ne montrerait que la face peinte (sans épaisseur).
        private Mesh BuildSolid(Mesh src)
        {
            if (!src.isReadable) return null;
            Vector3[] v = src.vertices;
            Vector3[] nrm = src.normals;
            Vector2[] uv = src.uv;
            Color[] col = src.colors;
            var pv = new List<Vector3>();
            var pn = new List<Vector3>();
            var puv = new List<Vector2>();
            var pc = new List<Color>();
            var tris = new List<int>();
            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                foreach (int i in src.GetTriangles(sub))
                {
                    tris.Add(pv.Count);
                    pv.Add(v[i]);
                    pn.Add(nrm.Length > i ? nrm[i] : Vector3.up);
                    puv.Add(sub == 0 && uv.Length > i ? uv[i] : new Vector2(-1f, -1f));
                    pc.Add(col.Length > i ? col[i] : Color.white);
                }
            }
            Mesh m = new Mesh { name = src.name + "_Solid" };
            if (pv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pv);
            m.SetNormals(pn);
            m.SetUVs(0, puv);
            m.SetColors(pc);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            generated.Track(m);
            return m;
        }

        private Arm BuildArm(string name, float seed)
        {
            Transform a = model.Find(name);
            if (a == null) return null;
            Arm arm = new Arm { Root = a, Seed = seed };
            foreach (Transform t in a.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "UpperArm") arm.Upper = t;
                else if (t.name == "Forearm") arm.Fore = t;
                else if (t.name == "Hand") arm.Hand = t;
            }
            if (arm.Upper == null || arm.Fore == null || arm.Hand == null) return null;
            arm.UpperRest = arm.Upper.localRotation;
            arm.ForeRest = arm.Fore.localRotation;
            arm.HandRest = arm.Hand.localRotation;
            arm.HandRestModel = a.localRotation * arm.UpperRest * arm.ForeRest * arm.HandRest;
            arm.PlantModel = model.InverseTransformPoint(arm.Hand.position);
            Transform tip = arm.Hand.childCount > 0 ? arm.Hand.GetChild(0) : null;
            Vector3 palm = tip != null ? Vector3.Lerp(arm.Hand.position, tip.position, 0.5f) : arm.Hand.position;
            arm.PalmLocal = arm.Hand.InverseTransformPoint(palm);
            return arm;
        }

        // ---------- Commandes des bras (monde) ----------

        // Prend la main sur un bras : le poignet va vers 'world'. flatHand 1 = paume à plat, 0 = suit l'avant-bras.
        public void ArmTo(int index, Vector3 world, float flatHand = 1f)
        {
            Arm a = arms[index];
            if (a == null) return;
            a.Override = true;
            a.OverrideTarget = world;
            a.FlatHand = flatHand;
        }

        // Rend le bras à la marche : la main se replante au sol
        public void ArmRelease(int index)
        {
            Arm a = arms[index];
            if (a == null || !a.Override) return;
            a.Override = false;
            a.FlatHand = 1f;
            // Repart d'où est la main maintenant
            a.Stepping = false;
            a.CrawlStep = false;
            a.Planted = a.Current;
        }

        public Vector3 HandPosition(int index)
        {
            Arm a = arms[index];
            return a != null ? a.Hand.position : root.transform.position;
        }

        public Vector3 PalmPosition(int index)
        {
            Arm a = arms[index];
            return a != null ? a.Hand.TransformPoint(a.PalmLocal) : root.transform.position;
        }

        public Vector3 ShoulderPosition(int index)
        {
            Arm a = arms[index];
            return a != null ? a.Upper.position : root.transform.position;
        }

        // Portée du bras (épaule → poignet tendu), en unités du monde
        public float Reach(int index)
        {
            Arm a = arms[index];
            if (a == null) return 1f;
            return (a.Fore.position - a.Upper.position).magnitude + (a.Hand.position - a.Fore.position).magnitude;
        }

        // Où la main se poserait au repos (monde, au sol)
        public Vector3 RestPlant(int index, float groundY)
        {
            Arm a = arms[index];
            if (a == null) return root.transform.position;
            Vector3 p = model.TransformPoint(a.PlantModel);
            p.y = groundY + PalmLift * Mathf.Abs(model.lossyScale.y);
            return p;
        }

        // Un objet tenu dans la main (caillou, tesson) : visuel seulement, retiré par Release
        public void Hold(int index, Mesh mesh, Material material, float size)
        {
            Arm a = arms[index];
            if (a == null) return;
            ReleaseHeld(index);
            GameObject go = new GameObject("Held");
            generated.Track(go);
            go.transform.SetParent(a.Hand, false);
            go.transform.localPosition = a.PalmLocal;
            go.transform.localRotation = Random.rotation;
            Vector3 ls = a.Hand.lossyScale;
            float s = size / Mathf.Max(0.0001f, Mathf.Abs(ls.x));
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            a.Held = go;
        }

        public void ReleaseHeld(int index)
        {
            Arm a = arms[index];
            if (a == null || a.Held == null) return;
            GeneratedObjects.SafeDestroy(a.Held);
            a.Held = null;
        }

        // Un morceau de bol au hasard (copie pleine : pluie de pierres de la phase V)
        public Mesh RandomShardMesh()
        {
            return shardMeshes.Count > 0 ? shardMeshes[Random.Range(0, shardMeshes.Count)] : null;
        }

        // Morceau posé au sol le plus proche de 'near' (distance à plat), à moins de 'maxDist', sauf 'exclude' ; -1 : aucun
        public int FindShard(Vector3 near, float maxDist, int exclude = -1)
        {
            int best = -1;
            float bestD = maxDist;
            for (int i = 0; i < fragments.Count; i++)
            {
                Fragment f = fragments[i];
                if (i == exclude || !f.Resting || f.Taken || f.Solid == null) continue;
                float d = Flat(Center(f) - near).magnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public bool ShardAvailable(int id) => id >= 0 && id < fragments.Count && fragments[id].Resting && !fragments[id].Taken;
        public Vector3 ShardPosition(int id) => Center(fragments[id]);
        public Mesh ShardMesh(int id) => fragments[id].Solid;

        // La main le prend : il disparaît du sol (la main tient alors sa copie, voir Hold)
        public void TakeShard(int id)
        {
            if (!ShardAvailable(id)) return;
            fragments[id].Taken = true;
            fragments[id].T.gameObject.SetActive(false);
        }

        // ---------- Phases ----------

        // Montre la phase (0 = I). instant : sans chute ni transition (aperçu éditeur, labo).
        public void SetPhase(int phase, bool instant)
        {
            if (phase == shownPhase && !instant) return;
            shownPhase = phase;
            if (bowl != null) bowl.SetActive(phase == 0 || fragments.Count == 0);
            if (instant) gap = phase >= 1 ? GapScale : 1f;
            foreach (Fragment f in fragments)
            {
                bool broken = phase >= f.BreakPhase;
                if (!broken)
                {
                    if (f.Fallen) Restore(f);
                    f.T.gameObject.SetActive(phase >= 1);
                }
                else if (!f.Fallen)
                {
                    Break(f, instant || phase == 0);
                }
                else if (instant && !Application.isPlaying)
                {
                    f.T.gameObject.SetActive(false);
                }
            }
        }

        // groundY : hauteur du sol (monde) ; dt 0 = aperçu (mains posées directement)
        public void Tick(float dt, float time, float flash, float groundY, Vector3 bodyVelocity)
        {
            if (dt > 0f)
            {
                gap = Mathf.MoveTowards(gap, shownPhase >= 1 ? GapScale : 1f, dt * 0.2f);
            }
            foreach (Fragment f in fragments)
            {
                if (!f.Fallen) f.T.localScale = Vector3.one * gap;
                else if (!f.Taken && f.Loose && f.T.gameObject.activeSelf && (dt > 0f || f.Instant)) Fall(f, dt, groundY);
            }

            // La chair respire (deux ondes : moins mécanique) ; grand souffle après une traction
            if (flesh != null)
            {
                float b = Mathf.Sin(time * 1.4f) * 0.018f + Mathf.Sin(time * 0.63f + 1f) * 0.01f;
                if (heave < 1f)
                {
                    heave = Mathf.Min(1f, heave + dt / 0.7f);
                    b += Mathf.Sin(heave * Mathf.PI) * 0.06f;
                }
                b -= CrawlStrain * 0.03f;               // se contracte pendant l'effort
                flesh.localScale = new Vector3(fleshRestScale.x * (1f + b), fleshRestScale.y * (1f + b), fleshRestScale.z * (1f - b * 0.5f));
            }

            UpdateArms(dt, time, groundY, bodyVelocity);

            block.SetFloat("_Flash", Mathf.Clamp01(flash));
            foreach (Renderer r in renderers)
            {
                if (r != null && !loose.Contains(r)) r.SetPropertyBlock(block);
            }
        }

        // ---------- Reptation ----------

        // À appeler à chaque image avant Tick. wantDx : distance (monde, x) jusqu'au but (0 : rester sur place) ;
        // speed : vitesse moyenne voulue. Renvoie le déplacement du corps pour cette image (x, monde).
        public float Crawl(float dt, float wantDx, float speed, float groundY)
        {
            if (dt <= 0f) return 0f;
            float dx = 0f;
            float lean = 0f;
            CrawlStrain = 0f;
            switch (crawl)
            {
                case CrawlStage.Idle:
                {
                    int lead = LeadArm(Mathf.Sign(wantDx));
                    Arm a = arms[lead];
                    Arm o = arms[1 - lead];
                    if (a == null || Mathf.Abs(wantDx) < Reach(lead) * 0.08f) break;
                    if (a.Override || a.Stepping || (o != null && o.Stepping && !o.Override)) break;
                    float r = Reach(lead);
                    crawlDir = Mathf.Sign(wantDx);
                    crawlLead = lead;
                    crawlDist = Mathf.Min(Mathf.Abs(wantDx), r * CrawlStride);
                    // Même vitesse moyenne que demandé : tractions plus rapides et plus violentes si besoin
                    float natural = crawlDist / (CrawlReach + CrawlPull + CrawlRest);
                    crawlScale = Mathf.Clamp(natural / Mathf.Max(0.01f, speed), 0.45f, 1.6f);
                    // La main s'étire loin devant : après la traction, elle sera à sa place de repos
                    Vector3 to = RestPlant(lead, groundY) + Vector3.right * crawlDir * crawlDist;
                    StartStep(a, to, CrawlReach * crawlScale, 0.3f);
                    a.CrawlStep = true;
                    crawl = CrawlStage.Reach;
                    crawlT = 0f;
                    crawlDone = 0f;
                    break;
                }
                case CrawlStage.Reach:
                {
                    lean = crawlDir * 0.25f;
                    if (arms[crawlLead] == null || arms[crawlLead].Override) { crawl = CrawlStage.Idle; break; }
                    // La main de derrière pousse : le bol glisse un peu pendant que l'autre s'étire
                    crawlT += dt;
                    float creep = Smooth01(crawlT / (CrawlReach * crawlScale)) * CrawlCreep * crawlDist;
                    dx = (creep - crawlDone) * crawlDir;
                    crawlDone = creep;
                    if (!arms[crawlLead].Stepping)
                    {
                        // Agrippe le sol
                        if (OnDust != null) OnDust(arms[crawlLead].Planted, 0.4f);
                        crawl = CrawlStage.Pull;
                        crawlT = 0f;
                        crawlDur = CrawlPull * crawlScale;
                        crawlScrape = false;
                    }
                    break;
                }
                case CrawlStage.Pull:
                {
                    if (arms[crawlLead] == null || arms[crawlLead].Override) { crawl = CrawlStage.Idle; break; }
                    crawlT += dt;
                    float u = Mathf.Clamp01(crawlT / crawlDur);
                    float goal = (CrawlCreep + (1f - CrawlCreep) * PullCurve(u)) * crawlDist;
                    dx = (goal - crawlDone) * crawlDir;
                    crawlDone = goal;
                    if (u < StrainEnd)
                    {
                        // Il force : le bras tire, le bol résiste et tremble, penché en arrière
                        CrawlStrain = Mathf.Clamp01(u / StrainEnd * 1.5f);
                        lean = -crawlDir * 0.5f;
                    }
                    else
                    {
                        // Le bol cède d'un coup et racle le sol
                        if (!crawlScrape)
                        {
                            crawlScrape = true;
                            if (OnDust != null) OnDust(root.transform.position, 1f);
                        }
                        lean = crawlDir;
                    }
                    if (u >= 1f)
                    {
                        crawl = CrawlStage.Rest;
                        crawlT = 0f;
                        crawlDur = CrawlRest * crawlScale;
                        heave = 0f;
                    }
                    break;
                }
                case CrawlStage.Rest:
                    crawlT += dt;
                    bool trailing = arms[1 - crawlLead] != null && arms[1 - crawlLead].Stepping;
                    if (crawlT >= crawlDur && !trailing) crawl = CrawlStage.Idle;
                    break;
            }
            CrawlLean = Mathf.MoveTowards(CrawlLean, lean, dt * 6f);
            return dx;
        }

        // Appelé pour la poussière : main qui agrippe, bol qui racle, morceau qui touche le sol (position, force 0..1)
        public System.Action<Vector3, float> OnDust;

        private const float StrainEnd = 0.3f;           // part de la traction passée à forcer

        // Presque rien pendant l'effort, puis le corps est traîné d'un coup et ralentit en frottant
        private static float PullCurve(float u)
        {
            if (u < StrainEnd) return 0.05f * (u / StrainEnd);
            float v = (u - StrainEnd) / (1f - StrainEnd);
            return 0.05f + 0.95f * Mathf.Sin(v * Mathf.PI * 0.5f);
        }

        private static float Smooth01(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        // Bras du côté où il va
        private int LeadArm(float dir)
        {
            float best = float.MinValue;
            int lead = Front;
            for (int i = 0; i < arms.Length; i++)
            {
                if (arms[i] == null) continue;
                float x = (model.TransformPoint(arms[i].PlantModel).x - root.transform.position.x) * dir;
                if (x > best) { best = x; lead = i; }
            }
            return lead;
        }

        private static void StartStep(Arm a, Vector3 to, float duration, float arc)
        {
            a.Stepping = true;
            a.StepT = 0f;
            a.StepFrom = a.Planted;
            a.StepTo = to;
            a.StepDur = Mathf.Max(0.05f, duration);
            a.StepArc = arc;
            a.CrawlStep = false;
        }

        // ---------- Bras : marche et cinématique inverse ----------

        private void UpdateArms(float dt, float time, float groundY, Vector3 bodyVelocity)
        {
            for (int i = 0; i < arms.Length; i++)
            {
                Arm a = arms[i];
                if (a == null) continue;
                Arm other = arms[1 - i];
                Vector3 ideal = RestPlant(i, groundY);
                float reach = Reach(i);

                if (!a.Initialized || dt <= 0f)
                {
                    a.Planted = ideal;
                    a.Current = ideal;
                    a.Stepping = false;
                    a.Initialized = true;
                }

                Vector3 walk = a.Planted;
                if (a.Stepping)
                {
                    a.StepT += dt / a.StepDur;
                    float u = Mathf.Clamp01(a.StepT);
                    // Pas de reptation : la main part lentement, s'étire puis s'abat (accélère jusqu'au sol)
                    float e = a.CrawlStep ? u * u : u * u * (3f - 2f * u);
                    float arcU = a.CrawlStep ? Mathf.Sin(Mathf.Pow(u, 0.7f) * Mathf.PI) : Mathf.Sin(u * Mathf.PI);
                    walk = Vector3.Lerp(a.StepFrom, a.StepTo, e) + Vector3.up * arcU * reach * a.StepArc;
                    if (u >= 1f)
                    {
                        a.Stepping = false;
                        a.CrawlStep = false;
                        a.Planted = a.StepTo;
                        walk = a.Planted;
                    }
                }
                else if (!a.Override)
                {
                    float far = (Flat(a.Planted) - Flat(ideal)).magnitude;
                    // Main relâchée en l'air (fin d'une attaque) : elle redescend se poser
                    bool lifted = a.Planted.y - ideal.y > reach * 0.06f;
                    // Pendant une traction, les mains restent agrippées (sauf si le bras n'en peut plus)
                    bool held = crawl == CrawlStage.Reach || crawl == CrawlStage.Pull || (crawl == CrawlStage.Rest && i == crawlLead);
                    float limit = held ? reach * 0.85f : reach * StepThreshold;
                    bool otherBusy = other != null && other.Stepping && !other.Override;
                    if (lifted || (far > limit && (!otherBusy || far > reach * 0.7f)))
                    {
                        // La main de derrière rattrape en traînant (arc bas)
                        bool trail = crawl == CrawlStage.Rest && i != crawlLead;
                        Vector3 lead = new Vector3(Mathf.Clamp(bodyVelocity.x, -3f, 3f), 0f, 0f) * 0.15f;
                        Vector3 to = ideal + lead;
                        to.y = ideal.y;
                        StartStep(a, to, trail ? StepDuration * crawlScale : (lifted ? 0.2f : StepDuration), trail ? 0.1f : 0.18f);
                    }
                }

                // Attaque : la main suit la cible, avec un fondu à la prise et au retour
                a.OverrideBlend = dt > 0f
                    ? Mathf.MoveTowards(a.OverrideBlend, a.Override ? 1f : 0f, dt * (a.Override ? 12f : 6f))
                    : (a.Override ? 1f : 0f);
                // (au retour, ArmRelease a replanté la main où elle était : pas de saut)
                Vector3 target = a.Override ? Vector3.Lerp(walk, a.OverrideTarget, a.OverrideBlend) : walk;
                // Petit frémissement des doigts au repos
                target += Vector3.up * Mathf.Sin(time * 2.1f + a.Seed) * 0.02f * reach * (1f - a.OverrideBlend);
                a.Current = target;
                // Poignet : passe en douceur de la paume à plat au prolongement de l'avant-bras
                float flatGoal = a.Override ? a.FlatHand : 1f;
                a.FlatBlend = dt > 0f ? Mathf.MoveTowards(a.FlatBlend, flatGoal, dt * 5f) : flatGoal;
                Solve(a, target, a.FlatBlend);
            }
        }

        // Cinématique inverse à deux os : le coude plie vers le haut et du côté où va la main
        private void Solve(Arm a, Vector3 target, float flat)
        {
            a.Upper.localRotation = a.UpperRest;
            a.Fore.localRotation = a.ForeRest;
            a.Hand.localRotation = a.HandRest;

            Vector3 s = a.Upper.position;
            Vector3 e0 = a.Fore.position;
            Vector3 w0 = a.Hand.position;
            float l1 = (e0 - s).magnitude;
            float l2 = (w0 - e0).magnitude;
            Vector3 toT = target - s;
            // Jamais tout à fait tendu : le coude garde un pli (un bras raide a l'air figé)
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 0.001f, (l1 + l2) * MaxExtend);
            Vector3 n = toT.sqrMagnitude > 1e-8f ? toT.normalized : Vector3.down;
            // Côté de la main (à défaut : côté de l'épaule par rapport au centre du bol)
            Vector3 side = Flat(target - s);
            if (side.sqrMagnitude < 1e-4f) side = Flat(s - root.transform.position);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            Vector3 pole = Vector3.up + side * 0.6f;
            Vector3 m = pole - n * Vector3.Dot(pole, n);
            m = m.sqrMagnitude > 1e-6f ? m.normalized : Vector3.up;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = s + n * (l1 * cosA) + m * (l1 * sinA);
            Vector3 wrist = s + n * d;

            RotateBone(a.Upper, e0 - s, elbow - s);
            RotateBone(a.Fore, a.Hand.position - a.Fore.position, wrist - a.Fore.position);

            // Paume : à plat (orientation du repos dans le repère du modèle), ou dans le prolongement de l'avant-bras
            if (flat > 0f)
            {
                Quaternion parent = a.Root.localRotation * a.Upper.localRotation * a.Fore.localRotation;
                Quaternion flatLocal = Quaternion.Inverse(parent) * a.HandRestModel;
                a.Hand.localRotation = Quaternion.Slerp(a.Hand.localRotation, flatLocal, flat);
            }
        }

        // Tourne l'os pour que 'from' (monde) devienne 'to' (monde), dans le repère local de son parent
        private static void RotateBone(Transform bone, Vector3 from, Vector3 to)
        {
            Transform p = bone.parent;
            Vector3 f = p.InverseTransformVector(from);
            Vector3 t = p.InverseTransformVector(to);
            if (f.sqrMagnitude < 1e-10f || t.sqrMagnitude < 1e-10f) return;
            bone.localRotation = Quaternion.FromToRotation(f, t) * bone.localRotation;
        }

        // ---------- Morceaux ----------

        // Se détache : quitte le boss (reste dans l'arène), projeté vers l'extérieur en tournoyant.
        // instant : posé directement au sol (labo) ; hors Play, simplement caché.
        private void Break(Fragment f, bool instant)
        {
            f.Fallen = true;
            foreach (Transform h in f.Handles) h.SetParent(f.T, true);
            if (instant && !Application.isPlaying)
            {
                f.T.gameObject.SetActive(false);
                return;
            }
            f.T.gameObject.SetActive(true);
            f.Renderers = f.T.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in f.Renderers)
            {
                r.SetPropertyBlock(null);               // plus de flash quand le boss est touché
                loose.Add(r);
            }
            Vector3 outward = Center(f) - root.transform.position;
            outward.y = 0f;
            outward.z *= 0.4f;                          // reste près du plan de jeu
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.right;

            f.T.SetParent(null, true);
            f.Loose = true;
            if (!f.Tracked)
            {
                generated.Track(f.T.gameObject);        // détruit avec le boss
                f.Tracked = true;
            }
            f.Resting = f.Taken = false;
            f.Bounces = 0;
            f.Settle = 0f;
            if (instant)
            {
                float s = Mathf.Abs(model.lossyScale.y);
                f.T.position = root.transform.position + outward * Random.Range(0.9f, 2.2f) * s;
                f.Instant = true;
                return;
            }
            f.Velocity = outward * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(3f, 5.5f);
            f.Spin = Random.onUnitSphere * Random.Range(180f, 420f);
        }

        // Chute, rebonds, puis se couche à plat et ne bouge plus
        private void Fall(Fragment f, float dt, float groundY)
        {
            if (f.Resting) return;
            if (f.Instant)
            {
                f.Instant = false;
                f.T.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * FlatRotation(f);
                SnapToGround(f, groundY);
                f.Resting = true;
                return;
            }
            if (f.Settle <= 0f)
            {
                f.Velocity += Physics.gravity * dt;
                f.T.position += f.Velocity * dt;
                f.T.rotation = Quaternion.Euler(f.Spin * dt) * f.T.rotation;
                float under = groundY - Bottom(f);
                if (under > 0f)
                {
                    f.T.position += Vector3.up * under;
                    if (f.Velocity.y < 0f)
                    {
                        if (f.Bounces == 0 && OnDust != null) OnDust(Center(f), 0.25f);
                        f.Bounces++;
                        f.Velocity.y = -f.Velocity.y * 0.35f;
                        f.Velocity.x *= 0.55f;
                        f.Velocity.z *= 0.55f;
                        f.Spin *= 0.45f;
                        if (f.Bounces >= 3 || f.Velocity.y < 1.2f)
                        {
                            f.Settle = 0.0001f;
                            f.FlatFrom = f.T.rotation;
                            f.FlatTo = FlatRotation(f);
                        }
                    }
                }
                return;
            }
            // Se couche en glissant un peu
            f.Settle += dt * 5f;
            float u = Mathf.Clamp01(f.Settle);
            f.T.rotation = Quaternion.Slerp(f.FlatFrom, f.FlatTo, u * u * (3f - 2f * u));
            f.Velocity = Vector3.MoveTowards(new Vector3(f.Velocity.x, 0f, f.Velocity.z), Vector3.zero, dt * 8f);
            f.T.position += f.Velocity * dt;
            SnapToGround(f, groundY);
            if (u >= 1f) f.Resting = true;
        }

        // Orientation où l'épaisseur du morceau est verticale (posé à plat, creux en haut ou en bas)
        private static Quaternion FlatRotation(Fragment f)
        {
            Vector3 radial = f.T.TransformVector(f.Radial);
            if (radial.sqrMagnitude < 1e-8f) return f.T.rotation;
            radial.Normalize();
            Vector3 up = radial.y >= 0f ? Vector3.up : Vector3.down;
            return Quaternion.FromToRotation(radial, up) * f.T.rotation;
        }

        private static void SnapToGround(Fragment f, float groundY)
        {
            Vector3 p = f.T.position;
            p.y += groundY - Bottom(f);
            p.z = Mathf.Clamp(p.z, -1.5f, 1.5f);
            f.T.position = p;
        }

        private static float Bottom(Fragment f)
        {
            float y = float.MaxValue;
            if (f.Renderers != null)
            {
                foreach (Renderer r in f.Renderers)
                {
                    if (r != null) y = Mathf.Min(y, r.bounds.min.y);
                }
            }
            return y < float.MaxValue ? y : f.T.position.y;
        }

        private void Restore(Fragment f)
        {
            f.Fallen = false;
            f.Resting = f.Taken = f.Instant = false;
            f.Settle = 0f;
            f.Bounces = 0;
            if (f.Renderers != null)
            {
                foreach (Renderer r in f.Renderers) loose.Remove(r);
            }
            if (f.Loose)
            {
                f.T.SetParent(model, false);
                f.Loose = false;
            }
            f.T.localPosition = f.RestPos;
            f.T.localRotation = f.RestRot;
            f.T.localScale = Vector3.one * gap;
            foreach (Handle h in handles)
            {
                if (!f.Handles.Contains(h.T)) continue;
                h.T.SetParent(h.Parent, false);
                h.T.localPosition = h.RestPos;
                h.T.localRotation = h.RestRot;
                h.T.localScale = Vector3.one;
            }
        }

        private static Vector3 Center(Fragment f)
        {
            if (f.Renderers == null) return Center(f.T);
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in f.Renderers)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b.center : f.T.position;
        }

        private static Vector3 Center(Transform t)
        {
            Renderer r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
