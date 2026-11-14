using ParryEverything.Audio;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Environment
{
    // Sanctuaire avant le boss : en y entrant, le chat se repose (membres en poche recousus, membres soignés,
    // âme pleine), la partie est sauvegardée et on y réapparaîtra. Tant qu'il y reste, le vent se tait
    // et le thème du boss s'entend au loin.
    [RequireComponent(typeof(Collider))]
    public class Sanctuary : MonoBehaviour
    {
        [SerializeField] private string sanctuaryId = "zone1-gardien";
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;   // point de réapparition par rapport au sanctuaire

        [Header("Flamme")]
        [SerializeField] private Light flame;                          // lueur du sanctuaire, s'avive au repos
        [SerializeField] private float flareBoost = 2.5f;
        [SerializeField] private Color restParticleColor = new Color(0.45f, 0.75f, 1f);

        private AmbientWind wind;
        private BossMusic music;
        private float baseIntensity = 1f;
        private float flare;

        public Vector3 SpawnPoint => transform.position + spawnOffset;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Start()
        {
            wind = FindFirstObjectByType<AmbientWind>();
            music = FindFirstObjectByType<BossMusic>();
            if (flame != null) baseIntensity = flame.intensity;
        }

        private void Update()
        {
            flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 0.8f);
            if (flame != null)
            {
                float flicker = 1f + (Mathf.PerlinNoise(Time.time * 3f, 0.4f) - 0.5f) * 0.3f;
                flame.intensity = baseIntensity * flicker * (1f + flare * flareBoost);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerBody body = other.GetComponentInParent<PlayerBody>();
            if (body == null || body.IsDead) return;

            body.RestAtSanctuary();
            if (GameFlow.Instance != null) GameFlow.Instance.SaveAtSanctuary(sanctuaryId, SpawnPoint);

            flare = 1f;
            ParticleBurst.Spawn("SanctuaryRest", transform.position + Vector3.up * 0.5f, restParticleColor, 60,
                lifetime: new Vector2(0.6f, 1.4f), speed: new Vector2(0.5f, 2.5f), size: new Vector2(0.04f, 0.12f),
                gravity: -0.3f, radius: 0.6f, duration: 0.6f);
            SetQuiet(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerBody>() == null) return;
            SetQuiet(false);
        }

        private void OnDisable()
        {
            SetQuiet(false);
        }

        // Vent coupé, thème du boss au loin
        private void SetQuiet(bool quiet)
        {
            if (wind != null) wind.Muted = quiet;
            if (music != null) music.Distant = quiet;
        }
    }
}
