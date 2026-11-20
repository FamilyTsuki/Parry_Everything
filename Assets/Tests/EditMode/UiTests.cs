using System;
using NUnit.Framework;
using ParryEverything.Core;
using ParryEverything.Player;
using ParryEverything.UI;

namespace ParryEverything.Tests
{
    // Textes de l'interface et fondu au noir
    public class UiTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        private static string Ago(TimeSpan delta) => (Now - delta).ToString("o");

        [Test]
        public void SaveLabel_RelativeTimes()
        {
            Assert.AreEqual("à l'instant", SaveLabel.When(Ago(TimeSpan.FromSeconds(20)), Now));
            Assert.AreEqual("il y a 5 min", SaveLabel.When(Ago(TimeSpan.FromMinutes(5.5)), Now));
            Assert.AreEqual("il y a 3 h", SaveLabel.When(Ago(TimeSpan.FromHours(3.2)), Now));
            Assert.AreEqual("hier", SaveLabel.When(Ago(TimeSpan.FromHours(30)), Now));
            StringAssert.StartsWith("le ", SaveLabel.When(Ago(TimeSpan.FromDays(5)), Now));
            Assert.AreEqual(string.Empty, SaveLabel.When("pas une date", Now));
        }

        [Test]
        public void SaveLabel_PlaceAndTime()
        {
            var data = new SaveData { placeName = "Sanctuaire du Gardien", savedAtUtc = Ago(TimeSpan.FromMinutes(2)) };
            Assert.AreEqual("Sanctuaire du Gardien · il y a 2 min", SaveLabel.Describe(data, Now));

            var noPlace = new SaveData { savedAtUtc = null };
            Assert.AreEqual(SaveLabel.NoPlace, SaveLabel.Describe(noPlace, Now));
        }

        // Vie basse : rien au-dessus de la moitié, maximal sous 15 %
        [Test]
        public void Danger_FromLife()
        {
            Assert.AreEqual(0f, DangerFeedback.DangerFromLife(0.8f), 1e-5f);
            Assert.AreEqual(0f, DangerFeedback.DangerFromLife(0.5f), 1e-5f);
            Assert.AreEqual(1f, DangerFeedback.DangerFromLife(0.1f), 1e-5f);
            float mid = DangerFeedback.DangerFromLife(0.325f);
            Assert.AreEqual(0.5f, mid, 1e-4f);
        }

        [Test]
        public void ScreenFade_ReachesTargetAndStops()
        {
            ScreenFade.Set(0f);
            ScreenFade.FadeTo(1f, 1f);
            ScreenFade.Tick(0.5f);
            Assert.AreEqual(0.5f, ScreenFade.Alpha, 1e-4f, "Milieu de la courbe douce");
            Assert.IsFalse(ScreenFade.IsBlack);
            ScreenFade.Tick(0.6f);
            Assert.IsTrue(ScreenFade.IsBlack);
            ScreenFade.Set(0f);
        }
    }
}
