using System;
using UnityEngine;

namespace ParryEverything.Player
{
    // Santé du joueur, sans barre de vie : l'état se lit sur le personnage.
    // Âme : chaque coup la consume (elle rétrécit), elle se régénère après un moment sans coup. Consumée = mort.
    // Les membres du corps (vie par membre, effets) viendront s'ajouter ici.
    public class PlayerBody : MonoBehaviour
    {
        [Header("Âme")]
        [SerializeField] private float soulMax = 100f;
        [SerializeField] private float soulRegenDelay = 2f;   // secondes sans coup avant la régénération
        [SerializeField] private float soulRegenRate = 10f;   // points par seconde
        [Range(0.1f, 1f)] [SerializeField] private float soulMinScale = 0.4f; // taille de l'âme presque consumée

        // Prévient les autres scripts (flux de jeu, musique...) de la mort du joueur
        public event Action OnDied;

        private float soul;
        private float lastHitTime = -100f;
        private bool dead;

        public bool IsDead => dead;
        public float SoulRatio => soulMax > 0f ? Mathf.Clamp01(soul / soulMax) : 0f;
        // Taille visée de l'âme : pleine = 1, presque consumée = soulMinScale
        public float SoulScale => Mathf.Lerp(soulMinScale, 1f, SoulRatio);
        // État global de santé (0 à 1), pour la musique et les effets d'écran
        public float HealthRatio => SoulRatio;

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

        // Applique un coup. Renvoie true si ce coup tue le joueur.
        public bool ApplyDamage(float damage)
        {
            if (dead) return false;

            lastHitTime = Time.time;
            soul = Mathf.Max(0f, soul - damage);
            if (soul > 0f) return false;

            dead = true;
            OnDied?.Invoke();
            return true;
        }

        // Réapparition : âme pleine
        public void Revive()
        {
            dead = false;
            soul = soulMax;
            lastHitTime = -100f;
        }
    }
}
