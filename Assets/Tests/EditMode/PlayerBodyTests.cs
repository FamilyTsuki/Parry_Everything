using NUnit.Framework;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Santé du joueur : l'âme se consume ; le torse (ventre, poitrine, bassin) puis les pattes
    // (cuisse, jarret, pied) prennent les coups à sa place. Une partie arrachée emporte ce qui y est cousu.
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

        // Torse (80 : ventre 25, poitrine 30, bassin 25) + 2 pattes (40 : cuisse 18, jarret 14, pied 8)
        private Limb[] GiveBiped()
        {
            body.GrantFlesh(Limb.Torso(80f));
            Limb[] legs = Limb.CatLegs(40f);
            body.AttachLimbs(legs);
            return legs;
        }

        // legs = Limb.CatLegs : [cuisse, jarret, pied] de la patte proche, puis de la patte loin
        private static Limb Thigh(Limb[] legs, int leg) => legs[leg * 3];
        private static Limb Shin(Limb[] legs, int leg) => legs[leg * 3 + 1];
        private static Limb Foot(Limb[] legs, int leg) => legs[leg * 3 + 2];

        private Limb Find(LimbKind kind) => ((System.Collections.Generic.List<Limb>)body.Limbs).Find(l => l.Kind == kind);

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
            body.GrantFlesh(Limb.Torso(80f));
            Assert.AreEqual(PlayerForm.Ball, body.Form);
            Assert.AreEqual(3, body.Limbs.Count, "Ventre, poitrine, bassin");
            body.AttachLimbs(Limb.CatLegs(40f));
            Assert.AreEqual(PlayerForm.Biped, body.Form);
            Assert.AreEqual(2, body.IntactLegCount);
        }

        [Test]
        public void TorsoAndLegs_LifeIsShared()
        {
            Limb[] legs = GiveBiped();
            Assert.AreEqual(25f, body.FleshLimb.MaxLife, 1e-4f);
            Assert.AreEqual(30f, Find(LimbKind.Chest).MaxLife, 1e-4f);
            Assert.AreEqual(25f, Find(LimbKind.Pelvis).MaxLife, 1e-4f);
            Assert.AreEqual(18f, Thigh(legs, 0).MaxLife, 1e-4f);
            Assert.AreEqual(14f, Shin(legs, 0).MaxLife, 1e-4f);
            Assert.AreEqual(8f, Foot(legs, 0).MaxLife, 1e-4f);
        }

        [Test]
        public void Legs_NeedFlesh()
        {
            Assert.IsFalse(body.AttachLimbs(Limb.CatLegs(40f)), "Sans chair, une patte ne tient pas");
            Assert.AreEqual(PlayerForm.Soul, body.Form);
        }

        [Test]
        public void Legs_NeedPelvis()
        {
            body.GrantFlesh(Limb.Flesh(25f));   // le ventre seul
            Assert.IsFalse(body.AttachLimbs(Limb.CatLegs(40f)), "Sans bassin, les cuisses ne tiennent pas");
            Assert.AreEqual(PlayerForm.Ball, body.Form);
        }

        [Test]
        public void Ball_TakesHitsInsteadOfSoul()
        {
            body.GrantFlesh(Limb.Torso(80f));

            Assert.IsFalse(body.ApplyDamage(10f));
            Assert.AreEqual(15f, body.FleshLimb.Life, 1e-4f, "Sans cible, le ventre prend le coup");
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f, "L'âme est protégée par la chair");
        }

        [Test]
        public void LosingBothLegs_BackToBall()
        {
            Limb[] legs = GiveBiped();
            body.TearLimb(Thigh(legs, 0));
            Assert.AreEqual(PlayerForm.Biped, body.Form, "Une patte : toujours debout (il sautille)");

            body.TearLimb(Thigh(legs, 1));
            Assert.AreEqual(PlayerForm.Ball, body.Form);
        }

        // ---------- Parties qui emportent ce qui y est cousu ----------

        [Test]
        public void ThighTorn_ShinAndFootFallWithIt()
        {
            Limb[] legs = GiveBiped();
            int lost = 0;
            body.OnLimbLost += _ => lost++;

            body.ApplyDamage(18f, Thigh(legs, 0));

            Assert.AreEqual(3, lost, "Cuisse, jarret et pied");
            Assert.IsTrue(Shin(legs, 0).IsTorn);
            Assert.IsTrue(Foot(legs, 0).IsTorn);
            Assert.IsFalse(Thigh(legs, 1).IsTorn, "L'autre patte tient");
            Assert.AreEqual(1, body.IntactLegCount);
        }

        [Test]
        public void ShinTorn_LegNoLongerStands()
        {
            Limb[] legs = GiveBiped();
            body.TearLimb(Shin(legs, 0));

            Assert.IsTrue(Foot(legs, 0).IsTorn, "Le pied tombe avec le jarret");
            Assert.IsFalse(Thigh(legs, 0).IsTorn, "La cuisse reste cousue");
            Assert.AreEqual(1, body.IntactLegCount, "Une cuisse seule ne porte pas le chat");
        }

        [Test]
        public void FootTorn_StillStandsButLimps()
        {
            Limb[] legs = GiveBiped();
            body.TearLimb(Foot(legs, 0));

            Assert.AreEqual(2, body.IntactLegCount);
            Assert.AreEqual(1, body.MissingFootCount);
            Assert.AreEqual(PlayerForm.Biped, body.Form);
        }

        // Torse + pattes + 2 bras + tête + queue
        private Limb[] GiveFullCat()
        {
            Limb[] legs = GiveBiped();
            body.AttachLimbs(Limb.CatArms(30f));
            body.AttachLimb(Limb.CatHead(30f));
            body.AttachLimb(Limb.CatTail(30f));
            return legs;
        }

        [Test]
        public void PelvisTorn_DropsLegsAndTail()
        {
            GiveFullCat();
            body.TearLimb(Find(LimbKind.Pelvis));

            Assert.AreEqual(PlayerForm.Ball, body.Form);
            Assert.IsTrue(Find(LimbKind.Tail).IsTorn);
            Assert.IsFalse(Find(LimbKind.Head).IsTorn, "La tête est sur la poitrine");
            foreach (Limb limb in body.Limbs)
            {
                if (limb.Kind == LimbKind.Leg || limb.Kind == LimbKind.Shin || limb.Kind == LimbKind.Foot) Assert.IsTrue(limb.IsTorn);
            }
        }

        [Test]
        public void ChestTorn_DropsHeadAndArms()
        {
            GiveFullCat();
            body.TearLimb(Find(LimbKind.Chest));

            Assert.IsTrue(Find(LimbKind.Head).IsTorn);
            Assert.IsTrue(Find(LimbKind.Arm).IsTorn);
            Assert.IsFalse(Find(LimbKind.Tail).IsTorn, "La queue est sur le bassin");
            Assert.AreEqual(PlayerForm.Biped, body.Form, "Les pattes tiennent au bassin");
        }

        [Test]
        public void BellyBursts_EverythingFallsAndBackToSoul()
        {
            GiveBiped();
            int lost = 0;
            body.OnLimbLost += _ => lost++;

            body.ApplyDamage(500f, body.FleshLimb);

            Assert.AreEqual(PlayerForm.Soul, body.Form);
            Assert.AreEqual(9, lost, "Les 3 parties du torse et les 6 des pattes tombent");
            Assert.AreEqual(1f, body.SoulRatio, 1e-4f, "Le surplus ne passe pas à l'âme");

            body.ApplyDamage(30f);
            Assert.AreEqual(0.7f, body.SoulRatio, 1e-4f, "Sans corps, l'âme reprend les coups");
        }

        [Test]
        public void NearestLimb_FeetLowBellyMiddle()
        {
            GiveBiped();
            Vector3 feet = body.transform.position + new Vector3(0.3f, -0.45f, -0.2f);
            Vector3 middle = body.transform.position + new Vector3(0f, 0.6f, 0f);

            Assert.AreEqual(LimbKind.Foot, body.NearestLimb(feet, 1f).Kind);
            Assert.AreEqual(LimbKind.Flesh, body.NearestLimb(middle, 1f).Kind, "Debout, le ventre est en hauteur");
        }

        // ---------- Recouture ----------

        [Test]
        public void TornLeg_SewnPartByPart()
        {
            Limb[] legs = GiveBiped();
            body.ApplyDamage(18f, Thigh(legs, 0));
            Assert.IsFalse(body.Sew(Thigh(legs, 0)), "Pas recousu tant qu'il n'est pas ramassé");

            body.PickUp(Foot(legs, 0));
            Assert.IsFalse(body.Sew(Foot(legs, 0)), "Le pied attend son jarret");

            body.PickUp(Thigh(legs, 0));
            body.PickUp(Shin(legs, 0));
            Assert.AreEqual(3, body.PocketCount);
            Assert.IsTrue(body.SewNext());
            Assert.IsTrue(body.SewNext());
            Assert.IsTrue(body.SewNext());
            Assert.AreEqual(2, body.IntactLegCount);
            Assert.AreEqual(0, body.MissingFootCount);
            Assert.AreEqual(18f, Thigh(legs, 0).Life, 1e-4f);
        }

        [Test]
        public void SewNext_BellyFirst()
        {
            GiveBiped();
            body.ApplyDamage(500f, body.FleshLimb);
            foreach (Limb limb in body.Limbs) body.PickUp(limb);

            Assert.IsTrue(body.SewNext());
            Assert.AreEqual(PlayerForm.Ball, body.Form, "Le ventre d'abord");
            while (body.SewNext()) { }
            Assert.AreEqual(PlayerForm.Biped, body.Form);
            Assert.AreEqual(9, body.IntactLimbCount);
        }

        [Test]
        public void RestoreLimbs_HealsAndClearsEffect()
        {
            body.GrantFlesh(Limb.Torso(80f));
            body.FleshLimb.Effect = LimbEffect.Bleeding;
            body.ApplyDamage(20f);

            body.RestoreLimbs();

            Assert.AreEqual(25f, body.FleshLimb.Life, 1e-4f);
            Assert.AreEqual(LimbEffect.None, body.FleshLimb.Effect);
        }

        [Test]
        public void Sanctuary_SewsPocketAndForgetsLostLimbs()
        {
            Limb[] legs = GiveBiped();
            body.TearLimb(Thigh(legs, 0));
            body.TearLimb(Thigh(legs, 1));
            body.PickUp(Thigh(legs, 0));    // patte proche ramassée en entier ; la patte loin reste au sol
            body.PickUp(Shin(legs, 0));
            body.PickUp(Foot(legs, 0));
            body.ApplyDamage(10f, body.FleshLimb);

            body.RestAtSanctuary();

            Assert.AreEqual(6, body.Limbs.Count, "Torse + patte proche");
            Assert.AreEqual(1, body.IntactLegCount);
            Assert.AreEqual(25f, body.FleshLimb.Life, 1e-4f);
        }

        [Test]
        public void SaveLimbs_RoundTrip()
        {
            GiveBiped();
            body.ApplyDamage(15f, body.Limbs[1]);   // la poitrine

            var saved = body.SaveLimbs();
            body.RemoveAllLimbs();
            body.LoadLimbs(saved);

            Assert.AreEqual(9, body.Limbs.Count);
            Assert.AreEqual(PlayerForm.Biped, body.Form);
            Assert.AreEqual(15f, body.Limbs[1].Life, 1e-4f);
            Assert.AreEqual(body.Limbs[1].Anchor, saved[1].anchor);
            Assert.AreEqual(Limb.BellyId, body.Limbs[1].ParentId, "Le porteur est gardé");
        }

        // ---------- Chat complet (bras, tête, queue) ----------

        [Test]
        public void ArmsHeadTail_NeedChestAndPelvis()
        {
            Assert.IsFalse(body.AttachLimb(Limb.CatHead(30f)), "Sans chair, la tête ne tient pas");
            body.GrantFlesh(Limb.Flesh(25f));
            Assert.IsFalse(body.AttachLimbs(Limb.CatArms(30f)), "Sans poitrine, les bras ne tiennent pas");

            body.RemoveAllLimbs();
            GiveFullCat();
            Assert.AreEqual(17, body.Limbs.Count);
            Assert.AreEqual(PlayerForm.Biped, body.Form);
        }

        [Test]
        public void BellyBurst_DropsEverySewnLimb()
        {
            GiveFullCat();
            int lost = 0;
            body.OnLimbLost += _ => lost++;

            body.ApplyDamage(500f, body.FleshLimb);

            Assert.AreEqual(PlayerForm.Soul, body.Form);
            Assert.AreEqual(17, lost, "Tout le corps tombe");
            foreach (Limb limb in body.Limbs) Assert.IsTrue(limb.IsTorn);
        }

        [Test]
        public void NearestLimb_HeadOnTop()
        {
            GiveFullCat();
            Vector3 top = body.transform.position + new Vector3(0.1f, 1.5f, 0f);
            Assert.AreEqual(LimbKind.Head, body.NearestLimb(top, 1f).Kind);
        }

        [Test]
        public void SaveLoad_KeepsNewLimbKinds()
        {
            GiveFullCat();
            var saved = body.SaveLimbs();
            body.RemoveAllLimbs();
            body.LoadLimbs(saved);

            Assert.AreEqual(17, body.Limbs.Count);
            Assert.AreEqual(LimbKind.Tail, body.Limbs[16].Kind);
            Assert.AreEqual(LimbKind.Head, body.Limbs[15].Kind);
            Assert.AreEqual(LimbKind.Hand, body.Limbs[11].Kind);
            Assert.AreEqual(LimbKind.Foot, body.Limbs[5].Kind);
        }

        [Test]
        public void ArmTorn_DropsForearmAndHand()
        {
            GiveFullCat();
            Limb upper = Find(LimbKind.Arm);
            int lost = 0;
            body.OnLimbLost += _ => lost++;

            body.ApplyDamage(500f, upper);

            Assert.AreEqual(3, lost, "Haut du bras, avant-bras et main");
            var limbs = (System.Collections.Generic.List<Limb>)body.Limbs;
            Assert.IsTrue(limbs.Find(l => l.Id == "avant-bras proche").IsTorn);
            Assert.IsTrue(limbs.Find(l => l.Id == "main proche").IsTorn);
            Assert.IsFalse(limbs.Find(l => l.Id == "bras loin").IsTorn, "L'autre bras reste");
        }

        [Test]
        public void HandTorn_KeepsForearm()
        {
            GiveFullCat();
            body.ApplyDamage(500f, Find(LimbKind.Hand));

            Assert.IsTrue(Find(LimbKind.Hand).IsTorn);
            Assert.IsFalse(Find(LimbKind.Forearm).IsTorn);
            Assert.IsFalse(Find(LimbKind.Arm).IsTorn);
        }
    }
}
