using UnityEngine;

namespace ParryEverything.Core
{
    // Effet plein écran d'ambiance (pipeline Built-in, OnRenderImage) : image froide et désaturée,
    // vignette, grain et aberration discrets. Dread (0..1) fait monter le malaise : vignette
    // rouge sombre, aberration et ondulation plus marquées (combat de boss).
    // Actif aussi hors Play pour voir le rendu dans la Game view.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class ScreenMood : MonoBehaviour
    {
        private const string ShaderName = "Hidden/ParryEverything/ScreenMood";

        [Header("Calme")]
        [Range(0f, 1f)] [SerializeField] private float desaturate = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float keepRed = 0.85f;
        [SerializeField] private Color tint = new Color(0.86f, 0.93f, 1.05f);
        [Range(0f, 1f)] [SerializeField] private float vignette = 0.6f;
        [SerializeField] private Color vignetteColor = new Color(0.01f, 0.015f, 0.03f);
        [Range(0f, 0.2f)] [SerializeField] private float grain = 0.035f;
        [Range(0f, 4f)] [SerializeField] private float aberration = 0.6f;
        [Range(0f, 2f)] [SerializeField] private float wobble = 0.25f;

        [Header("Malaise maximal (Dread = 1)")]
        [SerializeField] private Color dreadVignetteColor = new Color(0.09f, 0.0f, 0.01f);
        [Range(0f, 1f)] [SerializeField] private float dreadVignette = 0.85f;
        [Range(0f, 4f)] [SerializeField] private float dreadAberration = 1.6f;
        [Range(0f, 2f)] [SerializeField] private float dreadWobble = 0.8f;
        [Range(0f, 0.2f)] [SerializeField] private float dreadGrain = 0.05f;

        private Material material;
        private float dread;

        // Niveau de malaise 0..1, piloté de l'extérieur (Atmosphere)
        public float Dread
        {
            get => dread;
            set => dread = Mathf.Clamp01(value);
        }

        private void OnDisable()
        {
            if (material != null)
            {
                GeneratedObjects.SafeDestroy(material);
                material = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                material = MaterialFactory.FromShader(ShaderName);
                if (material == null)
                {
                    Graphics.Blit(source, destination);
                    return;
                }
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            material.SetFloat("_Desaturate", desaturate);
            material.SetFloat("_KeepRed", keepRed);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Vignette", Mathf.Lerp(vignette, dreadVignette, dread));
            material.SetColor("_VignetteColor", Color.Lerp(vignetteColor, dreadVignetteColor, dread));
            material.SetFloat("_Grain", Mathf.Lerp(grain, dreadGrain, dread));
            material.SetFloat("_Aberration", Mathf.Lerp(aberration, dreadAberration, dread));
            material.SetFloat("_Wobble", Mathf.Lerp(wobble, dreadWobble, dread));
            Graphics.Blit(source, destination, material);
        }
    }
}
