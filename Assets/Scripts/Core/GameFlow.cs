using System.Collections;
using ParryEverything.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParryEverything.Core
{
    // Flux de jeu : menu, jeu, pause, scène (vision), mort, fin.
    // Mort ou « Continuer » : on relance la scène, l'état de la dernière sauvegarde est rechargé dans GameSession
    // (sanctuaire, membres, boss vaincus, comportement) et chaque objet de la scène le relit au démarrage.
    // Les écrans sont dessinés par GameMenus ; les touches viennent de GameInput.
    public class GameFlow : MonoBehaviour
    {
        [Header("Démarrage")]
        [SerializeField] private bool startInMenu = true;      // décocher pour tester sans passer par le menu

        [Header("Mort")]
        [SerializeField] private float respawnDelay = 1f;      // fondu au noir (temps réel) avant de réapparaître
        [SerializeField] private float killHeight = -40f;      // tombé sous cette hauteur : mort (filet de sécurité)

        // Survit au rechargement de la scène : après une mort ou « Continuer », on joue directement
        private static bool skipMenu;
        private static GameFlow instance;

        private GameStateMachine machine;
        private PlayerBody player;
        private bool hasSave;                      // une partie à continuer (lu à l'ouverture du menu)
        private float deathTime = -1f;             // temps réel de la mort, pour le fondu

        public static GameFlow Instance => instance;
        public GameState State => machine != null ? machine.State : GameState.Playing;
        public bool HasSave => hasSave;
        public PlayerBody Player => player;
        // Fondu vers le noir pendant la mort : 0 au moment de la mort, 1 à la réapparition
        public float DeathProgress => State == GameState.Dead && respawnDelay > 0f
            ? Mathf.Clamp01((Time.unscaledTime - deathTime) / respawnDelay) : 0f;
        // Vrai quand le joueur peut agir. Sans GameFlow dans la scène, on joue toujours.
        public static bool IsPlaying => instance == null || instance.State == GameState.Playing;

        // ---------- Appels des objets de la scène ----------

        // Sanctuaire : nouveau point de réapparition, et sauvegarde
        public void SaveAtSanctuary(string sanctuaryId, Vector3 respawnPoint)
        {
            GameSession.SanctuaryId = sanctuaryId;
            GameSession.RespawnPoint = respawnPoint;
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
            SaveSystem.Save(GameSession.ToSaveData());
            hasSave = true;
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

        // ---------- Cycle de vie ----------

        private void Awake()
        {
            instance = this;
            TimeControl.ResetAll();

            bool playNow = skipMenu || !startInMenu;
            skipMenu = false;
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

            machine = new GameStateMachine(playNow ? GameState.Playing : GameState.Menu);
            machine.Changed += OnStateChanged;
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
            }

            hasSave = SaveSystem.HasSave();
            ApplyTime(machine.State);
        }

        private void OnDestroy()
        {
            if (player != null) player.OnDied -= OnPlayerDied;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            switch (machine.State)
            {
                case GameState.Menu:
                    if (GameInput.Pressed(GameAction.Confirm))
                    {
                        if (hasSave) ContinueGame();
                        else NewGame();
                    }
                    else if (hasSave && GameInput.Pressed(GameAction.NewGame)) NewGame();
                    else if (GameInput.Pressed(GameAction.Quit)) Quit();
                    break;

                case GameState.Playing:
                    if (GameInput.Pressed(GameAction.Pause)) machine.TogglePause();
                    else if (player != null && player.transform.position.y < killHeight) player.Kill();
                    break;

                case GameState.Paused:
                    if (GameInput.Pressed(GameAction.Pause)) machine.TogglePause();
                    else if (GameInput.Pressed(GameAction.Menu)) machine.ReturnToMenu();
                    else if (GameInput.Pressed(GameAction.Quit)) Quit();
                    break;

                case GameState.End:
                    if (GameInput.Pressed(GameAction.Confirm)) machine.ReturnToMenu();
                    break;
            }
        }

        // Menu : la scène est relancée et la sauvegarde rechargée (boss vaincus, corps...)
        private void ContinueGame()
        {
            skipMenu = true;
            ReloadScene();
        }

        // Menu : efface la sauvegarde et part du début de la scène
        private void NewGame()
        {
            SaveSystem.Delete();
            GameSession.Clear();
            hasSave = false;
            machine.StartGame();
        }

        private static void Quit()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
        }

        private void OnPlayerDied()
        {
            machine.PlayerDied();
        }

        private void OnStateChanged(GameState previous, GameState next)
        {
            ApplyTime(next);

            if (next == GameState.Dead)
            {
                deathTime = Time.unscaledTime;
                StartCoroutine(RespawnAfterDelay());
            }
            else if (next == GameState.Menu && previous != GameState.Menu)
            {
                // Retour au menu : l'écran titre repart d'une scène propre
                ReloadScene();
            }
        }

        // Le temps est figé hors du jeu (la mort garde le temps qui coule, pour l'effet)
        private static void ApplyTime(GameState state)
        {
            TimeControl.SetPaused(state == GameState.Menu || state == GameState.Paused
                                  || state == GameState.End || state == GameState.Cutscene);
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSecondsRealtime(respawnDelay);
            machine.Respawn();
            skipMenu = true;
            ReloadScene();
        }

        private static void ReloadScene()
        {
            TimeControl.ResetAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Les champs statiques survivent sans rechargement de domaine : on repart propre à chaque lancement
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            skipMenu = false;
            instance = null;
        }
    }
}
