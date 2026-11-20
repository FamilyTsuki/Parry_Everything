using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Vie basse ressentie, sans barre : la vignette rouge se resserre et bat (ScreenMood.Danger),
    // un cœur sourd bat de plus en plus vite. Ce qui compte : l'âme quand elle est à nu,
    // sinon le ventre (la chair qui l'enferme : éclaté, tout tombe).
    [RequireComponent(typeof(PlayerBody))]
    public class DangerFeedback : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float startRatio = 0.5f;   // le danger commence sous cette part de vie
        [Range(0f, 1f)] [SerializeField] private float fullRatio = 0.15f;   // danger maximal sous cette part
        [SerializeField] private float response = 3f;                       // lissage (par seconde)

        [Header("Cœur")]
        [SerializeField] private float heartMinThreshold = 0.25f;           // pas de cœur sous ce danger
        [SerializeField] private float heartRateCalm = 70f;                 // battements par minute au début du danger
        [SerializeField] private float heartRatePanic = 150f;
        [Range(0f, 1f)] [SerializeField] private float heartVolume = 0.5f;

        private PlayerBody body;
        private ScreenMood mood;
        private AudioSource source;
        private AudioClip thump;
        private float danger;
        private float nextBeat;

        public float Danger => danger;

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            thump = MakeThump();
        }

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam != null) mood = cam.GetComponent<ScreenMood>();
        }

        private void OnDisable()
        {
            if (mood != null) mood.Danger = 0f;
        }

        private void Update()
        {
            float target = GameFlow.IsPlaying && !body.IsDead ? DangerFromLife(LifeRatio()) : 0f;
            danger = Mathf.MoveTowards(danger, target, response * Time.unscaledDeltaTime);
            if (mood != null) mood.Danger = danger;

            if (danger < heartMinThreshold || Time.timeScale <= 0f)
            {
                nextBeat = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime >= nextBeat)
            {
                float bpm = Mathf.Lerp(heartRateCalm, heartRatePanic, danger);
                // Battement double (lub-dub) : le second, plus faible, juste après
                source.PlayOneShot(thump, heartVolume * danger * GameSettings.SfxVolume);
                StartCoroutine(SecondBeat(heartVolume * danger * 0.6f));
                nextBeat = Time.unscaledTime + 60f / bpm;
            }
        }

        private System.Collections.IEnumerator SecondBeat(float volume)
        {
            yield return new WaitForSecondsRealtime(0.16f);
            source.PlayOneShot(thump, volume * GameSettings.SfxVolume);
        }

        // Part de vie de ce qui protège le chat
        private float LifeRatio()
        {
            Limb flesh = body.FleshLimb;
            return flesh != null ? flesh.LifeRatio : body.SoulRatio;
        }

        // 0 au-dessus de startRatio, 1 sous fullRatio, courbe douce entre les deux
        public static float DangerFromLife(float ratio, float start = 0.5f, float full = 0.15f)
        {
            if (start <= full) return ratio <= full ? 1f : 0f;
            float t = Mathf.Clamp01((start - ratio) / (start - full));
            return t * t * (3f - 2f * t);
        }

        private float DangerFromLife(float ratio) => DangerFromLife(ratio, startRatio, fullRatio);

        // Coup sourd : sinus grave qui descend, très court
        private static AudioClip MakeThump()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int n = Mathf.RoundToInt(0.18f * rate);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = Mathf.Lerp(62f, 38f, t / 0.18f);
                phase += 2.0 * Mathf.PI * f / rate;
                float env = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-22f * t);
                data[i] = env * (float)System.Math.Sin(phase);
            }
            AudioClip clip = AudioClip.Create("HeartThump", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
