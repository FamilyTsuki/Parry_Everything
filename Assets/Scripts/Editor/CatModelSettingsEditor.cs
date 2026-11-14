using UnityEditor;
using UnityEngine;

namespace ParryEverything.Editor
{
    // Inspector des réglages du chat : rappel de ce qui se règle où, et bouton « Reconstruire ».
    [CustomEditor(typeof(CatModelSettings))]
    public class CatModelSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Forme des modèles du chat (corps et pattes).\n" +
                "Repère : X avant, Y haut, Z profondeur (− = côté caméra).\n" +
                "Placement en jeu (hauteur, inclinaison, démarche) : composant CatBody du « player ».",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Reconstruire maintenant", GUILayout.Height(28f)))
            {
                CatModelBuilder.BuildAll();
            }
        }
    }
}
