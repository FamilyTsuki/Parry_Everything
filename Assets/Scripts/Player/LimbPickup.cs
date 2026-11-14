using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Membre arraché tombé au sol. Le joueur le ramasse en passant dessus (il le garde « en poche »),
    // puis le recoud sur place (maintenir ↓) ou au sanctuaire. Reste là jusqu'à la mort du joueur.
    public class LimbPickup : MonoBehaviour
    {
        private const float GroundSearch = 30f;
        private const float PickupDelay = 0.8f;     // secondes avant de pouvoir le ramasser

        private PlayerBody owner;
        private Limb limb;
        private Material material;
        private Vector3 restPosition;
        private float age;

        // Crée le membre au sol sous 'from' (le point d'attache au moment de l'arrachage)
        public static LimbPickup Spawn(PlayerBody owner, Limb limb, Vector3 from, float facing)
        {
            GameObject go = new GameObject("LimbPickup " + limb.Id);
            LimbPickup pickup = go.AddComponent<LimbPickup>();
            pickup.owner = owner;
            pickup.limb = limb;

            // Projetée un peu en arrière, couchée au sol
            Vector3 start = from + new Vector3(-facing * 0.6f, 0.3f, 0f);
            start.z = 0f;
            pickup.restPosition = FindGround(start, owner) + Vector3.up * 0.08f;
            go.transform.position = pickup.restPosition;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f + Random.Range(-25f, 25f));

            // Patte couchée au sol, ou chair éclatée (un gros morceau)
            GameObject mesh = new GameObject("Mesh");
            mesh.transform.SetParent(go.transform, false);
            bool flesh = limb.Kind == LimbKind.Flesh;
            mesh.transform.localScale = flesh ? new Vector3(0.5f, 0.6f, 0.45f) : new Vector3(0.08f, 0.42f, 0.08f);
            mesh.transform.localPosition = flesh ? new Vector3(0f, 0.15f, 0f) : new Vector3(0f, 0.2f, 0f);
            mesh.AddComponent<MeshFilter>().sharedMesh = flesh ? CatBody.LumpMesh : CatBody.LegMesh;
            pickup.material = MaterialFactory.Lit(new Color(0.6f, 0.1f, 0.1f));
            mesh.AddComponent<MeshRenderer>().sharedMaterial = pickup.material;

            SphereCollider trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;
            return pickup;
        }

        // Premier sol sous le point (en ignorant le joueur) ; à défaut, le point lui-même
        private static Vector3 FindGround(Vector3 start, PlayerBody owner)
        {
            RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, GroundSearch, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = start;
            foreach (RaycastHit hit in hits)
            {
                if (owner != null && hit.collider.GetComponentInParent<PlayerBody>() == owner) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }
            return point;
        }

        private void Update()
        {
            // Recousu autrement (sanctuaire...) ou joueur disparu : plus rien à ramasser
            if (owner == null || limb == null || !limb.IsTorn || limb.IsPocketed)
            {
                Destroy(gameObject);
                return;
            }
            // Légère pulsation rouge : on le repère de loin
            age += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 4f);
            material.color = Color.Lerp(new Color(0.45f, 0.07f, 0.07f), new Color(0.95f, 0.25f, 0.2f), pulse);
            transform.position = restPosition + Vector3.up * (0.04f * pulse);
        }

        // Stay plutôt qu'Enter : le membre tombe souvent sur le joueur, il n'est ramassable qu'après un court délai
        private void OnTriggerStay(Collider other)
        {
            if (age < PickupDelay || owner == null || other.GetComponentInParent<PlayerBody>() != owner) return;
            owner.PickUp(limb);
            ParticleBurst.Spawn("LimbPickedUp", transform.position, new Color(0.9f, 0.3f, 0.25f), 25,
                lifetime: new Vector2(0.2f, 0.45f), speed: new Vector2(1.5f, 4f), size: new Vector2(0.04f, 0.1f),
                gravity: 0.5f, radius: 0.2f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
