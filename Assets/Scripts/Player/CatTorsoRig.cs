using UnityEngine;

namespace ParryEverything.Player
{
    // Points d'attache sur le torse (prefab FleshBall), posés par Parry Everything > Reconstruire les modèles du chat.
    // CatBody y accroche la tête, les bras, la queue et les pattes : ils suivent le torse (inclinaison, demi-tour, pas).
    // Ne pas modifier à la main : ces points se règlent dans CatModelSettings.
    // Repère du torse, chat qui regarde à droite : X = avant, Y = haut, Z = profondeur (− = côté caméra).
    public class CatTorsoRig : MonoBehaviour
    {
        [SerializeField] private Vector3 neck = new Vector3(0.08f, 0.5f, 0f);        // base de la tête
        [SerializeField] private Vector3 shoulder = new Vector3(0.06f, 0.28f, -0.22f); // épaule côté caméra (l'autre : z opposé)
        [SerializeField] private Vector3 tailBase = new Vector3(-0.24f, -0.3f, 0f);   // naissance de la queue
        [SerializeField] private Vector3 hip = new Vector3(-0.05f, -0.4f, -0.16f);    // hanche côté caméra (l'autre : z opposé)

        public Vector3 Neck => neck;
        public Vector3 Shoulder => shoulder;
        public Vector3 TailBase => tailBase;
        public Vector3 Hip => hip;

        // Appelé par le générateur de modèles
        public void SetPoints(Vector3 neckPoint, Vector3 shoulderPoint, Vector3 tailPoint, Vector3 hipPoint)
        {
            neck = neckPoint;
            shoulder = shoulderPoint;
            tailBase = tailPoint;
            hip = hipPoint;
        }
    }
}
