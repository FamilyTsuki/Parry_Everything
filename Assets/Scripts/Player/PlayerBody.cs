using System;
using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Santé du joueur, sans barre de vie : l'état se lit sur le personnage.
    // Âme : chaque coup la consume (elle rétrécit), elle se régénère après un moment sans coup. Consumée = mort.
    // Corps : la chair du Gardien enferme l'âme (boule qui roule), puis des pattes y sont cousues (debout).
    // Le torse a 3 parties (ventre autour de l'âme, poitrine, bassin), chaque patte aussi (cuisse, jarret, pied).
    // Chaque membre a sa vie et est cousu sur un autre (Limb.ParentId) ; tant que le ventre tient,
    // les coups frappent le membre le plus proche, pas l'âme.
    // Un membre arraché emporte tout ce qui y est cousu (poitrine → tête et bras ; bassin → pattes et queue ;
    // cuisse → jarret et pied ; jarret → pied). Ventre éclaté : tout tombe, retour à l'âme (vie pleine).
    // Une patte porte le chat tant que sa cuisse et son jarret tiennent ; sans pied, il boite.
    // Plus aucune patte : retour à la boule.
    // Bras, tête et queue n'ont pas encore de capacité (GAME_DESIGN 6.3, ⚠️) : ils sont visuels pour l'instant.
    // Un membre arraché tombe au sol (LimbPickup) ; ramassé, il peut être recousu une fois que ce qui le porte tient.
    public class PlayerBody : MonoBehaviour
    {
        [Header("Âme")]
        [SerializeField] private float soulMax = 100f;
        [SerializeField] private float soulRegenDelay = 2f;   // secondes sans coup avant la régénération
        [SerializeField] private float soulRegenRate = 10f;   // points par seconde
        [Range(0.1f, 1f)] [SerializeField] private float soulMinScale = 0.4f; // taille de l'âme presque consumée

        [Header("Corps")]
        [SerializeField] private List<Limb> limbs = new List<Limb>();  // vide au départ : on commence en âme

        // Prévient les autres scripts (flux de jeu, musique...) de la mort du joueur
        public event Action OnDied;
        // Coup fatal sur l'âme : renvoie true si quelque chose retient la mort (elle se décidera plus tard)
        public Func<bool> LethalHit;
        // Repos au sanctuaire
        public event Action OnRested;
        public event Action<Limb> OnLimbDamaged;
        // Un membre vient d'être arraché (il tombe au sol)
        public event Action<Limb> OnLimbLost;
        public event Action<Limb> OnLimbSewn;
        // Âme, boule ou debout : la forme vient de changer
        public event Action<PlayerForm> OnFormChanged;

        private float soul;
        private float lastHitTime = -100f;
        private bool dead;
        private PlayerForm lastForm = PlayerForm.Soul;

        public bool IsDead => dead;
        public float SoulMax => soulMax;
        // L'âme ne se régénère plus (cœur ralenti par les parades : CatHeart)
        public bool RegenBlocked { get; set; }
        // Outil de debug : les coups ne font plus rien
        public bool Invulnerable { get; set; }
        public float SoulRatio => soulMax > 0f ? Mathf.Clamp01(soul / soulMax) : 0f;
        // Taille visée de l'âme : pleine = 1, presque consumée = soulMinScale
        public float SoulScale => Mathf.Lerp(soulMinScale, 1f, SoulRatio);
        public IReadOnlyList<Limb> Limbs => limbs;
        // Point du monde où le dernier coup a touché (null : inconnu, ex. chute) ; lu par OnLimbDamaged (plaies)
        public Vector3? LastHitPoint { get; private set; }

        // La chair tient : le chat a un corps (boule ou debout)
        public bool HasBody => FleshLimb != null;
        public PlayerForm Form => !HasBody ? PlayerForm.Soul : IntactLegCount > 0 ? PlayerForm.Biped : PlayerForm.Ball;
        // Chair intacte (null si éclatée ou absente)
        public Limb FleshLimb => limbs.Find(l => l.Kind == LimbKind.Flesh && !l.IsTorn);
        // Pattes qui portent le chat : cuisse et jarret en place
        public int IntactLegCount => Count(IsStandingLeg);
        // Pattes qui portent le chat mais sans pied : il boite
        public int MissingFootCount => Count(l => IsStandingLeg(l) && !HasIntactChild(FindChild(l, LimbKind.Shin), LimbKind.Foot));
        public int IntactLimbCount => Count(l => !l.IsTorn);
        // Membres ramassés, prêts à être recousus
        public int PocketCount => Count(l => l.IsPocketed);
        // État global de santé (0 à 1), pour la musique et les effets d'écran : membres si on a un corps, sinon l'âme
        public float HealthRatio => HasBody ? BodyRatio() : SoulRatio;

        private void Awake()
        {
            soul = soulMax;
        }

        private void Update()
        {
            if (dead || soul >= soulMax || RegenBlocked) return;
            if (Time.time - lastHitTime < soulRegenDelay) return;
            soul = Mathf.Min(soulMax, soul + soulRegenRate * Time.deltaTime);
        }

        // Membre intact le plus proche d'un point du monde (null si aucun)
        public Limb NearestLimb(Vector3 point, float facing)
        {
            Limb best = null;
            float bestDist = float.MaxValue;
            foreach (Limb limb in limbs)
            {
                if (limb.IsTorn) continue;
                float d = (limb.WorldAnchor(transform.position, facing) - point).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = limb;
                }
            }
            return best;
        }

        // Applique un coup. Renvoie true si ce coup tue le joueur.
        // target : membre touché (le plus proche du coup, voir NearestLimb) ; null = la chair, ou le premier membre intact.
        // hitPoint : point d'impact dans le monde (plaie dessinée à cet endroit), facultatif.
        public bool ApplyDamage(float damage, Limb target = null, Vector3? hitPoint = null)
        {
            if (dead || Invulnerable) return false;
            lastHitTime = Time.time;
            LastHitPoint = hitPoint;

            if (HasBody)
            {
                Limb limb = target != null && !target.IsTorn && limbs.Contains(target)
                    ? target
                    : FleshLimb ?? limbs.Find(l => !l.IsTorn);
                bool torn = limb.Damage(damage);
                OnLimbDamaged?.Invoke(limb);
                if (torn) LoseLimb(limb);
                return false;
            }

            soul = Mathf.Max(0f, soul - damage);
            if (soul > 0f) return false;
            // Coup fatal : la mort peut encore se parer (DeathsDoor). L'âme ne tient plus qu'à un fil.
            if (LethalHit != null && LethalHit())
            {
                soul = soulMax * 0.02f;
                return false;
            }

            dead = true;
            OnDied?.Invoke();
            return true;
        }

        // Arrache un membre d'un coup (chute ratée). Sans effet si le membre est déjà arraché.
        public void TearLimb(Limb limb)
        {
            if (Invulnerable || limb == null || limb.IsTorn || !limbs.Contains(limb)) return;
            LastHitPoint = null;
            limb.Damage(limb.Life);
            OnLimbDamaged?.Invoke(limb);
            LoseLimb(limb);
        }

        private void LoseLimb(Limb limb)
        {
            OnLimbLost?.Invoke(limb);
            if (limb.Kind == LimbKind.Flesh)
            {
                // Le ventre éclate : tout ce qui est cousu sur le corps tombe avec lui
                foreach (Limb sewn in limbs)
                {
                    if (sewn == limb || sewn.IsTorn) continue;
                    sewn.Damage(sewn.Life);
                    OnLimbLost?.Invoke(sewn);
                }
                // Retour à l'âme, à vie pleine
                soul = soulMax;
                lastHitTime = -100f;
            }
            else
            {
                DropSewnOn(limb);
            }
            NotifyForm();
        }

        // Ce qui est cousu sur ce membre tombe avec lui (et ce qui est cousu dessus, etc.)
        private void DropSewnOn(Limb parent)
        {
            foreach (Limb sewn in limbs)
            {
                if (sewn.IsTorn || sewn.ParentId != parent.Id) continue;
                sewn.Damage(sewn.Life);
                OnLimbLost?.Invoke(sewn);
                DropSewnOn(sewn);
            }
        }

        // Mort immédiate, corps ou pas (chute dans le vide)
        public void Kill()
        {
            if (dead) return;
            dead = true;
            soul = 0f;
            OnDied?.Invoke();
        }

        // Rend des points d'âme (sans dépasser le maximum). Sans effet sur un mort.
        public void Heal(float amount)
        {
            if (dead || amount <= 0f) return;
            soul = Mathf.Min(soulMax, soul + amount);
        }

        // Ramasse un membre arraché tombé au sol
        public void PickUp(Limb limb)
        {
            if (limb != null && limbs.Contains(limb)) limb.PickUp();
        }

        // Peut-on recoudre ce membre maintenant ? Il faut que ce qui le porte tienne (le ventre d'abord, puis
        // la poitrine et le bassin, puis ce qui y est cousu...)
        public bool CanSew(Limb limb)
        {
            if (limb == null || !limb.IsPocketed || !limbs.Contains(limb)) return false;
            return CanHold(limb);
        }

        // Le corps peut porter ce membre : le ventre tient tout seul ; les autres ont besoin de leur porteur intact
        // (sans porteur indiqué, ancien membre : le ventre suffit)
        public bool CanHold(Limb limb)
        {
            if (limb.Kind == LimbKind.Flesh) return true;
            if (string.IsNullOrEmpty(limb.ParentId)) return HasBody;
            Limb parent = FindById(limb.ParentId);
            return parent != null && parent != limb && !parent.IsTorn;
        }

        // Membre sur lequel celui-ci est cousu (null : le ventre lui-même, ou porteur absent)
        public Limb ParentOf(Limb limb)
        {
            return limb == null || string.IsNullOrEmpty(limb.ParentId) ? null : FindById(limb.ParentId);
        }

        private Limb FindById(string id)
        {
            return limbs.Find(l => l.Id == id);
        }

        // Recoud un membre ramassé. Renvoie true s'il a été recousu.
        public bool Sew(Limb limb)
        {
            if (!CanSew(limb)) return false;
            limb.Restore();
            OnLimbSewn?.Invoke(limb);
            NotifyForm();
            return true;
        }

        // Recoud le prochain membre en poche (le ventre d'abord, puis ce qui peut tenir). Renvoie true si un membre a été recousu.
        public bool SewNext()
        {
            Limb next = limbs.Find(l => l.Kind == LimbKind.Flesh && CanSew(l)) ?? limbs.Find(CanSew);
            return Sew(next);
        }

        // Sanctuaire : recoud tout ce qui est en poche, soigne les membres présents, oublie les membres perdus
        // (restés au sol ailleurs) et remplit l'âme.
        public void RestAtSanctuary()
        {
            while (SewNext()) { }
            limbs.RemoveAll(l => l.IsTorn);
            RestoreLimbs();
            Heal(soulMax);
            NotifyForm();
            OnRested?.Invoke();
        }

        // La chair d'un boss s'agglutine autour de l'âme : remplace tout le corps actuel par cette chair
        // (le torse entier : Limb.Torso ; ou le ventre seul : Limb.Flesh)
        public void GrantFlesh(params Limb[] torso)
        {
            limbs.Clear();
            AttachLimbs(torso);
            NotifyForm();
        }

        // Coud de nouveaux membres sur le corps. Chacun ne tient que si ce qui le porte tient
        // (dans la liste, les porteurs passent avant : Limb.CatLegs donne cuisse, jarret, pied dans cet ordre).
        public bool AttachLimbs(IEnumerable<Limb> newLimbs)
        {
            if (newLimbs == null) return false;
            bool any = false;
            foreach (Limb limb in newLimbs)
            {
                if (limb == null || limbs.Contains(limb) || !CanHold(limb)) continue;
                limbs.Add(limb);
                any = true;
            }
            NotifyForm();
            return any;
        }

        // Coud un membre (la chair, ou un autre membre si la chair tient)
        public bool AttachLimb(Limb limb)
        {
            return AttachLimbs(new[] { limb });
        }

        // Retire tous les membres (retour à l'âme)
        public void RemoveAllLimbs()
        {
            limbs.Clear();
            NotifyForm();
        }

        // Soigne tous les membres présents : vie pleine, effets retirés (les membres arrachés restent arrachés)
        public void RestoreLimbs()
        {
            foreach (Limb limb in limbs)
            {
                if (!limb.IsTorn) limb.Restore();
            }
        }

        // Réapparition : âme pleine
        public void Revive()
        {
            dead = false;
            soul = soulMax;
            lastHitTime = -100f;
        }

        // ---------- Sauvegarde ----------

        public List<LimbSave> SaveLimbs()
        {
            var list = new List<LimbSave>();
            foreach (Limb limb in limbs) list.Add(limb.ToSave());
            return list;
        }

        public void LoadLimbs(IEnumerable<LimbSave> saves)
        {
            limbs.Clear();
            if (saves != null)
            {
                foreach (LimbSave save in saves) limbs.Add(Limb.FromSave(save));
            }
            NotifyForm();
        }

        // ---------- Outils ----------

        private void NotifyForm()
        {
            PlayerForm form = Form;
            if (form == lastForm) return;
            lastForm = form;
            OnFormChanged?.Invoke(form);
        }

        // Patte qui porte le chat : cuisse en place, avec son jarret en place
        private bool IsStandingLeg(Limb limb)
        {
            return limb.Kind == LimbKind.Leg && !limb.IsTorn && CanHold(limb) && HasIntactChild(limb, LimbKind.Shin);
        }

        private Limb FindChild(Limb parent, LimbKind kind)
        {
            return parent == null ? null : limbs.Find(l => l.Kind == kind && l.ParentId == parent.Id);
        }

        private bool HasIntactChild(Limb parent, LimbKind kind)
        {
            Limb child = FindChild(parent, kind);
            return child != null && !child.IsTorn;
        }

        private int Count(Predicate<Limb> match)
        {
            int n = 0;
            foreach (Limb limb in limbs) if (match(limb)) n++;
            return n;
        }

        // Vie moyenne des membres (les membres arrachés comptent pour 0)
        private float BodyRatio()
        {
            if (limbs.Count == 0) return 0f;
            float sum = 0f;
            foreach (Limb limb in limbs) sum += limb.LifeRatio;
            return sum / limbs.Count;
        }
    }
}
