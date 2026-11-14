using UnityEngine;

namespace ParryEverything.Core
{
    // Ennemi dont on peut absorber l'âme : parer à son contact quand il est étourdi.
    // Son corps redevient chair inerte (GAME_DESIGN D3).
    public interface ISoulVessel
    {
        bool CanAbsorb { get; }
        Vector3 Position { get; }
        void Absorb();
    }
}
