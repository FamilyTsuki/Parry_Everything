using UnityEngine.UIElements;

namespace ParryEverything.UI
{
    // Raccourcis pour construire l'interface en code
    public static class VisualElementExtensions
    {
        // Ajoute une classe USS et renvoie l'élément (pour enchaîner : new Label("x").WithClass("titre"))
        public static T WithClass<T>(this T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
