using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Statue-gardien : son regard (un faisceau) balaie le sol d'un côté à l'autre. Pris dedans, le chat brûle
    // (petits dégâts réguliers) ; parer dans le faisceau renvoie le regard : la statue s'aveugle pour de bon.
    // Origine = pied du socle ; le faisceau part de l'œil, en haut.
    [ExecuteAlways]
    public class GazeStatue : MonoBehaviour
    {
        [SerializeField] private float height = 4.5f;
        [SerializeField] private float sweepFrom = -5f;      // bout du balayage au sol (x relatif)
        [SerializeField] private float sweepTo = 5f;
        [SerializeField] private float sweepTime = 3.2f;     // aller simple
        [SerializeField] private float beamWidth = 1.6f;     // largeur du faisceau au sol
        [SerializeField] private float damage = 6f;
        [SerializeField] private float tick = 0.5f;
        [SerializeField] private Material material;

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Material eyeMat, beamMat;
        private MeshFilter beam;
        private float hitTimer;
        private bool blind;

        private Vector3 Eye => transform.position + new Vector3(0f, height, -0.3f);

        private void OnEnable()
        {
            generated.DestroyAll();
            Material stone = LevelPlayer.Or(material, new Color(0.42f, 0.42f, 0.48f), generated);
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Box()), stone, new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1f, 1.2f));
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Prism(6, 0.55f, 0.4f, height - 1.4f)), stone, new Vector3(0f, 1f, 0f), Vector3.one);
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Prism(6, 0.45f, 0.05f, 0.9f)), stone, new Vector3(0f, height - 0.4f, 0f), Vector3.one);
            eyeMat = generated.Track(MaterialFactory.Unlit(new Color(1f, 0.88f, 0.45f)));
            LevelPlayer.Part(generated, transform, generated.Track(LowPoly.Icosahedron(0f, 2)), eyeMat, new Vector3(0f, height, -0.42f), Vector3.one * 0.32f);
            beamMat = generated.Track(MaterialFactory.Unlit(new Color(1f, 0.88f, 0.45f, 0.22f)));
            GameObject b = generated.Track(new GameObject("Beam"));
            b.transform.SetParent(transform, false);
            beam = b.AddComponent<MeshFilter>();
            beam.sharedMesh = generated.Track(new Mesh { name = "GazeBeam" });
            b.AddComponent<MeshRenderer>().sharedMaterial = beamMat;
            DrawBeam(0f);
        }

        private void OnDisable() => generated.DestroyAll();

        // Bout du faisceau au sol (x relatif)
        private float Target(float time)
        {
            float u = Mathf.PingPong(time / sweepTime, 1f);
            return Mathf.Lerp(sweepFrom, sweepTo, u * u * (3f - 2f * u));
        }

        private void DrawBeam(float x)
        {
            Mesh m = beam.sharedMesh;
            Vector3 e = new Vector3(0f, height, -0.45f);
            m.Clear();
            m.vertices = new[] { e, new Vector3(x - beamWidth / 2f, 0.02f, -0.45f), new Vector3(x + beamWidth / 2f, 0.02f, -0.45f) };
            m.triangles = new[] { 0, 2, 1, 0, 1, 2 };
            m.RecalculateBounds();
        }

        private void Update()
        {
            if (blind || beam == null) return;
            float x = Target(Application.isPlaying ? Time.time : 0f);
            DrawBeam(x);
            if (!Application.isPlaying) return;

            hitTimer -= Time.deltaTime;
            PlayerController p = LevelPlayer.Get();
            if (!LevelPlayer.Active(p) || hitTimer > 0f) return;
            // Le chat est-il dans le faisceau ? (même position relative entre l'œil et le sol)
            Vector3 c = p.HitCenter - transform.position;
            if (c.y < 0f || c.y > height) return;
            float k = 1f - c.y / height;
            float center = x * k;
            float half = beamWidth / 2f * k + 0.4f;
            if (Mathf.Abs(c.x - center) > half) return;
            hitTimer = tick;
            if (LevelPlayer.HitParried(p, damage, transform, p.HitCenter))
            {
                // Regard renvoyé : la statue s'aveugle
                blind = true;
                beam.gameObject.SetActive(false);
                eyeMat.color = new Color(0.15f, 0.12f, 0.1f);
                ParticleBurst.Spawn("GazeBlind", Eye, new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.45f), Color.white), 40,
                    lifetime: new Vector2(0.3f, 0.8f), speed: new Vector2(1f, 4f), size: new Vector2(0.05f, 0.14f), gravity: 0.2f, radius: 0.3f);
                GameCamera.ShakeMain(0.2f, 0.1f);
            }
        }
    }
}
