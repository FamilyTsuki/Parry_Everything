using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // Contenu de la sauvegarde, écrit en JSON par SaveSystem.
    // Ajouter un champ : lui donner une valeur par défaut sensée (les anciennes sauvegardes ne l'ont pas).
    // Changer le sens d'un champ : monter SaveSystem.CurrentVersion et écrire la migration.
    [Serializable]
    public class SaveData
    {
        public int version;
        public string savedAtUtc;            // date ISO 8601, pour l'affichage

        // Point de réapparition (sanctuaire)
        public bool hasRespawnPoint;
        public Vector3 respawnPoint;
        public string sanctuaryId;
        public string placeName;             // nom du lieu, affiché sur « Continuer » (vide : pas de nom)

        // Progression
        public List<string> bossesDefeated = new List<string>();
        public List<LimbSave> limbs = new List<LimbSave>();   // vide : le chat est une âme
        public List<string> flags = new List<string>();       // étapes de la progression (GameSession.Flags)

        // Comportement (BehaviorLedger), enregistré pour les fins
        public int soulsAbsorbed;
        public int enemiesSpared;
        public int wanderingSoulsBumped;
    }
}
