using System;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Un membre du corps : la chair (autour de l'âme) ou une patte (cousue sur la chair).
    // Sa propre vie, un effet possible. Arraché quand sa vie tombe à 0.
    // Un membre arraché reste au sol (LimbPickup) ; ramassé, il est « en poche » jusqu'à la recouture.
    [Serializable]
    public class Limb
    {
        [SerializeField] private LimbKind kind = LimbKind.Leg;
        [SerializeField] private string id;           // ex : "patte gauche"
        [SerializeField] private float maxLife = 40f;
        [SerializeField] private float life = 40f;
        [SerializeField] private LimbEffect effect = LimbEffect.None;
        // Point d'attache sur le corps, côté droit (x : avant, y : haut, z : profondeur) ; retourné si le chat regarde à gauche
        [SerializeField] private Vector3 anchor;
        [SerializeField] private bool pocketed;        // arraché puis ramassé : prêt à être recousu

        public LimbKind Kind => kind;
        public string Id => id;
        public float MaxLife => maxLife;
        public float Life => life;
        public float LifeRatio => maxLife > 0f ? Mathf.Clamp01(life / maxLife) : 0f;
        public bool IsTorn => life <= 0f;
        public bool IsPocketed => pocketed && IsTorn;
        public Vector3 Anchor => anchor;
        public LimbEffect Effect
        {
            get => effect;
            set => effect = value;
        }

        public Limb(string id, float maxLife, Vector3 anchor = default, LimbKind kind = LimbKind.Leg)
        {
            this.kind = kind;
            this.id = id;
            this.maxLife = Mathf.Max(1f, maxLife);
            life = this.maxLife;
            this.anchor = anchor;
        }

        // La chair du Gardien, agglutinée autour de l'âme (debout, elle est portée en hauteur par les pattes)
        public static Limb Flesh(float life)
        {
            return new Limb("chair du Gardien", life, new Vector3(0f, 0.55f, 0f), LimbKind.Flesh);
        }

        // Les 2 grandes pattes fabriquées par l'âme marchande (le chat se tient debout)
        public static Limb[] CatLegs(float life)
        {
            return new[]
            {
                // Écartées sur les côtés de la boule : l'une vers l'avant, l'autre vers l'arrière
                new Limb("patte proche", life, new Vector3(0.2f, -0.05f, -0.2f)),
                new Limb("patte loin", life, new Vector3(-0.2f, -0.05f, 0.2f)),
            };
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
            return new LimbSave { kind = (int)kind, id = id, maxLife = maxLife, life = life, effect = (int)effect, anchor = anchor, pocketed = pocketed };
        }

        public static Limb FromSave(LimbSave save)
        {
            var limb = new Limb(save.id, save.maxLife, save.anchor, (LimbKind)save.kind)
            {
                life = Mathf.Clamp(save.life, 0f, Mathf.Max(1f, save.maxLife)),
                effect = (LimbEffect)save.effect,
                pocketed = save.pocketed,
            };
            return limb;
        }
    }
}
