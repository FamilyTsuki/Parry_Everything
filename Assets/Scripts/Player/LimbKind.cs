namespace ParryEverything.Player
{
    // Nature d'un membre. Chaque membre est cousu sur un autre (Limb.ParentId) :
    //   ventre (Flesh, cœur autour de l'âme) ← poitrine (tête, bras) et bassin (cuisses, queue) ;
    //   cuisse (Leg) ← jarret (Shin) ← pied (Foot) ; haut du bras (Arm) ← avant-bras (Forearm) ← main (Hand).
    // Une partie arrachée emporte tout ce qui y est cousu ; le ventre éclaté = retour à l'âme.
    // Ajouter les nouveaux types À LA FIN : la sauvegarde stocke le numéro.
    public enum LimbKind
    {
        Flesh,   // ventre : chair autour de l'âme (chair du Gardien), cœur du torse ; éclaté = retour à l'âme
        Leg,     // cuisse de la grande patte arrière (porte le jarret)
        Arm,     // bras (patte avant du chat redressé), cousu sur la poitrine
        Head,    // tête : oreilles, museau, yeux ; cousue sur la poitrine
        Tail,    // grosse queue touffue, cousue sur le bassin
        Chest,   // poitrine : haut du torse, porte la tête et les bras
        Pelvis,  // bassin : bas du torse, porte les cuisses et la queue
        Shin,    // jarret : du genou à la cheville, porte le pied
        Foot,    // pied : sans lui, le chat boite sur son moignon
        Forearm, // avant-bras : du coude au poignet, porte la main ; cousu sur le haut du bras (Arm)
        Hand,    // main griffue, au bout de l'avant-bras
    }
}
