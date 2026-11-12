using System.Collections;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace ParryEverything.Enemies
{
    // Ennemi de mêlée : à intervalle régulier, arme son coup puis balaie un arc de cercle devant lui.
    // Paré : l'arme repart en arrière et l'ennemi reste étourdi.
    public class MeleeEnemy : MonoBehaviour
    {
        [Header("Attaque")]
        // Objet "attaque" (enfant de l'ennemi) : sert de zone de collision et de visuel
        [FormerlySerializedAs("attaque")]
        [SerializeField] private GameObject attack;
        [SerializeField] private float attackInterval = 3.0f;
        [SerializeField] private float attackDuration = 0.4f;
        [SerializeField] private float damage = 10.0f;
        // Arc de cercle : rayon autour de l'ennemi et angle total balayé (degrés)
        [SerializeField] private float attackRadius = 1.5f;
        [SerializeField] private float arcAngle = 180.0f;

        [Header("Parade")]
        [SerializeField] private float parryReturnDuration = 0.12f; // temps pour que l'attaque reparte en arrière
        [SerializeField] private float stunDuration = 1.0f;         // ennemi étourdi : pas d'attaque pendant ce temps
        [SerializeField] private float parryFlashDuration = 0.08f;  // flash blanc de l'arme

        [Header("Modèle")]
        // Modèle 3D de l'ennemi (ex: FreeLichPolyart), instancié comme enfant au démarrage
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Vector3 modelOffset = new Vector3(0f, -0.5f, 0f);
        [SerializeField] private float modelScale = 1.0f;
        [SerializeField] private bool hidePlaceholder = true; // cache le cube d'origine

        [Header("Animation")]
        // État joué (Attack01 ou Attack02) et temps d'armement avant le coup
        [SerializeField] private string attackAnim = "Attack02";
        [SerializeField] private float windupDuration = 0.35f; // l'ennemi arme son coup (signal pour parer)

        private Collider attackCollider;
        private PlayerController target;
        private Transform model;
        private Animator animator;

        private void Start()
        {
            if (attack == null)
            {
                Debug.LogError($"{nameof(MeleeEnemy)}: objet 'attack' non assigné.", this);
                enabled = false;
                return;
            }

            attackCollider = attack.GetComponent<Collider>();
            target = FindFirstObjectByType<PlayerController>();
            SpawnModel();
            attack.SetActive(false);
            StartCoroutine(AttackLoop());
        }

        private void Update()
        {
            // Le modèle regarde vers le joueur (axe X du jeu 2D)
            if (model != null && target != null)
            {
                float side = target.transform.position.x < transform.position.x ? -1f : 1f;
                model.rotation = Quaternion.Euler(0f, side * 90f, 0f);
            }
        }

        private void SpawnModel()
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
        private void SyncAnimSpeed(string clipName, float duration)
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
        private void PlayAnim(string state, float fade = 0.1f)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (string stateName in new[] { state, state.ToLower() })
            {
                int hash = Animator.StringToHash(stateName);
                if (animator.HasState(0, hash))
                {
                    animator.CrossFadeInFixedTime(hash, fade, 0);
                    return;
                }
            }
        }

        private IEnumerator AttackLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(attackInterval);
                yield return AttackPlayer();
            }
        }

        private IEnumerator AttackPlayer()
        {
            // Côté du joueur : +1 à droite, -1 à gauche
            float side = (target != null && target.transform.position.x < transform.position.x) ? -1f : 1f;
            float startAngle = 90f - side * (90f - arcAngle / 2f);
            float endAngle = startAngle - side * arcAngle;

            // Armement : l'animation démarre avant le coup, calée sur armement + coup
            PlayAnim(attackAnim, 0.05f);
            SyncAnimSpeed(attackAnim, windupDuration + attackDuration);
            yield return new WaitForSeconds(windupDuration);

            PlaceAttack(startAngle);
            attack.SetActive(true);

            bool hasHit = false;
            bool wasParried = false;
            float currentAngle = startAngle;
            float timer = 0f;
            while (timer < attackDuration && !wasParried)
            {
                currentAngle = Mathf.Lerp(startAngle, endAngle, timer / attackDuration);
                PlaceAttack(currentAngle);
                Physics.SyncTransforms();

                if (!hasHit && attackCollider != null)
                {
                    Bounds b = attackCollider.bounds;
                    Collider[] hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity);
                    foreach (Collider hit in hits)
                    {
                        PlayerController p = hit.GetComponentInParent<PlayerController>();
                        if (p != null)
                        {
                            wasParried = !p.TakeDamage(damage, transform, attack.transform.position);
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

            attack.SetActive(false);
            if (animator != null) animator.speed = 1f;
            PlayAnim("Idle", 0.2f);
        }

        // Attaque parée : l'arme flashe et repart vite vers son point de départ, puis l'ennemi reste étourdi
        private IEnumerator Parried(float fromAngle, float toAngle)
        {
            HitFlash.Play(attack, parryFlashDuration);
            if (animator != null) animator.speed = 1f;
            PlayAnim("GetHit", 0.05f);

            float timer = 0f;
            while (timer < parryReturnDuration)
            {
                PlaceAttack(Mathf.Lerp(fromAngle, toAngle, timer / parryReturnDuration));
                timer += Time.deltaTime;
                yield return null;
            }
            attack.SetActive(false);

            yield return new WaitForSeconds(stunDuration);
        }

        private void PlaceAttack(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * attackRadius;
            attack.transform.position = transform.position + offset;
            attack.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
