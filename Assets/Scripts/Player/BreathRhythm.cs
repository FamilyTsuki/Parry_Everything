using UnityEngine;

namespace ParryEverything.Player
{
    // Rythme de la respiration et apnée (logique pure, testable). Un souffle dure 'Period' secondes ;
    // il commence par l'inspiration (phase 0), l'expiration commence à mi-cycle.
    // Tout se pare, même sa respiration : une parade à moins de 'Window' secondes du début d'une inspiration
    // bloque ce souffle. Souffles bloqués d'affilée = apnée ; au bout de 'SuffocateTime' secondes, étouffé.
    // Une inspiration laissée passer, ou une parade hors rythme, rend le souffle (l'apnée repart à zéro).
    public sealed class BreathRhythm
    {
        public readonly float Period;
        public readonly float Window;
        public readonly float SuffocateTime;

        private float now;
        private float cycleStart;
        private bool blocked;            // le souffle en cours est paré
        private bool decided;            // la fenêtre du souffle en cours est passée
        private float earlyPress = float.NegativeInfinity;   // parade juste avant le prochain souffle
        private float apneaStart = -1f;

        public BreathRhythm(float period, float window, float suffocateTime, float start = 0f)
        {
            Period = Mathf.Max(0.1f, period);
            Window = Mathf.Clamp(window, 0.01f, Period * 0.45f);
            SuffocateTime = Mathf.Max(0.1f, suffocateTime);
            Reset(start);
        }

        // 0 → 1 sur un souffle (0 : début de l'inspiration, 0,5 : début de l'expiration)
        public float Phase => Mathf.Clamp01((now - cycleStart) / Period);
        public bool Blocked => blocked;
        public bool Holding => apneaStart >= 0f;
        public float Apnea => Holding ? Mathf.Max(0f, now - apneaStart) : 0f;
        public float Suffocation => Mathf.Clamp01(Apnea / SuffocateTime);
        public bool Suffocated => Holding && Apnea >= SuffocateTime;

        public void Reset(float t)
        {
            now = t;
            cycleStart = t;
            blocked = false;
            decided = false;
            earlyPress = float.NegativeInfinity;
            apneaStart = -1f;
        }

        // Annule l'apnée en cours (menus, mise en scène) sans casser le rythme
        public void CancelApnea()
        {
            blocked = false;
            earlyPress = float.NegativeInfinity;
            apneaStart = -1f;
        }

        // Avance jusqu'à 't'. exhaled : une expiration (non bloquée) a commencé depuis le dernier appel.
        public void Tick(float t, out bool exhaled)
        {
            exhaled = false;
            float prev = (now - cycleStart) / Period;
            now = Mathf.Max(now, t);
            while (now - cycleStart >= Period)
            {
                if (!decided) Decide();
                if (!blocked && prev < 0.5f) exhaled = true;
                cycleStart += Period;
                blocked = cycleStart - earlyPress <= Window;
                decided = false;
                prev = 0f;
            }
            if (!decided && now - cycleStart > Window) Decide();
            if (!blocked && prev < 0.5f && (now - cycleStart) / Period >= 0.5f) exhaled = true;
        }

        // Le joueur pare à l'instant 't'
        public void Parry(float t)
        {
            float since = t - cycleStart;
            float until = cycleStart + Period - t;
            if (since >= 0f && since <= Window)
            {
                blocked = true;
            }
            else if (until <= Window)
            {
                earlyPress = t;
            }
            else
            {
                // Hors rythme : il reprend son souffle
                blocked = false;
                apneaStart = -1f;
            }
        }

        // Fin de la fenêtre : souffle bloqué (l'apnée continue) ou pris (elle s'arrête)
        private void Decide()
        {
            decided = true;
            if (blocked)
            {
                if (apneaStart < 0f) apneaStart = cycleStart;
            }
            else
            {
                apneaStart = -1f;
            }
        }
    }
}
