namespace ParryEverything.Core
{
    // Actions du joueur, indépendantes des touches
    public enum GameAction
    {
        Left,
        Right,
        Jump,
        Parry,
        Run,
        Sew,
        Pause,
        Confirm,
        NewGame,
        Menu,
        Quit,
        // Outils de debug (éditeur et builds de développement)
        DebugHeal,
        DebugGodMode,
        DebugToBoss,
        DebugOverlay,
        DebugToggleBody,
        DebugKillBoss
    }
}
