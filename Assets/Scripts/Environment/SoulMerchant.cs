using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Environment
{
    // L'âme marchande, au bord de la descente. Sans texte : une bulle montre ce qu'elle veut
    // (un morceau de chair du Gardien → une patte). Quand le chat le lui apporte, elle lui coud 2 pattes :
    // il se tient debout. Visuel généré en code (robe, capuche, yeux, lanterne).
    public class SoulMerchant : MonoBehaviour
    {
        [SerializeField] private float noticeRange = 6f;      // la bulle apparaît à cette distance
        [SerializeField] private float tradeRange = 2.2f;     // l'échange se fait à cette distance
        [SerializeField] private float legLife = 40f;

        [Header("Visuel")]
        [SerializeField] private Color robeColor = new Color(0.16f, 0.12f, 0.22f);
        [SerializeField] private Color eyeColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color lanternColor = new Color(1f, 0.6f, 0.25f);
        [SerializeField] private Color fleshColor = new Color(0.45f, 0.08f, 0.08f);
        [SerializeField] private Color legColor = new Color(0.12f, 0.12f, 0.2f);

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private PlayerBody player;
        private Transform figure;
        private Transform bubble;
        private Light lantern;
        private float bubbleShown;      // 0 : cachée, 1 : visible

        private void OnEnable()
        {
            BuildFigure();
            BuildBubble();
        }

        private void OnDisable()
        {
            generated.DestroyAll();
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerBody>();

            // Balancement lent, lanterne qui vacille
            float t = Time.time;
            figure.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f) * 3f);
            lantern.intensity = 1.6f + (Mathf.PerlinNoise(t * 4f, 0.2f) - 0.5f) * 0.6f;

            bool legsMade = GameSession.HasFlag(GameFlags.MerchantLegsMade);
            float distance = player != null ? Mathf.Abs(player.transform.position.x - transform.position.x) : float.MaxValue;

            // Échange : le chat apporte le morceau de chair, avec un corps sur lequel coudre
            if (!legsMade && distance < tradeRange && player.HasBody && GameFlow.IsPlaying &&
                GameSession.HasFlag(GameFlags.GuardianChunkCarried))
            {
                Trade();
                legsMade = true;
            }

            bool show = !legsMade && distance < noticeRange && player != null && !player.IsDead;
            bubbleShown = Mathf.MoveTowards(bubbleShown, show ? 1f : 0f, Time.deltaTime * 4f);
            bubble.gameObject.SetActive(bubbleShown > 0.01f);
            bubble.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, bubbleShown);
            bubble.localPosition = new Vector3(0f, 2.3f + Mathf.Sin(t * 2f) * 0.05f, 0f);
        }

        private void Trade()
        {
            GameSession.SetFlag(GameFlags.GuardianChunkCarried, false);
            GameSession.SetFlag(GameFlags.MerchantLegsMade);
            player.AttachLimbs(Limb.CatLegs(legLife));

            ParticleBurst.Spawn("MerchantSews", player.transform.position, new ParticleSystem.MinMaxGradient(lanternColor, Color.white), 70,
                lifetime: new Vector2(0.4f, 1f), speed: new Vector2(1f, 4f), size: new Vector2(0.04f, 0.12f),
                gravity: -0.2f, radius: 0.5f, duration: 0.5f);
            GameCamera.ShakeMain(0.2f, 0.1f);
            if (GameFlow.Instance != null) GameFlow.Instance.SaveProgress();
        }

        // ---------- Visuel ----------

        private void BuildFigure()
        {
            figure = generated.Track(new GameObject("MerchantFigure")).transform;
            figure.SetParent(transform, false);

            Material robe = generated.Track(MaterialFactory.Lit(robeColor));
            AddMesh(figure, generated.Track(LowPoly.Prism(7, 0.45f, 0.14f, 1.5f, 0.2f)), robe, Vector3.zero, Vector3.one);          // robe
            AddMesh(figure, CatBody.LumpMesh, robe, new Vector3(0f, 1.55f, 0f), new Vector3(0.42f, 0.48f, 0.42f)); // capuche
            Material eyes = generated.Track(MaterialFactory.Unlit(eyeColor));
            AddMesh(figure, CatBody.LumpMesh, eyes, new Vector3(-0.08f, 1.55f, -0.2f), Vector3.one * 0.05f);
            AddMesh(figure, CatBody.LumpMesh, eyes, new Vector3(0.08f, 1.55f, -0.2f), Vector3.one * 0.05f);

            // Lanterne tenue à bout de bras
            Material glow = generated.Track(MaterialFactory.Unlit(lanternColor));
            AddMesh(figure, CatBody.LumpMesh, glow, new Vector3(-0.45f, 0.9f, -0.15f), Vector3.one * 0.13f);
            GameObject light = generated.Track(new GameObject("Lantern"));
            light.transform.SetParent(figure, false);
            light.transform.localPosition = new Vector3(-0.45f, 0.9f, -0.4f);
            lantern = light.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = lanternColor;
            lantern.range = 5f;
            lantern.intensity = 1.6f;
        }

        // Bulle sans texte : un morceau de chair → une patte
        private void BuildBubble()
        {
            bubble = generated.Track(new GameObject("MerchantBubble")).transform;
            bubble.SetParent(transform, false);

            Material back = generated.Track(MaterialFactory.Unlit(new Color(0.95f, 0.92f, 0.85f, 0.9f)));
            AddMesh(bubble, generated.Track(LowPoly.Box()), back, new Vector3(0f, 0f, 0.05f), new Vector3(1.3f, 0.55f, 0.02f));
            AddMesh(bubble, generated.Track(LowPoly.Box()), back, new Vector3(0f, -0.33f, 0.05f), new Vector3(0.12f, 0.15f, 0.02f)); // pointe

            AddMesh(bubble, CatBody.LumpMesh, generated.Track(MaterialFactory.Lit(fleshColor)),
                new Vector3(-0.38f, 0f, -0.05f), new Vector3(0.3f, 0.24f, 0.2f));
            Material arrow = generated.Track(MaterialFactory.Unlit(new Color(0.25f, 0.2f, 0.2f)));
            AddMesh(bubble, generated.Track(LowPoly.Box()), arrow, new Vector3(0f, 0f, -0.03f), new Vector3(0.2f, 0.04f, 0.02f));
            Transform tip = AddMesh(bubble, generated.Track(LowPoly.Prism(3, 0.06f, 0f, 0.1f)), arrow, new Vector3(0.1f, 0f, -0.03f), Vector3.one);
            tip.localRotation = Quaternion.Euler(0f, 0f, -90f);

            Transform leg = AddMesh(bubble, CatBody.LegMesh, generated.Track(MaterialFactory.Lit(legColor)),
                new Vector3(0.38f, 0.17f, -0.05f), new Vector3(0.06f, 0.34f, 0.06f));
            leg.localRotation = Quaternion.Euler(180f, 0f, 12f);
            bubble.gameObject.SetActive(false);
        }

        private Transform AddMesh(Transform parent, Mesh mesh, Material mat, Vector3 position, Vector3 scale)
        {
            GameObject go = generated.Track(new GameObject("Part"));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
