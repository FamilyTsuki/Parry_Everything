using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Fond d'un gouffre (BoxCollider en déclencheur) : l'âme qui y tombe ne meurt pas, elle revient au bord
    // ('returnPoint') dans un petit souffle. Avec un corps, la chute fait aussi perdre un peu de vie.
    [RequireComponent(typeof(BoxCollider))]
    public class PitReturn : MonoBehaviour
    {
        [SerializeField] private Transform returnPoint;
        [SerializeField] private float bodyDamage = 8f;

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            PlayerController p = other.GetComponentInParent<PlayerController>();
            if (p == null || p.IsDead || returnPoint == null) return;
            if (p.HasBody) p.TakeDamage(bodyDamage, transform, p.transform.position);
            if (p.IsDead) return;
            ParticleBurst.Spawn("PitReturnOut", p.transform.position, new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.85f, 1f), Color.white), 20,
                lifetime: new Vector2(0.3f, 0.6f), speed: new Vector2(1f, 3f), size: new Vector2(0.05f, 0.12f), gravity: -0.3f, radius: 0.3f);
            if (GameFlow.Instance != null) GameFlow.Instance.PlacePlayer(returnPoint.position);
            ParticleBurst.Spawn("PitReturnIn", returnPoint.position, new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.85f, 1f), Color.white), 24,
                lifetime: new Vector2(0.3f, 0.7f), speed: new Vector2(1f, 3f), size: new Vector2(0.05f, 0.12f), gravity: -0.3f, radius: 0.4f);
        }
    }
}
