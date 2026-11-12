using System.Collections;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Enemies
{
    // Petit robot sauteur très simple : un cube, un œil lumineux, 2 jambes sur les côtés.
    //  - sautille sur place, se tourne vers le joueur
    //  - quand le joueur est proche : s'écrase (armement = signal pour parer), puis lui saute dessus
    //  - touche le joueur : dégâts, puis rebondit en arrière
    //  - paré : meurt (flash, écrasement, éclats)
    // Le visuel est généré en code et visible aussi dans l'éditeur hors Play ([ExecuteAlways]).
    [ExecuteAlways]
    public class JumperEnemy : MonoBehaviour
    {
        [Header("Forme")]
        [SerializeField] private float size = 0.6f;                 // taille globale (diamètre du collider)
        [SerializeField] private Color bodyColor = new Color(0.75f, 0.78f, 0.82f);
        [SerializeField] private Color legColor = new Color(0.25f, 0.27f, 0.3f);
        [SerializeField] private Color eyeIdleColor = new Color(0.2f, 0.9f, 1f);
        [SerializeField] private Color eyeAttackColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private float turnToCamera = 40f;          // degrés : se montre un peu de 3/4
        [SerializeField] private float turnSpeed = 720f;            // vitesse pour se retourner (degrés/s)

        [Header("Comportement")]
        [SerializeField] private float detectRange = 6f;            // distance à laquelle il repère le joueur
        [SerializeField] private float windupDuration = 0.45f;      // écrasement avant le saut (temps pour parer)
        [SerializeField] private float jumpHeight = 1.6f;           // hauteur du saut d'attaque
        [SerializeField] private float maxJumpDistance = 5f;
        [SerializeField] private float cooldown = 1.2f;             // repos entre deux attaques
        [SerializeField] private float hopHeight = 0.25f;           // petits sauts au repos
        [SerializeField] private float hopInterval = 0.8f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float bounceBack = 4f;             // recul après avoir touché le joueur

        [Header("Mort (paré)")]
        [SerializeField] private float deathDuration = 0.25f;
        [SerializeField] private int deathParticleCount = 25;

        private enum State { Idle, Windup, Jumping, Dead }

        private Rigidbody rb;
        private SphereCollider col;
        private PlayerController target;

        // Visuel
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private GameObject visual;
        private Transform body;
        private Material eyeMat;
        private readonly Transform[] legs = new Transform[2];
        private float yaw;
        private float groundTimer;                    // >0 : touche le sol (pour le visuel)

        // État
        private State state = State.Idle;
        private bool grounded;
        private bool hasHit;
        private float timer;
        private float facing = 1f;
        private float squash, squashVel;          // >0 écrasé, <0 étiré

        private void OnEnable()
        {
            BuildVisual();
            if (Application.isPlaying)
            {
                SetupPhysics();
            }
        }

        private void OnDisable()
        {
            generated.DestroyAll();
            visual = null;
        }

        private void OnValidate()
        {
            // Réglages modifiés dans l'Inspector : on reconstruit le visuel au prochain tick
            // (créer/détruire des objets est interdit pendant OnValidate)
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

        private void SetupPhysics()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.mass = 1f;

            col = GetComponent<SphereCollider>();
            if (col == null) col = gameObject.AddComponent<SphereCollider>();
            col.radius = size * 0.5f;

            // Le cube d'origine éventuel ne sert plus
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            target = FindFirstObjectByType<PlayerController>();
            timer = hopInterval;
        }

        // ---------- Visuel : robot très simple ----------
        // Un cube pour le corps, un œil carré lumineux, 2 jambes (bâtons) sur les côtés.
        // Repère du modèle : avant = +X, haut = +Y, côtés = ±Z. Unité = size.

        private void BuildVisual()
        {
            if (visual != null) return;

            visual = generated.Track(new GameObject("JumperVisual"));
            visual.transform.SetParent(transform, false);

            Material bodyMat = generated.Track(MaterialFactory.Lit(bodyColor));
            Material legMat = generated.Track(MaterialFactory.Lit(legColor));
            eyeMat = generated.Track(MaterialFactory.Lit(eyeIdleColor));
            eyeMat.EnableKeyword("_EMISSION");

            body = Cube("Body", visual.transform, bodyMat);
            Transform eye = Cube("Eye", body, eyeMat);
            eye.localPosition = new Vector3(0.51f, 0.12f, 0f);   // collé à la face avant du cube
            eye.localScale = new Vector3(0.05f, 0.22f, 0.3f);

            for (int i = 0; i < 2; i++)
            {
                legs[i] = Cube("Leg", visual.transform, legMat);
            }
            yaw = YawFor(facing);
            UpdateVisual(0f);
        }

        private Transform Cube(string cubeName, Transform parent, Material mat)
        {
            GameObject go = generated.Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = cubeName;
            GeneratedObjects.SafeDestroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private float YawFor(float f)
        {
            // Tourné vers sa cible, et un peu vers la caméra (-Z) pour voir les deux jambes
            return f > 0f ? turnToCamera : 180f - turnToCamera;
        }

        private void UpdateVisual(float dt)
        {
            if (visual == null) return;
            float s = size;
            bool inAir = Application.isPlaying && groundTimer <= 0f;

            // Accroupi pendant l'armement (ressort = petit rebond)
            float squashTarget = state == State.Windup ? 1f : 0f;
            if (dt > 0f)
            {
                squashVel += (250f * (squashTarget - squash) - 14f * squashVel) * dt;
                squash += squashVel * dt;
                yaw = Mathf.MoveTowardsAngle(yaw, YawFor(facing), turnSpeed * dt);
            }
            else
            {
                yaw = YawFor(facing);
            }
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Corps : cube posé sur les jambes, plus bas quand il s'accroupit, penché pendant l'attaque
            float bodySize = 0.6f * s;
            float bottom = -0.5f * s;                                  // le sol (bas du collider)
            float legLen = inAir ? 0.12f * s : (0.22f - 0.12f * squash) * s;
            float bodyY = bottom + legLen + bodySize * 0.5f;
            body.localPosition = new Vector3(0f, bodyY, 0f);
            body.localScale = Vector3.one * bodySize;
            body.localRotation = Quaternion.Euler(0f, 0f, state == State.Jumping ? -15f : 0f);

            // Œil : cyan au repos, rouge pendant l'armement et l'attaque
            Color ec = (state == State.Windup || state == State.Jumping) ? eyeAttackColor : eyeIdleColor;
            eyeMat.color = ec;
            eyeMat.SetColor("_EmissionColor", ec * 2f);

            // Jambes : bâtons sous les côtés du corps, du bas du cube jusqu'au sol
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                Transform leg = legs[i];
                leg.localScale = new Vector3(0.1f * s, legLen, 0.1f * s);
                leg.localPosition = new Vector3(0f, bodyY - bodySize * 0.5f - legLen * 0.5f, side * bodySize * 0.35f);
            }
        }

        // ---------- Comportement ----------

        private void Update()
        {
            if (!Application.isPlaying)
            {
                UpdateVisual(0f);
                return;
            }
            if (target != null && state != State.Dead && state != State.Jumping)
            {
                facing = target.transform.position.x < transform.position.x ? -1f : 1f;
            }
            groundTimer -= Time.deltaTime;
            UpdateVisual(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || rb == null || state == State.Dead) return;

            timer -= Time.fixedDeltaTime;
            float dist = target != null ? Mathf.Abs(target.transform.position.x - transform.position.x) : Mathf.Infinity;

            switch (state)
            {
                case State.Idle:
                    if (grounded && timer <= 0f)
                    {
                        if (dist <= detectRange)
                        {
                            state = State.Windup;
                            timer = windupDuration;
                        }
                        else
                        {
                            Jump(0f, hopHeight);
                            timer = hopInterval;
                        }
                    }
                    break;

                case State.Windup:
                    if (timer <= 0f) Attack();
                    break;

                case State.Jumping:
                    CheckHit();
                    if (grounded && rb.linearVelocity.y <= 0.01f)
                    {
                        state = State.Idle;
                        timer = cooldown;
                    }
                    break;
            }
            grounded = false; // recalculé par les collisions du prochain pas
        }

        // Saut en cloche qui retombe sur la position actuelle du joueur
        private void Attack()
        {
            float g = -Physics.gravity.y;
            float vy = Mathf.Sqrt(2f * g * jumpHeight);
            float flightTime = 2f * vy / g;
            float dx = target != null ? target.transform.position.x - transform.position.x : 0f;
            dx = Mathf.Clamp(dx, -maxJumpDistance, maxJumpDistance);
            rb.linearVelocity = new Vector3(dx / flightTime, vy, 0f);

            state = State.Jumping;
            hasHit = false;
            grounded = false;
        }

        private void Jump(float vx, float height)
        {
            float vy = Mathf.Sqrt(2f * -Physics.gravity.y * height);
            rb.linearVelocity = new Vector3(vx, vy, 0f);
            squashVel -= 4f; // petit étirement au décollage
        }

        private void CheckHit()
        {
            if (hasHit) return;
            Collider[] hits = Physics.OverlapSphere(transform.position, size * 0.6f);
            foreach (Collider hit in hits)
            {
                PlayerController p = hit.GetComponentInParent<PlayerController>();
                if (p == null) continue;

                hasHit = true;
                Vector3 contact = Vector3.Lerp(transform.position, p.transform.position, 0.5f);
                if (p.TakeDamage(damage, transform, contact))
                {
                    // Touché : rebondit en arrière
                    float away = transform.position.x < p.transform.position.x ? -1f : 1f;
                    rb.linearVelocity = new Vector3(away * bounceBack, bounceBack, 0f);
                }
                else
                {
                    StartCoroutine(Die());
                }
                return;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y > 0.6f)
                {
                    grounded = true;
                    groundTimer = 0.08f;
                    return;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnCollisionStay(collision);
        }

        // Paré : flash blanc, écrasement, éclats, puis disparition
        private IEnumerator Die()
        {
            state = State.Dead;
            rb.isKinematic = true;
            col.enabled = false;

            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                HitFlash.Play(r.gameObject, deathDuration);
            }

            // Temps réel : l'arrêt du temps de la parade ne bloque pas l'animation
            float t = 0f;
            Vector3 start = visual.transform.localScale;
            while (t < deathDuration)
            {
                float k = t / deathDuration;
                visual.transform.localScale = new Vector3(start.x * (1f + k), start.y * (1f - k), start.z * (1f + k));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            ParticleBurst.Spawn("JumperDeath", transform.position, bodyColor, deathParticleCount,
                lifetime: new Vector2(0.3f, 0.6f),
                speed: new Vector2(2f, 6f),
                size: new Vector2(0.08f, 0.2f),
                gravity: 1.5f,
                radius: size * 0.3f);
            Destroy(gameObject);
        }
    }
}
