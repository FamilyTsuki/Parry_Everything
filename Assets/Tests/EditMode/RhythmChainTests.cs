using NUnit.Framework;
using ParryEverything.Player;

namespace ParryEverything.Tests
{
    // Chaîne de parades en rythme (cœur, pas de course)
    public class RhythmChainTests
    {
        private const float W = 0.1f;

        // Événements toutes les 'period' s ; parade à 'offset' de chaque événement si 'parry'
        private static void Run(RhythmChain c, int events, float period, bool parry, float offset, float start = 0f)
        {
            for (int i = 0; i < events; i++)
            {
                float t = start + i * period;
                if (parry && offset < 0f) { c.Tick(t + offset); c.Parry(t + offset); }
                c.Tick(t);
                c.Event(t);
                if (parry && offset >= 0f) { c.Tick(t + offset); c.Parry(t + offset); }
                c.Tick(t + period * 0.5f);
            }
            c.Tick(start + events * period);
        }

        [Test]
        public void ParryAfterEachEvent_Counts()
        {
            var c = new RhythmChain(W);
            Run(c, 5, 0.4f, true, 0.05f);
            Assert.AreEqual(5, c.Count);
            Assert.AreEqual(0f, c.StartTime, 1e-4f);
        }

        [Test]
        public void ParryBeforeEachEvent_Counts()
        {
            var c = new RhythmChain(W);
            Run(c, 5, 0.4f, true, -0.05f);
            Assert.AreEqual(5, c.Count);
        }

        [Test]
        public void MissedEvent_Breaks()
        {
            var c = new RhythmChain(W);
            Run(c, 4, 0.4f, true, 0.03f);
            Run(c, 1, 0.4f, false, 0f, 1.6f);
            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void ParryFarFromEvents_Breaks()
        {
            var c = new RhythmChain(W);
            Run(c, 4, 1f, true, 0.03f);
            Assert.AreEqual(4, c.Count);
            c.Parry(4.5f);
            c.Tick(4.7f);
            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void Resolved_ReportsEachEvent()
        {
            var c = new RhythmChain(W);
            int parried = 0, missed = 0;
            c.Resolved = p => { if (p) parried++; else missed++; };
            Run(c, 3, 0.5f, true, 0.02f);
            Run(c, 2, 0.5f, false, 0f, 1.5f);
            Assert.AreEqual(3, parried);
            Assert.AreEqual(2, missed);
        }
    }
}
