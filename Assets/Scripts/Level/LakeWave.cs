using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // La vague du lac : quand le chat passe 'triggerX', une vague part de 'startX' et roule vers la gauche.
    // Elle frappe le chat (dégâts, il est repoussé) et repart du large, sauf s'il pare : elle s'ouvre en deux
    // et le lac reste calme. L'objet porte la vague ; son x est piloté ici.
    [ExecuteAlways]
    public class LakeWave : MonoBehaviour
    {
        [SerializeField] private float triggerX = -9f;
        [SerializeField] private float startX = 4f;
        [SerializeField] private float speed = 6f;
        [SerializeField] private float height = 3.2f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float pause = 1.4f;         // avant de repartir du large

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private enum State { Waiting, Rolling, Resting, Split }
        private State state;
        private float x;
        private float timer;
        private float split;
        private Transform left, right;

        private void OnEnable()
        {
            generated.DestroyAll();
            Material water = generated.Track(MaterialFactory.Lit(new Color(0.2f, 0.45f, 0.7f)));
            water.EnableKeyword("_EMISSION");
            water.SetColor("_EmissionColor", new Color(0.05f, 0.15f, 0.25f));
            Material foam = generated.Track(MaterialFactory.Unlit(new Color(0.85f, 0.95f, 1f)));
            Mesh wedge = generated.Track(LowPoly.Prism(3, 1f, 0f, 1f));
            left = generated.Track(new GameObject("WaveL")).transform; left.SetParent(transform, false);
            right = generated.Track(new GameObject("WaveR")).transform; right.SetParent(transform, false);
            foreach (Transform half in new[] { left, right })
            {
                LevelPlayer.Part(generated, half, generated.Track(LowPoly.Box(0.4f)), water, new Vector3(0.6f, height * 0.45f, 0f), new Vector3(1.6f, height * 0.9f, 2.5f));
                LevelPlayer.Part(generated, half, wedge, foam, new Vector3(-0.1f, height * 0.85f, 0f), new Vector3(0.5f, 0.5f, 1.2f), new Vector3(0f, 0f, 70f));
            }
            right.localPosition = Vector3.zero;
            x = startX;
            state = State.Waiting;
            Place();
        }

        private void OnDisable() => generated.DestroyAll();

        private void Place()
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            float s = state == State.Waiting || state == State.Resting ? 0.15f : 1f;
            transform.localScale = new Vector3(1f, s, 1f);
            if (left != null) left.localPosition = new Vector3(0f, 0f, -split);
            if (right != null) right.localPosition = new Vector3(0f, 0f, split);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            PlayerController p = LevelPlayer.Get();
            switch (state)
            {
                case State.Waiting:
                    if (LevelPlayer.Active(p) && p.transform.position.x > triggerX) { state = State.Rolling; x = startX; }
                    break;
                case State.Rolling:
                    x -= speed * Time.deltaTime;
                    if (LevelPlayer.Active(p) && p.HitBounds.max.x > x - 0.2f && p.HitBounds.min.x < x + 1.5f && p.HitBounds.min.y < transform.position.y + height)
                    {
                        if (LevelPlayer.HitParried(p, damage, transform, new Vector3(x, p.HitCenter.y, 0f)))
                        {
                            // Parée : la vague s'ouvre en deux, le lac reste calme
                            state = State.Split;
                            ParticleBurst.Spawn("WaveSplit", new Vector3(x, transform.position.y + height * 0.6f, 0f), new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.85f, 1f), Color.white), 60,
                                lifetime: new Vector2(0.4f, 1f), speed: new Vector2(2f, 7f), size: new Vector2(0.06f, 0.16f), gravity: 1.2f, radius: 0.6f);
                            GameCamera.ShakeMain(0.3f, 0.15f);
                        }
                        else
                        {
                            p.Knockback(new Vector3(-9f, 5f, 0f));
                            state = State.Resting;
                            timer = pause;
                        }
                    }
                    if (x < triggerX - 14f) { state = State.Resting; timer = pause; }
                    break;
                case State.Resting:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { state = State.Rolling; x = startX; }
                    break;
                case State.Split:
                    split = Mathf.MoveTowards(split, 3f, Time.deltaTime * 4f);
                    transform.localScale = new Vector3(1f, Mathf.MoveTowards(transform.localScale.y, 0f, Time.deltaTime * 0.6f), 1f);
                    if (left != null) left.localPosition = new Vector3(0f, 0f, -split);
                    if (right != null) right.localPosition = new Vector3(0f, 0f, split);
                    return;
            }
            Place();
        }
    }
}
