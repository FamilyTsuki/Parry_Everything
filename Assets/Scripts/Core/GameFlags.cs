namespace ParryEverything.Core
{
    // Noms des étapes de progression enregistrées dans GameSession (et la sauvegarde)
    public static class GameFlags
    {
        // Le chat a arraché un morceau du tas de chair du Gardien
        public const string GuardianChunkTaken = "zone1.guardian-chunk-taken";
        // Il porte ce morceau (pas encore donné à l'âme marchande)
        public const string GuardianChunkCarried = "zone1.guardian-chunk-carried";
        // L'âme marchande lui a fabriqué ses 2 pattes
        public const string MerchantLegsMade = "zone1.merchant-legs-made";
    }
}
