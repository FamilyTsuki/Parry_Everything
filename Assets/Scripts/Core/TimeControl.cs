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

        // Retour à la normale (rechargement de scène)
        public static void ResetAll()
        {
            paused = false;
            effectScale = 1f;
            inspectScale = 1f;
            Apply();
        }

        private static void Apply()
        {
            Time.timeScale = paused ? 0f : effectScale * inspectScale;
        }

        // Les champs statiques survivent sans rechargement de domaine : on repart propre à chaque lancement
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            paused = false;
            effectScale = 1f;
            inspectScale = 1f;
        }
    }
}
