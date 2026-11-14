using NUnit.Framework;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Santé du joueur : l'âme se consume ; la chair puis les pattes prennent les coups à sa place.
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

        // Boule de chair (vie 80) + 2 pattes (vie 40)
        private Limb[] GiveBiped()
        {
            body.GrantFlesh(Limb.Flesh(80f));
            Limb[] legs = Limb.CatLegs(40f);
            body.AttachLimbs(legs);
            return legs;
        }

        // ---------- Âme ----------

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
        public void Heal_CapsAtMax()
        {
            body.ApplyDamage(50f);
            body.Heal(20f);
            Assert.AreEqual(0.7f, body.SoulRatio, 1e-4f);

            body.Heal(1000f);
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f);
        }

        [Test]
        public void Invulnerable_IgnoresDamage()
        {
            body.Invulnerable = true;

            Assert.IsFalse(body.ApplyDamage(500f));
            Assert.IsFalse(body.IsDead);
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f);
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

        // ---------- Formes ----------

        [Test]
        public void Forms_SoulThenBallThenBiped()
        {
            Assert.AreEqual(PlayerForm.Soul, body.Form);
            body.GrantFlesh(Limb.Flesh(80f));
            Assert.AreEqual(PlayerForm.Ball, body.Form);
            body.AttachLimbs(Limb.CatLegs(40f));
            Assert.AreEqual(PlayerForm.Biped, body.Form);
            Assert.AreEqual(2, body.IntactLegCount);
        }

        [Test]
        public void Legs_NeedFlesh()
        {
            Assert.IsFalse(body.AttachLimbs(Limb.CatLegs(40f)), "Sans chair, une patte ne tient pas");
            Assert.AreEqual(PlayerForm.Soul, body.Form);
        }

        [Test]
        public void Ball_TakesHitsInsteadOfSoul()
        {
            body.GrantFlesh(Limb.Flesh(80f));

            Assert.IsFalse(body.ApplyDamage(30f));
            Assert.AreEqual(50f, body.FleshLimb.Life, 1e-4f);
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f, "L'âme est protégée par la chair");
        }

        [Test]
        public void LosingBothLegs_BackToBall()
        {
            Limb[] legs = GiveBiped();
            body.TearLimb(legs[0]);
            Assert.AreEqual(PlayerForm.Biped, body.Form, "Une patte : toujours debout (il sautille)");

            body.TearLimb(legs[1]);
            Assert.AreEqual(PlayerForm.Ball, body.Form);
        }

        [Test]
        public void FleshBursts_LegsFallAndBackToSoul()
        {
            GiveBiped();
            int lost = 0;
            body.OnLimbLost += _ => lost++;

            body.ApplyDamage(500f, body.FleshLimb);

            Assert.AreEqual(PlayerForm.Soul, body.Form);
            Assert.AreEqual(3, lost, "La chair et ses 2 pattes tombent");
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f, "Le surplus ne passe pas à l'âme");

            body.ApplyDamage(30f);
            Assert.AreEqual(0.7f, body.SoulRatio, 1e-4f, "Sans corps, l'âme reprend les coups");
        }

        [Test]
        public void NearestLimb_LegsLowFleshHigh()
        {
            GiveBiped();
            Vector3 feet = body.transform.position + new Vector3(0f, -0.5f, 0f);
            Vector3 top = body.transform.position + new Vector3(0f, 0.6f, 0f);

            Assert.AreEqual(LimbKind.Leg, body.NearestLimb(feet, 1f).Kind);
            Assert.AreEqual(LimbKind.Flesh, body.NearestLimb(top, 1f).Kind, "Debout, la chair est en hauteur");
        }

        // ---------- Recouture ----------

        [Test]
        public void TornLeg_PickUpThenSew()
        {
            Limb[] legs = GiveBiped();
            body.ApplyDamage(40f, legs[0]);
            Assert.IsFalse(body.Sew(legs[0]), "Pas recousu tant qu'il n'est pas ramassé");

            body.PickUp(legs[0]);
            Assert.AreEqual(1, body.PocketCount);
            Assert.IsTrue(body.SewNext());
            Assert.AreEqual(2, body.IntactLegCount);
            Assert.AreEqual(40f, legs[0].Life, 1e-4f);
        }

        [Test]
        public void SewNext_FleshBeforeLegs()
        {
            Limb[] legs = GiveBiped();
            Limb flesh = body.FleshLimb;
            body.ApplyDamage(500f, flesh);
            body.PickUp(legs[0]);
            Assert.IsFalse(body.Sew(legs[0]), "Une patte ne se coud pas sur une âme");

            body.PickUp(flesh);
            Assert.IsTrue(body.SewNext());
            Assert.AreEqual(PlayerForm.Ball, body.Form, "La chair d'abord");
            Assert.IsTrue(body.SewNext());
            Assert.AreEqual(PlayerForm.Biped, body.Form);
        }

        [Test]
        public void RestoreLimbs_HealsAndClearsEffect()
        {
            body.GrantFlesh(Limb.Flesh(80f));
            body.FleshLimb.Effect = LimbEffect.Bleeding;
            body.ApplyDamage(20f);

            body.RestoreLimbs();

            Assert.AreEqual(80f, body.FleshLimb.Life, 1e-4f);
            Assert.AreEqual(LimbEffect.None, body.FleshLimb.Effect);
        }

        [Test]
        public void Sanctuary_SewsPocketAndForgetsLostLimbs()
        {
            Limb[] legs = GiveBiped();
            body.TearLimb(legs[0]);
            body.TearLimb(legs[1]);
            body.PickUp(legs[0]);           // legs[1] reste au sol
            body.ApplyDamage(10f, body.FleshLimb);

            body.RestAtSanctuary();

            Assert.AreEqual(2, body.Limbs.Count);
            Assert.AreEqual(1, body.IntactLegCount);
            Assert.AreEqual(80f, body.FleshLimb.Life, 1e-4f);
        }

        [Test]
        public void SaveLimbs_RoundTrip()
        {
            GiveBiped();
            body.ApplyDamage(15f, body.Limbs[1]);

            var saved = body.SaveLimbs();
            body.RemoveAllLimbs();
            body.LoadLimbs(saved);

            Assert.AreEqual(3, body.Limbs.Count);
            Assert.AreEqual(PlayerForm.Biped, body.Form);
            Assert.AreEqual(25f, body.Limbs[1].Life, 1e-4f);
            Assert.AreEqual(body.Limbs[1].Anchor, saved[1].anchor);
        }
    }
}
