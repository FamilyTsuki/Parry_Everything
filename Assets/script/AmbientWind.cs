using UnityEngine;

// Vent d'ambiance, synthétisé en temps réel, joué en boucle pendant toute la partie.
//  - Souffle : bruit filtré dont le volume et la clarté suivent des rafales lentes et aléatoires.
//  - Sifflement : bande étroite et discrète qui monte et descend avec les rafales.
//  - Gauche et droite indépendants : le vent passe d'un côté à l'autre.
// Un peu plus discret pendant le combat contre le boss pour laisser la place à la musique.
// La synthèse tourne dans OnAudioFilterRead (thread audio) : pas d'API Unity ni d'allocation là-dedans.
public class AmbientWind : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.25f;
    [Range(0f, 1f)] public float whistle = 0.35f;        // part du sifflement
    [Range(0f, 1f)] public float duringBoss = 0.5f;      // volume relatif pendant le combat
    public float gustSpeed = 1f;                         // vitesse des rafales

    private volatile float gainTarget;
    private float gain;
    private float sampleRate = 48000f;
    private HellGuardian boss;

    // État du thread audio
    private uint seed = 12345u;
    private float gust, gustGoal, gustTimer;             // rafale globale 0..1
    private float pan, panGoal;
    private readonly float[] lp1 = new float[2], lp2 = new float[2];
    private readonly float[] svLow = new float[2], svBand = new float[2];

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    void Start()
    {
        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.Play();
    }

    void Update()
    {
        if (boss == null) boss = FindFirstObjectByType<HellGuardian>();
        bool fight = boss != null && boss.IsActive && !boss.IsDead;
        gainTarget = volume * (fight ? duringBoss : 1f);
    }

    float Rand()
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return seed / 4294967295f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        int frames = data.Length / channels;
        float dt = 1f / sampleRate;
        float blockTime = frames * dt;

        // Rafales : nouvelle cible toutes les 2 à 6 s, atteinte en douceur
        gustTimer -= blockTime * gustSpeed;
        if (gustTimer <= 0f)
        {
            gustTimer = 2f + Rand() * 4f;
            gustGoal = Rand() < 0.3f ? 0.7f + Rand() * 0.3f : 0.15f + Rand() * 0.4f;
            panGoal = Rand() * 1.2f - 0.6f;
        }
        float kg = 1f - Mathf.Exp(-blockTime * gustSpeed / 1.5f);
        float kv = 1f - Mathf.Exp(-blockTime / 1.5f);

        for (int f = 0; f < frames; f++)
        {
            gust += (gustGoal - gust) * kg / frames;
            pan += (panGoal - pan) * kg / frames;
            gain += (gainTarget - gain) * kv / frames;

            // Souffle plus clair et plus fort pendant une rafale
            float cutoff = 250f + 900f * gust;
            float a = Mathf.Min(cutoff * 6.2831853f * dt, 1f);
            float whistleHz = 500f + 700f * gust;
            float fsv = 2f * Mathf.Sin(Mathf.PI * whistleHz * dt);
            float amp = 0.25f + 0.75f * gust * gust;

            int idx = f * channels;
            for (int c = 0; c < 2; c++)
            {
                float n = Rand() * 2f - 1f;
                lp1[c] += (n - lp1[c]) * a;
                lp2[c] += (lp1[c] - lp2[c]) * a;
                float breath = lp2[c] * 2.2f;

                // Filtre résonant (bande étroite) : sifflement
                float high = n - svLow[c] - 0.04f * svBand[c];
                svBand[c] += fsv * high;
                svLow[c] += fsv * svBand[c];
                float whis = svBand[c] * 0.05f * gust;

                float side = c == 0 ? 1f - pan : 1f + pan;
                float s = (breath * (1f - whistle) + whis * whistle) * amp * side * gain;
                if (c < channels) data[idx + c] += s;
                else data[idx] += s;   // sortie mono : les deux côtés mélangés
            }
        }
    }
}
