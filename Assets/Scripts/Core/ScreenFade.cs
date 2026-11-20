using UnityEngine;

namespace ParryEverything.Core
{
    // Fondu au noir de tout l'écran, en temps réel (marche pendant la pause et les ralentis).
    // Statique : survit au rechargement de la scène, l'écran reste noir pendant le chargement, sans éclair.
    // GameFlow le fait avancer (Tick), l'interface le dessine (Alpha).
    public static class ScreenFade
    {
        private static float alpha;
        private static float from;
        private static float to;
        private static float duration;
        private static float elapsed;

        // 0 = image visible, 1 = noir
        public static float Alpha => alpha;
        public static bool IsFading => elapsed < duration;
        public static bool IsBlack => alpha >= 0.999f && !IsFading;

        // Fondu vers 'target' en 'seconds' (temps réel). Courbe douce aux deux bouts.
        public static void FadeTo(float target, float seconds)
        {
            from = alpha;
            to = Mathf.Clamp01(target);
            duration = Mathf.Max(0f, seconds);
            elapsed = 0f;
            if (duration <= 0f) alpha = to;
        }

        // Noir immédiat (avant un chargement) ou image immédiate
        public static void Set(float value)
        {
            alpha = from = to = Mathf.Clamp01(value);
            duration = elapsed = 0f;
        }

        public static void Tick(float unscaledDeltaTime)
        {
            if (elapsed >= duration) return;
            elapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Set(0f);
    }
}
