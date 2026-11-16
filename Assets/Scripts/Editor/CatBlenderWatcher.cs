using System;
using UnityEditor;

namespace ParryEverything.Editor
{
    // Le fichier Blender du chat vient d'être enregistré (Unity le réimporte) : reconstruit les modèles du jeu.
    public class CatBlenderWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CatModelSettings>(CatModelBuilder.SettingsPath);
            if (settings == null || settings.source != CatModelSettings.Source.Blender || settings.blenderModel == null) return;
            string path = AssetDatabase.GetAssetPath(settings.blenderModel);
            if (Array.IndexOf(imported, path) >= 0) CatModelBuilder.RequestRebuild();
        }
    }
}
