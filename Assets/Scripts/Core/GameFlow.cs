using System;
using System.Collections;
using ParryEverything.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParryEverything.Core
{
    // Flux de jeu : menu, jeu, pause, scène (vision), mort, réveil, fin.
    // Mort ou « Continuer » : fondu au noir, la scène est rechargée en arrière-plan, l'état de la dernière sauvegarde
    // est remis dans GameSession (sanctuaire, membres, boss vaincus, comportement) et chaque objet de la scène le relit
    // au démarrage ; puis le chat se réveille (fondu depuis le noir, le joueur reprend la main un peu après).
    // Les écrans sont dessinés par l'interface (UI/GameScreens), qui appelle les méthodes publiques ci-dessous.
    // Seule la touche Pause est lue ici ; la navigation dans les menus passe par l'interface.
    public class GameFlow : MonoBehaviour
    {
        [Header("Démarrage")]
        [SerializeField] private bool startInMenu = true;      // décocher pour tester sans passer par le menu
        [SerializeField] private float introFade = 1.5f;       // fondu depuis le noir à l'ouverture du jeu

        [Header("Mort")]
        [Range(0.05f, 1f)] [SerializeField] private float deathSlowMotion = 0.25f;  // ralenti pendant que l'âme s'éteint
        [SerializeField] private float deathHold = 2.2f;       // temps réel avant le fondu au noir
        [SerializeField] private float deathSkipAfter = 0.5f;  // une touche passe la mise en scène après ce délai
        [SerializeField] private float deathFadeOut = 0.9f;
        [SerializeField] private Color deathEmberColor = new Color(0.55f, 0.75f, 1f);
        [SerializeField] private float killHeight = -40f;      // tombé sous cette hauteur : mort (filet de sécurité)

        [Header("Réveil")]
        [SerializeField] private float wakeFadeIn = 1.4f;
        [SerializeField] private float wakeDuration = 1.6f;    // le joueur reprend la main au bout de ce temps
        [SerializeField] private Color wakeColor = new Color(0.45f, 0.75f, 1f);

        [Header("Transitions")]
        [SerializeField] private float transitionFade = 0.7f;  // fondu avant un rechargement (continuer, retour au titre)

        // Survivent au rechargement de la scène
        private static bool skipMenu;       // après une mort ou « Continuer » : on joue directement
        private static bool wakeOnLoad;     // ... en se réveillant
        private static bool launched;       // le jeu vient d'être lancé : fondu d'ouverture
        private static GameFlow instance;

        private GameStateMachine machine;
        private PlayerBody player;
        private bool hasSave;                      // une partie à continuer (lu à l'ouverture du menu)
        private SaveData saveSummary;              // pour afficher date et lieu sur « Continuer »
        private float deathTime = -1f;             // temps réel de la mort
        private bool busy;                         // une transition est en cours : les boutons ne font plus rien
        private int stateChangedFrame = -1;        // la touche Pause ne compte pas deux fois dans la même image

        public static GameFlow Instance => instance;
        public GameState State => machine != null ? machine.State : GameState.Playing;
        public bool HasSave => hasSave;
        public SaveData SaveSummary => saveSummary;
        public PlayerBody Player => player;
        public bool Busy => busy;
        // Temps réel écoulé depuis la mort (pour la mise en scène)
        public float DeathElapsed => State == GameState.Dead ? Time.unscaledTime - deathTime : 0f;
        // Vrai quand le joueur peut agir. Sans GameFlow dans la scène, on joue toujours.
        public static bool IsPlaying => instance == null || instance.State == GameState.Playing;

        // État changé (ancien, nouveau) ; une sauvegarde vient d'être écrite
        public event Action<GameState, GameState> StateChanged;
        public event Action Saved;

        // ---------- Appels des objets de la scène ----------

        // Sanctuaire : nouveau point de réapparition, et sauvegarde
        public void SaveAtSanctuary(string sanctuaryId, Vector3 respawnPoint, string placeName = null)
        {
            GameSession.SanctuaryId = sanctuaryId;
            GameSession.RespawnPoint = respawnPoint;
            if (!string.IsNullOrEmpty(placeName)) GameSession.PlaceName = placeName;
            SaveProgress();
        }

        // Boss vaincu (chair cousue) : enregistré tout de suite
        public void BossDefeated(string bossId)
        {
            GameSession.MarkBossDefeated(bossId);
            SaveProgress();
        }

        // Écrit l'état actuel (corps du joueur compris) dans la session et sur disque
        public void SaveProgress()
        {
            if (player != null) GameSession.SetLimbs(player.SaveLimbs());
            try
            {
                SaveSystem.Save(GameSession.ToSaveData());
            }
            catch (Exception e)
            {
                // Disque plein, droits... : le jeu continue, la partie n'est simplement pas enregistrée
                Debug.LogError($"Sauvegarde impossible : {e.Message}");
                return;
            }
            hasSave = true;
            Saved?.Invoke();
        }

        // Fin de la tranche (bas de la descente)
        public void ReachEnd()
        {
            machine.Finish();
        }

        public bool BeginCutscene() => machine.BeginCutscene();
        public bool EndCutscene() => machine.EndCutscene();

        // Déplace le joueur sans élan (réapparition, outils de debug)
        public void PlacePlayer(Vector3 position)
        {
            if (player == null) return;
            player.transform.position = position;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = position;
                rb.linearVelocity = Vector3.zero;
            }
        }

        // ---------- Appels de l'interface ----------

        // Menu : la scène est relancée et la sauvegarde rechargée (boss vaincus, corps...)
        public void ContinueGame()
        {
            if (busy || State != GameState.Menu) return;
            skipMenu = true;
            wakeOnLoad = true;
            StartCoroutine(FadeAndReload());
        }

        // Menu : efface la sauvegarde et part du début de la scène, sans rechargement (le titre la montre déjà)
        public void NewGame()
        {
            if (busy || State != GameState.Menu) return;
            SaveSystem.Delete();
            GameSession.Clear();
            hasSave = false;
            saveSummary = null;
            machine.StartGame();
        }

        public void Resume()
        {
            if (!busy && State == GameState.Paused) machine.TogglePause();
        }

        // Pause ou fin : retour au titre (la scène repart propre)
        public void ReturnToMenu()
        {
            if (busy || State == GameState.Menu) return;
            StartCoroutine(FadeAndReload());
        }

        public void Quit()
        {
            if (busy) return;
            StartCoroutine(FadeAndQuit());
        }

        // ---------- Cycle de vie ----------

        private void Awake()
        {
            instance = this;
            TimeControl.ResetAll();

            bool playNow = skipMenu || !startInMenu;
            bool waking = playNow && wakeOnLoad;
            skipMenu = false;
            wakeOnLoad = false;
            if (playNow)
            {
                // Réapparition ou « Continuer » : on repart de la dernière sauvegarde
                SaveData save = SaveSystem.Load();
                if (save != null) GameSession.LoadFrom(save);
                else GameSession.Clear();
            }
            else
            {
                GameSession.Clear();
            }

            GameState initial = waking ? GameState.Waking : playNow ? GameState.Playing : GameState.Menu;
            machine = new GameStateMachine(initial);
            machine.Changed += OnStateChanged;

            // Ouverture du jeu : on part du noir
            if (!launched)
            {
                launched = true;
                ScreenFade.Set(1f);
            }
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerBody>();
            if (player != null)
            {
                player.OnDied += OnPlayerDied;
                player.LoadLimbs(GameSession.Limbs);
                if (GameSession.RespawnPoint.HasValue)
                {
                    PlacePlayer(GameSession.RespawnPoint.Value);
                }
                // Recouture : enregistrée tout de suite (abonné après le chargement des membres)
                player.OnLimbSewn += OnLimbSewn;
            }

            hasSave = SaveSystem.HasSave();
            saveSummary = hasSave ? SaveSystem.Load() : null;
            ApplyTime(machine.State);

            if (machine.State == GameState.Waking) StartCoroutine(WakeUp());
            else ScreenFade.FadeTo(0f, ScreenFade.Alpha > 0f ? introFade : 0f);
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.OnDied -= OnPlayerDied;
                player.OnLimbSewn -= OnLimbSewn;
            }
            if (instance == this) instance = null;
        }

        private void Update()
        {
            ScreenFade.Tick(Time.unscaledDeltaTime);

            if (machine.State == GameState.Playing)
            {
                // Échap qui vient de fermer la pause (interface) ne la rouvre pas dans la même image
                if (Time.frameCount != stateChangedFrame && GameInput.Pressed(GameAction.Pause)) machine.TogglePause();
                else if (player != null && player.transform.position.y < killHeight) player.Kill();
            }
            // En pause, Échap est lu par l'interface (retour, ou reprise depuis l'écran de pause)
        }

        private void OnPlayerDied()
        {
            machine.PlayerDied();
        }

        private void OnLimbSewn(Limb limb)
        {
            if (State == GameState.Playing || State == GameState.Cutscene) SaveProgress();
        }

        private void OnStateChanged(GameState previous, GameState next)
        {
            stateChangedFrame = Time.frameCount;
            ApplyTime(next);
            if (next == GameState.Dead)
            {
                deathTime = Time.unscaledTime;
                StartCoroutine(DeathSequence());
            }
            StateChanged?.Invoke(previous, next);
        }

        // Le temps est figé en pause, à l'écran de fin et pendant les visions.
        // Au titre, il coule : la scène vit derrière le menu. À la mort, il ralentit (effet).
        private static void ApplyTime(GameState state)
        {
            TimeControl.SetPaused(state == GameState.Paused || state == GameState.End || state == GameState.Cutscene);
        }

        // ---------- Séquences ----------

        // L'âme s'éteint au ralenti, un mot apparaît (interface), fondu au noir, rechargement, réveil
        private IEnumerator DeathSequence()
        {
            busy = true;
            TimeControl.SetEffectScale(deathSlowMotion);
            if (player != null)
            {
                ParticleBurst.Spawn("DeathEmbers", player.transform.position, deathEmberColor, 80,
                    lifetime: new Vector2(0.8f, 2f), speed: new Vector2(0.3f, 2f), size: new Vector2(0.03f, 0.1f),
                    gravity: -0.4f, radius: 0.3f, duration: 0.4f);
            }

            while (DeathElapsed < deathHold)
            {
                if (DeathElapsed > deathSkipAfter && SkipPressed()) break;
                yield return null;
            }

            ScreenFade.FadeTo(1f, deathFadeOut);
            while (!ScreenFade.IsBlack) yield return null;

            machine.Respawn();
            skipMenu = true;
            wakeOnLoad = true;
            yield return LoadSceneBehindFade();
        }

        private static bool SkipPressed()
        {
            return GameInput.Pressed(GameAction.Confirm) || GameInput.Pressed(GameAction.Parry) || GameInput.Pressed(GameAction.Jump);
        }

        // Après une mort ou « Continuer » : l'écran s'éclaircit, une lueur entoure le chat, puis il reprend la main
        private IEnumerator WakeUp()
        {
            ScreenFade.FadeTo(0f, wakeFadeIn);
            if (player != null)
            {
                ParticleBurst.Spawn("WakeGlow", player.transform.position + Vector3.up * 0.3f, wakeColor, 50,
                    lifetime: new Vector2(0.8f, 1.6f), speed: new Vector2(0.2f, 1.2f), size: new Vector2(0.03f, 0.09f),
                    gravity: -0.5f, radius: 0.5f, duration: 0.8f);
            }
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < wakeDuration) yield return null;
            machine.FinishWaking();
        }

        private IEnumerator FadeAndReload()
        {
            busy = true;
            ScreenFade.FadeTo(1f, transitionFade);
            while (!ScreenFade.IsBlack) yield return null;
            yield return LoadSceneBehindFade();
        }

        private IEnumerator FadeAndQuit()
        {
            busy = true;
            ScreenFade.FadeTo(1f, transitionFade * 0.6f);
            while (!ScreenFade.IsBlack) yield return null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // L'écran est noir : on recharge la scène en arrière-plan (pas d'image figée pendant le chargement)
        private static IEnumerator LoadSceneBehindFade()
        {
            TimeControl.ResetAll();
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
            while (op != null && !op.isDone) yield return null;
        }

        // Les champs statiques survivent sans rechargement de domaine : on repart propre à chaque lancement
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            skipMenu = false;
            wakeOnLoad = false;
            launched = false;
            instance = null;
        }
    }
}
