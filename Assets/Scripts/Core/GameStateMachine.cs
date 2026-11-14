using System;

namespace ParryEverything.Core
{
    // Logique pure du flux de jeu : quelles transitions sont permises.
    // Sans MonoBehaviour, pour être testée en EditMode. GameFlow s'en sert dans la scène.
    public class GameStateMachine
    {
        // (ancien état, nouvel état)
        public event Action<GameState, GameState> Changed;

        public GameState State { get; private set; }

        public GameStateMachine(GameState initial = GameState.Menu)
        {
            State = initial;
        }

        // Menu titre -> jeu
        public bool StartGame() => State == GameState.Menu ? Go(GameState.Playing) : false;

        // Jeu <-> pause
        public bool TogglePause()
        {
            if (State == GameState.Playing) return Go(GameState.Paused);
            if (State == GameState.Paused) return Go(GameState.Playing);
            return false;
        }

        // Le joueur meurt : seulement en jeu (un coup ne peut pas tomber en pause)
        public bool PlayerDied() => State == GameState.Playing ? Go(GameState.Dead) : false;

        // Réapparition après la mort
        public bool Respawn() => State == GameState.Dead ? Go(GameState.Playing) : false;

        // Fin de la partie (boss vaincu, en attendant la vraie fin de zone)
        public bool Finish() => State == GameState.Playing ? Go(GameState.End) : false;

        // Scène sans contrôle (vision du meurtre...) : seulement depuis le jeu, et retour au jeu ensuite
        public bool BeginCutscene() => State == GameState.Playing ? Go(GameState.Cutscene) : false;
        public bool EndCutscene() => State == GameState.Cutscene ? Go(GameState.Playing) : false;

        // Retour au menu titre, depuis n'importe quel état
        public bool ReturnToMenu() => State != GameState.Menu ? Go(GameState.Menu) : false;

        private bool Go(GameState next)
        {
            GameState previous = State;
            State = next;
            Changed?.Invoke(previous, next);
            return true;
        }
    }
}
