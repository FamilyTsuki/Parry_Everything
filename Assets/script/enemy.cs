using System.Collections;
using UnityEngine;

public class enemy : MonoBehaviour
{
    // Objet "attaque" (enfant de l'ennemi) : sert de zone de collision et de visuel
    public GameObject attaque;
    public float attackInterval = 3.0f;
    public float attackDuration = 0.4f;
    public float damage = 10.0f;
    // Arc de cercle : rayon autour de l'ennemi et angle total balayé (degrés)
    public float attackRadius = 1.5f;
    public float arcAngle = 180.0f;

    // Réaction quand le joueur pare l'attaque
    public float parryReturnDuration = 0.12f; // temps pour que l'attaque reparte en arrière
    public float stunDuration = 1.0f;         // ennemi étourdi : pas d'attaque pendant ce temps
    public float parryFlashDuration = 0.08f;  // flash blanc de l'arme

    // Modèle 3D de l'ennemi (ex: FreeLichPolyart), instancié comme enfant au démarrage
    public GameObject modelPrefab;
    public Vector3 modelOffset = new Vector3(0f, -0.5f, 0f);
    public float modelScale = 1.0f;
    public bool hidePlaceholder = true; // cache le cube d'origine

    // Animation d'attaque : état joué (Attack01 ou Attack02) et temps d'armement avant le coup
    public string attackAnim = "Attack02";
    public float windupDuration = 0.35f; // l'ennemi arme son coup (signal pour parer)

    private Collider attaqueCollider;
    private player target;
    private Transform model;
    private Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (attaque == null)
        {
            Debug.LogError("enemy: objet 'attaque' non assigné.");
            enabled = false;
            return;
        }

        attaqueCollider = attaque.GetComponent<Collider>();
        target = FindFirstObjectByType<player>();
        SpawnModel();
        attaque.SetActive(false);
        StartCoroutine(AttackLoop());
    }

    // Update is called once per frame
    void Update()
    {
        // Le modèle regarde vers le joueur (axe X du jeu 2D)
        if (model != null && target != null)
        {
            float side = target.transform.position.x < transform.position.x ? -1f : 1f;
            model.rotation = Quaternion.Euler(0f, side * 90f, 0f);
        }
    }

    void SpawnModel()
    {
        GameObject m;
        if (modelPrefab != null)
        {
            m = Instantiate(modelPrefab, transform);
            m.transform.localPosition = modelOffset;
            m.transform.localScale = Vector3.one * modelScale;
        }
        else
        {
            // Modèle déjà placé comme enfant dans la scène (visible en mode édition)
            Animator a = GetComponentInChildren<Animator>();
            if (a == null) return;
            m = a.gameObject;
        }
        model = m.transform;

        // Le modèle est purement visuel : pas de collisions parasites
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (Rigidbody rb in m.GetComponentsInChildren<Rigidbody>()) Destroy(rb);

        animator = m.GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        if (hidePlaceholder)
        {
            MeshRenderer r = GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }
        PlayAnim("Idle");
    }

    // Accélère/ralentit l'Animator pour que le clip dure exactement 'duration'
    void SyncAnimSpeed(string clipName, float duration)
    {
        if (animator == null || animator.runtimeAnimatorController == null || duration <= 0f) return;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.ToLower().Contains(clipName.ToLower()))
            {
                animator.speed = clip.length / duration;
                return;
            }
        }
    }

    // Joue un état de l'Animator par son nom (essaie plusieurs casses)
    void PlayAnim(string state, float fade = 0.1f)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (string name in new[] { state, state.ToLower() })
        {
            int hash = Animator.StringToHash(name);
            if (animator.HasState(0, hash))
            {
                animator.CrossFadeInFixedTime(hash, fade, 0);
                return;
            }
        }
    }

    IEnumerator AttackLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(attackInterval);
            yield return AttackPlayer();
        }
    }

    IEnumerator AttackPlayer()
    {
        // Logic to attack the player
        Debug.Log("Enemy attacks the player!");
        // Côté du joueur : +1 à droite, -1 à gauche
        float side = (target != null && target.transform.position.x < transform.position.x) ? -1f : 1f;
        float startAngle = 90f - side * (90f - arcAngle / 2f);
        float endAngle = startAngle - side * arcAngle;

        // Armement : l'animation démarre avant le coup, calée sur armement + coup
        PlayAnim(attackAnim, 0.05f);
        SyncAnimSpeed(attackAnim, windupDuration + attackDuration);
        yield return new WaitForSeconds(windupDuration);

        PlaceAttaque(startAngle);
        attaque.SetActive(true);

        bool hasHit = false;
        bool wasParried = false;
        float currentAngle = startAngle;
        float timer = 0f;
        while (timer < attackDuration && !wasParried)
        {
            currentAngle = Mathf.Lerp(startAngle, endAngle, timer / attackDuration);
            PlaceAttaque(currentAngle);
            Physics.SyncTransforms();

            if (!hasHit && attaqueCollider != null)
            {
                Bounds b = attaqueCollider.bounds;
                Collider[] hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity);
                foreach (Collider hit in hits)
                {
                    player p = hit.GetComponentInParent<player>();
                    if (p != null)
                    {
                        if (p.TakeDamage(damage, transform, attaque.transform.position))
                        {
                            print("Player hit by attack! Life: " + p.life);
                        }
                        else
                        {
                            wasParried = true;
                        }
                        // Touché ou paré : l'attaque ne frappe qu'une fois
                        hasHit = true;
                        break;
                    }
                }
            }
            if (!wasParried)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        if (wasParried)
        {
            yield return Parried(currentAngle, startAngle);
        }

        attaque.SetActive(false);
        if (animator != null) animator.speed = 1f;
        PlayAnim("Idle", 0.2f);
    }

    // Attaque parée : l'arme flashe et repart vite vers son point de départ, puis l'ennemi reste étourdi
    IEnumerator Parried(float fromAngle, float toAngle)
    {
        HitFlash.Play(attaque, parryFlashDuration);
        if (animator != null) animator.speed = 1f;
        PlayAnim("GetHit", 0.05f);

        float timer = 0f;
        while (timer < parryReturnDuration)
        {
            PlaceAttaque(Mathf.Lerp(fromAngle, toAngle, timer / parryReturnDuration));
            timer += Time.deltaTime;
            yield return null;
        }
        attaque.SetActive(false);

        Debug.Log("Enemy stunned!");
        yield return new WaitForSeconds(stunDuration);
    }

    void PlaceAttaque(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * attackRadius;
        attaque.transform.position = transform.position + offset;
        attaque.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
