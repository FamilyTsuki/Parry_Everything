using UnityEngine;

namespace ParryEverything.Editor
{
    // Réglages des modèles 3D du chat : le corps de chair (FleshBall) et la patte (CatLeg).
    // Fichier : Assets/Meshes/Player/CatModelSettings.asset
    //   → menu Parry Everything > Modèle du chat : réglages (le sélectionne, le crée s'il manque).
    // Chaque modification dans l'Inspector reconstruit les modèles tout de suite, même en Play.
    // « Reset » (menu ⋮ de l'Inspector) remet toutes les valeurs ci-dessous.
    //
    // Repère du modèle (chat qui regarde à droite) :
    //   X = avant (+) / arrière (−), Y = haut, Z = profondeur (− = côté caméra).
    //
    // Ce fichier règle la FORME. Le placement EN JEU (hauteur du corps debout, inclinaison, attache des pattes,
    // démarche) se règle sur le composant CatBody de l'objet « player ».
    public class CatModelSettings : ScriptableObject
    {
        [Header("Reconstruction")]
        [Tooltip("Reconstruit à chaque modification. Décocher pour enchaîner plusieurs changements, puis « Reconstruire ».")]
        public bool autoRebuild = true;

        [Header("Corps — forme d'œuf / capsule")]
        [Tooltip("Demi-tailles en mètres : X largeur (avant-arrière), Y hauteur, Z épaisseur.")]
        public Vector3 bodyRadii = new Vector3(0.29f, 0.56f, 0.28f);
        [Tooltip("> 0 : haut plus étroit que le bas (œuf). < 0 : l'inverse. 0 : symétrique.")]
        [Range(-0.5f, 0.5f)] public float bodyTaper = 0.14f;
        [Tooltip("1 : ovale. Plus petit : flancs droits et bouts arrondis (capsule).")]
        [Range(0.3f, 1f)] public float bodyFlank = 0.75f;
        [Tooltip("Courbure. > 0 : les deux bouts partent vers l'arrière (haricot). < 0 : vers l'avant.")]
        [Range(-0.3f, 0.3f)] public float bodyBend = 0.03f;
        [Tooltip("Finesse du maillage. 4 ≈ 2 500 sommets, 5 ≈ 10 000.")]
        [Range(2, 5)] public int bodyDetail = 4;

        [Header("Corps — placement dans le modèle")]
        [Tooltip("Décalage du corps (mètres) par rapport au centre du modèle.")]
        public Vector3 bodyOffset = new Vector3(0f, -0.03f, 0f);
        [Tooltip("Rotation du corps (degrés). Z : penche avant/arrière. Y : tourne. X : roule vers la caméra.")]
        public Vector3 bodyRotation = Vector3.zero;
        [Tooltip("Échelle du corps, appliquée après la forme (1 = taille réglée plus haut).")]
        public Vector3 bodyScale = Vector3.one;

        [Header("Corps — chair")]
        [Tooltip("Hauteur des bosses et du grain. 0 : lisse. 0.3 : patate.")]
        [Range(0f, 0.5f)] public float lumpiness = 0.06f;
        [Tooltip("Nombre de grosses bosses.")]
        [Range(0, 40)] public int lumpCount = 13;
        [Tooltip("Nombre de morceaux de chair cousus ensemble.")]
        [Range(2, 30)] public int patchCount = 10;
        [Tooltip("Tirage aléatoire : change la place des bosses, des morceaux et leurs couleurs.")]
        public int seed = 3;
        [Tooltip("Largeur des coutures entre les morceaux.")]
        [Range(0f, 0.15f)] public float seamWidth = 0.06f;
        [Tooltip("Profondeur du sillon des coutures.")]
        [Range(0f, 0.15f)] public float seamDepth = 0.055f;

        [Header("Corps — couleurs")]
        [Tooltip("Couleurs possibles des morceaux de chair (tirées au hasard selon « seed »).")]
        public Color[] fleshPalette =
        {
            new Color(0.36f, 0.08f, 0.09f),   // rouge sombre
            new Color(0.24f, 0.07f, 0.12f),   // lie-de-vin
            new Color(0.42f, 0.16f, 0.12f),   // brun rouge
            new Color(0.19f, 0.09f, 0.13f),   // violacé
            new Color(0.3f, 0.12f, 0.1f),     // brun
            new Color(0.5f, 0.2f, 0.17f),     // rose cuit
            new Color(0.52f, 0.4f, 0.32f),    // peau morte, pâle
            new Color(0.14f, 0.11f, 0.17f),   // contusion
            new Color(0.66f, 0.2f, 0.18f),    // à vif
        };
        [Tooltip("Croûte sombre au fond des coutures.")]
        public Color crustColor = new Color(0.1f, 0.03f, 0.03f);
        [Tooltip("Points de couture. Alpha 0 : ils restent clairs quand la chair est abîmée (shader FleshLit).")]
        public Color stitchColor = new Color(0.86f, 0.8f, 0.68f, 0f);
        [Tooltip("Veines plus vives par endroits.")]
        public Color veinColor = new Color(0.62f, 0.12f, 0.12f);

        [Header("Pattes")]
        [Tooltip("Longueur de la patte (1 = première version, jugée trop petite). CatBody suit automatiquement.")]
        [Range(0.5f, 4f)] public float legLength = 2f;
        [Tooltip("Épaisseur de la patte.")]
        [Range(0.5f, 4f)] public float legThickness = 1.7f;
        [Tooltip("Couleur de la patte (bleu nuit).")]
        public Color legColor = new Color(0.1f, 0.1f, 0.17f);

        // Valeurs au dernier passage : OnValidate est aussi appelé au chargement, sans rien changer.
        // On ne reconstruit que si une valeur a vraiment bougé depuis.
        private string lastState;

        private void OnEnable()
        {
            lastState = JsonUtility.ToJson(this);
        }

        private void OnValidate()
        {
            string state = JsonUtility.ToJson(this);
            if (state == lastState) return;
            lastState = state;
            if (autoRebuild) CatModelBuilder.RequestRebuild();
        }
    }
}
