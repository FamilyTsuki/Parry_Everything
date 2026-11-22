using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Âme qui tombe du ciel quand le chat approche ; elle descend lentement vers l'endroit où il se tient.
    // Parer au contact la rattrape doucement (elle s'envole, apaisée) ; la heurter sans parer la fait éclater
    // (compte comme une âme bousculée). Une seule fois.
    public class FallingSoul : MonoBehaviour
    {
        [SerializeField] private float triggerRange = 5f;
        [SerializeField] private float dropHeight = 9f;
        [SerializeField] private float fallSpeed = 1.6f;

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Transform soul;
        private bool falling, done;

        private void OnDisable() => generated.DestroyAll();

        private void Update()
        {
            PlayerController p = LevelPlayer.Get();
            if (done || !LevelPlayer.Active(p)) return;
            if (!falling)
            {
                if (Mathf.Abs(p.transform.position.x - transform.position.x) > triggerRange) return;
                falling = true;
                soul = generated.Track(new GameObject("FallingSoul")).transform;
                soul.position = new Vector3(p.transform.position.x + p.Facing * 1.5f, transform.position.y + dropHeight, 0f);
                Material m = generated.Track(MaterialFactory.Unlit(new Color(0.8f, 0.88f, 1f)));
                LevelPlayer.Part(generated, soul, generated.Track(LowPoly.Icosahedron(0.1f, 9)), m, Vector3.zero, Vector3.one * 0.55f);
                return;
            }
            // Descend en dérivant vers le chat
            Vector3 s = soul.position;
            s.y -= fallSpeed * Time.deltaTime;
            s.x = Mathf.MoveTowards(s.x, p.transform.position.x, Time.deltaTime * 0.8f);
            s.x += Mathf.Sin(Time.time * 2f) * 0.01f;
            soul.position = s;

            if (Vector2.Distance(s, p.HitCenter) < 0.8f)
            {
                done = true;
                if (p.IsParrying())
                {
                    // Rattrapée : elle s'apaise et repart vers le haut
                    ParticleBurst.Spawn("SoulCaught", s, new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.9f, 1f), Color.white), 30,
                        lifetime: new Vector2(0.6f, 1.4f), speed: new Vector2(0.5f, 2f), size: new Vector2(0.05f, 0.14f), gravity: -0.5f, radius: 0.3f);
                }
                else
                {
                    BehaviorLedger.AddWanderingSoulBumped();
                    ParticleBurst.Spawn("SoulBurst", s, new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.8f, 1f, 0.6f), Color.white), 30,
                        lifetime: new Vector2(0.3f, 0.8f), speed: new Vector2(2f, 5f), size: new Vector2(0.05f, 0.12f), gravity: 0.4f, radius: 0.2f);
                }
                generated.DestroyAll();
                return;
            }
            if (s.y < transform.position.y - 1f)
            {
                done = true;
                generated.DestroyAll();
            }
        }
    }
}
