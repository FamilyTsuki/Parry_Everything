using System;
using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ParryEverything.UI
{
    // Écrans du jeu en UI Toolkit (mise en page : Assets/UI/GameScreens.uxml / .uss).
    // Lit l'état de GameFlow et montre l'écran qui va avec (titre, pause, mort, fin) ; options et confirmation
    // s'ouvrent par-dessus. Les boutons appellent GameFlow. Navigation : flèches + Entrée, Échap = retour.
    // Dessine aussi le fondu au noir (ScreenFade) et l'indicateur de sauvegarde.
    [RequireComponent(typeof(UIDocument))]
    public class GameScreens : MonoBehaviour
    {
        [SerializeField] private string gameTitle = "PARRY EVERYTHING";
        [SerializeField] private string deathWord = "CONSUMÉ";
        [SerializeField] private float deathWordDelay = 0.25f;     // temps réel après la mort avant que le mot apparaisse
        [SerializeField] private float deathWordFade = 0.9f;
        [SerializeField] private float deathHintDelay = 0.8f;      // « passer » apparaît après ce délai
        [SerializeField] private float saveIndicatorTime = 2f;
        [SerializeField] private float cancelGrace = 0.2f;         // Échap ignoré juste après l'ouverture d'un écran
        [Range(0f, 1f)] [SerializeField] private float titleShadeStrength = 0.85f;

        private enum Overlay { None, Options, Confirm }

        private GameFlow flow;
        private UiSounds sounds;
        private VisualElement root;
        private VisualElement titleScreen, pauseScreen, optionsScreen, confirmScreen, deathScreen, endScreen;
        private VisualElement fade, saveIndicator, saveGlyph;
        private Label deathLabel, deathHint, continueDetail, confirmMessage, confirmDetail;
        private Button continueButton, newButton, resumeButton, confirmNo, endButton, optionsBack;
        private readonly List<OptionRow> optionRows = new List<OptionRow>();
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private Texture2D shadeTexture;

        private Overlay overlay;
        private Action confirmAction;
        private Button focusAfterOverlay;       // bouton qui reprend le focus en fermant l'écran par-dessus
        private VisualElement shownScreen;
        private float shownAt;
        private float savedAt = -100f;
        private bool silentFocus;               // focus donné par le script : pas de son

        // ---------- Cycle de vie ----------

        private void OnEnable()
        {
            sounds = GetComponent<UiSounds>();
            if (sounds == null) sounds = gameObject.AddComponent<UiSounds>();

            root = GetComponent<UIDocument>().rootVisualElement;
            titleScreen = root.Q("title-screen");
            pauseScreen = root.Q("pause-screen");
            optionsScreen = root.Q("options-screen");
            confirmScreen = root.Q("confirm-screen");
            deathScreen = root.Q("death-screen");
            endScreen = root.Q("end-screen");
            fade = root.Q("fade");
            saveIndicator = root.Q("save-indicator");
            saveGlyph = root.Q("save-glyph");
            deathLabel = root.Q<Label>("death-word");
            deathHint = root.Q<Label>("death-hint");
            continueDetail = root.Q<Label>("continue-detail");
            confirmMessage = root.Q<Label>("confirm-message");
            confirmDetail = root.Q<Label>("confirm-detail");

            root.Q<Label>("game-title").text = gameTitle;
            root.Q<Label>("version").text = "v" + Application.version;
            root.Q<Label>("controls-hint").text = ControlsText();
            deathLabel.text = deathWord;
            deathHint.text = GameInput.KeyName(GameAction.Confirm) + " : passer";

            shadeTexture = MakeShade(titleShadeStrength);
            root.Q("title-shade").style.backgroundImage = new StyleBackground(shadeTexture);

            // Titre
            continueButton = Wire("btn-continue", () => Flow?.ContinueGame());
            newButton = Wire("btn-new", OnNewGame);
            Wire("btn-options", () => OpenOptions(root.Q<Button>("btn-options")));
            Wire("btn-quit", () => Flow?.Quit());
            // Pause
            resumeButton = Wire("btn-resume", () => Flow?.Resume());
            Wire("btn-pause-options", () => OpenOptions(root.Q<Button>("btn-pause-options")));
            Wire("btn-to-title", () => Confirm(root.Q<Button>("btn-to-title"), "Revenir au menu titre ?",
                "Ta progression est enregistrée au dernier sanctuaire.", () => Flow?.ReturnToMenu()));
            Wire("btn-pause-quit", () => Confirm(root.Q<Button>("btn-pause-quit"), "Quitter le jeu ?",
                "Ta progression est enregistrée au dernier sanctuaire.", () => Flow?.Quit()));
            // Options, confirmation, fin
            optionsBack = Wire("btn-options-back", CloseOverlay);
            confirmNo = Wire("btn-confirm-no", CloseOverlay);
            Wire("btn-confirm-yes", () =>
            {
                Action action = confirmAction;
                CloseOverlay();
                action?.Invoke();
            });
            endButton = Wire("btn-end-title", () => Flow?.ReturnToMenu());

            BuildOptions();

            root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            // Son au déplacement du focus (flèches ou souris)
            root.RegisterCallback<FocusInEvent>(e =>
            {
                if (!silentFocus && e.target is Button) sounds.PlayMove();
            });

            foreach (VisualElement screen in AllScreens()) SetVisible(screen, false);
        }

        private void Start()
        {
            if (Flow != null)
            {
                Flow.StateChanged += OnStateChanged;
                Flow.Saved += OnSaved;
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (flow != null)
            {
                flow.StateChanged -= OnStateChanged;
                flow.Saved -= OnSaved;
            }
            if (shadeTexture != null) Destroy(shadeTexture);
        }

        private GameFlow Flow
        {
            get
            {
                if (flow == null) flow = GameFlow.Instance;
                return flow;
            }
        }

        private void Update()
        {
            fade.style.opacity = ScreenFade.Alpha;
            UpdateDeath();
            UpdateSaveIndicator();

            // Un écran est ouvert mais rien n'a le focus (clic dans le vide) : on le rend, sinon le clavier ne fait plus rien
            if (shownScreen != null && root.focusController != null && root.focusController.focusedElement == null)
            {
                FocusDefault();
            }
        }

        // ---------- Écrans ----------

        private void OnStateChanged(GameState previous, GameState next)
        {
            overlay = Overlay.None;
            Refresh();
        }

        // Montre l'écran qui correspond à l'état du jeu (ou l'écran ouvert par-dessus)
        private void Refresh()
        {
            GameState state = Flow != null ? Flow.State : GameState.Playing;
            VisualElement baseScreen;
            switch (state)
            {
                case GameState.Menu: baseScreen = titleScreen; break;
                case GameState.Paused: baseScreen = pauseScreen; break;
                case GameState.Dead: baseScreen = deathScreen; break;
                case GameState.End: baseScreen = endScreen; break;
                default: baseScreen = null; break;
            }
            VisualElement target = overlay == Overlay.Options ? optionsScreen
                : overlay == Overlay.Confirm ? confirmScreen
                : baseScreen;

            if (state == GameState.Menu) RefreshTitle();

            foreach (VisualElement screen in AllScreens()) SetVisible(screen, screen == target);
            bool changed = shownScreen != target;
            shownScreen = target == deathScreen ? null : target;   // la mort n'a pas de bouton
            if (changed)
            {
                shownAt = Time.unscaledTime;
                if (shownScreen == null) root.focusController?.focusedElement?.Blur();
                else FocusDefault();
            }
        }

        private void RefreshTitle()
        {
            bool hasSave = Flow != null && Flow.HasSave;
            continueButton.style.display = hasSave ? DisplayStyle.Flex : DisplayStyle.None;
            continueDetail.style.display = hasSave ? DisplayStyle.Flex : DisplayStyle.None;
            continueDetail.text = hasSave ? SaveLabel.Describe(Flow.SaveSummary, DateTime.UtcNow) : string.Empty;
        }

        private void FocusDefault()
        {
            Button target = null;
            if (focusAfterOverlay != null && overlay == Overlay.None && shownScreen != null && shownScreen.Contains(focusAfterOverlay))
            {
                target = focusAfterOverlay;
                focusAfterOverlay = null;
            }
            else if (shownScreen == titleScreen) target = Flow != null && Flow.HasSave ? continueButton : newButton;
            else if (shownScreen == pauseScreen) target = resumeButton;
            else if (shownScreen == optionsScreen) target = optionRows.Count > 0 ? optionRows[0].Root : optionsBack;
            else if (shownScreen == confirmScreen) target = confirmNo;
            else if (shownScreen == endScreen) target = endButton;
            if (target == null) return;

            // L'écran vient d'être activé : le focus se donne à l'image suivante
            target.schedule.Execute(() =>
            {
                silentFocus = true;
                target.Focus();
                silentFocus = false;
            });
        }

        private void OnNewGame()
        {
            if (Flow == null) return;
            if (!Flow.HasSave)
            {
                Flow.NewGame();
                return;
            }
            Confirm(newButton, "Commencer une nouvelle partie ?", "La partie en cours sera effacée.", () => Flow.NewGame());
        }

        private void OpenOptions(Button from)
        {
            focusAfterOverlay = from;
            overlay = Overlay.Options;
            foreach (OptionRow row in optionRows) row.Refresh();
            Refresh();
        }

        private void Confirm(Button from, string message, string detail, Action onYes)
        {
            focusAfterOverlay = from;
            confirmMessage.text = message;
            confirmDetail.text = detail;
            confirmDetail.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
            confirmAction = onYes;
            overlay = Overlay.Confirm;
            Refresh();
        }

        private void CloseOverlay()
        {
            if (overlay == Overlay.Options) GameSettings.Save();
            overlay = Overlay.None;
            confirmAction = null;
            Refresh();
        }

        // Échap : ferme l'écran par-dessus, ou reprend le jeu depuis la pause
        private void OnCancel(NavigationCancelEvent e)
        {
            if (Time.unscaledTime - shownAt < cancelGrace) return;
            if (overlay != Overlay.None)
            {
                sounds.PlayBack();
                CloseOverlay();
            }
            else if (Flow != null && Flow.State == GameState.Paused)
            {
                sounds.PlayBack();
                Flow.Resume();
            }
        }

        // ---------- Options ----------

        private void BuildOptions()
        {
            VisualElement list = root.Q("options-list");
            list.Clear();
            optionRows.Clear();

            AddVolume(list, "Volume général", () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
            AddVolume(list, "Musique et ambiance", () => GameSettings.MusicVolume, GameSettings.SetMusicVolume);
            AddVolume(list, "Effets", () => GameSettings.SfxVolume, GameSettings.SetSfxVolume);
            AddChoice(list, "Plein écran", () => YesNo(GameSettings.Fullscreen), _ => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
            CollectResolutions();
            AddChoice(list, "Résolution", ResolutionText, StepResolution);
            AddChoice(list, "Synchro verticale", () => YesNo(GameSettings.VSync), _ => GameSettings.SetVSync(!GameSettings.VSync));
        }

        private void AddVolume(VisualElement list, string name, Func<float> get, Action<float> set)
        {
            var row = new OptionRow(name, get, d => set(Mathf.Round((get() + d * 0.1f) * 10f) / 10f), set);
            row.Changed += sounds.PlayMove;
            optionRows.Add(row);
            list.Add(row.Root);
        }

        private void AddChoice(VisualElement list, string name, Func<string> text, Action<int> step)
        {
            var row = new OptionRow(name, text, step);
            row.Changed += sounds.PlayMove;
            optionRows.Add(row);
            list.Add(row.Root);
        }

        private static string YesNo(bool value) => value ? "Oui" : "Non";

        private void CollectResolutions()
        {
            resolutions.Clear();
            foreach (Resolution r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
        }

        private int ResolutionIndex()
        {
            int w = GameSettings.ResolutionWidth > 0 ? GameSettings.ResolutionWidth : Screen.currentResolution.width;
            int h = GameSettings.ResolutionHeight > 0 ? GameSettings.ResolutionHeight : Screen.currentResolution.height;
            int index = resolutions.IndexOf(new Vector2Int(w, h));
            return index >= 0 ? index : resolutions.Count - 1;
        }

        private string ResolutionText()
        {
            Vector2Int r = resolutions[ResolutionIndex()];
            return r.x + " × " + r.y;
        }

        private void StepResolution(int direction)
        {
            int index = Mathf.Clamp(ResolutionIndex() + direction, 0, resolutions.Count - 1);
            GameSettings.SetResolution(resolutions[index].x, resolutions[index].y);
        }

        // ---------- Mort, sauvegarde ----------

        private void UpdateDeath()
        {
            if (Flow == null || Flow.State != GameState.Dead) return;
            float t = Flow.DeathElapsed;
            float a = Mathf.Clamp01((t - deathWordDelay) / Mathf.Max(0.01f, deathWordFade));
            a = a * a * (3f - 2f * a);
            deathLabel.style.opacity = a;
            // Les lettres s'écartent lentement : le mot se dissout
            deathLabel.style.letterSpacing = 12f + t * 7f;
            deathHint.style.opacity = Mathf.Clamp01((t - deathHintDelay) / 0.5f) * 0.8f;
        }

        private void OnSaved()
        {
            savedAt = Time.unscaledTime;
        }

        private void UpdateSaveIndicator()
        {
            float since = Time.unscaledTime - savedAt;
            bool visible = since < saveIndicatorTime;
            saveIndicator.EnableInClassList("save-indicator--visible", visible);
            if (visible) saveGlyph.style.rotate = new Rotate(new Angle(45f + since * 180f));
        }

        // ---------- Outils ----------

        private Button Wire(string name, Action onClick)
        {
            Button button = root.Q<Button>(name);
            if (button == null)
            {
                Debug.LogWarning($"GameScreens : bouton « {name} » absent de GameScreens.uxml");
                return null;
            }
            button.clicked += () =>
            {
                if (Flow != null && Flow.Busy) return;
                sounds.PlaySubmit();
                onClick();
            };
            // La souris donne le focus : clavier et souris partagent la même surbrillance
            button.RegisterCallback<PointerEnterEvent>(_ => { if (button.enabledInHierarchy) button.Focus(); });
            return button;
        }

        private IEnumerable<VisualElement> AllScreens()
        {
            yield return titleScreen;
            yield return pauseScreen;
            yield return optionsScreen;
            yield return confirmScreen;
            yield return deathScreen;
            yield return endScreen;
        }

        // Écran caché : transparent (fondu USS) et désactivé (ni clic, ni focus au clavier)
        private static void SetVisible(VisualElement screen, bool visible)
        {
            screen.EnableInClassList("screen--visible", visible);
            screen.SetEnabled(visible);
            screen.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        private static string ControlsText()
        {
            string K(GameAction a) => GameInput.KeyName(a);
            return $"{K(GameAction.Left)} {K(GameAction.Right)}  bouger      {K(GameAction.Jump)}  sauter      " +
                   $"{K(GameAction.Parry)}  parer      {K(GameAction.Run)}  courir      " +
                   $"{K(GameAction.Sew)} maintenu  recoudre      {K(GameAction.Pause)}  pause";
        }

        // Dégradé horizontal noir → transparent, derrière le menu du titre
        private static Texture2D MakeShade(float strength)
        {
            const int width = 256;
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                float a = strength * (1f - t * t * (3f - 2f * t));
                tex.SetPixel(x, 0, new Color(0.02f, 0.02f, 0.04f, a));
            }
            tex.Apply();
            return tex;
        }
    }
}
