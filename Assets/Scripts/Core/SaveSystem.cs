using System;
using System.Collections.Generic;
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
        public const int CurrentVersion = 3;
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
        private static SaveData Migrate(SaveData data)
        {
            if (data.version == 1)
            {
                data.limbs = SplitLimbsV2(data.limbs);
                data.version = 2;
            }
            if (data.version == 2)
            {
                data.limbs = SplitArmsV3(data.limbs);
                data.version = 3;
            }
            data.version = CurrentVersion;
            return data;
        }

        // Version 1 → 2 : le torse (chair) et chaque patte se découpent en 3 parties cousues les unes sur les autres.
        // Valeurs figées ici exprès (une migration ne doit pas changer si le jeu change plus tard) :
        //   kind 0 chair → ventre (25/80 de la vie), poitrine 5 (30/80), bassin 6 (25/80) ;
        //   kind 1 patte → cuisse (18/40), jarret 7 (14/40), pied 8 (8/40) ;
        //   bras (2) et tête (3) cousus sur la poitrine, queue (4) sur le bassin.
        // Chaque nouvelle partie garde l'état de l'ancien membre (part de vie, effet, en poche).
        private static List<LimbSave> SplitLimbsV2(List<LimbSave> old)
        {
            var result = new List<LimbSave>();
            if (old == null) return result;
            int legIndex = 0;
            foreach (LimbSave limb in old)
            {
                switch (limb.kind)
                {
                    case 0:
                        result.Add(Part(limb, 0, "ventre", null, 25f / 80f, new Vector3(0f, 0.6f, 0f)));
                        result.Add(Part(limb, 5, "poitrine", "ventre", 30f / 80f, new Vector3(0.08f, 0.95f, 0f)));
                        result.Add(Part(limb, 6, "bassin", "ventre", 25f / 80f, new Vector3(-0.05f, 0.3f, 0f)));
                        break;
                    case 1:
                        string side = limb.id != null && limb.id.StartsWith("patte ") ? limb.id.Substring(6) : (legIndex == 0 ? "proche" : "loin");
                        legIndex++;
                        Vector3 a = limb.anchor;
                        result.Add(Part(limb, 1, "cuisse " + side, "bassin", 18f / 40f, new Vector3(a.x, 0.1f, a.z)));
                        result.Add(Part(limb, 7, "jarret " + side, "cuisse " + side, 14f / 40f, new Vector3(a.x + 0.04f, -0.2f, a.z)));
                        result.Add(Part(limb, 8, "pied " + side, "jarret " + side, 8f / 40f, new Vector3(a.x + 0.1f, -0.42f, a.z)));
                        break;
                    default:
                        limb.parentId = limb.kind == 4 ? "bassin" : "poitrine";
                        result.Add(limb);
                        break;
                }
            }
            return result;
        }

        // Version 2 → 3 : chaque bras se découpe en haut du bras (14/30 de sa vie), avant-bras 9 (10/30) et main 10 (6/30),
        // cousus les uns sur les autres. Valeurs figées ici exprès. Chaque partie garde l'état de l'ancien bras.
        private static List<LimbSave> SplitArmsV3(List<LimbSave> old)
        {
            var result = new List<LimbSave>();
            if (old == null) return result;
            int armIndex = 0;
            foreach (LimbSave limb in old)
            {
                if (limb.kind != 2)
                {
                    result.Add(limb);
                    continue;
                }
                string side = limb.id != null && limb.id.StartsWith("bras ") ? limb.id.Substring(5) : (armIndex == 0 ? "proche" : "loin");
                armIndex++;
                Vector3 a = limb.anchor;
                string upper = "bras " + side, fore = "avant-bras " + side;
                result.Add(Part(limb, 2, upper, limb.parentId ?? "poitrine", 14f / 30f, a));
                result.Add(Part(limb, 9, fore, upper, 10f / 30f, new Vector3(a.x + 0.02f, 0.7f, a.z)));
                result.Add(Part(limb, 10, "main " + side, fore, 6f / 30f, new Vector3(a.x + 0.05f, 0.45f, a.z)));
            }
            return result;
        }

        private static LimbSave Part(LimbSave from, int kind, string id, string parentId, float share, Vector3 anchor)
        {
            float ratio = from.maxLife > 0f ? Mathf.Clamp01(from.life / from.maxLife) : 0f;
            float maxLife = Mathf.Max(1f, from.maxLife * share);
            return new LimbSave
            {
                kind = kind, id = id, parentId = parentId, maxLife = maxLife, life = maxLife * ratio,
                effect = from.effect, anchor = anchor, pocketed = from.pocketed,
            };
        }
    }
}
