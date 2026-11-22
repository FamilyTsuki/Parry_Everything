using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Parer ses pas. En courant, parer pile quand un pied touche le sol : à partir du 3e pas paré d'affilée,
    // chaque pas paré pousse le chat un peu plus vite (le sol, paré, le renvoie). Un pas manqué, une parade
    // hors rythme ou l'arrêt de la course : l'élan retombe peu à peu.
    public class StrideParry : MonoBehaviour
    {
        [SerializeField] private float window = 0.1f;            // ± autour de la pose du pied
        [SerializeField] private int stepsToBoost = 3;            // pas parés avant le premier élan
        [SerializeField] private float boostPerStep = 0.06f;      // vitesse en plus à chaque pas paré (× course)
        [SerializeField] private float maxBoost = 0.6f;
        [SerializeField] private float decay = 0.8f;              // perte d'élan par seconde sans rythme

        private RhythmChain chain;
        private PlayerController player;
        private CatBody catBody;
        private float bonus;
        private bool chainHolds;

        // Vitesse de course en plus (0,3 = +30 %)
        public float SpeedBonus => bonus;

        private void Awake()
        {
            chain = new RhythmChain(window);
            player = GetComponent<PlayerController>();
        }

        private void Start()
        {
            catBody = GetComponent<CatBody>();
            if (catBody != null) catBody.Footstep += OnFootstep;
            if (player != null) player.ParryPressed += OnParry;
            chain.Resolved = OnResolved;
        }

        private void OnDestroy()
        {
            if (catBody != null) catBody.Footstep -= OnFootstep;
            if (player != null) player.ParryPressed -= OnParry;
        }

        private void OnFootstep()
        {
            if (player != null && player.IsRunning) chain.Event(Time.time);
        }

        private void OnParry()
        {
            if (player != null && player.IsRunning) chain.Parry(Time.time);
        }

        private void OnResolved(bool parried)
        {
            chainHolds = parried;
            if (!parried) return;
            Vector3 feet = transform.position + Vector3.down * 0.45f;
            bool boosting = chain.Count >= stepsToBoost;
            if (boosting) bonus = Mathf.Min(maxBoost, bonus + boostPerStep);
            // Pied paré : poussière et étincelles (bleues dès que ça pousse)
            Color spark = boosting ? new Color(0.5f, 0.85f, 1f) : new Color(0.9f, 0.85f, 0.7f);
            ParticleBurst.Spawn("StrideParry", feet, new ParticleSystem.MinMaxGradient(spark, Color.white), boosting ? 14 : 8,
                lifetime: new Vector2(0.15f, 0.35f),
                speed: new Vector2(1.5f, 3.5f + bonus * 4f),
                size: new Vector2(0.04f, 0.1f),
                gravity: 1f,
                radius: 0.1f,
                direction: new Vector3(-(player != null ? player.Facing : 1f), 0.6f, 0f),
                coneAngle: 35f);
        }

        private void Update()
        {
            chain.Tick(Time.time);
            bool running = player != null && player.IsRunning && GameFlow.IsPlaying;
            if (!running) chain.Reset();
            if (!running || chain.Count == 0) chainHolds = false;
            if (!chainHolds) bonus = Mathf.MoveTowards(bonus, 0f, decay * Time.deltaTime);
        }
    }
}
