using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Respiration du chat : un cycle régulier lu par les visuels (CatSoul gonfle, CatBody monte et descend),
    // et une buée froide à chaque expiration (l'enfer est glacé) pour qu'on voie le rythme.
    // Parer au début de chaque inspiration bloque le souffle (BreathRhythm) : le chat se fige, la buée
    // disparaît, il suffoque de plus en plus (voile et cœur de DangerFeedback). Après 'suffocateTime'
    // secondes d'apnée, il meurt étouffé.
    public class CatBreath : MonoBehaviour
    {
        [SerializeField] private float period = 2.86f;           // durée d'un souffle (s)
        [SerializeField] private float window = 0.3f;            // ± autour du début de l'inspiration
        [SerializeField] private float suffocateTime = 30f;      // apnée mortelle (s)
        [SerializeField] private Color vaporColor = new Color(0.85f, 0.92f, 1f, 0.45f);

        private BreathRhythm rhythm;
        private PlayerBody body;
        private PlayerController player;
        private CatSoul soul;
        private float shown = -1f;

        // -1 poumons vides → 1 pleins (lissé)
        public float Value => shown;
        // 0 → 1 : avancée de l'apnée vers l'étouffement
        public float Suffocation => rhythm != null ? rhythm.Suffocation : 0f;
        public bool Holding => rhythm != null && rhythm.Holding;

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            player = GetComponent<PlayerController>();
            soul = GetComponent<CatSoul>();
            rhythm = new BreathRhythm(period, window, suffocateTime, Time.time);
        }

        private void Start()
        {
            if (player != null) player.ParryPressed += OnParryPressed;
        }

        private void OnDestroy()
        {
            if (player != null) player.ParryPressed -= OnParryPressed;
        }

        // Appelé à chaque appui sur la parade
        public void OnParryPressed()
        {
            if (rhythm != null) rhythm.Parry(Time.time);
        }

        private void Update()
        {
            if (rhythm == null) return;
            if (body != null && body.IsDead) return;
            // Menus, mise en scène : il respire tranquillement, pas d'apnée en cours
            bool playing = GameFlow.IsPlaying;
            if (!playing) rhythm.CancelApnea();
            rhythm.Tick(Time.time, out bool exhaled);
            if (exhaled) Vapor();
            if (playing && rhythm.Suffocated)
            {
                Suffocate();
                return;
            }

            // Souffle bloqué : poumons vides, avec des hoquets de plus en plus forts
            float target = rhythm.Blocked
                ? -1f + Suffocation * 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 9f))
                : -Mathf.Cos(rhythm.Phase * Mathf.PI * 2f);
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 6f);
        }

        // Buée froide devant la tête (l'âme fait la tête, même avec un corps)
        private void Vapor()
        {
            if (soul == null) soul = GetComponent<CatSoul>();
            float facing = player != null ? player.Facing : 1f;
            Vector3 head = soul != null ? soul.HeadPosition : transform.position;
            Vector3 dir = new Vector3(facing, 0.35f, 0f).normalized;
            ParticleBurst.Spawn("BreathVapor", head + dir * 0.3f,
                new ParticleSystem.MinMaxGradient(vaporColor, new Color(vaporColor.r, vaporColor.g, vaporColor.b, vaporColor.a * 0.4f)), 8,
                lifetime: new Vector2(0.6f, 1.1f),
                speed: new Vector2(0.4f, 0.9f),
                size: new Vector2(0.08f, 0.2f),
                gravity: -0.05f,
                radius: 0.04f,
                duration: 0.3f,
                direction: dir,
                coneAngle: 20f);
        }

        private void Suffocate()
        {
            Debug.Log("Le chat a paré sa propre respiration : étouffé.");
            if (body != null) body.Kill();
        }
    }
}
