using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Visuel du corps, fait de la chair du Gardien (modèles : Prefabs/Player, construits par
    // Parry Everything > Reconstruire les modèles du chat). L'état se lit dessus (pas de barre de vie).
    //  - Boule : la chair recouvre entièrement l'âme et roule.
    //  - Debout : la même boule, portée en hauteur par 2 grandes pattes de chat articulées.
    //    Marche réglée sur un cycle : chaque pied recule exactement à la vitesse du sol (il ne patine pas),
    //    puis revient en arc ; les 2 pattes sont en opposition. Le genou se plie par cinématique inverse.
    //    Le corps monte et redescend avec le cycle, bascule vers la patte d'appui, penche dans le mouvement ;
    //    il s'écrase en retombant d'un saut. Démarrage et arrêt fondus.
    // Abîmée, la chair vire à vif (_Damage) et tremble ; une parade réussie la fait flasher (_Flash).
    public class CatBody : MonoBehaviour
    {
        [Header("Modèles")]
        [SerializeField] private GameObject fleshBallPrefab;
        [SerializeField] private GameObject legPrefab;

        [Header("Placement")]
        [SerializeField] private float ballHeight = -0.08f;       // centre de la boule au sol, par rapport au joueur
        [SerializeField] private float standingBallHeight = 0.72f; // centre de la boule portée par les pattes
        [SerializeField] private float hipHeight = 0.3f;          // attache des pattes : assez bas pour que les genoux plient
        [SerializeField] private float standingTilt = 22f;        // debout, la boule penche vers l'avant (degrés)
        [SerializeField] private float legSplay = 10f;            // pattes un peu écartées vers l'extérieur (degrés)
        [SerializeField] private float morphSpeed = 3f;           // boule ↔ debout (par seconde)
        [SerializeField] private float turnSpeed = 900f;          // demi-tour du corps (degrés par seconde)

        [Header("Pattes")]
        // Longueurs cuisse / jarret : lues sur le prefab CatLeg (CatLegRig), réglées dans CatModelSettings
        [SerializeField] private float ankleHeight = 0.07f;       // cheville au-dessus du sol (épaisseur de la patte)

        [Header("Marche")]
        // Grands pas lents : plus le pas est long, moins il y en a par seconde à vitesse égale
        [SerializeField] private float strideMin = 0.9f;          // longueur d'un pas à vitesse lente
        [SerializeField] private float stridePerSpeed = 0.12f;    // le pas s'allonge avec la vitesse...
        [SerializeField] private float strideMax = 1.4f;          // ...dans la limite de la portée des pattes
        [SerializeField] private float maxReach = 0.72f;          // le pied ne s'écarte pas plus de la hanche
        [Range(0.5f, 0.8f)] [SerializeField] private float dutyFactor = 0.5f;   // marche : part du cycle pied au sol
        [SerializeField] private float stepHeight = 0.25f;        // hauteur de l'arc du pied
        [SerializeField] private float walkFade = 5f;             // démarrage / arrêt de la démarche (par seconde)
        // Accroupi en marchant : la hanche descend, la patte porte plus loin, le pas s'allonge
        [SerializeField] private float walkCrouch = 0.24f;
        [SerializeField] private float runCrouch = 0.3f;

        [Header("Course")]
        // En courant, les pieds restent moins au sol (phase de vol) : cycle plus lent, corps qui bondit
        [Range(0.3f, 0.8f)] [SerializeField] private float runDutyFactor = 0.3f;
        [SerializeField] private float runBlendFrom = 3.8f;       // vitesse où la course commence à remplacer la marche...
        [SerializeField] private float runBlendTo = 5.5f;         // ...et où elle l'a remplacée
        [SerializeField] private float runStepHeight = 0.34f;
        [SerializeField] private float runRise = 0.16f;           // bond du corps à chaque foulée

        [Header("Corps qui suit les pas")]
        [SerializeField] private float stepRise = 0.11f;          // le corps monte au milieu de chaque pas
        [SerializeField] private float landImpact = 1.2f;         // écrasement en retombant d'un saut
        [SerializeField] private float bodySpringStiffness = 120f;
        [SerializeField] private float bodySpringDamping = 12f;
        [SerializeField] private float squash = 1.2f;            // déformation de la boule à l'impact
        [SerializeField] private float runLean = 4f;              // degrés de penché par unité de vitesse
        [SerializeField] private float stepPitch = 10f;           // la boule bascule vers la patte d'appui
        [SerializeField] private float breathing = 0.012f;        // à l'arrêt, le corps respire
        [SerializeField] private float oneLegHop = 2.5f;          // une seule patte : chaque pas soulève le corps bien plus

        [Header("Ressenti")]
        [Range(0f, 1f)] [SerializeField] private float shakeBelow = 0.35f;   // tremble sous cette part de vie
        [SerializeField] private float parryFlashTime = 0.1f;

        [Header("Lumière du corps")]
        // L'âme éclairait le chat ; recouverte de chair, c'est cette lumière douce (côté caméra) qui le rend lisible
        [SerializeField] private Color fillColor = new Color(0.75f, 0.68f, 0.85f);
        [SerializeField] private float fillIntensity = 1.6f;
        [SerializeField] private float fillRange = 3f;
        [SerializeField] private Vector3 fillOffset = new Vector3(0.3f, 0.5f, -1.1f);

        private sealed class LimbView
        {
            public Transform root;
            public Transform ball;          // chair
            public Transform knee;          // patte : articulation
            public Renderer[] renderers;
            public float phaseOffset;
            // Marche (pattes)
            public Vector3 foot;            // position actuelle de la cheville (monde)
            public bool hasFoot;
            public float lift;              // 0 au sol, 1 au sommet de l'arc du pied
        }

        private static readonly int DamageId = Shader.PropertyToID("_Damage");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");

        private static Mesh legMesh;
        private static Mesh lumpMesh;

        private PlayerBody body;
        private PlayerController controller;
        private CatSoul soul;
        private Rigidbody rb;
        private CatLegRig legRig;
        private readonly Dictionary<Limb, LimbView> views = new Dictionary<Limb, LimbView>();
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private MaterialPropertyBlock block;
        private float roll;                 // rotation de la boule qui roule (degrés)
        private float bodyDrop, bodyDropVel; // ressort vertical du corps (impact des pas)
        private float bodyY;                // décalage vertical du corps ce frame (pas + respiration + impact)
        private float bodyPitch;            // bascule de la boule ce frame (degrés)
        private float gaitPhase;            // cycle de marche (0 → 1, tourne avec la distance parcourue)
        private float walkWeight;           // 0 : posture de repos, 1 : démarche complète
        private float runWeight;            // 0 : marche, 1 : course
        private float turnYaw;              // pivot de la boule : 0 regarde à droite, 180 à gauche
        private bool wasGrounded = true;
        private float standUp;              // 0 : boule, 1 : debout
        private float flash;
        private Light fill;

        // Meshes simples partagés (membres au sol, tas de chair, âme marchande)
        public static Mesh LegMesh
        {
            get
            {
                if (legMesh == null)
                {
                    legMesh = LowPoly.Prism(5, 1f, 0.7f, 1f, 0.3f);
                    legMesh.name = "CatLeg";
                }
                return legMesh;
            }
        }

        public static Mesh LumpMesh
        {
            get
            {
                if (lumpMesh == null)
                {
                    lumpMesh = LowPoly.Icosahedron(0.22f, 11);
                    lumpMesh.name = "FleshLump";
                }
                return lumpMesh;
            }
        }

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            controller = GetComponent<PlayerController>();
            soul = GetComponent<CatSoul>();
            rb = GetComponent<Rigidbody>();
            block = new MaterialPropertyBlock();
            if (fleshBallPrefab == null || legPrefab == null)
            {
                Debug.LogWarning("CatBody : modèles non assignés (Prefabs/Player) — le corps ne sera pas visible.", this);
            }
        }

        private void OnEnable()
        {
            PlayerController.OnParry += HandleParry;
        }

        private void OnDisable()
        {
            PlayerController.OnParry -= HandleParry;
            generated.DestroyAll();
            fill = null;
            views.Clear();
        }

        private void HandleParry()
        {
            flash = 1f;
        }

        private void LateUpdate()
        {
            if (body == null) return;
            if (soul == null) soul = GetComponent<CatSoul>();
            SyncViews();

            PlayerForm form = body.Form;
            float dt = Time.deltaTime;
            float facing = controller != null && controller.Facing < 0f ? -1f : 1f;
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            bool grounded = controller == null || controller.IsGrounded;
            float moving = Mathf.Clamp01(Mathf.Abs(vel.x) / 4f);

            // La chair recouvre entièrement l'âme dès qu'elle existe
            if (soul != null) soul.SetHidden(form != PlayerForm.Soul);
            UpdateFillLight(form != PlayerForm.Soul);

            standUp = Mathf.MoveTowards(standUp, form == PlayerForm.Biped ? 1f : 0f, dt * morphSpeed);
            if (form == PlayerForm.Ball) roll -= vel.x * dt / 0.45f * Mathf.Rad2Deg;
            else roll = Mathf.LerpAngle(roll, 0f, 1f - Mathf.Exp(-8f * dt));
            flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, parryFlashTime));
            UpdateBodyMotion(dt, facing, vel, grounded, moving);
            float hop = 0f;

            foreach (KeyValuePair<Limb, LimbView> pair in views)
            {
                Limb limb = pair.Key;
                LimbView view = pair.Value;
                bool show = !limb.IsTorn;
                if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
                if (!show) continue;

                float ratio = limb.LifeRatio;
                float shake = ratio < shakeBelow
                    ? (Mathf.PerlinNoise(Time.time * 25f, view.phaseOffset * 10f) - 0.5f) * (1f - ratio / shakeBelow)
                    : 0f;
                ApplyLook(view, 1f - ratio);

                if (limb.Kind == LimbKind.Flesh) PlaceFlesh(view, facing, hop, shake);
                else PlaceLeg(limb, view, facing, grounded, vel, shake, dt);
            }
        }

        private void UpdateFillLight(bool on)
        {
            if (fill == null)
            {
                if (!on) return;
                GameObject go = generated.Track(new GameObject("BodyFillLight"));
                fill = go.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.shadows = LightShadows.None;
            }
            fill.enabled = on;
            fill.color = fillColor;
            fill.range = fillRange;
            fill.intensity = fillIntensity * (1f + flash);
            fill.transform.position = transform.position + fillOffset;
        }

        // Chair abîmée → à vif ; parade → flash blanc
        private void ApplyLook(LimbView view, float damage)
        {
            foreach (Renderer r in view.renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(DamageId, Mathf.SmoothStep(0f, 1f, damage));
                block.SetFloat(FlashId, flash);
                r.SetPropertyBlock(block);
            }
        }

        private void PlaceFlesh(LimbView view, float facing, float hop, float shake)
        {
            view.root.position = transform.position + new Vector3(shake * 0.05f, hop, 0f);

            // Au sol elle roule ; debout, les pattes la soulèvent et elle suit chaque pas
            float s = Mathf.SmoothStep(0f, 1f, standUp);
            float y = Mathf.Lerp(ballHeight, standingBallHeight, s) + bodyY * s;
            view.ball.localPosition = new Vector3(0f, y, 0f);
            // Demi-tour : la boule pivote de 180° sur l'axe vertical, l'avant passe côté caméra
            turnYaw = Mathf.MoveTowards(turnYaw, facing > 0f ? 0f : 180f, turnSpeed * Time.deltaTime);
            // Debout, la boule penche vers l'avant et bascule à chaque pas.
            // Inclinaison exprimée dans le repère du chat (« regarde à droite ») : le pivot la retourne avec lui
            float tilt = (-standingTilt + bodyPitch * facing) * s;
            view.ball.localRotation = Quaternion.Euler(0f, 0f, roll)
                                      * Quaternion.Euler(0f, turnYaw, 0f)
                                      * Quaternion.Euler(0f, 0f, tilt + shake * 10f);
            // Écrasée quand un pied se pose, étirée quand le corps remonte
            float k = Mathf.Clamp(bodyDrop * squash * s, -0.25f, 0.25f);
            view.ball.localScale = new Vector3(1f - k * 0.6f, 1f + k, 1f - k * 0.6f);
        }

        // Cycle de marche et mouvement du corps debout
        private void UpdateBodyMotion(float dt, float facing, Vector3 vel, bool grounded, float moving)
        {
            float speed = Mathf.Abs(vel.x);
            float stride = Stride(speed);
            int legs = 0;
            foreach (KeyValuePair<Limb, LimbView> pair in views)
            {
                if (pair.Key.Kind == LimbKind.Leg && !pair.Key.IsTorn) legs++;
            }

            // Le cycle avance avec la distance : en appui, le pied recule exactement à la vitesse du sol
            walkWeight = Mathf.MoveTowards(walkWeight, grounded && speed > 0.3f ? 1f : 0f, dt * walkFade);
            float runGoal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(runBlendFrom, runBlendTo, speed));
            runWeight = Mathf.MoveTowards(runWeight, runGoal, dt * walkFade);
            if (grounded) gaitPhase = Mathf.Repeat(gaitPhase + Duty() * speed / Mathf.Max(0.05f, stride) * dt, 1f);

            // Le corps monte au milieu de chaque pas (deux fois par cycle) et bascule vers la patte d'appui
            float rise = Mathf.Lerp(stepRise, runRise, runWeight) * (legs == 1 ? oneLegHop : 1f);
            float crouch = Mathf.Lerp(walkCrouch, runCrouch, runWeight) * walkWeight;
            float bob = legs == 1
                ? Mathf.Max(0f, Mathf.Sin(gaitPhase * Mathf.PI * 2f))           // une patte : un bond par cycle
                : 0.5f - 0.5f * Mathf.Cos((gaitPhase - runWeight * Duty() * 0.5f) * Mathf.PI * 4f);
            // deux pattes : deux pas par cycle. Marche : le corps est au plus bas pattes écartées ;
            // course : au plus bas au milieu de l'appui, au plus haut pendant la phase de vol
            float sway = Mathf.Sin(gaitPhase * Mathf.PI * 2f) * stepPitch;

            // Retombée d'un saut : le corps s'écrase puis revient (ressort)
            if (grounded && !wasGrounded) bodyDropVel -= landImpact * Mathf.Clamp01(-vel.y / 6f + 0.4f);
            wasGrounded = grounded;
            float dtc = Mathf.Min(dt, 0.05f);
            bodyDropVel += (-bodySpringStiffness * bodyDrop - bodySpringDamping * bodyDropVel) * dtc;
            bodyDrop += bodyDropVel * dtc;

            float breath = Mathf.Sin(Time.time * 2.2f) * breathing * (1f - walkWeight);
            bodyY = grounded ? bob * rise * walkWeight - crouch + bodyDrop * 0.12f + breath : 0f;
            bodyPitch = grounded
                ? (sway * walkWeight * facing) - vel.x * runLean * 0.5f
                : -vel.y * 2f * facing;
        }

        // Part du cycle où le pied est au sol : moins en courant (phase de vol)
        private float Duty()
        {
            return Mathf.Lerp(dutyFactor, runDutyFactor, runWeight);
        }

        private float Stride(float speed)
        {
            return Mathf.Clamp(strideMin + speed * stridePerSpeed, strideMin, Mathf.Min(strideMax, maxReach * 2f));
        }

        private void PlaceLeg(Limb limb, LimbView view, float facing, bool grounded, Vector3 vel, float shake, float dt)
        {
            float s = Mathf.SmoothStep(0f, 1f, standUp);
            Vector3 hip = transform.position + new Vector3(limb.Anchor.x * facing, hipHeight + bodyY * s, limb.Anchor.z);
            view.root.position = hip;
            view.root.localScale = new Vector3(facing, 1f, 1f) * Mathf.Max(0.01f, standUp);

            UpdateFoot(view, hip, facing, grounded, vel, dt);

            // Cinématique inverse dans l'espace « regarde à droite » : x retourné si le chat regarde à gauche
            Vector2 target = new Vector2((view.foot.x - hip.x) * facing, view.foot.y - hip.y);
            target.x += shake * 0.1f;
            SolveLeg(target, out float thighAngle, out float shinAngle);

            Vector2 thigh = ThighVector, shin = ShinVector;
            float restThigh = Mathf.Atan2(thigh.y, thigh.x) * Mathf.Rad2Deg;
            float restShin = Mathf.Atan2(shin.y, shin.x) * Mathf.Rad2Deg;
            float hipRot = thighAngle - restThigh;
            float kneeRot = shinAngle - restShin - hipRot;

            // Écartée vers l'extérieur, du côté de son attache (devant ou derrière la boule)
            float splay = -legSplay * Mathf.Sign(limb.Anchor.z == 0f ? 1f : limb.Anchor.z);   // rotation autour de X : négatif = pied vers +Z
            view.root.rotation = Quaternion.Euler(splay, 0f, hipRot * facing);
            if (view.knee != null)
            {
                // Genou replacé à chaque image : suit la longueur de patte même si le modèle est reconstruit en Play
                view.knee.localPosition = thigh;
                view.knee.localRotation = Quaternion.Euler(0f, 0f, kneeRot);
            }
        }

        // Position du pied sur le cycle : appui (recule à la vitesse du sol) puis retour en arc vers l'avant
        private void UpdateFoot(LimbView view, Vector3 hip, float facing, bool grounded, Vector3 vel, float dt)
        {
            float legLength = ThighVector.magnitude + ShinVector.magnitude;

            // En l'air : pattes repliées sous le corps
            if (!grounded || standUp < 0.5f)
            {
                Vector3 tucked = hip + new Vector3(0.12f * facing, -legLength * 0.7f, 0f);
                view.foot = view.hasFoot ? Vector3.Lerp(view.foot, tucked, 1f - Mathf.Exp(-12f * dt)) : tucked;
                view.hasFoot = true;
                view.lift = 0f;
                return;
            }

            float stride = Stride(Mathf.Abs(vel.x));
            float dir = Mathf.Abs(vel.x) > 0.05f ? Mathf.Sign(vel.x) : facing;
            float p = Mathf.Repeat(gaitPhase + view.phaseOffset, 1f);
            float duty = Duty();
            float rel, lift;
            if (p < duty)
            {
                // Appui : de l'avant vers l'arrière, à vitesse constante (collé au sol)
                rel = Mathf.Lerp(0.5f, -0.5f, p / duty);
                lift = 0f;
            }
            else
            {
                // Retour : de l'arrière vers l'avant, en arc, adouci aux deux bouts
                float u = (p - duty) / (1f - duty);
                rel = Mathf.Lerp(-0.5f, 0.5f, u * u * (3f - 2f * u));
                lift = Mathf.Sin(u * Mathf.PI);
            }
            rel *= stride * walkWeight;
            lift *= walkWeight;

            float x = hip.x + rel * dir;
            float arc = Mathf.Lerp(stepHeight, runStepHeight, runWeight);
            Vector3 target = new Vector3(x, GroundAt(hip, x) + ankleHeight + lift * arc, hip.z);
            // Repose sous la hanche sans à-coup (atterrissage, changement de forme)
            view.foot = view.hasFoot ? Vector3.Lerp(view.foot, target, 1f - Mathf.Exp(-30f * dt)) : target;
            view.hasFoot = true;
            view.lift = lift;
        }

        // Hauteur du sol sous un point (le joueur ignoré) ; à défaut, le bas du joueur
        private float GroundAt(Vector3 hip, float x)
        {
            float fallback = transform.position.y - 0.5f;
            Vector3 from = new Vector3(x, hip.y + 0.3f, 0f);
            float best = float.MaxValue, y = fallback;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody == rb) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    y = hit.point.y;
                }
            }
            return y;
        }

        // Repères de la patte, lus sur le prefab (relus si le prefab est reconstruit pendant le Play)
        private CatLegRig LegRig
        {
            get
            {
                if (legRig == null && legPrefab != null) legRig = legPrefab.GetComponent<CatLegRig>();
                return legRig;
            }
        }

        private Vector2 ThighVector => LegRig != null ? LegRig.Thigh : new Vector2(0.14f, -0.36f);   // hanche → genou
        private Vector2 ShinVector => LegRig != null ? LegRig.Shin : new Vector2(0f, -0.47f);        // genou → cheville

        // Deux segments (cuisse, jarret) de la hanche (origine) vers la cheville ; genou vers l'avant.
        // Renvoie les angles (degrés) de la cuisse et du jarret dans l'espace « regarde à droite ».
        private void SolveLeg(Vector2 target, out float thighAngle, out float shinAngle)
        {
            float l1 = ThighVector.magnitude, l2 = ShinVector.magnitude;
            float d = Mathf.Clamp(target.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            float toTarget = Mathf.Atan2(target.y, target.x);
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float a1 = toTarget + Mathf.Acos(cosA);
            Vector2 knee = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * l1;
            Vector2 dir = target.normalized * d - knee;
            thighAngle = a1 * Mathf.Rad2Deg;
            shinAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        // Crée ou retire les visuels pour suivre la liste des membres
        private void SyncViews()
        {
            IReadOnlyList<Limb> limbs = body.Limbs;
            if (views.Count == limbs.Count)
            {
                bool same = true;
                foreach (Limb limb in limbs)
                {
                    if (!views.ContainsKey(limb)) { same = false; break; }
                }
                if (same) return;
            }

            generated.DestroyAll();
            fill = null;
            views.Clear();
            int legIndex = 0;
            foreach (Limb limb in limbs)
            {
                LimbView view = limb.Kind == LimbKind.Flesh ? BuildFlesh() : BuildLeg(legIndex++);
                if (view != null) views[limb] = view;
            }
        }

        private LimbView BuildFlesh()
        {
            if (fleshBallPrefab == null) return null;
            GameObject root = generated.Track(new GameObject("Flesh"));
            Transform ball = generated.Track(Instantiate(fleshBallPrefab, root.transform)).transform;
            return new LimbView
            {
                root = root.transform,
                ball = ball,
                renderers = root.GetComponentsInChildren<Renderer>(true),
                phaseOffset = 0.37f,
            };
        }

        private LimbView BuildLeg(int index)
        {
            if (legPrefab == null) return null;
            GameObject leg = generated.Track(Instantiate(legPrefab));
            return new LimbView
            {
                root = leg.transform,
                knee = leg.transform.Find("Knee"),
                renderers = leg.GetComponentsInChildren<Renderer>(true),
                phaseOffset = index * 0.5f,
            };
        }
    }
}
