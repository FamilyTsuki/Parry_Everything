using System.IO;
using NUnit.Framework;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Sauvegarde : aller-retour fichier, versions, fichiers absents ou abîmés.
    public class SaveSystemTests
    {
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), "ParryEverythingTests", Path.GetRandomFileName(), SaveSystem.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            string dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // Version 1 : chair et pattes entières → version 2 : 3 parties chacune, même part de vie
        [Test]
        public void MigrateV1_SplitsTorsoAndLegs()
        {
            var old = new SaveData { version = 1 };
            old.limbs.Add(new LimbSave { kind = 0, id = "chair du Gardien", maxLife = 80f, life = 40f });
            old.limbs.Add(new LimbSave { kind = 1, id = "patte proche", maxLife = 40f, life = 40f, anchor = new Vector3(0.2f, -0.05f, -0.2f) });
            old.limbs.Add(new LimbSave { kind = 3, id = "tête", maxLife = 30f, life = 30f });
            old.limbs.Add(new LimbSave { kind = 4, id = "queue", maxLife = 30f, life = 30f });

            SaveData data = SaveSystem.FromJson(JsonUtility.ToJson(old));

            Assert.AreEqual(SaveSystem.CurrentVersion, data.version);
            Assert.AreEqual(8, data.limbs.Count, "Ventre, poitrine, bassin, cuisse, jarret, pied, tête, queue");
            LimbSave belly = data.limbs[0];
            Assert.AreEqual(25f, belly.maxLife, 1e-4f);
            Assert.AreEqual(12.5f, belly.life, 1e-4f, "Moitié de vie gardée");
            Assert.AreEqual("bassin", data.limbs[3].parentId, "La cuisse est cousue sur le bassin");
            Assert.AreEqual(8, data.limbs[5].kind, "Le pied");
            Assert.AreEqual("jarret proche", data.limbs[5].parentId);
            Assert.AreEqual("poitrine", data.limbs[6].parentId, "La tête est cousue sur la poitrine");
            Assert.AreEqual("bassin", data.limbs[7].parentId, "La queue est cousue sur le bassin");
        }

        // Version 2 : bras entiers → version 3 : haut du bras, avant-bras, main (14 / 10 / 6 pour 30)
        [Test]
        public void MigrateV2_SplitsArms()
        {
            var old = new SaveData { version = 2 };
            old.limbs.Add(new LimbSave { kind = 0, id = "ventre", maxLife = 25f, life = 25f });
            old.limbs.Add(new LimbSave { kind = 5, id = "poitrine", parentId = "ventre", maxLife = 30f, life = 30f });
            old.limbs.Add(new LimbSave { kind = 2, id = "bras loin", parentId = "poitrine", maxLife = 30f, life = 15f, effect = 1,
                                         anchor = new Vector3(0.05f, 1f, 0.18f) });

            SaveData data = SaveSystem.FromJson(JsonUtility.ToJson(old));

            Assert.AreEqual(SaveSystem.CurrentVersion, data.version);
            Assert.AreEqual(5, data.limbs.Count, "Ventre, poitrine, haut du bras, avant-bras, main");
            Assert.AreEqual("bras loin", data.limbs[2].id);
            Assert.AreEqual(14f, data.limbs[2].maxLife, 1e-4f);
            Assert.AreEqual(7f, data.limbs[2].life, 1e-4f, "Moitié de vie gardée");
            Assert.AreEqual(9, data.limbs[3].kind, "L'avant-bras");
            Assert.AreEqual("bras loin", data.limbs[3].parentId);
            Assert.AreEqual(10, data.limbs[4].kind, "La main");
            Assert.AreEqual("avant-bras loin", data.limbs[4].parentId);
            Assert.AreEqual(1, data.limbs[4].effect, "L'effet est gardé");
        }

        [Test]
        public void SaveThenLoad_KeepsData()
        {
            SaveSystem.Save(new SaveData { hasRespawnPoint = true, respawnPoint = new Vector3(3f, 1f, 0f) }, path);
            SaveData loaded = SaveSystem.Load(path);

            Assert.IsTrue(SaveSystem.HasSave(path));
            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveSystem.CurrentVersion, loaded.version);
            Assert.IsTrue(loaded.hasRespawnPoint);
            Assert.AreEqual(new Vector3(3f, 1f, 0f), loaded.respawnPoint);
            Assert.IsFalse(string.IsNullOrEmpty(loaded.savedAtUtc));
        }

        [Test]
        public void SaveTwice_Overwrites()
        {
            SaveSystem.Save(new SaveData { respawnPoint = Vector3.one }, path);
            SaveSystem.Save(new SaveData { respawnPoint = Vector3.up }, path);

            Assert.AreEqual(Vector3.up, SaveSystem.Load(path).respawnPoint);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Load_MissingFile_ReturnsNull()
        {
            Assert.IsFalse(SaveSystem.HasSave(path));
            Assert.IsNull(SaveSystem.Load(path));
        }

        [Test]
        public void Load_CorruptFile_ReturnsNull()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{ pas du json");

            Assert.IsNull(SaveSystem.Load(path));
        }

        [Test]
        public void FromJson_NewerVersion_Ignored()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ignorée"));
            string json = "{\"version\":" + (SaveSystem.CurrentVersion + 1) + "}";

            Assert.IsNull(SaveSystem.FromJson(json));
        }

        [Test]
        public void FromJson_MissingNewField_GetsDefault()
        {
            // Sauvegarde écrite avant l'ajout du point de réapparition
            SaveData data = SaveSystem.FromJson("{\"version\":1}");

            Assert.IsNotNull(data);
            Assert.IsFalse(data.hasRespawnPoint);
        }

        // Sauvegarde abîmée (arrêt brutal, disque) : la copie précédente est relue
        [Test]
        public void Load_CorruptFile_FallsBackToBackup()
        {
            SaveSystem.Save(new SaveData { placeName = "ancien" }, path);
            SaveSystem.Save(new SaveData { placeName = "récent" }, path);
            File.WriteAllText(path, "{ pas du json");

            SaveData loaded = SaveSystem.Load(path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("ancien", loaded.placeName);
        }

        [Test]
        public void Delete_RemovesBackupToo()
        {
            SaveSystem.Save(new SaveData(), path);
            SaveSystem.Save(new SaveData(), path);
            Assert.IsTrue(File.Exists(SaveSystem.BackupPath(path)));

            SaveSystem.Delete(path);

            Assert.IsFalse(SaveSystem.HasSave(path));
            Assert.IsFalse(File.Exists(SaveSystem.BackupPath(path)));
        }

        [Test]
        public void Delete_RemovesSave()
        {
            SaveSystem.Save(new SaveData(), path);
            SaveSystem.Delete(path);

            Assert.IsFalse(SaveSystem.HasSave(path));
        }
    }
}
