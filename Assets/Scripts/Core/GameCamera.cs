using UnityEngine;
using UnityEngine.Serialization;

namespace ParryEverything.Core
{
    // Caméra qui suit le joueur, avec tremblement d'écran et cadrage élargi (combat de boss).
    public class GameCamera : MonoBehaviour
    {
        [FormerlySerializedAs("player")]
        [SerializeField] private Transform target;

        [Header("Écran titre")]
        [SerializeField] private Vector3 titleOffset = new Vector3(-2.6f, 1.1f, -1.2f); // cadrage du titre, par rapport au suivi
        [SerializeField] private Vector2 titleDrift = new Vector2(0.5f, 0.25f);        // dérive lente autour du cadrage
        [SerializeField] private float titleDriftSpeed = 0.08f;                         // tours par seconde
        [SerializeField] private float titleBlendTime = 2f;                             // passage du titre au jeu (s)

        // 1 = cadrage du titre, 0 = suivi du jeu (négatif : pas encore initialisé)
        private float titleBlend = -1f;

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
                Vector3 follow = new Vector3(targetPosition.x, targetPosition.y + yOffset, baseZ - extraDistance);
                transform.position = follow + TitleOffset();
            }

            // Décalage aléatoire autour de la position suivie
            if (shakeTimeLeft > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
                transform.position += new Vector3(offset.x, offset.y, 0f);
                shakeTimeLeft -= Time.unscaledDeltaTime;
            }
        }

        // Au titre, la caméra se décale et dérive lentement ; en lançant la partie, elle glisse vers le suivi
        private Vector3 TitleOffset()
        {
            bool inMenu = GameFlow.Instance != null && GameFlow.Instance.State == GameState.Menu;
            if (titleBlend < 0f) titleBlend = inMenu ? 1f : 0f;
            float step = titleBlendTime > 0f ? Time.unscaledDeltaTime / titleBlendTime : 1f;
            titleBlend = Mathf.MoveTowards(titleBlend, inMenu ? 1f : 0f, step);
            if (titleBlend <= 0f) return Vector3.zero;

            float k = titleBlend * titleBlend * (3f - 2f * titleBlend);
            float a = Time.unscaledTime * titleDriftSpeed * 2f * Mathf.PI;
            Vector3 drift = new Vector3(Mathf.Sin(a) * titleDrift.x, Mathf.Sin(a * 1.7f + 1f) * titleDrift.y, 0f);
            return (titleOffset + drift) * k;
        }
    }
}
