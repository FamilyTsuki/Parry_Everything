using UnityEngine;

namespace ParryEverything.Core
{
    // Petits outils de dessin pour les HUD en OnGUI.
    public static class GuiDraw
    {
        // Rectangle plein de la couleur donnée (modifie GUI.color : à restaurer par l'appelant)
        public static void FilledRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        // Texte avec ombre portée
        public static void ShadowLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }
    }
}
