using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Porte-guillotine : la lame remonte, attend, tremble (signal), puis s'abat. En bas, elle barre le passage.
    // Le chat dessous au moment du coup est blessé, sauf s'il pare : la lame reste alors coincée en haut,
    // le passage est ouvert pour de bon. Origine = bas du cadre, au sol.
    [ExecuteAlways]
    public class Guillotine : MonoBehaviour
    {
        [SerializeField] private float width = 1.4f;
        [SerializeField] private float height = 4.2f;
        [SerializeField] private float openTime = 1.8f;
        [SerializeField] private float warnTime = 0.45f;
        [SerializeField] private float closedTime = 1.0f;
        [SerializeField] private float damage = 15f;
        [SerializeField] private Material frameMaterial;
        [SerializeField] private Material bladeMaterial;

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Transform blade;
        private BoxCollider bladeCollider;
        private float t;
        private bool jammed;

        private float Cycle => openTime + warnTime + 0.12f + closedTime + 0.5f;

        private void OnEnable()
        {
            generated.DestroyAll();
            Material frame = LevelPlayer.Or(frameMaterial, new Color(0.3f, 0.3f, 0.36f), generated);
            Material steel = LevelPlayer.Or(bladeMaterial, new Color(0.7f, 0.72f, 0.78f), generated);
            Mesh box = generated.Track(LowPoly.Box());
            LevelPlayer.Part(generated, transform, box, frame, new Vector3(-width / 2f - 0.15f, height / 2f + 0.3f, 0f), new Vector3(0.3f, height + 0.6f, 0.5f));
            LevelPlayer.Part(generated, transform, box, frame, new Vector3(width / 2f + 0.15f, height / 2f + 0.3f, 0f), new Vector3(0.3f, height + 0.6f, 0.5f));
            LevelPlayer.Part(generated, transform, box, frame, new Vector3(0f, height + 0.45f, 0f), new Vector3(width + 0.6f, 0.3f, 0.5f));
            blade = generated.Track(new GameObject("Blade")).transform;
            blade.SetParent(transform, false);
            LevelPlayer.Part(generated, blade, box, steel, new Vector3(0f, 0.7f, 0f), new Vector3(width, 1.4f, 0.2f));
            LevelPlayer.Part(generated, blade, generated.Track(LowPoly.Prism(3, 0.3f, 0f, width)), steel, new Vector3(-width / 2f, 0.05f, 0f), new Vector3(1f, 1f, 0.5f), new Vector3(0f, 0f, -90f));
            bladeCollider = blade.gameObject.AddComponent<BoxCollider>();
            bladeCollider.size = new Vector3(width, 1.4f, 3f);
            bladeCollider.center = new Vector3(0f, 0.7f, 0f);
            SetBlade(Application.isPlaying ? 1f : 0f);
        }

        private void OnDisable() => generated.DestroyAll();

        // open : 1 = en haut, 0 = en bas
        private void SetBlade(float open)
        {
            if (blade == null) return;
            blade.localPosition = new Vector3(0f, Mathf.Lerp(0f, height - 1.4f, open), 0f);
            bladeCollider.enabled = open < 0.5f;
        }

        private void Update()
        {
            if (!Application.isPlaying || blade == null) return;
            if (jammed)
            {
                SetBlade(1f);
                blade.localPosition += new Vector3(Mathf.Sin(Time.time * 40f) * 0.01f, 0f, 0f);
                return;
            }
            float prev = t;
            t = Mathf.Repeat(t + Time.deltaTime, Cycle);
            float strikeAt = openTime + warnTime;
            if (t < openTime) SetBlade(1f);
            else if (t < strikeAt)
            {
                SetBlade(1f);
                blade.localPosition += new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0f, 0.05f), 0f);   // tremble : signal
            }
            else if (t < strikeAt + 0.12f) SetBlade(1f - (t - strikeAt) / 0.12f);
            else if (t < strikeAt + 0.12f + closedTime) SetBlade(0f);
            else SetBlade((t - strikeAt - 0.12f - closedTime) / 0.5f);

            // Le coup tombe
            if (prev < strikeAt + 0.08f && t >= strikeAt + 0.08f) Strike();
        }

        private void Strike()
        {
            GameCamera.ShakeMain(0.12f, 0.08f);
            PlayerController p = LevelPlayer.Get();
            if (!LevelPlayer.Active(p)) return;
            Bounds b = p.HitBounds;
            Vector3 o = transform.position;
            bool under = b.max.x > o.x - width / 2f && b.min.x < o.x + width / 2f && b.min.y < o.y + height;
            if (!under) return;
            Vector3 point = new Vector3(p.transform.position.x, b.max.y, 0f);
            if (LevelPlayer.HitParried(p, damage, transform, point))
            {
                // Parée : la lame se coince en haut
                jammed = true;
                ParticleBurst.Spawn("GuillotineJam", o + Vector3.up * (height - 1f), new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f), Color.white), 30,
                    lifetime: new Vector2(0.2f, 0.5f), speed: new Vector2(2f, 6f), size: new Vector2(0.04f, 0.1f), gravity: 1f, radius: 0.4f);
            }
            else
            {
                p.Knockback(new Vector3(-Mathf.Sign(p.Facing) * 7f, 5f, 0f));
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * height / 2f, new Vector3(width, height, 1f));
        }
    }
}
