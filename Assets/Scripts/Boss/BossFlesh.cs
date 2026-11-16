using System.Collections;
using ParryEverything.Core;
using ParryEverything.Environment;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Ce que devient le Gardien vaincu : un tas de chair inerte, là où il est tombé.
    //  1. Le chat s'en approche : vision du meurtre, puis la chair s'agglutine autour de l'âme (boule qui roule).
    //     La zone se rallume, la partie est sauvegardée.
    //  2. Il reste un tas plus petit. L'âme marchande en réclame un morceau pour fabriquer des pattes :
    //     le chat revient l'arracher (il le porte jusqu'à elle).
    public class BossFlesh : MonoBehaviour
    {
        private enum State { Waiting, Lying, Agglutinating, Remains, Consumed }

        [SerializeField] private HellGuardian boss;                 // trouvé automatiquement si vide
        [SerializeField] private string bossId = "HellGuardian";
        [SerializeField] private float groundY = -0.45f;            // la chair tombe au sol de l'arène
        [SerializeField] private float touchRange = 1.5f;          // distance à laquelle le chat s'en sert
        [SerializeField] private float fleshLife = 80f;            // vie de la boule de chair

        [Header("Visuel")]
        [SerializeField] private Color fleshColor = new Color(0.3f, 0.05f, 0.06f);
        [SerializeField] private Color emberColor = new Color(1f, 0.35f, 0.1f);
        [Range(0.1f, 1f)] [SerializeField] private float remainsScale = 0.5f;   // tas restant après l'agglutination

        private State state;
        private Vector3 lastBossPosition;
        private bool trackingBoss;          // un boss était bien là : sa disparition fait apparaître la chair
        private PlayerBody player;
        private Material fleshMat;
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private Transform pile;
        private Transform carried;          // morceau porté par le chat jusqu'à l'âme marchande
        private float baseScale = 1f;

        private void Start()
        {
            if (GameSession.IsBossDefeated(bossId))
            {
                // Déjà vaincu (réapparition, Continuer) : le tas reste, sauf si le morceau a été pris
                if (GameSession.HasFlag(GameFlags.GuardianChunkTaken)) state = State.Consumed;
                else
                {
                    BuildPile(new Vector3(transform.position.x, groundY, 0f));
                    baseScale = remainsScale;
                    state = State.Remains;
                }
                return;
            }
            if (boss == null) boss = FindFirstObjectByType<HellGuardian>();
            if (boss != null)
            {
                lastBossPosition = boss.CorePosition;
                trackingBoss = true;
            }
        }

        private void OnDisable()
        {
            generated.DestroyAll();
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerBody>();

            switch (state)
            {
                case State.Waiting:
                    // Le Gardien se détruit à la fin de sa mort : la chair apparaît là où il est tombé
                    if (boss != null) lastBossPosition = boss.CorePosition;
                    else if (trackingBoss) Appear();
                    break;

                case State.Lying:
                    Pulse();
                    if (PlayerTouches()) StartCoroutine(Agglutinate());
                    break;

                case State.Remains:
                    Pulse();
                    // Arracher un morceau : il faut un corps de chair pour le porter
                    if (PlayerTouches() && player.HasBody && !GameSession.HasFlag(GameFlags.GuardianChunkTaken))
                    {
                        TakeChunk();
                    }
                    break;
            }
            FollowCarried();
        }

        private bool PlayerTouches()
        {
            return pile != null && player != null && !player.IsDead && GameFlow.IsPlaying &&
                   Mathf.Abs(player.transform.position.x - pile.position.x) < touchRange;
        }

        private void Appear()
        {
            state = State.Lying;
            Vector3 p = new Vector3(lastBossPosition.x, groundY, 0f);
            BuildPile(p);
            ParticleBurst.Spawn("FleshFalls", p + Vector3.up * 0.3f, new ParticleSystem.MinMaxGradient(fleshColor, emberColor), 80,
                lifetime: new Vector2(0.4f, 1f), speed: new Vector2(1f, 5f), size: new Vector2(0.05f, 0.15f),
                gravity: 1f, radius: 0.5f);
        }

        private void BuildPile(Vector3 position)
        {
            pile = generated.Track(new GameObject("BossFleshPile")).transform;
            pile.position = position;
            fleshMat = generated.Track(MaterialFactory.Lit(fleshColor));
            // Trois masses de chair affaissées
            Vector3[] offsets = { new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.15f, 0.2f), new Vector3(-0.4f, 0.12f, -0.15f) };
            float[] sizes = { 0.75f, 0.5f, 0.45f };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject part = generated.Track(new GameObject("Lump"));
                part.transform.SetParent(pile, false);
                part.transform.localPosition = offsets[i];
                part.transform.localScale = new Vector3(sizes[i] * 1.3f, sizes[i] * 0.7f, sizes[i]);
                part.transform.localRotation = Quaternion.Euler(0f, i * 70f, i * 12f);
                part.AddComponent<MeshFilter>().sharedMesh = CatBody.LumpMesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = fleshMat;
            }
        }

        // La chair palpite encore faiblement, des braises s'en échappent
        private void Pulse()
        {
            if (pile == null) return;
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 1.6f)), 6f);
            pile.localScale = Vector3.one * (baseScale * (1f + beat * 0.05f));
            fleshMat.color = Color.Lerp(fleshColor, emberColor, beat * 0.25f);
        }

        private IEnumerator Agglutinate()
        {
            state = State.Agglutinating;
            GameFlow flow = GameFlow.Instance;
            if (flow != null) flow.BeginCutscene();

            yield return new WaitForSecondsRealtime(0.4f);
            bool visionDone = false;
            MurderVision.Play(() => visionDone = true);
            while (!visionDone) yield return null;

            // Pendant le blanc de fin de vision : la chair enferme l'âme, le tas se réduit, la zone se rallume
            player.GrantFlesh(Limb.Torso(fleshLife));
            baseScale = remainsScale;
            if (Atmosphere.Current != null) Atmosphere.Current.Rekindle();
            ParticleBurst.Spawn("FleshWraps", player.transform.position, new ParticleSystem.MinMaxGradient(fleshColor, emberColor), 90,
                lifetime: new Vector2(0.3f, 0.8f), speed: new Vector2(2f, 6f), size: new Vector2(0.06f, 0.16f),
                gravity: 0.5f, radius: 0.8f);
            state = State.Remains;

            while (MurderVision.IsPlaying) yield return null;
            if (flow != null)
            {
                flow.EndCutscene();
                flow.BossDefeated(bossId);
            }
            GameCamera.ShakeMain(0.3f, 0.15f);
        }

        // Le chat arrache un morceau du tas et le porte
        private void TakeChunk()
        {
            GameSession.SetFlag(GameFlags.GuardianChunkTaken);
            GameSession.SetFlag(GameFlags.GuardianChunkCarried);
            ParticleBurst.Spawn("ChunkTorn", pile.position + Vector3.up * 0.3f, new ParticleSystem.MinMaxGradient(fleshColor, emberColor), 50,
                lifetime: new Vector2(0.3f, 0.7f), speed: new Vector2(1f, 4f), size: new Vector2(0.05f, 0.12f),
                gravity: 1f, radius: 0.4f);
            GameCamera.ShakeMain(0.15f, 0.08f);
            // Ce qui reste du tas s'effondre en braises
            Destroy(pile.gameObject);
            pile = null;
            state = State.Consumed;
        }

        // Le morceau porté flotte à côté du chat jusqu'à ce que l'âme marchande le prenne
        private void FollowCarried()
        {
            bool carrying = GameSession.HasFlag(GameFlags.GuardianChunkCarried) && player != null;
            if (!carrying)
            {
                if (carried != null)
                {
                    Destroy(carried.gameObject);
                    carried = null;
                }
                return;
            }
            if (carried == null)
            {
                carried = generated.Track(new GameObject("CarriedChunk")).transform;
                carried.localScale = Vector3.one * 0.3f;
                carried.gameObject.AddComponent<MeshFilter>().sharedMesh = CatBody.LumpMesh;
                carried.gameObject.AddComponent<MeshRenderer>().sharedMaterial = generated.Track(MaterialFactory.Lit(fleshColor));
                carried.position = player.transform.position;
            }
            Vector3 target = player.transform.position + new Vector3(-0.55f, 0.55f + Mathf.Sin(Time.time * 3f) * 0.06f, -0.2f);
            carried.position = Vector3.Lerp(carried.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            carried.Rotate(0f, 90f * Time.deltaTime, 0f);
        }
    }
}
