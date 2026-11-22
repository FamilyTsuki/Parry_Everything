using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Ancre d'âme (losange qui flotte) : parer à moins de 'reach' d'une ancre projette le chat vers elle, un peu
    // au-dessus. On traverse un gouffre d'ancre en ancre. L'ancre s'allume quand le chat est à portée.
    [ExecuteAlways]
    public class ParryAnchor : MonoBehaviour
    {
        [SerializeField] private float reach = 4f;
        [SerializeField] private float speed = 13f;
        [SerializeField] private float lift = 2.5f;          // vitesse vers le haut en plus

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Material mat;
        private Transform gem;
        private PlayerController player;
        private float cooldown;

        private static readonly Color Idle = new Color(0.45f, 0.35f, 0.65f);
        private static readonly Color Ready = new Color(0.8f, 0.6f, 1f);

        private void OnEnable()
        {
            generated.DestroyAll();
            mat = generated.Track(MaterialFactory.Unlit(Idle));
            gem = LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Prism(4, 0.35f, 0f, 0.45f)), mat, Vector3.zero, Vector3.one);
            LevelPlayer.Part(generated, gem, generated.Track(LowPoly.Prism(4, 0.35f, 0f, 0.45f)), mat, Vector3.zero, Vector3.one, new Vector3(180f, 0f, 0f));
            if (Application.isPlaying)
            {
                player = LevelPlayer.Get();
                if (player != null) player.ParryPressed += OnParry;
            }
        }

        private void OnDisable()
        {
            if (player != null) player.ParryPressed -= OnParry;
            generated.DestroyAll();
        }

        private bool InReach(PlayerController p) => LevelPlayer.Active(p) && Vector3.Distance(p.HitCenter, transform.position) < reach;

        private void OnParry()
        {
            if (cooldown > 0f || !InReach(player)) return;
            Vector3 d = transform.position - player.HitCenter;
            d.z = 0f;
            // Projeté vers l'ancre (on la dépasse un peu, en hauteur, pour viser la suivante)
            player.Knockback(d.normalized * speed + Vector3.up * lift);
            cooldown = 0.4f;
            ParticleBurst.Spawn("AnchorPull", transform.position, new ParticleSystem.MinMaxGradient(Ready, Color.white), 18,
                lifetime: new Vector2(0.2f, 0.5f), speed: new Vector2(1f, 4f), size: new Vector2(0.04f, 0.1f), gravity: 0f, radius: 0.2f);
        }

        private void Update()
        {
            if (gem == null) return;
            gem.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            gem.localPosition = Vector3.up * Mathf.Sin(Time.time * 2f + transform.position.x) * 0.12f;
            if (!Application.isPlaying) return;
            cooldown -= Time.deltaTime;
            mat.color = InReach(player) ? Color.Lerp(Ready, Color.white, Mathf.PingPong(Time.time * 4f, 0.4f)) : Idle;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.7f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
