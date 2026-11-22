using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Plateforme qui cède : quand le chat se pose dessus, elle craque (tremble) puis tombe. Parer au moment où
    // elle cède la fige en l'air quelques secondes (elle luit), le temps de passer. Elle revient ensuite.
    // L'objet porte le BoxCollider et le rendu de la plateforme.
    [RequireComponent(typeof(BoxCollider))]
    public class CrumblePlatform : MonoBehaviour
    {
        [SerializeField] private float crackTime = 0.8f;
        [SerializeField] private float frozenTime = 3f;
        [SerializeField] private float respawnTime = 4f;

        private enum State { Idle, Cracking, Frozen, Falling, Gone }
        private State state;
        private float timer;
        private Vector3 home;
        private BoxCollider box;
        private Renderer rend;
        private Color baseColor;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            home = transform.position;
            box = GetComponent<BoxCollider>();
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            baseColor = rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color") ? rend.sharedMaterial.color : Color.white;
        }

        private bool PlayerOnTop(PlayerController p)
        {
            if (!LevelPlayer.Active(p)) return false;
            Bounds b = box.bounds, pb = p.HitBounds;
            return pb.max.x > b.min.x && pb.min.x < b.max.x && Mathf.Abs(pb.min.y - b.max.y) < 0.25f;
        }

        private void Update()
        {
            PlayerController p = LevelPlayer.Get();
            switch (state)
            {
                case State.Idle:
                    if (PlayerOnTop(p)) { state = State.Cracking; timer = crackTime; }
                    break;
                case State.Cracking:
                    timer -= Time.deltaTime;
                    transform.position = home + new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.03f, 0.03f), 0f);
                    if (timer <= 0f)
                    {
                        transform.position = home;
                        if (p != null && p.IsParrying())
                        {
                            state = State.Frozen;
                            timer = frozenTime;
                            Tint(new Color(0.5f, 0.85f, 1f));
                            ParticleBurst.Spawn("PlatformFrozen", box.bounds.center, new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.85f, 1f), Color.white), 20,
                                lifetime: new Vector2(0.2f, 0.5f), speed: new Vector2(1f, 3f), size: new Vector2(0.04f, 0.1f), gravity: 0f, radius: 0.5f);
                        }
                        else Fall();
                    }
                    break;
                case State.Frozen:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Fall();
                    break;
                case State.Falling:
                    timer -= Time.deltaTime;
                    transform.position += Vector3.down * 9f * Time.deltaTime;
                    if (timer <= 0f) { state = State.Gone; timer = respawnTime; if (rend != null) rend.enabled = false; }
                    break;
                case State.Gone:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        transform.position = home;
                        box.enabled = true;
                        if (rend != null) rend.enabled = true;
                        Tint(Color.clear);
                        state = State.Idle;
                    }
                    break;
            }
        }

        private void Fall()
        {
            state = State.Falling;
            timer = 0.8f;
            box.enabled = false;
            Tint(Color.clear);
            ParticleBurst.Spawn("PlatformCrumble", box.bounds.center, new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.38f, 0.36f), new Color(0.6f, 0.55f, 0.5f)), 30,
                lifetime: new Vector2(0.4f, 0.9f), speed: new Vector2(1f, 3f), size: new Vector2(0.06f, 0.18f), gravity: 1.5f, radius: 0.6f);
        }

        // Teinte (Color.clear = couleur d'origine), sur ce rendu seulement
        private void Tint(Color c)
        {
            if (rend == null) return;
            rend.GetPropertyBlock(block);
            block.SetColor("_Color", c.a > 0f ? c : baseColor);
            rend.SetPropertyBlock(block);
        }
    }
}
