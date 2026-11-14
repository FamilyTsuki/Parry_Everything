using System;
using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Santé du joueur, sans barre de vie : l'état se lit sur le personnage.
    // Âme : chaque coup la consume (elle rétrécit), elle se régénère après un moment sans coup. Consumée = mort.
    // Corps : la chair du Gardien enferme l'âme (boule qui roule), puis des pattes y sont cousues (debout).
    // Chaque membre a sa vie ; tant que la chair tient, les coups frappent le membre le plus proche, pas l'âme.
    // Pattes perdues : retour à la boule. Chair éclatée : les pattes tombent avec, retour à l'âme (vie pleine).
    // Un membre arraché tombe au sol (LimbPickup) ; ramassé, il peut être recousu (la chair d'abord).
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
        // Outil de debug : les coups ne font plus rien
        public bool Invulnerable { get; set; }
        public float SoulRatio => soulMax > 0f ? Mathf.Clamp01(soul / soulMax) : 0f;
        // Taille visée de l'âme : pleine = 1, presque consumée = soulMinScale
        public float SoulScale => Mathf.Lerp(soulMinScale, 1f, SoulRatio);
        public IReadOnlyList<Limb> Limbs => limbs;

        // La chair tient : le chat a un corps (boule ou debout)
        public bool HasBody => FleshLimb != null;
        public PlayerForm Form => !HasBody ? PlayerForm.Soul : IntactLegCount > 0 ? PlayerForm.Biped : PlayerForm.Ball;
        // Chair intacte (null si éclatée ou absente)
        public Limb FleshLimb => limbs.Find(l => l.Kind == LimbKind.Flesh && !l.IsTorn);
        public int IntactLegCount => Count(l => l.Kind == LimbKind.Leg && !l.IsTorn);
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
            if (dead || soul >= soulMax) return;
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
        public bool ApplyDamage(float damage, Limb target = null)
        {
            if (dead || Invulnerable) return false;
            lastHitTime = Time.time;

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

            dead = true;
            OnDied?.Invoke();
            return true;
        }

        // Arrache un membre d'un coup (chute ratée). Sans effet si le membre est déjà arraché.
        public void TearLimb(Limb limb)
        {
            if (Invulnerable || limb == null || limb.IsTorn || !limbs.Contains(limb)) return;
            limb.Damage(limb.Life);
            OnLimbDamaged?.Invoke(limb);
            LoseLimb(limb);
        }

        private void LoseLimb(Limb limb)
        {
            OnLimbLost?.Invoke(limb);
            if (limb.Kind == LimbKind.Flesh)
            {
                // La chair éclate : les pattes cousues dessus tombent avec elle
                foreach (Limb leg in limbs)
                {
                    if (leg.Kind != LimbKind.Leg || leg.IsTorn) continue;
                    leg.Damage(leg.Life);
                    OnLimbLost?.Invoke(leg);
                }
                // Retour à l'âme, à vie pleine
                soul = soulMax;
                lastHitTime = -100f;
            }
            NotifyForm();
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

        // Peut-on recoudre ce membre maintenant ? (une patte se coud sur la chair : il faut la chair d'abord)
        public bool CanSew(Limb limb)
        {
            if (limb == null || !limb.IsPocketed || !limbs.Contains(limb)) return false;
            return limb.Kind == LimbKind.Flesh || HasBody;
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

        // Recoud le prochain membre en poche (la chair avant les pattes). Renvoie true si un membre a été recousu.
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
        }

        // La chair d'un boss s'agglutine autour de l'âme : remplace tout le corps actuel par cette chair
        public void GrantFlesh(Limb flesh)
        {
            limbs.Clear();
            if (flesh != null) limbs.Add(flesh);
            NotifyForm();
        }

        // Coud de nouveaux membres sur la chair (pattes de l'âme marchande). Sans chair, rien ne tient.
        public bool AttachLimbs(IEnumerable<Limb> newLimbs)
        {
            if (newLimbs == null) return false;
            bool any = false;
            foreach (Limb limb in newLimbs)
            {
                if (limb == null || limbs.Contains(limb)) continue;
                if (limb.Kind == LimbKind.Leg && !HasBody) continue;
                limbs.Add(limb);
                any = true;
            }
            NotifyForm();
            return any;
        }

        // Coud un membre (la chair, ou une patte si la chair tient)
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
