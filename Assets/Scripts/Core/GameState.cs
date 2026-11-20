namespace ParryEverything.Core
{
    // États du flux de jeu
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        Dead,
        End,
        Cutscene,      // scène sans contrôle du joueur (vision, couture)
        Waking         // réveil après la mort : le temps coule, le joueur n'agit pas encore
    }
}
