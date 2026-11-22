using System.Collections.Generic;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Projectile d'échange (famille du Jugement), caillou ou morceau du bol (Look ; null : bulle lumineuse) :
    // vole vers le joueur ; paré, il repart vers le boss, qui le rattrape et le relance tant qu'il lui reste
    // des échanges, puis le prend de plein fouet.
    // Variantes (réglées à la création) :
    //  - division : au retour, le boss la coupe en 2 morceaux plus petits, lancés l'un après l'autre ;
    //  - accélération : chaque renvoi multiplie sa vitesse ;
    //  - feinte : en route vers le joueur, elle peut s'arrêter un instant en l'air, puis repartir.
    public class RallyBubble : MonoBehaviour
    {
        public struct Settings
        {
            public float Speed;            // vitesse de départ
            public float Size;             // diamètre
            public float Damage;           // dégâts au joueur
            public float ReflectDamage;    // dégâts au boss quand elle le frappe
            public int Rallies;            // renvois du boss avant de la prendre
            public int Splits;             // divisions restantes (chacune double le nombre de bulles)
            public float SpeedUp;          // facteur de vitesse à chaque renvoi
            public float FeintChance;      // chance de feinte à chaque passage vers le joueur
            public float SplitStagger;     // écart entre les deux morceaux d'une division
            public Color Color;
            public ProjectileLook Look;    // null : bulle lumineuse
        }

        private enum State { Waiting, ToPlayer, Feint, ToBoss }

        private static readonly HashSet<RallyBubble> active = new HashSet<RallyBubble>();
        private static readonly Color ReflectColor = new Color(0.4f, 0.8f, 1f);

        // Bulles d'échange encore en jeu (le boss attend qu'il n'en reste plus)
        public static int ActiveCount => active.Count;

        private Settings s;
        private HellGuardian owner;
        private PlayerController target;
        private State state;
        private float speed;
        private Vector3 velocity;
        private float timer;               // attente avant le départ / durée de la feinte
        private float feintAt = -1f;       // distance parcourue à laquelle elle feinte (-1 : pas de feinte)
        private float travelled;
        private float life = 14f;
        private Vector3 holdOffset;        // décalage par rapport au boss pendant l'attente
        private Material mat;
        private Transform visual;
        private TrailRenderer trail;
        private Color baseColor;
        private float baseEmission;
        private Color trailColor;

        // Crée une bulle près du boss (décalée de 'holdOffset') ; elle part vers le joueur après 'wait' secondes
        public static RallyBubble Spawn(Vector3 pos, Settings settings, HellGuardian owner, float wait = 0f, Vector3 holdOffset = default)
        {
            GameObject go = new GameObject("RallyBubble");
            go.transform.position = pos;
            RallyBubble b = go.AddComponent<RallyBubble>();
            b.s = settings;
            b.holdOffset = holdOffset;
            b.owner = owner;
            b.speed = settings.Speed;
            b.target = FindFirstObjectByType<PlayerController>();
            b.Build();
            b.state = State.Waiting;
            b.timer = wait;
            return b;
        }

        private void Build()
        {
            ProjectileLook look = s.Look;
            if (look != null)
            {
                mat = look.CreateMaterial();
                baseColor = look.BaseColor(mat);
                baseEmission = 0f;
            }
            else
            {
                mat = MaterialFactory.Lit(s.Color);
                mat.EnableKeyword("_EMISSION");
                baseColor = s.Color;
                baseEmission = 2.5f;
            }
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * (look != null ? look.Scale(s.Size) : s.Size);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = look != null ? look.Mesh() : LowPoly.Icosahedron(0.1f, Random.Range(0, 999));
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = look != null ? 0.15f : 0.25f;
            trail.startWidth = s.Size * (look != null ? 0.5f : 0.8f);
            trail.endWidth = 0f;
            trail.material = MaterialFactory.Unlit(Color.white);
            trailColor = look != null ? look.TrailColor : s.Color;
            SetColor(baseColor, baseEmission);
        }

        private void OnEnable() { active.Add(this); }
        private void OnDisable() { active.Remove(this); }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (owner == null || owner.IsDead)
            {
                Burst();
                return;
            }
            life -= dt;
            if (life <= 0f)
            {
                Burst();
                return;
            }
            visual.Rotate(200f * dt, 300f * dt, 0f);

            switch (state)
            {
                case State.Waiting:
                    // Tenue près du boss en attendant son départ
                    transform.position = Vector3.Lerp(transform.position, owner.CorePosition + holdOffset, 10f * dt);
                    timer -= dt;
                    if (timer <= 0f) Launch();
                    break;

                case State.ToPlayer:
                {
                    Vector3 prev = transform.position;
                    Move(dt);
                    if (CheckPlayer(prev)) break;
                    // Sol, mur, objet du décor : il se brise (l'échange s'arrête là)
                    if (ProjectileHits.HitsWorld(prev, transform.position, s.Size * 0.25f, owner.GroundY, out Vector3 at))
                    {
                        transform.position = at;
                        Burst();
                        break;
                    }
                    if (feintAt >= 0f && travelled >= feintAt)
                    {
                        feintAt = -1f;
                        state = State.Feint;
                        timer = Random.Range(0.25f, 0.55f);
                    }
                    break;
                }

                case State.Feint:
                    // Suspendue, elle tremble, puis repart droit sur le joueur
                    visual.localPosition = Random.insideUnitSphere * 0.06f;
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        visual.localPosition = Vector3.zero;
                        AimAtPlayer();
                        state = State.ToPlayer;
                    }
                    CheckPlayer(transform.position);
                    break;

                case State.ToBoss:
                    // Légèrement guidée : le boss bouge
                    velocity = Flat(owner.CorePosition - transform.position).normalized * velocity.magnitude;
                    Move(dt);
                    if (Vector3.Distance(transform.position, owner.CorePosition) < 0.9f) ReachBoss();
                    break;
            }
        }

        private void Launch()
        {
            state = State.ToPlayer;
            AimAtPlayer();
            // Feinte : quelque part entre le tiers et les deux tiers du trajet
            if (target != null && Random.value < s.FeintChance)
                feintAt = Vector3.Distance(transform.position, target.HitCenter) * Random.Range(0.35f, 0.65f);
            travelled = 0f;
        }

        private void AimAtPlayer()
        {
            if (target == null) return;
            velocity = Flat(target.HitCenter - transform.position).normalized * speed;
        }

        private void Move(float dt)
        {
            Vector3 step = velocity * dt;
            transform.position += step;
            travelled += step.magnitude;
        }

        // Contact avec le joueur sur le trajet de l'image (depuis 'from') ; vrai si la bulle a touché ou été parée
        private bool CheckPlayer(Vector3 from)
        {
            if (target == null) return false;
            if (!ProjectileHits.TouchesPlayer(target, from, transform.position, 0.1f + s.Size * 0.35f)) return false;

            Vector3 hitPoint = Vector3.Lerp(transform.position, target.HitCenter, 0.5f);
            if (target.TakeDamage(s.Damage, transform, hitPoint))
            {
                Burst();
                return true;
            }
            // Parée : repart vers le boss, plus vite
            state = State.ToBoss;
            feintAt = -1f;
            visual.localPosition = Vector3.zero;
            SetColor(ReflectColor, 3f);
            velocity = Flat(owner.CorePosition - transform.position).normalized * Mathf.Max(speed * 1.5f, 10f);
            return true;
        }

        private void ReachBoss()
        {
            if (s.Splits > 0 && owner.ReturnOrb())
            {
                // Division : deux morceaux plus petits, lancés l'un après l'autre
                // L'écart double à chaque génération : les morceaux de deux parents s'intercalent
                // (t, t+2e, puis t+e, t+3e) au lieu d'arriver en même temps
                Settings child = s;
                child.Splits = s.Splits - 1;
                child.SplitStagger = s.SplitStagger * 2f;
                child.Size = s.Size * 0.75f;
                child.Speed = speed * s.SpeedUp;
                child.ReflectDamage = s.ReflectDamage * 0.5f;
                Spawn(transform.position, child, owner, 0.15f);
                Spawn(transform.position, child, owner, 0.15f + s.SplitStagger);
                Burst();
                return;
            }
            if (s.Rallies > 0 && owner.ReturnOrb())
            {
                // Renvoi : repart vers le joueur, plus vite si l'échange est truqué
                s.Rallies--;
                speed *= s.SpeedUp;
                SetColor(baseColor, baseEmission);
                Launch();
                return;
            }
            owner.TakeHit(s.ReflectDamage);
            Burst();
        }

        private void Burst()
        {
            // Caillou / tesson : se brise en éclats de sa matière
            if (s.Look != null && state != State.Waiting)
            {
                ProjectileHits.Shatter(transform.position, s.Size, s.Look, baseColor);
                Destroy(gameObject);
                return;
            }
            ParticleBurst.Spawn("RallyBubbleBurst", transform.position, new ParticleSystem.MinMaxGradient(mat.color, Color.white), 14,
                lifetime: new Vector2(0.3f, 0.7f),
                speed: new Vector2(1.5f, 5f),
                size: new Vector2(0.05f, 0.18f),
                gravity: 0.5f,
                radius: s.Size * 0.4f);
            Destroy(gameObject);
        }

        private void SetColor(Color c, float emission)
        {
            mat.color = c;
            mat.SetColor("_EmissionColor", c * emission);
            Color t = emission > 0f ? c : trailColor;
            trail.startColor = t;
            trail.endColor = new Color(t.r, t.g, t.b, 0f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.z = 0f;
            return v;
        }

        private void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
