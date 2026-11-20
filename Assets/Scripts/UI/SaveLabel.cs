using System;
using System.Globalization;
using ParryEverything.Core;

namespace ParryEverything.UI
{
    // Texte sous « Continuer » : lieu de la dernière sauvegarde et quand (« Sanctuaire du Gardien · il y a 5 min »).
    public static class SaveLabel
    {
        public const string NoPlace = "Au départ";

        public static string Describe(SaveData data, DateTime nowUtc)
        {
            if (data == null) return string.Empty;
            string place = string.IsNullOrEmpty(data.placeName) ? NoPlace : data.placeName;
            string when = When(data.savedAtUtc, nowUtc);
            return string.IsNullOrEmpty(when) ? place : place + " · " + when;
        }

        // Date relative ; vide si la date est absente ou illisible
        public static string When(string savedAtUtc, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(savedAtUtc)) return string.Empty;
            if (!DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved))
            {
                return string.Empty;
            }
            saved = saved.ToUniversalTime();
            TimeSpan delta = nowUtc - saved;
            if (delta.TotalMinutes < 1) return "à l'instant";
            if (delta.TotalMinutes < 60) return $"il y a {(int)delta.TotalMinutes} min";
            if (delta.TotalHours < 24) return $"il y a {(int)delta.TotalHours} h";
            if (delta.TotalHours < 48) return "hier";
            return "le " + saved.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
    }
}
