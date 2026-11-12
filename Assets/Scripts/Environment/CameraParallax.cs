using UnityEngine;

namespace ParryEverything.Environment
{
    // Suit la caméra principale sur X (objet très lointain : lune, ciel).
    // followX = 1 : reste fixe à l'écran ; 0 : reste fixe dans le monde.
    public class CameraParallax : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] private float followX = 0.85f;

        private float originX;

        private void Start()
        {
            originX = transform.position.x;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 p = transform.position;
            p.x = cam.transform.position.x * followX + originX * (1f - followX);
            transform.position = p;
        }
    }
}
