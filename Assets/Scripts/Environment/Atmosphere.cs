using ParryEverything.Boss;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Environment
{
    // Ambiance de la zone : brouillard, lumière, lune et effet d'écran.
    //  - Calme : bleu nuit froid, lumière qui "respire" lentement et vacille de temps en temps.
    //  - Combat de boss : le rouge sombre gagne du terrain en quelques secondes, la lumière faiblit
    //    et vacille plus souvent, la lune pulse.
    // Les réglages "calme" sont aussi ceux de la scène (Lighting) : rien ne change hors Play.
    public class Atmosphere : MonoBehaviour
    {
        [Header("Références")]
        [SerializeField] private Light sun;
        [SerializeField] private Renderer moon;
        [SerializeField] private Renderer sky;          // dégradé du fond, teinté de la couleur du brouillard
        [SerializeField] private ScreenMood screenMood;

        [Header("Calme")]
        [SerializeField] private Color fogColor = new Color(0.035f, 0.045f, 0.075f);
        [SerializeField] private float fogStart = 14f;
        [SerializeField] private float fogEnd = 115f;
        [SerializeField] private Color lightColor = new Color(0.62f, 0.72f, 0.9f);
        [SerializeField] private float lightIntensity = 0.55f;
        [SerializeField] private Color moonColor = new Color(0.42f, 0.07f, 0.07f);

        [Header("Combat de boss")]
        [SerializeField] private Color bossFogColor = new Color(0.07f, 0.025f, 0.035f);
        [SerializeField] private float bossFogEnd = 85f;     // le brouillard se resserre
        [SerializeField] private Color bossLightColor = new Color(0.75f, 0.55f, 0.6f);
        [SerializeField] private float bossLightIntensity = 0.42f;
        [SerializeField] private Color bossMoonColor = new Color(0.75f, 0.06f, 0.05f);
        [SerializeField] private float transitionTime = 6f;   // secondes pour passer d'une ambiance à l'autre

        [Header("Lumière instable")]
        [Range(0f, 0.5f)] [SerializeField] private float breathAmount = 0.1f;   // variation lente d'intensité
        [SerializeField] private float breathSpeed = 0.12f;
        [SerializeField] private Vector2 flickerInterval = new Vector2(14f, 40f);
        [SerializeField] private Vector2 bossFlickerInterval = new Vector2(5f, 12f);
        [Range(0f, 1f)] [SerializeField] private float flickerDepth = 0.55f;    // baisse d'intensité pendant un vacillement

        private HellGuardian boss;
        private float dread;
        private float flickerTimer;
        private float flickerLeft;
        private float flickerLength;
        private MaterialPropertyBlock block;
        private float flash;              // lueur rouge lointaine en cours (0..1)
        private float flashDecay = 1f;
        private Color flashColor = new Color(0.32f, 0.07f, 0.05f);

        // Ambiance active de la scène (null s'il n'y en a pas)
        public static Atmosphere Current { get; private set; }

        public float Dread => dread;

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        // Lueur rouge qui traverse le brouillard (feu lointain, éclair de chaleur) :
        // intensité 0..1, durée en secondes
        public void Flash(float intensity, float duration)
        {
            flash = Mathf.Max(flash, Mathf.Clamp01(intensity));
            flashDecay = 1f / Mathf.Max(0.05f, duration);
        }

        private void Start()
        {
            block = new MaterialPropertyBlock();
            flickerTimer = Random.Range(flickerInterval.x, flickerInterval.y);
            Apply(1f);
        }

        private void Update()
        {
            if (boss == null) boss = FindFirstObjectByType<HellGuardian>();
            bool fight = boss != null && boss.IsActive && !boss.IsDead;
            dread = Mathf.MoveTowards(dread, fight ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, transitionTime));

            // Vacillement : brève chute de lumière, à intervalles irréguliers
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                Vector2 range = Vector2.Lerp(flickerInterval, bossFlickerInterval, dread);
                flickerTimer = Random.Range(range.x, range.y);
                flickerLength = Random.Range(0.12f, 0.5f);
                flickerLeft = flickerLength;
            }

            float flicker = 1f;
            if (flickerLeft > 0f)
            {
                flickerLeft -= Time.deltaTime;
                // Deux ou trois creux rapides pendant le vacillement, pas une simple coupure
                float p = 1f - flickerLeft / flickerLength;
                float stutter = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3f));
                flicker = 1f - flickerDepth * stutter;
            }

            // Respiration : bruit lent, jamais régulier
            float breath = 1f + (Mathf.PerlinNoise(Time.time * breathSpeed, 0.37f) - 0.5f) * 2f * breathAmount;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * flashDecay);
            Apply(breath * flicker);
        }

        private void Apply(float lightFactor)
        {
            Color fog = Color.Lerp(fogColor, bossFogColor, dread);
            // La lueur vacille un peu en s'éteignant
            float glow = flash * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 3.1f));
            fog = Color.Lerp(fog, flashColor, glow * 0.6f);
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, bossFogEnd, dread);

            Camera cam = Camera.main;
            if (cam != null) cam.backgroundColor = fog;

            if (sun != null)
            {
                sun.color = Color.Lerp(lightColor, bossLightColor, dread);
                sun.intensity = Mathf.Lerp(lightIntensity, bossLightIntensity, dread) * lightFactor;
            }

            if (block == null) return;
            if (sky != null) SetColor(sky, fog);

            if (moon != null)
            {
                // Pendant le combat, la lune pulse lentement comme un cœur fatigué
                float pulse = 1f + Mathf.Sin(Time.time * 1.3f) * 0.12f * dread;
                Color c = Color.Lerp(moonColor, bossMoonColor, dread) * pulse * Mathf.Lerp(1f, lightFactor, 0.5f);
                c.a = 1f;
                SetColor(moon, c);
            }

            if (screenMood != null) screenMood.Dread = dread;
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            target.SetPropertyBlock(block);
        }
    }
}
