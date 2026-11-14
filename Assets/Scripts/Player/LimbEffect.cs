namespace ParryEverything.Player
{
    // Effet subi par un membre (GAME_DESIGN 6.4, propositions). Causes et soins : à définir.
    public enum LimbEffect
    {
        None,
        Bleeding,      // hémorragie : perd de la vie en continu
        Infection,     // s'affaiblit et gagne un membre voisin
        Hypertrophy    // plus solide mais alourdit le chat
    }
}
