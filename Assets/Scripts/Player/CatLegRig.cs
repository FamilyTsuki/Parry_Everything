using UnityEngine;

namespace ParryEverything.Player
{
    // Repères de la patte de chat, posés sur le prefab CatLeg par Parry Everything > Reconstruire les modèles du chat.
    // CatBody s'en sert pour plier la patte (cinématique inverse). Ne pas modifier à la main :
    // la longueur se règle dans CatModelSettings, ces valeurs suivent.
    // Repère : la hanche est l'origine, patte vue de profil (X = avant, Y = haut).
    public class CatLegRig : MonoBehaviour
    {
        [SerializeField] private Vector3 kneeOffset = new Vector3(0.14f, -0.36f, 0f);   // hanche → genou
        [SerializeField] private Vector3 ankleOffset = new Vector3(0f, -0.47f, 0f);     // genou → cheville

        public Vector3 KneeOffset => kneeOffset;
        public Vector2 Thigh => kneeOffset;
        public Vector2 Shin => ankleOffset;

        // Appelé par le générateur de modèles
        public void SetGeometry(Vector3 knee, Vector3 ankleFromKnee)
        {
            kneeOffset = knee;
            ankleOffset = ankleFromKnee;
        }
    }
}
