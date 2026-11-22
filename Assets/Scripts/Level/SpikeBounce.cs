using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Rangée de pics. Les toucher blesse ; parer au contact fait rebondir le chat très haut
    // (on atteint ainsi ce qui est hors de portée d'un saut). Origine = milieu de la rangée, au sol.
    [ExecuteAlways]
    public class SpikeBounce : MonoBehaviour
    {
        [SerializeField] private float width = 3f;
        [SerializeField] private float height = 0.7f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float bounceSpeed = 15f;
        [SerializeField] private Material material;

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private float cooldown;

        private void OnEnable() => Build();
        private void OnDisable() => generated.DestroyAll();

        private void Build()
        {
            generated.DestroyAll();
            Material m = LevelPlayer.Or(material, new Color(0.55f, 0.58f, 0.66f), generated);
            int n = Mathf.Max(1, Mathf.RoundToInt(width / 0.5f));
            Mesh spike = generated.Track(LowPoly.Prism(4, 0.22f, 0f, height));
            for (int i = 0; i < n; i++)
            {
                float x = -width / 2f + (i + 0.5f) * width / n;
                LevelPlayer.Part(generated, transform, spike, m, new Vector3(x, 0f, 0f), Vector3.one, new Vector3(0f, 45f, 0f));
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            cooldown -= Time.deltaTime;
            PlayerController p = LevelPlayer.Get();
            if (!LevelPlayer.Active(p) || cooldown > 0f) return;
            Bounds b = p.HitBounds;
            Vector3 o = transform.position;
            bool touch = b.max.x > o.x - width / 2f && b.min.x < o.x + width / 2f && b.min.y < o.y + height && b.max.y > o.y;
            if (!touch) return;
            cooldown = 0.5f;
            Vector3 point = new Vector3(Mathf.Clamp(p.transform.position.x, o.x - width / 2f, o.x + width / 2f), o.y + height, 0f);
            if (LevelPlayer.HitParried(p, damage, transform, point))
            {
                // Paré : le sol renvoie le chat vers le haut
                p.Knockback(new Vector3(p.Facing * 2f, bounceSpeed, 0f));
                ParticleBurst.Spawn("SpikeBounce", point, new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.85f, 1f), Color.white), 20,
                    lifetime: new Vector2(0.2f, 0.45f), speed: new Vector2(2f, 6f), size: new Vector2(0.05f, 0.12f),
                    gravity: 1f, radius: 0.2f, direction: Vector3.up, coneAngle: 40f);
            }
            else
            {
                p.Knockback(new Vector3(-p.Facing * 4f, 6f, 0f));
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * height / 2f, new Vector3(width, height, 1f));
        }
    }
}
