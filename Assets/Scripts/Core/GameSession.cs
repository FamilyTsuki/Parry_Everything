using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // État de la partie en cours, qui survit au rechargement de la scène (mort, continuer).
    // Rempli depuis la sauvegarde au chargement, recopié dedans à chaque sanctuaire et boss vaincu.
    // Les objets de la scène le lisent au démarrage (boss vaincu : il n'apparaît plus, la zone reste rallumée...).
    public static class GameSession
    {
        private static readonly HashSet<string> bossesDefeated = new HashSet<string>();
        private static readonly List<LimbSave> limbs = new List<LimbSave>();
        private static readonly HashSet<string> flags = new HashSet<string>();

        // Étapes de la progression (noms dans GameFlags)
        public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public static void SetFlag(string flag, bool value = true)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (value) flags.Add(flag);
            else flags.Remove(flag);
        }

        public static Vector3? RespawnPoint { get; set; }
        public static string SanctuaryId { get; set; }
        // Nom du dernier lieu de repos, pour l'écran titre
        public static string PlaceName { get; set; }
        // Membres à remettre sur le joueur au démarrage de la scène
        public static IReadOnlyList<LimbSave> Limbs => limbs;

        public static bool IsBossDefeated(string bossId) => !string.IsNullOrEmpty(bossId) && bossesDefeated.Contains(bossId);

        public static void MarkBossDefeated(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId)) bossesDefeated.Add(bossId);
        }

        public static void SetLimbs(IEnumerable<LimbSave> saves)
        {
            limbs.Clear();
            if (saves != null) limbs.AddRange(saves);
        }

        // Nouvelle partie
        public static void Clear()
        {
            bossesDefeated.Clear();
            limbs.Clear();
            flags.Clear();
            RespawnPoint = null;
            SanctuaryId = null;
            PlaceName = null;
            BehaviorLedger.Reset();
        }

        public static void LoadFrom(SaveData data)
        {
            Clear();
            if (data == null) return;
            RespawnPoint = data.hasRespawnPoint ? data.respawnPoint : (Vector3?)null;
            SanctuaryId = data.sanctuaryId;
            PlaceName = data.placeName;
            if (data.bossesDefeated != null)
            {
                foreach (string id in data.bossesDefeated) MarkBossDefeated(id);
            }
            SetLimbs(data.limbs);
            if (data.flags != null)
            {
                foreach (string flag in data.flags) SetFlag(flag);
            }
            BehaviorLedger.ReadFrom(data);
        }

        public static SaveData ToSaveData()
        {
            var data = new SaveData
            {
                hasRespawnPoint = RespawnPoint.HasValue,
                respawnPoint = RespawnPoint ?? Vector3.zero,
                sanctuaryId = SanctuaryId,
                placeName = PlaceName,
                bossesDefeated = new List<string>(bossesDefeated),
                limbs = new List<LimbSave>(limbs),
                flags = new List<string>(flags),
            };
            BehaviorLedger.WriteTo(data);
            return data;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
