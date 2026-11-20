using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // Seul endroit qui lit le clavier. Le jeu demande des actions (GameInput.Pressed(GameAction.Parry)),
    // jamais des touches : la manette ou la reconfiguration s'ajouteront ici sans toucher au reste.
    public static class GameInput
    {
        private static readonly Dictionary<GameAction, KeyCode[]> defaults = new Dictionary<GameAction, KeyCode[]>
        {
            { GameAction.Left, new[] { KeyCode.LeftArrow } },
            { GameAction.Right, new[] { KeyCode.RightArrow } },
            { GameAction.Jump, new[] { KeyCode.UpArrow, KeyCode.Space } },
            { GameAction.Parry, new[] { KeyCode.X, KeyCode.J } },
            { GameAction.Run, new[] { KeyCode.LeftShift } },
            { GameAction.Sew, new[] { KeyCode.DownArrow } },
            { GameAction.Pause, new[] { KeyCode.Escape } },
            { GameAction.Confirm, new[] { KeyCode.Return, KeyCode.KeypadEnter } },
            { GameAction.NewGame, new[] { KeyCode.N } },
            { GameAction.Menu, new[] { KeyCode.M } },
            { GameAction.Quit, new[] { KeyCode.Q } },
            { GameAction.DebugHeal, new[] { KeyCode.F1 } },
            { GameAction.DebugGodMode, new[] { KeyCode.F2 } },
            { GameAction.DebugToBoss, new[] { KeyCode.F3 } },
            { GameAction.DebugOverlay, new[] { KeyCode.F4 } },
            { GameAction.DebugToggleBody, new[] { KeyCode.F5 } },
            { GameAction.DebugKillBoss, new[] { KeyCode.F6 } },
            { GameAction.DebugSave, new[] { KeyCode.F7 } },
            { GameAction.DebugDie, new[] { KeyCode.F8 } },
            { GameAction.DebugEraseSave, new[] { KeyCode.F9 } },
        };

        private static readonly Dictionary<GameAction, KeyCode[]> bindings = new Dictionary<GameAction, KeyCode[]>();

        static GameInput()
        {
            ResetBindings();
        }

        // Tests et outils : force le déplacement horizontal (null = clavier)
        public static float? MoveOverride { get; set; }

        // Tests et outils : actions simulées (appui à l'image suivante, ou maintien)
        private static readonly Dictionary<GameAction, int> forcedPress = new Dictionary<GameAction, int>();
        private static readonly HashSet<GameAction> forcedHeld = new HashSet<GameAction>();

        // Appelé depuis OnGUI (après les Update) : l'appui compte à l'image suivante
        public static void SimulatePress(GameAction action) => forcedPress[action] = Time.frameCount + 1;

        public static void SimulateHold(GameAction action, bool held)
        {
            if (held) forcedHeld.Add(action);
            else forcedHeld.Remove(action);
        }

        // Déplacement horizontal : -1 gauche, 0, +1 droite (les deux appuyées s'annulent)
        public static float MoveX
        {
            get
            {
                if (MoveOverride.HasValue) return Mathf.Clamp(MoveOverride.Value, -1f, 1f);
                float x = 0f;
                if (Held(GameAction.Right)) x += 1f;
                if (Held(GameAction.Left)) x -= 1f;
                return x;
            }
        }

        // Appuyé à cette image
        public static bool Pressed(GameAction action)
        {
            if (forcedPress.TryGetValue(action, out int frame) && frame == Time.frameCount) return true;
            foreach (KeyCode key in Keys(action))
            {
                if (Input.GetKeyDown(key)) return true;
            }
            return false;
        }

        // Maintenu
        public static bool Held(GameAction action)
        {
            if (forcedHeld.Contains(action)) return true;
            foreach (KeyCode key in Keys(action))
            {
                if (Input.GetKey(key)) return true;
            }
            return false;
        }

        public static IReadOnlyList<KeyCode> Keys(GameAction action)
        {
            return bindings.TryGetValue(action, out KeyCode[] keys) ? keys : Array.Empty<KeyCode>();
        }

        // Nom lisible de la touche principale, pour les écrans (« Entrée : jouer »)
        public static string KeyName(GameAction action)
        {
            IReadOnlyList<KeyCode> keys = Keys(action);
            return keys.Count > 0 ? KeyLabel(keys[0]) : "?";
        }

        // Change les touches d'une action (futur écran d'options)
        public static void Rebind(GameAction action, params KeyCode[] keys)
        {
            bindings[action] = keys != null ? (KeyCode[])keys.Clone() : Array.Empty<KeyCode>();
        }

        public static void ResetBindings()
        {
            bindings.Clear();
            foreach (KeyValuePair<GameAction, KeyCode[]> pair in defaults)
            {
                bindings[pair.Key] = (KeyCode[])pair.Value.Clone();
            }
        }

        // Sans rechargement de domaine, les touches changées survivraient d'un lancement à l'autre
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MoveOverride = null;
            forcedPress.Clear();
            forcedHeld.Clear();
            ResetBindings();
        }

        private static string KeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Return: return "Entrée";
                case KeyCode.Escape: return "Échap";
                case KeyCode.Space: return "Espace";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftShift: return "Maj";
                default: return key.ToString();
            }
        }
    }
}
