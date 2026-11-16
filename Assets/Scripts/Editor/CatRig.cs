using UnityEngine;

namespace ParryEverything.Editor
{
    // Articulations et points d'attache du chat, en mètres. Viennent des réglages (modèle généré)
    // ou des repères du fichier Blender (CatBlenderImport). Servent à construire les prefabs.
    public sealed class CatRig
    {
        // Repère du torse
        public Vector3 Neck;        // base du cou : la tête s'y accroche
        public Vector3 Shoulder;    // épaule côté caméra (z négatif)
        public Vector3 TailBase;    // naissance de la queue
        public Vector3 Hip;         // hanche côté caméra (z négatif)

        public Vector3 Knee;        // repère de la hanche : hanche → genou
        public Vector3 AnkleFromKnee;   // genou → cheville
        public Vector3 Elbow;       // repère de l'épaule : épaule → coude

        // Repère de la tête : œil côté caméra (l'autre en miroir) et truffe
        public Vector3 Eye;
        public Vector3 Nose;

        public int TailSegments;
        public float TailSegmentLength;

        // Pose debout au repos (chat immobile, regarde à droite)
        public Vector2 StandingPosition;    // centre du torse / centre du joueur
        public float StandingTilt;          // torse penché vers l'avant (degrés)
        public float LegSplay;              // patte côté caméra écartée (degrés)
        public float FootForward;           // cheville devant la hanche
        public float AnkleHeight;           // cheville au-dessus du sol
        public Quaternion HeadRest = Quaternion.identity;    // tête / torse
        public Quaternion ArmRest = Quaternion.identity;     // bras côté caméra / torse
        public Quaternion ElbowRest = Quaternion.identity;   // avant-bras / bras
        public Vector3[] TailPositions;     // segment i / segment i−1 (le 1er : 0, il est sur TailBase)
        public Quaternion[] TailRotations;  // segment i / segment i−1 (le 1er : / torse)

        // Pose lue dans les réglages (modèle généré, ou fichier Blender sans pose)
        public void SetPoseFrom(CatModelSettings s)
        {
            StandingPosition = s.standingPosition;
            StandingTilt = s.standingTilt;
            LegSplay = s.legSplay;
            FootForward = s.footForward;
            AnkleHeight = s.ankleHeight;
            HeadRest = Quaternion.Euler(0f, 0f, s.headAngle);
            ArmRest = Quaternion.Euler(0f, 0f, s.armAngle);
            ElbowRest = Quaternion.Euler(0f, 0f, s.elbowAngle);
            TailRotations = new Quaternion[TailSegments];
            for (int i = 0; i < TailSegments; i++) TailRotations[i] = Quaternion.Euler(0f, 0f, i == 0 ? -s.tailLift : -s.tailCurl);
        }

        // Segments de queue en ligne droite (vers −X), s'ils ne viennent pas du fichier
        public void SetStraightTail()
        {
            TailPositions = new Vector3[TailSegments];
            for (int i = 1; i < TailSegments; i++) TailPositions[i] = new Vector3(-TailSegmentLength, 0f, 0f);
        }

        // Cheville dans le repère de la hanche
        public Vector3 Ankle => Knee + AnkleFromKnee;
    }
}
