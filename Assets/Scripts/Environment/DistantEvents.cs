using System.Collections;
using ParryEverything.Audio;
using UnityEngine;

namespace ParryEverything.Environment
{
    // Vie lointaine de l'enfer éteint : des choses arrivent partout dans la zone, que le joueur
    // soit là ou non. Éboulements sur les montagnes, lueurs rouges dans le brouillard suivies
    // d'un grondement, foyers mal éteints qui se ravivent, âmes qui tombent du ciel, cris au loin.
    // Le son dépend de la distance et du côté de l'événement par rapport à la caméra :
    // un événement loin du joueur s'entend à peine, voire pas du tout.
    public class DistantEvents : MonoBehaviour
    {
        [Header("Effets (systèmes de particules du décor, déclenchés par Emit)")]
        [SerializeField] private ParticleSystem rockFx;
        [SerializeField] private ParticleSystem dustFx;
        [SerializeField] private ParticleSystem emberFx;
        [SerializeField] private ParticleSystem fallingSoulFx;
        [SerializeField] private Light[] vents;          // foyers éteints qui peuvent se raviver

        [Header("Son")]
        [SerializeField] private AmbientWind wind;
        [SerializeField] private float hearingDistance = 110f;

        [Header("Où et quand")]
        [SerializeField] private Vector2 interval = new Vector2(4f, 14f);
        [SerializeField] private Vector2 xRange = new Vector2(-90f, 90f);
        [SerializeField] private Vector2 mountainDepth = new Vector2(38f, 70f);
        [SerializeField] private Vector2 mountainHeight = new Vector2(3f, 12f);

        private float timer;
        private float[] ventBase;

        private void Start()
        {
            timer = Random.Range(interval.x, interval.y) * 0.5f;
            ventBase = new float[vents != null ? vents.Length : 0];
            for (int i = 0; i < ventBase.Length; i++) ventBase[i] = vents[i] != null ? vents[i].intensity : 0f;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(interval.x, interval.y);

            float r = Random.value;
            if (r < 0.3f) Rockfall();
            else if (r < 0.5f) HeatFlash();
            else if (r < 0.7f) VentFlare();
            else if (r < 0.85f) FallingSoul();
            else Cry();
        }

        private void Rockfall()
        {
            Vector3 p = new Vector3(Random.Range(xRange.x, xRange.y), Random.Range(mountainHeight.x, mountainHeight.y),
                Random.Range(mountainDepth.x, mountainDepth.y));
            Emit(rockFx, p, Random.Range(8, 20), 1.5f, new Vector3(Random.Range(-1f, 1f), 0.5f, -1f), 1.5f);
            Emit(dustFx, p + Vector3.down * 2f, Random.Range(4, 8), 3f, Vector3.up * 0.3f, 0.4f);
            Sound(AmbientWind.DistantSound.Rumble, p, 0.4f);
        }

        private void HeatFlash()
        {
            Atmosphere atmosphere = Atmosphere.Current;
            if (atmosphere != null) atmosphere.Flash(Random.Range(0.35f, 0.9f), Random.Range(1.2f, 2.8f));
            // Le tonnerre arrive après la lueur, comme s'il venait de loin
            Vector3 p = new Vector3(Random.Range(xRange.x, xRange.y), 20f, Random.Range(60f, 120f));
            StartCoroutine(Delayed(Random.Range(0.6f, 2.5f), () => Sound(AmbientWind.DistantSound.Rumble, p, 0.8f)));
        }

        private void VentFlare()
        {
            if (vents == null || vents.Length == 0) return;
            int i = Random.Range(0, vents.Length);
            Light vent = vents[i];
            if (vent == null) return;
            StartCoroutine(Flare(vent, ventBase[i]));
            Emit(emberFx, vent.transform.position, Random.Range(10, 25), 0.6f, Vector3.up * 1.2f, 0.6f);
        }

        private void FallingSoul()
        {
            Vector3 p = new Vector3(Random.Range(xRange.x, xRange.y), Random.Range(18f, 28f), Random.Range(30f, 80f));
            Emit(fallingSoulFx, p, 1, 0f, new Vector3(Random.Range(-4f, 4f), -9f, 0f), 1f);
            if (Random.value < 0.4f) StartCoroutine(Delayed(0.4f, () => Sound(AmbientWind.DistantSound.Cry, p, 0.5f)));
        }

        private void Cry()
        {
            Vector3 p = new Vector3(Random.Range(xRange.x, xRange.y), 0f, Random.Range(20f, 70f));
            Sound(AmbientWind.DistantSound.Cry, p, 0.7f);
        }

        // Émission ponctuelle d'un système de particules du décor, à une position donnée
        private static void Emit(ParticleSystem fx, Vector3 position, int count, float spread, Vector3 velocity, float velocitySpread)
        {
            if (fx == null) return;
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = position + Random.insideUnitSphere * spread;
                ep.velocity = velocity + Random.insideUnitSphere * velocitySpread;
                fx.Emit(ep, 1);
            }
        }

        private void Sound(AmbientWind.DistantSound kind, Vector3 position, float loudness)
        {
            if (wind == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 d = position - cam.transform.position;
            float distance = new Vector2(d.x, d.z).magnitude;
            float level = loudness * Mathf.Clamp01(1f - distance / hearingDistance);
            if (level < 0.03f) return;
            wind.PlayDistant(kind, d.x / 40f, level);
        }

        private static IEnumerator Delayed(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action();
        }

        // Un foyer se ravive quelques secondes puis retombe en braises
        private static IEnumerator Flare(Light vent, float baseIntensity)
        {
            float duration = Random.Range(2.5f, 5f);
            float peak = baseIntensity * Random.Range(3f, 5f) + 0.5f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float p = t / duration;
                float env = Mathf.Min(1f, p * 6f) * (1f - p);
                float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 8f, baseIntensity);
                vent.intensity = Mathf.Lerp(baseIntensity, peak, env) * flicker;
                yield return null;
            }
            vent.intensity = baseIntensity;
        }
    }
}
