using NUnit.Framework;
using ParryEverything.Player;

namespace ParryEverything.Tests
{
    // Parades parfaites enchaînées : délai max entre deux, plafond, remise à zéro.
    public class PerfectParryChainTests
    {
        [Test]
        public void ThreeQuickPerfects_ReachMax()
        {
            var chain = new PerfectParryChain(2f, 3);

            Assert.AreEqual(1, chain.Register(0f));
            Assert.AreEqual(2, chain.Register(1.5f));
            Assert.AreEqual(3, chain.Register(3f));
            Assert.AreEqual(3, chain.Register(4f), "Plafonné");
        }

        [Test]
        public void TooSlow_RestartsAtOne()
        {
            var chain = new PerfectParryChain(2f, 3);
            chain.Register(0f);
            chain.Register(1f);

            Assert.AreEqual(1, chain.Register(3.5f));
        }

        [Test]
        public void Tick_DropsAfterGap()
        {
            var chain = new PerfectParryChain(2f, 3);
            chain.Register(0f);

            Assert.AreEqual(1, chain.Tick(1.9f));
            Assert.AreEqual(0, chain.Tick(2.1f));
        }

        [Test]
        public void Reset_ClearsLevel()
        {
            var chain = new PerfectParryChain();
            chain.Register(0f);
            chain.Register(0.5f);
            chain.Reset();

            Assert.AreEqual(0, chain.Level);
            Assert.AreEqual(1, chain.Register(0.6f));
        }
    }
}
