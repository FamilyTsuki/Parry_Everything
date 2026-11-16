using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Sang qui goutte d'une plaie ouverte (moignon d'un membre arraché, ou le membre tombé) : gouttes de plus en plus
    // espacées pendant 'duration' secondes. Avec flaque : une fois l'objet posé (Rigidbody presque immobile), une petite
    // flaque sombre s'étale lentement au sol dessous.
    public class BloodDrip : MonoBehaviour
    {
        public static readonly Color Blood = new Color(0.4f, 0.02f, 0.03f);
        private static Material poolMaterial;

        private const float FirstInterval = 0.07f;   // goutte à goutte serré au début...
        private const float LastInterval = 0.55f;    // ... de plus en plus espacé
        private const float PoolRadius = 0.22f;
        private const float PoolGrowTime = 2.5f;

        private Vector3 localOrigin;     // point de la plaie, dans le repère de l'objet (il suit sa rotation)
        private float duration;
        private float elapsed;
        private float timer;
        private bool withPool;
        private bool pooled;
        private bool destroyHost;
        private Rigidbody body;
        private Transform ignored;       // le joueur : la flaque ne se pose pas sur lui

        // host : objet qui saigne ; worldOrigin : la plaie ; destroyHostWhenDone : objet créé juste pour saigner (moignon)
        public static BloodDrip Begin(GameObject host, Vector3 worldOrigin, float duration, bool pool,
                                      bool destroyHostWhenDone, Transform ignore = null)
        {
            BloodDrip drip = host.AddComponent<BloodDrip>();
            drip.localOrigin = host.transform.InverseTransformPoint(worldOrigin);
            drip.duration = duration;
            drip.withPool = pool;
            drip.destroyHost = destroyHostWhenDone;
            drip.body = host.GetComponent<Rigidbody>();
            drip.ignored = ignore;
            return drip;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Vector3 origin = transform.TransformPoint(localOrigin);

            if (elapsed < duration)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    float t = elapsed / duration;
                    timer = Mathf.Lerp(FirstInterval, LastInterval, t * t) * Random.Range(0.7f, 1.3f);
                    ParticleBurst.Spawn("BloodDrip", origin, Blood, Random.Range(1, 3),
                        lifetime: new Vector2(0.5f, 0.9f), speed: new Vector2(0.05f, 0.35f), size: new Vector2(0.025f, 0.05f),
                        gravity: 1.5f, radius: 0.03f);
                }
            }

            if (withPool && !pooled && elapsed > 0.4f && (body == null || body.linearVelocity.sqrMagnitude < 0.04f))
            {
                pooled = true;
                SpawnPool(origin);
            }

            if (elapsed >= duration && (!withPool || pooled))
            {
                if (destroyHost) Destroy(gameObject);
                else Destroy(this);
            }
        }

        // Flaque : disque très plat posé sur le premier sol sous la plaie, qui s'étale
        private void SpawnPool(Vector3 origin)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin + Vector3.up * 0.3f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit? ground = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (ignored != null && hit.collider.transform.IsChildOf(ignored)) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                ground = hit;
            }
            if (!ground.HasValue) return;

            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "BloodPool";
            Destroy(pool.GetComponent<Collider>());
            if (poolMaterial == null) poolMaterial = MaterialFactory.Lit(new Color(0.22f, 0.01f, 0.02f));
            pool.GetComponent<MeshRenderer>().sharedMaterial = poolMaterial;
            pool.transform.position = ground.Value.point + ground.Value.normal * 0.004f;
            pool.transform.up = ground.Value.normal;
            pool.AddComponent<BloodPool>().Grow(PoolRadius * Random.Range(0.8f, 1.2f), PoolGrowTime);
        }
    }
}
