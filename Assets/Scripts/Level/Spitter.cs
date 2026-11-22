using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Statue cracheuse qui barre le passage (son BoxCollider bloque le chat). Quand il approche, elle tire une
    // boule d'âme sur lui (du côté 'direction'). Renvoyée (ParryShot), la boule la fait éclater : le chemin
    // s'ouvre. Signal : l'œil s'allume juste avant le tir. Origine = pied de la statue.
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider))]
    public class Spitter : MonoBehaviour
    {
        [SerializeField] private float direction = -1f;      // -1 : tire vers la gauche
        [SerializeField] private float range = 14f;           // distance d'éveil
        [SerializeField] private float interval = 2.6f;
        [SerializeField] private float warn = 0.5f;           // l'œil s'allume avant le tir
        [SerializeField] private float shotSpeed = 7f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private Material material;

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Material eyeMat;
        private float timer;

        private Vector3 Muzzle => transform.position + new Vector3(direction * 0.55f, 1.6f, 0f);

        private void OnEnable() => Build();
        private void OnDisable() => generated.DestroyAll();

        private void Build()
        {
            generated.DestroyAll();
            BoxCollider box = GetComponent<BoxCollider>();
            box.size = new Vector3(1.1f, 2.3f, 3f);
            box.center = new Vector3(0f, 1.15f, 0f);
            Material stone = LevelPlayer.Or(material, new Color(0.45f, 0.47f, 0.55f), generated);
            eyeMat = generated.Track(MaterialFactory.Unlit(new Color(0.15f, 0.2f, 0.25f)));
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Prism(6, 0.55f, 0.42f, 1.5f)), stone, Vector3.zero, Vector3.one);
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Icosahedron(0.05f, 5)), stone, new Vector3(0f, 1.75f, 0f), Vector3.one * 0.9f);
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Prism(6, 0.16f, 0.2f, 0.4f)), stone, new Vector3(direction * 0.42f, 1.6f, 0f), Vector3.one, new Vector3(0f, 0f, direction * 90f));
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Icosahedron(0f, 1)), eyeMat, new Vector3(direction * 0.2f, 1.9f, -0.35f), Vector3.one * 0.2f);
            timer = interval;
        }

        // Touchée par sa propre boule renvoyée : elle éclate, le passage est libre
        public void Shatter()
        {
            ParticleBurst.Spawn("SpitterShatter", transform.position + Vector3.up * 1.2f, new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.58f, 0.66f), new Color(1f, 0.85f, 0.4f)), 80,
                lifetime: new Vector2(0.4f, 1.1f), speed: new Vector2(2f, 8f), size: new Vector2(0.08f, 0.25f), gravity: 1.5f, radius: 0.8f);
            GameCamera.ShakeMain(0.3f, 0.18f);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying || eyeMat == null) return;
            PlayerController p = LevelPlayer.Get();
            bool awake = LevelPlayer.Active(p) && Mathf.Abs(p.transform.position.x - transform.position.x) < range;
            if (!awake)
            {
                eyeMat.color = new Color(0.15f, 0.2f, 0.25f);
                timer = Mathf.Max(timer, warn + 0.3f);
                return;
            }
            timer -= Time.deltaTime;
            eyeMat.color = timer < warn ? Color.Lerp(new Color(0.4f, 0.9f, 1f), Color.white, Mathf.PingPong(Time.time * 10f, 1f)) : new Color(0.3f, 0.6f, 0.7f);
            if (timer <= 0f)
            {
                timer = interval;
                // Vise le chat ; renvoyée, la boule revient droit sur la statue
                Vector3 aim = p.HitCenter - Muzzle;
                aim.z = 0f;
                if (aim.x * direction < 0.5f) aim = new Vector3(direction, 0f, 0f);
                ParryShot.Fire(Muzzle, aim.normalized * shotSpeed, damage, transform.position.y - 0.05f);
            }
        }
    }
}
