using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Projectiles du boss. Apparence (ProjectileLook) : caillou, morceau du bol, ou orbe lumineuse (null).
    //  - Orbe / caillou : vole vers le joueur. Paré : renvoyé vers le boss (bleu, plus rapide) et le blesse.
    //  - Onde de choc : file au ras du sol. On peut sauter par-dessus, ou la parer (elle se brise).
    //  - Météore / chute de pierre : zone marquée au sol, puis tombe du ciel. Paré : renvoyé vers le boss.
    //  - Jugement : énorme projectile. Paré, le boss le renvoie (rallies fois) avant de le prendre de plein fouet.
    //  - Pilier / pic de roche : zone marquée au sol (elle se remplit, puis flashe à l'instant où il jaillit).
    //    Haut (infranchissable) ou bas (se saute). Parer un pilier neutralise toute sa salve.
    public class BossProjectile : MonoBehaviour
    {
        public enum Kind { Orb, Shockwave, Pillar }

        // Salve de piliers : une seule parade suffit pour toute la salve
        public sealed class PillarSalvo
        {
            public bool Parried;
        }

        private const float LowPillarHeight = 1.3f;     // pilier bas : se franchit d'un saut
        private const float PillarHitTime = 0.4f;       // durée pendant laquelle un pilier haut blesse
        private const float LowPillarHitTime = 0.25f;   // plus court pour un pilier bas (laisse le temps de le sauter)
        private const float PillarFlashTime = 0.1f;     // flash de la marque juste avant le jaillissement

        private Kind kind;
        private Vector3 velocity;
        private float damage;
        private float reflectDamage;
        private HellGuardian owner;
        private PlayerController target;
        private bool reflected;
        private float life = 6f;
        private float groundY;
        private Material mat;
        private Color reflectColor = new Color(0.4f, 0.8f, 1f);
        private Transform visual;
        private int rallies;              // renvois restants du boss (orbe du Jugement)
        private float delay;              // météore : attente avant la chute
        private Transform marker;         // météore : zone marquée au sol
        private Color baseColor;
        private float height;             // fente / pilier : hauteur de la zone dangereuse
        private bool spent;               // pilier : a déjà frappé
        private float age;
        private float delayTotal;         // pilier : attente totale (remplissage de la marque)
        private bool lowPillar;
        private PillarSalvo salvo;
        private bool neutralized;         // pilier : salve parée, couleur de parade appliquée
        private float baseEmission = 2.5f;  // lueur au repos (0 pour un caillou)
        private float hitSize;              // orbe / caillou : diamètre de la zone de contact
        private ProjectileLook look;        // matière des éclats quand il se brise

        public static BossProjectile SpawnOrb(Vector3 pos, Vector3 dir, float speed, float damage, float reflectDamage, HellGuardian owner, Color color, float size = 0.45f, ProjectileLook look = null)
        {
            BossProjectile p = Create("BossOrb", pos, Kind.Orb, damage, owner, color, look);
            p.velocity = dir.normalized * speed;
            p.reflectDamage = reflectDamage;
            p.hitSize = size;
            p.look = look;
            p.groundY = owner != null ? owner.GroundY : pos.y - 50f;

            GameObject orb = p.Part(look != null ? look.Mesh() : LowPoly.Icosahedron(0.1f, Random.Range(0, 999)));
            orb.transform.localScale = Vector3.one * (look != null ? look.Scale(size) : size);

            // Traînée : lumineuse (orbe) ou poussière (caillou)
            Color tc = look != null ? look.TrailColor : color;
            TrailRenderer trail = p.gameObject.AddComponent<TrailRenderer>();
            trail.time = look != null ? 0.15f : 0.25f;
            trail.startWidth = size * (look != null ? 0.5f : 0.8f);
            trail.endWidth = 0f;
            trail.material = MaterialFactory.Unlit(Color.white);
            trail.startColor = tc;
            trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);
            return p;
        }

        // Météore : marque la zone au sol, attend 'delay', puis tombe tout droit
        public static void SpawnMeteor(float x, float groundY, float delay, float speed, float damage, float reflectDamage, HellGuardian owner, Color color, ProjectileLook look = null)
        {
            BossProjectile p = SpawnOrb(new Vector3(x, groundY + 12f, 0f), Vector3.down, speed, damage, reflectDamage, owner, color, 0.95f, look);
            p.delay = delay;
            p.groundY = groundY;
            p.life = delay + 3f;

            // Zone au sol : disque plat lumineux
            GameObject m = new GameObject("MeteorMarker");
            m.transform.position = new Vector3(x, groundY + 0.03f, 0f);
            m.AddComponent<MeshFilter>().sharedMesh = LowPoly.Prism(8, 1.3f, 1.3f, 0.04f);
            m.AddComponent<MeshRenderer>().sharedMaterial = p.mat;
            p.marker = m.transform;
        }

        // Orbe du Jugement : énorme, lente, renvoyée 'rallies' fois par le boss
        public static BossProjectile SpawnJudgment(Vector3 pos, Vector3 dir, float speed, float damage, float reflectDamage, int rallies, HellGuardian owner, Color color, ProjectileLook look = null)
        {
            BossProjectile p = SpawnOrb(pos, dir, speed, damage, reflectDamage, owner, color, 1.3f, look);
            p.rallies = rallies;
            p.life = 10f;
            return p;
        }

        // Pilier : marque au sol pendant 'delay', puis colonne de 'height' qui jaillit (pic de roche si 'look').
        // 'low' : pilier bas qu'on saute (petite marque). 'salvo' : partagée par tous les piliers d'une même attaque.
        public static void SpawnPillar(float x, float groundY, float delay, float damage, HellGuardian owner, Color color, float height, bool low, PillarSalvo salvo, ProjectileLook look = null)
        {
            BossProjectile p = Create("BossPillar", new Vector3(x, groundY, 0f), Kind.Pillar, damage, owner, color, look);
            p.groundY = groundY;
            p.lowPillar = low;
            p.height = low ? LowPillarHeight : height;
            p.delay = delay;
            p.delayTotal = Mathf.Max(delay, 0.01f);
            p.salvo = salvo;
            p.life = 0.75f;
            GameObject col = p.Part(look != null ? LowPoly.Prism(5, 0.62f, 0.05f, 1f, 0.3f) : LowPoly.Prism(6, 0.6f, 0.35f, 1f, 0.4f));
            col.transform.localScale = new Vector3(1f, 0f, 1f);
            GameObject m = new GameObject("PillarMarker");
            m.transform.position = new Vector3(x, groundY + 0.03f, 0f);
            m.AddComponent<MeshFilter>().sharedMesh = LowPoly.Prism(6, 0.75f, 0.75f, 0.04f);
            m.AddComponent<MeshRenderer>().sharedMaterial = p.mat;
            p.marker = m.transform;
        }

        public static void SpawnShockwave(Vector3 groundPos, float dir, float speed, float damage, HellGuardian owner, Color color, float size = 1f, ProjectileLook look = null)
        {
            BossProjectile p = Create("BossShockwave", groundPos, Kind.Shockwave, damage, owner, color, look);
            p.velocity = new Vector3(Mathf.Sign(dir) * speed, 0f, 0f);
            p.groundY = groundPos.y;
            p.life = 2.2f;

            // Rangée de pics qui sortent du sol
            p.height = 1.1f * size;
            Mesh spike = LowPoly.Prism(4, 0.22f * size, 0f, 1f);
            for (int i = 0; i < 3; i++)
            {
                GameObject s = p.Part(spike);
                s.transform.localPosition = new Vector3(-Mathf.Sign(dir) * i * 0.3f * size, 0f, 0f);
                s.transform.localScale = new Vector3(1f, (0.9f - i * 0.25f) * size, 1f);
            }
        }

        private static BossProjectile Create(string name, Vector3 pos, Kind kind, float damage, HellGuardian owner, Color color, ProjectileLook look = null)
        {
            GameObject go = new GameObject(name);
            go.transform.position = pos;
            BossProjectile p = go.AddComponent<BossProjectile>();
            p.kind = kind;
            p.damage = damage;
            p.owner = owner;
            p.target = FindFirstObjectByType<PlayerController>();

            if (look != null)
            {
                p.mat = look.CreateMaterial();
                p.baseColor = look.BaseColor(p.mat);
                p.baseEmission = 0f;
            }
            else
            {
                p.baseColor = color;
                p.mat = MaterialFactory.Lit(color);
                p.mat.EnableKeyword("_EMISSION");
                p.mat.SetColor("_EmissionColor", color * 2.5f);
            }

            p.visual = new GameObject("Visual").transform;
            p.visual.SetParent(go.transform, false);
            return p;
        }

        private GameObject Part(Mesh mesh)
        {
            GameObject go = new GameObject("Part");
            go.transform.SetParent(visual, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Météore / pilier : la zone au sol clignote avant le coup
            if (delay > 0f)
            {
                delay -= dt;
                if (kind == Kind.Pillar) UpdatePillarMarker();
                else if (marker != null) marker.localScale = Vector3.one * (0.6f + Mathf.PingPong(Time.time * 6f, 0.4f));
                if (delay <= 0f && kind == Kind.Pillar)
                {
                    if (marker != null) Destroy(marker.gameObject);
                    if (!neutralized) SetColor(baseColor, baseEmission);
                }
                return;
            }
            age += dt;
            if (kind == Kind.Pillar)
            {
                UpdatePillar(dt);
                return;
            }
            Vector3 prev = transform.position;
            transform.position += velocity * dt;
            life -= dt;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (kind == Kind.Orb)
            {
                visual.Rotate(200f * dt, 300f * dt, 0f);
                if (reflected)
                {
                    // Renvoyé vers le boss : ignore le décor (la parade n'est jamais perdue)
                    UpdateReflected();
                    return;
                }
                if (ProjectileHits.TouchesPlayer(target, prev, transform.position, 0.1f + hitSize * 0.35f))
                {
                    HitPlayer();
                    return;
                }
                // Sol, mur, objet du décor : il se brise
                if (ProjectileHits.HitsWorld(prev, transform.position, hitSize * 0.25f, groundY, out Vector3 at))
                {
                    ProjectileHits.Shatter(at, hitSize, look, baseColor);
                    Destroy(gameObject);
                }
            }
            else
            {
                // Les pics ondulent en avançant
                float k = 1f + Mathf.Sin(Time.time * 30f) * 0.15f;
                visual.localScale = new Vector3(1f, k, 1f);
                CheckPlayer(0f);
            }
        }

        // Marque du pilier : se remplit pendant l'attente, puis flashe blanc à l'instant du jaillissement.
        // Pilier bas : marque plus petite.
        private void UpdatePillarMarker()
        {
            NeutralizeIfParried();
            if (marker == null) return;
            float fill = 1f - Mathf.Clamp01(delay / delayTotal);
            float size = (lowPillar ? 0.6f : 1f) * Mathf.Lerp(0.3f, 1f, fill);
            marker.localScale = new Vector3(size, 1f, size);
            if (!neutralized && delay < PillarFlashTime) mat.SetColor("_EmissionColor", Color.white * 4f);
        }

        // Salve parée : les piliers restants deviennent inoffensifs et prennent la couleur de parade
        private void NeutralizeIfParried()
        {
            if (neutralized || salvo == null || !salvo.Parried) return;
            neutralized = true;
            spent = true;
            SetColor(reflectColor, 1.5f);
        }

        // Colonne : jaillit en 0,08 s, reste, puis retombe. Frappe une seule fois.
        private void UpdatePillar(float dt)
        {
            NeutralizeIfParried();
            float grow = Mathf.Clamp01(age / 0.08f) * Mathf.Clamp01((0.75f - age) / 0.2f);
            Transform col = visual.GetChild(0);
            col.localScale = new Vector3(1f + Mathf.Sin(Time.time * 35f) * 0.08f, height * grow, 1f + Mathf.Cos(Time.time * 31f) * 0.08f);
            float hitTime = lowPillar ? LowPillarHitTime : PillarHitTime;
            if (!spent && age < hitTime && target != null)
            {
                Vector3 pp = target.transform.position;
                if (Mathf.Abs(pp.x - transform.position.x) < 0.75f && pp.y - groundY < height * grow)
                {
                    spent = true;
                    bool hurt = target.TakeDamage(damage, transform, new Vector3(transform.position.x, pp.y, 0f));
                    if (!hurt && salvo != null) salvo.Parried = true;
                }
            }
            life -= dt;
            if (life <= 0f) Destroy(gameObject);
        }

        // Onde au sol : le joueur doit être dessus et sous sa hauteur
        private void CheckPlayer(float radius)
        {
            if (target == null) return;
            Vector3 pp = target.transform.position;
            if (Mathf.Abs(pp.x - transform.position.x) < 0.45f && pp.y - groundY < height) HitPlayer();
        }

        private void HitPlayer()
        {
            Vector3 hitPoint = Vector3.Lerp(transform.position, target.HitCenter, 0.5f);
            if (target.TakeDamage(damage, transform, hitPoint) || kind != Kind.Orb)
            {
                // Touché, ou onde parée : le projectile se brise sur lui
                if (kind == Kind.Orb) ProjectileHits.Shatter(transform.position, hitSize, look, baseColor);
                Destroy(gameObject);
                return;
            }

            // Orbe paré : renvoyé vers le boss
            reflected = true;
            life = 4f;
            SetColor(reflectColor, 3f);
            if (marker != null) Destroy(marker.gameObject);
            AimAtOwner(Mathf.Max(velocity.magnitude * 1.5f, 10f));
        }

        private void SetColor(Color c, float emission)
        {
            mat.color = c;
            mat.SetColor("_EmissionColor", c * emission);
            TrailRenderer trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startColor = c;
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }

        private void UpdateReflected()
        {
            if (owner == null || owner.IsDead)
            {
                Destroy(gameObject);
                return;
            }
            // Légèrement guidé : le boss bouge
            AimAtOwner(velocity.magnitude);
            if (Vector3.Distance(transform.position, owner.CorePosition) < 0.9f)
            {
                // Jugement : le boss la renvoie au joueur, plus vite
                if (rallies > 0 && owner.ReturnOrb())
                {
                    rallies--;
                    reflected = false;
                    SetColor(baseColor, baseEmission);
                    if (target != null)
                    {
                        Vector3 d = target.HitCenter - transform.position;
                        d.z = 0f;
                        velocity = d.normalized * velocity.magnitude * 0.8f;
                    }
                    return;
                }
                owner.TakeHit(reflectDamage);
                Destroy(gameObject);
            }
        }

        private void AimAtOwner(float speed)
        {
            if (owner == null) return;
            Vector3 d = owner.CorePosition - transform.position;
            d.z = 0f;
            velocity = d.normalized * speed;
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
            if (mat != null) Destroy(mat);
        }
    }
}
