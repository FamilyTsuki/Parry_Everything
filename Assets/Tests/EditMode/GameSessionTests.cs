using NUnit.Framework;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Session : aller-retour avec la sauvegarde (boss vaincus, membres, comportement).
    public class GameSessionTests
    {
        [TearDown]
        public void TearDown()
        {
            GameSession.Clear();
        }

        [Test]
        public void SaveData_RoundTrip()
        {
            GameSession.Clear();
            GameSession.RespawnPoint = new Vector3(2f, 1f, 0f);
            GameSession.SanctuaryId = "zone1";
            GameSession.MarkBossDefeated("HellGuardian");
            GameSession.SetFlag(GameFlags.GuardianChunkTaken);
            GameSession.SetLimbs(new[] { new LimbSave { id = "patte", maxLife = 40f, life = 12f } });
            BehaviorLedger.AddSoulAbsorbed();
            BehaviorLedger.AddEnemySpared();
            BehaviorLedger.AddEnemySpared();

            SaveData data = SaveSystem.FromJson(SaveSystem.ToJson(Stamp(GameSession.ToSaveData())));
            GameSession.Clear();
            Assert.IsFalse(GameSession.IsBossDefeated("HellGuardian"));

            GameSession.LoadFrom(data);

            Assert.AreEqual(new Vector3(2f, 1f, 0f), GameSession.RespawnPoint);
            Assert.AreEqual("zone1", GameSession.SanctuaryId);
            Assert.IsTrue(GameSession.IsBossDefeated("HellGuardian"));
            Assert.IsTrue(GameSession.HasFlag(GameFlags.GuardianChunkTaken));
            Assert.IsFalse(GameSession.HasFlag(GameFlags.MerchantLegsMade));
            Assert.AreEqual(1, GameSession.Limbs.Count);
            Assert.AreEqual(12f, GameSession.Limbs[0].life, 1e-4f);
            Assert.AreEqual(1, BehaviorLedger.SoulsAbsorbed);
            Assert.AreEqual(2, BehaviorLedger.EnemiesSpared);
        }

        [Test]
        public void Clear_ResetsLedger()
        {
            BehaviorLedger.AddWanderingSoulBumped();
            GameSession.Clear();

            Assert.AreEqual(0, BehaviorLedger.WanderingSoulsBumped);
            Assert.IsNull(GameSession.RespawnPoint);
        }

        // SaveSystem.Save remplit la version ; ici on reste en mémoire
        private static SaveData Stamp(SaveData data)
        {
            data.version = SaveSystem.CurrentVersion;
            return data;
        }
    }
}
