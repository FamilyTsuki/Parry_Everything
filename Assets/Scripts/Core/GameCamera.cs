using UnityEngine;
using UnityEngine.Serialization;

namespace ParryEverything.Core
{
    // Caméra qui suit le joueur, avec tremblement d'écran et cadrage élargi (combat de boss).
    public class GameCamera : MonoBehaviour
    {
        [FormerlySerializedAs("player")]
        [SerializeField] private Transform target;

        // Tremblement en cours (temps réel, pour marcher même quand le jeu est figé)
        private float shakeTimeLeft;
        private float shakeMagnitude;

        // Cadrage large (ex : combat de boss) : la caméra recule et monte, en douceur
        private float baseZ;
        private bool baseZSet;
        private float extraDistance, extraDistanceTarget;
        private float yOffset, yOffsetTarget;

        // GameCamera de la caméra principale (null si absente)
        public static GameCamera Main
        {
            get
            {
                Camera cam = Camera.main;
                return cam != null ? cam.GetComponent<GameCamera>() : null;
            }
        }

        // Raccourci : tremblement de la caméra principale, si elle existe
        public static void ShakeMain(float duration, float magnitude)
        {
            GameCamera cam = Main;
            if (cam != null)
            {
                cam.Shake(duration, magnitude);
            }
        }

        public void SetFraming(float distance, float height)
        {
            extraDistanceTarget = distance;
            yOffsetTarget = height;
        }

        // Lance un tremblement d'écran : durée en secondes, amplitude en unités
        public void Shake(float duration, float magnitude)
        {
            shakeTimeLeft = duration;
            shakeMagnitude = magnitude;
        }

        private void Update()
        {
            if (target != null)
            {
                if (!baseZSet)
                {
                    baseZ = transform.position.z;
                    baseZSet = true;
                }
                // Temps réel : le cadrage bouge aussi pendant les ralentis
                float k = 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime);
                extraDistance = Mathf.Lerp(extraDistance, extraDistanceTarget, k);
                yOffset = Mathf.Lerp(yOffset, yOffsetTarget, k);

                Vector3 targetPosition = target.position;
                transform.position = new Vector3(targetPosition.x, targetPosition.y + yOffset, baseZ - extraDistance);
            }

            // Décalage aléatoire autour de la position suivie
            if (shakeTimeLeft > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
                transform.position += new Vector3(offset.x, offset.y, 0f);
                shakeTimeLeft -= Time.unscaledDeltaTime;
            }
        }
    }
}
