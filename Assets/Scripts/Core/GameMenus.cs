using UnityEngine;

namespace ParryEverything.Core
{
    // Écrans provisoires en OnGUI : titre, pause, mort, fin. Lit l'état de GameFlow, ne le change pas.
    // Les vrais menus (options, manette) remplaceront ce script en phase 5.
    [RequireComponent(typeof(GameFlow))]
    public class GameMenus : MonoBehaviour
    {
        [SerializeField] private string gameTitle = "PARRY EVERYTHING";
        [Range(0f, 1f)] [SerializeField] private float menuDim = 0.75f;   // assombrissement derrière le titre et la fin
        [Range(0f, 1f)] [SerializeField] private float pauseDim = 0.55f;

        private GameFlow flow;
        private GUIStyle titleStyle;
        private GUIStyle hintStyle;

        private void Awake()
        {
            flow = GetComponent<GameFlow>();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || flow == null) return;
            GUI.depth = -100; // par-dessus les autres HUD (voile rouge, barre du boss)

            switch (flow.State)
            {
                case GameState.Menu:
                    DrawTitleScreen();
                    break;
                case GameState.Paused:
                    DrawScreen("PAUSE", pauseDim,
                        $"{K(GameAction.Pause)} : reprendre    {K(GameAction.Menu)} : menu titre    {K(GameAction.Quit)} : quitter");
                    break;
                case GameState.Dead:
                    // Fondu au noir avant la réapparition
                    Color old = GUI.color;
                    GuiDraw.FilledRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, flow.DeathProgress));
                    GUI.color = old;
                    break;
                case GameState.End:
                    DrawScreen("À SUIVRE", menuDim, $"{K(GameAction.Confirm)} : menu titre");
                    break;
            }
        }

        private void DrawTitleScreen()
        {
            string choices = flow.HasSave
                ? $"{K(GameAction.Confirm)} : continuer    {K(GameAction.NewGame)} : nouvelle partie    {K(GameAction.Quit)} : quitter"
                : $"{K(GameAction.Confirm)} : nouvelle partie    {K(GameAction.Quit)} : quitter";
            string controls = $"{K(GameAction.Left)} {K(GameAction.Right)} : bouger    {K(GameAction.Jump)} : sauter    " +
                              $"{K(GameAction.Parry)} : parer    {K(GameAction.Run)} : courir    " +
                              $"{K(GameAction.Sew)} (maintenir) : recoudre    {K(GameAction.Pause)} : pause";
            DrawScreen(gameTitle, menuDim, choices, controls);
        }

        private static string K(GameAction action) => GameInput.KeyName(action);

        // Fond assombri, titre au centre, une ou deux lignes d'aide en dessous
        private void DrawScreen(string title, float dim, string hint, string subHint = null)
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            float w = Screen.width, h = Screen.height;
            // La taille suit la hauteur, mais se réduit si la ligne déborderait en largeur
            titleStyle.fontSize = GuiDraw.FontSizeFitting(86f, title.Length, 0.9f, 18);
            int longest = Mathf.Max(hint.Length, subHint != null ? subHint.Length : 0);
            hintStyle.fontSize = GuiDraw.FontSizeFitting(32f, longest, 0.9f, 11);

            Color old = GUI.color;
            GuiDraw.FilledRect(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, dim));
            GUI.color = Color.white;
            GuiDraw.ShadowLabel(new Rect(0f, h * 0.35f, w, h * 0.12f), title, titleStyle, Color.white);
            float margin = w * 0.05f;
            GuiDraw.ShadowLabel(new Rect(margin, h * 0.52f, w - 2f * margin, h * 0.08f), hint, hintStyle, new Color(1f, 1f, 1f, 0.85f));
            if (subHint != null)
            {
                GuiDraw.ShadowLabel(new Rect(margin, h * 0.61f, w - 2f * margin, h * 0.12f), subHint, hintStyle, new Color(1f, 1f, 1f, 0.6f));
            }
            GUI.color = old;
        }
    }
}
