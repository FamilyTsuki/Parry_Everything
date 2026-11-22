using System.Collections;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Parer sa propre mort. Au coup fatal, le temps se fige : une faucheuse s'approche dans le dos de l'âme,
    // lève sa faux… la lame brille (signal), puis frappe. Parade juste avant le coup : la faux se brise, le chat
    // survit avec un peu d'âme. Sinon il meurt. Une fois par passage au sanctuaire.
    public class DeathsDoor : MonoBehaviour
    {
        [SerializeField] private float approachTime = 1.1f;      // la faucheuse arrive (temps réel)
        [SerializeField] private float raiseTime = 0.5f;         // lève la faux
        [SerializeField] private float glintTime = 0.35f;        // la lame brille, puis le coup tombe
        [SerializeField] private float windowBefore = 0.25f;     // parade acceptée avant l'impact
        [SerializeField] private float windowAfter = 0.06f;      //   et juste après
        [Range(0f, 1f)] [SerializeField] private float survivalSoul = 0.3f;   // âme rendue en cas de réussite

        private PlayerBody body;
        private PlayerController player;
        private CatSoul soul;
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private bool used;
        private bool active;
        private bool parried;
        private float clock;                                      // temps réel de la séquence (hors pause)
        private float strikeAt;

        public bool Available => !used;

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            player = GetComponent<PlayerController>();
        }

        private void Start()
        {
            soul = GetComponent<CatSoul>();
            if (body != null)
            {
                body.LethalHit = TryHold;
                body.OnRested += OnRested;
            }
            if (player != null) player.ParryPressed += OnParry;
        }

        private void OnDestroy()
        {
            if (body != null)
            {
                if (body.LethalHit == TryHold) body.LethalHit = null;
                body.OnRested -= OnRested;
            }
            if (player != null) player.ParryPressed -= OnParry;
            generated.DestroyAll();
            TimeControl.SetLayerScale("reaper", 1f);
        }

        private void OnRested() { used = false; }

        // Appelé par PlayerBody au coup fatal : la mort attend la faucheuse
        private bool TryHold()
        {
            if (used || active || !GameFlow.IsPlaying || !isActiveAndEnabled) return false;
            used = true;
            StartCoroutine(Sequence());
            return true;
        }

        private void OnParry()
        {
            if (!active || parried) return;
            if (clock >= strikeAt - windowBefore && clock <= strikeAt + windowAfter) parried = true;
        }

        private IEnumerator Sequence()
        {
            active = true;
            parried = false;
            clock = 0f;
            body.Invulnerable = true;
            TimeControl.SetLayerScale("reaper", 0f);

            float facing = player != null ? player.Facing : 1f;
            Vector3 head = soul != null ? soul.HeadPosition : transform.position;
            Transform reaper = BuildReaper(out Transform scythe, out Material blade);
            Vector3 from = head + new Vector3(-facing * 4f, 1.2f, 0.4f);
            Vector3 to = head + new Vector3(-facing * 1.4f, 0.2f, 0.4f);
            reaper.rotation = Quaternion.Euler(0f, facing > 0f ? 0f : 180f, 0f);
            strikeAt = approachTime + raiseTime + glintTime;
            float end = strikeAt + windowAfter;

            while (clock < end && !parried)
            {
                if (!TimeControl.Paused) clock += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(clock / approachTime);
                reaper.position = Vector3.Lerp(from, to, a * a * (3f - 2f * a)) + Vector3.up * Mathf.Sin(clock * 3f) * 0.05f;
                // Faux : basse, puis levée haut derrière, puis abattue vers l'âme
                float raise = Mathf.Clamp01((clock - approachTime) / raiseTime);
                float swing = Mathf.Clamp01((clock - (strikeAt - 0.12f)) / 0.12f);
                float angle = Mathf.Lerp(-5f, 55f, raise * raise * (3f - 2f * raise));
                angle = Mathf.Lerp(angle, -115f, swing * swing);
                scythe.localRotation = Quaternion.Euler(0f, 0f, angle);
                // Signal : la lame brille juste avant le coup
                bool glint = clock >= approachTime + raiseTime && clock < strikeAt;
                blade.color = glint ? Color.Lerp(Color.white, new Color(0.7f, 0.9f, 1f), Mathf.PingPong(clock * 12f, 1f)) : new Color(0.55f, 0.58f, 0.62f);
                yield return null;
            }

            if (parried)
            {
                // La faux se brise : le chat survit
                ParticleBurst.Spawn("ReaperShatter", scythe.position, new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.9f, 1f), Color.white), 60,
                    lifetime: new Vector2(0.3f, 0.9f),
                    speed: new Vector2(3f, 9f),
                    size: new Vector2(0.05f, 0.18f),
                    gravity: 1f,
                    radius: 0.4f);
                ParticleBurst.Spawn("ReaperFade", reaper.position, new ParticleSystem.MinMaxGradient(new Color(0.08f, 0.06f, 0.12f), new Color(0.4f, 0.3f, 0.55f)), 40,
                    lifetime: new Vector2(0.5f, 1.2f),
                    speed: new Vector2(0.5f, 2f),
                    size: new Vector2(0.2f, 0.5f),
                    gravity: -0.2f,
                    radius: 0.6f);
                GameCamera.ShakeMain(0.4f, 0.3f);
                if (soul != null) soul.OnParrySuccess(0.25f);
                generated.DestroyAll();
                TimeControl.SetLayerScale("reaper", 1f);
                body.Heal(body.SoulMax * survivalSoul);
                Debug.Log("Le chat a paré sa propre mort.");
                yield return new WaitForSecondsRealtime(0.8f);
                body.Invulnerable = false;
            }
            else
            {
                generated.DestroyAll();
                TimeControl.SetLayerScale("reaper", 1f);
                body.Invulnerable = false;
                body.Kill();
            }
            active = false;
        }

        // Faucheuse en formes simples : manteau en cône, tête pâle, yeux violets, faux (manche + lame)
        private Transform BuildReaper(out Transform scythe, out Material blade)
        {
            Transform root = Part("Reaper", null, null, null).transform;
            Material cloak = generated.Track(MaterialFactory.Lit(new Color(0.07f, 0.06f, 0.1f)));
            Material bone = generated.Track(MaterialFactory.Lit(new Color(0.82f, 0.79f, 0.72f)));
            Material eyes = generated.Track(MaterialFactory.Unlit(new Color(0.75f, 0.45f, 1f)));
            blade = generated.Track(MaterialFactory.Unlit(new Color(0.55f, 0.58f, 0.62f)));
            Material wood = generated.Track(MaterialFactory.Lit(new Color(0.25f, 0.18f, 0.12f)));

            Transform robe = Part("Cloak", root, generated.Track(LowPoly.Prism(7, 0.75f, 0.18f, 2.1f, 0.3f)), cloak).transform;
            robe.localPosition = new Vector3(0f, -1.4f, 0f);
            Transform skull = Part("Skull", root, generated.Track(LowPoly.Icosahedron(0.08f, 7)), bone).transform;
            skull.localPosition = new Vector3(0.28f, 0.72f, 0f);
            skull.localScale = Vector3.one * 0.5f;
            Transform hood = Part("Hood", root, generated.Track(LowPoly.Prism(6, 0.42f, 0.05f, 0.8f)), cloak).transform;
            hood.localPosition = new Vector3(-0.12f, 0.5f, 0f);
            for (int i = 0; i < 2; i++)
            {
                Transform eye = Part("Eye", skull, generated.Track(LowPoly.Icosahedron(0f, 0)), eyes).transform;
                eye.localPosition = new Vector3(0.4f, 0.05f, i == 0 ? 0.18f : -0.18f);
                eye.localScale = Vector3.one * 0.26f;
            }

            scythe = Part("Scythe", root, null, null).transform;
            scythe.localPosition = new Vector3(0.35f, 0.1f, -0.25f);
            Transform handle = Part("Handle", scythe, generated.Track(LowPoly.Prism(5, 0.05f, 0.05f, 2.4f)), wood).transform;
            handle.localPosition = new Vector3(0f, -0.8f, 0f);
            Transform edge = Part("Blade", scythe, generated.Track(LowPoly.Prism(3, 0.14f, 0f, 1.3f)), blade).transform;
            edge.localPosition = new Vector3(0f, 1.55f, 0f);
            edge.localRotation = Quaternion.Euler(0f, 0f, -100f);
            edge.localScale = new Vector3(1f, 1f, 0.3f);
            return root;
        }

        private GameObject Part(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            if (parent == null) generated.Track(go);
            else go.transform.SetParent(parent, false);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return go;
        }
    }
}
