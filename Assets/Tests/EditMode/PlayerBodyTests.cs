using NUnit.Framework;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Santé du joueur : l'âme se consume, meurt à 0, se relève pleine.
    public class PlayerBodyTests
    {
        private GameObject go;
        private PlayerBody body;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("PlayerBodyTest");
            body = go.AddComponent<PlayerBody>();
            // Awake n'est pas appelé en EditMode : on part d'une âme pleine
            body.Revive();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Damage_ShrinksSoul()
        {
            bool killed = body.ApplyDamage(30f);

            Assert.IsFalse(killed);
            Assert.AreEqual(0.7f, body.SoulRatio, 1e-4f);
            Assert.Less(body.SoulScale, 1f);
        }

        [Test]
        public void LethalDamage_KillsOnce()
        {
            int deaths = 0;
            body.OnDied += () => deaths++;

            Assert.IsTrue(body.ApplyDamage(150f));
            Assert.IsTrue(body.IsDead);
            Assert.IsFalse(body.ApplyDamage(10f), "Un mort ne meurt pas deux fois");
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void Revive_RestoresFullSoul()
        {
            body.ApplyDamage(150f);
            body.Revive();

            Assert.IsFalse(body.IsDead);
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f);
            Assert.AreEqual(1f, body.SoulScale, 1e-4f);
        }
    }
}
