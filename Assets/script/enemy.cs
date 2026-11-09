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

    private Collider attaqueCollider;
    private player target;

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
        attaque.SetActive(false);
        StartCoroutine(AttackLoop());
    }

    // Update is called once per frame
    void Update()
    {

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

        PlaceAttaque(startAngle);
        attaque.SetActive(true);

        bool hasHit = false;
        float timer = 0f;
        while (timer < attackDuration)
        {
            // Balayage en arc de cercle du haut vers le bas, devant l'ennemi
            PlaceAttaque(Mathf.Lerp(startAngle, endAngle, timer / attackDuration));
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
                        p.TakeDamage(damage);
                        print("Player hit by attack! Life: " + p.life);
                        hasHit = true; // un seul coup par attaque
                        break;
                    }
                }
            }
            timer += Time.deltaTime;
            yield return null;
        }

        attaque.SetActive(false);
    }

    // Place l'objet attaque sur le cercle autour de l'ennemi (plan XY), orienté vers l'extérieur
    void PlaceAttaque(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * attackRadius;
        attaque.transform.position = transform.position + offset;
        attaque.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
