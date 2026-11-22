using ParryEverything.Boss;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Boule d'âme tirée par une statue cracheuse. Touche le joueur ; parée, elle repart en sens inverse, plus vite,
    // et brise la statue qui l'a tirée si elle la percute (Spitter.Shatter).
    public class ParryShot : MonoBehaviour
    {
        private static readonly Color Glow = new Color(0.6f, 0.9f, 1f);
        private static readonly Color Reflected = new Color(1f, 0.85f, 0.4f);

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Vector3 velocity;
        private float damage;
        private float life = 6f;
        private bool reflected;
        private float groundY;
        private Material mat;

        public static ParryShot Fire(Vector3 pos, Vector3 velocity, float damage, float groundY)
        {
            GameObject go = new GameObject("ParryShot");
            go.transform.position = pos;
            ParryShot s = go.AddComponent<ParryShot>();
            s.velocity = velocity;
            s.damage = damage;
            s.groundY = groundY;
            s.mat = s.generated.Track(MaterialFactory.Unlit(Glow));
            LevelPlayer.Part(s.generated, go.transform, s.generated.Track(LowPoly.Icosahedron(0.1f, 3)), s.mat, Vector3.zero, Vector3.one * 0.45f);
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.startWidth = 0.3f;
            trail.endWidth = 0f;
            trail.material = s.generated.Track(MaterialFactory.Unlit(Color.white));
            trail.startColor = Glow;
            trail.endColor = new Color(Glow.r, Glow.g, Glow.b, 0f);
            return s;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 prev = transform.position;
            transform.position += velocity * dt;

            PlayerController p = LevelPlayer.Get();
            if (!reflected && LevelPlayer.Active(p) && ProjectileHits.TouchesPlayer(p, prev, transform.position, 0.35f))
            {
                if (LevelPlayer.HitParried(p, damage, transform, transform.position))
                {
                    reflected = true;
                    velocity = -velocity * 1.4f;
                    mat.color = Reflected;
                    GetComponent<TrailRenderer>().startColor = Reflected;
                    return;
                }
                Burst();
                return;
            }

            if (ProjectileHits.HitsWorld(prev, transform.position, 0.15f, groundY, out Vector3 at, out Collider hit))
            {
                if (reflected && hit != null)
                {
                    Spitter spitter = hit.GetComponentInParent<Spitter>();
                    if (spitter != null) spitter.Shatter();
                }
                transform.position = at;
                Burst();
            }
        }

        private void Burst()
        {
            Color c = reflected ? Reflected : Glow;
            ParticleBurst.Spawn("ParryShotBurst", transform.position, new ParticleSystem.MinMaxGradient(c, Color.white), 16,
                lifetime: new Vector2(0.2f, 0.5f), speed: new Vector2(1.5f, 5f), size: new Vector2(0.05f, 0.14f),
                gravity: 0.5f, radius: 0.15f);
            Destroy(gameObject);
        }

        private void OnDestroy() => generated.DestroyAll();
    }
}
