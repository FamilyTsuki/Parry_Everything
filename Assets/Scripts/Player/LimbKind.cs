namespace ParryEverything.Player
{
    // Nature d'un membre. La chair est le cœur du corps (le torse) : tous les autres membres y sont cousus.
    // Ajouter les nouveaux types À LA FIN : la sauvegarde stocke le numéro.
    public enum LimbKind
    {
        Flesh,   // chair autour de l'âme (chair du Gardien), devient le torse ; éclatée = retour à l'âme
        Leg,     // grande patte arrière de chat
        Arm,     // bras (patte avant du chat redressé)
        Head,    // tête : oreilles, museau, yeux
        Tail     // queue au bout en flamme
    }
}
