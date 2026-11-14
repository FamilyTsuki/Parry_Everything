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

        [Test]
        public void Delete_RemovesSave()
        {
            SaveSystem.Save(new SaveData(), path);
            SaveSystem.Delete(path);

            Assert.IsFalse(SaveSystem.HasSave(path));
        }
    }
}
