using UnityEngine;

namespace ParryEverything.Environment
{
    // Balancement irrégulier autour du point d'accroche (chaînes, cages, lambeaux pendus).
    // Le pivot est l'objet lui-même : placer l'origine en haut de ce qui pend.
    public class Sway : MonoBehaviour
    {
        [SerializeField] private float angle = 6f;      // amplitude en degrés
        [SerializeField] private float speed = 0.35f;

        private Quaternion baseRotation;
        private float seed;

        private void Start()
        {
            baseRotation = transform.localRotation;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * speed;
            float x = (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * angle;
            float z = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * angle;
            transform.localRotation = baseRotation * Quaternion.Euler(x, 0f, z);
        }
    }
}
