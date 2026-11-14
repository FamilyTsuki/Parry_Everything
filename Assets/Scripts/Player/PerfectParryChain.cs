namespace ParryEverything.Player
{
    // Compte les parades parfaites enchaînées (GAME_DESIGN 6.2, tranche verticale) :
    // chacune à moins de maxGap secondes de la précédente ; un coup reçu ou une parade ratée remet à zéro.
    // Logique pure (temps passé en paramètre), testée en EditMode.
    public class PerfectParryChain
    {
        private readonly float maxGap;
        private readonly int maxLevel;
        private float lastTime = float.NegativeInfinity;

        public int Level { get; private set; }

        public PerfectParryChain(float maxGap = 2f, int maxLevel = 3)
        {
            this.maxGap = maxGap;
            this.maxLevel = maxLevel;
        }

        // Une parade parfaite à l'instant time. Renvoie le niveau atteint (1 à maxLevel).
        public int Register(float time)
        {
            if (time - lastTime > maxGap) Level = 0;
            Level = Level < maxLevel ? Level + 1 : maxLevel;
            lastTime = time;
            return Level;
        }

        // À appeler régulièrement : le niveau retombe si la dernière parade parfaite est trop ancienne
        public int Tick(float time)
        {
            if (Level > 0 && time - lastTime > maxGap) Level = 0;
            return Level;
        }

        public void Reset()
        {
            Level = 0;
            lastTime = float.NegativeInfinity;
        }
    }
}
