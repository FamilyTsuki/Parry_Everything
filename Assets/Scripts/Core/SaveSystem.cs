using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ParryEverything.Core
{
    // Sauvegarde : un fichier JSON versionné dans Application.persistentDataPath.
    // Les méthodes acceptent un chemin, pour que les tests écrivent ailleurs que dans la vraie sauvegarde.
    public static class SaveSystem
    {
        // À monter quand le format change de sens ; Migrate fait passer les anciennes versions à la nouvelle
        public const int CurrentVersion = 1;
        public const string FileName = "save.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave(string path = null)
        {
            return File.Exists(path ?? DefaultPath);
        }

        // Écrit la sauvegarde. Passe par un fichier temporaire : un arrêt brutal ne laisse pas un fichier à moitié écrit.
        public static void Save(SaveData data, string path = null)
        {
            path = path ?? DefaultPath;
            data.version = CurrentVersion;
            data.savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson(data));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        // Lit la sauvegarde. Renvoie null si elle n'existe pas ou est illisible (le jeu repart alors de zéro).
        public static SaveData Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path)) return null;
            try
            {
                return FromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Sauvegarde illisible ({path}) : {e.Message}");
                return null;
            }
        }

        public static void Delete(string path = null)
        {
            path = path ?? DefaultPath;
            if (File.Exists(path)) File.Delete(path);
        }

        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        // null si le texte n'est pas une sauvegarde valide, ou vient d'une version plus récente du jeu
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version < 1) return null;
            if (data.version > CurrentVersion)
            {
                Debug.LogWarning($"Sauvegarde en version {data.version}, ce jeu lit jusqu'à la {CurrentVersion} : ignorée.");
                return null;
            }
            return Migrate(data);
        }

        // Fait passer une ancienne sauvegarde au format actuel, une version à la fois.
        // Exemple pour la future version 2 : if (data.version == 1) { ...; data.version = 2; }
        private static SaveData Migrate(SaveData data)
        {
            data.version = CurrentVersion;
            return data;
        }
    }
}
