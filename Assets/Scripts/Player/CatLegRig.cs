using UnityEngine;

namespace ParryEverything.Player
{
    // Repères de la patte de chat, posés sur le prefab CatLeg par Parry Everything > Reconstruire les modèles du chat.
    // CatBody s'en sert pour plier la patte (cinématique inverse). Ne pas modifier à la main :
    // ces valeurs viennent du fichier Blender (ou de CatModelSettings pour le modèle généré).
    // Repère : la hanche est l'origine, patte vue de profil (X = avant, Y = haut), avant l'écart (splay).
    public class CatLegRig : MonoBehaviour
    {
        [SerializeField] private Vector3 kneeOffset = new Vector3(0.14f, -0.36f, 0f);   // hanche → genou
        [SerializeField] private Vector3 ankleOffset = new Vector3(0f, -0.47f, 0f);     // genou → cheville
        // Pose debout, chat immobile
        [SerializeField] private float splay = 10f;          // patte côté caméra écartée vers l'extérieur (degrés, l'autre en miroir)
        [SerializeField] private float footForward = 0f;     // cheville devant la hanche (mètres)
        [SerializeField] private float ankleHeight = 0.07f;  // cheville au-dessus du sol (mètres)

        public Vector3 KneeOffset => kneeOffset;
        public Vector3 AnkleOffset => ankleOffset;
        public Vector2 Thigh => kneeOffset;
        public Vector2 Shin => ankleOffset;
        public float Splay => splay;
        public float FootForward => footForward;
        public float AnkleHeight => ankleHeight;

        // Appelé par le générateur de modèles
        public void SetGeometry(Vector3 knee, Vector3 ankleFromKnee)
        {
            kneeOffset = knee;
            ankleOffset = ankleFromKnee;
        }

        public void SetStance(float splayAngle, float forward, float height)
        {
            splay = splayAngle;
            footForward = forward;
            ankleHeight = height;
        }
    }
}
