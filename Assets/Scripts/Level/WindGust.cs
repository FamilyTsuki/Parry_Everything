using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Rafales glacées dans une zone (BoxCollider en déclencheur, rien d'autre) : calme, puis la neige se lève
    // (signal), puis la rafale pousse le chat vers 'push'. Parer au moment où elle se lève l'annule pour lui.
    [RequireComponent(typeof(BoxCollider))]
    public class WindGust : MonoBehaviour
    {
        [SerializeField] private Vector2 push = new Vector2(-4.5f, 0f);  // déplacement imposé pendant la rafale (m/s)
        [SerializeField] private float calmTime = 2.4f;
        [SerializeField] private float warnTime = 0.6f;
        [SerializeField] private float gustTime = 1.3f;
        [SerializeField] private float parryWindow = 0.3f;              // autour du début de la rafale

        private BoxCollider zone;
        private PlayerController player;
        private float t;
        private bool cancelled;
        private float lastParry = -10f;

        private float GustStart => calmTime + warnTime;

        private void Awake()
        {
            zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
        }

        private void OnEnable()
        {
            player = LevelPlayer.Get();
            if (player != null) player.ParryPressed += OnParry;
        }

        private void OnDisable()
        {
            if (player != null) player.ParryPressed -= OnParry;
        }

        private void OnParry() => lastParry = Time.time;

        private bool Inside(PlayerController p) => LevelPlayer.Active(p) && zone.bounds.Contains(new Vector3(p.HitCenter.x, p.HitCenter.y, zone.bounds.center.z));

        private void Update()
        {
            float prev = t;
            t = Mathf.Repeat(t + Time.deltaTime, calmTime + warnTime + gustTime);
            Bounds b = zone.bounds;

            // Signal : la neige se lève dans le sens du vent
            if (prev < calmTime && t >= calmTime)
            {
                ParticleBurst.Spawn("GustWarn", b.center, new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.92f, 1f, 0.7f), Color.white), 30,
                    lifetime: new Vector2(0.5f, 1f), speed: new Vector2(1f, 3f), size: new Vector2(0.04f, 0.1f), gravity: -0.1f,
                    radius: Mathf.Min(b.extents.x, 3f), direction: new Vector3(push.x, push.y, 0f), coneAngle: 15f);
            }
            // Début de la rafale : parée juste avant ou juste après ?
            if (prev < GustStart && t >= GustStart) cancelled = Time.time - lastParry <= parryWindow && Inside(player);
            if (t >= GustStart && t < GustStart + parryWindow && !cancelled && Time.time - lastParry < 0.02f && Inside(player))
            {
                cancelled = true;
            }
            if (t >= GustStart && t < GustStart + 0.05f && cancelled)
            {
                ParticleBurst.Spawn("GustParried", player.HitCenter, new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.9f, 1f), Color.white), 20,
                    lifetime: new Vector2(0.2f, 0.5f), speed: new Vector2(2f, 5f), size: new Vector2(0.04f, 0.1f), gravity: 0f, radius: 0.3f);
            }

            if (t >= GustStart && !cancelled)
            {
                // Rafale : flocons et poussée
                if (Random.value < 0.25f)
                {
                    Vector3 at = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), -0.5f);
                    ParticleBurst.Spawn("Gust", at, new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.92f, 1f, 0.6f), Color.white), 3,
                        lifetime: new Vector2(0.3f, 0.6f), speed: new Vector2(6f, 10f), size: new Vector2(0.03f, 0.07f), gravity: 0f,
                        radius: 0.1f, direction: new Vector3(push.x, push.y, 0f), coneAngle: 5f);
                }
            }
        }

        // La poussée déplace le corps directement : le contrôleur du joueur réécrit sa vitesse à chaque pas
        private void FixedUpdate()
        {
            if (t < GustStart || cancelled || !Inside(player)) return;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            rb.MovePosition(rb.position + new Vector3(push.x, push.y, 0f) * Time.fixedDeltaTime);
        }

        private void OnDrawGizmos()
        {
            BoxCollider b = GetComponent<BoxCollider>();
            if (b == null) return;
            Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
        }
    }
}
