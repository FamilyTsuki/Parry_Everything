using UnityEngine;

namespace ParryEverything.Lab
{
    // Caméra de la scène AnimationLab : tourne autour d'un point pour voir les modèles sous tous les angles.
    // Clic droit + glisser : tourner. Clic molette + glisser : décaler. Molette : zoom.
    // Marche aussi en pause (temps réel). Outil de visualisation : la souris est lue directement.
    public class LabCamera : MonoBehaviour
    {
        [SerializeField] private float distance = 7f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 60f;
        [SerializeField] private float orbitSpeed = 0.3f;     // degrés par pixel
        [SerializeField] private float zoomStep = 0.12f;      // part de la distance par cran de molette
        [SerializeField] private float followSharpness = 10f;

        private Vector3 focus;          // point suivi (personnage)
        private Vector3 pan;            // décalage choisi à la souris
        private Vector3 smoothFocus;
        private float yaw;              // 0 = vue du jeu (caméra côté -Z)
        private float pitch = 8f;
        private Vector3 lastMouse;
        private bool started;

        public void SetFocus(Vector3 point) => focus = point;

        public void SetView(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = newPitch;
        }

        public void Frame(float newDistance)
        {
            distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
            pan = Vector3.zero;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 mouse = Input.mousePosition;
            Vector3 delta = mouse - lastMouse;
            lastMouse = mouse;

            if (Input.GetMouseButton(1))
            {
                yaw += delta.x * orbitSpeed;
                pitch = Mathf.Clamp(pitch - delta.y * orbitSpeed, -80f, 85f);
            }
            if (Input.GetMouseButton(2))
            {
                float k = distance * 0.0015f;
                pan -= (transform.right * delta.x + transform.up * delta.y) * k;
            }
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) distance = Mathf.Clamp(distance * (1f - scroll * zoomStep), minDistance, maxDistance);

            Vector3 goal = focus + pan;
            smoothFocus = started ? Vector3.Lerp(smoothFocus, goal, 1f - Mathf.Exp(-followSharpness * dt)) : goal;
            started = true;

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = smoothFocus + rot * (Vector3.back * distance);
            transform.rotation = rot;
        }
    }
}
