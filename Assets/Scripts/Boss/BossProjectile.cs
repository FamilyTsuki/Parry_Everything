using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Projectiles du Gardien des Enfers.
    //  - Orbe : vole vers le joueur. Paré : renvoyé vers le boss (bleu, plus rapide) et le blesse.
    //  - Onde de choc : file au ras du sol. On peut sauter par-dessus, ou la parer (elle se brise).
    //  - Météore : zone marquée au sol, puis tombe du ciel. Paré : renvoyé vers le boss comme un orbe.
    //  - Jugement : énorme orbe. Parée, le boss la renvoie (rallies fois) avant de la prendre de plein fouet.
    //  - Fente : croissant de feu lancé par la faux, file à l'horizontale. Parée, elle se brise.
    //  - Pilier : zone marquée au sol, puis colonne de feu qui jaillit. Parée, elle ne blesse pas.
    public class BossProjectile : MonoBehaviour
    {
        public enum Kind { Orb, Shockwave, Slash, Pillar }

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
        private bool isMeteor;
        private Color baseColor;
        private float height;             // fente / pilier : hauteur de la zone dangereuse
        private bool spent;               // pilier : a déjà frappé
        private float age;

        public static BossProjectile SpawnOrb(Vector3 pos, Vector3 dir, float speed, float damage, float reflectDamage, HellGuardian owner, Color color, float size = 0.45f)
        {
            BossProjectile p = Create("BossOrb", pos, Kind.Orb, damage, owner, color);
            p.velocity = dir.normalized * speed;
            p.reflectDamage = reflectDamage;

            GameObject orb = p.Part(LowPoly.Icosahedron(0.1f, Random.Range(0, 999)));
            orb.transform.localScale = Vector3.one * size;

            // Traînée lumineuse
            TrailRenderer trail = p.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = size * 0.8f;
            trail.endWidth = 0f;
            trail.material = MaterialFactory.Unlit(Color.white);
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            return p;
        }

        // Météore : marque la zone au sol, attend 'delay', puis tombe tout droit
        public static void SpawnMeteor(float x, float groundY, float delay, float speed, float damage, float reflectDamage, HellGuardian owner, Color color)
        {
            BossProjectile p = SpawnOrb(new Vector3(x, groundY + 12f, 0f), Vector3.down, speed, damage, reflectDamage, owner, color, 0.95f);
            p.isMeteor = true;
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
        public static BossProjectile SpawnJudgment(Vector3 pos, Vector3 dir, float speed, float damage, float reflectDamage, int rallies, HellGuardian owner, Color color)
        {
            BossProjectile p = SpawnOrb(pos, dir, speed, damage, reflectDamage, owner, color, 1.3f);
            p.rallies = rallies;
            p.life = 10f;
            return p;
        }

        // Fente : croissant vertical de 'height' de haut qui file au ras du sol
        public static void SpawnSlash(Vector3 groundPos, float dir, float speed, float damage, HellGuardian owner, Color color, float height)
        {
            BossProjectile p = Create("BossSlash", groundPos, Kind.Slash, damage, owner, color);
            p.velocity = new Vector3(Mathf.Sign(dir) * speed, 0f, 0f);
            p.groundY = groundPos.y;
            p.height = height;
            p.life = 1.8f;
            // Arc de lames lumineuses, bombé vers l'avant
            Mesh seg = LowPoly.Box(1f);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f);                       // 0 bas, 1 haut
                float a = (u - 0.5f) * 140f;                  // -70..70°
                float r = height * 0.55f;
                GameObject g = p.Part(seg);
                g.transform.localPosition = new Vector3(Mathf.Sign(dir) * (Mathf.Cos(a * Mathf.Deg2Rad) * r - r * 0.6f), height * 0.5f + Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f);
                g.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sign(dir) * a);
                float thick = 0.12f + 0.16f * Mathf.Sin(u * Mathf.PI);
                g.transform.localScale = new Vector3(thick, height * 0.24f, 0.3f);
            }
            TrailRenderer trail = p.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = height * 0.8f;
            trail.endWidth = 0f;
            trail.material = MaterialFactory.Unlit(Color.white);
            trail.startColor = new Color(color.r, color.g, color.b, 0.5f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        // Pilier de feu : marque au sol pendant 'delay', puis colonne de 'height' qui jaillit
        public static void SpawnPillar(float x, float groundY, float delay, float damage, HellGuardian owner, Color color, float height)
        {
            BossProjectile p = Create("BossPillar", new Vector3(x, groundY, 0f), Kind.Pillar, damage, owner, color);
            p.groundY = groundY;
            p.height = height;
            p.delay = delay;
            p.life = 0.75f;
            GameObject col = p.Part(LowPoly.Prism(6, 0.6f, 0.35f, 1f, 0.4f));
            col.transform.localScale = new Vector3(1f, 0f, 1f);
            GameObject m = new GameObject("PillarMarker");
            m.transform.position = new Vector3(x, groundY + 0.03f, 0f);
            m.AddComponent<MeshFilter>().sharedMesh = LowPoly.Prism(6, 0.75f, 0.75f, 0.04f);
            m.AddComponent<MeshRenderer>().sharedMaterial = p.mat;
            p.marker = m.transform;
        }

        public static void SpawnShockwave(Vector3 groundPos, float dir, float speed, float damage, HellGuardian owner, Color color, float size = 1f)
        {
            BossProjectile p = Create("BossShockwave", groundPos, Kind.Shockwave, damage, owner, color);
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

        private static BossProjectile Create(string name, Vector3 pos, Kind kind, float damage, HellGuardian owner, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.position = pos;
            BossProjectile p = go.AddComponent<BossProjectile>();
            p.kind = kind;
            p.damage = damage;
            p.owner = owner;
            p.target = FindFirstObjectByType<PlayerController>();

            p.baseColor = color;
            p.mat = MaterialFactory.Lit(color);
            p.mat.EnableKeyword("_EMISSION");
            p.mat.SetColor("_EmissionColor", color * 2.5f);

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
                if (marker != null) marker.localScale = Vector3.one * (0.6f + Mathf.PingPong(Time.time * 6f, 0.4f));
                if (delay <= 0f && kind == Kind.Pillar && marker != null) Destroy(marker.gameObject);
                return;
            }
            age += dt;
            if (kind == Kind.Pillar)
            {
                UpdatePillar(dt);
                return;
            }
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
                if (reflected) UpdateReflected();
                else CheckPlayer(0.35f + visual.GetChild(0).localScale.x * 0.5f);

                // Météore arrivé au sol sans toucher
                if (isMeteor && !reflected && transform.position.y <= groundY)
                {
                    Destroy(gameObject);
                }
            }
            else if (kind == Kind.Slash)
            {
                // Le croissant scintille et s'étire en avançant
                float k = 1f + Mathf.Sin(Time.time * 40f) * 0.06f;
                visual.localScale = new Vector3(1f + age * 0.4f, k, 1f);
                CheckPlayer(0f);
            }
            else
            {
                // Les pics ondulent en avançant
                float k = 1f + Mathf.Sin(Time.time * 30f) * 0.15f;
                visual.localScale = new Vector3(1f, k, 1f);
                CheckPlayer(0f);
            }
        }

        // Colonne : jaillit en 0,08 s, reste, puis retombe. Frappe une seule fois.
        private void UpdatePillar(float dt)
        {
            float grow = Mathf.Clamp01(age / 0.08f) * Mathf.Clamp01((0.75f - age) / 0.2f);
            Transform col = visual.GetChild(0);
            col.localScale = new Vector3(1f + Mathf.Sin(Time.time * 35f) * 0.08f, height * grow, 1f + Mathf.Cos(Time.time * 31f) * 0.08f);
            if (!spent && age < 0.4f && target != null)
            {
                Vector3 pp = target.transform.position;
                if (Mathf.Abs(pp.x - transform.position.x) < 0.75f && pp.y - groundY < height * grow)
                {
                    spent = true;
                    target.TakeDamage(damage, transform, new Vector3(transform.position.x, pp.y, 0f));
                }
            }
            life -= dt;
            if (life <= 0f) Destroy(gameObject);
        }

        private void CheckPlayer(float radius)
        {
            if (target == null) return;
            Vector3 pp = target.transform.position;
            bool touch;
            if (kind == Kind.Orb)
            {
                Vector2 d = new Vector2(pp.x - transform.position.x, pp.y - transform.position.y);
                touch = d.magnitude < radius;
            }
            else
            {
                // Onde / fente au sol : le joueur doit être dessus et sous sa hauteur
                float w = kind == Kind.Slash ? 0.6f : 0.45f;
                touch = Mathf.Abs(pp.x - transform.position.x) < w && pp.y - groundY < height;
            }
            if (!touch) return;

            Vector3 hitPoint = Vector3.Lerp(transform.position, pp, 0.5f);
            if (target.TakeDamage(damage, transform, hitPoint) || kind != Kind.Orb)
            {
                // Touché, ou onde parée : le projectile disparaît
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
                    SetColor(baseColor, 2.5f);
                    if (target != null)
                    {
                        Vector3 d = target.transform.position - transform.position;
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
