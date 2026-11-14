using UnityEngine;

namespace ParryEverything.Core
{
    // Petits outils de dessin pour les HUD en OnGUI.
    // GUI.color est partagé entre tous les OnGUI : chaque outil le remet comme il l'a trouvé,
    // sinon un fond noir dessiné avant colore en noir le texte dessiné après.
    public static class GuiDraw
    {
        // Hauteur d'écran de référence pour les tailles de police
        private const float ReferenceHeight = 1080f;

        // Rectangle plein de la couleur donnée
        public static void FilledRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // Texte avec ombre portée, dans la couleur donnée (indépendante de GUI.color)
        public static void ShadowLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            Color old = GUI.color;
            GUI.color = Color.white;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            GUI.color = old;
        }

        // Taille de police pensée pour un écran 1080p, ramenée à l'écran actuel (et jamais illisible)
        public static int FontSize(float sizeAt1080p, int min = 10)
        {
            return Mathf.Max(min, Mathf.RoundToInt(sizeAt1080p * Screen.height / ReferenceHeight));
        }

        // Comme FontSize, mais réduit encore si le texte (environ 'chars' caractères) déborderait en largeur
        public static int FontSizeFitting(float sizeAt1080p, int chars, float widthShare = 0.92f, int min = 10)
        {
            int size = FontSize(sizeAt1080p, min);
            // Largeur moyenne d'un caractère ≈ 0,55 × la taille de police
            float maxByWidth = Screen.width * widthShare / Mathf.Max(1, chars) / 0.55f;
            return Mathf.Max(min, Mathf.Min(size, Mathf.FloorToInt(maxByWidth)));
        }
    }
}
