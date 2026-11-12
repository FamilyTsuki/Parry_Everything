using System.Collections;
using UnityEngine;

// Fait clignoter un objet en blanc pendant un court instant (temps réel, marche même quand le jeu est figé).
// Utilisation : HitFlash.Play(gameObject, 0.08f);
public class HitFlash : MonoBehaviour
{
    private Renderer rend;
    private Color originalColor;
    private Color originalEmission;
    private bool hadEmission;
    private Coroutine routine;

    public static void Play(GameObject target, float duration)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return;
        }
        HitFlash flash = target.GetComponent<HitFlash>();
        if (flash == null)
        {
            flash = target.AddComponent<HitFlash>();
        }
        flash.Flash(duration);
    }

    void Awake()
    {
        // Couleurs d'origine mémorisées une seule fois, pour ne jamais rester bloqué en blanc
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            originalColor = rend.material.color;
            hadEmission = rend.material.IsKeywordEnabled("_EMISSION");
            if (rend.material.HasProperty("_EmissionColor"))
            {
                originalEmission = rend.material.GetColor("_EmissionColor");
            }
        }
    }

    void Flash(float duration)
    {
        if (rend == null)
        {
            return;
        }
        if (routine != null)
        {
            StopCoroutine(routine);
        }
        routine = StartCoroutine(FlashRoutine(duration));
    }

    IEnumerator FlashRoutine(float duration)
    {
        // Blanc + émission blanche : l'objet devient blanc pur, même dans l'ombre
        rend.material.color = Color.white;
        rend.material.EnableKeyword("_EMISSION");
        rend.material.SetColor("_EmissionColor", Color.white);
        yield return new WaitForSecondsRealtime(duration);
        Restore();
        routine = null;
    }

    void Restore()
    {
        if (rend == null)
        {
            return;
        }
        rend.material.color = originalColor;
        rend.material.SetColor("_EmissionColor", originalEmission);
        if (!hadEmission)
        {
            rend.material.DisableKeyword("_EMISSION");
        }
    }

    // Objet désactivé pendant le flash : la coroutine s'arrête, on remet les couleurs
    void OnDisable()
    {
        Restore();
        routine = null;
    }
}
