using System;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Un membre du corps : une partie du torse (ventre, poitrine, bassin) ou une partie cousue dessus
    // (cuisse, jarret, pied, bras, tête, queue). Sa propre vie, un effet possible. Arraché quand sa vie tombe à 0.
    // Chaque membre est cousu sur un autre (ParentId, voir LimbKind) : arraché, il emporte ce qui y est cousu.
    // Un membre arraché reste au sol (LimbPickup) ; ramassé, il est « en poche » jusqu'à la recouture.
    [Serializable]
    public class Limb
    {
        // Ids des parties du torse : les autres membres s'y cousent
        public const string BellyId = "ventre";
        public const string ChestId = "poitrine";
        public const string PelvisId = "bassin";

        // Partage de la vie du torse (chair du Gardien) et d'une patte entre leurs 3 parties
        public const float BellyShare = 25f / 80f, ChestShare = 30f / 80f, PelvisShare = 25f / 80f;
        public const float ThighShare = 18f / 40f, ShinShare = 14f / 40f, FootShare = 8f / 40f;
        public const float UpperArmShare = 14f / 30f, ForearmShare = 10f / 30f, HandShare = 6f / 30f;

        [SerializeField] private LimbKind kind = LimbKind.Leg;
        [SerializeField] private string id;           // ex : "cuisse proche" ; unique dans le corps
        [SerializeField] private string parentId;     // membre sur lequel il est cousu (vide : le ventre, cœur du corps)
        [SerializeField] private float maxLife = 40f;
        [SerializeField] private float life = 40f;
        [SerializeField] private LimbEffect effect = LimbEffect.None;
        // Point d'attache sur le corps, côté droit (x : avant, y : haut, z : profondeur) ; retourné si le chat regarde à gauche
        [SerializeField] private Vector3 anchor;
        [SerializeField] private bool pocketed;        // arraché puis ramassé : prêt à être recousu

        public LimbKind Kind => kind;
        public string Id => id;
        public string ParentId => parentId;
        public float MaxLife => maxLife;
        public float Life => life;
        public float LifeRatio => maxLife > 0f ? Mathf.Clamp01(life / maxLife) : 0f;
        public bool IsTorn => life <= 0f;
        public bool IsPocketed => pocketed && IsTorn;
        public Vector3 Anchor => anchor;
        // Partie du torse (chair) : au sol, c'est un morceau de chair, pas un membre
        public bool IsTorsoPart => kind == LimbKind.Flesh || kind == LimbKind.Chest || kind == LimbKind.Pelvis;
        public LimbEffect Effect
        {
            get => effect;
            set => effect = value;
        }

        public Limb(string id, float maxLife, Vector3 anchor = default, LimbKind kind = LimbKind.Leg, string parentId = null)
        {
            this.kind = kind;
            this.id = id;
            this.parentId = parentId;
            this.maxLife = Mathf.Max(1f, maxLife);
            life = this.maxLife;
            this.anchor = anchor;
        }

        // Le ventre seul : la chair du Gardien agglutinée autour de l'âme (cœur du torse)
        public static Limb Flesh(float life)
        {
            return new Limb(BellyId, life, new Vector3(0f, 0.6f, 0f), LimbKind.Flesh);
        }

        // Le torse entier, chair du Gardien : ventre (autour de l'âme), poitrine et bassin cousus dessus.
        // totalLife est partagé entre les 3 parties (25 / 30 / 25 pour 80).
        public static Limb[] Torso(float totalLife)
        {
            return new[]
            {
                Flesh(totalLife * BellyShare),
                new Limb(ChestId, totalLife * ChestShare, new Vector3(0.08f, 0.95f, 0f), LimbKind.Chest, BellyId),
                new Limb(PelvisId, totalLife * PelvisShare, new Vector3(-0.05f, 0.3f, 0f), LimbKind.Pelvis, BellyId),
            };
        }

        // Les 2 grandes pattes fabriquées par l'âme marchande (le chat se tient debout), cousues sur le bassin.
        // Chacune : cuisse → jarret → pied ; legLife est partagé entre les 3 (18 / 14 / 8 pour 40).
        public static Limb[] CatLegs(float legLife)
        {
            var list = new System.Collections.Generic.List<Limb>();
            // Écartées sur les côtés : l'une vers l'avant (côté caméra), l'autre vers l'arrière
            AddLeg(list, "proche", legLife, 0.2f, -0.2f);
            AddLeg(list, "loin", legLife, -0.2f, 0.2f);
            return list.ToArray();
        }

        private static void AddLeg(System.Collections.Generic.List<Limb> list, string side, float legLife, float x, float z)
        {
            string thigh = "cuisse " + side, shin = "jarret " + side;
            list.Add(new Limb(thigh, legLife * ThighShare, new Vector3(x, 0.1f, z), LimbKind.Leg, PelvisId));
            list.Add(new Limb(shin, legLife * ShinShare, new Vector3(x + 0.04f, -0.2f, z), LimbKind.Shin, thigh));
            list.Add(new Limb("pied " + side, legLife * FootShare, new Vector3(x + 0.1f, -0.42f, z), LimbKind.Foot, shin));
        }

        // Les 2 bras, cousus en haut de la poitrine (devant et derrière, comme les pattes).
        // Chacun : haut du bras → avant-bras → main ; life est partagé entre les 3 (14 / 10 / 6 pour 30).
        public static Limb[] CatArms(float life)
        {
            var list = new System.Collections.Generic.List<Limb>();
            AddArm(list, "proche", life, 0.15f, -0.18f);
            AddArm(list, "loin", life, 0.05f, 0.18f);
            return list.ToArray();
        }

        private static void AddArm(System.Collections.Generic.List<Limb> list, string side, float life, float x, float z)
        {
            string upper = "bras " + side, fore = "avant-bras " + side;
            list.Add(new Limb(upper, life * UpperArmShare, new Vector3(x, 1.0f, z), LimbKind.Arm, ChestId));
            list.Add(new Limb(fore, life * ForearmShare, new Vector3(x + 0.02f, 0.7f, z), LimbKind.Forearm, upper));
            list.Add(new Limb("main " + side, life * HandShare, new Vector3(x + 0.05f, 0.45f, z), LimbKind.Hand, fore));
        }

        // La tête, au sommet de la poitrine
        public static Limb CatHead(float life)
        {
            return new Limb("tête", life, new Vector3(0.12f, 1.45f, 0f), LimbKind.Head, ChestId);
        }

        // La queue, cousue en bas du dos, sur le bassin
        public static Limb CatTail(float life)
        {
            return new Limb("queue", life, new Vector3(-0.35f, 0.45f, 0f), LimbKind.Tail, PelvisId);
        }

        // Position du point d'attache dans le monde
        public Vector3 WorldAnchor(Vector3 bodyPosition, float facing)
        {
            return bodyPosition + new Vector3(anchor.x * Mathf.Sign(facing == 0f ? 1f : facing), anchor.y, anchor.z);
        }

        // Abîme le membre. Renvoie true si ce coup l'arrache.
        public bool Damage(float amount)
        {
            if (IsTorn || amount <= 0f) return false;
            life = Mathf.Max(0f, life - amount);
            if (IsTorn) pocketed = false;
            return IsTorn;
        }

        // Ramassé au sol
        public void PickUp()
        {
            if (IsTorn) pocketed = true;
        }

        // Recouture : vie pleine, effet retiré
        public void Restore()
        {
            life = maxLife;
            effect = LimbEffect.None;
            pocketed = false;
        }

        public LimbSave ToSave()
        {
            return new LimbSave
            {
                kind = (int)kind, id = id, parentId = parentId, maxLife = maxLife, life = life,
                effect = (int)effect, anchor = anchor, pocketed = pocketed,
            };
        }

        public static Limb FromSave(LimbSave save)
        {
            var limb = new Limb(save.id, save.maxLife, save.anchor, (LimbKind)save.kind, save.parentId)
            {
                life = Mathf.Clamp(save.life, 0f, Mathf.Max(1f, save.maxLife)),
                effect = (LimbEffect)save.effect,
                pocketed = save.pocketed,
            };
            return limb;
        }
    }
}
