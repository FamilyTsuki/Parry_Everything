using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.UI
{
    // Sons de l'interface, synthétisés au démarrage (comme le reste du son du jeu) :
    // déplacement (petit tintement), validation (deux notes qui montent), retour (deux notes qui descendent).
    // Jouent aussi en pause (une AudioSource n'est pas arrêtée par Time.timeScale).
    public class UiSounds : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float volume = 0.35f;

        private AudioSource source;
        private AudioClip move;
        private AudioClip submit;
        private AudioClip back;

        public void PlayMove() => Play(move, 0.6f);
        public void PlaySubmit() => Play(submit, 1f);
        public void PlayBack() => Play(back, 0.9f);

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            move = Tone("UiMove", 0.05f, 1320f, 1320f, 60f);
            submit = Tone("UiSubmit", 0.16f, 660f, 990f, 22f);
            back = Tone("UiBack", 0.14f, 620f, 410f, 26f);
        }

        private void Play(AudioClip clip, float gain)
        {
            if (clip != null) source.PlayOneShot(clip, volume * gain * GameSettings.SfxVolume);
        }

        // Note douce (sinus + un peu d'octave), qui glisse de f0 à f1 à mi-durée, décroissance exponentielle
        private static AudioClip Tone(string name, float seconds, float f0, float f1, float decay)
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = t < seconds * 0.5f ? f0 : f1;
                phase += 2.0 * Mathf.PI * f / rate;
                float attack = Mathf.Clamp01(t / 0.004f);
                float env = attack * Mathf.Exp(-decay * t);
                data[i] = env * (0.8f * (float)System.Math.Sin(phase) + 0.2f * (float)System.Math.Sin(phase * 2.0));
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
