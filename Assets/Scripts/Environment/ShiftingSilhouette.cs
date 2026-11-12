using UnityEngine;

namespace ParryEverything.Environment
{
    // Silhouette qui hante la brume, avec sa propre routine, sans rien savoir du joueur :
    // elle reste immobile longtemps, tourne lentement la tête, puis marche à pas lents
    // vers un autre point de son territoire et s'y arrête à nouveau.
    public class ShiftingSilhouette : MonoBehaviour
    {
        [SerializeField] private MeshFilter ground;                                  // sol sur lequel marcher
        [SerializeField] private Transform head;                                     // tourne lentement (optionnel)
        [SerializeField] private float homeRange = 9f;                               // distance max au point de départ, sur X
        [SerializeField] private Vector2 depthRange = new Vector2(8.5f, 11.5f);      // Z possibles
        [SerializeField] private Vector2 standTime = new Vector2(15f, 50f);
        [SerializeField] private float walkSpeed = 0.35f;

        private Vector3 home;
        private Vector3 target;
        private float standLeft;
        private bool walking;
        private float headYaw, headYawGoal, headTimer;
        private Quaternion headBase;

        private void Start()
        {
            home = transform.position;
            standLeft = Random.Range(standTime.x, standTime.y) * Random.value;
            if (head != null) headBase = head.localRotation;
        }

        private void Update()
        {
            UpdateHead();

            if (!walking)
            {
                standLeft -= Time.deltaTime;
                if (standLeft <= 0f) PickTarget();
                return;
            }

            Vector3 p = transform.position;
            Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
            float step = walkSpeed * Time.deltaTime;
            if (flat.magnitude <= step)
            {
                walking = false;
                standLeft = Random.Range(standTime.x, standTime.y);
                return;
            }

            Vector3 dir = flat.normalized;
            p += dir * step;
            if (GroundHeight.TryGet(ground, p.x, p.z, out float y)) p.y = y;
            transform.position = p;
            // Se tourne dans le sens de la marche, lentement
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 25f * Time.deltaTime);
        }

        private void PickTarget()
        {
            float x = home.x + Random.Range(-homeRange, homeRange);
            float z = Random.Range(depthRange.x, depthRange.y);
            if (!GroundHeight.TryGet(ground, x, z, out float y))
            {
                standLeft = 5f;
                return;
            }
            target = new Vector3(x, y, z);
            walking = true;
        }

        // La tête regarde à droite, à gauche, parfois longtemps dans la même direction
        private void UpdateHead()
        {
            if (head == null) return;
            headTimer -= Time.deltaTime;
            if (headTimer <= 0f)
            {
                headTimer = Random.Range(3f, 14f);
                headYawGoal = Random.value < 0.3f ? 0f : Random.Range(-70f, 70f);
            }
            headYaw = Mathf.MoveTowards(headYaw, headYawGoal, 12f * Time.deltaTime);
            head.localRotation = headBase * Quaternion.Euler(0f, headYaw, 0f);
        }
    }
}
