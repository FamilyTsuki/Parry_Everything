using ParryEverything.Boss;
using UnityEngine;
using UnityEngine.Serialization;

namespace ParryEverything.Audio
{
    // Ambiance sonore de la zone, synthétisée en temps réel, jouée en boucle pendant toute la partie.
    //  - Vent glacé : bruit filtré sombre, rafales lentes, sifflement fin et lointain.
    //  - Drone : deux notes graves presque à l'unisson (battement lent) et une tierce mineure.
    //    Pendant le combat de boss, un triton s'ajoute peu à peu.
    //  - Chuchotements : rares, très bas, d'un seul côté. Bruit passé dans deux formants qui
    //    sautent de "syllabe" en "syllabe" : on croit entendre une voix sans comprendre les mots.
    //  - Craquements : rares, comme de la glace ou du bois qui travaille.
    //  - Silences soudains : de temps en temps, tout se tait quelques secondes (hors combat).
    //  - Sons lointains à la demande (PlayDistant) : grondement d'éboulement ou de tonnerre,
    //    cri d'une âme au loin. Placés à gauche ou à droite selon la position de l'événement.
    // Plus discret pendant le combat contre le boss pour laisser la place à la musique.
    // La synthèse tourne dans OnAudioFilterRead (thread audio) : pas d'API Unity ni d'allocation là-dedans.
    public class AmbientWind : MonoBehaviour
    {
        public enum DistantSound { Rumble, Cry }

        [FormerlySerializedAs("volume")]
        [Range(0f, 1f)] [SerializeField] private float volume = 0.25f;
        [FormerlySerializedAs("whistle")]
        [Range(0f, 1f)] [SerializeField] private float whistle = 0.2f;        // part du sifflement dans le vent
        [FormerlySerializedAs("duringBoss")]
        [Range(0f, 1f)] [SerializeField] private float duringBoss = 0.5f;     // volume relatif pendant le combat
        [SerializeField] private float gustSpeed = 0.7f;                      // vitesse des rafales

        [Header("Couches")]
        [Range(0f, 1f)] [SerializeField] private float drone = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float whispers = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float creaks = 0.5f;

        [Header("Fréquence des événements (secondes entre deux)")]
        [SerializeField] private Vector2 whisperInterval = new Vector2(18f, 45f);
        [SerializeField] private Vector2 creakInterval = new Vector2(10f, 30f);
        [SerializeField] private Vector2 silenceInterval = new Vector2(45f, 100f);

        private volatile float gainTarget;
        private volatile float dreadTarget;
        private volatile bool fighting;
        private float sampleRate = 48000f;
        private HellGuardian boss;

        // État du thread audio
        private uint seed = 12345u;
        private float gain, dread;

        // Vent
        private float gust, gustGoal, gustTimer;
        private float pan, panGoal;
        private readonly float[] lp1 = new float[2], lp2 = new float[2];
        private readonly float[] svLow = new float[2], svBand = new float[2];

        // Drone
        private float droneA, droneB, droneC, droneD, droneLfo;

        // Chuchotements
        private float whisperTimer = 12f;
        private float whisperLeft, whisperLength, whisperPan;
        private float syllableLeft, syllableLength;
        private float f1, f2, f1Goal, f2Goal;
        private float w1Low, w1Band, w2Low, w2Band, whisperHp;

        // Craquements
        private float creakTimer = 6f;
        private float creakLeft, creakLength, creakPan;
        private float creakRateStart, creakRateEnd, creakPhase, creakFreq;
        private float c1Low, c1Band, c2Low, c2Band;

        // Silences
        private float silenceTimer = 60f;
        private float silenceLeft;
        private float duck = 1f;

        // Demandes de sons lointains (écrites par le thread principal, lues par le thread audio)
        private volatile bool rumbleRequested, cryRequested;
        private volatile float rumbleReqPan, rumbleReqLoud, cryReqPan, cryReqLoud;

        // Grondement
        private float rumbleLeft, rumbleLength, rumblePan, rumbleLoud;
        private float r1, r2, r3;

        // Cri lointain
        private float cryLeft, cryLength, cryPan, cryLoud, cryPhase, cryPitchStart, cryPitchEnd, cryVibrato;
        private float y1Low, y1Band, y2Low, y2Band, cryLp;

        // pan : -1 gauche .. 1 droite ; loudness : 0..1 (plus faible quand c'est loin)
        // Coupé (sanctuaire) : le vent s'efface en douceur
        public bool Muted { get; set; }

        public void PlayDistant(DistantSound sound, float pan, float loudness)
        {
            pan = Mathf.Clamp(pan, -1f, 1f);
            loudness = Mathf.Clamp01(loudness);
            if (sound == DistantSound.Rumble)
            {
                rumbleReqPan = pan;
                rumbleReqLoud = loudness;
                rumbleRequested = true;
            }
            else
            {
                cryReqPan = pan;
                cryReqLoud = loudness;
                cryRequested = true;
            }
        }

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            seed = (uint)Random.Range(1, int.MaxValue);
        }

        private void Start()
        {
            AudioSource src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.Play();
        }

        private void Update()
        {
            if (boss == null) boss = FindFirstObjectByType<HellGuardian>();
            bool fight = boss != null && boss.IsActive && !boss.IsDead;
            fighting = fight;
            dreadTarget = fight ? 1f : 0f;
            gainTarget = Muted ? 0f : volume * (fight ? duringBoss : 1f);
        }

        private float Rand()
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed / 4294967295f;
        }

        private float Range(Vector2 r)
        {
            return r.x + Rand() * (r.y - r.x);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            int frames = data.Length / channels;
            float dt = 1f / sampleRate;
            float blockTime = frames * dt;

            ScheduleEvents(blockTime);

            float kg = 1f - Mathf.Exp(-blockTime * gustSpeed / 1.5f);
            float kv = 1f - Mathf.Exp(-blockTime / 1.5f);
            float kd = 1f - Mathf.Exp(-blockTime / 4f);
            // Silence : coupure rapide, retour lent
            float duckGoal = silenceLeft > 0f ? 0.04f : 1f;
            float kDuck = 1f - Mathf.Exp(-blockTime / (silenceLeft > 0f ? 0.12f : 2.5f));

            float twoPi = 6.2831853f;
            float droneLevel = drone * 0.11f;

            for (int f = 0; f < frames; f++)
            {
                gust += (gustGoal - gust) * kg / frames;
                pan += (panGoal - pan) * kg / frames;
                gain += (gainTarget - gain) * kv / frames;
                dread += (dreadTarget - dread) * kd / frames;
                duck += (duckGoal - duck) * kDuck / frames;

                // --- Vent : souffle sombre, plus clair pendant une rafale ---
                float cutoff = 160f + 520f * gust;
                float a = Mathf.Min(cutoff * twoPi * dt, 1f);
                float whistleHz = 900f + 500f * gust;
                float fsv = 2f * Mathf.Sin(Mathf.PI * whistleHz * dt);
                float amp = 0.2f + 0.8f * gust * gust;

                // --- Drone : battement lent, respiration très lente du volume ---
                droneLfo += 0.04f * dt;
                if (droneLfo > 1f) droneLfo -= 1f;
                float breathe = 0.65f + 0.35f * Mathf.Sin(droneLfo * twoPi);
                droneA += 49f * dt; if (droneA > 1f) droneA -= 1f;
                droneB += 49.31f * dt; if (droneB > 1f) droneB -= 1f;
                droneC += 58.27f * dt; if (droneC > 1f) droneC -= 1f;
                droneD += 69.3f * dt; if (droneD > 1f) droneD -= 1f;
                float third = Mathf.Sin(droneC * twoPi) * 0.45f;
                float tritone = Mathf.Sin(droneD * twoPi) * 0.5f * dread;
                float droneL = (Mathf.Sin(droneA * twoPi) + third + tritone) * droneLevel * breathe;
                float droneR = (Mathf.Sin(droneB * twoPi) + third + tritone) * droneLevel * breathe;

                float whisper = WhisperSample(dt);
                float creak = CreakSample(dt);
                float rumble = RumbleSample(dt);
                float cry = CrySample(dt);

                int idx = f * channels;
                for (int c = 0; c < 2; c++)
                {
                    float n = Rand() * 2f - 1f;
                    lp1[c] += (n - lp1[c]) * a;
                    lp2[c] += (lp1[c] - lp2[c]) * a;
                    float breath = lp2[c] * 2.6f;

                    // Filtre résonant (bande étroite) : sifflement fin
                    float high = n - svLow[c] - 0.03f * svBand[c];
                    svBand[c] += fsv * high;
                    svLow[c] += fsv * svBand[c];
                    float whis = svBand[c] * 0.04f * gust;

                    float side = c == 0 ? 1f - pan : 1f + pan;
                    float wind = (breath * (1f - whistle) + whis * whistle) * amp * side;

                    float wSide = c == 0 ? 1f - whisperPan : 1f + whisperPan;
                    float cSide = c == 0 ? 1f - creakPan : 1f + creakPan;
                    float rSide = c == 0 ? 1f - rumblePan * 0.7f : 1f + rumblePan * 0.7f;
                    float ySide = c == 0 ? 1f - cryPan * 0.8f : 1f + cryPan * 0.8f;
                    float events = whisper * wSide + creak * cSide + rumble * rSide + cry * ySide;

                    float droneS = c == 0 ? droneL : droneR;
                    float s = ((wind + events) * duck + droneS * (0.35f + 0.65f * duck)) * gain;
                    if (c < channels) data[idx + c] += s;
                    else data[idx] += s;   // sortie mono : les deux côtés mélangés
                }
            }
        }

        // Décompte des événements rares (une fois par bloc audio)
        private void ScheduleEvents(float blockTime)
        {
            // Rafales : nouvelle cible toutes les 3 à 8 s
            gustTimer -= blockTime * gustSpeed;
            if (gustTimer <= 0f)
            {
                gustTimer = 3f + Rand() * 5f;
                gustGoal = Rand() < 0.25f ? 0.6f + Rand() * 0.4f : 0.1f + Rand() * 0.35f;
                panGoal = Rand() * 1.4f - 0.7f;
            }

            whisperTimer -= blockTime;
            if (whisperTimer <= 0f && whisperLeft <= 0f)
            {
                whisperTimer = Range(whisperInterval) * (fighting ? 0.7f : 1f);
                whisperLength = 1.4f + Rand() * 1.8f;
                whisperLeft = whisperLength;
                whisperPan = (Rand() < 0.5f ? -1f : 1f) * (0.6f + Rand() * 0.3f);
                syllableLeft = 0f;
            }

            creakTimer -= blockTime;
            if (creakTimer <= 0f && creakLeft <= 0f)
            {
                creakTimer = Range(creakInterval);
                creakLength = 0.35f + Rand() * 0.9f;
                creakLeft = creakLength;
                creakPan = Rand() * 1.6f - 0.8f;
                creakRateStart = 12f + Rand() * 30f;
                creakRateEnd = 6f + Rand() * 50f;
                creakFreq = 280f + Rand() * 450f;
            }

            // Pas de silence pendant le combat : la musique doit rester présente
            silenceTimer -= blockTime;
            if (silenceTimer <= 0f)
            {
                silenceTimer = Range(silenceInterval);
                if (!fighting) silenceLeft = 1.8f + Rand() * 2.2f;
            }
            if (silenceLeft > 0f) silenceLeft -= blockTime;

            if (rumbleRequested)
            {
                rumbleRequested = false;
                rumbleLength = 2.5f + Rand() * 2.5f;
                rumbleLeft = rumbleLength;
                rumblePan = rumbleReqPan;
                rumbleLoud = rumbleReqLoud;
            }
            if (cryRequested && cryLeft <= 0f)
            {
                cryRequested = false;
                cryLength = 1.6f + Rand() * 1.6f;
                cryLeft = cryLength;
                cryPan = cryReqPan;
                cryLoud = cryReqLoud;
                cryPitchStart = 230f + Rand() * 120f;
                cryPitchEnd = cryPitchStart * (0.55f + Rand() * 0.2f);
                cryVibrato = 4f + Rand() * 3f;
            }
        }

        // Grondement : bruit très grave, attaque rapide, longue traîne qui roule
        private float RumbleSample(float dt)
        {
            if (rumbleLeft <= 0f) return 0f;
            rumbleLeft -= dt;
            float p = Mathf.Clamp01(1f - rumbleLeft / rumbleLength);
            float n = Rand() * 2f - 1f;
            float k = 70f * 6.2831853f * dt;
            r1 += (n - r1) * k;
            r2 += (r1 - r2) * k;
            r3 += (r2 - r3) * k;
            float env = Mathf.Min(1f, p * 12f) * Mathf.Pow(1f - p, 1.6f);
            // Roulements : le volume ondule pendant la traîne
            float roll = 0.7f + 0.3f * Mathf.Sin(p * 23f + Mathf.Sin(p * 7f) * 2f);
            return r3 * 9f * env * roll * rumbleLoud;
        }

        // Cri lointain : voix qui glisse vers le grave, étouffée par la distance
        private float CrySample(float dt)
        {
            if (cryLeft <= 0f) return 0f;
            cryLeft -= dt;
            float p = Mathf.Clamp01(1f - cryLeft / cryLength);
            float pitch = Mathf.Lerp(cryPitchStart, cryPitchEnd, p * p) * (1f + Mathf.Sin(p * cryLength * cryVibrato * 6.2831853f) * 0.02f);
            cryPhase += pitch * dt;
            if (cryPhase > 1f) cryPhase -= 1f;
            float src = (cryPhase * 2f - 1f) * 0.6f + (Rand() * 2f - 1f) * 0.25f;

            float k1 = 2f * Mathf.Sin(Mathf.PI * 650f * dt);
            float h1 = src - y1Low - 0.2f * y1Band;
            y1Band += k1 * h1;
            y1Low += k1 * y1Band;
            float k2 = 2f * Mathf.Sin(Mathf.PI * 1050f * dt);
            float h2 = src - y2Low - 0.25f * y2Band;
            y2Band += k2 * h2;
            y2Low += k2 * y2Band;

            float voice = y1Band * 0.6f + y2Band * 0.4f;
            // La distance mange les aigus
            cryLp += (voice - cryLp) * Mathf.Min(1f, 900f * 6.2831853f * dt);
            float env = Mathf.Sin(p * Mathf.PI);
            env *= env;
            return cryLp * 0.18f * env * cryLoud;
        }

        private float WhisperSample(float dt)
        {
            if (whisperLeft <= 0f) return 0f;
            whisperLeft -= dt;

            // Nouvelle "syllabe" : formants d'une voyelle au hasard, courte pause possible
            syllableLeft -= dt;
            if (syllableLeft <= 0f)
            {
                syllableLength = 0.07f + Rand() * 0.16f;
                syllableLeft = syllableLength;
                f1Goal = 300f + Rand() * 500f;
                f2Goal = 900f + Rand() * 1400f;
                if (f1 <= 0f)
                {
                    f1 = f1Goal;
                    f2 = f2Goal;
                }
            }
            f1 += (f1Goal - f1) * 0.002f;
            f2 += (f2Goal - f2) * 0.002f;

            // Bruit sans graves (souffle de voix)
            float n = Rand() * 2f - 1f;
            whisperHp += (n - whisperHp) * 0.08f;
            float src = n - whisperHp;

            float k1 = 2f * Mathf.Sin(Mathf.PI * f1 * dt);
            float h1 = src - w1Low - 0.25f * w1Band;
            w1Band += k1 * h1;
            w1Low += k1 * w1Band;
            float k2 = 2f * Mathf.Sin(Mathf.PI * f2 * dt);
            float h2 = src - w2Low - 0.3f * w2Band;
            w2Band += k2 * h2;
            w2Low += k2 * w2Band;

            float sp = 1f - syllableLeft / syllableLength;
            float syllable = Mathf.Sin(sp * Mathf.PI);
            syllable *= syllable;
            float overall = Mathf.Sin((1f - whisperLeft / whisperLength) * Mathf.PI);
            return (w1Band * 0.6f + w2Band * 0.4f) * 0.35f * syllable * overall * whispers;
        }

        private float CreakSample(float dt)
        {
            if (creakLeft <= 0f) return 0f;
            creakLeft -= dt;
            float p = Mathf.Clamp01(1f - creakLeft / creakLength);

            // Train de petits clics dont le rythme glisse, chaque clic fait sonner deux résonances
            float rate = Mathf.Lerp(creakRateStart, creakRateEnd, p);
            creakPhase += rate * dt;
            float impulse = 0f;
            if (creakPhase >= 1f)
            {
                creakPhase -= 1f;
                impulse = 0.5f + Rand() * 0.5f;
            }

            float k1 = 2f * Mathf.Sin(Mathf.PI * creakFreq * dt);
            float h1 = impulse - c1Low - 0.05f * c1Band;
            c1Band += k1 * h1;
            c1Low += k1 * c1Band;
            float k2 = 2f * Mathf.Sin(Mathf.PI * creakFreq * 1.63f * dt);
            float h2 = impulse - c2Low - 0.08f * c2Band;
            c2Band += k2 * h2;
            c2Low += k2 * c2Band;

            float env = Mathf.Sin(p * Mathf.PI);
            return (c1Band + c2Band * 0.5f) * 7f * env * creaks;
        }
    }
}
