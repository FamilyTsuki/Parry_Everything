using UnityEngine;

namespace ParryEverything.Environment
{
    // Lévitation lente de haut en bas (îles flottantes, objets en suspension).
    public class FloatingBob : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.3f;
        [SerializeField] private float speed = 0.6f;
        [SerializeField] private float phase;   // décalage pour que les objets ne bougent pas tous ensemble

        private float baseY;

        private void Start()
        {
            baseY = transform.localPosition.y;
        }

        private void Update()
        {
            Vector3 p = transform.localPosition;
            p.y = baseY + Mathf.Sin(Time.time * speed + phase) * amplitude;
            transform.localPosition = p;
        }
    }
}
