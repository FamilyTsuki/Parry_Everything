using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Parer son cœur. Quand le chat est en danger, son cœur bat fort (DangerFeedback). Parer pile sur chaque
    // battement le ralentit, et le monde ralentit avec lui : un avant-goût de la parade du temps.
    // Mais le cœur ne doit pas s'arrêter : après 'arrestTime' secondes de battements parés d'affilée, arrêt
    // cardiaque, le chat meurt. Un battement laissé passer, ou une parade hors rythme, le relance.
    // Tant que le cœur est ralenti, l'âme ne se régénère plus.
    public class CatHeart : MonoBehaviour
    {
        [SerializeField] private float window = 0.12f;          // ± autour d'un battement (temps réel)
        [SerializeField] private int chainToSlow = 2;            // battements parés avant que ça ralentisse
        [SerializeField] private float arrestTime = 20f;         // battements parés d'affilée avant l'arrêt
        [Range(0.1f, 1f)] [SerializeField] private float slowestHeart = 0.35f;   // cœur au plus lent (× bpm)
        [Range(0.1f, 1f)] [SerializeField] private float slowestWorld = 0.55f;   // monde au plus lent (× temps)

        private RhythmChain chain;
        private DangerFeedback danger;
        private PlayerController player;
        private PlayerBody body;
        private float lastBeat = -10f;
        private float slow;                                       // 0 → 1 : ralentissement affiché

        // 0 → 1 : avancée vers l'arrêt cardiaque
        public float Arrest => chain != null && chain.Count >= chainToSlow ? Mathf.Clamp01(chain.Duration(Time.unscaledTime) / arrestTime) : 0f;

        private void Awake()
        {
            chain = new RhythmChain(window);
            danger = GetComponent<DangerFeedback>();
            player = GetComponent<PlayerController>();
            body = GetComponent<PlayerBody>();
        }

        private void Start()
        {
            if (danger == null) danger = GetComponent<DangerFeedback>();
            if (danger != null) danger.Beat += OnBeat;
            if (player != null) player.ParryPressed += OnParry;
            chain.Resolved = OnResolved;
        }

        private void OnDestroy()
        {
            if (danger != null) danger.Beat -= OnBeat;
            if (player != null) player.ParryPressed -= OnParry;
            Restore();
        }

        private void OnDisable()
        {
            Restore();
        }

        private void OnBeat()
        {
            lastBeat = Time.unscaledTime;
            chain.Event(lastBeat);
        }

        private void OnParry()
        {
            chain.Parry(Time.unscaledTime);
        }

        // Battement paré : petite étincelle rouge sur la poitrine
        private void OnResolved(bool parried)
        {
            if (!parried) return;
            ParticleBurst.Spawn("HeartParry", transform.position, new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.15f, 0.2f), Color.white), 10,
                lifetime: new Vector2(0.15f, 0.35f),
                speed: new Vector2(1.5f, 3.5f),
                size: new Vector2(0.04f, 0.1f),
                gravity: 0f,
                radius: 0.15f);
        }

        private void Update()
        {
            if (body != null && body.IsDead)
            {
                Restore();
                return;
            }
            float now = Time.unscaledTime;
            chain.Tick(now);
            // Plus de battements (danger passé, menus) : la chaîne s'arrête
            if (now - lastBeat > 2.5f || !GameFlow.IsPlaying) chain.Reset();

            float goal = chain.Count >= chainToSlow ? Mathf.Clamp01(chain.Duration(now) / arrestTime) : 0f;
            // Cœur ralenti : l'âme ne se régénère plus (sinon le danger, et donc les battements, s'arrêteraient)
            if (body != null) body.RegenBlocked = chain.Count >= chainToSlow;
            slow = Mathf.MoveTowards(slow, goal, Time.unscaledDeltaTime * (goal > slow ? 1f : 2f));
            float eased = slow * (2f - slow);
            if (danger != null) danger.RateScale = Mathf.Lerp(1f, slowestHeart, eased);
            TimeControl.SetLayerScale("heart", Mathf.Lerp(1f, slowestWorld, eased));

            if (goal >= 1f && GameFlow.IsPlaying)
            {
                Debug.Log("Le chat a paré son propre cœur : arrêt cardiaque.");
                Restore();
                if (body != null) body.Kill();
            }
        }

        private void Restore()
        {
            if (chain != null) chain.Reset();
            slow = 0f;
            if (danger != null) danger.RateScale = 1f;
            if (body != null) body.RegenBlocked = false;
            TimeControl.SetLayerScale("heart", 1f);
        }
    }
}
