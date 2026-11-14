using UnityEngine;

namespace ParryEverything.Core
{
    // Mesure du comportement du joueur, pour les fins (GAME_DESIGN 4 et 9). Enregistrée, pas encore utilisée.
    // Revient à la valeur de la dernière sauvegarde quand le joueur meurt.
    public static class BehaviorLedger
    {
        public static int SoulsAbsorbed { get; private set; }
        public static int EnemiesSpared { get; private set; }
        public static int WanderingSoulsBumped { get; private set; }

        public static void AddSoulAbsorbed() => SoulsAbsorbed++;
        public static void AddEnemySpared() => EnemiesSpared++;
        public static void AddWanderingSoulBumped() => WanderingSoulsBumped++;

        public static void Reset()
        {
            SoulsAbsorbed = 0;
            EnemiesSpared = 0;
            WanderingSoulsBumped = 0;
        }

        public static void WriteTo(SaveData data)
        {
            data.soulsAbsorbed = SoulsAbsorbed;
            data.enemiesSpared = EnemiesSpared;
            data.wanderingSoulsBumped = WanderingSoulsBumped;
        }

        public static void ReadFrom(SaveData data)
        {
            SoulsAbsorbed = Mathf.Max(0, data.soulsAbsorbed);
            EnemiesSpared = Mathf.Max(0, data.enemiesSpared);
            WanderingSoulsBumped = Mathf.Max(0, data.wanderingSoulsBumped);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
