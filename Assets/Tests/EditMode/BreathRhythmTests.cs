using NUnit.Framework;
using ParryEverything.Player;

namespace ParryEverything.Tests
{
    // Respiration : parer chaque inspiration pendant 30 s étouffe le chat ; un souffle pris ou une parade
    // hors rythme remet l'apnée à zéro.
    public class BreathRhythmTests
    {
        private const float Period = 3f;
        private const float Window = 0.3f;
        private const float Dt = 1f / 60f;

        // Avance de 'from' à 'to' en parant au début de chaque inspiration si 'parry'
        private static float Run(BreathRhythm r, float from, float to, bool parry, float offset = 0.1f)
        {
            float next = NextInhale(from, offset);
            for (float t = from; t < to; t += Dt)
            {
                if (parry && t >= next)
                {
                    r.Parry(t);
                    next += Period;
                }
                r.Tick(t, out _);
            }
            return to;
        }

        // Prochain instant « début d'inspiration + offset » à partir de 'from' (cycles calés sur 0)
        private static float NextInhale(float from, float offset)
        {
            float k = (float)System.Math.Ceiling((from - offset) / Period);
            return k * Period + offset;
        }

        [Test]
        public void ParryingEveryBreath_For30s_Suffocates()
        {
            var r = new BreathRhythm(Period, Window, 30f, 0f);
            Run(r, 0f, 29f, true);
            Assert.IsTrue(r.Holding);
            Assert.IsFalse(r.Suffocated);
            Run(r, 29f, 31f, true);
            Assert.IsTrue(r.Suffocated);
        }

        [Test]
        public void ParryJustBeforeInhale_AlsoBlocks()
        {
            var r = new BreathRhythm(Period, Window, 30f, 0f);
            Run(r, 0f, 34f, true, -0.15f);   // l'apnée part du 2e souffle (t = 3)
            Assert.IsTrue(r.Suffocated);
        }

        [Test]
        public void SkippingOneBreath_ResetsApnea()
        {
            var r = new BreathRhythm(Period, Window, 30f, 0f);
            Run(r, 0f, 15f, true);
            Assert.IsTrue(r.Holding);
            Run(r, 15f, 18.5f, false);          // un souffle laissé passer
            Assert.IsFalse(r.Holding);
            Assert.AreEqual(0f, r.Apnea);
        }

        [Test]
        public void OffRhythmParry_ResetsApnea()
        {
            var r = new BreathRhythm(Period, Window, 30f, 0f);
            Run(r, 0f, 10.5f, true);
            Assert.IsTrue(r.Holding);
            r.Parry(10.5f + 1f);                // au milieu d'un souffle
            Assert.IsFalse(r.Holding);
        }

        [Test]
        public void NotParrying_NeverHolds()
        {
            var r = new BreathRhythm(Period, Window, 30f, 0f);
            Run(r, 0f, 60f, false);
            Assert.IsFalse(r.Holding);
            Assert.IsFalse(r.Suffocated);
        }

        [Test]
        public void Exhale_IsReportedOncePerFreeBreath()
        {
            var r = new BreathRhythm(Period, Window, 30f, 0f);
            int exhales = 0;
            for (float t = 0f; t < Period * 4f - 0.01f; t += Dt)
            {
                r.Tick(t, out bool ex);
                if (ex) exhales++;
            }
            Assert.AreEqual(4, exhales);
        }
    }
}
