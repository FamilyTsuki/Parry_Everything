using UnityEngine;

namespace ParryEverything.Editor
{
    // Réglages des modèles 3D du chat : torse (FleshBall), pattes (CatLeg), bras (CatArm), tête (CatHead), queue (CatTail).
    // Chaque membre a deux couches : la chair cousue dessous, la fourrure par-dessus (qui part quand il est abîmé).
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
        // Rendu du corps : un shader par style, tous avec la même chair et le même état des membres (FleshCore.cginc)
        public enum Style
        {
            Smooth,   // FleshLit : rendu réaliste lisse
            Fur,      // FleshFur : poils en couches (shell fur)
            Toon,     // FleshToon : aplats, liseré, contour
            Facets,   // FleshFacet : low-poly facetté
        }

        [Header("Reconstruction")]
        [Tooltip("Reconstruit à chaque modification. Décocher pour enchaîner plusieurs changements, puis « Reconstruire ».")]
        public bool autoRebuild = true;

        // D'où viennent les formes : générées d'après les réglages ci-dessous, ou modelées dans Blender
        public enum Source
        {
            Generated,   // CatModelBuilder construit tout d'après les réglages
            Blender,     // formes prises dans « blenderModel » (CatBlenderImport) ; chair, fourrure, yeux ajoutés par Unity
        }

        [Header("Source des formes")]
        [Tooltip("Generated : formes calculées d'après les réglages. Blender : formes du fichier « blenderModel ».\n" +
                 "Aller-retour : menu Parry Everything > Exporter le chat vers Blender, puis enregistrer en .blend dans Assets/Models/Cat.")]
        public Source source = Source.Generated;
        [Tooltip("Fichier Blender (.blend) du chat, importé par Unity. Objets attendus : Torso, Head, UpperArm, Forearm, Leg, " +
                 "TailSeg0…N ; repères : Neck, Shoulder, TailBase, Hip, Elbow, Knee, Ankle (+ Eye, Nose facultatifs).")]
        public GameObject blenderModel;

        [Header("Style")]
        [Tooltip("Rendu du corps (torse, membres) : lisse, fourrure douce, toon ou low-poly facetté.")]
        public Style style = Style.Facets;

        [Header("Torse — silhouette")]
        [Tooltip("Demi-tailles en mètres : X largeur (avant-arrière), Y hauteur, Z épaisseur.")]
        public Vector3 bodyRadii = new Vector3(0.26f, 0.58f, 0.25f);
        [Tooltip("Largeur du torse selon la hauteur. Horizontal : −1 = bas (bassin), 0 = milieu, 1 = haut (épaules).\n" +
                 "Vertical : multiplicateur de largeur (1 = largeur réglée plus haut). Creux = taille fine.")]
        public AnimationCurve torsoWidth = new AnimationCurve(
            new Keyframe(-1f, 0.85f), new Keyframe(-0.6f, 1.08f),  // bassin large
            new Keyframe(-0.1f, 0.7f),                              // taille fine
            new Keyframe(0.4f, 1.05f),                              // poitrail
            new Keyframe(0.85f, 0.85f), new Keyframe(1f, 0.55f));  // épaules
        [Tooltip("Avancée du torse selon la hauteur (même axe horizontal). > 0 : vers l'avant, en part de la largeur X.\n" +
                 "Poitrail bombé en haut, ventre rentré, fesses en arrière : dos cambré.")]
        public AnimationCurve torsoSpine = new AnimationCurve(
            new Keyframe(-1f, -0.05f), new Keyframe(-0.6f, -0.15f), // fesses en arrière
            new Keyframe(-0.1f, 0.02f),                              // ventre
            new Keyframe(0.4f, 0.2f),                                // poitrail en avant
            new Keyframe(1f, 0.05f));
        [Tooltip("> 0 : haut plus étroit que le bas (œuf). < 0 : l'inverse. 0 : symétrique.")]
        [Range(-0.5f, 0.5f)] public float bodyTaper = 0f;
        [Tooltip("1 : ovale. Plus petit : flancs droits et bouts arrondis (capsule).")]
        [Range(0.3f, 1f)] public float bodyFlank = 0.75f;
        [Tooltip("Courbure. > 0 : les deux bouts partent vers l'arrière (haricot). < 0 : vers l'avant.")]
        [Range(-0.3f, 0.3f)] public float bodyBend = 0f;
        [Tooltip("Finesse du maillage. 4 ≈ 2 500 sommets, 5 ≈ 10 000.")]
        [Range(2, 5)] public int bodyDetail = 2;

        [Header("Torse — placement dans le modèle")]
        [Tooltip("Décalage du corps (mètres) par rapport au centre du modèle.")]
        public Vector3 bodyOffset = new Vector3(0f, -0.03f, 0f);
        [Tooltip("Rotation du corps (degrés). Z : penche avant/arrière. Y : tourne. X : roule vers la caméra.")]
        public Vector3 bodyRotation = Vector3.zero;
        [Tooltip("Échelle du corps, appliquée après la forme (1 = taille réglée plus haut).")]
        public Vector3 bodyScale = Vector3.one;

        [Header("Torse — chair")]
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

        [Header("Torse — couleurs")]
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

        [Header("Fourrure (membre en bonne santé)")]
        // Abîmé, un membre perd sa fourrure par plaques et montre la chair cousue dessous (shader FleshLit)
        [Tooltip("Fourrure principale (bleu nuit).")]
        public Color furDark = new Color(0.07f, 0.08f, 0.19f);
        [Tooltip("Fourrure claire : poitrail, museau, joues, bout des pattes.")]
        public Color furLight = new Color(0.72f, 0.75f, 0.86f);
        [Tooltip("Couleur vive du bout de la queue.")]
        public Color furAccent = new Color(0.95f, 0.42f, 0.08f);
        [Tooltip("Taille des plaques de fourrure qui tombent (plus grand = plaques plus petites).")]
        [Range(1f, 12f)] public float furPatchScale = 4f;
        [Tooltip("Étendue du poitrail clair sur le devant du torse (0 : aucun).")]
        [Range(0f, 1f)] public float chestAmount = 0.55f;

        [Header("Torse — découpe en 3 parties (gameplay)")]
        // Chaque partie a sa vie et peut être arrachée seule (Limb : poitrine, ventre, bassin).
        // Hauteurs dans le repère du torse (mètres, 0 = centre) ; coupe le long des triangles.
        [Tooltip("Au-dessus : la poitrine (porte la tête et les bras).")]
        public float chestSplit = 0.18f;
        [Tooltip("En dessous : le bassin (porte les pattes et la queue). Entre les deux : le ventre, autour de l'âme.")]
        public float pelvisSplit = -0.2f;

        [Header("Sutures entre les membres (modèle Blender)")]
        // À chaque jonction de membres détachables : bourrelet de chair à vif + gros points de fil sombre,
        // cousus à la va-vite. Ajoutés au membre cousu (il les emporte quand il est arraché). Voir CatSutures.
        [Tooltip("Afficher les sutures.")]
        public bool sutures = true;
        [Tooltip("Écart moyen entre deux points de couture (mètres).")]
        [Range(0.015f, 0.15f)] public float stitchSpacing = 0.045f;
        [Tooltip("Longueur d'un point, en travers de la jonction (mètres).")]
        [Range(0.01f, 0.15f)] public float stitchLength = 0.06f;
        [Tooltip("Épaisseur du fil (mètres).")]
        [Range(0.002f, 0.02f)] public float stitchThread = 0.0055f;
        [Tooltip("Désordre : points de travers, mal espacés, de longueurs inégales, quelques trous.")]
        [Range(0f, 1f)] public float stitchMess = 0.65f;
        [Tooltip("Part des points doublés en croix.")]
        [Range(0f, 1f)] public float stitchCross = 0.3f;
        [Tooltip("Épaisseur du bourrelet de chair à vif le long de la jonction (mètres).")]
        [Range(0f, 0.05f)] public float sutureLip = 0.014f;
        [Tooltip("Fil des points (noir / brun foncé). Alpha 0 : ne vire pas à vif quand le membre est abîmé.")]
        public Color threadColor = new Color(0.09f, 0.055f, 0.04f, 0f);
        [Tooltip("Chair à vif du bourrelet (alpha 1 : saigne et vire à vif avec le membre).")]
        public Color sutureFleshColor = new Color(0.5f, 0.05f, 0.06f, 1f);

        [Header("Torse — collerette")]
        // Touffes de poils clairs autour du haut du torse, sous la tête, qui retombent vers l'avant et le bas
        [Tooltip("Longueur des touffes de la collerette (0 : pas de collerette).")]
        [Range(0f, 0.35f)] public float collarLength = 0.17f;
        [Tooltip("Nombre de touffes par rang (2 rangs).")]
        [Range(0, 20)] public int collarTufts = 12;
        [Tooltip("Hauteur du premier rang sur le torse : 0 = milieu, 1 = sommet.")]
        [Range(0f, 1f)] public float collarHeight = 0.66f;
        [Tooltip("Étendue autour du torse depuis le devant (degrés de chaque côté ; 180 = tout le tour).")]
        [Range(30f, 180f)] public float collarSpread = 120f;

        [Header("Torse — points d'attache (repère du torse)")]
        [Tooltip("Base du cou : la tête s'y accroche.")]
        public Vector3 neckPoint = new Vector3(0.08f, 0.45f, 0f);
        [Tooltip("Épaule côté caméra (z négatif). L'autre épaule est symétrique (z opposé).")]
        public Vector3 shoulderPoint = new Vector3(0.07f, 0.24f, -0.22f);
        [Tooltip("Naissance de la queue, en bas du dos.")]
        public Vector3 tailPoint = new Vector3(-0.22f, -0.32f, 0f);
        [Tooltip("Hanche côté caméra (z négatif), en bas et à l'arrière du torse : le haut de la cuisse y forme la fesse.\n" +
                 "L'autre hanche est symétrique.")]
        public Vector3 hipPoint = new Vector3(-0.06f, -0.4f, -0.15f);

        [Header("Pose debout au repos (modèle généré, ou fichier Blender sans sol « Ground »)")]
        // Avec un fichier Blender posé (objet Ground présent), la pose vient du fichier et ces valeurs sont ignorées
        [Tooltip("Centre du torse par rapport au centre du joueur (pieds : 0,5 plus bas).")]
        public Vector2 standingPosition = new Vector2(0f, 1.01f);
        [Tooltip("Inclinaison du torse vers l'avant (degrés).")]
        public float standingTilt = 22f;
        [Tooltip("Écart des hanches en profondeur, en plus du point de hanche.")]
        public float hipSpread = 0.08f;
        [Tooltip("Pattes écartées vers l'extérieur (degrés).")]
        public float legSplay = 10f;
        [Tooltip("Cheville au-dessus du sol (mètres).")]
        public float ankleHeight = 0.07f;
        [Tooltip("Cheville devant la hanche (mètres).")]
        public float footForward = 0f;
        [Tooltip("Tête relevée par rapport au torse (degrés).")]
        public float headAngle = 18.7f;
        [Tooltip("Bras ramenés vers l'avant par rapport au torse (degrés ; = inclinaison du torse : bras pendants).")]
        public float armAngle = 22f;
        [Tooltip("Coude plié (degrés).")]
        public float elbowAngle = 10f;
        [Tooltip("La queue monte à sa naissance (degrés).")]
        public float tailLift = 14f;
        [Tooltip("Puis se recourbe à chaque segment (degrés).")]
        public float tailCurl = 9f;

        [Header("Tête")]
        [Tooltip("Demi-tailles du crâne (X avant, Y haut, Z largeur).")]
        public Vector3 headRadii = new Vector3(0.24f, 0.22f, 0.22f);
        [Tooltip("Finesse du maillage du crâne (1 : très facetté, 3 : lisse).")]
        [Range(1, 4)] public int headDetail = 2;
        [Tooltip("Centre du crâne par rapport à la base du cou.")]
        public Vector3 headOffset = new Vector3(0.05f, 0.17f, 0f);
        [Tooltip("Rotation de toute la tête (degrés). Z : lève / baisse le museau.")]
        public Vector3 headRotation = Vector3.zero;
        [Tooltip("Avancée du museau.")]
        [Range(0f, 0.25f)] public float muzzleLength = 0.05f;
        [Tooltip("Longueur des oreilles.")]
        [Range(0f, 0.6f)] public float earLength = 0.27f;
        [Tooltip("Largeur de la base des oreilles.")]
        [Range(0.02f, 0.2f)] public float earWidth = 0.13f;
        [Tooltip("Intérieur clair de l'oreille, en part de sa taille (0 : aucun).")]
        [Range(0f, 0.9f)] public float earInner = 0.7f;
        [Tooltip("Touffes de poils clairs qui sortent de l'oreille (0 : aucune).")]
        [Range(0f, 0.15f)] public float earTuft = 0.06f;
        [Tooltip("Place des oreilles sur le crâne (X avant, Y haut, Z écart).")]
        public Vector3 earPosition = new Vector3(-0.03f, 0.14f, 0.1f);
        [Tooltip("Écartement des oreilles vers l'extérieur (degrés).")]
        [Range(-30f, 60f)] public float earSpread = 14f;
        [Tooltip("Inclinaison des oreilles vers l'arrière (degrés).")]
        [Range(-30f, 60f)] public float earBack = 18f;
        [Tooltip("Longueur des touffes de la collerette des joues (0 : aucune).")]
        [Range(0f, 0.25f)] public float cheekTuft = 0.13f;
        [Tooltip("Nombre de touffes de chaque côté de la tête.")]
        [Range(0, 14)] public int cheekTuftCount = 7;
        [Tooltip("Mèches sur le front, entre les oreilles (0 : aucune).")]
        [Range(0f, 0.15f)] public float foreheadTuft = 0.06f;
        [Tooltip("Taille des yeux (en amande).")]
        [Range(0.01f, 0.12f)] public float eyeSize = 0.06f;
        [Tooltip("Inclinaison des yeux (degrés) : > 0, le coin arrière remonte.")]
        [Range(-30f, 30f)] public float eyeTilt = 12f;
        [Tooltip("Place de l'œil côté caméra par rapport au centre du crâne (l'autre œil : z opposé).")]
        public Vector3 eyePosition = new Vector3(0.15f, 0.03f, -0.12f);
        [Tooltip("Couleur des yeux (lumineux).")]
        public Color eyeColor = new Color(1f, 0.78f, 0.12f);

        [Header("Bras")]
        [Tooltip("Longueur épaule → poignet.")]
        [Range(0.2f, 1.2f)] public float armLength = 0.68f;
        [Tooltip("Épaisseur du bras.")]
        [Range(0.02f, 0.15f)] public float armThickness = 0.048f;
        [Tooltip("Nombre de doigts de la main.")]
        [Range(0, 5)] public int fingerCount = 4;
        [Tooltip("Touffe de poils derrière le coude (0 : aucune).")]
        [Range(0f, 0.15f)] public float elbowTuft = 0.07f;
        [Tooltip("Manchette de poils au poignet (0 : aucune).")]
        [Range(0f, 0.12f)] public float wristTuft = 0.05f;
        [Tooltip("Longueur des griffes.")]
        [Range(0f, 0.15f)] public float clawLength = 0.05f;
        [Tooltip("Couleur des griffes.")]
        public Color clawColor = new Color(0.85f, 0.83f, 0.78f);

        [Header("Bras — silhouette")]
        // Courbes : horizontal 0 = début du segment (épaule ou coude), 1 = fin (coude ou poignet).
        // Épaisseur : multiplicateur de « armThickness ». Bosse : décalage du muscle, > 0 vers l'avant, < 0 vers l'arrière.
        [Tooltip("Épaisseur du bras, de l'épaule (0) au coude (1).")]
        public AnimationCurve upperArmWidth = new AnimationCurve(
            new Keyframe(0f, 1.3f), new Keyframe(0.3f, 1.15f), new Keyframe(1f, 0.8f));
        [Tooltip("Bosse du bras (biceps devant), de l'épaule (0) au coude (1).")]
        public AnimationCurve upperArmBulge = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.4f, 0.15f), new Keyframe(1f, 0f));
        [Tooltip("Épaisseur de l'avant-bras, du coude (0) au poignet (1).")]
        public AnimationCurve forearmWidth = new AnimationCurve(
            new Keyframe(0f, 0.85f), new Keyframe(0.3f, 0.95f), new Keyframe(1f, 0.62f));
        [Tooltip("Bosse de l'avant-bras (muscle derrière), du coude (0) au poignet (1).")]
        public AnimationCurve forearmBulge = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.3f, -0.12f), new Keyframe(1f, 0f));

        [Header("Queue")]
        [Tooltip("Longueur totale.")]
        [Range(0.2f, 2.5f)] public float tailLength = 1.1f;
        [Tooltip("Nombre de segments (plus = ondulation plus souple).")]
        [Range(3, 12)] public int tailSegments = 7;
        [Tooltip("Épaisseur maximale (rayon) de la queue touffue.")]
        [Range(0.03f, 0.35f)] public float tailRadius = 0.16f;
        [Tooltip("Épaisseur le long de la queue : horizontal 0 = naissance, 1 = bout ; vertical : part de « tailRadius ».")]
        public AnimationCurve tailWidth = new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.18f, 0.75f), new Keyframe(0.5f, 1f),
            new Keyframe(0.82f, 0.75f), new Keyframe(1f, 0.15f));
        [Tooltip("Touffes de poils autour de chaque segment (0 : queue lisse).")]
        [Range(0, 12)] public int tailTufts = 6;
        [Tooltip("Longueur des touffes, là où la queue est la plus épaisse.")]
        [Range(0f, 0.3f)] public float tailTuftLength = 0.13f;

        [Header("Pattes")]
        [Tooltip("Longueur de la patte (1 = première version, jugée trop petite). CatBody suit automatiquement.")]
        [Range(0.5f, 4f)] public float legLength = 2f;
        [Tooltip("Épaisseur de la patte.")]
        [Range(0.5f, 4f)] public float legThickness = 1.7f;
        [Tooltip("Couleur de la chair de la patte, sous la fourrure.")]
        public Color legColor = new Color(0.1f, 0.1f, 0.17f);
        [Tooltip("Cuisse vue de face : 1 = ronde (cylindre), plus petit = aplatie, large de profil et fine de face.")]
        [Range(0.35f, 1f)] public float thighDepth = 0.62f;
        [Tooltip("Haut de la cuisse arrondi en dôme sur cette part de la cuisse (0 : coupé net).")]
        [Range(0f, 0.5f)] public float thighDome = 0.28f;
        [Tooltip("Place du talon entre le genou (0) et la base des doigts (1). Plus bas = métatarse plus long " +
                 "(le segment talon → doigts, levé du sol) et tibia plus court.")]
        [Range(0.15f, 0.7f)] public float heelHeight = 0.36f;
        [Tooltip("Recul du talon vers l'arrière (en part de la longueur de patte) : plus grand = patte plus coudée.")]
        [Range(0f, 0.15f)] public float heelBack = 0.065f;
        [Tooltip("Demi-tailles des doigts posés au sol (phalanges) : X longueur vers l'avant, Y hauteur, Z largeur, en mètres.")]
        public Vector3 pawSize = new Vector3(0.085f, 0.05f, 0.08f);
        [Tooltip("Nombre de doigts au bout du pied.")]
        [Range(0, 5)] public int toeCount = 4;
        [Tooltip("Rayon des doigts.")]
        [Range(0f, 0.08f)] public float toeSize = 0.038f;
        [Tooltip("Bosse du genou (rotule), en part de l'épaisseur de la cuisse au genou.")]
        [Range(0f, 1.5f)] public float kneeBump = 0.9f;
        [Tooltip("Bosse du talon (jarret), en part de l'épaisseur du jarret.")]
        [Range(0f, 1.5f)] public float heelBump = 1f;
        [Tooltip("Hauteur où commence la « chaussette » claire, en part de la patte (0 = genou, 1 = cheville).")]
        [Range(0f, 1f)] public float sockStart = 0.45f;

        [Header("Pattes — silhouette")]
        // Courbes : horizontal 0 = début du segment (hanche ou genou), 1 = fin (genou ou cheville).
        // Épaisseur : multiplicateur (1 = épaisseur de base × legThickness). Bosse : > 0 vers l'avant, < 0 vers l'arrière.
        [Tooltip("Épaisseur de la cuisse, du haut de la fesse (0, dans le torse) au genou (1).")]
        public AnimationCurve thighWidth = new AnimationCurve(
            new Keyframe(0f, 1.55f), new Keyframe(0.25f, 1.5f), new Keyframe(0.55f, 1.05f), new Keyframe(0.85f, 0.78f), new Keyframe(1f, 0.7f));
        [Tooltip("Bosse de la cuisse : < 0 en haut = fesse en arrière, > 0 plus bas = muscle devant. Du haut (0) au genou (1).")]
        public AnimationCurve thighBulge = new AnimationCurve(
            new Keyframe(0f, -0.25f), new Keyframe(0.25f, 0f), new Keyframe(0.5f, 0.15f), new Keyframe(1f, 0f));
        [Tooltip("Épaisseur du bas de patte, du genou (0) à la base des doigts (1). Creux à « heelHeight » : le talon.")]
        public AnimationCurve shinWidth = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.16f, 1f), new Keyframe(0.36f, 0.72f), new Keyframe(0.8f, 0.66f), new Keyframe(1f, 0.85f));
        [Tooltip("Bosse du jarret (mollet derrière), du genou (0) à la cheville (1).")]
        public AnimationCurve shinBulge = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.14f, -0.22f), new Keyframe(0.36f, 0f), new Keyframe(1f, 0f));

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
