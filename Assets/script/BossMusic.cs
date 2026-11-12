using UnityEngine;

// Musique du combat contre le Gardien des Enfers, synthétisée en temps réel (aucun fichier audio).
// Ré mineur, progression Dm – Bb – Gm – A (une mesure chacun). Les couches arrivent avec le combat :
//   début          : cors graves + grosses percussions (boum sub-grave, taiko grave)
//   1re parade     : charleston « ti ti ti ti » en doubles croches
//   phase II       : violons en ostinato + basse (les cors s'effacent)
//   phase III      : taikos, caisse claire, cuivres en staccato 3-3-2, chœur
//   phase IV       : mélodie héroïque aux cuivres
//   phase V        : le chœur et une trompette doublent la mélodie, tempo plus rapide
// Chaque nouvelle phase tombe sur un premier temps avec un impact (accord de cuivres, boum, cymbale).
// Tout changement attend le premier temps de la mesure suivante ; une montée de souffle l'annonce.
// Vie du joueur basse : tension (violons en trémolo, pulsation grave en croches), puis battements de cœur.
// Boss vaincu : la musique finit sa mesure (roulement de caisse claire), puis cadence Si♭ – Do – Ré majeur
// qui ralentit, trompette qui monte (Fa – Sol – La), taikos et cymbale sur l'accord final.
// La synthèse tourne dans OnAudioFilterRead (thread audio) : pas d'API Unity ni d'allocation là-dedans.
public class BossMusic : MonoBehaviour
{
    [Header("Réglages")]
    [Range(0f, 1f)] public float masterVolume = 0.6f;
    public float baseBpm = 108f;          // phase I
    public float finalBpm = 132f;         // phase V
    public bool reverb = true;            // écho de grotte

    [Header("Références (trouvées automatiquement si vides)")]
    public HellGuardian boss;
    public player hero;

    // ---------- Couches ----------
    const int L_HORN = 0, L_TICK = 1, L_STRING = 2, L_DRUM = 3, L_BRASS = 4, L_CHOIR = 5, L_HEART = 6, L_VICTORY = 7, L_FX = 8,
              L_PERC = 9, L_STAB = 10, L_TENSION = 11, L_COUNT = 12;
    private readonly float[] layerRequest = new float[L_COUNT];  // demandé par le jeu (thread principal)
    private readonly float[] layerTarget = new float[L_COUNT];   // appliqué au début de chaque mesure (thread audio)
    private readonly float[] layerGain = new float[L_COUNT];     // lissé par le thread audio

    // État partagé avec le thread audio
    private volatile bool playing;
    private volatile bool victory;
    private volatile int phase;
    private int hitPhase = -1;            // dernière phase annoncée par un impact (thread audio)
    private volatile float bpmTarget = 108f;

    // État du thread principal
    private bool parried;
    private bool wasActive;
    private float maxLife;

    // ---------- Synthèse ----------
    const int T_BRASS = 0, T_STRING = 1, T_HAT = 2, T_TAIKO = 3, T_SNARE = 4, T_CHOIR = 5, T_PAD = 6, T_CYMBAL = 7, T_SWELL = 8, T_BOOM = 9;

    class Voice
    {
        public bool on;
        public int type, layer;
        public float freq, amp, pan;
        public float t, attack, hold, release;
        public float ph1, ph2, ph3, vibPh;
        public float lp, lp2;
        public float detune, bright;
        public readonly float[] harm = new float[6];
    }

    private Voice[] voices;
    private float sampleRate = 48000f;
    private double beat;                  // position en temps
    private int lastStep = -1;
    private float bpm = 108f;
    private int cadenceStep = -1;         // étape de la cadence de victoire (-1 = pas commencée)
    private volatile bool cadenceStarted;
    private bool audioStarted;            // le combat a commencé côté audio
    private uint noiseState = 22222u;
    private System.Random rng = new System.Random(7);

    // Accords (racine MIDI, mineur ?)
    static readonly int[] chordRoot = { 50, 46, 43, 45 };          // D3, Bb2, G2, A2
    static readonly bool[] chordMinor = { true, false, true, false };
    // Ostinato des violons : indices dans [fondamentale, tierce, quinte, octave]
    static readonly int[] ostinato = { 0, 1, 2, 1, 3, 1, 2, 1, 0, 1, 2, 1, 3, 2, 1, 2 };
    // Taikos (doubles croches)
    static readonly int[] taiko = { 1, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1 };
    // Mélodie : par mesure, (début en doubles croches, durée, note MIDI)
    static readonly int[][] melody =
    {
        new[] { 0, 6, 74,  6, 2, 77,  8, 8, 81 },              // D5 . F5 A5
        new[] { 0, 6, 82,  6, 2, 81,  8, 8, 77 },              // Bb5 . A5 F5
        new[] { 0, 4, 79,  4, 4, 81,  8, 4, 82,  12, 4, 86 },  // G5 A5 Bb5 D6
        new[] { 0, 8, 85,  8, 8, 81 },                         // C#6 A5
    };

    // =====================================================================
    //  Thread principal
    // =====================================================================

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        voices = new Voice[128];
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
    }

    void Start()
    {
        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.Play();

        if (reverb && GetComponent<AudioReverbFilter>() == null)
        {
            // Ajouté après ce script : traite le son généré
            AudioReverbFilter rev = gameObject.AddComponent<AudioReverbFilter>();
            rev.reverbPreset = AudioReverbPreset.Cave;
            rev.dryLevel = 0f;
            rev.room = -1200f;
        }
        player.OnParry += HandleParry;
    }

    void OnDestroy()
    {
        player.OnParry -= HandleParry;
    }

    void HandleParry()
    {
        if (playing) parried = true;
    }

    void Update()
    {
        if (hero == null) hero = FindFirstObjectByType<player>();
        if (boss == null && !wasActive) boss = FindFirstObjectByType<HellGuardian>();
        if (hero != null && maxLife <= 0f) maxLife = hero.life;

        bool active = boss != null && boss.IsActive && !boss.IsDead;
        if (active)
        {
            wasActive = true;
            playing = true;
            phase = boss.Phase;
        }
        // Boss mort (ou détruit) après le combat : victoire
        if (wasActive && (boss == null || boss.IsDead)) victory = true;

        int ph = phase;
        // À la victoire, le combat continue jusqu'au début de la cadence, puis s'efface
        bool on = playing && !cadenceStarted;
        layerRequest[L_HORN] = on && ph < 1 ? 1f : 0f;   // les cors laissent la place aux violons en phase II
        layerRequest[L_TICK] = on && parried ? 1f : 0f;
        layerRequest[L_STRING] = on && ph >= 1 ? 1f : 0f;
        layerRequest[L_DRUM] = on && ph >= 2 ? 1f : 0f;
        layerRequest[L_BRASS] = on && ph >= 3 ? 1f : 0f;
        layerRequest[L_CHOIR] = on && ph >= 2 ? 1f : 0f;
        layerRequest[L_PERC] = on ? 1f : 0f;
        layerRequest[L_STAB] = on && ph >= 2 ? 1f : 0f;
        layerRequest[L_VICTORY] = victory ? 1f : 0f;

        // Vie du joueur : tension sous 55 %, cœur sous 30 %
        float ratio = (hero != null && maxLife > 0f) ? Mathf.Clamp01(hero.life / maxLife) : 1f;
        layerRequest[L_TENSION] = on && !victory ? Mathf.Clamp01((0.55f - ratio) / 0.35f) : 0f;
        layerRequest[L_HEART] = on && ratio > 0f ? Mathf.Clamp01((0.3f - ratio) / 0.25f) : 0f;

        // Ralentissement final pendant la cadence
        bpmTarget = cadenceStarted ? baseBpm * 0.8f : Mathf.Lerp(baseBpm, finalBpm, ph / 4f);
    }

    // =====================================================================
    //  Thread audio
    // =====================================================================

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (voices == null) return;
        int frames = data.Length / channels;
        float blockTime = frames / sampleRate;

        // Fondus des couches (~1 s), tempo qui glisse
        float k = 1f - Mathf.Exp(-blockTime / 0.8f);
        // Entrée franche (sur le temps), sortie en fondu
        float kIn = 1f - Mathf.Exp(-blockTime / 0.06f);
        for (int l = 0; l < L_COUNT; l++)
        {
            float kl = layerTarget[l] > layerGain[l] ? kIn : k;
            layerGain[l] += (layerTarget[l] - layerGain[l]) * kl;
        }
        bpm += (bpmTarget - bpm) * (1f - Mathf.Exp(-blockTime / 3f));
        double beatInc = bpm / 60.0 / sampleRate;
        // Début du combat : la musique part sur le premier temps
        if (playing && !audioStarted)
        {
            audioStarted = true;
            beat = 0.0;
            lastStep = -1;
        }
        float dt = 1f / sampleRate;
        float vol = masterVolume;

        for (int f = 0; f < frames; f++)
        {
            // Séquenceur : une étape par double croche
            beat += beatInc;
            int step = (int)(beat * 4.0);
            if (step != lastStep)
            {
                lastStep = step;
                Step(step);
            }

            float left = 0f, right = 0f;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (!v.on) continue;
                float g = layerGain[v.layer];
                float s = Render(v, dt) * g;
                left += s * (1f - v.pan);
                right += s * (1f + v.pan);
            }
            // Saturation douce
            left *= vol; right *= vol;
            left = left / (1f + Mathf.Abs(left));
            right = right / (1f + Mathf.Abs(right));

            int idx = f * channels;
            if (channels >= 2)
            {
                data[idx] += left;
                data[idx + 1] += right;
                for (int c = 2; c < channels; c++) data[idx + c] += (left + right) * 0.5f;
            }
            else
            {
                data[idx] += (left + right) * 0.5f;
            }
        }
    }

    // Déclenche les notes d'une double croche
    void Step(int step)
    {
        int s16 = step % 16;
        int bar = step / 16;
        int c = bar % 4;
        float stepDur = 60f / bpm / 4f;
        float barDur = stepDur * 16f;

        // La victoire réagit tout de suite (roulement) ; le reste attend le premier temps
        layerTarget[L_VICTORY] = layerRequest[L_VICTORY];
        layerTarget[L_FX] = 1f;                 // effets de transition : toujours audibles
        if (s16 == 0 && !cadenceStarted)
        {
            for (int l = 0; l < L_COUNT; l++) if (l != L_FX) layerTarget[l] = layerRequest[l];
        }

        // Une couche va entrer à la prochaine mesure : montée de souffle sur le dernier temps
        if (s16 == 12 && playing && !victory)
        {
            for (int l = 0; l < L_COUNT; l++)
            {
                if (l != L_VICTORY && l != L_FX && l != L_HEART && l != L_TENSION && layerRequest[l] > 0.5f && layerTarget[l] < 0.5f)
                {
                    Play(T_SWELL, L_FX, 0, 0.1f, stepDur * 4f, 0f, 0.02f, 0f, 0f);
                    break;
                }
            }
        }

        if (victory && playing)
        {
            if (cadenceStep < 0)
            {
                if (s16 == 0)
                {
                    // Premier temps : la cadence commence
                    cadenceStep = 0;
                    cadenceStarted = true;
                    // Les couches du combat s'effacent dès le début de la cadence
                    for (int l = 0; l < L_COUNT; l++) if (l != L_VICTORY && l != L_FX) layerTarget[l] = 0f;
                }
                else if (s16 >= 10)
                {
                    // Roulement de caisse claire qui monte jusqu'au premier temps
                    Play(T_SNARE, L_VICTORY, 0, 0.05f + (s16 - 10) * 0.025f, 0f, 0f, 0.09f, 0.1f, 0f);
                }
            }
            if (cadenceStep >= 0)
            {
                Cadence(cadenceStep, stepDur);
                cadenceStep++;
                return;
            }
        }
        if (!playing) return;

        int root = chordRoot[c];
        int third = root + (chordMinor[c] ? 3 : 4);
        int fifth = root + 7;
        int[] tones = { root, third, fifth, root + 12 };

        // Impact au premier temps d'une nouvelle phase : accord de cuivres, boum, cymbale
        if (s16 == 0 && phase > hitPhase)
        {
            hitPhase = phase;
            float hl = stepDur * 3f;
            Play(T_BRASS, L_FX, root - 24, 0.12f, 0.01f, hl, 0.6f, 0f, 3f);
            Play(T_BRASS, L_FX, root - 12, 0.1f, 0.01f, hl, 0.6f, -0.3f, 3f);
            Play(T_BRASS, L_FX, fifth - 12, 0.08f, 0.01f, hl, 0.6f, 0.3f, 3f);
            Play(T_BRASS, L_FX, root, 0.08f, 0.01f, hl, 0.6f, -0.2f, 3f);
            Play(T_BRASS, L_FX, third + 12, 0.06f, 0.01f, hl, 0.6f, 0.2f, 3f);
            Play(T_BOOM, L_FX, 0, 0.9f, 0f, 0f, 1.4f, 0f, 0f);
            Play(T_TAIKO, L_FX, 0, 0.8f, 0f, 0f, 0.6f, 0f, 0f);
            Play(T_CYMBAL, L_FX, 0, 0.11f, 0f, 0f, 1.6f, 0f, 0f);
        }

        // Cors : accord tenu sur toute la mesure, doublé à l'octave grave
        if (s16 == 0 && (layerTarget[L_HORN] > 0f || layerGain[L_HORN] > 0.01f))
        {
            Play(T_BRASS, L_HORN, root - 24, 0.1f, 0.2f, barDur - 0.2f, 0.5f, 0f, 0.8f);
            Play(T_BRASS, L_HORN, root - 12, 0.13f, 0.25f, barDur - 0.25f, 0.5f, 0f, 1.2f);
            Play(T_BRASS, L_HORN, root, 0.1f, 0.3f, barDur - 0.3f, 0.5f, -0.3f, 1.2f);
            Play(T_BRASS, L_HORN, fifth, 0.08f, 0.35f, barDur - 0.35f, 0.5f, 0.3f, 1.2f);
            Play(T_BRASS, L_HORN, third + 12, 0.05f, 0.4f, barDur - 0.4f, 0.5f, 0.1f, 1.2f);
        }

        // Grosses percussions (tout le combat) : boum sub-grave toutes les 2 mesures, taiko grave
        if (layerTarget[L_PERC] > 0f || layerGain[L_PERC] > 0.01f)
        {
            if (s16 == 0 && bar % 2 == 0) Play(T_BOOM, L_PERC, 0, 0.6f, 0f, 0f, 1.1f, 0f, 0f);
            if (s16 == 0 || s16 == 8 || (c == 3 && (s16 == 13 || s16 == 14 || s16 == 15)))
                Play(T_TAIKO, L_PERC, 0, s16 == 0 ? 0.55f : 0.4f, 0f, 0f, 0.45f, 0f, -1f);
        }

        // Tension (vie basse) : pulsation grave en croches + violons aigus en trémolo
        if (layerTarget[L_TENSION] > 0f || layerGain[L_TENSION] > 0.01f)
        {
            if (s16 % 2 == 0)
            {
                Play(T_STRING, L_TENSION, root - 12, 0.09f, 0.004f, stepDur * 0.8f, 0.06f, -0.2f, 0f);
                Play(T_STRING, L_TENSION, root - 24, 0.07f, 0.004f, stepDur * 0.8f, 0.06f, 0.2f, 0f);
            }
            if (s16 == 0)
            {
                Play(T_STRING, L_TENSION, fifth + 12, 0.045f, 0.3f, barDur - 0.3f, 0.3f, -0.5f, 13f);
                Play(T_STRING, L_TENSION, root + 24, 0.04f, 0.3f, barDur - 0.3f, 0.3f, 0.5f, 13f);
                Play(T_STRING, L_TENSION, third + 24, 0.035f, 0.3f, barDur - 0.3f, 0.3f, 0f, 13f);
            }
        }

        // Cuivres en staccato, rythme 3-3-2 (phase III+)
        if ((layerTarget[L_STAB] > 0f || layerGain[L_STAB] > 0.01f) && (s16 == 0 || s16 == 3 || s16 == 6 || s16 == 8 || s16 == 11 || s16 == 14))
        {
            float a = (s16 == 0 || s16 == 8) ? 1f : 0.7f;
            float len = stepDur * 1.2f;
            Play(T_BRASS, L_STAB, root - 12, 0.09f * a, 0.01f, len, 0.12f, 0f, 2.6f);
            Play(T_BRASS, L_STAB, root, 0.07f * a, 0.01f, len, 0.12f, -0.3f, 2.6f);
            Play(T_BRASS, L_STAB, fifth, 0.06f * a, 0.01f, len, 0.12f, 0.3f, 2.6f);
        }

        // Charleston : ti ti ti ti
        if (layerTarget[L_TICK] > 0f || layerGain[L_TICK] > 0.01f)
        {
            float a = s16 % 4 == 2 ? 0.09f : (s16 % 2 == 1 ? 0.045f : 0.065f);
            Play(T_HAT, L_TICK, 0, a, 0f, 0f, s16 % 4 == 2 ? 0.06f : 0.025f, 0.35f, 0f);
        }

        // Violons en ostinato + basse
        if (layerTarget[L_STRING] > 0f || layerGain[L_STRING] > 0.01f)
        {
            int note = tones[ostinato[s16]] + 12;
            Play(T_STRING, L_STRING, note, 0.055f, 0.005f, stepDur * 0.6f, 0.07f, (s16 % 2 == 0) ? -0.5f : 0.5f, 0f);
            if (s16 % 2 == 0) Play(T_STRING, L_STRING, root - 12, 0.08f, 0.005f, stepDur * 1.2f, 0.1f, 0f, 0f);
        }

        // Taikos + caisse claire (roulement à la dernière mesure en phase V)
        if (layerTarget[L_DRUM] > 0f || layerGain[L_DRUM] > 0.01f)
        {
            bool fill = phase >= 4 && c == 3 && s16 >= 12;
            if (taiko[s16] == 1 || fill) Play(T_TAIKO, L_DRUM, 0, s16 == 0 ? 0.7f : 0.45f, 0f, 0f, 0.4f, 0f, 0f);
            if (s16 == 4 || s16 == 12 || fill) Play(T_SNARE, L_DRUM, 0, 0.18f, 0f, 0f, 0.15f, 0.1f, 0f);
        }

        // Mélodie héroïque (cuivres), doublée aux violons en phase V
        if (layerTarget[L_BRASS] > 0f || layerGain[L_BRASS] > 0.01f)
        {
            int[] m = melody[c];
            for (int i = 0; i < m.Length; i += 3)
            {
                if (m[i] != s16) continue;
                float len = m[i + 1] * stepDur;
                Play(T_BRASS, L_BRASS, m[i + 2] - 12, 0.12f, 0.04f, len - 0.04f, 0.25f, 0.1f, 2.6f);
                Play(T_BRASS, L_BRASS, m[i + 2] - 24, 0.08f, 0.04f, len - 0.04f, 0.25f, -0.1f, 2f);
                if (phase >= 4)
                {
                    // Phase V : trompette à l'octave, chœur et violons à l'unisson
                    Play(T_BRASS, L_BRASS, m[i + 2], 0.06f, 0.03f, len - 0.03f, 0.25f, 0.2f, 3f);
                    Play(T_CHOIR, L_BRASS, m[i + 2] - 12, 0.06f, 0.05f, len - 0.05f, 0.3f, -0.3f, 0f);
                    Play(T_PAD, L_BRASS, m[i + 2], 0.05f, 0.05f, len - 0.05f, 0.3f, -0.2f, 0f);
                }
            }
        }

        // Chœur : accord tenu
        if (s16 == 0 && (layerTarget[L_CHOIR] > 0f || layerGain[L_CHOIR] > 0.01f))
        {
            Play(T_CHOIR, L_CHOIR, root, 0.06f, 0.5f, barDur - 0.5f, 0.8f, 0f, 0f);       // basses du chœur
            Play(T_CHOIR, L_CHOIR, root + 12, 0.06f, 0.6f, barDur - 0.6f, 0.8f, -0.4f, 0f);
            Play(T_CHOIR, L_CHOIR, third + 12, 0.05f, 0.6f, barDur - 0.6f, 0.8f, 0.4f, 0f);
            Play(T_CHOIR, L_CHOIR, fifth + 12, 0.05f, 0.6f, barDur - 0.6f, 0.8f, 0f, 0f);
        }

        // Battements de cœur (vie très basse) : boum-boum toutes les 2 temps
        if ((layerTarget[L_HEART] > 0f || layerGain[L_HEART] > 0.01f) && (s16 == 0 || s16 == 2 || s16 == 8 || s16 == 10))
        {
            Play(T_TAIKO, L_HEART, 0, (s16 % 8 == 0) ? 0.6f : 0.4f, 0f, 0f, 0.25f, 0f, -1f);
        }
    }

    // Fin : Si♭ (2 temps) – Do (2 temps) – Ré majeur tenu. Trompette Fa – Sol – La.
    void Cadence(int r, float stepDur)
    {
        if (r == 0 || r == 8)
        {
            bool bb = r == 0;
            int root = bb ? 46 : 48;                     // Bb2 / C3
            float hold = 8f * stepDur - 0.05f;
            Play(T_BRASS, L_VICTORY, root - 12, 0.11f, 0.05f, hold, 0.6f, 0f, 1.2f);
            Play(T_BRASS, L_VICTORY, root, 0.08f, 0.05f, hold, 0.6f, -0.3f, 1.4f);
            Play(T_BRASS, L_VICTORY, root + 7, 0.07f, 0.05f, hold, 0.6f, 0.3f, 1.4f);
            Play(T_BRASS, L_VICTORY, root + 16, 0.06f, 0.05f, hold, 0.6f, 0f, 1.4f);
            Play(T_PAD, L_VICTORY, root + 19, 0.03f, 0.08f, hold, 0.6f, -0.5f, 0f);
            Play(T_PAD, L_VICTORY, root + 24, 0.03f, 0.08f, hold, 0.6f, 0.5f, 0f);
            Play(T_BRASS, L_VICTORY, bb ? 77 : 79, 0.08f, 0.03f, hold, 0.5f, 0.1f, 2.4f);   // trompette F5 / G5
            Play(T_TAIKO, L_VICTORY, 0, 0.7f, 0f, 0f, 0.5f, 0f, 0f);
            Play(T_SNARE, L_VICTORY, 0, 0.15f, 0f, 0f, 0.2f, 0.1f, 0f);
        }
        else if (r == 4 || r == 12)
        {
            Play(T_TAIKO, L_VICTORY, 0, 0.45f, 0f, 0f, 0.35f, 0f, 0f);
        }
        else if (r == 16)
        {
            // Ré majeur final, large et lumineux
            float hold = 3.5f;
            Play(T_BRASS, L_VICTORY, 38, 0.12f, 0.08f, hold, 2.5f, 0f, 1.2f);
            Play(T_BRASS, L_VICTORY, 50, 0.08f, 0.08f, hold, 2.5f, -0.3f, 1.4f);
            Play(T_BRASS, L_VICTORY, 57, 0.07f, 0.08f, hold, 2.5f, 0.3f, 1.4f);
            Play(T_BRASS, L_VICTORY, 62, 0.06f, 0.08f, hold, 2.5f, -0.1f, 1.4f);
            Play(T_BRASS, L_VICTORY, 66, 0.06f, 0.08f, hold, 2.5f, 0.1f, 1.4f);
            Play(T_BRASS, L_VICTORY, 81, 0.08f, 0.04f, hold, 2.5f, 0.1f, 2.4f);              // trompette A5
            Play(T_CHOIR, L_VICTORY, 74, 0.05f, 0.4f, hold, 2.5f, -0.4f, 0f);
            Play(T_CHOIR, L_VICTORY, 78, 0.045f, 0.4f, hold, 2.5f, 0.4f, 0f);
            Play(T_CHOIR, L_VICTORY, 81, 0.045f, 0.4f, hold, 2.5f, 0f, 0f);
            Play(T_PAD, L_VICTORY, 86, 0.025f, 0.6f, hold, 2.5f, -0.5f, 0f);
            Play(T_PAD, L_VICTORY, 90, 0.02f, 0.6f, hold, 2.5f, 0.5f, 0f);
            Play(T_TAIKO, L_VICTORY, 0, 0.9f, 0f, 0f, 0.7f, 0f, 0f);
            Play(T_TAIKO, L_VICTORY, 0, 0.6f, 0f, 0f, 0.6f, 0f, -1f);                         // grave
            Play(T_CYMBAL, L_VICTORY, 0, 0.12f, 0f, 0f, 1.8f, 0f, 0f);
        }
    }

    float Pan() { return (float)(rng.NextDouble() * 1.2 - 0.6); }

    // Lance une note. bright : brillance des cuivres ; trémolo (Hz) des violons ; pour le taiko, -1 = grave
    void Play(int type, int layer, int midi, float amp, float attack, float hold, float release, float pan, float bright)
    {
        Voice v = null;
        float oldest = -1f;
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].on) { v = voices[i]; break; }
            if (voices[i].t > oldest) { oldest = voices[i].t; v = voices[i]; }
        }
        v.on = true;
        v.type = type;
        v.layer = layer;
        v.freq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        v.amp = amp;
        v.attack = Mathf.Max(attack, 0.001f);
        v.hold = Mathf.Max(hold, 0f);
        v.release = release;
        v.pan = Mathf.Clamp(pan, -1f, 1f);
        v.t = 0f;
        v.ph1 = (float)rng.NextDouble();
        v.ph2 = (float)rng.NextDouble();
        v.ph3 = (float)rng.NextDouble();
        v.vibPh = (float)rng.NextDouble();
        v.lp = v.lp2 = 0f;
        v.bright = bright;
        // Désaccord léger : effet d'ensemble (chorus)
        float cents = 6f;
        v.detune = Mathf.Pow(2f, cents / 1200f);

        if (type == T_CHOIR)
        {
            // Voyelle « aah » : harmoniques pondérées autour de deux formants
            float sum = 0f;
            for (int h = 0; h < v.harm.Length; h++)
            {
                float fh = v.freq * (h + 1);
                float w = Gauss(fh, 750f, 250f) + 0.8f * Gauss(fh, 1150f, 300f) + 0.15f / (h + 1);
                v.harm[h] = w;
                sum += w;
            }
            for (int h = 0; h < v.harm.Length; h++) v.harm[h] /= sum;
        }
    }

    static float Gauss(float x, float mu, float sigma)
    {
        float z = (x - mu) / sigma;
        return Mathf.Exp(-z * z);
    }

    float Noise()
    {
        noiseState ^= noiseState << 13;
        noiseState ^= noiseState >> 17;
        noiseState ^= noiseState << 5;
        return (noiseState / 4294967295f) * 2f - 1f;
    }

    // Enveloppe tenue : montée, maintien, extinction exponentielle
    static float Env(Voice v)
    {
        if (v.t < v.attack) return v.t / v.attack;
        float r = v.t - v.attack - v.hold;
        if (r <= 0f) return 1f;
        return Mathf.Exp(-r / Mathf.Max(v.release, 0.001f));
    }

    static float Saw(ref float ph, float inc)
    {
        ph += inc;
        if (ph >= 1f) ph -= 1f;
        return ph * 2f - 1f;
    }

    float Render(Voice v, float dt)
    {
        float s = 0f;
        float twoPiDt = 6.2831853f * dt;
        switch (v.type)
        {
            case T_BRASS:
            {
                float env = Env(v);
                float inc = v.freq * dt;
                float x = Saw(ref v.ph1, inc) + Saw(ref v.ph2, inc * v.detune);
                // Le filtre s'ouvre avec le souffle : son de cor
                float cutoff = 180f + 900f * v.bright * env + v.freq;
                float a = Mathf.Min(cutoff * twoPiDt, 1f);
                v.lp += (x - v.lp) * a;
                v.lp2 += (v.lp - v.lp2) * a;
                s = v.lp2 * env * v.amp;
                if (v.t > v.attack + v.hold + v.release * 6f) v.on = false;
                break;
            }
            case T_STRING:
            case T_PAD:
            {
                float env = Env(v);
                v.vibPh += 5.5f * dt;
                float vib = 1f + Mathf.Sin(v.vibPh * 6.2831853f) * 0.003f;
                float inc = v.freq * dt * vib;
                float x = Saw(ref v.ph1, inc) + Saw(ref v.ph2, inc * v.detune) + Saw(ref v.ph3, inc / v.detune);
                float a = Mathf.Min((v.type == T_PAD ? 3500f : 2600f) * twoPiDt, 1f);
                v.lp += (x - v.lp) * a;
                v.lp2 += (v.lp - v.lp2) * a;
                s = v.lp2 * env * v.amp * 0.6f;
                // Trémolo (archet qui frotte vite) : bright = vitesse en Hz
                if (v.bright > 0f) s *= 0.6f + 0.4f * Mathf.Sin(v.t * v.bright * 6.2831853f);
                if (v.t > v.attack + v.hold + v.release * 6f) v.on = false;
                break;
            }
            case T_SWELL:
            {
                // Souffle qui monte (cymbale à l'envers) jusqu'au temps suivant
                float n = Noise();
                v.lp += (n - v.lp) * 0.3f;
                float e = v.t < v.attack ? (v.t / v.attack) * (v.t / v.attack) : Mathf.Exp(-(v.t - v.attack) / v.release);
                s = (n - v.lp) * e * v.amp;
                if (v.t > v.attack + v.release * 6f) v.on = false;
                break;
            }
            case T_BOOM:
            {
                // Boum de cinéma : sinus qui plonge de 90 à 30 Hz + souffle grave à l'attaque
                float f = 30f + 60f * Mathf.Exp(-v.t * 7f);
                v.ph1 += f * dt;
                if (v.ph1 >= 1f) v.ph1 -= 1f;
                float n = Noise();
                v.lp += (n - v.lp) * 0.02f;
                float x = Mathf.Sin(v.ph1 * 6.2831853f) + v.lp * 3f * Mathf.Exp(-v.t * 18f);
                s = x * Mathf.Exp(-v.t / v.release) * v.amp;
                if (v.t > v.release * 6f) v.on = false;
                break;
            }
            case T_CYMBAL:
            {
                // Bruit aigu à longue extinction
                float n = Noise();
                v.lp += (n - v.lp) * 0.25f;
                s = (n - v.lp) * Mathf.Exp(-v.t / v.release) * v.amp;
                if (v.t > v.release * 6f) v.on = false;
                break;
            }
            case T_HAT:
            {
                float n = Noise();
                v.lp += (n - v.lp) * 0.5f;          // passe-haut = bruit - passe-bas
                s = (n - v.lp) * Mathf.Exp(-v.t / v.release) * v.amp;
                if (v.t > v.release * 6f) v.on = false;
                break;
            }
            case T_TAIKO:
            {
                bool heart = v.bright < 0f;
                float f0 = heart ? 55f : 110f;
                float f = f0 * (0.45f + 0.55f * Mathf.Exp(-v.t * 28f));
                v.ph1 += f * dt;
                if (v.ph1 >= 1f) v.ph1 -= 1f;
                float body = Mathf.Sin(v.ph1 * 6.2831853f);
                float click = v.t < 0.006f ? Noise() * (1f - v.t / 0.006f) * 0.5f : 0f;
                s = (body + click) * Mathf.Exp(-v.t / v.release) * v.amp;
                if (v.t > v.release * 6f) v.on = false;
                break;
            }
            case T_SNARE:
            {
                float n = Noise();
                v.lp += (n - v.lp) * 0.35f;
                v.lp2 += (v.lp - v.lp2) * 0.08f;    // bande médium = passe-bas - passe-bas plus lent
                v.ph1 += 190f * dt;
                if (v.ph1 >= 1f) v.ph1 -= 1f;
                float tone = Mathf.Sin(v.ph1 * 6.2831853f) * Mathf.Exp(-v.t / 0.05f) * 0.5f;
                s = ((v.lp - v.lp2) * 1.6f + tone) * Mathf.Exp(-v.t / v.release) * v.amp;
                if (v.t > v.release * 6f) v.on = false;
                break;
            }
            case T_CHOIR:
            {
                float env = Env(v);
                v.vibPh += 4.8f * dt;
                float vib = 1f + Mathf.Sin(v.vibPh * 6.2831853f) * 0.004f;
                v.ph1 += v.freq * dt * vib;
                if (v.ph1 >= 1f) v.ph1 -= 1f;
                v.ph2 += v.freq * dt * vib * v.detune;
                if (v.ph2 >= 1f) v.ph2 -= 1f;
                float x = 0f;
                for (int h = 0; h < v.harm.Length; h++)
                {
                    float w = v.harm[h];
                    if (w < 0.01f) continue;
                    x += w * (Mathf.Sin(v.ph1 * 6.2831853f * (h + 1)) + Mathf.Sin(v.ph2 * 6.2831853f * (h + 1)));
                }
                s = x * env * v.amp;
                if (v.t > v.attack + v.hold + v.release * 6f) v.on = false;
                break;
            }
        }
        v.t += dt;
        return s;
    }
}
