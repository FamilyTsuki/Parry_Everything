using System;
using System.Collections;
using ParryEverything.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ParryEverything.Player
{
    // Joueur : déplacement, saut, parade, dégâts et HUD (barre de vie + voile rouge).
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Déplacement")]
        [SerializeField] private float speed = 5.0f;

        [Header("Vie")]
        [SerializeField] private float life = 100.0f;

        [Header("Saut")]
        [SerializeField] private KeyCode jumpKey = KeyCode.UpArrow;
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
        // Fenêtre active après appui, puis temps d'attente si raté
        [SerializeField] private KeyCode parryKey = KeyCode.X;
        [SerializeField] private float parryWindow = 0.35f;
        [SerializeField] private float parryCooldown = 0.4f;

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

        public float Life => life;
        public float Facing => facing;
        public bool IsGrounded => coyoteTimer > 0f;
        public bool IsParrying() => parryTimer > 0f;

        private Rigidbody rb;
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

        // HUD
        private float maxLife;
        private float displayLife;          // barre claire qui rattrape la vraie vie
        private float hurtTime = -10f;      // temps réel du dernier coup
        private float hurtStrength;

        private float facing = 1f; // +1 droite, -1 gauche
        private bool isKnockedBack;
        private bool isHitStopped;
        private float moveX;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            // Mouvement lissé entre deux pas physiques : visuel et caméra sans saccades
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            soul = GetComponent<CatSoul>();
            if (soul == null)
            {
                soul = gameObject.AddComponent<CatSoul>();
            }
            parryStar = CreateStar();
            maxLife = displayLife = life;
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
            if (Input.GetKey(KeyCode.RightArrow))
            {
                moveX = 1f;
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                moveX = -1f;
            }
            if (moveX != 0f)
            {
                facing = moveX;
            }

            // Saut : l'appui est mémorisé un court instant, le saut part dans FixedUpdate
            jumpBufferTimer -= Time.deltaTime;
            if (Input.GetKeyDown(jumpKey))
            {
                jumpBufferTimer = jumpBufferTime;
            }
            jumpHeld = Input.GetKey(jumpKey);

            // Parade : ouvre la fenêtre, et bloque un nouvel appui jusqu'à la fin de fenêtre + cooldown
            parryTimer -= Time.deltaTime;
            parryCooldownTimer -= Time.deltaTime;
            if (Input.GetKeyDown(parryKey) && parryCooldownTimer <= 0f)
            {
                parryTimer = parryWindow;
                parryCooldownTimer = parryWindow + parryCooldown;
            }
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
            isGrounded = false;

            Vector3 v = rb.linearVelocity;

            // Saut : vitesse calculée pour atteindre exactement jumpHeight
            if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isKnockedBack)
            {
                v.y = Mathf.Sqrt(2f * -g * jumpHeight);
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
            v.y += g * (gravityScale - 1f) * dt;
            v.y = Mathf.Max(v.y, -maxFallSpeed);

            if (isKnockedBack)
            {
                if (Mathf.Abs(v.x) > knockbackEndSpeed)
                {
                    rb.linearVelocity = v;
                    return;
                }
                isKnockedBack = false;
            }

            rb.linearVelocity = new Vector3(moveX * speed, v.y, 0);
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
        public bool TakeDamage(float damage, Transform source = null, Vector3? hitPoint = null)
        {
            if (IsParrying())
            {
                OnParried(source, hitPoint);
                return false;
            }

            life -= damage;
            if (soul != null)
            {
                soul.OnHurt();
            }

            // Ressenti : voile rouge, son, éclats rouges au point d'impact
            hurtTime = Time.unscaledTime;
            hurtStrength = Mathf.Clamp01(0.45f + damage / 40f);
            if (sfx != null && hurtClip != null)
            {
                sfx.pitch = Random.Range(0.9f, 1.08f);
                sfx.PlayOneShot(hurtClip, hurtVolume * Mathf.Clamp(0.6f + damage / 30f, 0.6f, 1.2f));
            }
            Vector3 bloodPos = transform.position;
            if (hitPoint.HasValue) bloodPos = Vector3.Lerp(transform.position, hitPoint.Value, 0.5f);
            bloodPos.z = transform.position.z;
            SpawnSparks(bloodPos, hurtColor, 50, 1.3f);

            if (life <= 0)
            {
                Die();
            }
            else if (source != null && rb != null)
            {
                Vector3 dir = transform.position - source.position;
                dir.z = 0f;

                rb.linearVelocity = dir.normalized * knockbackForce;
                isKnockedBack = true;
            }

            if (life > 0)
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

        // Parade réussie : aucun dégât, et on peut re-parer tout de suite
        private void OnParried(Transform source, Vector3? hitPoint)
        {
            parryTimer = 0f;
            parryCooldownTimer = 0f;
            OnParry?.Invoke();

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
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            isHitStopped = false;
        }

        private void Die()
        {
            // TODO : mort du joueur (recharger la scène, écran de game over...)
            Debug.Log("Player has died.");
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

        // Voile rouge sur les bords de l'écran (coup reçu, puis pulsation quand la vie est basse) + barre de vie
        private void OnGUI()
        {
            if (maxLife <= 0f) return;
            if (Event.current.type != EventType.Repaint) return;
            displayLife = Mathf.MoveTowards(displayLife, Mathf.Max(life, 0f), maxLife * 0.5f * Time.unscaledDeltaTime);
            if (displayLife < life) displayLife = life;

            float ratio = Mathf.Clamp01(life / maxLife);
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

            // Barre de vie en haut à gauche, tremble au moment du coup
            float w = 260f, h = 16f;
            float x = 24f, y = 22f;
            if (since < 0.3f)
            {
                x += Random.Range(-4f, 4f) * (1f - since / 0.3f);
                y += Random.Range(-3f, 3f) * (1f - since / 0.3f);
            }
            GuiDraw.FilledRect(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), new Color(0f, 0f, 0f, 0.8f));
            GuiDraw.FilledRect(new Rect(x, y, w, h), new Color(0.15f, 0.03f, 0.05f, 0.9f));
            GuiDraw.FilledRect(new Rect(x, y, w * Mathf.Clamp01(displayLife / maxLife), h), new Color(1f, 0.9f, 0.85f));
            Color lifeColor = Color.Lerp(new Color(0.85f, 0.1f, 0.15f), new Color(0.35f, 0.95f, 0.75f), ratio);
            GuiDraw.FilledRect(new Rect(x, y, w * ratio, h), Color.Lerp(lifeColor, Color.white, hit * 0.6f));
            GuiDraw.FilledRect(new Rect(x, y, w * ratio, h * 0.35f), new Color(1f, 1f, 1f, 0.2f));
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
