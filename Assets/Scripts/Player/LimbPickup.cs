using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Membre arraché tombé au sol : le vrai membre, figé dans sa pose et son état (CatBody), qui tombe et reste là.
    // Pas de repère : à toi de le retrouver. Le joueur le ramasse en passant dessus (il le garde « en poche »),
    // puis le recoud sur place (maintenir ↓) ou au sanctuaire. Ce qui était cousu dessus est tombé avec lui et se
    // ramasse avec lui. Reste là jusqu'à la mort du joueur.
    public class LimbPickup : MonoBehaviour
    {
        private const float GroundSearch = 30f;
        private const float PickupDelay = 0.8f;     // secondes avant de pouvoir le ramasser
        private const float PickupRadius = 0.7f;

        private PlayerBody owner;
        private readonly List<Limb> limbs = new List<Limb>();   // le membre, puis ce qui est tombé avec lui
        private readonly List<Object> owned = new List<Object>(); // maillages et matériaux créés pour lui
        private float age;

        // Rend ramassable un membre déjà construit (objet qui tombe)
        public static LimbPickup Attach(GameObject piece, PlayerBody owner, Limb limb)
        {
            LimbPickup pickup = piece.AddComponent<LimbPickup>();
            pickup.owner = owner;
            pickup.limbs.Add(limb);
            SphereCollider trigger = piece.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = PickupRadius;
            return pickup;
        }

        // Membre tombé avec celui-ci (cousu dessus) : ramassé en même temps
        public void Add(Limb limb)
        {
            if (limb != null && !limbs.Contains(limb)) limbs.Add(limb);
        }

        // Objet créé pour ce membre (maillage figé...) : détruit avec lui
        public void Own(Object obj)
        {
            if (obj != null) owned.Add(obj);
        }

        // Repli, sans modèle à figer : un morceau simple posé au sol sous 'from'
        public static LimbPickup Spawn(PlayerBody owner, Limb limb, Vector3 from, float facing)
        {
            GameObject go = new GameObject("LimbPickup " + limb.Id);
            Vector3 start = from + new Vector3(-facing * 0.6f, 0.3f, 0f);
            start.z = 0f;
            go.transform.position = FindGround(start, owner) + Vector3.up * 0.08f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f + Random.Range(-25f, 25f));

            GameObject mesh = new GameObject("Mesh");
            mesh.transform.SetParent(go.transform, false);
            bool flesh = limb.IsTorsoPart;
            float length = limb.Kind == LimbKind.Foot || limb.Kind == LimbKind.Hand ? 0.18f
                : limb.Kind == LimbKind.Shin || limb.Kind == LimbKind.Leg || limb.Kind == LimbKind.Forearm ? 0.3f : 0.42f;
            float bulk = limb.Kind == LimbKind.Flesh ? 1f : 0.7f;
            mesh.transform.localScale = flesh ? new Vector3(0.5f, 0.6f, 0.45f) * bulk : new Vector3(0.08f, length, 0.08f);
            mesh.transform.localPosition = flesh ? new Vector3(0f, 0.15f, 0f) : new Vector3(0f, 0.2f, 0f);
            mesh.AddComponent<MeshFilter>().sharedMesh = flesh ? CatBody.LumpMesh : CatBody.LegMesh;
            Material material = MaterialFactory.Lit(new Color(0.35f, 0.08f, 0.08f));
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            LimbPickup pickup = Attach(go, owner, limb);
            pickup.Own(material);
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
            age += Time.deltaTime;
            // Recousu autrement (sanctuaire...) ou joueur disparu : plus rien à ramasser
            Limb main = limbs.Count > 0 ? limbs[0] : null;
            if (owner == null || main == null || !main.IsTorn || main.IsPocketed) Destroy(gameObject);
        }

        // Stay plutôt qu'Enter : le membre tombe souvent sur le joueur, il n'est ramassable qu'après un court délai
        private void OnTriggerStay(Collider other)
        {
            if (age < PickupDelay || owner == null || other.GetComponentInParent<PlayerBody>() != owner) return;
            foreach (Limb limb in limbs)
            {
                if (limb.IsTorn && !limb.IsPocketed) owner.PickUp(limb);
            }
            ParticleBurst.Spawn("LimbPickedUp", transform.position, new Color(0.9f, 0.3f, 0.25f), 25,
                lifetime: new Vector2(0.2f, 0.45f), speed: new Vector2(1.5f, 4f), size: new Vector2(0.04f, 0.1f),
                gravity: 0.5f, radius: 0.2f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            foreach (Object obj in owned)
            {
                if (obj != null) Destroy(obj);
            }
        }
    }
}
