using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Visuel du corps du chat, assemblé membre par membre (modèles : Prefabs/Player, construits par
    // Parry Everything > Reconstruire les modèles du chat). L'état se lit dessus (pas de barre de vie).
    //  - Boule : la chair (le torse) recouvre entièrement l'âme et roule.
    //  - Debout : le torse, porté en hauteur par 2 grandes pattes de chat articulées.
    //    Marche réglée sur un cycle : chaque pied recule exactement à la vitesse du sol (il ne patine pas),
    //    puis revient en arc ; les 2 pattes sont en opposition. Le genou se plie par cinématique inverse.
    //    Le corps monte et redescend avec le cycle, bascule vers la patte d'appui, penche dans le mouvement ;
    //    il s'écrase en retombant d'un saut. Démarrage et arrêt fondus.
    //  - Tête, bras, queue : accrochés au torse (points du CatTorsoRig), ils le suivent.
    //    La tête compense l'inclinaison du torse ; les bras balancent à l'opposé des pattes et se lèvent
    //    pour parer ; la queue ondule au repos, file et claque dans le vent du mouvement, fouette aux arrêts.
    // État de chaque membre (shader FleshLit) :
    //  - en bonne santé, il est couvert de fourrure ; abîmé, elle part par plaques et la chair cousue apparaît,
    //    puis vire à vif (_Fur, _Damage) ; presque arraché, il tremble ;
    //  - hémorragie : la chair rougit, pulse et goutte ; infection : veines violacées ; hypertrophie : il gonfle.
    // Une parade réussie fait flasher tout le corps (_Flash).
    public class CatBody : MonoBehaviour
    {
        [Header("Modèles")]
        [SerializeField] private GameObject fleshBallPrefab;
        [SerializeField] private GameObject legPrefab;
        [SerializeField] private GameObject armPrefab;
        [SerializeField] private GameObject headPrefab;
        [SerializeField] private GameObject tailPrefab;

        [Header("Placement")]
        // Pose debout au repos (hauteur et inclinaison du torse, écart des pattes, angles de la tête, des bras, de la queue) :
        // celle du fichier Blender, lue sur les prefabs (CatTorsoRig, CatLegRig, rotations des pièces). Les mouvements s'y ajoutent.
        [SerializeField] private float ballHeight = -0.08f;       // centre de la boule au sol, par rapport au joueur
        [SerializeField] private float hipHeight = 0.3f;          // attache des pattes sans torse (sinon : hanche du CatTorsoRig)
        [SerializeField] private float morphSpeed = 3f;           // boule ↔ debout (par seconde)
        [SerializeField] private float turnSpeed = 900f;          // demi-tour du corps (degrés par seconde)

        [Header("Pattes")]
        // Longueurs cuisse / jarret, écart, place du pied au repos : lues sur le prefab CatLeg (CatLegRig)
        [SerializeField] private float toeOff = 30f;              // le pied bascule pointe en bas en se levant (degrés)
        [SerializeField] private float airToeDown = 35f;          // en l'air, pointes des pieds vers le bas (degrés)

        [Header("Marche")]
        // Grands pas lents : plus le pas est long, moins il y en a par seconde à vitesse égale
        [SerializeField] private float strideMin = 1.1f;          // longueur d'un pas à vitesse lente
        [SerializeField] private float stridePerSpeed = 0.12f;    // le pas s'allonge avec la vitesse...
        [SerializeField] private float strideMax = 1.9f;          // ...dans la limite de la portée des pattes
        [SerializeField] private float maxReach = 0.97f;          // le pied ne s'écarte pas plus de la hanche
        [Range(0.5f, 0.8f)] [SerializeField] private float dutyFactor = 0.5f;   // marche : part du cycle pied au sol
        [SerializeField] private float stepHeight = 0.25f;        // hauteur de l'arc du pied
        [SerializeField] private float walkFade = 5f;             // démarrage / arrêt de la démarche (par seconde)
        // Accroupi en marchant : la hanche descend, la patte porte plus loin, le pas s'allonge
        [SerializeField] private float walkCrouch = 0.15f;
        [SerializeField] private float runCrouch = 0.3f;

        [Header("Course")]
        // En courant, les pieds restent moins au sol (phase de vol) : cycle plus lent, corps qui bondit
        [Range(0.3f, 0.8f)] [SerializeField] private float runDutyFactor = 0.3f;
        [SerializeField] private float runBlendFrom = 3.8f;       // vitesse où la course commence à remplacer la marche...
        [SerializeField] private float runBlendTo = 5.5f;         // ...et où elle l'a remplacée
        [SerializeField] private float runStepHeight = 0.34f;
        [SerializeField] private float runRise = 0.16f;           // bond du corps à chaque foulée
        // Le torse penche vers l'avant avec la vitesse (sensation de vitesse), plus encore en accélérant,
        // et se redresse / bascule en arrière au freinage. Tête et bras compensent (headLevel, armHang)
        [SerializeField] private float walkLean = 4f;             // penché en marchant (degrés)
        [SerializeField] private float runLean = 16f;             // penché en courant (degrés)
        [SerializeField] private float overspeedLean = 1f;        // en plus par m/s au-delà de runBlendTo (degrés)
        [SerializeField] private float accelLean = 1.2f;          // en plus par m/s² d'accélération (négatif au freinage)
        [SerializeField] private float maxLean = 26f;             // limite vers l'avant...
        [SerializeField] private float maxBrakeLean = 10f;        // ...et vers l'arrière (freinage)
        [SerializeField] private float leanResponse = 6f;         // vitesse à laquelle le torse suit (par seconde)

        [Header("Une patte : il sautille")]
        // Un bond par cycle : appui court (s'accroupit puis pousse), puis vol en arc, patte repliée qui revient devant
        [Range(0.2f, 0.6f)] [SerializeField] private float hopDuty = 0.35f;   // part du cycle pied au sol
        [SerializeField] private float hopRateSlow = 1.9f;        // bonds par seconde, lentement...
        [SerializeField] private float hopRateFast = 3.2f;        // ...et vite (à hopFastSpeed)
        [SerializeField] private float hopFastSpeed = 6f;
        [SerializeField] private float hopHeight = 2.5f;          // hauteur du bond × hauteur physique du vol (g·t²/8)
        [SerializeField] private float hopSquat = 0.12f;          // s'accroupit pendant l'appui (mètres)
        [SerializeField] private float hopTilt = 12f;             // penche en avant en poussant (degrés)
        [SerializeField] private float hopImpact = 0.6f;          // écrasement à chaque réception
        [SerializeField] private float hopTuck = 0.12f;           // patte repliée en l'air (mètres)

        [Header("Sans pattes, avec la tête : il rampe")]
        // Couché sur le ventre, tête relevée. Avec bras : chaque bras s'allonge devant, s'accroche, tire (en alternance).
        // Sans bras : ondule comme un ver. Sans tête : il roule (boule).
        [SerializeField] private float crawlStride = 0.45f;       // distance gagnée à chaque traction d'un bras (mètres)
        [Range(0.2f, 0.6f)] [SerializeField] private float crawlReach = 0.5f;   // part du cycle où le bras va devant (en l'air)
        // La main est posée au sol (bras plié, coude vers le haut) : plantée pendant la traction, elle recule à la vitesse du sol
        // Le bras va loin devant (main posée), puis ramène la main vers lui pour se tirer : l'avant du corps monte
        // pendant qu'il ramène le bras, redescend quand il le tend devant
        [SerializeField] private float crawlHandFront = 0.75f;    // main posée devant l'épaule, bras tendu (mètres)
        [SerializeField] private float crawlHandLift = 0.1f;      // main levée en repartant devant (mètres)
        [SerializeField] private float crawlRestHand = 0.45f;     // à l'arrêt : mains posées devant (mètres devant l'épaule)
        [SerializeField] private float crawlChestLift = 0.18f;    // l'avant du corps monte en ramenant le bras (mètres)
        [SerializeField] private float crawlWave = 5f;            // ondulation du corps avec bras (degrés)
        [SerializeField] private float wormWave = 14f;            // sans bras : ondule comme un ver (degrés)
        [SerializeField] private float wormLift = 0.05f;          // sans bras : le corps se soulève à chaque ondulation (mètres)

        [Header("Corps qui suit les pas")]
        [SerializeField] private float stepRise = 0.11f;          // le corps monte au milieu de chaque pas
        [SerializeField] private float landImpact = 1.2f;         // écrasement en retombant d'un saut
        [SerializeField] private float bodySpringStiffness = 120f;
        [SerializeField] private float bodySpringDamping = 12f;
        [SerializeField] private float squash = 1.2f;            // déformation de la boule à l'impact
        // Au sol, le torse ne tourne pas : il monte et descend seulement
        [SerializeField] private float shoulderSwing = 0.03f;     // l'épaule avance avec son bras (torsion des épaules)
        [SerializeField] private float breathing = 0.012f;        // à l'arrêt, le corps respire

        [Header("Tête")]
        [Range(0f, 1f)] [SerializeField] private float headLevel = 0.85f;   // part de l'inclinaison du torse compensée (1 : tête droite)
        [SerializeField] private float headNod = 35f;             // la tête hoche à chaque impact (degrés par unité d'écrasement)
        [SerializeField] private float headBob = 3f;              // hochement léger au rythme des pas (degrés)

        [Header("Bras")]
        [SerializeField] private float armHang = 1f;              // part du basculement du torse compensée (bras pendants)
        // Marche : balancier calé sur la patte opposée (bras devant quand elle est devant, derrière quand elle est derrière)
        [SerializeField] private float walkSwing = 16f;           // amplitude (degrés vers l'avant)
        [Range(0f, 1f)] [SerializeField] private float armBackRatio = 0.65f;   // le bras recule moins qu'il n'avance
        // Coude (angle absolu, bras tendu = 0) : se ferme un peu quand le bras avance
        [SerializeField] private float walkElbow = 12f;
        [SerializeField] private float elbowPump = 4f;
        // Course : bras ballants, sans force, poussés en arrière par le vent de la course (pendule amorti).
        // Angle d'équilibre : tan = (armDrag × vitesse² + accélération) / gravité ; les rebonds du corps les font flotter
        [SerializeField] private float armDrag = 0.3f;            // poussée du vent (plus grand : bras plus vite à l'horizontale)
        [SerializeField] private float maxArmTrail = 80f;         // bras jamais plus en arrière (degrés depuis la verticale)
        [SerializeField] private float armStiffness = 40f;        // rappel du pendule (plus petit : bras plus mous, plus lents)
        [SerializeField] private float armDamping = 5f;           // amortissement (plus petit : bras qui ballottent plus longtemps)
        [SerializeField] private float armBounce = 1f;            // effet des rebonds du corps sur les bras
        [SerializeField] private float armFlutter = 4f;           // flottement dans le vent (degrés)
        [SerializeField] private float limpElbow = 15f;           // coude relâché, légèrement plié (degrés)
        [SerializeField] private float elbowFlop = 0.12f;         // l'avant-bras traîne quand le bras part en arrière (degrés par degré/s)
        [SerializeField] private float parryArmAngle = 95f;       // parade : bras levés devant (degrés)
        [SerializeField] private float airArmAngle = 55f;         // en l'air : bras levés pour l'équilibre
        [SerializeField] private float armSharpness = 14f;        // vitesse de passage d'une pose à l'autre (air, parade, sol)

        [Header("Queue")]
        // Queue continue (chaîne d'os, CatTailBuilder) : sa forme est une courbure le long de sa longueur, intégrée os par os.
        // Au repos : elle enchaîne des gestes de chat (point d'interrogation, balayage lent, pause attentive, crochet qui
        // bascule), fondus lentement l'un dans l'autre ; respiration, balancement en profondeur.
        // Tout s'efface avec la vitesse.
        // En mouvement : le vent (opposé à la vitesse, chute et saut compris) la couche dans son sens, elle claque à pleine
        // vitesse. Chaque os suit sa cible avec un ressort plus mou vers le bout : les gestes partent de la base et
        // arrivent au bout en retard, la queue fouette aux arrêts.
        [SerializeField] private float tailIdle = 1f;             // force des gestes au repos (0 : queue immobile)
        [SerializeField] private float tailIdleTempo = 1f;        // rythme des gestes (plus grand : plus vifs)
        [SerializeField] private float tailGestureMin = 4f;       // durée d'un geste (s, tirée entre min et max)
        [SerializeField] private float tailGestureMax = 8f;
        [SerializeField] private float tailGestureBlend = 1.8f;   // fondu d'un geste au suivant (s)
        [SerializeField] private float tailBreath = 3f;           // la queue monte et descend avec la respiration (degrés)
        [SerializeField] private float tailDepth = 7f;            // balancement vers / loin de la caméra (degrés, au bout)
        [SerializeField] private float tailWaveSpeed = 2.6f;      // rythme de base du claquement (s'y ajoute tailFlutterSpeed)
        [SerializeField] private float tailWind = 0.12f;          // prise au vent (part couchée = k·v² / (k·v² + 1))
        [Range(0f, 1f)] [SerializeField] private float tailMaxStream = 0.95f;   // au plus, part de l'écart rattrapée vers le vent
        [SerializeField] private float tailMaxBend = 110f;        // décalage maximal d'un os par rapport au repos (degrés)
        [SerializeField] private float tailFullSpeed = 6f;        // vitesse de plein vent (m/s) : claquement et rafales au maximum
        [SerializeField] private float tailFlutter = 16f;         // claquement à pleine vitesse (degrés, au bout)
        [SerializeField] private float tailFlutterSpeed = 16f;    // rythme ajouté au claquement à pleine vitesse
        [SerializeField] private float tailGust = 10f;            // rafales irrégulières à pleine vitesse (degrés, au bout)
        [SerializeField] private float tailStiffness = 70f;       // rappel du ressort à la base (plus petit : queue plus molle)
        [SerializeField] private float tailDamping = 9f;          // amortissement (plus petit : fouette plus longtemps)
        [Range(0.05f, 1f)] [SerializeField] private float tailTipSoftness = 0.3f;   // raideur du bout par rapport à la base

        [Header("État des membres")]
        [Range(0f, 1f)] [SerializeField] private float furFullAbove = 0.85f;   // vie au-dessus : fourrure intacte
        [Range(0f, 1f)] [SerializeField] private float furGoneBelow = 0.15f;   // vie en dessous : plus de fourrure
        [SerializeField] private float hypertrophyScale = 1.25f;  // hypertrophie : le membre gonfle
        [SerializeField] private float bleedDripInterval = 0.3f;  // hémorragie : une goutte toutes les ... secondes
        [Range(0f, 1f)] [SerializeField] private float infectionMin = 0.35f;   // infection : étendue des veines, vie pleine

        [Header("Ressenti")]
        [Range(0f, 1f)] [SerializeField] private float shakeBelow = 0.35f;   // tremble sous cette part de vie
        [SerializeField] private float parryFlashTime = 0.1f;

        [Header("Lumière du corps")]
        // L'âme éclairait le chat ; recouverte de chair, c'est cette lumière douce (côté caméra) qui le rend lisible
        [SerializeField] private Color fillColor = new Color(0.75f, 0.68f, 0.85f);
        [SerializeField] private float fillIntensity = 1.6f;
        [SerializeField] private float fillRange = 3f;
        [SerializeField] private Vector3 fillOffset = new Vector3(0.3f, 0.5f, -1.1f);

        private sealed class LimbView
        {
            // Partie d'un autre modèle (poitrine, bassin : dans le torse ; jarret, pied : dans la patte) :
            // pas de placement propre, seulement ses rendus (aspect, visible ou non)
            public bool isPart;
            public Transform root;
            public Transform ball;          // chair (torse)
            public Vector2[] outline;       // torse : contour de profil (enveloppe convexe, repère de la boule) pour rouler
            public Transform knee;          // patte : articulation
            public Transform ankle;         // patte : cheville (le pied reste à plat)
            public Transform hipAnchor;     // patte : haut de la cuisse (fesse), ne tourne pas avec la cuisse
            public Transform elbow;         // bras : articulation
            public Transform[] tailJoints;  // queue : un pivot par segment
            public Renderer[] renderers;
            public float phaseOffset;
            public float armPose;           // bras : 0 balancier au sol, 1 pose tenue (air, parade)
            public float foreLength;        // bras : coude → bout de la main (mètres), pour poser la main au sol
            public float trail, trailVel;   // bras ballant : angle vers l'arrière depuis la verticale (degrés) et sa vitesse
            public float shoulderY, shoulderVelY, shoulderAccelY;   // mouvement vertical de l'épaule (rebonds du corps)
            public bool hasShoulder;
            public int side;                // bras : −1 côté caméra, +1 côté loin
            public float swell = 1f;        // hypertrophie : facteur de taille ce frame
            public float dripTimer;         // hémorragie : prochaine goutte
            // Marche (pattes)
            public Vector3 foot;            // position actuelle de la cheville (monde)
            public bool hasFoot;
            public float lift;              // 0 au sol, 1 au sommet de l'arc du pied
            // Bras
            public float armAngle, elbowAngle;
            // Pose de repos (prefab = fichier Blender) : les mouvements s'y ajoutent
            public Quaternion restRoot = Quaternion.identity;     // tête, bras (côté loin : en miroir)
            public Quaternion restElbow = Quaternion.identity;
            public Quaternion[] restTail;
            // Queue : par segment, direction de repos (degrés, repère du torse), décalage du ressort et sa vitesse
            public float[] tailRestAngle, tailBend, tailBendVel;
            public float tailPhase;         // phase du claquement (son rythme change avec la vitesse)
            public int tailGesture, tailNextGesture;   // geste en cours et suivant (TailGestures)
            public float tailGestureTimer;  // temps restant avant de passer au geste suivant (s)
            public float tailGestureBlend = 1f;   // 0 → 1 : fondu du geste en cours vers le suivant
            public float tailIdleClock;     // horloge des gestes (avance au rythme tailIdleTempo)
        }

        // Pieds du joueur sous son centre (bas du collider) : le sol du fichier Blender est à cette hauteur
        public const float FeetBelowCenter = 0.5f;

        private static readonly int DamageId = Shader.PropertyToID("_Damage");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int FurId = Shader.PropertyToID("_Fur");
        private static readonly int BleedId = Shader.PropertyToID("_Bleed");
        private static readonly int InfectionId = Shader.PropertyToID("_Infection");

        private static Mesh legMesh;
        private static Mesh lumpMesh;

        private PlayerBody body;
        private PlayerController controller;
        private CatSoul soul;
        private Rigidbody rb;
        private CatLegRig legRig;
        private CatTorsoRig torsoRig;
        private LimbView torsoView;         // vue de la chair : les autres membres s'y accrochent
        private float torsoTilt;            // inclinaison du torse ce frame (degrés, repère « regarde à droite »)
        private readonly Dictionary<Limb, LimbView> views = new Dictionary<Limb, LimbView>();
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private MaterialPropertyBlock block;
        private float roll;                 // rotation de la boule qui roule (degrés)
        private float bodyDrop, bodyDropVel; // ressort vertical du corps (impact des pas)
        private float bodyY;                // décalage vertical du corps ce frame (pas + respiration + impact)
        private float bodyPitch;            // bascule de la boule ce frame (degrés)
        private float lean;                 // penché vers l'avant dû à la vitesse (degrés, positif = vers l'avant)
        private float lastForwardSpeed;     // vitesse vers l'avant au frame précédent (accélération)
        private float hopWeight;            // 0 : deux pattes, 1 : une seule (sautille)
        private float crawlWeight;          // 0 : roule (ou debout), 1 : rampe (sans pattes, avec la tête)
        private float crawlPhase;           // cycle de reptation (un bras tire par demi-cycle)
        private float crawlLift;            // soulèvement du corps en rampant (mètres)
        private float crawlPitch;           // l'avant du corps se relève en tirant (degrés)
        private float hopLean;              // penché dû à la poussée du bond (degrés)
        private float forwardAccel;         // accélération vers l'avant, lissée (la physique ne change la vitesse qu'à ses pas)
        private float gaitPhase;            // cycle de marche (0 → 1, tourne avec la distance parcourue)
        private float walkWeight;           // 0 : posture de repos, 1 : démarche complète
        private float runWeight;            // 0 : marche, 1 : course
        private float turnYaw;              // pivot de la boule : 0 regarde à droite, 180 à gauche
        private bool wasGrounded = true;
        private float standUp;              // 0 : boule, 1 : debout
        private float flash;
        private Light fill;

        // Meshes simples partagés (membres au sol, tas de chair, âme marchande)
        public static Mesh LegMesh
        {
            get
            {
                if (legMesh == null)
                {
                    legMesh = LowPoly.Prism(5, 1f, 0.7f, 1f, 0.3f);
                    legMesh.name = "CatLeg";
                }
                return legMesh;
            }
        }

        public static Mesh LumpMesh
        {
            get
            {
                if (lumpMesh == null)
                {
                    lumpMesh = LowPoly.Icosahedron(0.22f, 11);
                    lumpMesh.name = "FleshLump";
                }
                return lumpMesh;
            }
        }

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            controller = GetComponent<PlayerController>();
            soul = GetComponent<CatSoul>();
            rb = GetComponent<Rigidbody>();
            block = new MaterialPropertyBlock();
            if (fleshBallPrefab == null || legPrefab == null)
            {
                Debug.LogWarning("CatBody : modèles non assignés (Prefabs/Player) — le corps ne sera pas visible.", this);
            }
        }

        private void OnEnable()
        {
            PlayerController.OnParry += HandleParry;
        }

        private void OnDisable()
        {
            PlayerController.OnParry -= HandleParry;
            generated.DestroyAll();
            fill = null;
            views.Clear();
        }

        private void HandleParry()
        {
            flash = 1f;
        }

        // Labo d'animation : vitesse (m/s, vers l'avant) utilisée pour animer à la place de la vraie vitesse,
        // comme sur un tapis roulant (le chat ne se déplace pas). null : vraie vitesse.
        public float? TreadmillSpeed { get; set; }

        private void LateUpdate()
        {
            if (body == null) return;
            if (soul == null) soul = GetComponent<CatSoul>();
            SyncViews();

            PlayerForm form = body.Form;
            float dt = Time.deltaTime;
            float facing = controller != null && controller.Facing < 0f ? -1f : 1f;
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            if (TreadmillSpeed.HasValue) vel.x = TreadmillSpeed.Value * facing;   // labo : allure imposée, le chat reste sur place
            bool grounded = controller == null || controller.IsGrounded;
            float moving = Mathf.Clamp01(Mathf.Abs(vel.x) / 4f);

            // La chair recouvre entièrement l'âme dès qu'elle existe
            if (soul != null) soul.SetHidden(form != PlayerForm.Soul);
            UpdateFillLight(form != PlayerForm.Soul);

            standUp = Mathf.MoveTowards(standUp, form == PlayerForm.Biped ? 1f : 0f, dt * morphSpeed);
            bool crawling = form == PlayerForm.Ball && HasPart(LimbKind.Head);
            crawlWeight = Mathf.MoveTowards(crawlWeight, crawling ? 1f : 0f, dt * morphSpeed);
            if (crawling) UpdateCrawl(dt, facing, vel, grounded);
            // Boule : roule sans glisser sur son vrai contour (rayon = distance du centre au point qui touche le sol)
            else if (form == PlayerForm.Ball) roll -= vel.x * dt / Mathf.Max(0.1f, RollRadius()) * Mathf.Rad2Deg;
            else roll = Mathf.LerpAngle(roll, 0f, 1f - Mathf.Exp(-8f * dt));
            flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, parryFlashTime));
            UpdateBodyMotion(dt, facing, vel, grounded, moving);
            float hop = 0f;

            // Le torse d'abord : les autres membres sont placés dans son repère
            foreach (KeyValuePair<Limb, LimbView> pair in views)
            {
                Limb limb = pair.Key;
                LimbView view = pair.Value;
                bool show = !limb.IsTorn;
                if (view.isPart)
                {
                    // Partie d'un autre modèle : seulement ses rendus (placée avec son modèle)
                    foreach (Renderer r in view.renderers)
                    {
                        if (r != null && r.enabled != show) r.enabled = show;
                    }
                    if (show) ApplyLook(limb, view, dt);
                    continue;
                }
                if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
                if (!show) continue;

                float ratio = limb.LifeRatio;
                float shake = ratio < shakeBelow
                    ? (Mathf.PerlinNoise(Time.time * 25f, view.phaseOffset * 10f) - 0.5f) * (1f - ratio / shakeBelow)
                    : 0f;
                ApplyLook(limb, view, dt);

                switch (limb.Kind)
                {
                    case LimbKind.Flesh: PlaceFlesh(view, facing, hop, shake); break;
                    case LimbKind.Leg: PlaceLeg(limb, view, facing, grounded, vel, shake, dt); break;
                    case LimbKind.Head: PlaceHead(view, shake); break;
                    case LimbKind.Arm: PlaceArm(view, grounded, shake, dt); break;
                    case LimbKind.Tail: PlaceTail(view, shake, vel, dt); break;
                }
            }
        }

        private void UpdateFillLight(bool on)
        {
            if (fill == null)
            {
                if (!on) return;
                GameObject go = generated.Track(new GameObject("BodyFillLight"));
                fill = go.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.shadows = LightShadows.None;
            }
            fill.enabled = on;
            fill.color = fillColor;
            fill.range = fillRange;
            fill.intensity = fillIntensity * (1f + flash);
            fill.transform.position = transform.position + fillOffset;
        }

        // Apparence selon l'état du membre : fourrure → chair cousue → à vif, effets, flash de parade
        private void ApplyLook(Limb limb, LimbView view, float dt)
        {
            float ratio = limb.LifeRatio;
            float fur = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(furGoneBelow, furFullAbove, ratio));
            float damage = Mathf.SmoothStep(0f, 1f, 1f - ratio);
            LimbEffect effect = limb.Effect;
            float bleed = effect == LimbEffect.Bleeding ? 1f : 0f;
            float infection = effect == LimbEffect.Infection ? Mathf.Lerp(infectionMin, 1f, 1f - ratio) : 0f;

            // Hypertrophie : gonfle, avec une pulsation lente
            float swellGoal = effect == LimbEffect.Hypertrophy ? hypertrophyScale + Mathf.Sin(Time.time * 2.5f) * 0.03f : 1f;
            view.swell = Mathf.Lerp(view.swell, swellGoal, 1f - Mathf.Exp(-4f * dt));

            foreach (Renderer r in view.renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(FurId, fur);
                block.SetFloat(DamageId, damage);
                block.SetFloat(BleedId, bleed);
                block.SetFloat(InfectionId, infection);
                block.SetFloat(FlashId, flash);
                r.SetPropertyBlock(block);
            }

            // Hémorragie : gouttes de sang qui tombent du membre
            if (bleed > 0f && dt > 0f)
            {
                view.dripTimer -= dt;
                if (view.dripTimer <= 0f)
                {
                    view.dripTimer = bleedDripInterval * Random.Range(0.6f, 1.4f);
                    Vector3 at = view.renderers.Length > 0 && view.renderers[0] != null
                        ? view.renderers[0].bounds.center
                        : view.root.position;
                    ParticleBurst.Spawn("BloodDrip", at, new Color(0.45f, 0.02f, 0.03f), 2,
                        lifetime: new Vector2(0.4f, 0.8f), speed: new Vector2(0.1f, 0.5f), size: new Vector2(0.03f, 0.06f),
                        gravity: 1.5f, radius: 0.1f);
                }
            }
        }

        private void PlaceFlesh(LimbView view, float facing, float hop, float shake)
        {
            view.root.position = transform.position + new Vector3(shake * 0.05f, hop, 0f);

            // Au sol elle roule ; debout, les pattes la soulèvent et elle suit chaque pas
            float s = Mathf.SmoothStep(0f, 1f, standUp);
            Vector2 standing = StandingPosition;
            // Au sol, le point le plus bas du contour touche le sol (forme allongée : le centre monte et descend en roulant)
            float grounded = view.outline != null ? -FeetBelowCenter + RollRadius() + crawlLift * crawlWeight : ballHeight;
            float y = Mathf.Lerp(grounded, standing.y, s) + bodyY * s;
            view.ball.localPosition = new Vector3(standing.x * s * facing, y, 0f);
            // Demi-tour : la boule pivote de 180° sur l'axe vertical, l'avant passe côté caméra
            turnYaw = Mathf.MoveTowards(turnYaw, facing > 0f ? 0f : 180f, turnSpeed * Time.deltaTime);
            // Debout, la boule penche vers l'avant (angle fixe au sol, bascule seulement en l'air).
            // Inclinaison exprimée dans le repère du chat (« regarde à droite ») : le pivot la retourne avec lui
            float tilt = (-StandingTilt - lean - hopLean + bodyPitch * facing) * s;
            // Couché (rampe) : l'inclinaison vue par la tête et les bras est celle du corps à plat
            torsoTilt = Mathf.Lerp(tilt, Mathf.DeltaAngle(0f, roll) * facing, crawlWeight * (1f - s));
            view.ball.localRotation = Quaternion.Euler(0f, 0f, roll)
                                      * Quaternion.Euler(0f, turnYaw, 0f)
                                      * Quaternion.Euler(0f, 0f, tilt + shake * 10f);
            // Écrasée quand un pied se pose, étirée quand le corps remonte
            float k = Mathf.Clamp(bodyDrop * squash * s, -0.25f, 0.25f);
            view.ball.localScale = new Vector3(1f - k * 0.6f, 1f + k, 1f - k * 0.6f) * view.swell;
        }

        // ---------- Tête, bras, queue (dans le repère du torse, chat qui regarde à droite) ----------

        // Tête : compense l'inclinaison du torse, hoche aux impacts et au rythme des pas
        private void PlaceHead(LimbView view, float shake)
        {
            CatTorsoRig rig = TorsoRig;
            if (rig != null) view.root.localPosition = rig.Neck;
            float nod = bodyDrop * headNod + Mathf.Sin(gaitPhase * Mathf.PI * 4f) * headBob * walkWeight;
            view.root.localRotation = Quaternion.Euler(0f, 0f, -TiltOffRest * headLevel + nod + shake * 15f) * view.restRoot;
            view.root.localScale = Vector3.one * view.swell;
        }

        // Bras : pendants, balancent à l'opposé des pattes ; levés devant pour parer, levés en l'air
        private void PlaceArm(LimbView view, bool grounded, float shake, float dt)
        {
            CatTorsoRig rig = TorsoRig;
            if (rig != null)
            {
                // Torsion des épaules : l'épaule avance avec son bras, l'autre recule
                Vector3 p = rig.Shoulder;
                float twist = shoulderSwing * walkWeight * Mathf.Sin((gaitPhase + view.phaseOffset) * Mathf.PI * 2f);
                view.root.localPosition = new Vector3(p.x + twist, p.y, Mathf.Abs(p.z) * view.side);
            }

            // Angles ajoutés à la pose de repos (+ = vers l'avant / coude qui se plie)
            float rest = SignedZ(view.restRoot), restElbow = SignedZ(view.restElbow);
            bool parrying = controller != null && controller.IsParrying();

            // Marche : balancier calculé directement sur le cycle des pattes (aucun retard).
            // forward : +1 quand la patte opposée est tout devant, −1 tout derrière
            // (bras côté caméra : phase de la patte loin, et inversement : même décalage de phase)
            float forward = GaitForward(gaitPhase + view.phaseOffset);
            float swing = walkSwing * walkWeight * (forward >= 0f ? forward : forward * armBackRatio);
            float walkArm = -TiltOffRest * armHang + swing;
            float walkBend = Mathf.Max(0f, (walkElbow + elbowPump * 0.3f * forward) * walkWeight) - restElbow * walkWeight;

            // Course : bras ballants poussés par le vent (angle vers l'arrière depuis la verticale, repère du monde)
            UpdateLimpArm(view, dt);
            float flutter = armFlutter * Mathf.Clamp01(Mathf.Abs(lastForwardSpeed) / 6f)
                            * (Mathf.PerlinNoise(Time.time * 2.5f, view.phaseOffset * 7f + 3f) - 0.5f) * 2f;
            float limpArm = -(view.trail + flutter) - TiltOffRest;
            float limpBend = Mathf.Max(0f, limpElbow + elbowFlop * view.trailVel) - restElbow;

            float run = Mathf.Max(runWeight, hopWeight) * walkWeight;   // sautille : bras ballants aussi, secoués par les bonds
            float groundArm = Mathf.Lerp(walkArm, limpArm, run);
            float groundElbow = Mathf.Lerp(walkBend, limpBend, run);

            // Rampe : le bras va devant puis tire (angles depuis la verticale du monde, + = devant)
            if (crawlWeight > 0f)
            {
                CrawlArm(view, out float crawlArm, out float crawlBend);
                groundArm = Mathf.Lerp(groundArm, crawlArm - TiltOffRest, crawlWeight);
                groundElbow = Mathf.Lerp(groundElbow, crawlBend - restElbow, crawlWeight);
            }

            // Poses tenues (air, parade) : mélangées en douceur avec le balancier au sol
            float poseArm = parrying ? parryArmAngle - torsoTilt - rest : airArmAngle - rest;
            float poseElbow = parrying ? 60f - restElbow : -restElbow * 0.5f;
            float k = 1f - Mathf.Exp(-armSharpness * dt);
            view.armPose = Mathf.Lerp(view.armPose, parrying || !grounded ? 1f : 0f, k);
            view.armAngle = Mathf.Lerp(groundArm, poseArm, view.armPose);
            view.elbowAngle = Mathf.Lerp(groundElbow, poseElbow, view.armPose);

            view.root.localRotation = Quaternion.Euler(0f, 0f, view.armAngle + shake * 20f) * view.restRoot;
            if (view.elbow != null) view.elbow.localRotation = Quaternion.Euler(0f, 0f, view.elbowAngle) * view.restElbow;
            // Côté loin : forme retournée en profondeur (vrai miroir du bras côté caméra)
            view.root.localScale = new Vector3(1f, 1f, view.side > 0 ? -1f : 1f) * view.swell;
        }

        // Bras ballant : pendule amorti. Le vent (vitesse²) et l'accélération le poussent en arrière, la gravité le ramène ;
        // quand le corps rebondit (épaule qui monte / descend), la gravité ressentie change et le bras flotte
        private void UpdateLimpArm(LimbView view, float dt)
        {
            if (dt <= 0f) return;
            float y = view.root.position.y;
            if (!view.hasShoulder)
            {
                view.shoulderY = y;
                view.trail = 0f;
                view.trailVel = 0f;
                view.hasShoulder = true;
            }
            float velY = (y - view.shoulderY) / dt;
            view.shoulderY = y;
            float accelY = (velY - view.shoulderVelY) / dt;
            view.shoulderVelY = velY;
            view.shoulderAccelY = Mathf.Lerp(view.shoulderAccelY, Mathf.Clamp(accelY, -40f, 40f), 1f - Mathf.Exp(-20f * dt));

            float speed = lastForwardSpeed;   // > 0 : vers l'avant
            float push = armDrag * speed * Mathf.Abs(speed) + forwardAccel;
            float gravity = Mathf.Clamp(9.81f + view.shoulderAccelY * armBounce, 2f, 30f);
            float goal = Mathf.Clamp(Mathf.Atan2(push, gravity) * Mathf.Rad2Deg, -30f, maxArmTrail);

            float dtc = Mathf.Min(dt, 0.05f);
            view.trailVel += (armStiffness * (goal - view.trail) - armDamping * view.trailVel) * dtc;
            view.trail = Mathf.Clamp(view.trail + view.trailVel * dtc, -30f, maxArmTrail);
        }

        // Gestes de la queue au repos. Courbures en degrés pour toute la longueur de la queue.
        // Signes : « lift » > 0 lève la base, « hook » > 0 enroule le bout vers l'avant (crochet du point d'interrogation).
        private struct TailGesture
        {
            public float lift;        // base levée (degrés)
            public float hook;        // crochet du bout (degrés de courbure sur le dernier tiers)
            public float hookSway;    // le crochet bascule lentement d'un côté à l'autre (amplitude, degrés)
            public float wave;        // vague de courbure qui descend vers le bout (degrés sur la longueur)
            public float waves;       // nombre de vagues le long de la queue

            public static TailGesture Lerp(TailGesture a, TailGesture b, float t)
            {
                return new TailGesture
                {
                    lift = Mathf.Lerp(a.lift, b.lift, t),
                    hook = Mathf.Lerp(a.hook, b.hook, t),
                    hookSway = Mathf.Lerp(a.hookSway, b.hookSway, t),
                    wave = Mathf.Lerp(a.wave, b.wave, t),
                    waves = Mathf.Lerp(a.waves, b.waves, t),
                };
            }
        }

        private static readonly TailGesture[] TailGestures =
        {
            // Point d'interrogation : base levée, bout en crochet qui bascule doucement
            new TailGesture { lift = 10f, hook = 95f, hookSway = 30f, wave = 45f, waves = 0.8f },
            // Balayage lent et ample : une grande vague la parcourt
            new TailGesture { lift = -3f, hook = 30f, hookSway = 12f, wave = 180f, waves = 0.9f },
            // Pause attentive : presque immobile, la queue repose dans sa courbe
            new TailGesture { lift = 4f, hook = 50f, hookSway = 6f, wave = 14f, waves = 1.2f },
            // Crochet qui bascule : le bout passe lentement d'un côté à l'autre
            new TailGesture { lift = 2f, hook = 20f, hookSway = 80f, wave = 30f, waves = 0.7f },
        };

        // Queue : courbe de repos du fichier + gestes au repos + vent du mouvement.
        // Chaque os k a un décalage « absolu » (par rapport à sa direction de repos, repère du torse) :
        //  - cible = somme des plis des os 0..k (gestes, courbure intégrée le long de la queue : courbe lisse)
        //            + vent : la part couchée croît avec v², plus forte vers le bout ;
        //  - ressort vers cette cible, de plus en plus mou vers le bout : les gestes partent de la base et arrivent au
        //    bout en retard ; la queue fouette aux arrêts, demi-tours, sauts, atterrissages ;
        //  - par-dessus, directement : claquement et rafales de la course.
        // Rotation locale d'un os = son décalage moins celui de l'os précédent (les rotations s'enchaînent).
        // Une rotation Z positive tourne l'os dans le sens trigonométrique (repère « regarde à droite ») ; la queue
        // pointe vers l'arrière, donc une rotation négative la lève / l'enroule vers l'avant.
        // La rotation Y la balance vers la caméra ou loin d'elle.
        private void PlaceTail(LimbView view, float shake, Vector3 vel, float dt)
        {
            CatTorsoRig rig = TorsoRig;
            if (rig != null) view.root.localPosition = rig.TailBase;
            view.root.localScale = Vector3.one * view.swell;
            if (view.tailJoints == null) return;

            int n = view.tailJoints.Length;
            EnsureTailState(view, n);

            // Vent ressenti, dans le repère du torse (il suit le demi-tour et l'inclinaison) : opposé au mouvement
            Transform torso = view.root.parent;
            Vector3 wind = torso != null ? torso.InverseTransformDirection(-vel) : -vel;
            float speed = new Vector2(wind.x, wind.y).magnitude;
            float windAngle = Mathf.Atan2(wind.y, wind.x) * Mathf.Rad2Deg;
            float pressure = tailWind * speed * speed;
            float stream = tailMaxStream * pressure / (pressure + 1f);
            float full = Mathf.Clamp01(speed / Mathf.Max(0.1f, tailFullSpeed));
            view.tailPhase += dt * (tailWaveSpeed + tailFlutterSpeed * full);
            float dtc = Mathf.Min(dt, 0.05f);

            // Repos : geste en cours (fondu vers le suivant), s'efface quand le vent prend le relais
            float idle = (1f - full) * tailIdle;
            TailGesture g = UpdateTailGesture(view, dt);
            view.tailIdleClock += dt * tailIdleTempo;
            float clock = view.tailIdleClock;
            float time = Time.time;
            float hook = g.hook + g.hookSway * Mathf.Sin(clock * 0.75f + 0.8f) + g.hookSway * 0.3f * Mathf.Sin(clock * 1.9f);
            float lift = g.lift + tailBreath * Mathf.Sin(time * 2.2f);   // même rythme que la respiration du corps
            float wavePhase = clock * 1.75f;                                // ≈ 3,6 s par vague
            float ds = 1f / n;

            float fold = 0f;                 // courbure intégrée : décalage d'idle de l'os k
            float previous = 0f, previousDepth = 0f;
            float shakePerBone = shake * 70f / n;
            for (int k = 0; k < n; k++)
            {
                float s = (k + 0.5f) / n;    // place de l'os le long de la queue (0 base, 1 bout)
                float tip = TipCurve(s);     // courbure du dernier tiers (intégrale 1 sur la queue)

                // Pli de l'os k (degrés) : base levée, crochet du bout, vague qui descend vers le bout
                float bendHere = (k == 0 ? -lift : 0f)
                                 - hook * tip * ds
                                 + g.wave * Mathf.Sin(wavePhase - Mathf.PI * 2f * g.waves * s) * Mathf.Lerp(0.35f, 1f, s) * ds;
                fold += bendHere * idle;

                // Vent : la queue se couche dans son sens (le bout plus que la base)
                float windGoal = speed > 0.01f
                    ? stream * Mathf.Lerp(0.6f, 1f, s) * Mathf.DeltaAngle(view.tailRestAngle[k], windAngle)
                    : 0f;
                float goal = Mathf.Clamp(fold + windGoal, -tailMaxBend, tailMaxBend);

                // Ressort de plus en plus mou vers le bout (retard, léger dépassement)
                float stiff = tailStiffness * Mathf.Lerp(1f, tailTipSoftness, s);
                float damp = tailDamping * Mathf.Sqrt(stiff / Mathf.Max(0.01f, tailStiffness));
                view.tailBendVel[k] += (stiff * (goal - view.tailBend[k]) - damp * view.tailBendVel[k]) * dtc;
                view.tailBend[k] = Mathf.Clamp(view.tailBend[k] + view.tailBendVel[k] * dtc, -tailMaxBend, tailMaxBend);

                // Directement : claquement et rafales de la course
                float flutter = Mathf.Sin(view.tailPhase - Mathf.PI * 2f * 1.2f * s) * tailFlutter * full * s;
                float gust = (Mathf.PerlinNoise(time * 3f, view.phaseOffset * 5f + s * 2.5f) - 0.5f) * 2f * tailGust * full * s;
                float bend = view.tailBend[k] + flutter + gust;

                // Profondeur : lent balancement vers la caméra et loin d'elle
                float depth = tailDepth * s * Mathf.Sin(clock * 1.3f + 0.7f - Mathf.PI * 1.2f * s) * idle;

                // La base compense la moitié du basculement du torse
                float angle = bend - previous + (k == 0 ? -TiltOffRest * 0.5f : 0f) + shakePerBone;
                float yaw = depth - previousDepth;
                previous = bend;
                previousDepth = depth;
                Quaternion rest = view.restTail != null && k < view.restTail.Length ? view.restTail[k] : Quaternion.identity;
                view.tailJoints[k].localRotation = Quaternion.Euler(0f, yaw, angle) * rest;
            }
        }

        // Répartition du crochet sur la queue : en cloche sur les 60 % du bout (nulle avant, maximale vers 70 %,
        // le tout dernier bout presque droit) : le crochet s'arrondit au lieu de casser. Intégrale ≈ 1 sur [0, 1].
        private static float TipCurve(float s)
        {
            const float from = 0.4f;
            if (s <= from) return 0f;
            float x = (s - from) / (1f - from);
            float bell = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.8f));
            return bell * bell / (0.5f * (1f - from));
        }

        // Geste en cours, fondu (ease-in/ease-out) vers le suivant ; un nouveau geste est tiré quand le temps est écoulé
        private TailGesture UpdateTailGesture(LimbView view, float dt)
        {
            view.tailGestureTimer -= dt;
            if (view.tailGestureBlend >= 1f && view.tailGestureTimer <= 0f)
            {
                view.tailGesture = view.tailNextGesture;
                int next = UnityEngine.Random.Range(0, TailGestures.Length - 1);
                view.tailNextGesture = next >= view.tailGesture ? next + 1 : next;   // jamais deux fois le même
                view.tailGestureBlend = 0f;
                view.tailGestureTimer = UnityEngine.Random.Range(tailGestureMin, Mathf.Max(tailGestureMin, tailGestureMax));
            }
            if (view.tailGestureBlend < 1f) view.tailGestureBlend = Mathf.Min(1f, view.tailGestureBlend + dt / Mathf.Max(0.05f, tailGestureBlend));
            float t = view.tailGestureBlend;
            float ease = t * t * t * (t * (t * 6f - 15f) + 10f);   // départ et arrivée tout en douceur
            return TailGesture.Lerp(TailGestures[view.tailGesture], TailGestures[view.tailNextGesture], ease);
        }

        // Direction de repos de chaque os (degrés, repère du torse ; os orienté vers −X dans son repère)
        private void EnsureTailState(LimbView view, int n)
        {
            if (view.tailBend != null && view.tailBend.Length == n) return;
            view.tailBend = new float[n];
            view.tailBendVel = new float[n];
            view.tailRestAngle = new float[n];
            view.tailGesture = view.tailNextGesture = UnityEngine.Random.Range(0, TailGestures.Length);
            view.tailGestureBlend = 1f;
            view.tailGestureTimer = UnityEngine.Random.Range(tailGestureMin, Mathf.Max(tailGestureMin, tailGestureMax));
            view.tailIdleClock = UnityEngine.Random.Range(0f, 20f);
            Quaternion chain = view.root.localRotation;
            for (int i = 0; i < n; i++)
            {
                chain *= view.restTail != null && i < view.restTail.Length ? view.restTail[i] : Quaternion.identity;
                Vector3 dir = chain * Vector3.left;
                view.tailRestAngle[i] = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
        }

        // Cycle de marche et mouvement du corps debout
        private void UpdateBodyMotion(float dt, float facing, Vector3 vel, bool grounded, float moving)
        {
            float speed = Mathf.Abs(vel.x);
            float stride = Stride(speed);
            int legs = body.IntactLegCount;

            // Le cycle avance avec la distance : en appui, le pied recule exactement à la vitesse du sol.
            // Une patte : un bond par cycle, à cadence propre (la longueur du bond en découle)
            walkWeight = Mathf.MoveTowards(walkWeight, grounded && speed > 0.3f ? 1f : 0f, dt * walkFade);
            hopWeight = Mathf.MoveTowards(hopWeight, legs == 1 ? 1f : 0f, dt * walkFade);
            float runGoal = legs == 1 ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(runBlendFrom, runBlendTo, speed));
            runWeight = Mathf.MoveTowards(runWeight, runGoal, dt * walkFade);
            float previousPhase = gaitPhase;
            if (grounded)
            {
                float rate = legs == 1 ? (speed > 0.3f ? HopRate(speed) : 0f) : Duty() * speed / Mathf.Max(0.05f, stride);
                gaitPhase = Mathf.Repeat(gaitPhase + rate * dt, 1f);
            }

            // Deux pattes : le corps monte au milieu de chaque pas (deux fois par cycle), sans tourner.
            // Marche : au plus bas pattes écartées ; course : au plus bas au milieu de l'appui, au plus haut pendant le vol
            float rise = Mathf.Lerp(stepRise, runRise, runWeight);
            // (une patte : pas d'accroupi de marche, le bond a le sien)
            float crouch = Mathf.Lerp(walkCrouch, runCrouch, runWeight) * walkWeight * (1f - hopWeight);
            float bob = (0.5f - 0.5f * Mathf.Cos((gaitPhase - runWeight * Duty() * 0.5f) * Mathf.PI * 4f)) * rise;
            // Une patte : accroupi pendant l'appui, arc pendant le vol ; écrasé à chaque réception
            if (legs == 1)
            {
                bob = HopHeightAt(gaitPhase, speed);
                if (grounded && gaitPhase < previousPhase) bodyDropVel -= hopImpact * walkWeight;
            }
            hopLean = legs == 1 && gaitPhase < hopDuty ? hopTilt * Mathf.Sin(Mathf.PI * gaitPhase / hopDuty) * walkWeight : 0f;

            // Retombée d'un saut : le corps s'écrase puis revient (ressort)
            if (grounded && !wasGrounded) bodyDropVel -= landImpact * Mathf.Clamp01(-vel.y / 6f + 0.4f);
            wasGrounded = grounded;
            float dtc = Mathf.Min(dt, 0.05f);
            bodyDropVel += (-bodySpringStiffness * bodyDrop - bodySpringDamping * bodyDropVel) * dtc;
            bodyDrop += bodyDropVel * dtc;

            float breath = Mathf.Sin(Time.time * 2.2f) * breathing * (1f - walkWeight);
            bodyY = grounded ? bob * walkWeight - crouch + bodyDrop * 0.12f + breath : 0f;
            // Au sol le torse garde son angle ; en l'air il bascule selon la vitesse verticale
            bodyPitch = grounded ? 0f : -vel.y * 2f * facing;
            UpdateLean(dt, facing, vel, grounded);
        }

        // Penché selon la vitesse vers l'avant (marche → course) et l'accélération ; en l'air, garde l'élan
        private void UpdateLean(float dt, float facing, Vector3 vel, bool grounded)
        {
            if (dt <= 0f) return;
            float forward = vel.x * facing;   // > 0 : avance dans le sens du regard
            forwardAccel = Mathf.Lerp(forwardAccel, (forward - lastForwardSpeed) / dt, 1f - Mathf.Exp(-10f * dt));
            lastForwardSpeed = forward;
            if (!grounded) return;
            float pace = Mathf.Lerp(walkLean, runLean, runWeight) + Mathf.Max(0f, Mathf.Abs(forward) - runBlendTo) * overspeedLean;
            float cruise = pace * walkWeight * Mathf.Sign(forward);
            float goal = Mathf.Clamp(cruise + forwardAccel * accelLean, -maxBrakeLean, maxLean);
            lean = Mathf.Lerp(lean, goal, 1f - Mathf.Exp(-leanResponse * dt));
        }

        // Sans pattes, avec la tête : couché sur le ventre (torse à 90° vers l'avant), avance par tractions des bras
        // (ondulation légère) ou, sans bras, ondule comme un ver
        private void UpdateCrawl(float dt, float facing, Vector3 vel, bool grounded)
        {
            float speed = Mathf.Abs(vel.x);
            float moving = Mathf.Clamp01(speed / 0.5f);
            bool arms = HasPart(LimbKind.Arm);
            if (grounded) crawlPhase = Mathf.Repeat(crawlPhase + speed / Mathf.Max(0.05f, crawlStride) * 0.5f * dt, 1f);
            float wave = Mathf.Sin(crawlPhase * Mathf.PI * 4f);   // deux tractions par cycle (un bras puis l'autre)
            // Avec bras : l'avant monte pendant que le bras qui tire ramène la main vers lui (bras en alternance)
            float pull = 0f;
            if (arms)
            {
                for (int side = 0; side < 2; side++)
                {
                    float p = Mathf.Repeat(crawlPhase + 0.5f * side, 1f);
                    if (p >= crawlReach) pull = Mathf.Max(pull, Mathf.Sin((p - crawlReach) / (1f - crawlReach) * Mathf.PI));
                }
            }
            float chest = crawlChestLift * pull * moving;
            crawlPitch = Mathf.Asin(Mathf.Clamp01(chest / 0.45f)) * Mathf.Rad2Deg;
            float lying = -(90f - crawlPitch) * facing + (arms ? crawlWave * 0.3f : wormWave) * wave * moving;
            roll = Mathf.LerpAngle(roll, lying, 1f - Mathf.Exp(-12f * dt));
            crawlLift = arms ? chest * 0.4f : wormLift * Mathf.Max(0f, wave) * moving;
        }

        // Bras en rampant : la main est posée au sol, le bras se plie pour l'atteindre (deux os, coude vers le haut).
        // À l'arrêt : mains devant (sphinx). En avançant : main plantée qui recule à la vitesse du sol (traction),
        // puis qui repart devant en arc. Angles rendus depuis la verticale du monde (+ = devant), coude absolu.
        private void CrawlArm(LimbView view, out float arm, out float bend)
        {
            float moving = Mathf.Clamp01(Mathf.Abs(lastForwardSpeed) / 0.5f);
            float p = Mathf.Repeat(crawlPhase + view.phaseOffset, 1f);
            float span = 2f * crawlStride * (1f - crawlReach);   // chemin de la main pendant la traction
            float front = crawlHandFront, back = crawlHandFront - span;
            float along, lift;
            if (p < crawlReach)
            {
                float u = p / crawlReach;
                along = Mathf.Lerp(back, front, u * u * (3f - 2f * u));
                lift = crawlHandLift * Mathf.Sin(u * Mathf.PI);
            }
            else
            {
                along = Mathf.Lerp(front, back, (p - crawlReach) / (1f - crawlReach));
                lift = 0f;
            }
            along = Mathf.Lerp(crawlRestHand, along, moving);
            lift *= moving;

            // Main visée, dans le repère « regarde à droite » de l'épaule
            Vector3 shoulder = view.root.position;
            float ground = transform.position.y - FeetBelowCenter + 0.03f;
            var target = new Vector2(along, ground + lift - shoulder.y);

            float l1 = view.elbow != null ? view.elbow.localPosition.magnitude : 0.45f;
            if (view.foreLength <= 0f) view.foreLength = ForeLength(view);
            float l2 = view.foreLength;
            float d = Mathf.Clamp(target.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.01f);
            float toTarget = Mathf.Atan2(target.x, -target.y) * Mathf.Rad2Deg;
            float atShoulder = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f)) * Mathf.Rad2Deg;
            float atElbow = Mathf.Acos(Mathf.Clamp((l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2), -1f, 1f)) * Mathf.Rad2Deg;
            arm = toTarget - atShoulder;   // coude vers le bas, avant-bras posé vers l'avant
            bend = 180f - atElbow;
        }

        // Longueur coude → bout de la main (maillages de l'avant-bras et de la main, repère du coude)
        private static float ForeLength(LimbView view)
        {
            float length = 0f;
            if (view.elbow == null) return 0.5f;
            foreach (MeshFilter filter in view.elbow.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) length = Mathf.Max(length, -filter.sharedMesh.bounds.min.y);
            }
            return length > 0.05f ? length * 0.9f : 0.5f;
        }

        // Le chat a-t-il encore ce membre (non arraché) ?
        private bool HasPart(LimbKind kind)
        {
            foreach (Limb limb in body.Limbs)
            {
                if (limb.Kind == kind && !limb.IsTorn) return true;
            }
            return false;
        }

        // Une patte : bonds par seconde selon la vitesse
        private float HopRate(float speed)
        {
            return Mathf.Lerp(hopRateSlow, hopRateFast, Mathf.Clamp01(speed / Mathf.Max(0.1f, hopFastSpeed)));
        }

        // Hauteur du corps sur le cycle d'un bond : accroupi pendant l'appui, arc (parabole) pendant le vol
        private float HopHeightAt(float phase, float speed)
        {
            float p = Mathf.Repeat(phase, 1f);
            if (p < hopDuty) return -hopSquat * Mathf.Sin(Mathf.PI * p / hopDuty);
            float v = (p - hopDuty) / (1f - hopDuty);
            return HopApex(speed) * 4f * v * (1f - v);
        }

        // Sommet du bond : hauteur d'un vrai vol de cette durée (g·t²/8), exagérée par hopHeight
        private float HopApex(float speed)
        {
            float flight = (1f - hopDuty) / HopRate(speed);
            return 9.81f * flight * flight / 8f * hopHeight;
        }

        // Position avant/arrière d'une patte sur son cycle, adoucie : +1 tout devant (pose du pied, phase 0),
        // −1 tout derrière (pied qui décolle, fin de l'appui), en cosinus sur l'appui puis sur le retour
        private float GaitForward(float phase)
        {
            float p = Mathf.Repeat(phase, 1f);
            float duty = Duty();
            return p < duty
                ? Mathf.Cos(Mathf.PI * p / duty)
                : -Mathf.Cos(Mathf.PI * (p - duty) / (1f - duty));
        }

        // Part du cycle où le pied est au sol : moins en courant (phase de vol)
        private float Duty()
        {
            return Mathf.Lerp(dutyFactor, runDutyFactor, runWeight);
        }

        private float Stride(float speed)
        {
            return Mathf.Clamp(strideMin + speed * stridePerSpeed, strideMin, Mathf.Min(strideMax, maxReach * 2f));
        }

        // Attache de la patte : la hanche du torse (CatTorsoRig), en bas et à l'arrière ; la cuisse y forme la fesse
        // et suit le torse (inclinaison, pas, demi-tour). Sans torse : hauteur fixe sous le joueur.
        private Vector3 HipPosition(Limb limb, float facing, float standing)
        {
            CatTorsoRig rig = TorsoRig;
            float side = Mathf.Sign(limb.Anchor.z == 0f ? 1f : limb.Anchor.z);   // −1 : côté caméra
            if (rig == null || torsoView == null || !torsoView.root.gameObject.activeSelf)
            {
                return transform.position + new Vector3(limb.Anchor.x * facing, hipHeight + bodyY * standing, limb.Anchor.z);
            }
            // Le torse fait demi-tour sur lui-même (180°) : le côté caméra de son repère s'inverse quand il regarde à gauche
            Vector3 local = rig.Hip;
            float depth = Mathf.Abs(local.z);
            local.z = depth * side * (facing > 0f ? 1f : -1f);
            Vector3 hip = torsoView.ball.TransformPoint(local);
            hip.z = transform.position.z + depth * side;   // la profondeur ne bascule pas pendant le demi-tour
            return hip;
        }

        private void PlaceLeg(Limb limb, LimbView view, float facing, bool grounded, Vector3 vel, float shake, float dt)
        {
            float s = Mathf.SmoothStep(0f, 1f, standUp);
            Vector3 hip = HipPosition(limb, facing, s);
            view.root.position = hip;
            // Côté loin : forme retournée en profondeur (vrai miroir de la patte côté caméra)
            float mirror = limb.Anchor.z >= 0f ? -1f : 1f;   // même côté que HipPosition (z nul : côté loin)
            view.root.localScale = new Vector3(facing, 1f, mirror) * Mathf.Max(0.01f, standUp) * view.swell;

            UpdateFoot(view, hip, facing, grounded, vel, dt);

            // Cinématique inverse dans l'espace « regarde à droite » : x retourné si le chat regarde à gauche.
            // Patte écartée (rotation autour de X) : la hauteur voulue se divise par cos(écart), le pied part un peu en dehors
            float splayAngle = LegRig != null ? LegRig.Splay : 10f;
            Vector2 target = new Vector2((view.foot.x - hip.x) * facing,
                                         (view.foot.y - hip.y) / Mathf.Max(0.2f, Mathf.Cos(splayAngle * Mathf.Deg2Rad)));
            target.x += shake * 0.1f;
            SolveLeg(target, out float thighAngle, out float shinAngle);

            Vector2 thigh = ThighVector, shin = ShinVector;
            float restThigh = Mathf.Atan2(thigh.y, thigh.x) * Mathf.Rad2Deg;
            float restShin = Mathf.Atan2(shin.y, shin.x) * Mathf.Rad2Deg;
            float hipRot = thighAngle - restThigh;
            float kneeRot = shinAngle - restShin - hipRot;

            // Écartée vers l'extérieur, du côté de son attache (devant ou derrière la boule)
            float splay = -splayAngle * Mathf.Sign(limb.Anchor.z == 0f ? 1f : limb.Anchor.z);   // rotation autour de X : négatif = pied vers +Z
            view.root.rotation = Quaternion.Euler(splay, 0f, hipRot * facing);
            // Haut de la cuisse (fesse) : annule la rotation de la cuisse, reste collé au corps ; le pli se fait à la hanche
            if (view.hipAnchor != null) view.hipAnchor.localRotation = Quaternion.Euler(0f, 0f, -hipRot);
            if (view.knee != null)
            {
                // Genou replacé à chaque image : suit la longueur de patte même si le modèle est reconstruit en Play
                view.knee.localPosition = KneeOffset;
                view.knee.localRotation = Quaternion.Euler(0f, 0f, kneeRot);
            }
            if (view.ankle != null)
            {
                // Pied à plat : la cheville annule la rotation de la cuisse et du jarret.
                // Il bascule pointe en bas quand il se lève (fin du pas), et en l'air (pattes repliées)
                float footPitch = !grounded || standUp < 0.5f ? -airToeDown : -view.lift * toeOff;
                view.ankle.localPosition = AnkleOffset;
                view.ankle.localRotation = Quaternion.Euler(0f, 0f, footPitch - hipRot - kneeRot);
            }
        }

        // Position du pied sur le cycle : appui (recule à la vitesse du sol) puis retour en arc vers l'avant
        private void UpdateFoot(LimbView view, Vector3 hip, float facing, bool grounded, Vector3 vel, float dt)
        {
            float legLength = ThighVector.magnitude + ShinVector.magnitude;

            // En l'air : pattes repliées sous le corps
            if (!grounded || standUp < 0.5f)
            {
                Vector3 tucked = hip + new Vector3(0.12f * facing, -legLength * 0.7f, 0f);
                view.foot = view.hasFoot ? Vector3.Lerp(view.foot, tucked, 1f - Mathf.Exp(-12f * dt)) : tucked;
                view.hasFoot = true;
                view.lift = 0f;
                return;
            }

            float stride = Stride(Mathf.Abs(vel.x));
            float dir = Mathf.Abs(vel.x) > 0.05f ? Mathf.Sign(vel.x) : facing;
            if (body.IntactLegCount == 1)
            {
                UpdateHopFoot(view, hip, facing, dir, Mathf.Abs(vel.x), dt);
                return;
            }
            float p = Mathf.Repeat(gaitPhase + view.phaseOffset, 1f);
            float duty = Duty();
            float rel, lift;
            if (p < duty)
            {
                // Appui : de l'avant vers l'arrière, à vitesse constante (collé au sol)
                rel = Mathf.Lerp(0.5f, -0.5f, p / duty);
                lift = 0f;
            }
            else
            {
                // Retour : de l'arrière vers l'avant, en arc, adouci aux deux bouts
                float u = (p - duty) / (1f - duty);
                rel = Mathf.Lerp(-0.5f, 0.5f, u * u * (3f - 2f * u));
                lift = Mathf.Sin(u * Mathf.PI);
            }
            rel *= stride * walkWeight;
            lift *= walkWeight;

            // Au repos, la cheville est à la place du fichier Blender (devant la hanche, au-dessus du sol)
            float forward = LegRig != null ? LegRig.FootForward : 0f;
            float ankleHeight = LegRig != null ? LegRig.AnkleHeight : 0.07f;
            float x = hip.x + forward * facing + rel * dir;
            float arc = Mathf.Lerp(stepHeight, runStepHeight, runWeight);
            Vector3 target = new Vector3(x, GroundAt(hip, x) + ankleHeight + lift * arc, hip.z);
            // Repose sous la hanche sans à-coup (atterrissage, changement de forme)
            view.foot = view.hasFoot ? Vector3.Lerp(view.foot, target, 1f - Mathf.Exp(-30f * dt)) : target;
            view.hasFoot = true;
            view.lift = lift;
        }

        // Une patte : pendant l'appui le pied recule à la vitesse du sol ; en l'air il quitte le sol avec le corps,
        // se replie, et revient se poser devant la hanche
        private void UpdateHopFoot(LimbView view, Vector3 hip, float facing, float dir, float speed, float dt)
        {
            float p = Mathf.Repeat(gaitPhase, 1f);
            float rate = HopRate(speed);
            float reach = speed * hopDuty / rate * walkWeight;   // distance parcourue pendant l'appui
            float rel, height, lift;
            if (p < hopDuty)
            {
                rel = Mathf.Lerp(0.5f, -0.5f, p / hopDuty) * reach;
                height = 0f;
                lift = 0f;
            }
            else
            {
                float v = (p - hopDuty) / (1f - hopDuty);
                rel = Mathf.Lerp(-0.5f, 0.5f, v * v * (3f - 2f * v)) * reach;
                lift = Mathf.Sin(v * Mathf.PI);
                height = (HopApex(speed) * 4f * v * (1f - v) + hopTuck * lift) * walkWeight;
            }

            float forward = LegRig != null ? LegRig.FootForward : 0f;
            float ankleHeight = LegRig != null ? LegRig.AnkleHeight : 0.07f;
            float x = hip.x + forward * facing + rel * dir;
            Vector3 target = new Vector3(x, GroundAt(hip, x) + ankleHeight + height, hip.z);
            view.foot = view.hasFoot ? Vector3.Lerp(view.foot, target, 1f - Mathf.Exp(-30f * dt)) : target;
            view.hasFoot = true;
            view.lift = lift * walkWeight;
        }

        // Hauteur du sol sous un point (le joueur ignoré) ; à défaut, le bas du joueur
        private float GroundAt(Vector3 hip, float x)
        {
            float fallback = transform.position.y - FeetBelowCenter;
            Vector3 from = new Vector3(x, hip.y + 0.3f, 0f);
            float best = float.MaxValue, y = fallback;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody == rb) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    y = hit.point.y;
                }
            }
            return y;
        }

        // Repères de la patte, lus sur le prefab (relus si le prefab est reconstruit pendant le Play)
        private CatLegRig LegRig
        {
            get
            {
                if (legRig == null && legPrefab != null) legRig = legPrefab.GetComponent<CatLegRig>();
                return legRig;
            }
        }

        private Vector2 ThighVector => KneeOffset;    // hanche → genou (profil)
        private Vector2 ShinVector => AnkleOffset;    // genou → cheville (profil)
        private Vector3 KneeOffset => LegRig != null ? LegRig.KneeOffset : new Vector3(0.14f, -0.36f, 0f);
        private Vector3 AnkleOffset => LegRig != null ? LegRig.AnkleOffset : new Vector3(0f, -0.47f, 0f);

        // Pose debout du torse (CatTorsoRig) ; sans prefab, valeurs d'avant le fichier Blender
        private Vector2 StandingPosition => TorsoRig != null ? TorsoRig.StandingPosition : new Vector2(0f, 1.01f);
        private float StandingTilt => TorsoRig != null ? TorsoRig.StandingTilt : 22f;

        // Basculement du torse par rapport à sa pose de repos (0 debout immobile) : tête, bras et queue le compensent
        private float TiltOffRest => torsoTilt + StandingTilt;

        // Angle autour de Z d'une rotation (−180 → 180)
        private static float SignedZ(Quaternion q)
        {
            return Mathf.DeltaAngle(0f, q.eulerAngles.z);
        }

        // Deux segments (cuisse, jarret) de la hanche (origine) vers la cheville ; genou vers l'avant.
        // Renvoie les angles (degrés) de la cuisse et du jarret dans l'espace « regarde à droite ».
        private void SolveLeg(Vector2 target, out float thighAngle, out float shinAngle)
        {
            float l1 = ThighVector.magnitude, l2 = ShinVector.magnitude;
            float d = Mathf.Clamp(target.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            float toTarget = Mathf.Atan2(target.y, target.x);
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float a1 = toTarget + Mathf.Acos(cosA);
            Vector2 knee = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * l1;
            Vector2 dir = target.normalized * d - knee;
            thighAngle = a1 * Mathf.Rad2Deg;
            shinAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        // Crée ou retire les visuels pour suivre la liste des membres
        private void SyncViews()
        {
            IReadOnlyList<Limb> limbs = body.Limbs;
            if (views.Count == limbs.Count)
            {
                bool same = true;
                foreach (Limb limb in limbs)
                {
                    if (!views.ContainsKey(limb)) { same = false; break; }
                }
                if (same) return;
            }

            generated.DestroyAll();
            fill = null;
            views.Clear();
            torsoView = null;

            // Le torse d'abord (ventre) : tête, bras et queue s'y accrochent (sans torse, rien ne tient)
            foreach (Limb limb in limbs)
            {
                if (limb.Kind != LimbKind.Flesh || torsoView != null) continue;
                torsoView = BuildFlesh();
                if (torsoView != null) views[limb] = torsoView;
            }

            // Puis les modèles à part (cuisse = la patte entière, bras, tête, queue)
            int legIndex = 0, armIndex = 0;
            foreach (Limb limb in limbs)
            {
                LimbView view = null;
                switch (limb.Kind)
                {
                    case LimbKind.Leg: view = BuildLeg(legIndex++); break;
                    case LimbKind.Arm:
                        view = BuildPart(armPrefab, armIndex * 0.5f + 0.5f, armIndex++ == 0 ? -1 : 1);
                        // Le haut du bras seul : avant-bras et main sont des parties à part (PartOf)
                        Transform upperPart = view != null ? view.root.Find("Upper") : null;
                        if (upperPart != null) view.renderers = upperPart.GetComponents<Renderer>();
                        break;
                    case LimbKind.Head: view = BuildPart(headPrefab, 0f, 0); break;
                    case LimbKind.Tail: view = BuildPart(tailPrefab, 0f, 0); break;
                }
                if (view != null) views[limb] = view;
            }

            // Enfin les parties de ces modèles : poitrine et bassin (torse), jarret et pied (patte de leur cuisse),
            // avant-bras et main (bras de leur haut du bras)
            foreach (Limb limb in limbs)
            {
                LimbView view = null;
                switch (limb.Kind)
                {
                    case LimbKind.Chest: view = PartOf(torsoView, "Chest"); break;
                    case LimbKind.Pelvis: view = PartOf(torsoView, "Pelvis"); break;
                    case LimbKind.Shin: view = PartOf(LegViewOf(limb), "Shin"); break;
                    case LimbKind.Foot: view = PartOf(LegViewOf(body.ParentOf(limb)), "Foot"); break;
                    case LimbKind.Forearm: view = PartOf(ViewOf(body.ParentOf(limb)), "Elbow/Fore"); break;
                    case LimbKind.Hand: view = PartOf(ViewOf(body.ParentOf(body.ParentOf(limb))), "Elbow/Hand"); break;
                }
                if (view != null) views[limb] = view;
            }
        }

        // Vue déjà construite d'un membre (null si aucune)
        private LimbView ViewOf(Limb limb)
        {
            return limb != null && views.TryGetValue(limb, out LimbView view) ? view : null;
        }

        // Vue de la patte (cuisse) qui porte ce jarret
        private LimbView LegViewOf(Limb shin)
        {
            Limb thigh = body.ParentOf(shin);
            return thigh != null && views.TryGetValue(thigh, out LimbView view) ? view : null;
        }

        // Partie nommée d'un modèle déjà construit (rendus seulement)
        private static LimbView PartOf(LimbView host, string childName)
        {
            if (host == null) return null;
            Transform model = host.ball != null ? host.ball : host.root;
            Transform part = model.Find(childName);
            if (part == null) return null;
            return new LimbView
            {
                isPart = true,
                root = part,
                renderers = part.GetComponents<Renderer>(),
                phaseOffset = host.phaseOffset,
            };
        }

        // Points d'attache du torse, lus sur le prefab (relus si le prefab est reconstruit pendant le Play)
        private CatTorsoRig TorsoRig
        {
            get
            {
                if (torsoRig == null && fleshBallPrefab != null) torsoRig = fleshBallPrefab.GetComponent<CatTorsoRig>();
                return torsoRig;
            }
        }

        // Tête, bras ou queue : instancié comme enfant du torse
        private LimbView BuildPart(GameObject prefab, float phaseOffset, int side)
        {
            if (prefab == null || torsoView == null) return null;
            Transform t = generated.Track(Instantiate(prefab, torsoView.ball)).transform;
            var view = new LimbView
            {
                root = t,
                elbow = t.Find("Elbow"),
                renderers = t.GetComponentsInChildren<Renderer>(true),
                phaseOffset = phaseOffset,
                side = side,
            };
            // Pose de repos du prefab. Bras côté loin : racine en miroir (et forme retournée, PlaceArm) ;
            // le coude, dans le bras retourné, garde la même rotation (même règle que les copies Far_ dans Blender)
            view.restRoot = side > 0 ? MirrorDepth(t.localRotation) : t.localRotation;
            if (view.elbow != null) view.restElbow = view.elbow.localRotation;

            // Queue : chaîne d'os Bone0 → Bone1 → ... (queue continue), sinon ancienne chaîne Seg0 → Seg1 → ...
            var joints = new List<Transform>();
            string jointName = t.Find("Bone0") != null ? "Bone" : "Seg";
            Transform seg = t.Find(jointName + "0");
            while (seg != null)
            {
                joints.Add(seg);
                seg = seg.Find(jointName + joints.Count);
            }
            if (joints.Count > 0)
            {
                view.tailJoints = joints.ToArray();
                view.restTail = new Quaternion[joints.Count];
                for (int i = 0; i < joints.Count; i++) view.restTail[i] = joints[i].localRotation;
            }
            return view;
        }

        // Distance du centre du torse au point le plus bas de son contour, pour la rotation actuelle (boule qui roule)
        private float RollRadius()
        {
            if (torsoView == null || torsoView.outline == null) return 0.45f;
            float a = roll * Mathf.Deg2Rad, sin = Mathf.Sin(a), cos = Mathf.Cos(a);
            float yaw = Mathf.Cos(turnYaw * Mathf.Deg2Rad);   // demi-tour : le profil se retourne
            float below = 0f;
            foreach (Vector2 p in torsoView.outline)
            {
                float y = p.x * yaw * sin + p.y * cos;
                below = Mathf.Max(below, -y);
            }
            return below * torsoView.swell;
        }

        // Contour de profil du torse (plan X/Y de la boule) : enveloppe convexe de ses sommets
        private static Vector2[] ProfileOutline(Transform ball)
        {
            var points = new List<Vector2>();
            foreach (MeshFilter filter in ball.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = ball.InverseTransformPoint(filter.transform.TransformPoint(v));
                    points.Add(new Vector2(p.x, p.y));
                }
            }
            if (points.Count < 3) return null;
            // Enveloppe convexe (chaîne monotone)
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 p = points[pass == 0 ? i : points.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull.ToArray();
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        // Rotation vue dans un miroir (plan avant / arrière du torse, profondeur Z inversée)
        private static Quaternion MirrorDepth(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, q.z, q.w);
        }

        private LimbView BuildFlesh()
        {
            if (fleshBallPrefab == null) return null;
            GameObject root = generated.Track(new GameObject("Flesh"));
            Transform ball = generated.Track(Instantiate(fleshBallPrefab, root.transform)).transform;
            // Le ventre seul : poitrine et bassin sont des parties à part (PartOf)
            Transform belly = ball.Find("Belly");
            return new LimbView
            {
                root = root.transform,
                ball = ball,
                outline = ProfileOutline(ball),
                renderers = belly != null ? belly.GetComponents<Renderer>() : ball.GetComponentsInChildren<Renderer>(true),
                phaseOffset = 0.37f,
            };
        }

        private LimbView BuildLeg(int index)
        {
            if (legPrefab == null) return null;
            GameObject leg = generated.Track(Instantiate(legPrefab));
            // La cuisse seule : jarret et pied sont des parties à part (PartOf)
            Transform thigh = leg.transform.Find("Thigh");
            return new LimbView
            {
                root = leg.transform,
                knee = leg.transform.Find("Knee"),
                ankle = leg.transform.Find("Knee/Ankle"),
                hipAnchor = leg.transform.Find("HipAnchor"),
                renderers = thigh != null ? thigh.GetComponents<Renderer>() : leg.GetComponentsInChildren<Renderer>(true),
                phaseOffset = index * 0.5f,
            };
        }
    }
}
