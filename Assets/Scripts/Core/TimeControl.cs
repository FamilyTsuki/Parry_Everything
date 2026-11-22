using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // Seul endroit qui écrit Time.timeScale.
    // Les effets (arrêt du temps, ralentis) demandent une échelle ; la pause passe avant tout.
    public static class TimeControl
    {
        private static bool paused;
        private static float effectScale = 1f;
        private static float inspectScale = 1f;
        private static readonly Dictionary<string, float> layers = new Dictionary<string, float>();

        public static bool Paused => paused;

        // Pause du jeu (menu, pause, écran de fin) : le temps est figé quels que soient les effets
        public static void SetPaused(bool value)
        {
            paused = value;
            Apply();
        }

        // Échelle voulue par un effet de jeu (0 = arrêt du temps, 0.3 = ralenti, 1 = normal)
        public static void SetEffectScale(float scale)
        {
            effectScale = Mathf.Max(0f, scale);
            Apply();
        }

        // Ralenti d'observation (scène AnimationLab) : multiplie les effets sans les écraser
        public static void SetInspectScale(float scale)
        {
            inspectScale = Mathf.Max(0f, scale);
            Apply();
        }

        public static float InspectScale => inspectScale;

        // Ralenti ou arrêt durable d'un système ('key'), multiplié aux autres : un arrêt sur image (SetEffectScale)
        // ne l'écrase pas. 1 = retiré. Ex. : « heart » (cœur paré), « reaper » (la faucheuse fige le temps).
        public static void SetLayerScale(string key, float scale)
        {
            scale = Mathf.Max(0f, scale);
            if (Mathf.Approximately(scale, 1f)) layers.Remove(key);
            else layers[key] = scale;
            Apply();
        }

        // Retour à la normale (rechargement de scène)
        public static void ResetAll()
        {
            paused = false;
            effectScale = 1f;
            inspectScale = 1f;
            layers.Clear();
            Apply();
        }

        private static void Apply()
        {
            float scale = effectScale * inspectScale;
            foreach (float l in layers.Values) scale *= l;
            Time.timeScale = paused ? 0f : scale;
        }

        // Les champs statiques survivent sans rechargement de domaine : on repart propre à chaque lancement
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            paused = false;
            effectScale = 1f;
            inspectScale = 1f;
            layers.Clear();
        }
    }
}
