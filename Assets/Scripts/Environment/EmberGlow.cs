using UnityEngine;

namespace ParryEverything.Environment
{
    // Lueur d'une braise mourante (fissure de lave figée, foyer éteint) : l'émission monte et
    // retombe lentement, à son propre rythme. L'enfer se ranime pendant le combat de boss.
    [RequireComponent(typeof(Renderer))]
    public class EmberGlow : MonoBehaviour
    {
        [SerializeField] private Color emission = new Color(0.55f, 0.06f, 0.02f);
        [SerializeField] private float speed = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float depth = 0.75f;   // amplitude de la pulsation
        [SerializeField] private float bossBoost = 1.8f;                 // multiplicateur au plus fort du combat

        private Renderer target;
        private MaterialPropertyBlock block;
        private float seed;

        private void Start()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * speed, seed);
            float level = 1f - depth + depth * n * n * 1.6f;
            float dread = Atmosphere.Current != null ? Atmosphere.Current.Dread : 0f;
            level *= Mathf.Lerp(1f, bossBoost, dread);

            target.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", emission * level);
            target.SetPropertyBlock(block);
        }
    }
}
