using System.Collections.Generic;
using System.Collections;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Boss : le Gardien des Enfers.
    // Spectre en armure d'obsidienne qui flotte sur une queue de flammes, casque à cornes, couronne infernale,
    // cœur de braise et faux géante. On ne peut le blesser qu'en PARANT.
    //
    // 5 phases (barre de vie en 5 segments). Chaque phase ajoute une attaque et accélère le boss :
    //   I   Le Gardien s'éveille : coup de faux, orbes de feu
    //   II  Colère des Abysses   : + écrasement et ondes de choc
    //   III Pluie d'Âmes         : + météores qui tombent du ciel (zones marquées au sol)
    //   IV  Le Faucheur          : + charge à travers l'écran, combo de faux
    //   V   Jugement Final       : + orbe du Jugement (échange de parades), tout plus rapide
    // Entre deux phases : ralenti, rugissement et MÉTAMORPHOSE. Il grandit à chaque phase et la caméra recule :
    //   II  fissures de lave sur l'armure      III ailes spectrales
    //   IV  cornes géantes, faux en feu        V   éclats d'armure en orbite, armure noircie
    // Aperçu des phases dans l'éditeur : champ « previewPhase ».
    // Visuel généré en code, visible dans l'éditeur hors Play ([ExecuteAlways]).
    [ExecuteAlways]
    public class HellGuardian : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private string bossName = "GARDIEN DES ENFERS";
        [SerializeField] private float maxHealth = 250f;
        [SerializeField] private float activationRange = 16f;     // le combat commence quand le joueur approche
        [SerializeField] private float groundY = -0.45f;
        [SerializeField] private float hoverHeight = 2.2f;        // hauteur du centre au-dessus du sol (en unités de scale)
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float preferredDistance = 7f;    // distance de repos par rapport au joueur
        [SerializeField] private float hitRadius = 0.6f;          // portée des coups autour du centre du joueur

        [Header("Faux")]
        [SerializeField] private float swingWindup = 0.65f;
        [SerializeField] private float swingDamage = 20f;
        [SerializeField] private float swingParryDamage = 12f;    // dégâts subis par le boss quand on pare la faux
        [SerializeField] private float staggerDuration = 1.3f;

        [Header("Orbes")]
        [SerializeField] private int orbCount = 3;
        [SerializeField] private float orbSpeed = 7f;
        [SerializeField] private float orbDamage = 10f;
        [SerializeField] private float orbReflectDamage = 8f;

        [Header("Écrasement")]
        [SerializeField] private float slamDamage = 15f;
        [SerializeField] private float slamParryDamage = 10f;
        [SerializeField] private float shockwaveSpeed = 9f;
        [SerializeField] private float shockwaveDamage = 10f;

        [Header("Météores (phase III+)")]
        [SerializeField] private int meteorCount = 6;
        [SerializeField] private float meteorDamage = 12f;
        [SerializeField] private float meteorReflectDamage = 6f;

        [Header("Charge (phase IV+)")]
        [SerializeField] private float dashDamage = 20f;
        [SerializeField] private float dashParryDamage = 14f;

        [Header("Fente de la faux (phase II+) et piliers de feu (phase II+)")]
        [SerializeField] private float slashDamage = 14f;
        [SerializeField] private float slashSpeed = 12f;
        [SerializeField] private float pillarDamage = 18f;

        [Header("Jugement (phase V)")]
        [SerializeField] private float judgmentDamage = 30f;
        [SerializeField] private float judgmentReflectDamage = 25f;
        [SerializeField] private int judgmentRallies = 2;         // fois où le boss renvoie l'orbe avant de la prendre

        [Header("Apparence")]
        [SerializeField] private float scale = 1.8f;              // taille en phase I ; il grandit à chaque phase
        [SerializeField] private float growthPerPhase = 0.09f;    // +9 % de taille par phase
        [SerializeField] private float turnToCamera = 25f;
        [SerializeField] private Color armorColor = new Color(0.1f, 0.08f, 0.12f);
        [SerializeField] private Color trimColor = new Color(0.42f, 0.1f, 0.07f);
        [SerializeField] private Color bladeColor = new Color(0.3f, 0.3f, 0.36f);
        [SerializeField] private Color glowColor = new Color(1f, 0.5f, 0.1f);     // phase I
        [SerializeField] private Color rageColor = new Color(1f, 0.05f, 0.12f);   // phase V
        [Range(0, 4)] public int previewPhase = 0;              // éditeur hors Play : voir le boss à une phase donnée

        private static readonly string[] phaseNames =
        {
            "Le Gardien s'éveille", "Colère des Abysses", "Pluie d'Âmes", "Le Faucheur", "Jugement Final",
        };
        private static readonly string[] roman = { "I", "II", "III", "IV", "V" };

        // ---------- État ----------
        private float health;
        private float trailHealth;              // barre blanche qui suit les dégâts avec retard
        private float lastHitTime;
        private bool active;
        private bool dead;
        private bool invulnerable;
        private int phase;                      // 0..4
        private int lastAttack = -1;
        private PlayerController target;
        private float facing = -1f;
        private float flip = -1f;               // facing animé (passe par 0 quand il se retourne)
        private bool lockFacing;
        private Vector3 moveVel;
        private float bannerTime = -100f;
        private BossProjectile judgmentOrb;
        private bool phaseChanged;              // changement de phase à lancer au prochain Update
        private bool dieStarted;
        private Vector3 playerVel;              // vitesse du joueur (visée anticipée)
        private Vector3 lastPlayerPos;
        private const float bladeScale = 1.4f;          // faux géante

        public bool IsDead { get { return dead; } }
        public bool IsActive { get { return active; } }
        public int Phase { get { return phase; } }
        public Vector3 CorePosition { get { return core != null ? core.position : transform.position; } }

        // ---------- Pose (les coroutines changent les cibles, Update lisse) ----------
        private float weaponAngle = 25f, weaponTarget = 25f;   // angle de la faux (+ = levée derrière)
        private float tilt, tiltTarget = 6f;                    // inclinaison du corps (+ = en avant)
        private float handRaise, handRaiseTarget;               // main libre levée (lancer d'orbes)
        private float glow, glowTarget;                         // intensité de la lueur
        private float sharpness = 8f;                           // vitesse de la pose (élevée pendant un coup)
        private float flash;                                    // flash blanc quand il est touché
        private float punch;
        private float deathFade = 1f;
        private float crownSpin;
        // Ressorts (position + vitesse) : élan, inertie et léger dépassement au lieu de mouvements secs
        private float weaponVel, tiltVel, handVel;
        private float punchPos, punchVel;
        private float headAngle, headVel;
        private float tailSway, tailVel;
        private float squash, squashVel;
        private Vector3 lastPos, bodyVel;

        // ---------- Visuel ----------
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private GameObject visual;
        private Transform bodyT, core, tail, tailPivot, head, weaponPivot, frontHand, backHand, crown;
        private readonly List<Transform> crownSpikes = new List<Transform>();
        private float curScale = 1.8f;                          // taille actuelle (grandit à chaque phase)
        // Pièces qui apparaissent à une phase donnée (pop animé)
        private readonly List<Transform> phaseParts = new List<Transform>();
        private readonly List<Vector3> phasePartBase = new List<Vector3>();
        private readonly List<int> phasePartMin = new List<int>();
        private readonly List<float> phasePartGrow = new List<float>();
        private readonly List<float> phasePartVel = new List<float>();
        private readonly List<Transform> horns = new List<Transform>();
        private readonly List<Transform> wings = new List<Transform>();
        private Transform orbit;
        private ParticleSystem bladeFire;
        private float hornGrow;
        private ParticleSystem embers;
        private readonly List<Vector3> bladePoints = new List<Vector3>(); // tranchant, repère de la faux
        private Material armorMat, trimMat, bladeMat, glowMat;
        private readonly List<Material> mats = new List<Material>();
        private readonly List<Color> baseColors = new List<Color>();

        // =====================================================================
        //  Cycle de vie
        // =====================================================================

        private void OnEnable()
        {
            BuildVisual();
            health = trailHealth = maxHealth;
        }

        private void OnDisable()
        {
            generated.DestroyAll();
            mats.Clear();
            baseColors.Clear();
            bladePoints.Clear();
            crownSpikes.Clear();
            phaseParts.Clear();
            phasePartBase.Clear();
            phasePartMin.Clear();
            phasePartGrow.Clear();
            phasePartVel.Clear();
            horns.Clear();
            wings.Clear();
            visual = null;
            if (Application.isPlaying) Time.timeScale = 1f;
        }

        private void OnValidate()
        {
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null || !isActiveAndEnabled) return;
                    OnDisable();
                    BuildVisual();
                };
            }
    #endif
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            target = FindFirstObjectByType<PlayerController>();
            // Le gardien attend sous terre
            transform.position = new Vector3(transform.position.x, groundY - 4f * scale, 0f);
            SetVisible(false);
            lastPos = transform.position;
            if (target != null) lastPlayerPos = target.transform.position;
        }

        private void Update()
        {
            if (visual == null) return;
            if (!Application.isPlaying)
            {
                phase = previewPhase;
                ApplyPose(0f, 0f);
                return;
            }

            if (!active && !dead && target != null &&
                Mathf.Abs(target.transform.position.x - transform.position.x) < activationRange)
            {
                active = true;
                StartCoroutine(Fight(false));
            }

            // Changement de phase / mort : relancés ici, hors des coroutines d'attaque
            if (dead && !dieStarted)
            {
                dieStarted = true;
                StopAllCoroutines();
                Time.timeScale = 1f;
                StartCoroutine(Die());
            }
            else if (phaseChanged && !dead)
            {
                phaseChanged = false;
                StopAllCoroutines();
                Time.timeScale = 1f;
                StartCoroutine(Fight(true));
            }

            if (target != null && Time.deltaTime > 0f)
            {
                Vector3 pv = (target.transform.position - lastPlayerPos) / Time.deltaTime;
                lastPlayerPos = target.transform.position;
                playerVel = Vector3.Lerp(playerVel, Vector3.ClampMagnitude(pv, 30f), 1f - Mathf.Exp(-8f * Time.deltaTime));
            }

            if (target != null && !lockFacing && !dead)
            {
                facing = target.transform.position.x < transform.position.x ? -1f : 1f;
            }

            // La barre blanche rattrape la vraie vie après un court délai
            if (Time.time - lastHitTime > 0.6f)
            {
                trailHealth = Mathf.MoveTowards(trailHealth, health, maxHealth * 0.25f * Time.deltaTime);
            }
            ApplyPose(Time.deltaTime, Time.time);
        }

        // =====================================================================
        //  Combat
        // =====================================================================

        // Plus rapide et plus fort à chaque phase
        private float Spd { get { return 1f + phase * 0.1f + (phase == 4 ? 0.1f : 0f); } }
        private float Dmg { get { return 1f + phase * 0.12f; } }

        private IEnumerator Fight(bool transition)
        {
            if (transition) yield return PhaseTransition();
            else yield return Intro();

            while (!dead)
            {
                yield return PickAttack();
                if (dead) yield break;
                // À partir de la phase III, il enchaîne parfois une deuxième attaque sans souffler
                if (phase >= 2 && Random.value < 0.15f + phase * 0.1f)
                {
                    yield return new WaitForSeconds(0.15f);
                    yield return PickAttack();
                    if (dead) yield break;
                }
                yield return Reposition(Mathf.Lerp(0.9f, 0.3f, phase / 4f));
            }
        }

        // Choisit une attaque débloquée par la phase, jamais deux fois la même d'affilée
        private IEnumerator PickAttack()
        {
            // 0 faux, 1 orbes, 2 écrasement, 3 météores, 4 charge, 5 jugement, 6 piliers
            List<int> pool = new List<int> { 0, 1 };
            if (phase >= 1) { pool.Add(2); pool.Add(6); }
            if (phase >= 2) pool.Add(3);
            if (phase >= 3) pool.Add(4);
            if (phase >= 4 && judgmentOrb == null) pool.Add(5);
            if (DistToPlayer() < 5f * curScale) pool.Add(0); // proche : la faux plus souvent
            if (pool.Count > 1) pool.RemoveAll(a => a == lastAttack && pool.Count > 1);

            int pick = pool[Random.Range(0, pool.Count)];
            lastAttack = pick;
            switch (pick)
            {
                case 0: yield return Swing(); break;
                case 1: yield return Orbs(); break;
                case 2: yield return Slam(); break;
                case 3: yield return MeteorRain(); break;
                case 4: yield return Dash(); break;
                case 5: yield return Judgment(); break;
                case 6: yield return Pillars(); break;
            }
        }

        // Sort du sol en tremblant, rugit
        private IEnumerator Intro()
        {
            invulnerable = true;
            bannerTime = Time.unscaledTime;
            SetVisible(true);
            Framing();
            Shake(1.6f, 0.15f);
            glowTarget = 1f;
            Vector3 start = transform.position;
            Vector3 end = new Vector3(start.x, HoverY(), 0f);
            float t = 0f;
            while (t < 1.8f)
            {
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / 1.8f));
                t += Time.deltaTime;
                yield return null;
            }
            yield return Roar(1f);
            invulnerable = false;
        }

        // Faux levée, corps cabré, lueur maximale, écran qui tremble, anneau de braises
        private IEnumerator Roar(float duration)
        {
            weaponTarget = 95f;
            tiltTarget = -15f;
            handRaiseTarget = 1f;
            glowTarget = 3f;
            flash = 0.6f;
            Shake(duration * 0.8f, 0.25f);
            SpawnBurst(60, 10f, 0.8f);
            yield return new WaitForSeconds(duration);
            ResetPose();
            yield return new WaitForSeconds(0.3f);
        }

        // Passage à la phase suivante : ralenti, bannière, retraite, rugissement
        private IEnumerator PhaseTransition()
        {
            invulnerable = true;
            ResetPose();
            // Laisse finir l'arrêt du temps de la parade du joueur
            yield return new WaitForSecondsRealtime(0.12f);
            bannerTime = Time.unscaledTime;
            flash = 1f;
            Framing();
            Shake(1f, 0.3f);
            SpawnBurst(80, 14f, 0.6f);
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.7f);
            Time.timeScale = 1f;

            // Recule au centre de l'arène
            float t = 0f;
            while (t < 0.7f && target != null)
            {
                float side = target.transform.position.x < transform.position.x ? 1f : -1f;
                MoveToward(target.transform.position.x + side * preferredDistance, HoverY() + 0.8f * curScale, 2f);
                t += Time.deltaTime;
                yield return null;
            }
            yield return Roar(1.1f);
            invulnerable = false;
        }

        private void ResetPose()
        {
            sharpness = 8f;
            weaponTarget = 25f;
            tiltTarget = 6f;
            handRaiseTarget = 0f;
            glowTarget = 0f;
            lockFacing = false;
        }

        // Se replace à bonne distance du joueur
        private IEnumerator Reposition(float duration)
        {
            float t = 0f;
            while (t < duration && !dead)
            {
                if (target != null)
                {
                    float side = target.transform.position.x < transform.position.x ? 1f : -1f;
                    MoveToward(target.transform.position.x + side * preferredDistance, HoverY());
                }
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ---------- Faux ----------
        private IEnumerator Swing()
        {
            // Approche : se place pour que l'arc de la faux passe sur le joueur
            float t = 0f;
            while (t < 1.5f && target != null)
            {
                Vector3 goal = SwingSpot();
                if (Vector2.Distance(transform.position, goal) < 0.4f) break;
                MoveToward(goal.x, goal.y, 2.5f);
                t += Time.deltaTime;
                yield return null;
            }

            // Combo : 1 coup, 2 en phase IV, 3 en phase V
            int swings = phase >= 4 ? 3 : (phase >= 3 ? 2 : 1);
            for (int s = 0; s < swings && !dead; s++)
            {
                bool parried = false;
                yield return OneSwing(s == 0 ? swingWindup : swingWindup * 0.5f, r => parried = r);
                if (parried)
                {
                    TakeHit(swingParryDamage);
                    if (phaseChanged || dead) yield break;
                    yield return Stagger();
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.25f);
            ResetPose();
        }

        private IEnumerator OneSwing(float windup, System.Action<bool> result)
        {
            // Armement : faux levée au-dessus de l'épaule, corps en arrière, œil allumé
            lockFacing = false;
            sharpness = 7f;
            weaponTarget = 80f;
            tiltTarget = -10f;
            glowTarget = 1.5f;
            // Pendant l'armement, il continue de suivre le joueur pour rester à portée
            float w = 0f;
            while (w < windup / Spd)
            {
                // Tension qui monte : la faux recule encore un peu, le corps se cabre
                float u = w / (windup / Spd);
                weaponTarget = Mathf.Lerp(80f, 102f, u * u);
                tiltTarget = Mathf.Lerp(-8f, -17f, u);
                if (target != null)
                {
                    Vector3 goal = SwingSpot();
                    MoveToward(goal.x, goal.y, 2.5f);
                }
                w += Time.deltaTime;
                yield return null;
            }
            moveVel = Vector3.zero;

            // Coup : arc rapide vers l'avant et le bas, avec une fente du corps vers le joueur
            lockFacing = true;
            ResetBladeSweep();
            sharpness = 30f * Spd;
            weaponTarget = -110f;
            tiltTarget = 24f;
            Shake(0.15f, 0.08f);
            float lungeDir = facing;
            float lunge = LungeDist();
            bool hasHit = false;
            bool slashed = phase < 1;
            float t = 0f;
            while (t < 0.35f)
            {
                // Fente : rapide puis freinée (sur 0,12 s)
                float u0 = Mathf.Clamp01(t / 0.12f), u1 = Mathf.Clamp01((t + Time.deltaTime) / 0.12f);
                float e0 = 1f - (1f - u0) * (1f - u0), e1 = 1f - (1f - u1) * (1f - u1);
                transform.position += new Vector3(lungeDir * lunge * (e1 - e0), 0f, 0f);
                // Phase II+ : la faux libère un croissant de feu au ras du sol
                if (!slashed && t > 0.08f)
                {
                    slashed = true;
                    Vector3 g = new Vector3(transform.position.x + lungeDir * 1.2f * curScale, groundY, 0f);
                    BossProjectile.SpawnSlash(g, lungeDir, slashSpeed * Spd, slashDamage * Dmg, this, PhaseColor(), 1.5f + phase * 0.15f);
                }
                if (!hasHit && BladeTouchesPlayer(out Vector3 point))
                {
                    hasHit = true;
                    if (!target.TakeDamage(swingDamage * Dmg, transform, point))
                    {
                        ResetPose();
                        result(true);
                        yield break;
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            glowTarget = 0f;
            sharpness = 8f;
        }

        // Chancelle après une parade : recule en titubant
        private IEnumerator Stagger()
        {
            lockFacing = true;
            sharpness = 12f;
            weaponTarget = 60f;
            tiltTarget = -25f;
            glowTarget = 0f;
            tiltVel -= 220f;     // encaisse le choc
            weaponVel += 300f;
            float t = 0f;
            float v = -facing * 9f;
            while (t < staggerDuration && !dead)
            {
                transform.position += new Vector3(v * Time.deltaTime, 0f, 0f);
                v *= Mathf.Exp(-7f * Time.deltaTime);   // glisse puis s'arrête
                t += Time.deltaTime;
                yield return null;
            }
            ResetPose();
        }

        // Position du boss pour que le joueur soit sur l'arc de la faux :
        // la main est placée devant le joueur et au-dessus, à une distance égale au rayon de la lame.
        private Vector3 SwingSpot()
        {
            Vector3 pp = target.transform.position;
            float side = pp.x < transform.position.x ? 1f : -1f;   // côté où se trouve le boss
            float r = BladeRadius();
            // La fente du coup rapproche le boss : on se place d'autant plus loin
            Vector3 hand = new Vector3(pp.x + side * (r * 0.8f + LungeDist()), pp.y + r * 0.55f, 0f);
            // Main prévue en fin de frappe (elle bouge avec la faux), côté joueur
            Vector3 hl = HandLocal(-90f, 20f);
            float yr = turnToCamera * Mathf.Deg2Rad;
            Vector3 handOffset = new Vector3(-side * (hl.x * Mathf.Cos(yr) + hl.z * Mathf.Sin(yr)) * curScale, hl.y * curScale, 0f);
            return new Vector3(hand.x - handOffset.x, hand.y - handOffset.y, 0f);
        }

        // Distance moyenne main → tranchant, en unités du monde
        private float BladeRadius()
        {
            float sum = 0f;
            for (int i = 0; i < bladePoints.Count; i++) sum += bladePoints[i].magnitude;
            return sum / Mathf.Max(1, bladePoints.Count) * curScale * bladeScale * 0.9f;
        }

        private float LungeDist() { return 0.9f * curScale; }

        // Point visé avec anticipation : là où le joueur sera quand le projectile arrivera
        private Vector3 Lead(Vector3 from, float speed, float amount)
        {
            Vector3 pp = target.transform.position;
            float time = Vector2.Distance(from, pp) / Mathf.Max(speed, 0.1f);
            Vector3 p = pp + new Vector3(playerVel.x, playerVel.y * 0.3f, 0f) * time * amount;
            p.y = Mathf.Max(p.y, groundY + 0.3f);
            return p;
        }

        // Balayage de la lame : chaque point du tranchant est testé sur tout son trajet depuis l'image précédente,
        // contre toute la hauteur du joueur (pieds, centre, tête)
        private Vector3[] prevBlade;

        private void ResetBladeSweep()
        {
            if (prevBlade == null || prevBlade.Length != bladePoints.Count) prevBlade = new Vector3[bladePoints.Count];
            for (int i = 0; i < bladePoints.Count; i++) prevBlade[i] = weaponPivot.TransformPoint(bladePoints[i]);
        }

        private bool BladeTouchesPlayer(out Vector3 point)
        {
            point = Vector3.zero;
            if (target == null) return false;
            if (prevBlade == null || prevBlade.Length != bladePoints.Count) ResetBladeSweep();
            Vector2 pc = target.transform.position;
            Vector2[] body = { pc, pc + Vector2.up * 0.5f, pc - Vector2.up * 0.5f };
            bool hit = false;
            for (int i = 0; i < bladePoints.Count; i++)
            {
                Vector3 cur = weaponPivot.TransformPoint(bladePoints[i]);
                if (!hit)
                {
                    foreach (Vector2 b in body)
                    {
                        if (DistToSegment(b, prevBlade[i], cur) < hitRadius + 0.1f * curScale)
                        {
                            point = cur;
                            hit = true;
                            break;
                        }
                    }
                }
                prevBlade[i] = cur;
            }
            return hit;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------- Orbes ----------
        private IEnumerator Orbs()
        {
            handRaiseTarget = 1f;
            glowTarget = 1.2f;
            yield return new WaitForSeconds(0.5f / Spd);

            int n = orbCount + phase;
            for (int i = 0; i < n && !dead; i++)
            {
                if (target != null)
                {
                    Vector3 from = backHand.position;
                    from.z = 0f;
                    Vector3 dir = Lead(from, orbSpeed * Spd, 0.7f) - from;
                    dir.z = 0f;
                    // Éventail autour du joueur, plus large au fil des phases
                    dir = Quaternion.Euler(0f, 0f, (i - (n - 1) / 2f) * (5f + phase * 2f)) * dir;
                    BossProjectile.SpawnOrb(from, dir, orbSpeed * Spd, orbDamage * Dmg, orbReflectDamage, this, PhaseColor(), 0.7f + phase * 0.06f);
                    punch = 1f;
                }
                yield return new WaitForSeconds(0.42f / Spd);
            }
            yield return new WaitForSeconds(0.3f);
            ResetPose();
        }

        // ---------- Écrasement ----------
        private IEnumerator Slam()
        {
            if (target == null) yield break;

            // Monte au-dessus du joueur
            weaponTarget = 85f;
            glowTarget = 1.5f;
            float t = 0f;
            while (t < 0.8f)
            {
                MoveToward(target.transform.position.x, HoverY() + 2.5f * curScale, 3f);
                t += Time.deltaTime;
                yield return null;
            }
            // Suspendu : signal ; il suit encore le joueur, puis se fige juste avant de tomber
            tiltTarget = 15f;
            flash = 0.4f;
            t = 0f;
            float hang = 0.45f / Spd;
            while (t < hang)
            {
                if (t < hang - 0.15f) MoveToward(target.transform.position.x + playerVel.x * 0.15f, HoverY() + 2.5f * curScale, 4f);
                t += Time.deltaTime;
                yield return null;
            }
            moveVel = Vector3.zero;

            // Chute
            Vector3 start = transform.position;
            Vector3 end = new Vector3(start.x, groundY + 1.4f * curScale, 0f);
            sharpness = 30f;
            weaponTarget = -90f;
            t = 0f;
            while (t < 0.14f)
            {
                float u = t / 0.14f;
                transform.position = Vector3.Lerp(start, end, u * u);
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = end;
            moveVel = Vector3.zero;
            squashVel += 4f;
            tiltVel += 120f;
            Shake(0.4f, 0.3f);
            SpawnBurst(40, 8f, 0.4f);

            // Impact direct
            Vector3 pp = target.transform.position;
            if (Mathf.Abs(pp.x - end.x) < 1.6f * curScale && pp.y < end.y + 1f * curScale)
            {
                if (!target.TakeDamage(slamDamage * Dmg, transform, Vector3.Lerp(end, pp, 0.5f)))
                {
                    TakeHit(slamParryDamage);
                    if (phaseChanged || dead) yield break;
                    yield return Stagger();
                    yield break;
                }
            }

            // Ondes de choc des deux côtés (deux vagues en phase V)
            int waves = phase >= 4 ? 3 : (phase >= 2 ? 2 : 1);
            for (int w = 0; w < waves; w++)
            {
                Vector3 ground = new Vector3(end.x, groundY, 0f);
                BossProjectile.SpawnShockwave(ground + Vector3.right * 0.8f, 1f, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1.35f);
                BossProjectile.SpawnShockwave(ground - Vector3.right * 0.8f, -1f, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1.35f);
                if (w < waves - 1) yield return new WaitForSeconds(0.45f);
            }

            yield return new WaitForSeconds(0.5f);
            ResetPose();
            t = 0f;
            while (t < 0.6f)
            {
                MoveToward(transform.position.x, HoverY(), 1.5f);
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ---------- Météores (phase III+) ----------
        private IEnumerator MeteorRain()
        {
            if (target == null) yield break;
            // Lève les bras vers le ciel
            handRaiseTarget = 1f;
            weaponTarget = 95f;
            tiltTarget = -15f;
            glowTarget = 2.5f;
            Shake(0.6f, 0.12f);
            yield return new WaitForSeconds(0.6f / Spd);

            int n = meteorCount + 2 + (phase >= 4 ? 3 : 0);
            for (int i = 0; i < n && !dead; i++)
            {
                // Le premier vise le joueur, les autres tombent autour
                float x = target.transform.position.x + (i == 0 ? playerVel.x * 0.75f / Spd : Random.Range(-6f, 6f));
                BossProjectile.SpawnMeteor(x, groundY, 0.75f / Spd, 16f, meteorDamage * Dmg, meteorReflectDamage, this, PhaseColor());
                yield return new WaitForSeconds(0.28f / Spd);
            }
            yield return new WaitForSeconds(0.6f);
            ResetPose();
        }

        // ---------- Charge (phase IV+) ----------
        private IEnumerator Dash()
        {
            if (target == null) yield break;

            // Se place à distance, à hauteur du joueur
            float side = target.transform.position.x < transform.position.x ? 1f : -1f;
            float y = groundY + 1.5f * curScale;
            float t = 0f;
            while (t < 0.9f)
            {
                MoveToward(target.transform.position.x + side * 8f, y, 2f);
                t += Time.deltaTime;
                yield return null;
            }

            // Armement : se ramasse en arrière, faux pointée devant
            weaponTarget = -20f;
            tiltTarget = -20f;
            glowTarget = 2.5f;
            flash = 0.5f;
            Shake(0.4f, 0.08f);
            yield return new WaitForSeconds(0.55f / Spd);

            // Charge à travers l'écran
            lockFacing = true;
            float dir = facing;
            tiltTarget = 30f;
            weaponTarget = -70f;
            sharpness = 20f;
            float endX = target.transform.position.x + dir * 6f;
            Vector3 start = transform.position;
            Vector3 end = new Vector3(endX, y, 0f);
            float dur = 0.4f / Spd;
            ResetBladeSweep();
            bool hasHit = false;
            t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                transform.position = Vector3.Lerp(start, end, 1f - (1f - u) * (1f - u) * (1f - u));
                if (!hasHit)
                {
                    Vector3 pp = target.transform.position;
                    bool body = new Vector2(pp.x - transform.position.x, pp.y - transform.position.y).magnitude < 1.4f * curScale;
                    if (body || BladeTouchesPlayer(out _))
                    {
                        hasHit = true;
                        if (!target.TakeDamage(dashDamage * Dmg, transform, Vector3.Lerp(transform.position, pp, 0.5f)))
                        {
                            moveVel = Vector3.zero;
                            TakeHit(dashParryDamage);
                            if (phaseChanged || dead) yield break;
                            yield return Stagger();
                            yield break;
                        }
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            moveVel = Vector3.zero;
            // Phase V : demi-tour immédiat et deuxième charge
            if (phase >= 4 && !dead && !secondDash)
            {
                secondDash = true;
                lockFacing = false;
                yield return new WaitForSeconds(0.25f);
                yield return Dash();
                secondDash = false;
                yield break;
            }
            yield return new WaitForSeconds(0.35f);
            ResetPose();
        }
        private bool secondDash;

        // ---------- Piliers de feu (phase II+) ----------
        // Il plonge la faux dans le sol : une ligne de colonnes de feu jaillit jusqu'au joueur et au-delà
        private IEnumerator Pillars()
        {
            if (target == null) yield break;
            sharpness = 9f;
            weaponTarget = 100f;
            tiltTarget = -14f;
            glowTarget = 2f;
            yield return new WaitForSeconds(0.55f / Spd);

            // Frappe la faux au sol
            lockFacing = true;
            sharpness = 28f;
            weaponTarget = -120f;
            tiltTarget = 26f;
            squashVel += 3f;
            Shake(0.35f, 0.22f);
            SpawnBurst(30, 7f, 0.4f);

            float dir = facing;
            float start = transform.position.x + dir * 1.5f * curScale;
            float h = 5f + phase * 0.6f;
            int n = 7 + phase;
            float spacing = 1.7f;
            for (int i = 0; i < n && !dead; i++)
            {
                BossProjectile.SpawnPillar(start + dir * i * spacing, groundY, 0.5f / Spd, pillarDamage * Dmg, this, PhaseColor(), h);
                // Phase IV+ : deuxième ligne venant de l'autre côté du joueur
                if (phase >= 3 && target != null)
                    BossProjectile.SpawnPillar(target.transform.position.x + dir * (n - i) * spacing * 0.5f, groundY, 0.5f / Spd, pillarDamage * Dmg, this, PhaseColor(), h);
                yield return new WaitForSeconds(0.1f / Spd);
            }
            yield return new WaitForSeconds(0.5f);
            ResetPose();
        }

        // ---------- Jugement (phase V) ----------
        private IEnumerator Judgment()
        {
            if (target == null) yield break;

            // Monte et charge une énorme orbe au-dessus de sa tête
            handRaiseTarget = 1f;
            weaponTarget = 95f;
            tiltTarget = -12f;
            float t = 0f;
            while (t < 1.4f)
            {
                MoveToward(transform.position.x, HoverY() + 1.5f * curScale, 1f);
                glowTarget = 1f + t * 2f;
                if (Random.value < 0.15f) Shake(0.1f, 0.06f + t * 0.05f);
                t += Time.deltaTime;
                yield return null;
            }
            Vector3 from = backHand.position + Vector3.up * 0.8f * curScale;
            from.z = 0f;
            Vector3 dir = target.transform.position - from;
            dir.z = 0f;
            judgmentOrb = BossProjectile.SpawnJudgment(from, dir, 6f, judgmentDamage * Dmg, judgmentReflectDamage, judgmentRallies, this, rageColor);
            Shake(0.5f, 0.2f);
            punch = 1f;
            handRaiseTarget = 0f;

            // Attend la fin de l'échange
            t = 0f;
            while (judgmentOrb != null && t < 10f && !dead)
            {
                t += Time.deltaTime;
                yield return null;
            }
            ResetPose();
        }

        // L'orbe du Jugement revient sur lui : la renvoie au joueur tant qu'il reste des échanges
        public bool ReturnOrb()
        {
            if (dead) return false;
            weaponVel -= 1600f;  // revers de faux fouetté
            weaponTarget = 25f;
            sharpness = 8f;
            flash = 0.5f;
            Shake(0.2f, 0.15f);
            return true;
        }

        // ---------- Dégâts et mort ----------

        public void TakeHit(float amount)
        {
            if (dead || !active || invulnerable) return;
            health -= amount;
            lastHitTime = Time.time;
            flash = 1f;
            tiltVel -= 90f;
            headVel -= 120f;
            Shake(0.15f, 0.12f);

            if (health <= 0f)
            {
                health = 0f;
                dead = true;
                return;
            }

            // Nouveau segment de la barre : phase suivante (une seule à la fois)
            int newPhase = Mathf.Min(4, (int)((maxHealth - health) / (maxHealth / 5f)));
            if (newPhase > phase)
            {
                phase++;
                health = Mathf.Max(health, maxHealth * (1f - phase / 5f) - 0.01f);
                invulnerable = true;
                phaseChanged = true;
            }
        }

        private IEnumerator Die()
        {
            lockFacing = true;
            bannerTime = -100f;
            yield return new WaitForSecondsRealtime(0.12f);
            Time.timeScale = 0.25f;
            Shake(2f, 0.35f);
            tiltTarget = -30f;
            weaponTarget = -60f;
            glowTarget = 4f;

            // Tremble, flashe, puis éclate en braises
            float t = 0f;
            Vector3 basePos = transform.position;
            while (t < 2f)
            {
                flash = Mathf.PingPong(t * 8f, 1f);
                float nt = Time.unscaledTime * 25f;
                transform.position = basePos + new Vector3(Mathf.PerlinNoise(nt, 0f) - 0.5f, Mathf.PerlinNoise(0f, nt) - 0.5f, 0f) * 0.25f;
                if (Random.value < 0.1f) SpawnBurst(10, 6f, 0.3f);
                t += Time.unscaledDeltaTime;
                if (t > 1.2f) Time.timeScale = 1f;
                yield return null;
            }
            Time.timeScale = 1f;
            SpawnBurst(250, 16f, 1f);
            t = 0f;
            while (t < 0.4f)
            {
                deathFade = 1f - t / 0.4f;
                t += Time.deltaTime;
                yield return null;
            }
            Debug.Log(bossName + " vaincu !");
            GameCamera cam = GameCamera.Main;
            if (cam != null) cam.SetFraming(0f, 0f);
            Destroy(gameObject);
        }

        private void SpawnBurst(int count, float speed, float radius)
        {
            ParticleBurst.Spawn("GuardianBurst", CorePosition, new ParticleSystem.MinMaxGradient(PhaseColor(), Color.white), count,
                lifetime: new Vector2(0.5f, 1.4f),
                speed: new Vector2(speed * 0.3f, speed),
                size: new Vector2(0.08f, 0.3f),
                gravity: 0.3f,
                radius: radius * curScale,
                duration: 1f);
        }

        // ---------- Petits outils ----------

        // Ressort amorti (freq en Hz, damp 1 = sans dépassement). Sous-pas pour rester stable.
        private static void Spring(ref float x, ref float v, float target, float freq, float damp, float dt)
        {
            float w = 2f * Mathf.PI * freq;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 8);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                v += (w * w * (target - x) - 2f * damp * w * v) * h;
                x += v * h;
            }
        }

        // Main de la faux, repère du modèle, selon l'angle de la faux et l'inclinaison du corps
        private static Vector3 HandLocal(float angle, float tiltDeg)
        {
            Vector3 mid = new Vector3(0.6f, 0.05f, -0.8f);      // repos (faux à 25°)
            Vector3 high = new Vector3(0.25f, 0.6f, -0.7f);     // faux levée
            Vector3 low = new Vector3(0.95f, -0.35f, -0.65f);   // fin de frappe
            Vector3 p = angle >= 25f
                ? Vector3.LerpUnclamped(mid, high, Mathf.Min((angle - 25f) / 70f, 1.3f))
                : Vector3.LerpUnclamped(mid, low, Mathf.Min((25f - angle) / 135f, 1.3f));
            return Quaternion.Euler(0f, 0f, -tiltDeg) * p;
        }

        private float PhaseScale() { return scale * (1f + growthPerPhase * phase); }

        private float HoverY() { return groundY + hoverHeight * curScale; }

        private float DistToPlayer()
        {
            return target != null ? Mathf.Abs(target.transform.position.x - transform.position.x) : Mathf.Infinity;
        }

        // Déplacement lourd et fluide (accélère et freine)
        private void MoveToward(float x, float y, float speedMul = 1f)
        {
            Vector3 goal = new Vector3(x, y, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref moveVel, 0.35f / speedMul, moveSpeed * speedMul);
        }

        private void Shake(float duration, float magnitude)
        {
            GameCamera.ShakeMain(duration, magnitude);
        }

        // La caméra recule et monte pour cadrer le boss, un peu plus à chaque phase
        private void Framing()
        {
            GameCamera cam = GameCamera.Main;
            if (cam != null) cam.SetFraming(3.5f + phase * 0.8f, 1.5f + phase * 0.35f);
        }

        // Lueur : orange en phase I, de plus en plus rouge jusqu'à la phase V
        private Color PhaseColor() { return Color.Lerp(glowColor, rageColor, phase / 4f); }

        private void SetVisible(bool v)
        {
            if (visual != null) visual.SetActive(v);
        }

        // =====================================================================
        //  Visuel
        // =====================================================================
        // Repère du modèle : avant = +X, haut = +Y, côté caméra = -Z. Unité = scale.

        private void BuildVisual()
        {
            if (visual != null) return;

            visual = new GameObject("GuardianVisual");
            Track(visual);
            visual.transform.SetParent(transform, false);

            armorMat = MakeMat(armorColor, 0.6f, false);
            trimMat = MakeMat(trimColor, 0.4f, false);
            bladeMat = MakeMat(bladeColor, 0.8f, false);
            glowMat = MakeMat(glowColor, 0f, true);

            bodyT = Empty("Body", visual.transform);

            // Torse : plus large aux épaules
            Part("Torso", bodyT, LowPoly.Prism(6, 0.38f, 0.62f, 1.0f, 0.2f), armorMat, new Vector3(0f, -0.45f, 0f), Vector3.zero, Vector3.one);
            Part("Collar", bodyT, LowPoly.Prism(6, 0.64f, 0.5f, 0.14f, 0.2f), trimMat, new Vector3(0f, 0.52f, 0f), Vector3.zero, Vector3.one);
            Part("Belt", bodyT, LowPoly.Prism(6, 0.42f, 0.42f, 0.12f, 0.2f), trimMat, new Vector3(0f, -0.42f, 0f), Vector3.zero, Vector3.one);

            // Cœur de braise sur la poitrine
            core = Part("Core", bodyT, LowPoly.Icosahedron(0.1f, 3), glowMat, new Vector3(0.5f, 0.15f, 0f), Vector3.zero, Vector3.one * 0.3f);

            // Queue de flammes (spectre : pas de jambes)
            tailPivot = Empty("TailPivot", bodyT);
            tailPivot.localPosition = new Vector3(0f, -0.45f, 0f);
            tail = Part("Tail", tailPivot, LowPoly.Prism(6, 0.38f, 0f, 1.5f, 0.3f), glowMat, Vector3.zero, new Vector3(180f, 0f, 0f), Vector3.one);

            // Casque : boîte effilée, fente lumineuse, cornes
            head = Empty("Head", bodyT);
            head.localPosition = new Vector3(0.05f, 0.85f, 0f);
            Part("Helmet", head, LowPoly.Box(0.75f), armorMat, Vector3.zero, Vector3.zero, new Vector3(0.45f, 0.5f, 0.42f));
            Part("Visor", head, LowPoly.Box(1f), trimMat, new Vector3(0.2f, -0.02f, 0f), Vector3.zero, new Vector3(0.1f, 0.22f, 0.38f));
            Part("Eyes", head, LowPoly.Box(1f), glowMat, new Vector3(0.255f, 0.02f, 0f), Vector3.zero, new Vector3(0.03f, 0.05f, 0.3f));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                // Corne en deux parties : vers l'extérieur, puis vers le haut et l'arrière
                Transform horn = Part("Horn", head, LowPoly.Prism(5, 0.09f, 0.06f, 0.35f), trimMat, new Vector3(-0.05f, 0.15f, side * 0.18f), new Vector3(side * 70f, 0f, 10f), Vector3.one);
                horns.Add(horn);
                Part("HornTip", horn, LowPoly.Prism(5, 0.06f, 0f, 0.5f), armorMat, new Vector3(0f, 0.33f, 0f), new Vector3(-side * 60f, 0f, 20f), Vector3.one);
            }

            // Couronne infernale : anneau de pointes lumineuses derrière la tête (une de plus par phase)
            crown = Empty("Crown", head);
            crown.localPosition = new Vector3(-0.1f, 0.1f, 0.25f);
            Mesh crownSpike = LowPoly.Prism(4, 0.06f, 0f, 0.38f);
            for (int i = 0; i < 9; i++)
            {
                Transform pivot = Empty("CrownPivot", crown);
                pivot.localRotation = Quaternion.Euler(0f, 0f, i * 40f);
                Part("CrownSpike", pivot, crownSpike, glowMat, new Vector3(0f, 0.55f, 0f), Vector3.zero, Vector3.one, i == 0);
                crownSpikes.Add(pivot);
            }

            // Épaulières à pointes
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                Part("Pauldron", bodyT, LowPoly.Icosahedron(0.08f, 10 + i), trimMat, new Vector3(0f, 0.55f, side * 0.62f), Vector3.zero, new Vector3(0.5f, 0.32f, 0.45f));
                Part("PauldronSpike", bodyT, LowPoly.Prism(4, 0.08f, 0f, 0.4f), armorMat, new Vector3(-0.05f, 0.65f, side * 0.68f), new Vector3(side * 35f, 0f, 15f), Vector3.one);
                Part("PauldronSpike", bodyT, LowPoly.Prism(4, 0.06f, 0f, 0.28f), armorMat, new Vector3(0.12f, 0.62f, side * 0.62f), new Vector3(side * 20f, 0f, -10f), Vector3.one);
            }

            // Mains flottantes : la main avant (côté caméra) tient la faux, l'autre lance les orbes
            frontHand = Part("HandFront", visual.transform, LowPoly.Icosahedron(0.1f, 20), armorMat, Vector3.zero, Vector3.zero, Vector3.one * 0.3f);
            backHand = Part("HandBack", visual.transform, LowPoly.Icosahedron(0.1f, 21), armorMat, Vector3.zero, Vector3.zero, Vector3.one * 0.3f);

            // Faux : pivot dans la main avant
            weaponPivot = Empty("ScythePivot", visual.transform);
            BuildScythe(weaponPivot);

            BuildPhaseParts();
            BuildEmbers();
            ApplyPose(0f, 0f);
        }

        private void AddPhasePart(Transform t, int minPhase)
        {
            phaseParts.Add(t);
            phasePartBase.Add(t.localScale);
            phasePartMin.Add(minPhase);
            phasePartGrow.Add(0f);
            phasePartVel.Add(0f);
            t.localScale = Vector3.zero;
        }

        private void BuildPhaseParts()
        {
            System.Random r = new System.Random(5);
            float R(float a, float b) { return a + (float)r.NextDouble() * (b - a); }

            // Phase II : fissures de lave sur le torse et le casque
            Mesh crack = LowPoly.Box(1f);
            for (int i = 0; i < 12; i++)
            {
                float y = R(-0.35f, 0.45f);
                float a = R(-75f, 75f);
                float rad = (0.38f + 0.24f * (y + 0.45f)) * 0.88f;
                Vector3 pos = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * rad, y, Mathf.Sin(a * Mathf.Deg2Rad) * rad);
                Transform t = Part("Crack", bodyT, crack, glowMat, pos, new Vector3(0f, -a, R(-55f, 55f)), new Vector3(0.05f, R(0.15f, 0.38f), 0.035f), i == 0);
                AddPhasePart(t, 1);
            }

            // Phase III : ailes spectrales (éclats lumineux en éventail dans le dos)
            float[] len = { 1.5f, 1.85f, 1.65f, 1.3f, 0.95f };
            for (int w = 0; w < 2; w++)
            {
                float side = w == 0 ? 1f : -1f;
                Transform wing = Empty("Wing", bodyT);
                wing.localPosition = new Vector3(-0.3f, 0.45f, side * 0.3f);
                wings.Add(wing);
                for (int i = 0; i < len.Length; i++)
                {
                    Transform shard = Part("WingShard", wing, LowPoly.Prism(3, 0.13f, 0f, len[i]), glowMat, Vector3.zero, new Vector3(side * 28f, 0f, 22f + i * 24f), new Vector3(1f, 1f, 0.4f));
                    AddPhasePart(shard, 2);
                }
            }

            // Phase IV : la faux s'enflamme (les cornes grandissent dans ApplyPose)
            GameObject fire = new GameObject("BladeFire");
            Track(fire);
            fire.transform.SetParent(weaponPivot, false);
            fire.transform.localPosition = new Vector3(0.95f, 1.5f, 0f);
            bladeFire = fire.AddComponent<ParticleSystem>();
            bladeFire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = bladeFire.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.gravityModifier = -0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = bladeFire.emission;
            em.rateOverTime = 0f;
            var shape = bladeFire.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.8f, 0.8f, 0.1f);
            var sol = bladeFire.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            Material fm = MaterialFactory.Unlit(Color.white);
            Track(fm);
            bladeFire.GetComponent<ParticleSystemRenderer>().material = fm;
            bladeFire.Play();

            // Phase V : éclats d'armure arrachés qui tournent autour de lui
            orbit = Empty("Orbit", visual.transform);
            orbit.localPosition = new Vector3(0f, 0.15f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 pos = new Vector3(Mathf.Cos(a) * 1.45f, Mathf.Sin(a * 3f) * 0.25f, Mathf.Sin(a) * 1.45f);
                Transform shard = Part("OrbitShard", orbit, LowPoly.Prism(4, 0.16f, 0f, R(0.4f, 0.65f), 0.5f), i % 2 == 0 ? trimMat : armorMat, pos, new Vector3(R(0f, 360f), R(0f, 360f), R(0f, 360f)), Vector3.one);
                AddPhasePart(shard, 4);
            }
        }

        // Braises qui s'échappent du corps et montent
        private void BuildEmbers()
        {
            GameObject go = new GameObject("Embers");
            Track(go);
            go.transform.SetParent(bodyT, false);
            go.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            embers = go.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = embers.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = glowColor;
            main.gravityModifier = -0.15f; // montent
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = embers.emission;
            emission.rateOverTime = 12f;

            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;

            var size = embers.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            Material m = MaterialFactory.Unlit(Color.white);
            Track(m);
            embers.GetComponent<ParticleSystemRenderer>().material = m;
            embers.Play();
        }

        private void BuildScythe(Transform pivot)
        {
            // Manche
            Part("Shaft", pivot, LowPoly.Prism(5, 0.05f, 0.045f, 3.1f), armorMat, new Vector3(0f, -1.2f, 0f), Vector3.zero, Vector3.one);
            Part("Pommel", pivot, LowPoly.Icosahedron(0f, 0), trimMat, new Vector3(0f, -1.25f, 0f), Vector3.zero, Vector3.one * 0.14f);
            Part("Collar", pivot, LowPoly.Prism(6, 0.09f, 0.09f, 0.18f), trimMat, new Vector3(0f, 1.62f, 0f), Vector3.zero, Vector3.one);

            // Lame courbe : tranchant (extérieur) et dos (intérieur), vers l'avant puis vers le bas
            Vector3[] edge =
            {
                new Vector3(0f, 1.8f, 0f), new Vector3(0.45f, 1.95f, 0f), new Vector3(0.95f, 1.92f, 0f),
                new Vector3(1.4f, 1.72f, 0f), new Vector3(1.72f, 1.38f, 0f), new Vector3(1.85f, 1.02f, 0f),
            };
            float[] width = { 0.32f, 0.34f, 0.3f, 0.22f, 0.12f, 0.0f };
            List<Vector3> v = new List<Vector3>();
            List<Vector3> glowV = new List<Vector3>();
            float th = 0.035f;
            Vector3 inner(int k)
            {
                Vector3 tan = (edge[Mathf.Min(k + 1, edge.Length - 1)] - edge[Mathf.Max(k - 1, 0)]).normalized;
                Vector3 n = new Vector3(tan.y, -tan.x, 0f); // vers l'intérieur de la courbe (en bas)
                return edge[k] + n * width[k];
            }
            for (int k = 0; k < edge.Length - 1; k++)
            {
                Vector3 o0 = edge[k], o1 = edge[k + 1], i0 = inner(k), i1 = inner(k + 1);
                Vector3 f = Vector3.back * th, b = Vector3.forward * th;
                // Faces avant et arrière (légèrement bombées au milieu)
                Vector3 m0 = (o0 + i0) / 2f, m1 = (o1 + i1) / 2f;
                LowPoly.Quad(v, o0, o1, m1 + f, m0 + f, Vector3.back);
                LowPoly.Quad(v, m0 + f, m1 + f, i1, i0, Vector3.back);
                LowPoly.Quad(v, o0, o1, m1 + b, m0 + b, Vector3.forward);
                LowPoly.Quad(v, m0 + b, m1 + b, i1, i0, Vector3.forward);
                // Liseré lumineux le long du tranchant
                Vector3 outN = (o0 - i0).normalized * 0.05f, outN1 = (o1 - i1).normalized * 0.05f;
                LowPoly.Quad(glowV, o0, o1, o1 + outN1, o0 + outN, Vector3.back);
                LowPoly.Quad(glowV, o0, o1, o1 + outN1, o0 + outN, Vector3.forward);
            }
            Mesh blade = LowPoly.Build("Blade", v);
            Track(blade);
            Mesh edgeMesh = LowPoly.Build("BladeEdge", glowV);
            Track(edgeMesh);
            Part("Blade", pivot, blade, bladeMat, Vector3.zero, Vector3.zero, Vector3.one, false);
            Part("BladeEdge", pivot, edgeMesh, glowMat, Vector3.zero, Vector3.zero, Vector3.one, false);

            bladePoints.Clear();
            bladePoints.AddRange(edge);
            bladePoints.Add(new Vector3(0f, 1.6f, 0f));
        }


        // Place les pièces selon la pose courante
        private void ApplyPose(float dt, float time)
        {
            float fwdVel = 0f;
            if (dt > 0f)
            {
                // Vitesse réelle du corps (sert à l'inclinaison, à la queue, à l'étirement)
                Vector3 raw = Vector3.ClampMagnitude((transform.position - lastPos) / dt, 40f);
                lastPos = transform.position;
                bodyVel = Vector3.Lerp(bodyVel, raw, 1f - Mathf.Exp(-12f * dt));
                fwdVel = bodyVel.x * facing;

                // Faux : ressort. Coup rapide = peu amorti -> elle dépasse puis revient (accompagnement du geste)
                float wDamp = sharpness >= 20f ? 0.42f : 0.8f;
                Spring(ref weaponAngle, ref weaponVel, weaponTarget, sharpness * 0.33f, wDamp, dt);
                // Corps lourd : se penche dans le sens du mouvement, respire légèrement
                float lean = Mathf.Clamp(fwdVel * 1.2f, -18f, 25f) + Mathf.Sin(time * 0.9f) * 2f;
                Spring(ref tilt, ref tiltVel, tiltTarget + lean, 2.4f, 0.6f, dt);
                Spring(ref handRaise, ref handVel, handRaiseTarget, 2.8f, 0.7f, dt);
                punch = Mathf.Max(0f, punch - dt * 8f);
                Spring(ref punchPos, ref punchVel, punch, 5f, 0.35f, dt);
                glow = Mathf.Lerp(glow, glowTarget, 1f - Mathf.Exp(-10f * dt));
                flash = Mathf.Max(0f, flash - dt * 4f);

                // Tête : regarde le joueur et compense en partie l'inclinaison du corps (en retard sur le corps)
                float look = 0f;
                if (target != null && head != null)
                {
                    Vector3 d = target.transform.position - head.position;
                    look = Mathf.Clamp(Mathf.Atan2(d.y, Mathf.Abs(d.x) + 0.01f) * Mathf.Rad2Deg * 0.5f, -18f, 14f);
                }
                Spring(ref headAngle, ref headVel, look + tilt * 0.45f, 3f, 0.55f, dt);
                // Queue : traîne derrière le mouvement, pend vers le bas, ondule
                float sway = -Mathf.Clamp(fwdVel * 3f, -40f, 40f) + tilt * 0.6f + Mathf.Sin(time * 1.7f) * 6f;
                Spring(ref tailSway, ref tailVel, sway, 1.8f, 0.35f, dt);
                // Étiré quand il file à la verticale ; s'écrase tout seul à l'arrêt (le ressort dépasse)
                float stretch = -Mathf.Clamp(Mathf.Abs(bodyVel.y) * 0.006f, 0f, 0.14f);
                Spring(ref squash, ref squashVel, stretch, 3f, 0.3f, dt);

                // Demi-tour : il pivote face caméra puis de l'autre côté
                flip = Mathf.MoveTowards(flip, facing, dt * 4.5f);
                crownSpin += dt * (20f + phase * 15f);
                // Métamorphose : grandit lentement vers la taille de la phase
                curScale = Mathf.Lerp(curScale, PhaseScale(), 1f - Mathf.Exp(-1.5f * dt));
                hornGrow = Mathf.Lerp(hornGrow, phase >= 3 ? 1f : 0f, 1f - Mathf.Exp(-3f * dt));
            }
            else
            {
                weaponAngle = weaponTarget;
                tilt = tiltTarget;
                handRaise = handRaiseTarget;
                weaponVel = tiltVel = handVel = headVel = tailVel = squashVel = 0f;
                headAngle = tailSway = squash = punchPos = 0f;
                lastPos = transform.position;
                flip = facing;
                curScale = PhaseScale();
                hornGrow = phase >= 3 ? 1f : 0f;
            }

            // Pièces de phase : apparaissent avec un rebond
            for (int i = 0; i < phaseParts.Count; i++)
            {
                float goal = phase >= phasePartMin[i] ? 1f : 0f;
                float g = phasePartGrow[i];
                if (dt > 0f)
                {
                    float v = phasePartVel[i];
                    v += (90f * (goal - g) - 10f * v) * dt;
                    g += v * dt;
                    phasePartVel[i] = v;
                }
                else g = goal;
                phasePartGrow[i] = g;
                bool show = g > 0.01f;
                if (phaseParts[i].gameObject.activeSelf != show) phaseParts[i].gameObject.SetActive(show);
                phaseParts[i].localScale = phasePartBase[i] * Mathf.Max(g, 0f);
            }
            foreach (Transform h in horns) h.localScale = Vector3.one * (1f + hornGrow * 0.7f);
            for (int w = 0; w < wings.Count; w++)
            {
                // Battement lent des ailes
                float side = w == 0 ? 1f : -1f;
                float flap = Mathf.Sin(time * (1.6f + Mathf.Abs(fwdVel) * 0.15f));
                wings[w].localRotation = Quaternion.Euler(side * flap * 8f, 0f, flap * 10f - Mathf.Clamp(fwdVel * 2f, -25f, 25f) + squash * 40f);
            }
            if (orbit != null) orbit.localRotation = Quaternion.Euler(18f, time * 70f, 0f);
            if (bladeFire != null)
            {
                var fm = bladeFire.main;
                fm.startColor = new ParticleSystem.MinMaxGradient(PhaseColor(), Color.Lerp(PhaseColor(), Color.white, 0.5f));
                var fe = bladeFire.emission;
                fe.rateOverTime = phase >= 3 ? 70f : 0f;
            }

            // Miroir selon le côté du joueur (la faux reste côté caméra), et un peu tourné vers la caméra
            // Demi-tour : vraie rotation. À mi-chemin il fait face à la caméra (yaw 90) ; là, le miroir
            // ne change que la profondeur, pas la silhouette : le passage de l'autre côté est invisible.
            float sgn = Mathf.Abs(flip) < 1e-4f ? Mathf.Sign(facing) : Mathf.Sign(flip);
            float m01 = Mathf.SmoothStep(0f, 1f, Mathf.Abs(flip));
            float yaw = Mathf.Lerp(90f, sgn * turnToCamera, m01);
            visual.transform.localScale = new Vector3(sgn * (1f + squash * 0.5f), 1f - squash, 1f + squash * 0.5f) * curScale * deathFade;
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Lévitation lente et lourde (deux ondes : moins mécanique)
            float bob = Mathf.Sin(time * 1.3f) * 0.08f + Mathf.Sin(time * 0.71f + 1.3f) * 0.04f;
            visual.transform.localPosition = new Vector3(0f, bob * curScale, 0f);
            bodyT.localRotation = Quaternion.Euler(0f, 0f, -tilt);
            bodyT.localScale = new Vector3(1f, 1f + Mathf.Sin(time * 1.3f + 0.6f) * 0.015f, 1f); // respiration
            head.localRotation = Quaternion.Euler(0f, 0f, headAngle);
            tailPivot.localRotation = Quaternion.Euler(0f, 0f, tailSway);

            // Flammes de la queue
            tail.localScale = new Vector3(1f + Mathf.Sin(time * 13f) * 0.06f, 1f + Mathf.Sin(time * 9f) * 0.12f + phase * 0.05f, 1f + Mathf.Cos(time * 11f) * 0.06f);
            core.localScale = Vector3.one * (0.3f + Mathf.Sin(time * (4f + phase)) * 0.03f + glow * 0.05f);

            // Couronne : 5 pointes en phase I, 9 en phase V, tourne de plus en plus vite
            crown.localRotation = Quaternion.Euler(0f, 0f, crownSpin);
            int shown = 5 + phase;
            for (int i = 0; i < crownSpikes.Count; i++)
            {
                bool on = i < shown;
                if (crownSpikes[i].gameObject.activeSelf != on) crownSpikes[i].gameObject.SetActive(on);
                crownSpikes[i].localRotation = Quaternion.Euler(0f, 0f, i * 360f / shown);
                crownSpikes[i].localScale = Vector3.one * (1f + glow * 0.08f);
            }

            // Mains
            // La main suit la faux (levée = main haute en arrière, frappe = main basse en avant) et le corps
            Vector3 frontPos = HandLocal(weaponAngle, tilt) + new Vector3(0f, Mathf.Sin(time * 2.3f) * 0.03f, 0f);
            frontHand.localPosition = frontPos;
            Vector3 backRest = new Vector3(0.35f, -0.05f + Mathf.Sin(time * 2.1f + 1f) * 0.05f, 0.75f);
            Vector3 backCast = new Vector3(0.7f, 1.2f, 0.6f);
            Vector3 back = Vector3.LerpUnclamped(backRest, backCast, handRaise) + Vector3.right * punchPos * 0.35f;
            backHand.localPosition = Quaternion.Euler(0f, 0f, -tilt) * back;

            weaponPivot.localPosition = frontPos;
            weaponPivot.localScale = Vector3.one * bladeScale;
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, weaponAngle);

            // Couleurs : lueur selon la phase, flash blanc quand il est touché
            Color gc = PhaseColor();
            float pulse = 1.6f + Mathf.Sin(time * 4f) * 0.3f + glow * 1.5f;
            for (int i = 0; i < mats.Count; i++)
            {
                Material m = mats[i];
                Color baseC = m == glowMat ? gc : baseColors[i];
                // L'armure noircit et rougeoie au fil des phases
                if (m == armorMat) baseC = Color.Lerp(armorColor, new Color(0.05f, 0.01f, 0.02f), phase / 4f);
                if (m == trimMat) baseC = Color.Lerp(trimColor, rageColor * 0.55f, phase / 4f);
                m.color = Color.Lerp(baseC, Color.white, flash);
                if (m == glowMat) m.SetColor("_EmissionColor", Color.Lerp(gc * pulse, Color.white * 3f, flash));
                else m.SetColor("_EmissionColor", Color.white * flash * 1.5f);
            }

            if (embers != null)
            {
                var main = embers.main;
                main.startColor = gc;
                var emission = embers.emission;
                emission.rateOverTime = 12f + phase * 10f + glow * 15f;
            }
        }

        // ---------- Construction ----------

        private Transform Empty(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            Track(go);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private Transform Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 size, bool trackMesh = true)
        {
            if (trackMesh) Track(mesh);
            GameObject go = new GameObject(name);
            Track(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private Material MakeMat(Color c, float gloss, bool glowing)
        {
            Material m = MaterialFactory.Lit(c);
            m.SetFloat("_Glossiness", gloss);
            m.EnableKeyword("_EMISSION"); // aussi pour le flash blanc
            m.SetColor("_EmissionColor", glowing ? c * 2f : Color.black);
            Track(m);
            mats.Add(m);
            baseColors.Add(c);
            return m;
        }

        private void Track(Object o)
        {
            generated.Track(o);
        }

        // =====================================================================
        //  Barre de vie (5 segments) et bannière de phase
        // =====================================================================

        private void OnGUI()
        {
            if (!Application.isPlaying || !active) return;

            float w = Mathf.Min(Screen.width * 0.8f, 1100f);
            float h = 30f;
            float x = (Screen.width - w) / 2f;
            float y = Screen.height - 64f;
            Color pc = PhaseColor();

            // Nom + phase courante
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            GUIStyle phaseStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Italic, alignment = TextAnchor.LowerRight };
            GuiDraw.ShadowLabel(new Rect(x, y - 40f, w, 36f), bossName, nameStyle, pc);
            GuiDraw.ShadowLabel(new Rect(x, y - 36f, w, 30f), "Phase " + roman[phase] + " — " + phaseNames[phase], phaseStyle, new Color(1f, 0.9f, 0.8f));

            Color old = GUI.color;
            // Cadre
            GuiDraw.FilledRect(new Rect(x - 4f, y - 4f, w + 8f, h + 8f), new Color(0f, 0f, 0f, 0.85f));
            GuiDraw.FilledRect(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), Color.Lerp(trimColor, pc, 0.4f));
            GuiDraw.FilledRect(new Rect(x, y, w, h), new Color(0.12f, 0.02f, 0.03f));
            // Dégâts récents (traînée claire), puis vie
            GuiDraw.FilledRect(new Rect(x, y, w * Mathf.Clamp01(trailHealth / maxHealth), h), new Color(1f, 0.85f, 0.55f));
            GuiDraw.FilledRect(new Rect(x, y, w * Mathf.Clamp01(health / maxHealth), h), Color.Lerp(pc, Color.white, flash * 0.5f));
            // Reflet en haut de la barre
            GuiDraw.FilledRect(new Rect(x, y, w * Mathf.Clamp01(health / maxHealth), h * 0.3f), new Color(1f, 1f, 1f, 0.18f));
            // Séparations des 5 phases
            for (int i = 1; i < 5; i++)
            {
                GuiDraw.FilledRect(new Rect(x + w * i / 5f - 2f, y - 3f, 4f, h + 6f), new Color(0f, 0f, 0f, 0.9f));
            }
            GUI.color = old;

            // Bannière au centre de l'écran au début de chaque phase
            float bt = Time.unscaledTime - bannerTime;
            if (bt >= 0f && bt < 3f && !dead)
            {
                float a = Mathf.Clamp01(bt / 0.3f) * Mathf.Clamp01((3f - bt) / 0.8f);
                GUIStyle big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                GUIStyle sub = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
                float cy = Screen.height * 0.3f;
                GuiDraw.FilledRect(new Rect(0f, cy - 70f, Screen.width, 150f), new Color(0f, 0f, 0f, 0.55f * a));
                Color c = pc; c.a = a;
                GuiDraw.ShadowLabel(new Rect(0f, cy - 60f, Screen.width, 80f), "PHASE " + roman[phase], big, c);
                GuiDraw.ShadowLabel(new Rect(0f, cy + 20f, Screen.width, 40f), phaseNames[phase], sub, new Color(1f, 0.92f, 0.85f, a));
            }
        }
    }
}
