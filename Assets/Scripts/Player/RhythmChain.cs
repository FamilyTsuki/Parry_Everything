using System;

namespace ParryEverything.Player
{
    // Enchaînement de parades « en rythme » sur des événements qui ne sont pas des attaques (battements de cœur,
    // pas de course…). Logique pure, testable.
    //  - Un événement est paré si une parade tombe à moins de 'Window' secondes de lui (avant ou après).
    //  - Un événement laissé passer, ou une parade qui ne tombe près d'aucun événement, rompt la chaîne.
    // Resolved(paré) est appelé une fois par événement, quand sa fenêtre se referme.
    public sealed class RhythmChain
    {
        public readonly float Window;
        public Action<bool> Resolved;

        private float lastEvent = float.NegativeInfinity;
        private bool lastParried;
        private bool pending;                                   // dernier événement pas encore tranché
        private float pendingPress = float.NegativeInfinity;    // parade en avance, en attente d'un événement

        public RhythmChain(float window)
        {
            Window = Math.Max(0.01f, window);
        }

        public int Count { get; private set; }                  // événements parés d'affilée
        public float StartTime { get; private set; } = -1f;     // instant du premier événement de la chaîne

        public float Duration(float now) => Count > 0 ? Math.Max(0f, now - StartTime) : 0f;

        public void Reset()
        {
            lastEvent = float.NegativeInfinity;
            pending = false;
            lastParried = false;
            pendingPress = float.NegativeInfinity;
            Break();
        }

        // Un événement a lieu à 't'
        public void Event(float t)
        {
            if (pending) Resolve();
            lastEvent = t;
            pending = true;
            lastParried = t - pendingPress <= Window;
            if (lastParried) pendingPress = float.NegativeInfinity;
        }

        // Le joueur pare à 't'
        public void Parry(float t)
        {
            if (pending && !lastParried && t - lastEvent <= Window)
            {
                lastParried = true;
                return;
            }
            // Deux parades sans événement entre elles : ce n'est plus du rythme
            if (!float.IsNegativeInfinity(pendingPress)) Break();
            pendingPress = t;
        }

        // Ferme les fenêtres dépassées
        public void Tick(float t)
        {
            if (pending && t - lastEvent > Window) Resolve();
            if (!float.IsNegativeInfinity(pendingPress) && t - pendingPress > Window)
            {
                pendingPress = float.NegativeInfinity;
                Break();
            }
        }

        private void Resolve()
        {
            pending = false;
            if (lastParried)
            {
                if (Count == 0) StartTime = lastEvent;
                Count++;
            }
            else
            {
                Break();
            }
            Resolved?.Invoke(lastParried);
        }

        private void Break()
        {
            Count = 0;
            StartTime = -1f;
        }
    }
}
