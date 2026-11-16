using UnityEngine;

namespace ParryEverything.Player
{
    // Flaque de sang (disque très plat) qui s'étale jusqu'à son rayon, de plus en plus lentement, puis reste au sol.
    public class BloodPool : MonoBehaviour
    {
        private const float Thickness = 0.004f;

        private float radius;
        private float growTime;
        private float age;

        public void Grow(float targetRadius, float time)
        {
            radius = targetRadius;
            growTime = Mathf.Max(0.01f, time);
            transform.localScale = new Vector3(0.01f, Thickness, 0.01f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / growTime);
            float r = radius * 2f * (1f - (1f - t) * (1f - t));   // diamètre ; ralentit en fin d'étalement
            transform.localScale = new Vector3(Mathf.Max(0.01f, r), Thickness, Mathf.Max(0.01f, r));
            if (t >= 1f) enabled = false;
        }
    }
}
