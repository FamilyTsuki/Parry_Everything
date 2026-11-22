using System.Collections.Generic;
using System.Collections;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Boss de la zone 1. Visuel : « la Jarre aux âmes » (JarVisual, modèle Assets/Models/Jar/jar.blend) : un vieux bol
    // grec resté sous l'eau, rempli de chair, que deux longs bras humains traînent au sol. On ne peut le blesser qu'en PARANT.
    // Il rampe en se tirant avec ses bras (JarVisual : mains plantées au sol, cinématique inverse), frappe le sol
    // et lance des cailloux — puis, en phase V, les morceaux de son propre bol.
    //
    // 5 phases (barre de vie en 5 segments). Chaque phase ajoute des attaques et accélère le boss :
    //   I   : frappe d'une main, cailloux lancés
    //   II  : + écrasement à deux mains (ondes de choc), pics de roche, Jugement (rocher échangé en parades)
    //   III : + pluie de pierres (zones marquées au sol), caillou qui se divise
    //   IV  : + bond (il se propulse avec les bras), double Jugement
    //   V   : + échange truqué (feintes, accélération), bras de fer ; il lance les morceaux de son bol
    // Entre deux phases : ralenti, rugissement, il grandit et la caméra recule. Le bol se brise :
    //   II  les morceaux se disjoignent (fentes sur la chair)   III, IV  des morceaux tombent   V  plus de bol
    // Aperçu des phases dans l'éditeur : champ « previewPhase ». Visible dans l'éditeur hors Play ([ExecuteAlways]).
    [ExecuteAlways]
    public class HellGuardian : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private string bossName = "GARDIEN DES ENFERS";
        [SerializeField] private string bossId = "HellGuardian";   // identifiant dans la sauvegarde (boss vaincus)
        [SerializeField] private float maxHealth = 250f;
        [SerializeField] private float activationRange = 16f;     // le combat commence quand le joueur approche
        [SerializeField] private float groundY = -0.45f;
        [SerializeField] private float hoverHeight = 0.95f;       // hauteur du centre au-dessus du sol (en unités de scale) : bol posé
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float preferredDistance = 7f;    // distance de repos par rapport au joueur
        [SerializeField] private float hitRadius = 0.6f;          // portée des coups autour du centre du joueur

        [Header("Frappe d'une main")]
        [SerializeField] private float swingWindup = 0.65f;
        [SerializeField] private float swingDamage = 20f;
        [SerializeField] private float swingParryDamage = 12f;    // dégâts subis par le boss quand on pare la frappe
        [SerializeField] private float staggerDuration = 1.3f;

        [Header("Cailloux lancés")]
        [SerializeField] private int orbCount = 3;
        [SerializeField] private float orbSpeed = 7f;
        [SerializeField] private float orbDamage = 10f;
        [SerializeField] private float orbReflectDamage = 8f;

        [Header("Écrasement à deux mains, ondes de choc")]
        [SerializeField] private float slamDamage = 15f;
        [SerializeField] private float slamParryDamage = 10f;
        [SerializeField] private float shockwaveSpeed = 9f;
        [SerializeField] private float shockwaveDamage = 10f;

        [Header("Pluie de pierres (phase III+)")]
        [SerializeField] private int meteorCount = 6;
        [SerializeField] private float meteorDamage = 12f;
        [SerializeField] private float meteorReflectDamage = 6f;

        [Header("Bond (phase IV+)")]
        [SerializeField] private float dashDamage = 20f;
        [SerializeField] private float dashParryDamage = 14f;

        [Header("Pics de roche (phase II+)")]
        [SerializeField] private float pillarDamage = 18f;

        [Header("Jugement (phase II+)")]
        [SerializeField] private float judgmentDamage = 30f;
        [SerializeField] private float judgmentReflectDamage = 25f;
        [SerializeField] private int judgmentRallies = 2;         // fois où le boss renvoie l'orbe avant de la prendre

        [Header("Échanges : caillou qui se divise (III+), double Jugement (IV+), échange truqué (V)")]
        [SerializeField] private float splitDamage = 14f;
        [SerializeField] private float splitReflectDamage = 24f;  // partagé entre les morceaux finaux
        [SerializeField] private int splitCount = 2;              // divisions : 1, 2 puis 4 bulles
        [SerializeField] private float splitStagger = 0.35f;      // écart entre deux morceaux
        [SerializeField] private float doubleDamage = 20f;
        [SerializeField] private float doubleReflectDamage = 14f; // par bulle
        [SerializeField] private float doubleOffset = 0.55f;      // écart entre les deux bulles
        [SerializeField] private float riggedDamage = 25f;
        [SerializeField] private float riggedReflectDamage = 35f;
        [SerializeField] private int riggedRallies = 4;
        [SerializeField] private float riggedSpeedUp = 1.18f;     // vitesse ×1,18 à chaque renvoi
        [SerializeField] private float riggedFeintChance = 0.5f;

        [Header("Bras de fer (phase V)")]
        [SerializeField] private float tugDamage = 35f;
        [SerializeField] private float tugReflectDamage = 40f;
        [SerializeField] private int tugParries = 4;              // parades pour l'envoyer dans le boss
        [SerializeField] private float tugSpeed = 3f;
        [SerializeField] private float tugSpeedUp = 1.2f;         // ajoutée à chaque parade

        [Header("Apparence")]
        [SerializeField] private float scale = 1.8f;              // taille en phase I ; il grandit à chaque phase
        [SerializeField] private float growthPerPhase = 0.09f;    // +9 % de taille par phase
        [SerializeField] private float turnToCamera = 25f;
        [SerializeField] private GameObject jarModel;             // Assets/Models/Jar/jar.blend
        [SerializeField] private Color glowColor = new Color(1f, 0.5f, 0.1f);     // phase I
        [SerializeField] private Color rageColor = new Color(1f, 0.05f, 0.12f);   // phase V
        [Range(0, 4)] public int previewPhase = 0;              // éditeur hors Play : voir le boss à une phase donnée
        [SerializeField] private bool labMode;                  // scène AnimationLab : pas de combat, gestes joués à la demande

        // Gestes jouables un par un dans la scène AnimationLab
        // (noms historiques : Swing = frappe d'une main, Orbs = cailloux, MeteorRain = pluie de pierres, Dash = bond)
        public enum LabMove { Swing, Orbs, Slam, MeteorRain, Dash, Pillars, Judgment, Roar, Stagger, PhaseTransition, Hit, Die,
            SplitBubble, DoubleJudgment, RiggedRally, TugOfWar }

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
        private float crawlWant;                // distance (x) jusqu'au but de MoveToward, lue à l'image suivante
        private float crawlSpeed;
        private bool crawlWanted;
        private float bannerTime = -100f;
        private BossProjectile judgmentOrb;
        private bool phaseChanged;              // changement de phase à lancer au prochain Update
        private bool dieStarted;
        private Vector3 playerVel;              // vitesse du joueur (visée anticipée)
        private Vector3 lastPlayerPos;

        public bool IsDead { get { return dead; } }
        public string BossId => bossId;
        public bool IsActive { get { return active; } }
        public int Phase { get { return phase; } }
        public Vector3 CorePosition { get { return core != null ? core.position : transform.position; } }
        public float GroundY => groundY;

        // ---------- Pose (les coroutines changent les cibles, Update lisse) ----------
        private float tilt, tiltTarget = 4f;                    // inclinaison du bol (+ = en avant)
        private float flash;                                    // flash blanc quand il est touché
        private float deathFade = 1f;
        // Ressorts (position + vitesse) : élan, inertie et léger dépassement au lieu de mouvements secs
        private float tiltVel;
        private float squash, squashVel;
        private Vector3 lastPos, bodyVel;

        // ---------- Visuel ----------
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private GameObject visual;
        private JarVisual jar;
        private Transform bodyT, core;
        private float curScale = 1.8f;                          // taille actuelle (grandit à chaque phase)

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
            jar = null;
            visual = null;
            if (Application.isPlaying) TimeControl.SetEffectScale(1f);
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
            if (labMode)
            {
                LabStart();
                return;
            }
            // Déjà vaincu dans cette partie : il ne revient pas
            if (GameSession.IsBossDefeated(bossId))
            {
                gameObject.SetActive(false);
                return;
            }
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
                TimeControl.SetEffectScale(1f);
                StartCoroutine(Die());
            }
            else if (phaseChanged && labMode)
            {
                phaseChanged = false;
                invulnerable = false;
            }
            else if (phaseChanged && !dead)
            {
                phaseChanged = false;
                StopAllCoroutines();
                TimeControl.SetEffectScale(1f);
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
            // Temps arrêté (pause, arrêt sur image d'une parade) : on garde la pose telle quelle
            // (dt 0 = aperçu éditeur : remettrait les mains au repos d'un coup)
            if (Time.deltaTime > 0f) ApplyPose(Time.deltaTime, Time.time);
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
            // 0 frappe, 1 cailloux, 2 écrasement, 3 pluie de pierres, 4 bond, 5 jugement, 6 pics de roche,
            // 7 caillou qui se divise, 8 double jugement, 10 échange truqué, 11 bras de fer
            List<int> pool = new List<int> { 0, 1 };
            bool exchangeFree = judgmentOrb == null && RallyBubble.ActiveCount == 0 && PushBubble.ActiveCount == 0;
            if (phase >= 1) { pool.Add(2); pool.Add(6); }
            if (phase >= 1 && exchangeFree) pool.Add(5);
            if (phase >= 2) pool.Add(3);
            if (phase >= 2 && exchangeFree) pool.Add(7);
            if (phase >= 3) pool.Add(4);
            if (phase >= 3 && exchangeFree) pool.Add(8);
            if (phase >= 4 && exchangeFree) { pool.Add(10); pool.Add(11); }
            if (DistToPlayer() < Reach(FrontArm) * 0.8f) pool.Add(0); // proche : la frappe plus souvent
            if (pool.Count > 1) pool.RemoveAll(a => a == lastAttack && pool.Count > 1);

            int pick = pool[Random.Range(0, pool.Count)];
            lastAttack = pick;
            switch (pick)
            {
                case 0: yield return Slap(); break;
                case 1: yield return ThrowRocks(); break;
                case 2: yield return Slam(); break;
                case 3: yield return RockRain(); break;
                case 4: yield return Leap(); break;
                case 5: yield return Judgment(); break;
                case 6: yield return Pillars(); break;
                case 7: yield return SplitBubble(); break;
                case 8: yield return DoubleJudgment(); break;
                case 10: yield return RiggedRally(); break;
                case 11: yield return TugOfWar(); break;
            }
        }

        // Sort de terre en s'agrippant au sol, puis rugit
        private IEnumerator Intro()
        {
            invulnerable = true;
            bannerTime = Time.unscaledTime;
            SetVisible(true);
            Framing();
            Shake(1.6f, 0.15f);
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

        // Les deux bras se dressent, tremblent, puis frappent le sol ensemble
        private IEnumerator Roar(float duration)
        {
            tiltTarget = -14f;
            flash = 0.6f;
            Vector3 f0 = ShoulderPos(FrontArm), b0 = ShoulderPos(BackArm);
            Vector3 fUp = ArmPoint(FrontArm, f0.x + facing * 0.25f * Reach(FrontArm), f0.y + 0.85f * Reach(FrontArm));
            Vector3 bUp = ArmPoint(BackArm, b0.x - facing * 0.25f * Reach(BackArm), b0.y + 0.85f * Reach(BackArm));
            yield return ArmsMove(fUp, bUp, 0.35f, false);
            float t = 0f;
            Shake(duration * 0.8f, 0.25f);
            SpawnBurst(60, 10f, 0.8f);
            while (t < duration * 0.7f)
            {
                Vector3 j = Random.insideUnitSphere * 0.08f * curScale;
                ArmTo(FrontArm, fUp + j, 0.3f);
                ArmTo(BackArm, bUp - j, 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            yield return ArmsMove(RestPlant(FrontArm), RestPlant(BackArm), 0.12f, true);
            Impact(HandPos(FrontArm), 0.3f);
            Impact(HandPos(BackArm), 0f);
            ReleaseArms();
            ResetPose();
            yield return new WaitForSeconds(0.3f);
        }

        // Passage à la phase suivante : ralenti, bannière, recul, rugissement
        private IEnumerator PhaseTransition()
        {
            invulnerable = true;
            ReleaseArms();
            ResetPose();
            // Laisse finir l'arrêt du temps de la parade du joueur
            yield return new WaitForSecondsRealtime(0.12f);
            bannerTime = Time.unscaledTime;
            flash = 1f;
            Framing();
            Shake(1f, 0.3f);
            SpawnBurst(80, 14f, 0.6f);
            TimeControl.SetEffectScale(0.3f);
            yield return new WaitForSecondsRealtime(0.7f);
            TimeControl.SetEffectScale(1f);

            // Recule en rampant
            float t = 0f;
            while (t < 0.9f && target != null)
            {
                float side = target.transform.position.x < transform.position.x ? 1f : -1f;
                MoveToward(target.transform.position.x + side * preferredDistance, HoverY(), 2f);
                t += Time.deltaTime;
                yield return null;
            }
            yield return Roar(1.1f);
            invulnerable = false;
        }

        private void ResetPose()
        {
            tiltTarget = 4f;
            lockFacing = false;
        }

        // Se replace à bonne distance du joueur, en rampant
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

        // Rampe jusqu'à ce que le joueur soit à 'distance' (ou au bout de 'timeout')
        private IEnumerator CrawlInRange(float distance, float timeout)
        {
            float t = 0f;
            while (t < timeout && target != null && !dead)
            {
                float dx = target.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) <= distance) break;
                MoveToward(target.transform.position.x - Mathf.Sign(dx) * distance * 0.85f, HoverY(), 1.6f);
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ---------- Frappe d'une main (phase I+ ; combo de 2 en IV, 3 en V) ----------
        // La main se dresse au-dessus du joueur (elle le suit), tremble, puis s'abat. Phase II+ : onde de choc.
        private IEnumerator Slap()
        {
            yield return CrawlInRange(Reach(FrontArm) * 0.75f, 1.5f);
            int slaps = phase >= 4 ? 3 : (phase >= 3 ? 2 : 1);
            for (int s = 0; s < slaps && !dead && target != null; s++)
            {
                int arm = s % 2 == 0 ? FrontArm : BackArm;
                float windup = (s == 0 ? swingWindup : swingWindup * 0.6f) / Spd;
                lockFacing = false;
                tiltTarget = -8f;
                float w = 0f;
                Vector3 up = HandPos(arm);
                while (w < windup)
                {
                    float u = w / windup;
                    // Main dressée haut au-dessus de l'épaule, coude plié, penchée vers le joueur
                    Vector3 sh = ShoulderPos(arm);
                    float r = Reach(arm);
                    float toward = Mathf.Clamp((target.transform.position.x - sh.x) / r, -0.35f, 0.35f);
                    Vector3 goal = ArmPoint(arm, sh.x + toward * r, sh.y + (0.5f + 0.12f * u) * r);
                    up = Vector3.Lerp(up, goal, 1f - Mathf.Exp(-14f * Time.deltaTime));
                    ArmTo(arm, up + Random.insideUnitSphere * 0.04f * u * curScale, 0.5f);
                    w += Time.deltaTime;
                    yield return null;
                }

                // S'abat sur le joueur (aussi loin que le bras porte) : rapide, accélérée ; testée sur tout son trajet
                lockFacing = true;
                tiltTarget = 12f;
                Vector3 from = up;
                Vector3 shoulder = ShoulderPos(arm);
                float reach = Reach(arm);
                float strikeX = shoulder.x + Mathf.Clamp(target.transform.position.x - shoulder.x, -reach * 0.85f, reach * 0.85f);
                Vector3 to = ArmPoint(arm, strikeX, groundY + 0.15f * curScale);
                Vector3 prev = HandPos(arm);
                float dur = 0.11f / Spd;
                bool hasHit = false;
                float t = 0f;
                while (t < dur)
                {
                    float u = Mathf.Clamp01(t / dur);
                    ArmTo(arm, Vector3.LerpUnclamped(from, to, u * u), 1f);
                    yield return null;
                    Vector3 cur = HandPos(arm);
                    if (!hasHit && SweepTouchesPlayer(prev, cur, hitRadius + 0.3f * curScale, out Vector3 point))
                    {
                        hasHit = true;
                        if (!target.TakeDamage(swingDamage * Dmg, transform, point))
                        {
                            TakeHit(swingParryDamage);
                            if (phaseChanged || dead) yield break;
                            ReleaseArms();
                            yield return Stagger();
                            yield break;
                        }
                    }
                    prev = cur;
                    t += Time.deltaTime;
                }
                ArmTo(arm, to, 1f);
                Impact(to, 0.25f);
                // Au sol : le joueur juste à côté de la main est pris aussi
                if (!hasHit && NearGround(to.x, 0.9f * curScale, 1.2f * curScale, out Vector3 near))
                {
                    if (!target.TakeDamage(swingDamage * Dmg, transform, near))
                    {
                        TakeHit(swingParryDamage);
                        if (phaseChanged || dead) yield break;
                        ReleaseArms();
                        yield return Stagger();
                        yield break;
                    }
                }
                if (phase >= 1)
                {
                    float dir = Mathf.Sign(target.transform.position.x - to.x);
                    if (dir == 0f) dir = facing;
                    BossProjectile.SpawnShockwave(new Vector3(to.x + dir * 0.6f, groundY, 0f), dir, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1.1f, ProjectileLook.Rock());
                }
                yield return new WaitForSeconds(0.18f / Spd);
                ArmRelease(arm);
            }
            yield return new WaitForSeconds(0.2f);
            ResetPose();
        }

        // Chancelle après une parade : le bol tangue en arrière et glisse
        private IEnumerator Stagger()
        {
            lockFacing = true;
            ReleaseArms();
            tiltTarget = -22f;
            tiltVel -= 220f;     // encaisse le choc
            float t = 0f;
            float v = -facing * 7f;
            while (t < staggerDuration && !dead)
            {
                transform.position += new Vector3(v * Time.deltaTime, 0f, 0f);
                v *= Mathf.Exp(-6f * Time.deltaTime);   // glisse puis s'arrête
                t += Time.deltaTime;
                yield return null;
            }
            ResetPose();
        }

        // Point visé avec anticipation : là où le joueur sera quand le projectile arrivera
        private Vector3 Lead(Vector3 from, float speed, float amount)
        {
            Vector3 pp = target.HitCenter;          // le corps, pas les pieds
            float time = Vector2.Distance(from, pp) / Mathf.Max(speed, 0.1f);
            Vector3 p = pp + new Vector3(playerVel.x, playerVel.y * 0.3f, 0f) * time * amount;
            p.y = Mathf.Max(p.y, groundY + 0.3f);
            return p;
        }

        // Le segment parcouru par la main (a → b) touche-t-il le joueur (bas, centre, haut du corps) ?
        private bool SweepTouchesPlayer(Vector3 a, Vector3 b, float radius, out Vector3 point)
        {
            point = b;
            if (target == null) return false;
            Vector2 pc = target.HitCenter;
            Vector2[] body = { pc, pc + Vector2.up * 0.5f, pc - Vector2.up * 0.5f };
            foreach (Vector2 p in body)
            {
                if (DistToSegment(p, a, b) < radius)
                {
                    point = Vector3.Lerp(b, (Vector3)p, 0.5f);
                    return true;
                }
            }
            return false;
        }

        // Le joueur est-il au sol près de x (à moins de 'width' et sous 'height') ?
        private bool NearGround(float x, float width, float height, out Vector3 point)
        {
            point = new Vector3(x, groundY, 0f);
            if (target == null) return false;
            Vector3 pp = target.transform.position;
            if (Mathf.Abs(pp.x - x) > width || pp.y - groundY > height) return false;
            point = new Vector3(Mathf.Lerp(x, pp.x, 0.5f), pp.y, 0f);
            return true;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------- Lancer (cailloux ; morceaux du bol en phase V) ----------
        // Main qui lance : la main avant (côté joueur) ; l'autre reste plantée derrière, en appui.
        private const int ThrowArm = JarVisual.Front;
        private const int OtherArm = JarVisual.Back;
        private readonly int[] reservedShard = { -1, -1 };

        // Apparence du prochain projectile de la main 'arm' : caillou ; en phase V, un vrai morceau du bol posé
        // au sol à portée de main (réservé ici, ramassé par Scoop) ; s'il n'y en a plus à portée, un caillou.
        private ProjectileLook Look(int arm)
        {
            reservedShard[arm] = -1;
            if (phase >= 4 && jar != null)
            {
                int id = jar.FindShard(ShoulderPos(arm), Reach(arm) * 0.9f, reservedShard[1 - arm]);
                if (id >= 0)
                {
                    reservedShard[arm] = id;
                    return ProjectileLook.Shard(jar.ShardMesh(id), jar.PotteryMaterial);
                }
            }
            return ProjectileLook.Rock();
        }

        // Projectile qui ne passe pas par la main (pluie de pierres) : copie d'un morceau en phase V
        private ProjectileLook LookCopy()
        {
            if (phase >= 4 && jar != null)
                return ProjectileLook.Shard(jar.RandomShardMesh(), jar.PotteryMaterial);
            return ProjectileLook.Rock();
        }

        // Ramasse : la main va au sol en arc (sur le morceau réservé en phase V), fouille, et en ressort avec le projectile
        private IEnumerator Scoop(int arm, ProjectileLook look, float size, float speed)
        {
            int id = reservedShard[arm];
            bool shard = jar != null && jar.ShardAvailable(id);
            Vector3 g;
            if (shard)
            {
                g = jar.ShardPosition(id);
                g.y = RestPlant(arm).y;
            }
            else
            {
                g = RestPlant(arm);
                g.x = Mathf.Lerp(g.x, transform.position.x, 0.35f);
            }
            tiltTarget = 8f;
            yield return ArmArc(arm, g, 0.22f / speed, 0.18f * Reach(arm), 1f);
            Impact(g, 0f, 8);
            reservedShard[arm] = -1;
            if (jar != null)
            {
                if (shard) jar.TakeShard(id);
                Material m = Track(look.CreateMaterial());
                jar.Hold(arm, look.Mesh(), m, look.Scale(size));
            }
            yield return new WaitForSeconds(0.06f / speed);
        }

        // Lancer d'un seul geste : ramène le projectile contre l'épaule en armant le bras au-dessus du bol (le bol
        // se penche en arrière), un court élan qui tremble (signal), puis coup de fouet par-dessus l'épaule.
        private IEnumerator WindAndThrow(int arm, float speed, System.Action<Vector3> release)
        {
            Vector3 start = HandPos(arm);
            tiltTarget = -10f;
            yield return ArmPath(arm, 0.3f / speed, e => Curve(e, start, ShoulderRel(arm, 0.15f, 0.12f), ShoulderRel(arm, -0.38f, 0.45f)), Smooth, 0f);
            float hold = 0.12f / speed;
            float t = 0f;
            while (t < hold)
            {
                float u = t / hold;
                ArmTo(arm, ShoulderRel(arm, -0.38f - 0.05f * u, 0.45f) + Random.insideUnitSphere * 0.015f * curScale * u, 0f);
                t += Time.deltaTime;
                yield return null;
            }
            yield return Whip(arm, speed, release, true);
        }

        // Coup de fouet : la main part de là où elle est (armée), lâche le projectile devant l'épaule, continue
        // et retombe au sol devant le bol ; le bol bascule en avant. overhead : passe d'abord au-dessus de l'épaule.
        private IEnumerator Whip(int arm, float speed, System.Action<Vector3> release, bool overhead)
        {
            Vector3 start = HandPos(arm);
            tiltTarget = 14f;
            tiltVel += 60f;
            System.Func<float, Vector3> path = overhead
                ? (System.Func<float, Vector3>)(e => Curve(e, start, ShoulderRel(arm, 0.02f, 0.62f), ShoulderRel(arm, 0.48f, 0.3f), GroundAhead(arm)))
                : (e => Curve(e, start, ShoulderRel(arm, 0.48f, 0.3f), GroundAhead(arm)));
            yield return ArmPath(arm, (overhead ? 0.28f : 0.22f) / speed, path, Smooth, 0f, overhead ? 2f / 3f : 0.5f, () =>
            {
                if (release != null) release(ReleaseFrom(arm));
            });
            ArmTo(arm, GroundAhead(arm), 1f);
            Impact(GroundAhead(arm), 0f, 6);
        }

        private IEnumerator ThrowRocks()
        {
            int n = orbCount + phase;
            for (int i = 0; i < n && !dead && target != null; i++)
            {
                ProjectileLook look = Look(ThrowArm);
                float size = 0.7f + phase * 0.06f;
                int k = i;
                yield return Scoop(ThrowArm, look, size, Spd);
                yield return WindAndThrow(ThrowArm, Spd, from =>
                {
                    Vector3 dir = Lead(from, orbSpeed * Spd, 0.7f) - from;
                    dir.z = 0f;
                    // Léger éventail autour du joueur, plus large au fil des phases
                    dir = Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 1f : -1f) * k * (1.5f + phase * 0.8f)) * dir;
                    BossProjectile.SpawnOrb(from, dir, orbSpeed * Spd, orbDamage * Dmg, orbReflectDamage, this, PhaseColor(), size, look);
                });
                yield return new WaitForSeconds(0.08f / Spd);
            }
            ArmRelease(ThrowArm);
            yield return new WaitForSeconds(0.3f);
            ResetPose();
        }

        // ---------- Gestes du bras : chemins lisses ----------

        // Point dans le plan du bras, relatif à l'épaule : fx vers l'avant (côté joueur), fy vers le haut (× portée)
        private Vector3 ShoulderRel(int arm, float fx, float fy)
        {
            Vector3 s = ShoulderPos(arm);
            float r = Reach(arm);
            return ArmPoint(arm, s.x + facing * fx * r, s.y + fy * r);
        }

        // Main posée au sol un peu devant sa place de repos
        private Vector3 GroundAhead(int arm)
        {
            Vector3 g = RestPlant(arm);
            g.x += facing * 0.1f * Reach(arm);
            return g;
        }

        private static float Smooth(float u) => u * u * (3f - 2f * u);

        // Courbe qui passe par tous les points (Catmull-Rom), u de 0 à 1
        private static Vector3 Curve(float u, params Vector3[] p)
        {
            int n = p.Length - 1;
            float f = Mathf.Clamp01(u) * n;
            int i = Mathf.Min((int)f, n - 1);
            float t = f - i;
            Vector3 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, n)];
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t));
        }

        // La main suit un chemin recalculé à chaque image (il suit le corps qui bouge). ease : temps → avancée.
        // release : appelé une fois quand l'avancée dépasse releaseAt (ou à la fin).
        private IEnumerator ArmPath(int arm, float duration, System.Func<float, Vector3> path, System.Func<float, float> ease, float flatHand,
            float releaseAt = 2f, System.Action release = null)
        {
            float t = 0f;
            bool released = false;
            while (true)
            {
                float u = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                float e = ease(u);
                ArmTo(arm, path(e), flatHand);
                if (!released && e >= releaseAt)
                {
                    released = true;
                    if (release != null) release();
                }
                if (u >= 1f) break;
                t += Time.deltaTime;
                yield return null;
            }
            if (!released && release != null) release();
        }

        // De la main actuelle à 'to' en arc
        private IEnumerator ArmArc(int arm, Vector3 to, float duration, float height, float flatHand)
        {
            Vector3 from = HandPos(arm);
            yield return ArmPath(arm, duration, e => Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * height, Smooth, flatHand);
        }

        // ---------- Écrasement à deux mains (phase II+) ----------
        // S'approche, dresse les deux mains (la main avant au-dessus du joueur), puis les abat : ondes de choc.
        private IEnumerator Slam()
        {
            if (target == null) yield break;
            yield return CrawlInRange(Reach(FrontArm) * 0.6f, 1.4f);
            tiltTarget = -12f;
            float t = 0f;
            float hang = 0.75f / Spd;
            Vector3 fUp = HandPos(FrontArm), bUp = HandPos(BackArm);
            while (t < hang)
            {
                Vector3 fs = ShoulderPos(FrontArm), bs = ShoulderPos(BackArm);
                float fr = Reach(FrontArm), br = Reach(BackArm);
                float toward = Mathf.Clamp((target.transform.position.x - fs.x) / fr, -0.45f, 0.45f);
                // Il suit encore le joueur, puis se fige juste avant de frapper
                if (t < hang - 0.15f) fUp = Vector3.Lerp(fUp, ArmPoint(FrontArm, fs.x + toward * fr, fs.y + 0.55f * fr), 1f - Mathf.Exp(-10f * Time.deltaTime));
                Vector3 bGoal = ArmPoint(BackArm, bs.x - facing * 0.25f * br, bs.y + 0.55f * br);
                bUp = Vector3.Lerp(bUp, bGoal, 1f - Mathf.Exp(-10f * Time.deltaTime));
                float tremble = t / hang;
                ArmTo(FrontArm, fUp + Random.insideUnitSphere * 0.05f * tremble * curScale, 0.5f);
                ArmTo(BackArm, bUp + Random.insideUnitSphere * 0.05f * tremble * curScale, 0.5f);
                t += Time.deltaTime;
                yield return null;
            }
            flash = 0.4f;
            lockFacing = true;
            tiltTarget = 14f;
            // La main avant s'abat sur le joueur (aussi loin qu'elle porte), l'autre derrière le bol
            Vector3 fsh = ShoulderPos(FrontArm);
            float fx = fsh.x + Mathf.Clamp(target.transform.position.x - fsh.x, -Reach(FrontArm) * 0.85f, Reach(FrontArm) * 0.85f);
            Vector3 fTo = ArmPoint(FrontArm, fx, groundY + 0.15f * curScale);
            Vector3 bTo = ArmPoint(BackArm, bUp.x - facing * 0.3f * Reach(BackArm), groundY + 0.15f * curScale);
            yield return ArmsMove(fTo, bTo, 0.12f / Spd, true);
            squashVel += 4f;
            Impact(fTo, 0.35f, 40);
            Impact(bTo, 0f, 20);

            // Impact direct sous la main avant
            if (NearGround(fTo.x, 1.3f * curScale, 1.4f * curScale, out Vector3 point))
            {
                if (!target.TakeDamage(slamDamage * Dmg, transform, point))
                {
                    TakeHit(slamParryDamage);
                    if (phaseChanged || dead) yield break;
                    yield return Stagger();
                    yield break;
                }
            }

            // Ondes de choc depuis les deux mains, vers l'extérieur (plusieurs vagues aux phases avancées)
            int waves = phase >= 4 ? 3 : (phase >= 2 ? 2 : 1);
            for (int w = 0; w < waves; w++)
            {
                float out1 = Mathf.Sign(fTo.x - transform.position.x);
                float out2 = Mathf.Sign(bTo.x - transform.position.x);
                BossProjectile.SpawnShockwave(new Vector3(fTo.x + out1 * 0.6f, groundY, 0f), out1, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1.35f, ProjectileLook.Rock());
                BossProjectile.SpawnShockwave(new Vector3(bTo.x + out2 * 0.6f, groundY, 0f), out2, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1.35f, ProjectileLook.Rock());
                if (w < waves - 1)
                {
                    // Les mains se relèvent un peu et refrappent
                    yield return ArmsMove(fTo + Vector3.up * 1.2f * curScale, bTo + Vector3.up * 1.2f * curScale, 0.18f / Spd, true);
                    yield return ArmsMove(fTo, bTo, 0.08f / Spd, true);
                    Impact(fTo, 0.2f, 15);
                    Impact(bTo, 0f, 10);
                }
            }
            yield return new WaitForSeconds(0.4f);
            ReleaseArms();
            ResetPose();
        }

        // ---------- Pluie de pierres (phase III+) ----------
        // Lance des poignées de pierres vers le ciel ; elles retombent sur les zones marquées.
        private IEnumerator RockRain()
        {
            if (target == null) yield break;
            int n = meteorCount + 2 + (phase >= 4 ? 3 : 0);
            int throws = 3;
            int done = 0;
            for (int k = 0; k < throws && !dead; k++)
            {
                ProjectileLook look = Look(ThrowArm);
                yield return Scoop(ThrowArm, look, 0.95f, Spd);
                // Jet vers le haut : s'accroupit (main basse, en arrière), puis lance vers le ciel et la main retombe
                Vector3 start = HandPos(ThrowArm);
                tiltTarget = -6f;
                yield return ArmPath(ThrowArm, 0.2f / Spd, e => Curve(e, start, ShoulderRel(ThrowArm, -0.1f, 0.05f), ShoulderRel(ThrowArm, -0.22f, 0.15f)), Smooth, 0f);
                tiltTarget = -16f;
                tiltVel -= 50f;
                yield return ArmPath(ThrowArm, 0.14f / Spd, e => Curve(e, ShoulderRel(ThrowArm, -0.22f, 0.15f), ShoulderRel(ThrowArm, 0f, 0.6f), ShoulderRel(ThrowArm, 0.12f, 0.92f)),
                    u => u * u, 0f, 0.9f, () => { if (jar != null) jar.ReleaseHeld(ThrowArm); });
                Shake(0.15f, 0.06f);
                StartCoroutine(ArmArc(ThrowArm, GroundAhead(ThrowArm), 0.3f / Spd, 0f, 1f));
                int batch = (n - done) / (throws - k);
                for (int i = 0; i < batch && !dead; i++, done++)
                {
                    // La première vise le joueur, les autres tombent autour
                    float x = target.transform.position.x + (done == 0 ? playerVel.x * 0.75f / Spd : Random.Range(-6f, 6f));
                    BossProjectile.SpawnMeteor(x, groundY, (0.75f + i * 0.12f) / Spd, 16f, meteorDamage * Dmg, meteorReflectDamage, this, PhaseColor(), i == 0 ? look : LookCopy());
                }
                yield return new WaitForSeconds(0.3f / Spd);
            }
            ArmRelease(ThrowArm);
            yield return new WaitForSeconds(0.6f);
            ResetPose();
        }

        // ---------- Bond (phase IV+ ; deux bonds en V) ----------
        // Plante les mains derrière lui, se ramasse, puis se propulse en un bond par-dessus / sur le joueur.
        private IEnumerator Leap()
        {
            if (target == null) yield break;
            float side = target.transform.position.x < transform.position.x ? 1f : -1f;
            float t = 0f;
            while (t < 0.9f)
            {
                MoveToward(target.transform.position.x + side * 7f, HoverY(), 2f);
                t += Time.deltaTime;
                yield return null;
            }

            // Armement : mains plantées derrière, bol qui se ramasse vers l'arrière
            lockFacing = true;
            float dir = facing;
            tiltTarget = -18f;
            flash = 0.5f;
            Shake(0.4f, 0.08f);
            Vector3 fPlant = ArmPoint(FrontArm, transform.position.x - dir * 0.4f * Reach(FrontArm), groundY + 0.1f * curScale);
            Vector3 bPlant = ArmPoint(BackArm, transform.position.x - dir * 0.75f * Reach(BackArm), groundY + 0.1f * curScale);
            yield return ArmsMove(fPlant, bPlant, 0.3f / Spd, true);
            squashVel -= 3f;
            yield return new WaitForSeconds(0.3f / Spd);

            // Bond : parabole jusqu'au-delà du joueur
            tiltTarget = 22f;
            Vector3 start = transform.position;
            Vector3 end = new Vector3(target.transform.position.x + dir * 2.5f, HoverY(), 0f);
            float dur = 0.6f / Spd;
            float height = 2.6f * curScale;
            bool hasHit = false;
            t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                Vector3 p = Vector3.Lerp(start, end, u);
                p.y += Mathf.Sin(u * Mathf.PI) * height;
                transform.position = p;
                // Les bras poussent puis traînent derrière, en l'air
                Vector3 c = CorePosition;
                ArmTo(FrontArm, ArmPoint(FrontArm, c.x - dir * 0.5f * Reach(FrontArm), c.y - 0.2f * Reach(FrontArm)), 0.3f);
                ArmTo(BackArm, ArmPoint(BackArm, c.x - dir * 0.7f * Reach(BackArm), c.y + 0.1f * Reach(BackArm)), 0.3f);
                if (!hasHit && target != null)
                {
                    Vector3 pp = target.transform.position;
                    if (new Vector2(pp.x - c.x, pp.y - c.y).magnitude < 1.3f * curScale)
                    {
                        hasHit = true;
                        if (!target.TakeDamage(dashDamage * Dmg, transform, Vector3.Lerp(c, pp, 0.5f)))
                        {
                            TakeHit(dashParryDamage);
                            if (phaseChanged || dead) yield break;
                            transform.position = new Vector3(p.x, HoverY(), 0f);
                            yield return Stagger();
                            yield break;
                        }
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = end;
            moveVel = Vector3.zero;
            crawlWanted = false;
            squashVel += 5f;
            ReleaseArms();
            Impact(new Vector3(end.x, groundY, 0f), 0.4f, 40);
            // À l'atterrissage, petites ondes des deux côtés
            BossProjectile.SpawnShockwave(new Vector3(end.x + 1.2f * curScale, groundY, 0f), 1f, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1f, ProjectileLook.Rock());
            BossProjectile.SpawnShockwave(new Vector3(end.x - 1.2f * curScale, groundY, 0f), -1f, shockwaveSpeed * Spd, shockwaveDamage * Dmg, this, PhaseColor(), 1f, ProjectileLook.Rock());

            // Phase V : demi-tour immédiat et deuxième bond
            if (phase >= 4 && !dead && !secondLeap)
            {
                secondLeap = true;
                lockFacing = false;
                yield return new WaitForSeconds(0.3f);
                yield return Leap();
                secondLeap = false;
                yield break;
            }
            yield return new WaitForSeconds(0.35f);
            ResetPose();
        }
        private bool secondLeap;

        // ---------- Pics de roche (phase II+) ----------
        // Il abat les deux mains devant lui : une ligne de pics de roche jaillit jusqu'au joueur et au-delà
        private IEnumerator Pillars()
        {
            if (target == null) yield break;
            tiltTarget = -12f;
            Vector3 f0 = ShoulderPos(FrontArm), b0 = ShoulderPos(BackArm);
            yield return ArmsMove(ArmPoint(FrontArm, f0.x, f0.y + 0.8f * Reach(FrontArm)),
                                  ArmPoint(BackArm, b0.x + facing * 0.3f * Reach(BackArm), b0.y + 0.8f * Reach(BackArm)), 0.5f / Spd, false);

            // Frappe le sol devant lui
            lockFacing = true;
            tiltTarget = 20f;
            float dir = facing;
            float hitX = transform.position.x + dir * 1.3f * curScale;
            yield return ArmsMove(ArmPoint(FrontArm, hitX + dir * 0.3f * curScale, groundY + 0.15f * curScale),
                                  ArmPoint(BackArm, hitX - dir * 0.1f * curScale, groundY + 0.15f * curScale), 0.1f / Spd, true);
            squashVel += 3f;
            Impact(new Vector3(hitX, groundY, 0f), 0.22f, 30);

            // Motif répété le long des lignes : pic haut, pic bas (se saute), trou sûr.
            // Toute l'attaque forme une seule salve : une parade réussie la neutralise entièrement.
            float start = hitX + dir * 0.6f * curScale;
            float h = 5f + phase * 0.6f;
            float spacing = 1.7f;
            float delay = 0.5f / Spd;
            float step = 0.1f / Spd;
            int patternOffset = Random.Range(0, 3);
            BossProjectile.PillarSalvo salvo = new BossProjectile.PillarSalvo();

            // Phase IV+ : deux lignes convergent vers la position du joueur (l'une part du boss, l'autre de
            // l'autre côté du joueur) et se rejoignent en un pic unique, à parer ou à fuir.
            float meet = target.transform.position.x;
            int lineSlots = Mathf.FloorToInt((meet - start) * dir / spacing);
            if (phase >= 3 && lineSlots >= 1)
            {
                const int farSlots = 4;                              // longueur de la ligne venue d'en face
                int offset = Mathf.Max(0, farSlots - lineSlots);     // aligne les deux arrivées
                int meetSlot = lineSlots + offset;
                for (int s = 0; s <= meetSlot && !dead; s++)
                {
                    int k = meetSlot - s;   // distance au point de rencontre, en emplacements
                    if (k == 0)
                    {
                        BossProjectile.SpawnPillar(meet, groundY, delay, pillarDamage * Dmg, this, PhaseColor(), h, false, salvo, ProjectileLook.Rock());
                    }
                    else
                    {
                        if (k <= lineSlots) SpawnPatternPillar(meet - dir * k * spacing, k, patternOffset, delay, h, salvo);
                        if (k <= farSlots) SpawnPatternPillar(meet + dir * k * spacing, k, patternOffset, delay, h, salvo);
                    }
                    yield return new WaitForSeconds(step);
                }
            }
            else
            {
                int n = 7 + phase;
                for (int i = 0; i < n && !dead; i++)
                {
                    SpawnPatternPillar(start + dir * i * spacing, i, patternOffset, delay, h, salvo);
                    yield return new WaitForSeconds(step);
                }
            }
            yield return new WaitForSeconds(0.5f);
            ReleaseArms();
            ResetPose();
        }

        // Emplacement 'index' du motif : 0 = pic haut, 1 = pic bas, 2 = trou
        private void SpawnPatternPillar(float x, int index, int patternOffset, float delay, float h, BossProjectile.PillarSalvo salvo)
        {
            int slot = (index + patternOffset) % 3;
            if (slot == 2) return;
            BossProjectile.SpawnPillar(x, groundY, delay, pillarDamage * Dmg, this, PhaseColor(), h, slot == 1, salvo, ProjectileLook.Rock());
        }

        // ---------- Jugement (phase II+) : gros rocher échangé ----------
        private IEnumerator Judgment()
        {
            if (target == null) yield break;
            ProjectileLook look = Look(ThrowArm);
            yield return ChargeExchange(1.4f, look, 1.3f);
            yield return Whip(ThrowArm, 1f, from =>
            {
                Vector3 dir = target != null ? target.HitCenter - from : Vector3.right * facing;
                dir.z = 0f;
                judgmentOrb = BossProjectile.SpawnJudgment(from, dir, 6f, judgmentDamage * Dmg, judgmentReflectDamage, judgmentRallies, this, rageColor, look);
                Shake(0.5f, 0.2f);
            }, false);

            // Attend la fin de l'échange
            float t = 0f;
            while (judgmentOrb != null && t < 10f && !dead)
            {
                t += Time.deltaTime;
                yield return null;
            }
            ArmRelease(ThrowArm);
            ResetPose();
        }

        // Le projectile revient sur lui : il le rattrape d'une main et le relance, tant qu'il reste des échanges
        public bool ReturnOrb()
        {
            if (dead) return false;
            flash = 0.5f;
            Shake(0.2f, 0.15f);
            if (Application.isPlaying && isActiveAndEnabled)
            {
                if (catchRoutine != null) StopCoroutine(catchRoutine);
                catchRoutine = StartCoroutine(CatchGesture());
            }
            return true;
        }

        private Coroutine catchRoutine;

        // Rattrapage : la main claque devant le bol, recule sous le choc en armant, puis relance d'un coup de fouet
        // et retombe au sol
        private IEnumerator CatchGesture()
        {
            Vector3 start = HandPos(ThrowArm);
            tiltVel -= 70f;
            yield return ArmPath(ThrowArm, 0.3f, e => Curve(e, start, ShoulderRel(ThrowArm, 0.45f, 0.25f), ShoulderRel(ThrowArm, -0.15f, 0.5f)),
                u => 1f - (1f - u) * (1f - u), 0f);
            yield return Whip(ThrowArm, 1.3f, null, false);
            ArmRelease(ThrowArm);
            catchRoutine = null;
        }

        // ---------- Échanges (famille du Jugement) ----------

        // Ramasse un rocher et le soulève au-dessus du bol en tremblant : signal commun aux échanges
        private IEnumerator ChargeExchange(float duration, ProjectileLook look, float size)
        {
            yield return Scoop(ThrowArm, look, size, 1f);
            // Soulève : la main remonte contre le bol puis au-dessus, le bol se penche en arrière sous le poids
            Vector3 start = HandPos(ThrowArm);
            tiltTarget = -12f;
            yield return ArmPath(ThrowArm, 0.45f, e => Curve(e, start, ShoulderRel(ThrowArm, 0.2f, 0.25f), ShoulderRel(ThrowArm, -0.15f, 0.8f)), Smooth, 0f);
            float t = 0f;
            while (t < duration && !dead)
            {
                float u = t / duration;
                // Le poids le fait plier peu à peu, et trembler de plus en plus
                Vector3 up = ShoulderRel(ThrowArm, -0.15f - 0.1f * u, 0.8f - 0.08f * u);
                ArmTo(ThrowArm, up + Random.insideUnitSphere * 0.04f * u * curScale, 0f);
                if (Random.value < 0.12f) Shake(0.1f, 0.05f + u * 0.06f);
                t += Time.deltaTime;
                yield return null;
            }
            Shake(0.4f, 0.18f);
        }

        // Attend la fin de l'échange (plus aucun projectile d'échange en jeu)
        private IEnumerator WaitExchangeEnd(float timeout)
        {
            float t = 0f;
            while ((RallyBubble.ActiveCount > 0 || PushBubble.ActiveCount > 0) && t < timeout && !dead)
            {
                t += Time.deltaTime;
                yield return null;
            }
            ArmRelease(ThrowArm);
            ArmRelease(OtherArm);
            ResetPose();
        }

        private RallyBubble.Settings ExchangeSettings(float speed, float size, float damage, float reflectDamage, Color color, ProjectileLook look)
        {
            return new RallyBubble.Settings
            {
                Speed = speed * Spd,
                Size = size,
                Damage = damage * Dmg,
                ReflectDamage = reflectDamage,
                SpeedUp = 1.05f,
                Color = color,
                Look = look,
            };
        }

        // Lâche le projectile que tient la main : renvoie le point de départ (paume, plan de jeu)
        private Vector3 ReleaseFrom(int arm)
        {
            Vector3 from = PalmPos(arm);
            from.z = 0f;
            if (jar != null) jar.ReleaseHeld(arm);
            return from;
        }

        // Caillou qui se divise (phase III+) : chaque parade le casse en 2 au retour → 1, 2 puis 4 parades en rythme
        private IEnumerator SplitBubble()
        {
            if (target == null) yield break;
            ProjectileLook look = Look(ThrowArm);
            yield return ChargeExchange(1.1f, look, 1.1f);
            RallyBubble.Settings s = ExchangeSettings(6.5f, 1.1f, splitDamage, splitReflectDamage, PhaseColor(), look);
            s.Splits = splitCount;
            s.SplitStagger = splitStagger;
            yield return Whip(ThrowArm, 1f, from => RallyBubble.Spawn(from, s, this), false);
            yield return WaitExchangeEnd(20f);
        }

        // Double Jugement (phase IV+) : un caillou dans chaque main, lancés l'un après l'autre à des hauteurs
        // différentes ; il les renvoie en alternance
        private IEnumerator DoubleJudgment()
        {
            if (target == null) yield break;
            ProjectileLook look2 = Look(OtherArm);
            yield return Scoop(OtherArm, look2, 1f, 1f);
            // La main arrière garde son caillou levé derrière le bol
            Vector3 os = HandPos(OtherArm);
            yield return ArmPath(OtherArm, 0.35f, e => Curve(e, os, ShoulderRel(OtherArm, -0.1f, 0.3f), ShoulderRel(OtherArm, -0.05f, 0.65f)), Smooth, 0f);
            ProjectileLook look1 = Look(ThrowArm);
            yield return ChargeExchange(1.3f, look1, 1f);
            RallyBubble.Settings s1 = ExchangeSettings(6f, 1f, doubleDamage, doubleReflectDamage, rageColor, look1);
            s1.Rallies = 2;
            yield return Whip(ThrowArm, 1f, from => RallyBubble.Spawn(from, s1, this, 0f, Vector3.up * 1.2f * curScale), false);
            yield return new WaitForSeconds(Mathf.Max(0f, doubleOffset / Spd - 0.25f));
            RallyBubble.Settings s2 = ExchangeSettings(6f, 1f, doubleDamage, doubleReflectDamage, rageColor, look2);
            s2.Rallies = 2;
            yield return Whip(OtherArm, 1f, from => RallyBubble.Spawn(from, s2, this, 0f, Vector3.down * 0.5f * curScale), true);
            yield return WaitExchangeEnd(25f);
        }

        // Échange truqué (phase V) : plus de renvois, de plus en plus rapides, avec des feintes en l'air
        private IEnumerator RiggedRally()
        {
            if (target == null) yield break;
            ProjectileLook look = Look(ThrowArm);
            yield return ChargeExchange(1.2f, look, 1.2f);
            RallyBubble.Settings s = ExchangeSettings(6.5f, 1.2f, riggedDamage, riggedReflectDamage, rageColor, look);
            s.Rallies = riggedRallies;
            s.SpeedUp = riggedSpeedUp;
            s.FeintChance = riggedFeintChance;
            yield return Whip(ThrowArm, 1f, from => RallyBubble.Spawn(from, s, this), false);
            yield return WaitExchangeEnd(30f);
        }

        // Bras de fer (phase V) : il pousse un gros rocher vers le joueur ; chaque parade le renvoie plus près du boss
        private IEnumerator TugOfWar()
        {
            if (target == null) yield break;
            ProjectileLook look = Look(ThrowArm);
            yield return ChargeExchange(1.4f, look, 1.6f);
            if (jar != null) jar.ReleaseHeld(ThrowArm);
            PushBubble.Spawn(this, tugDamage * Dmg, tugReflectDamage, 1.6f, tugSpeed * Spd, tugSpeedUp, 18f, tugParries, rageColor, look);
            // Les deux mains en avant, comme s'il poussait (le bol se jette en avant)
            tiltTarget = 16f;
            tiltVel += 50f;
            Vector3 c = CorePosition;
            StartCoroutine(ArmArc(OtherArm, ArmPoint(OtherArm, c.x + facing * 0.6f * Reach(OtherArm), c.y + 0.3f * Reach(OtherArm)), 0.25f, 0.1f * Reach(OtherArm), 0f));
            yield return ArmArc(ThrowArm, ArmPoint(ThrowArm, c.x + facing * 0.7f * Reach(ThrowArm), c.y + 0.1f * Reach(ThrowArm)), 0.2f, 0f, 0f);
            yield return WaitExchangeEnd(35f);
        }

        // ---------- Bras : petits outils ----------

        private const int FrontArm = JarVisual.Front;
        private const int BackArm = JarVisual.Back;

        // Point de la main dans le plan du bras (x, y du monde ; profondeur du bras au repos)
        private Vector3 ArmPoint(int arm, float x, float y)
        {
            float z = jar != null ? jar.RestPlant(arm, groundY).z : 0f;
            return new Vector3(x, y, z);
        }

        private Vector3 RestPlant(int arm) => jar != null ? jar.RestPlant(arm, groundY) : new Vector3(transform.position.x, groundY, 0f);
        private Vector3 HandPos(int arm) => jar != null ? jar.HandPosition(arm) : CorePosition;
        private Vector3 PalmPos(int arm) => jar != null ? jar.PalmPosition(arm) : CorePosition;
        private Vector3 ShoulderPos(int arm) => jar != null ? jar.ShoulderPosition(arm) : CorePosition;
        private float Reach(int arm) => jar != null ? jar.Reach(arm) : 3f * curScale;

        private void ArmTo(int arm, Vector3 world, float flatHand)
        {
            if (jar != null) jar.ArmTo(arm, world, flatHand);
        }

        private void ArmRelease(int arm)
        {
            if (jar != null) jar.ArmRelease(arm);
        }

        private void ReleaseArms()
        {
            ArmRelease(FrontArm);
            ArmRelease(BackArm);
            if (jar != null)
            {
                jar.ReleaseHeld(FrontArm);
                jar.ReleaseHeld(BackArm);
            }
        }

        // Les deux mains en même temps ; strike : accélère jusqu'au bout (frappe)
        private IEnumerator ArmsMove(Vector3 front, Vector3 back, float dur, bool strike)
        {
            Vector3 f0 = HandPos(FrontArm), b0 = HandPos(BackArm);
            float t = 0f;
            while (t < dur)
            {
                float u = Mathf.Clamp01(t / dur);
                float e = strike ? u * u : u * u * (3f - 2f * u);
                ArmTo(FrontArm, Vector3.LerpUnclamped(f0, front, e), strike ? 1f : 0.4f);
                ArmTo(BackArm, Vector3.LerpUnclamped(b0, back, e), strike ? 1f : 0.4f);
                t += Time.deltaTime;
                yield return null;
            }
            ArmTo(FrontArm, front, strike ? 1f : 0.4f);
            ArmTo(BackArm, back, strike ? 1f : 0.4f);
        }

        // Main qui frappe le sol : secousse et gerbe de poussière
        private void Impact(Vector3 at, float shake, int dust = 25)
        {
            if (shake > 0f) Shake(shake, shake * 0.8f);
            ParticleBurst.Spawn("JarDust", new Vector3(at.x, groundY + 0.1f, 0f), new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.47f, 0.42f), new Color(0.75f, 0.72f, 0.66f)), dust,
                lifetime: new Vector2(0.4f, 0.9f),
                speed: new Vector2(1f, 4f + shake * 6f),
                size: new Vector2(0.08f, 0.3f),
                gravity: 0.6f,
                radius: 0.3f * curScale,
                duration: 0.2f);
        }

        // ---------- Dégâts et mort ----------

        // Outil de debug : mort immédiate (la séquence de mort se joue normalement)
        public void DebugKill()
        {
            if (dead) return;
            active = true;
            health = 0f;
            dead = true;
        }

        public void TakeHit(float amount)
        {
            if (dead || !active || invulnerable) return;
            health -= amount;
            lastHitTime = Time.time;
            HitReaction();

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

        // Recul visible quand il est touché
        private void HitReaction()
        {
            flash = 1f;
            tiltVel -= 90f;
            Shake(0.15f, 0.12f);
        }

        private IEnumerator Die()
        {
            lockFacing = true;
            bannerTime = -100f;
            yield return new WaitForSecondsRealtime(0.12f);
            TimeControl.SetEffectScale(0.25f);
            Shake(2f, 0.35f);
            tiltTarget = -30f;

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
                if (t > 1.2f) TimeControl.SetEffectScale(1f);
                yield return null;
            }
            TimeControl.SetEffectScale(1f);
            SpawnBurst(250, 16f, 1f);
            t = 0f;
            while (t < 0.4f)
            {
                deathFade = 1f - t / 0.4f;
                t += Time.deltaTime;
                yield return null;
            }
            if (labMode)
            {
                LabRevive();
                yield break;
            }
            Debug.Log(bossName + " vaincu !");
            GameCamera cam = GameCamera.Main;
            if (cam != null) cam.SetFraming(0f, 0f);
            Destroy(gameObject);
        }

        // =====================================================================
        //  Scène AnimationLab : gestes joués un par un
        // =====================================================================

        private Vector3 labHome;
        private bool labBusy;

        public bool IsLab => labMode;
        public bool LabBusy => labBusy;

        // Visible tout de suite, en vol stationnaire, sans combat automatique
        private void LabStart()
        {
            target = FindFirstObjectByType<PlayerController>();
            active = true;
            health = trailHealth = maxHealth;
            labHome = new Vector3(transform.position.x, HoverY(), 0f);
            transform.position = labHome;
            lastPos = transform.position;
            if (target != null) lastPlayerPos = target.transform.position;
            SetVisible(true);
        }

        public void LabPlay(LabMove move)
        {
            if (!labMode || !Application.isPlaying || dead) return;
            StopAllCoroutines();
            TimeControl.SetEffectScale(1f);
            ResetPose();
            invulnerable = false;
            labBusy = false;
            switch (move)
            {
                case LabMove.Hit:
                    HitReaction();
                    return;
                case LabMove.Die:
                    DebugKill();   // Update lance la séquence de mort ; le boss revient ensuite
                    labBusy = true;
                    return;
            }
            labBusy = true;
            StartCoroutine(LabRun(LabRoutine(move)));
        }

        // Change la phase (taille, pièces, couleur) sans passer par la transition
        public void LabSetPhase(int newPhase)
        {
            if (!labMode || dead) return;
            phase = Mathf.Clamp(newPhase, 0, 4);
            health = trailHealth = maxHealth * (1f - phase / 5f);
            labHome.y = HoverY();
        }

        private IEnumerator LabRoutine(LabMove move)
        {
            switch (move)
            {
                case LabMove.Swing: return Slap();
                case LabMove.Orbs: return ThrowRocks();
                case LabMove.Slam: return Slam();
                case LabMove.MeteorRain: return RockRain();
                case LabMove.Dash: return Leap();
                case LabMove.Pillars: return Pillars();
                case LabMove.Judgment: return Judgment();
                case LabMove.SplitBubble: return SplitBubble();
                case LabMove.DoubleJudgment: return DoubleJudgment();
                case LabMove.RiggedRally: return RiggedRally();
                case LabMove.TugOfWar: return TugOfWar();
                case LabMove.Roar: return Roar(1f);
                case LabMove.Stagger: return Stagger();
                default: return PhaseTransition();
            }
        }

        // Joue le geste puis revient à sa place
        private IEnumerator LabRun(IEnumerator move)
        {
            yield return move;
            ResetPose();
            invulnerable = false;
            float t = 0f;
            while (t < 1f && !dead)
            {
                MoveToward(labHome.x, labHome.y, 1.5f);
                t += Time.deltaTime;
                yield return null;
            }
            labBusy = false;
        }

        // Après la séquence de mort : revient au lieu d'être détruit
        private void LabRevive()
        {
            dead = false;
            dieStarted = false;
            deathFade = 1f;
            health = trailHealth = maxHealth * (1f - phase / 5f);
            transform.position = labHome;
            lastPos = labHome;
            ResetPose();
            labBusy = false;
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

        private float PhaseScale() { return scale * (1f + growthPerPhase * phase); }

        private float HoverY() { return groundY + hoverHeight * curScale; }

        private float DistToPlayer()
        {
            return target != null ? Mathf.Abs(target.transform.position.x - transform.position.x) : Mathf.Infinity;
        }

        // Déplacement pénible : il se traîne par tractions (JarVisual.Crawl, appliqué dans ApplyPose).
        // À appeler à chaque image tant qu'il doit avancer ; la hauteur suit en douceur.
        private void MoveToward(float x, float y, float speedMul = 1f)
        {
            if (jar == null)
            {
                Vector3 goal = new Vector3(x, y, 0f);
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 7f);
                transform.position = Vector3.SmoothDamp(transform.position, goal, ref moveVel, 0.35f / speedMul, moveSpeed * speedMul * pulse);
                return;
            }
            crawlWant = x - transform.position.x;
            // Même vitesse moyenne que l'ancien glissement
            crawlSpeed = moveSpeed * speedMul * 0.55f;
            crawlWanted = true;
            Vector3 p = transform.position;
            p.y = Mathf.SmoothDamp(p.y, y, ref moveVel.y, 0.35f / speedMul);
            transform.position = p;
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
        // Repère du visuel : avant = +X, haut = +Y, côté caméra = -Z. Unité = scale. Origine = centre du bol.

        private void BuildVisual()
        {
            if (visual != null) return;

            visual = new GameObject("GuardianVisual");
            Track(visual);
            visual.transform.SetParent(transform, false);
            bodyT = Empty("Body", visual.transform);
            if (jarModel != null)
            {
                jar = new JarVisual(jarModel, bodyT, generated);
                core = jar.Core;
                // Main qui agrippe, bol qui racle, morceau qui tombe : poussière (et petite secousse au raclement)
                jar.OnDust = (at, strength) => Impact(at, strength >= 1f ? 0.04f : 0f, Mathf.RoundToInt(4 + 10 * strength));
            }
            else
            {
                core = Empty("Core", bodyT);
            }

            ApplyPose(0f, 0f);
        }

        // Place les pièces selon la pose courante
        private void ApplyPose(float dt, float time)
        {
            float fwdVel = 0f;
            // Reptation : la traction fait avancer le corps (avant le calcul de la vitesse et des bras)
            if (jar != null && dt > 0f && Application.isPlaying)
            {
                float dx = jar.Crawl(dt, crawlWanted && !dead ? crawlWant : 0f, crawlSpeed, groundY);
                transform.position += new Vector3(dx, 0f, 0f);
                crawlWanted = false;
            }
            if (dt > 0f)
            {
                // Vitesse réelle du corps (sert à l'inclinaison, à la queue, à l'étirement)
                Vector3 raw = Vector3.ClampMagnitude((transform.position - lastPos) / dt, 40f);
                lastPos = transform.position;
                bodyVel = Vector3.Lerp(bodyVel, raw, 1f - Mathf.Exp(-12f * dt));
                fwdVel = bodyVel.x * facing;

                // Bol lourd : penche dans le sens où il est tiré, tangue légèrement
                float lean = Mathf.Clamp(fwdVel * 1.2f, -18f, 25f) + Mathf.Sin(time * 0.9f) * 2f;
                if (jar != null) lean += jar.CrawlLean * facing * 10f;
                Spring(ref tilt, ref tiltVel, tiltTarget + lean, 2.4f, 0.6f, dt);
                flash = Mathf.Max(0f, flash - dt * 4f);

                // Étiré quand il file à la verticale ; s'écrase tout seul à l'arrêt (le ressort dépasse)
                float stretch = -Mathf.Clamp(Mathf.Abs(bodyVel.y) * 0.006f, 0f, 0.14f);
                Spring(ref squash, ref squashVel, stretch, 3f, 0.3f, dt);

                // Demi-tour : il pivote face caméra puis de l'autre côté
                flip = Mathf.MoveTowards(flip, facing, dt * 4.5f);
                // Métamorphose : grandit lentement vers la taille de la phase
                curScale = Mathf.Lerp(curScale, PhaseScale(), 1f - Mathf.Exp(-1.5f * dt));
            }
            else
            {
                tilt = tiltTarget;
                tiltVel = squashVel = 0f;
                squash = 0f;
                lastPos = transform.position;
                flip = facing;
                curScale = PhaseScale();
            }

            // Miroir selon le côté du joueur (le bras avant reste côté joueur), et un peu tourné vers la caméra
            // Demi-tour : vraie rotation. À mi-chemin il fait face à la caméra (yaw 90) ; là, le miroir
            // ne change que la profondeur, pas la silhouette : le passage de l'autre côté est invisible.
            float sgn = Mathf.Abs(flip) < 1e-4f ? Mathf.Sign(facing) : Mathf.Sign(flip);
            float m01 = Mathf.SmoothStep(0f, 1f, Mathf.Abs(flip));
            float yaw = Mathf.Lerp(90f, sgn * turnToCamera, m01);
            visual.transform.localScale = new Vector3(sgn * (1f + squash * 0.5f), 1f - squash, 1f + squash * 0.5f) * curScale * deathFade;
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Posé au sol : le bol tangue autour de son pied (pas d'inclinaison franche).
            // Pendant l'effort d'une traction, il tremble.
            float strain = jar != null && dt > 0f ? jar.CrawlStrain : 0f;
            visual.transform.localPosition = strain > 0f
                ? new Vector3(Random.Range(-1f, 1f), Random.Range(-0.4f, 0.4f), 0f) * 0.025f * strain * curScale
                : Vector3.zero;
            bodyT.localRotation = Quaternion.Euler(0f, 0f, -tilt * 0.35f);

            // Bol : morceaux selon la phase, chair qui respire, flash quand il est touché
            if (jar != null)
            {
                jar.SetPhase(phase, dt <= 0f);
                jar.Tick(dt, time, flash, groundY, bodyVel);
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

        private T Track<T>(T o) where T : Object
        {
            return generated.Track(o);
        }

        // =====================================================================
        //  Barre de vie (5 segments) et bannière de phase
        // =====================================================================

        private void OnGUI()
        {
            if (!Application.isPlaying || !active) return;

            // Tailles pensées pour 1080p, ramenées à l'écran : rien ne sort du cadre sur une petite fenêtre
            float ui = Screen.height / 1080f;
            float w = Mathf.Min(Screen.width * 0.8f, 1100f * Mathf.Max(ui, 0.5f));
            float h = Mathf.Max(12f, 30f * ui);
            float x = (Screen.width - w) / 2f;
            float y = Screen.height - Mathf.Max(28f, 64f * ui);
            Color pc = PhaseColor();

            // Nom + phase courante
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { fontSize = GuiDraw.FontSize(26f), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft, clipping = TextClipping.Overflow };
            GUIStyle phaseStyle = new GUIStyle(GUI.skin.label) { fontSize = GuiDraw.FontSize(16f), fontStyle = FontStyle.Italic, alignment = TextAnchor.LowerRight, clipping = TextClipping.Overflow };
            float labelH = Mathf.Max(18f, 40f * ui);
            GuiDraw.ShadowLabel(new Rect(x, y - labelH, w, labelH - 4f), bossName, nameStyle, pc);
            GuiDraw.ShadowLabel(new Rect(x, y - labelH, w, labelH - 4f), "Phase " + roman[phase] + " — " + phaseNames[phase], phaseStyle, new Color(1f, 0.9f, 0.8f));

            Color old = GUI.color;
            // Cadre
            GuiDraw.FilledRect(new Rect(x - 4f, y - 4f, w + 8f, h + 8f), new Color(0f, 0f, 0f, 0.85f));
            GuiDraw.FilledRect(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), Color.Lerp(new Color(0.42f, 0.1f, 0.07f), pc, 0.4f));
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
                GUIStyle big = new GUIStyle(GUI.skin.label) { fontSize = GuiDraw.FontSizeFitting(64f, 10, 0.9f, 16), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                GUIStyle sub = new GUIStyle(GUI.skin.label) { fontSize = GuiDraw.FontSizeFitting(30f, phaseNames[phase].Length, 0.9f, 11), fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                float cy = Screen.height * 0.3f;
                float band = big.fontSize * 2.4f;
                GuiDraw.FilledRect(new Rect(0f, cy - band * 0.47f, Screen.width, band), new Color(0f, 0f, 0f, 0.55f * a));
                Color c = pc; c.a = a;
                GuiDraw.ShadowLabel(new Rect(0f, cy - big.fontSize * 0.8f, Screen.width, big.fontSize * 1.3f), "PHASE " + roman[phase], big, c);
                GuiDraw.ShadowLabel(new Rect(0f, cy + big.fontSize * 0.45f, Screen.width, sub.fontSize * 1.5f), phaseNames[phase], sub, new Color(1f, 0.92f, 0.85f, a));
            }
        }
    }
}
