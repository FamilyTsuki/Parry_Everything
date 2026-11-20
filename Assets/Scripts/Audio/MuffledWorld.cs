using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Audio
{
    // Le monde ralentit au son (GAME_DESIGN 6.2) : chaque parade parfaite enchaînée assourdit le son ;
    // au bout de 3, la musique baisse d'un demi-ton et on n'entend presque plus que le cœur du chat.
    // À poser sur l'objet qui porte l'AudioListener (caméra) : le filtre passe-bas s'applique à tout le son.
    public class MuffledWorld : MonoBehaviour
    {
        // Index = niveau de la chaîne (0 à 3)
        [SerializeField] private float[] cutoffByLevel = { 22000f, 6000f, 2200f, 700f };
        [SerializeField] private float[] musicSemitonesByLevel = { 0f, 0f, 0f, -1f };
        [SerializeField] private float[] heartbeatByLevel = { 0f, 0f, 0.25f, 0.9f };
        [SerializeField] private float muffleSpeed = 8f;    // vers plus assourdi : rapide
        [SerializeField] private float recoverSpeed = 1.5f; // retour au son normal : lent
        [SerializeField] private float heartbeatBpm = 58f;

        private AudioLowPassFilter filter;
        private AudioSource heart;
        private PlayerController player;
        private BossMusic music;
        private float cutoff = 22000f;

        private void Start()
        {
            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null) filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = cutoff;

            // Source sur un enfant créé inactif : sinon elle tente de jouer à l'ajout, avant d'avoir son clip
            GameObject heartObject = new GameObject("Heartbeat");
            heartObject.SetActive(false);
            heartObject.transform.SetParent(transform, false);
            heart = heartObject.AddComponent<AudioSource>();
            heart.playOnAwake = false;
            heart.clip = MakeHeartbeat(heartbeatBpm);
            heart.loop = true;
            heart.spatialBlend = 0f;
            heart.volume = 0f;
            heartObject.SetActive(true);
            heart.Play();
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (music == null) music = FindFirstObjectByType<BossMusic>();
            int level = player != null ? Mathf.Clamp(player.PerfectChainLevel, 0, cutoffByLevel.Length - 1) : 0;

            // Lissage en échelle logarithmique (l'oreille entend les fréquences ainsi)
            float target = cutoffByLevel[level];
            float speed = target < cutoff ? muffleSpeed : recoverSpeed;
            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(target), k));
            filter.cutoffFrequency = cutoff;

            heart.volume = Mathf.Lerp(heart.volume, Pick(heartbeatByLevel, level) * GameSettings.SfxVolume, k);
            if (music != null) music.Transpose = Pick(musicSemitonesByLevel, level);
        }

        private static float Pick(float[] values, int level)
        {
            return values.Length == 0 ? 0f : values[Mathf.Min(level, values.Length - 1)];
        }

        // Battement de cœur généré : « poum-poum » grave, une fois par battement
        private static AudioClip MakeHeartbeat(float bpm)
        {
            int sr = AudioSettings.outputSampleRate;
            int n = Mathf.Max(1, (int)(sr * 60f / Mathf.Max(20f, bpm)));
            float[] d = new float[n];
            AddThump(d, sr, 0f, 1f);
            AddThump(d, sr, 0.28f, 0.7f);
            AudioClip c = AudioClip.Create("Heartbeat", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static void AddThump(float[] d, int sr, float start, float gain)
        {
            int s0 = (int)(start * sr);
            int len = (int)(0.18f * sr);
            float ph = 0f;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = i / (float)sr;
                ph += (38f + 40f * Mathf.Exp(-t * 30f)) / sr;
                float env = Mathf.Exp(-t / 0.05f) * Mathf.Min(1f, t * 400f);
                d[s0 + i] += Mathf.Sin(ph * 6.2831853f) * env * gain;
            }
        }
    }
}
