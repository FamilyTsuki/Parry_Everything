using System;
using System.Collections;
using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace ParryEverything.Player
{
    // Joueur : déplacement, saut, parade, dégâts et voile rouge à l'écran.
    // La santé (âme, membres) est gérée par PlayerBody : pas de barre de vie.
    // Trois formes (PlayerBody.Form) : âme (lente, flottante), boule de chair (lourde, roule avec de l'inertie),
    // debout sur ses pattes (course, parade de chute ; sur une seule patte, il sautille).
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Déplacement")]
        [SerializeField] private float soulSpeed = 3.5f;
        [FormerlySerializedAs("speed")]
        [SerializeField] private float bodySpeed = 3.5f;                // debout : les pattes (0,85 m) font des pas d'environ 0,9 m
        [SerializeField] private float runSpeed = 6.0f;                 // debout sur 2 pattes, Maj gauche
        [Range(0f, 1f)] [SerializeField] private float oneLegSpeedFactor = 0.55f;    // une seule patte : il sautille
        [Range(0f, 1f)] [SerializeField] private float oneLegJumpFactor = 0.5f;      // une seule patte : saut réduit
        [Range(0f, 1f)] [SerializeField] private float missingFootSpeedFactor = 0.8f; // par pied perdu : il boite sur son moignon

        [Header("Boule de chair : lourde, roule")]
        [SerializeField] private float ballSpeed = 4.2f;
        [SerializeField] private float ballAcceleration = 9f;           // prend de l'élan...
        [SerializeField] private float ballDeceleration = 5f;           // ...et continue de rouler un peu
        [SerializeField] private float ballJumpHeight = 1.1f;           // rebond court

        [Header("Âme : saut lent et flottant")]
        [Range(0.1f, 1f)] [SerializeField] private float soulGravityFactor = 0.45f;  // part de la gravité ressentie par l'âme
        [SerializeField] private float soulMaxFallSpeed = 6f;

        [Header("Collision")]
        // Debout, la boule est portée haut par les pattes : la collision grandit vers le haut (les pieds restent au sol)
        [SerializeField] private float standingHeight = 1.75f;

        [Header("Chute (corps)")]
        [SerializeField] private float fallDamageHeight = 4f;           // au-delà, une patte est arrachée...
        [SerializeField] private float fallParryTime = 0.15f;           // ...sauf parade juste avant l'impact

        [Header("Recouture sur place")]
        [SerializeField] private float sewHoldTime = 1.5f;              // maintenir ↓, immobile au sol

        [Header("Absorption d'âme")]
        [SerializeField] private float absorbRange = 1.6f;              // parer au contact d'un ennemi étourdi

        [Header("Saut")]
        [SerializeField] private float jumpHeight = 2.0f;               // hauteur max (touche maintenue)
        [SerializeField] private float fallGravityMultiplier = 2.2f;    // chute plus rapide que la montée : moins flottant
        [SerializeField] private float lowJumpGravityMultiplier = 3.0f; // touche relâchée tôt : petit saut
        [SerializeField] private float apexGravityMultiplier = 0.6f;    // gravité réduite au sommet (touche maintenue) : contrôle en l'air
        [SerializeField] private float apexThreshold = 1.5f;            // vitesse verticale sous laquelle on est "au sommet"
        [SerializeField] private float maxFallSpeed = 18f;
        [SerializeField] private float coyoteTime = 0.1f;               // saut encore possible juste après avoir quitté un bord
        [SerializeField] private float jumpBufferTime = 0.12f;          // appui juste avant d'atterrir : le saut part à l'atterrissage
        [Range(0f, 1f)] [SerializeField] private float groundNormalMin = 0.6f; // pente max considérée comme sol

        [Header("Recul")]
        [SerializeField] private float knockbackForce = 20.0f;
        [SerializeField] private float knockbackEndSpeed = 1.0f;

        [Header("Impact quand on prend un coup")]
        [SerializeField] private float hitStopDuration = 0.2f;   // arrêt du temps
        [SerializeField] private float shakeDuration = 0.2f;     // tremblement d'écran
        [SerializeField] private float shakeMagnitude = 0.15f;

        [Header("Parade")]
        // Fenêtre active après appui, puis temps d'attente si raté (touches : GameInput)
        [SerializeField] private float soulParryWindow = 0.35f;
        [SerializeField] private float soulParryCooldown = 0.5f;
        [SerializeField] private float bodyParryWindow = 0.3f;
        [SerializeField] private float bodyParryCooldown = 0.3f;
        [SerializeField] private float perfectParryTime = 0.1f;   // parade réussie dans ce début de fenêtre = parfaite
        [SerializeField] private float perfectChainGap = 2f;      // délai max entre deux parades parfaites enchaînées

        [Header("Indicateur de parade (étoile en haut à droite du joueur)")]
        [SerializeField] private Vector3 parryStarOffset = new Vector3(0.7f, 0.7f, -0.6f);
        [SerializeField] private float parryStarSize = 0.25f;
        [SerializeField] private Color parryStarColor = new Color(1f, 0.85f, 0.1f);

        [Header("Parade réussie")]
        [SerializeField] private Color parryParticleColor = new Color(1f, 0.9f, 0.4f);
        [SerializeField] private int parryParticleCount = 40;
        // Impact plus court que quand on prend un coup
        [SerializeField] private float parryHitStopDuration = 0.08f;
        [SerializeField] private float parryShakeDuration = 0.1f;
        [SerializeField] private float parryShakeMagnitude = 0.1f;
        [SerializeField] private float parryFlashDuration = 0.08f;  // flash blanc du joueur

        [Header("Ressenti des dégâts")]
        // Voile rouge à l'écran, barre de vie, son d'impact, éclats rouges
        [SerializeField] private Color hurtColor = new Color(0.85f, 0.05f, 0.08f);
        [SerializeField] private float hurtVolume = 0.8f;

        // Prévient les autres scripts (ex : musique) à chaque parade réussie
        public static event Action OnParry;
        // Parade parfaite : niveau de la chaîne (1 à 3)
        public static event Action<int> OnPerfectParry;

        // Santé globale de 0 à 1 (musique, effets d'écran)
        public float HealthRatio => body != null ? body.HealthRatio : 1f;
        public bool IsDead => body != null && body.IsDead;
        // Chaque appui sur la parade, qu'une parade parte ou non (temps de recharge)
        public event System.Action ParryPressed;
        // Volume touché par les projectiles (boîte de collision ; l'origine du joueur est à ses pieds)
        public Bounds HitBounds => box != null ? box.bounds : new Bounds(transform.position + Vector3.up * 0.4f, Vector3.one * 0.6f);
        public Vector3 HitCenter => HitBounds.center;
        public float Facing => facing;
        public bool IsGrounded => coyoteTimer > 0f;
        public bool IsParrying() => parryTimer > 0f;
        public bool HasBody => body != null && body.HasBody;
        public PlayerForm Form => body != null ? body.Form : PlayerForm.Soul;
        public bool IsRunning { get; private set; }
        // Recouture sur place en cours : 0 à 1
        public float SewProgress => sewHoldTime > 0f ? Mathf.Clamp01(sewTimer / sewHoldTime) : 0f;
        // Parades parfaites enchaînées (0 à 3) : assourdit le monde (MuffledWorld)
        public int PerfectChainLevel => perfectChain != null ? perfectChain.Level : 0;
        // Fenêtres de parade selon l'état (âme ou corps)
        // Debout, le chat pare plus vite ; l'âme et la boule restent lentes
        public float ParryWindow => Form == PlayerForm.Biped ? bodyParryWindow : soulParryWindow;
        public float ParryCooldown => Form == PlayerForm.Biped ? bodyParryCooldown : soulParryCooldown;
        public float ParryTimeLeft => Mathf.Max(0f, parryTimer);
        public float ParryCooldownLeft => Mathf.Max(0f, parryCooldownTimer);

        private Rigidbody rb;
        private BoxCollider box;
        private Vector3 baseBoxSize, baseBoxCenter;
        private PlayerBody body;
        private StrideParry stride;
        private CatSoul soul; // visuel : âme de chat
        private GameObject parryStar;
        private AudioSource sfx;
        private AudioClip hurtClip;
        private static Texture2D vignetteTex;

        // Saut
        private bool isGrounded;   // contact avec le sol au dernier pas physique
        private bool isJumping;    // montée due à un saut (pour le saut court)
        private bool jumpHeld;
        private float coyoteTimer;
        private float jumpBufferTimer;

        // Parade
        private float parryTimer;          // temps restant de la fenêtre de parade
        private float parryCooldownTimer;  // temps avant de pouvoir re-parer
        private float activeParryWindow;   // durée de la fenêtre ouverte (pour la parade parfaite)
        private bool parrySucceeded;       // la fenêtre en cours a paré quelque chose
        private float lastParryPressTime = -10f;
        private PerfectParryChain perfectChain;

        // Boule : vitesse de roulement (indépendante du frottement)
        private float ballVelocityX;

        // Chute
        private bool wasGrounded = true;
        private float airApexY;            // point le plus haut depuis qu'on a quitté le sol

        // Recouture
        private float sewTimer;
        private float sewSparkTimer;

        // Voile rouge
        private float hurtTime = -10f;      // temps réel du dernier coup
        private float hurtStrength;

        private float facing = 1f; // +1 droite, -1 gauche
        private bool isKnockedBack;
        private bool isHitStopped;
        private float moveX;

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            if (body == null)
            {
                body = gameObject.AddComponent<PlayerBody>();
            }
            perfectChain = new PerfectParryChain(perfectChainGap, 3);
        }

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            if (box != null)
            {
                baseBoxSize = box.size;
                baseBoxCenter = box.center;
            }
            // Mouvement lissé entre deux pas physiques : visuel et caméra sans saccades
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            soul = GetComponent<CatSoul>();
            if (soul == null)
            {
                soul = gameObject.AddComponent<CatSoul>();
            }
            if (GetComponent<CatBody>() == null)
            {
                gameObject.AddComponent<CatBody>();
            }
            // Ce qui se pare sans être une attaque
            if (GetComponent<CatBreath>() == null) gameObject.AddComponent<CatBreath>();
            if (GetComponent<CatHeart>() == null) gameObject.AddComponent<CatHeart>();
            stride = GetComponent<StrideParry>();
            if (stride == null) stride = gameObject.AddComponent<StrideParry>();
            if (GetComponent<DeathsDoor>() == null) gameObject.AddComponent<DeathsDoor>();
            parryStar = CreateStar();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            hurtClip = MakeHurtClip();
            parryStar.SetActive(false);
        }

        private void Update()
        {
            // Sans touche appuyée, pas de déplacement
            moveX = 0f;
            jumpHeld = false;
            IsRunning = false;
            // Mort, menu, pause, scène ou écran de fin : le joueur ne contrôle rien
            if (IsDead || !GameFlow.IsPlaying)
            {
                sewTimer = 0f;
                return;
            }
            perfectChain.Tick(Time.time);

            moveX = GameInput.MoveX;
            if (moveX != 0f)
            {
                facing = moveX;
            }
            // Course : debout sur ses 2 pattes
            IsRunning = moveX != 0f && Form == PlayerForm.Biped && body.IntactLegCount >= 2 && GameInput.Held(GameAction.Run);

            // Saut : l'appui est mémorisé un court instant, le saut part dans FixedUpdate
            jumpBufferTimer -= Time.deltaTime;
            if (GameInput.Pressed(GameAction.Jump))
            {
                jumpBufferTimer = jumpBufferTime;
            }
            jumpHeld = GameInput.Held(GameAction.Jump);

            UpdateSewing();

            // Parade : ouvre la fenêtre, et bloque un nouvel appui jusqu'à la fin de fenêtre + cooldown
            bool wasParrying = parryTimer > 0f;
            parryTimer -= Time.deltaTime;
            parryCooldownTimer -= Time.deltaTime;
            if (wasParrying && parryTimer <= 0f && !parrySucceeded)
            {
                // Fenêtre fermée sans rien parer : la chaîne de parades parfaites se rompt
                perfectChain.Reset();
            }
            if (GameInput.Pressed(GameAction.Parry))
            {
                lastParryPressTime = Time.time;
                // Tout se pare, même ce qui n'attaque pas : respiration, cœur, pas, la mort (CatBreath, CatHeart,
                // StrideParry, DeathsDoor écoutent cet appui)
                ParryPressed?.Invoke();
                if (TryAbsorbSoul())
                {
                    // Parer au contact d'un ennemi étourdi : son âme est absorbée
                }
                else if (parryCooldownTimer <= 0f)
                {
                    activeParryWindow = ParryWindow;
                    parryTimer = activeParryWindow;
                    parryCooldownTimer = activeParryWindow + ParryCooldown;
                    parrySucceeded = false;
                }
            }
        }

        // Recouture sur place : maintenir ↓, immobile au sol, avec un membre en poche
        private void UpdateSewing()
        {
            bool canSew = body.PocketCount > 0 && IsGrounded && GameInput.Held(GameAction.Sew);
            if (!canSew)
            {
                sewTimer = 0f;
                return;
            }

            // On reste sur place pendant la couture
            moveX = 0f;
            IsRunning = false;
            jumpBufferTimer = 0f;
            sewTimer += Time.deltaTime;
            sewSparkTimer -= Time.deltaTime;
            if (sewSparkTimer <= 0f)
            {
                sewSparkTimer = 0.2f;
                SpawnSparks(transform.position + new Vector3(0f, -0.3f, -0.3f), new Color(0.9f, 0.85f, 0.8f), 6, 0.4f);
            }
            if (sewTimer >= sewHoldTime && body.SewNext())
            {
                sewTimer = 0f;
                SpawnSparks(transform.position, new Color(1f, 0.4f, 0.35f), 40, 1f);
                if (soul != null) soul.OnParrySuccess(0.15f);
                GameCamera.ShakeMain(0.15f, 0.08f);
            }
        }

        // Parade au contact d'un ennemi étourdi : absorbe son âme. Renvoie true si une âme a été absorbée.
        private bool TryAbsorbSoul()
        {
            foreach (Collider c in Physics.OverlapSphere(transform.position, absorbRange))
            {
                ISoulVessel vessel = c.GetComponentInParent<ISoulVessel>();
                if (vessel == null || !vessel.CanAbsorb) continue;

                Vector3 from = vessel.Position;
                vessel.Absorb();
                BehaviorLedger.AddSoulAbsorbed();

                // L'âme bleue quitte le corps et rejoint le chat
                SpawnSparks(from, new Color(0.4f, 0.75f, 1f), 60, 1.2f);
                SpawnSparks(transform.position, new Color(0.6f, 0.9f, 1f), 30, 0.6f);
                if (soul != null) soul.OnParrySuccess(0.2f);
                GameCamera.ShakeMain(0.2f, 0.12f);
                if (!isHitStopped) StartCoroutine(HitStop(0.12f));
                return true;
            }
            return false;
        }

        // Affiche l'étoile seulement pendant la fenêtre de parade, et la garde collée au joueur
        private void LateUpdate()
        {
            bool show = IsParrying();
            parryStar.SetActive(show);
            if (show)
            {
                parryStar.transform.position = transform.position + parryStarOffset;
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float g = Physics.gravity.y; // négatif

            // isGrounded vient des collisions du pas précédent ; on le remet à zéro pour ce pas
            if (isGrounded)
            {
                coyoteTimer = coyoteTime;
            }
            else
            {
                coyoteTimer -= dt;
            }
            // Atterrissage : hauteur de chute depuis le point le plus haut
            UpdateCollider();
            bool grounded = isGrounded;
            float y = rb.position.y;
            if (grounded && !wasGrounded)
            {
                OnLanded(airApexY - y);
            }
            airApexY = grounded ? y : Mathf.Max(airApexY, y);
            wasGrounded = grounded;
            isGrounded = false;

            Vector3 v = rb.linearVelocity;
            PlayerForm form = Form;
            bool hasBody = form != PlayerForm.Soul;
            // L'âme ressent moins la gravité : saut lent et flottant
            float gravityFactor = hasBody ? 1f : soulGravityFactor;

            // Saut : vitesse calculée pour atteindre exactement la hauteur voulue
            if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isKnockedBack)
            {
                float height = form == PlayerForm.Ball ? ballJumpHeight : jumpHeight;
                if (form == PlayerForm.Biped && body.IntactLegCount == 1) height *= oneLegJumpFactor;
                v.y = Mathf.Sqrt(2f * -g * gravityFactor * height);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                isJumping = true;
            }

            // Gravité ajustée (la gravité normale est déjà appliquée par le Rigidbody)
            float gravityScale = 1f;
            if (v.y < 0f)
            {
                gravityScale = fallGravityMultiplier;
                isJumping = false;
            }
            else if (isJumping && !jumpHeld)
            {
                gravityScale = lowJumpGravityMultiplier;
            }
            if (jumpHeld && Mathf.Abs(v.y) < apexThreshold && !IsGrounded)
            {
                gravityScale = apexGravityMultiplier;
            }
            v.y += g * (gravityScale * gravityFactor - 1f) * dt;
            v.y = Mathf.Max(v.y, -(hasBody ? maxFallSpeed : soulMaxFallSpeed));

            if (isKnockedBack)
            {
                if (Mathf.Abs(v.x) > knockbackEndSpeed)
                {
                    rb.linearVelocity = v;
                    return;
                }
                isKnockedBack = false;
            }

            float targetX = moveX * CurrentSpeed();
            if (form == PlayerForm.Ball)
            {
                // La boule prend de l'élan et continue de rouler quand on lâche. Sa vitesse est gardée à part :
                // celle du Rigidbody est rongée par le frottement du sol à chaque pas physique (elle ne roulerait pas)
                float accel = moveX != 0f ? ballAcceleration : ballDeceleration;
                // Contre un mur, la physique a annulé la vitesse : l'élan est perdu
                if (Mathf.Abs(v.x) < 0.01f && Mathf.Abs(ballVelocityX) > 1f) ballVelocityX = 0f;
                ballVelocityX = Mathf.MoveTowards(ballVelocityX, targetX, accel * dt);
                targetX = ballVelocityX;
            }
            else
            {
                ballVelocityX = targetX;
            }
            rb.linearVelocity = new Vector3(targetX, v.y, 0);
        }

        // Debout, la collision monte jusqu'à la boule portée par les pattes ; le bas (les pieds) ne bouge pas
        private void UpdateCollider()
        {
            if (box == null) return;
            float height = Form == PlayerForm.Biped ? Mathf.Max(baseBoxSize.y, standingHeight) : baseBoxSize.y;
            if (Mathf.Approximately(box.size.y, height)) return;
            box.size = new Vector3(baseBoxSize.x, height, baseBoxSize.z);
            box.center = baseBoxCenter + Vector3.up * ((height - baseBoxSize.y) * 0.5f);
        }

        // Vitesse selon la forme : âme lente, boule, debout (course, une seule patte, pied perdu)
        private float CurrentSpeed()
        {
            switch (Form)
            {
                case PlayerForm.Soul: return soulSpeed;
                case PlayerForm.Ball: return ballSpeed;
                default:
                    float limp = Mathf.Pow(missingFootSpeedFactor, body.MissingFootCount);
                    // Course : les pas parés en rythme la poussent (StrideParry)
                    if (IsRunning) return runSpeed * limp * (1f + (stride != null ? stride.SpeedBonus : 0f));
                    return (body.IntactLegCount >= 2 ? bodySpeed : bodySpeed * oneLegSpeedFactor) * limp;
            }
        }

        // Chute de plus de fallDamageHeight debout : une patte est arrachée, sauf parade juste avant l'impact.
        // L'âme flotte, la boule rebondit : seules les pattes souffrent des chutes.
        private void OnLanded(float drop)
        {
            if (Form != PlayerForm.Biped || drop <= fallDamageHeight || IsDead) return;

            Vector3 feet = transform.position + Vector3.down * 0.45f;
            if (Time.time - lastParryPressTime <= fallParryTime)
            {
                // Parade de chute : poussière, petit arrêt du temps, aucun dégât
                SpawnSparks(feet, new Color(0.55f, 0.5f, 0.48f), 50, 1.1f);
                SpawnSparks(feet, parryParticleColor, 20, 0.8f);
                OnParry?.Invoke();
                if (soul != null) soul.OnParrySuccess(parryFlashDuration);
                GameCamera.ShakeMain(parryShakeDuration, parryShakeMagnitude);
                if (!isHitStopped) StartCoroutine(HitStop(0.06f));
                return;
            }

            // Chute ratée : une patte au hasard est arrachée à la cuisse (jarret et pied tombent avec)
            var thighs = new List<Limb>();
            foreach (Limb limb in body.Limbs)
            {
                if (!limb.IsTorn && limb.Kind == LimbKind.Leg) thighs.Add(limb);
            }
            if (thighs.Count > 0) body.TearLimb(thighs[Random.Range(0, thighs.Count)]);
            perfectChain.Reset();
            FeelHurt(20f, feet);
            GameCamera.ShakeMain(shakeDuration + 0.1f, shakeMagnitude + 0.2f);
            if (!isHitStopped) StartCoroutine(HitStop(hitStopDuration));
        }

        // Sol = contact dont la normale pointe vers le haut
        private void CheckGround(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= groundNormalMin)
                {
                    isGrounded = true;
                    return;
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            CheckGround(collision);
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckGround(collision);

            if (collision.gameObject.CompareTag("enemy"))
            {
                TakeDamage(10.0f, collision.transform);
            }
        }

        // Renvoie true si les dégâts ont été pris, false si le coup a été paré
        // hitPoint : point de contact du coup (pour les étincelles), optionnel
        // Projette le joueur (recul d'un coup, ou happé par une aspiration) : il ne contrôle plus sa course
        // jusqu'à ce que la vitesse retombe
        public void Knockback(Vector3 velocity)
        {
            if (rb == null || IsDead) return;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
            ballVelocityX = velocity.x;
            isKnockedBack = true;
        }

        public bool TakeDamage(float damage, Transform source = null, Vector3? hitPoint = null)
        {
            if (IsDead)
            {
                return true;
            }
            if (IsParrying())
            {
                OnParried(source, hitPoint);
                return false;
            }

            // Avec un corps, le coup touche la patte la plus proche du point d'impact
            Vector3 impact = hitPoint ?? (source != null ? source.position : transform.position);
            Limb target = body.HasBody ? body.NearestLimb(impact, facing) : null;
            bool killed = body.ApplyDamage(damage, target, impact);
            perfectChain.Reset();

            Vector3 bloodPos = transform.position;
            if (hitPoint.HasValue) bloodPos = Vector3.Lerp(transform.position, hitPoint.Value, 0.5f);
            FeelHurt(damage, bloodPos);

            if (killed)
            {
                Die();
            }
            else if (source != null && rb != null)
            {
                Vector3 dir = transform.position - source.position;
                dir.z = 0f;

                Knockback(dir.normalized * knockbackForce);
            }

            if (!killed)
            {
                // Secousse plus forte pour les gros coups
                GameCamera.ShakeMain(shakeDuration + 0.1f, shakeMagnitude + damage * 0.012f);

                if (!isHitStopped)
                {
                    StartCoroutine(HitStop(hitStopDuration));
                }
            }

            return true;
        }

        // Ressenti d'un coup reçu : flash de l'âme, voile rouge, son, éclats rouges
        private void FeelHurt(float damage, Vector3 bloodPos)
        {
            if (soul != null)
            {
                soul.OnHurt();
            }
            hurtTime = Time.unscaledTime;
            hurtStrength = Mathf.Clamp01(0.45f + damage / 40f);
            if (sfx != null && hurtClip != null)
            {
                sfx.pitch = Random.Range(0.9f, 1.08f);
                sfx.PlayOneShot(hurtClip, hurtVolume * Mathf.Clamp(0.6f + damage / 30f, 0.6f, 1.2f) * GameSettings.SfxVolume);
            }
            bloodPos.z = transform.position.z;
            SpawnSparks(bloodPos, hurtColor, 50, 1.3f);
        }

        // Parade réussie : aucun dégât, et on peut re-parer tout de suite
        private void OnParried(Transform source, Vector3? hitPoint)
        {
            // Parfaite si le coup arrive dans le tout début de la fenêtre
            float elapsed = activeParryWindow - parryTimer;
            bool perfect = elapsed <= perfectParryTime;
            parrySucceeded = true;
            parryTimer = 0f;
            parryCooldownTimer = 0f;
            OnParry?.Invoke();
            if (perfect)
            {
                OnPerfectParry?.Invoke(perfectChain.Register(Time.time));
            }

            // Étincelles au point de contact, sinon entre le joueur et l'origine du coup
            Vector3 sparkPos = transform.position + Vector3.right * facing * 0.8f;
            if (hitPoint.HasValue)
            {
                sparkPos = hitPoint.Value;
                sparkPos.z = transform.position.z;
            }
            else if (source != null)
            {
                sparkPos = Vector3.Lerp(transform.position, source.position, 0.5f);
                sparkPos.z = transform.position.z;
            }
            SpawnSparks(sparkPos, parryParticleColor, parryParticleCount, 1f);

            // Impact : flash blanc, petit tremblement, court gel du temps
            if (soul != null)
            {
                soul.OnParrySuccess(parryFlashDuration);
            }
            else
            {
                HitFlash.Play(gameObject, parryFlashDuration);
            }
            GameCamera.ShakeMain(parryShakeDuration, parryShakeMagnitude);
            if (!isHitStopped)
            {
                StartCoroutine(HitStop(parryHitStopDuration));
            }
        }

        // Fige le jeu pendant duration secondes (temps réel), puis le relance
        private IEnumerator HitStop(float duration)
        {
            isHitStopped = true;
            TimeControl.SetEffectScale(0f);
            yield return new WaitForSecondsRealtime(duration);
            TimeControl.SetEffectScale(1f);
            isHitStopped = false;
        }

        // Âme consumée : effets de mort. La réapparition est gérée par GameFlow (PlayerBody.OnDied).
        private void Die()
        {
            parryTimer = 0f;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
            GameCamera.ShakeMain(shakeDuration + 0.2f, shakeMagnitude * 2f);
        }

        // ---------- Effets ----------

        private static void SpawnSparks(Vector3 position, Color color, int count, float power)
        {
            ParticleBurst.Spawn("ParryParticles", position, color, count,
                lifetime: new Vector2(0.15f, 0.35f),
                speed: new Vector2(4f, 9f) * power,
                size: new Vector2(0.05f, 0.15f) * power,
                gravity: 1f,
                radius: 0.1f);
        }

        // Étoile à 5 branches construite en mesh (pas de collider, pas enfant du joueur pour ne pas tourner avec lui)
        private GameObject CreateStar()
        {
            const int points = 5;
            Vector3[] vertices = new Vector3[points * 2 + 1];
            int[] triangles = new int[points * 2 * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < points * 2; i++)
            {
                // Alternance pointe / creux, première pointe vers le haut
                float radius = (i % 2 == 0) ? parryStarSize : parryStarSize * 0.45f;
                float angle = Mathf.PI / 2f + i * Mathf.PI / points;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % (points * 2) + 1;
            }

            Mesh mesh = new Mesh { name = "ParryStar", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();

            GameObject star = new GameObject("ParryStar");
            star.AddComponent<MeshFilter>().mesh = mesh;
            star.AddComponent<MeshRenderer>().material = MaterialFactory.Unlit(parryStarColor);
            return star;
        }

        // Son d'impact généré une fois : coup sourd grave qui plonge + craquement bref
        private static AudioClip MakeHurtClip()
        {
            int sr = AudioSettings.outputSampleRate;
            int n = (int)(sr * 0.35f);
            float[] d = new float[n];
            System.Random r = new System.Random(3);
            float ph = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float f = 45f + 110f * Mathf.Exp(-t * 25f);
                ph += f / sr;
                float thump = Mathf.Sin(ph * 6.2831853f) * Mathf.Exp(-t / 0.09f);
                float nz = (float)r.NextDouble() * 2f - 1f;
                lp += (nz - lp) * 0.25f;
                float crunch = lp * Mathf.Exp(-t / 0.035f) * 1.4f;
                float x = (thump + crunch) * 1.6f;
                d[i] = x / (1f + Mathf.Abs(x)); // saturation douce
            }
            AudioClip c = AudioClip.Create("Hurt", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        // ---------- HUD ----------

        // Voile rouge sur les bords de l'écran : coup reçu, puis pulsation quand la santé est basse.
        // Pas de barre de vie : l'état se lit sur le personnage.
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float ratio = HealthRatio;
            float since = Time.unscaledTime - hurtTime;
            float hit = since < 0.6f ? hurtStrength * (1f - since / 0.6f) : 0f;
            float low = ratio < 0.3f ? (0.3f - ratio) / 0.3f * (0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
            float a = Mathf.Clamp01(Mathf.Max(hit, low));
            Color old = GUI.color;
            if (a > 0.01f)
            {
                if (vignetteTex == null) vignetteTex = MakeVignette();
                GUI.color = new Color(hurtColor.r, hurtColor.g, hurtColor.b, a);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTex);
            }
            GUI.color = old;
        }

        // Texture : transparente au centre, opaque sur les bords
        private static Texture2D MakeVignette()
        {
            const int s = 128;
            Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int j = 0; j < s; j++)
            {
                for (int i = 0; i < s; i++)
                {
                    float dx = (i + 0.5f) / s * 2f - 1f, dy = (j + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float al = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    t.SetPixel(i, j, new Color(1f, 1f, 1f, al));
                }
            }
            t.Apply();
            return t;
        }
    }
}
