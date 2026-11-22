using NUnit.Framework;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Visions : les souvenirs passent dans l'ordre, un chapitre par nouvelle partie du corps ; les images existent.
    public class MurderVisionTests
    {
        [SetUp]
        public void SetUp() => GameSession.Clear();

        [TearDown]
        public void TearDown() => GameSession.Clear();

        [Test]
        public void Memories_ComeInOrder_ThenRunOut()
        {
            for (int expected = 1; expected < MurderVision.Chapters.Length; expected++)
            {
                Assert.AreEqual(expected, MurderVision.NextMemoryChapter());
                GameSession.SetFlag(GameFlags.Memory(expected));
            }
            Assert.AreEqual(-1, MurderVision.NextMemoryChapter());
        }

        [Test]
        public void QuickPlates_AreMarked()
        {
            Assert.IsTrue(MurderVision.IsQuick("!11a_le_bond"));
            Assert.AreEqual("11a_le_bond", MurderVision.PlateName("!11a_le_bond"));
            Assert.IsFalse(MurderVision.IsQuick("11_le_coup"));
        }

        [Test]
        public void EveryPlate_IsInResources()
        {
            foreach (string[] chapter in MurderVision.Chapters)
            {
                foreach (string plate in chapter)
                {
                    Assert.IsNotNull(Resources.Load<Texture2D>("Visions/" + MurderVision.PlateName(plate)), plate);
                }
            }
        }
    }
}
