# Parry Everything — Roadmap de développement

Plan de développement, du prototype actuel au jeu complet. Le design détaillé est dans [GAME_DESIGN.md](GAME_DESIGN.md).

**Règles :**
- On ne passe à une phase que quand la précédente remplit ses **critères de sortie**.
- Chaque phase liste les **décisions de design à prendre avant** de la commencer (⚠️). Pas de code sur une mécanique non tranchée.
- Taille : **S** = quelques jours, **M** = 1 à 2 semaines, **L** = 3 semaines ou plus (solo, à temps partiel : à réajuster après la phase 1).

**Légende :** ✅ fait · 🔶 en cours · ⬜ à faire

---

## Vue d'ensemble

| Phase | Objectif | Taille | Statut |
|---|---|---|---|
| 0 | Fondations techniques | M | 🔶 |
| 1 | Tranche verticale : zone 1, de l'âme aux pattes | L | 🔶 |
| 2 | Test de jeu et décision « on continue ? » | S | ⬜ |
| 3 | Zone 2 : la forêt des suicidés | L | ⬜ |
| 4 | Zone 3 : le Tribunal des morts | L | ⬜ |
| 5 | Démo : finitions et sortie | M | ⬜ |
| 6 | Jeu complet : zones 4 à 6, fins | L+ | ⬜ |
| 7 | Sortie PC | M | ⬜ |

La **démo** (phases 0 à 5) est le premier vrai objectif. La phase 6 ne se planifie en détail qu'après la démo.

---

## Phase 0 — Fondations techniques (M)

Ce dont toutes les phases suivantes ont besoin. Rien de visible pour le joueur, mais tout repose dessus.

| # | Tâche | Statut |
|---|---|---|
| 0.1 | **Flux de jeu** (`GameFlow`) : états menu, jeu, pause, mort, réveil, fin ; mise en scène de la mort ; fondus (`ScreenFade`) ; rechargement masqué. | 🔶 refait le 2026-11-20 ; à tester en jeu |
| 0.2 | **Sauvegarde** (`SaveSystem`) : fichier JSON versionné, copie `.bak`, écrite au sanctuaire / boss / recouture, icône. | 🔶 refait le 2026-11-20 ; à tester en jeu |
| 0.3 | **Santé du joueur** (`PlayerBody`) : âme qui se consume ; membres ; vie basse ressentie (`DangerFeedback`). | 🔶 à tester en jeu |
| 0.4 | **Entrées** centralisées dans une seule classe (clavier ; manette plus tard sans tout réécrire). | ✅ `GameInput` + `GameAction` |
| 0.5 | **Menus** en UI Toolkit (`GameScreens`) : titre sur scène vivante, pause, options (son, affichage), confirmations, mort, fin. | 🔶 refait le 2026-11-20 ; à tester en jeu |
| 0.6 | **Outils de debug** : F1 à F9 (soin, invincible, boss, panneau, forme, tuer le boss, sauver ici, mourir, effacer la sauvegarde). | 🔶 à tester en jeu |
| 0.7 | **Contrôle de version** : commits réguliers, une branche par phase. | 🔶 branche `phase-0` ; commits en attente |

**Décisions à prendre avant :** aucune.

**Critères de sortie :** lancer le jeu depuis le menu, mourir, réapparaître, quitter, reprendre la partie au même endroit.

---

## Phase 1 — Tranche verticale (L)

Zone 1 jouable d'un bout à l'autre : arrivée en âme, Gardien des Enfers, couture des pattes, descente avec le nouveau corps. Le détail est dans GAME_DESIGN.md, section 9 « Tranche verticale ».

| # | Tâche | Statut |
|---|---|---|
| 1.1 | **Sanctuaire** : sauvegarde, recouture, musique d'ambiance coupée, thème du boss au loin. | 🔶 codé ; à tester en jeu |
| 1.2 | **Corps** : boule de chair (vie 80, en 3 parties : ventre, poitrine, bassin) puis 2 pattes (vie 40 chacune, en 3 parties : cuisse, jarret, pied) ; le membre le plus proche du coup est touché, arraché à 0 avec ce qui y est cousu, perte visible et ressentie. | 🔶 codé ; à tester en jeu |
| 1.3 | **Visuel du corps** : pattes cousues et dépareillées sur l'âme ; état abîmé visible. | 🔶 codé ; à tester en jeu |
| 1.4 | **Membre au sol** (`LimbPickup`) et **recouture**. | 🔶 codé ; à tester en jeu |
| 1.5 | **Déplacement du corps** : course, parade de chute, dégâts de chute au-delà de 4 m. | 🔶 codé ; à tester en jeu |
| 1.6 | **Fin du Gardien** (`BossFlesh`) : chair inerte, vision du meurtre, la chair s'agglutine en boule, la zone change ; **âme marchande** (`SoulMerchant`) : 2 pattes contre un morceau de chair. | 🔶 codé ; à tester en jeu |
| 1.7 | **Absorption d'âme** : parade au contact d'un ennemi étourdi, son corps devient chair inerte. | 🔶 codé ; à tester en jeu |
| 1.8 | **Parade parfaite** : assourdissement du son après 3 parades parfaites enchaînées. | 🔶 codé ; à tester en jeu |
| 1.9 | **Mesure du comportement** (`BehaviorLedger`) : âmes absorbées, ennemis épargnés, âmes errantes bousculées. | 🔶 codé ; à tester en jeu |
| 1.10 | **Parcours** : placer les rencontres, le sanctuaire, la descente, l'écran de fin. | 🔶 parcours en blocs gris (« greybox ») ; à tester et à habiller |

**Décisions à prendre avant :**
- ⚠️ D4 : recoudre au sanctuaire, sur place, ou les deux (avant 1.4). *En attendant : les deux sont codés (spec de la tranche).*
- ✅ Vision du meurtre : 3 plans en silhouettes sur fond rouge, fondus blancs (décidé le 2026-11-13).
- ✅ Changement de la zone 1 après le boss : elle se rallume (décidé le 2026-11-13).

**Critères de sortie :** jouer du menu à l'écran de fin d'une traite, sans touche de debug, en 15 à 25 minutes.

---

## Phase 2 — Test de jeu et décision (S)

Le moment de vérifier que les deux piliers sont amusants, avant d'investir des mois dans les zones suivantes.

| # | Tâche | Statut |
|---|---|---|
| 2.1 | Faire jouer 3 à 5 personnes, sans les aider. Noter où elles meurent, ce qu'elles comprennent ou pas, ce qu'elles ressentent. | ⬜ |
| 2.2 | Régler les valeurs (vie de l'âme et des pattes, fenêtres de parade, dégâts). | ⬜ |
| 2.3 | Mesurer le temps réel passé sur la phase 1, et réestimer les phases 3 à 7. | ⬜ |
| 2.4 | Décider : on continue tel quel, on ajuste le design, ou on réduit le scope. | ⬜ |

**Critères de sortie :** une décision écrite dans GAME_DESIGN.md (section 10), et une roadmap réestimée.

---

## Phase 3 — Zone 2 : la forêt des suicidés (L)

| # | Tâche | Statut |
|---|---|---|
| 3.1 | Décor de la forêt (vert malade, arbres qui saignent), ambiance sonore. | ⬜ |
| 3.2 | Parade vers le haut et en l'air (pour les harpies). | ⬜ |
| 3.3 | Ennemis : arbres-âmes, harpies, pendus. | ⬜ |
| 3.4 | Voleur de membres : le charognard (ou le collectionneur). | ⬜ |
| 3.5 | Sanctuaire de la zone 2. | ⬜ |
| 3.6 | Boss : l'Arbre pendu, combat du sol à la cime, musique adaptative. | ⬜ |
| 3.7 | Partie du corps gagnée : la Queue (équilibre, saut mural, mouvement en l'air). | ⬜ |
| 3.8 | Changement de la zone après le boss. | ⬜ |

**Décisions à prendre avant :**
- ⚠️ Vulnérabilité liée à la Queue.
- ⚠️ Le chat se redresse-t-il sur 2 pattes à cette étape (forme finale) ?
- ⚠️ Phases et attaques de l'Arbre pendu ; sa musique.
- ⚠️ Les effets sur les membres (hémorragie, infection, hypertrophie) arrivent-ils ici ? Causes et soins.
- ⚠️ D2 : fonctionnement du sacrifice (il est dans le MVP).

**Critères de sortie :** zone 1 enchaînée avec la zone 2, sans rupture, du menu à la mort de l'Arbre pendu.

---

## Phase 4 — Zone 3 : le Tribunal des morts (L)

| # | Tâche | Statut |
|---|---|---|
| 4.1 | Décor du Tribunal (or, papier jauni, couloirs sans fin), ambiance sonore. | ⬜ |
| 4.2 | Ennemis : greffiers, file d'âmes, gardes masqués (à retravailler). | ⬜ |
| 4.3 | Sanctuaire de la zone 3. | ⬜ |
| 4.4 | Boss : le Juge (pesée du cœur, parer = refuser la sentence), musique adaptative. | ⬜ |
| 4.5 | Partie du corps gagnée : les Yeux (signal de parade plus visible, murs fragiles ; aveuglé par les flashs). | ⬜ |
| 4.6 | Changement de la zone après le boss. | ⬜ |

**Décisions à prendre avant :**
- ⚠️ Gardes masqués : leur version finale.
- ⚠️ Yeux ou griffes.
- ⚠️ Phases et attaques du Juge ; sa musique.
- ⚠️ Murs fragiles : sont-ils dans la démo (les Yeux les révèlent) ?

**Critères de sortie :** les 3 zones enchaînées, du menu à la mort du Juge.

---

## Phase 5 — Démo : finitions et sortie (M)

| # | Tâche | Statut |
|---|---|---|
| 5.1 | Vrais menus (remplacer OnGUI), options : volume, plein écran, résolution. | ⬜ |
| 5.2 | Manette. | ⬜ |
| 5.3 | Fin de la démo (écran ou petite scène). | ⬜ |
| 5.4 | Optimisation, correction des bugs, test sur une autre machine. | ⬜ |
| 5.5 | Build PC et publication de la démo (itch.io, ou page Steam). | ⬜ |
| 5.6 | Deuxième test de jeu, sur la démo entière. | ⬜ |

**Décisions à prendre avant :** ⚠️ où publier la démo, gratuite ou non.

**Critères de sortie :** démo publiée, jouable de 1 h à 1 h 30.

---

## Phase 6 — Jeu complet (L+)

À découper en détail après la démo, avec les retours des joueurs.

- Zone 4 (⚠️ à définir) et son boss.
- Zone 5 : la Tour de l'Horloge, l'Horloger, le Cœur, la **parade du temps** complète.
- Zone 6 : le seuil des vivants, le combat contre le père, le choix final et les **quatre fins**.
- Murs et sols destructibles, marché des âmes, chair de remplacement.
- Petites améliorations à trouver en explorant.

**Décisions à prendre avant :** tout ce qui reste ⚠️ dans GAME_DESIGN.md (zone 4, chemins vers chaque fin, issue du pacifisme hypocrite, musiques…).

---

## Phase 7 — Sortie PC (M)

- Page Steam (ou autre), bande-annonce, captures.
- Succès, sauvegardes dans le cloud (si Steam).
- Dernière passe de bugs et d'équilibrage.

---

## Journal

- 2026-11-22 — Niveau v2 de la zone 1 appliqué dans Main (plan `References/LevelDesign/zone1_niveau_v2.png`, sauvegarde de l'ancienne scène dans `References/Backups/`). Le niveau part de x −150 : Réveil (pics → rebond), Prairie (cracheuse qui barre le passage, colline-panorama, âme qui tombe, duo d'ennemis), Nécropole (statue au regard, guillotine), Gorge (gouffre, ancres, rafales, retour au bord), Néant (mur qui poursuit), Sanctuaire (déplacé en −18), Rive (vague), puis l'arène, l'âme marchande et la descente existantes (+ une plateforme qui cède). 11 composants dans `Scripts/Level/`. Décor prolongé à l'ouest (2e instance de SoulRealm). Chaque mécanique vérifiée en jeu par script (parade simulée). Tests : 74/74. Le parcours complet manette en main reste à faire.
- 2026-11-22 — Prologue : 5 plans rapides du sacrifice entre le coup (11) et la mort du chat (12) : 11a le bond, 11b l'impact, 11c les éclats, 11d la chute, 11e l'enfant (gros plan, larme). Cadrages serrés et penchés. `MurderVision` : entrée « !nom » = plan rapide (0,42 s, avance plus vite, secousse, éclair rouge au lieu du blanc). Images + .meta (mêmes réglages d'import) dans `Resources/Visions/`. Fait sans Unity (MCP déconnecté) : compilation et test à refaire.
- 2026-11-22 — Visions du passé refaites (retours de tsuki : formes géométriques trop visibles). Style retenu après tests (papier découpé, encre, 3D, abstrait) : affiche abstraite (père = grand bloc noir qui penche, regard qui rougit ; mère = arche crème ; enfant = petit bloc crème avec une larme ; chat = arche noire ; fonds ocre → froid → brun → rouge → noir). 13 plans : famille heureuse (1-3), la mère (4-6), la chute du père (7-10), le meurtre (11-13). Sources vectorielles `References/Flashbacks/Histoire/`, images `Assets/Resources/Visions/` (1920×1080, BC7). `MurderVision` : chapitres — prologue (11-13) à la nouvelle partie (`GameFlow.NewGame`), puis un souvenir par nouvelle partie du corps (chair du Gardien : 1-3, pattes de l'âme marchande : 4-6, boss suivants : 7-8, 9-10), vu = flag `GameFlags.Memory(n)` (sauvegardé). Lente avancée dans l'image. Vérifié en jeu (prologue). Tests : 73/73.
- 2026-11-22 — Tout se pare, suite. Logique commune `Player/RhythmChain` (testée) : parades en rythme sur des événements (fenêtre ±, raté ou parade hors rythme = chaîne rompue). `PlayerController.ParryPressed` : chaque appui (écouté par CatBreath, CatHeart, StrideParry, DeathsDoor). **Cœur** (`CatHeart`) : en danger, parer chaque battement (`DangerFeedback.Beat`) ralentit le cœur et le monde (`TimeControl.SetLayerScale("heart")`, ×0,55 au plus), l'âme ne se régénère plus ; 20 s → arrêt cardiaque. **Mort** (`DeathsDoor`) : coup fatal sur l'âme (`PlayerBody.LethalHit`) → temps figé (`"reaper"`), faucheuse géométrique, faux levée, lame qui brille, coup ; parade juste avant → 30 % d'âme ; une fois par sanctuaire (`PlayerBody.OnRested`). **Course** (`StrideParry`, `CatBody.Footstep`) : parer à chaque pose du pied, dès le 3e pas paré chaque pas pousse (+6 %, max +60 %), l'élan retombe sans rythme. `TimeControl.SetLayerScale` : ralentis durables non écrasés par l'arrêt sur image. Vérifié en jeu (Main, labo). Tests : 71/71.
- 2026-11-22 — Respiration du chat (`Player/CatBreath` + logique pure `BreathRhythm`, testée). Un souffle toutes les 2,86 s, lu par l'âme (elle gonfle) et le corps (CatBody : corps et queue), buée froide à chaque expiration. Tout se pare, même sa respiration : parer à ±0,3 s du début d'une inspiration bloque le souffle ; 30 s d'apnée parée d'affilée → mort étouffé (`PlayerBody.Kill`). Un souffle laissé passer ou une parade hors rythme rend le souffle. Apnée : poumons vides avec hoquets, l'âme tremble, voile rouge et cœur de `DangerFeedback`. Pas d'apnée hors jeu (menus). Vérifié dans Main (mort après 30 s d'apnée). Tests EditMode : 66/66.
- 2026-11-22 — Jarre, retours de tsuki. **Lancer** : un seul geste continu (`ArmPath` / `Curve` : ramasse en arc, arme au-dessus du bol, élan qui tremble, coup de fouet par-dessus l'épaule, la main s'abat au sol) par la main avant, le bol bascule en arrière puis en avant ; bras jamais tendu à fond (97 %), poignet lissé ; plus de « pose » : le temps arrêté (dt 0) remettait les mains au repos, ApplyPose est sauté. **Coquille** : paroi 0,07 → 0,12 (Blender, morceaux régénérés) ; les tessons lancés n'avaient qu'un matériau (face peinte seule, sans épaisseur) → copie pleine en un sous-maillage, FacetTextured sans texture sur les UV négatifs. Les morceaux détachés tombent, rebondissent et restent posés à plat ; en phase V il ramasse ces vrais morceaux pour les lancer (caillou s'il n'y en a plus à portée). **Déplacement** : tractions (`JarVisual.Crawl`) — la main s'étire loin et agrippe (poussière), effort (bol qui tremble, penché en arrière, chair contractée), bol traîné d'un coup en raclant, grand souffle, l'autre main rattrape ; même vitesse moyenne qu'avant. Vérifié au labo (images, 22 gestes en phases V et II sans erreur). Tests EditMode : 60/60. À juger en jeu.
- 2026-11-22 — Projectiles qui traversaient tout. `Boss/ProjectileHits` : contact joueur sur le trajet de l'image contre sa boîte de collision (`PlayerController.HitBounds`), collision avec le monde (sol, murs, objets, ennemis ; SphereCast, déclencheurs ignorés) → le projectile se brise (éclats roche / poterie, poussière, petite secousse). Renvoyé vers le boss : ignore le décor. Visée au centre du corps. Tessons lancés ramenés à la taille de leur zone de contact (trop grands : ils semblaient traverser). Rocher du bras de fer : sur la ligne corps du joueur → boss, ne casse pas contre le décor. Vérifié dans Main (sol, StartWall, ennemi, caillou paré) et au labo (échanges complets). Tests : 60/60.
- 2026-11-21 — Boss : refonte décidée. Nouveau visuel, la **Jarre aux âmes** (jarre kintsugi flottante, bras de chair, modélisée dans Blender, style à facettes), et 5 nouvelles attaques façon Jugement (bulle qui se divise, échange truqué, double Jugement, bras de fer, aspiration). Première étape faite : piliers corrigés (ils étaient impossibles à esquiver et, en phase IV-V, à parer). Motif haut / bas (se saute) / trou sûr ; une parade neutralise toute la salve (piliers restants en bleu) ; marque qui se remplit puis flashe au jaillissement ; phase IV+ : deux lignes qui convergent en un pilier unique sur le joueur. À tester en jeu. Tests EditMode : 60/60.
  Deuxième étape faite : les 5 échanges (`RallyBubble` : division, double, truqué ; `PushBubble` : bras de fer ; aspiration dans `HellGuardian`, traction via `PlayerController.SetExternalPull` / `Knockback`). Répartition : Jugement dès la phase II, division III, double Jugement + aspiration IV, truqué + bras de fer V. Boutons dans `AnimationLab`. Vérifiés par script en mode Play avec une parade forcée (7, 6, 5 et 4 parades, aspiration : traction, à-coups parés, avalé si on reste immobile). Ressenti et réglages à tester en jeu.
  Troisième étape : modèle Blender de la Jarre (`Tools/Blender/jar_build.py` → `Assets/Models/Jar/jar.blend`, rendus dans `References/Boss/`). Après retours de tsuki : bol / chaudron rempli de chair (plus de liquide, de visage ni de fausses fissures), paroi découpée en vrais morceaux épais (`Frag_P<n>_<k>` : fentes en phase II, chute en phase III, IV, V), anses rattachées à leur morceau, 2 longs bras humains maigres enracinés dans la chair (`UpperArm`, `Forearm`, `Hand`) qui traînent le bol au sol (+2 en phase IV). Gameplay décidé par tsuki : il rampe avec ses bras, tape le sol, lance des cailloux, puis les morceaux du bol en phase V. Échanges (Jugement, division, double, truqué, bras de fer) = cailloux qu'il rattrape et relance ; piliers = pics de roche quand il frappe le sol ; charge = petit bond propulsé par les bras ; aspiration supprimée. Puis : 2 bras seulement (pas de membres en plus), bol grec antique resté sous l'eau (texture `jar_pottery.png` : vernis noir, rayons, vagues, grecque, languettes, délavé, croûtes, algues, balanes), fichier modifiable à la main (`jar_build.py` une seule fois, `jar_fracture.py` refait les morceaux depuis `Bowl`, `jar_render.py` pour les rendus). Modèle validé dans son principe.
  Quatrième étape : import dans le jeu. `JarVisual` (instance de jar.blend, tournée d'un demi-tour) remplace tout le visuel du Gardien : bol entier en phase I, morceaux disjoints en phase II, chute des morceaux (et de leur anse) à leur phase, chair qui respire, flash quand il est touché. Bol posé au sol (`hoverHeight` 0,95). Matériaux `Materials/Boss/Jar_*` (shader `Custom/FacetTextured` pour la poterie, `FleshFacet` pour chair et bras), remappés dans l'import du modèle. Les attaques restent celles du Gardien, avec des repères invisibles (mains, faux) : à refaire (ramper, frapper le sol, lancer des cailloux). Vérifié en mode Play au labo (phases dans les deux sens, gestes, mort : 0 erreur). Tests EditMode : 60/60.
  Cinquième étape : combat de la Jarre. Bras pilotés par cinématique inverse (`JarVisual` : mains plantées au sol qui se replantent quand le corps avance = il rampe ; une attaque prend la main sur un bras). Attaques refaites : frappe d'une main (combo 2/3, onde de choc en II+), cailloux ramassés puis lancés, écrasement à deux mains (ondes), pics de roche, pluie de pierres lancées vers le ciel, bond propulsé par les bras (2 en V), Jugement et échanges avec cailloux rattrapés et relancés, bras de fer = rocher poussé ; en phase V, il lance les morceaux de son bol. Apparence des projectiles : `ProjectileLook` (roche / tesson). Supprimés : faux, fente, aspiration (`SetExternalPull`). Vérifié au labo par une séquence scriptée de tous les gestes (0 erreur, échanges complets avec parade forcée). Tests EditMode : 60/60. À régler en jouant (timings, portées, lisibilité des signaux).

- 2026-11-20 — Phase 0 reprise pour la qualité (retour de tsuki : « rien de pro »). Menus OnGUI remplacés par UI Toolkit (`UI/GameScreens`, `Assets/UI/GameScreens.uxml/.uss`, `GamePanel.asset`) : titre sur la scène vivante (caméra de titre dans `GameCamera` : décalage + dérive, glisse vers le jeu en lançant), pause, options (`GameSettings` : volumes général / musique et ambiance / effets, plein écran, résolution, vsync ; PlayerPrefs), confirmations, fin ; navigation clavier + souris, sons synthétisés (`UiSounds`). `GameFlow` refait : fondus (`ScreenFade`, statique, l'écran reste noir pendant le chargement asynchrone), mort en mise en scène (ralenti, braises, mot « CONSUMÉ » qui se dissout, passable), état `Waking` (réveil au sanctuaire, lueur). Sauvegarde : copie `.bak` relue si la sauvegarde est abîmée, nom du lieu (`placeName`) affiché sur Continuer, écrite aussi à la recouture, icône d'enregistrement. Vie basse : `ScreenMood.Danger` (vignette rouge qui bat) + cœur (`DangerFeedback`). Debug F7 sauver ici, F8 mourir, F9 effacer. Coût de la mort décidé (provisoire) : modèle souls, voir GAME_DESIGN. Tests EditMode : 60/60.

- 2026-11-15 — Scripts Blender à lancer dans le fichier ouvert (`Tools/Blender/`) : `cat_tail.py` (queue fine, rayon 0,06 → 0,035), `cat_ear_pivots.py` (origine des oreilles à leur base, côté crâne ; maillage partagé avec le miroir traité une seule fois), `cat_ear_fix.py` (remet Ear_R à sa place de `Cat.fbx`), `cat_paw.py` (vraie patte : paume, 4 doigts pliés vers le corps, griffes claires ; ancienne main retirée). Export Unity : origine des oreilles à leur base, avant-bras = morceau le plus long.
- 2026-11-15 — `cat_tail.py` refait chaque segment de queue (tube effilé 8 côtés + demi-boule arrière de même rayon, bout arrondi, couleur reprise) : plus de pointes aux articulations (l'ancien affinage avait écrasé les boules en fuseaux). `cat_straighten.py` : torse à 12° (au lieu de 22), tête/bras/jambes gardent leur orientation, hanches à la même hauteur.
- 2026-11-15 — Yeux modelables dans Blender : `cat_eyes.py` crée `Eye_R` (globe + pupille, couleurs des sommets) au repère Eye, `Eye_L` en miroir (`cat_mirror.py`, paires de la tête). Import Unity : objets `Eye_…` de la tête ajoutés au sous-mesh lumineux à la place des yeux générés (truffe toujours générée).
- 2026-11-15 — `cat_leg_pivots.py` : hanche (origine de Leg) recollée à la jambe quand Thigh/Shin/Foot ont été déplacés ensemble dans Leg (pivot resté ~8 cm à l'extérieur de la cuisse) ; Knee/Ankle remis dans le plan de la jambe.
- 2026-11-15 — Hanche : le haut de la cuisse (jusqu'à 30 cm au-dessus du pivot, fesse) tournait avec la patte et ressortait du torse en marchant. Os `HipAnchor` (prefab CatLeg, 4e os) : poids du haut de cuisse dessus (pli centré sur la hanche, largeur 0,5 × cuisse), CatBody lui donne la rotation inverse de la cuisse.
- 2026-11-15 — Sensation de vitesse : le torse penche vers l'avant selon l'allure (`walkLean` 4°, `runLean` 16°) et l'accélération (`accelLean`, en arrière au freinage, limites `maxLean` 26° / `maxBrakeLean` 10°), lissé (`leanResponse`) ; tête et bras compensent comme avant.
- 2026-11-15 — AnimationLab : Immobile/Marche/Course (aller-retour) remplacés par un curseur d'allure 0 → 20 m/s sur place (`CatBody.TreadmillSpeed` : vitesse d'animation imposée, le chat ne bouge pas). Penché en plus au-delà de la course (`overspeedLean` 1°/m/s, limite `maxLean`).
- 2026-11-15 — Bras refaits : balancier calé sur la patte opposée (était décalé d'un quart de cycle : `sin` au lieu de la position réelle du pied, qui dépend de `Duty`), calculé sans retard (avant : lissé, rétrécissait à grande allure), amplitude marche 16° / course 30° / sprint 40°, recul ×0,65, coude plié en courant (75°) qui se ferme un peu en avançant ; air et parade mélangés en douceur (`armPose`).
- 2026-11-15 — Bras en course : style « bras en arrière » choisi (3 styles comparés, `References/Styles/bras_course.png`) : en courant, bras rejetés derrière (`runArmForward` −70°), presque tendus (`runElbow` 12°), qui suivent le torse penché (`runArmHang` 0), balancier ±6–8° ; la marche ne change pas, passage progressif avec `runWeight`.
- 2026-11-15 — Épaule : l'origine de UpperArm était au milieu du bras (le haut du bras monte 22 cm au-dessus) : le bras tournait par son milieu et ressortait du torse. Essai avec un os d'ancrage (comme la hanche) abandonné : le bras se pliait en son milieu. Solution : bras rigide, pivot recalé à l'import au centre des 40 % du haut du bras (`ShoulderJoint`), attache et coude décalés d'autant (pose inchangée).
- 2026-11-15 — Bras en course ballants : plus de pose tenue, chaque bras est un pendule amorti poussé en arrière par le vent (`armDrag` × vitesse² + accélération, contre la gravité ; ≈ 48° à 6 m/s, 72° à 10 m/s, limite `maxArmTrail`), qui flotte avec les rebonds du corps (`armBounce`) et le vent (`armFlutter`) ; coude relâché (`limpElbow`) dont l'avant-bras traîne (`elbowFlop`). Marche inchangée.
- 2026-11-15 — Une patte : il sautille. Un bond par cycle (`hopRateSlow` 1,9 → `hopRateFast` 3,2 bonds/s selon la vitesse) : appui court (`hopDuty` 0,35) où il s'accroupit (`hopSquat`) et penche en avant en poussant (`hopTilt`), puis vol en parabole (hauteur g·t²/8 × `hopHeight` 2,5, ≈ 25 cm à 2 m/s), pied qui quitte le sol, se replie (`hopTuck`) et revient se poser devant ; écrasement à chaque réception (`hopImpact`). Bras ballants secoués par les bonds. Remplace `oneLegHop`.
- 2026-11-15 — Boule (torse sans pattes) : roulait comme une sphère (flottait à plat, s'enfonçait debout). Roule maintenant sur son contour de profil (enveloppe convexe des sommets, `ProfileOutline`) : point le plus bas posé sur le sol, rotation = distance / rayon actuel (`RollRadius`) : bascule vite sur le flanc, ralentit sur les bouts. Collision du joueur inchangée (boîte).
- 2026-11-15 — HUD du corps (`Player/BodyHud`, sur le joueur de Main et AnimationLab) : silhouette de face en haut à droite, un morceau par membre (tête, poitrine, ventre, bassin, bras, cuisses, jarrets, pieds, queue), couleur selon la vie, flash blanc au coup, gris si arraché, pastille d'effet. Rien en âme. Membres en polygones aux coins arrondis (tête à oreilles, poitrine large aux épaules, bassin aux hanches, bras/pattes qui s'affinent, pied, queue effilée), textures faites à la taille exacte à l'écran. Membres séparés (vide ≥ 4 unités, plus que les contours), sans rotation, queue pendante à côté de la patte loin, pastille d'effet au milieu du membre : rien ne se chevauche. Capture : `Assets/Screenshots/body_hud_zoom-1.png`.
- 2026-11-16 — Bras en 3 membres (haut du bras `Arm`, avant-bras `Forearm`, main `Hand`, nouveaux `LimbKind` en fin ; vie 14 / 10 / 6 pour 30) : arracher le haut du bras emporte tout, l'avant-bras emporte la main. Maillages séparés (`CatForearm`, `CatHand` = objet Hand du .blend), prefab CatArm `Elbow/Fore` + `Elbow/Hand`, rendus par `PartOf`. Sauvegarde v3 (migration : chaque bras découpé, même part de vie et effet). HUD : 3 morceaux par bras. Labo : « Bras » ajoute / retire les 3. Tests : 53/53 (+ main seule, bras entier, migration v2). ⚠️ effet de la main perdue à décider.
- 2026-11-16 — Rampe (sans pattes, avec la tête) : torse couché à 90° (point bas au sol), tête relevée et bras qui compensent via `torsoTilt`. Avec bras : main posée au sol par IK à 2 os (coude vers le haut), plantée pendant la traction (recule à la vitesse du sol), repart devant en arc ; bras en alternance ; sphinx à l'arrêt. Sans bras : ondulation de ver (`wormWave`, `wormLift`). Sans tête : roule comme avant. Vitesse et petit bond : ⚠️ à faire (animation seulement).
- 2026-11-16 — Rampe corrigée (les bras traversaient le corps : main ramenée trop près de l'épaule, coude vers le haut) : la main se pose loin devant (`crawlHandFront` 0,75 m, bras presque tendu), est ramenée vers le corps pendant la traction (avant-bras au sol, coude vers le bas) ; l'avant du corps monte pendant qu'un bras tire (`crawlChestLift`, torse qui se relève) et redescend quand il tend l'autre ; bras en alternance exacte (`crawlReach` 0,5).
- 2026-11-15 — Côté loin en vrai miroir (forme retournée) : copies `Far_*` dans Blender (`Tools/Blender/cat_mirror.py`, vérifié sommet par sommet) et bras / patte du côté loin en jeu (échelle −1 en profondeur).
- 2026-11-15 — Pose du chat définie dans Blender : `cat.blend` montre le chat debout immobile comme en jeu (sol `Ground`, côté loin `Far_*` en miroir par pilotes). Unity y lit hauteur et inclinaison du torse, attaches (origines des pièces), écart des pattes, place de la cheville, rotations de repos de la tête, des bras, du coude, de la queue ; CatBody ajoute les mouvements par-dessus. Réglages retirés de CatBody (`standingBallHeight`, `standingTilt`, `legSplay`, `ankleHeight`, `elbowBend`, `tailLift`, `tailCurl`) ; valeurs du modèle généré dans `CatModelSettings`. L'export refait `cat.blend` via Blender en arrière-plan (sauvegarde dans `References/Backups/`). Pattes plus écartées (hanche z 0,23). Tests EditMode : 50/50.
- 2026-11-15 — Export Blender découpé en morceaux : torse (Chest, Belly, Pelvis), tête (Skull, Ear_R, Ear_L), avant-bras (Forearm, Hand), patte (Thigh, Shin, Foot). L'export part du modèle actuel (le .blend s'il est utilisé). Au retour, tout objet rangé sous une pièce est recollé avec elle.

- 2026-11-15 — Torse et pattes en 3 parties, une vie chacune (GAME_DESIGN 6.4) : ventre / poitrine / bassin (`LimbKind.Chest`, `Pelvis`), cuisse / jarret / pied (`Leg`, `Shin`, `Foot`). Chaque membre connaît son porteur (`Limb.ParentId`) : arraché, il emporte ce qui y est cousu ; recousu seulement si son porteur tient. Patte debout = cuisse + jarret ; pied perdu = boite (`missingFootSpeedFactor` 0,8 par pied). Sauvegarde version 2 (migration des anciennes). Modèles découpés à la construction (torse par hauteur : `chestSplit`, `pelvisSplit` ; patte par os), y compris depuis Blender. Labo : bouton ✕ pour arracher une partie. Tests EditMode : 50/50.

- 2026-11-15 — Chat modifiable dans Blender. Export : menu *Parry Everything > Exporter le chat vers Blender* → `Assets/Models/Cat/Cat.fbx` (FBX binaire, paquet `com.unity.formats.fbx`), une pièce par objet, origines sur les articulations, repères vides (Neck, Shoulder, TailBase, Hip, Elbow, Knee, Ankle, Eye, Nose), couleur des sommets = fourrure. Retour : `CatModelSettings.source = Blender` + `blenderModel` (`Assets/Models/Cat/cat.blend`) ; chaque enregistrement du .blend reconstruit le chat (`CatBlenderWatcher`). Unity refait chair, plaques, veines, yeux et truffe, poids de la patte (`CatBlenderImport`). Touffes retirées.

- 2026-11-15 — 3 styles de rendu du chat à comparer (réglage `style` de `CatModelSettings`) : fourrure douce (`FleshFur`, poils en couches), toon (`FleshToon`, aplats + contour), low-poly facetté (`FleshFacet`). Calcul de l'état des membres partagé (`FleshCore.cginc`). Style actuel inchangé (`Smooth`) en attendant le choix. **Choix : low-poly** (`style = Facets`) : maillages moins détaillés, une couleur et une normale par triangle (`FlattenFaces`), lumière d'appoint par face (`_FacetKey`) pour rester lisible dans les scènes sombres. Captures : `References/Styles/lowpoly_*.png`.

- 2026-11-15 — Tête, bras et queue refaits d'après `References/chat_fourrure.png` (couleurs gardées). Fourrure en relief : touffes (`MeshDraft.AddTuft`). Tête : museau court, grandes oreilles triangulaires à l'intérieur clair et touffes, collerette des joues, mèches du front, yeux en amande. Bras : plus fins et longs, touffe au coude, manchette au poignet, main à 4 doigts griffus. Queue : grosse et touffue (`tailRadius` + courbe `tailWidth`, touffes en couronne, mèche au bout), **plus de flamme**. Torse : collerette claire de deux rangs. Tout réglable dans `CatModelSettings`.

- 2026-11-14 — Chat debout : le torse ne bascule plus d'avant en arrière à chaque pas. Au sol il ne tourne plus du tout (angle fixe, ni bascule ni penché selon la vitesse) : il monte et descend seulement ; les épaules avancent avec leur bras (`shoulderSwing`). Pattes plus grandes (`legLength` 2 → 2,7, `legThickness` 1,7 → 2 ; corps porté plus haut, pas plus longs). Bras plus calmes : pendent droits, balancier réduit (12° marche, 25° course), coude moins plié.

- 2026-11-14 — Chat complet, visuel seulement (capacités ⚠️ en 3.7 et suivants) : nouveaux membres `LimbKind.Arm/Head/Tail` (`Limb.CatArms/CatHead/CatTail`), cousus sur la chair ; la chair qui éclate fait tomber tous les membres. Modèles générés : tête (oreilles, joues, yeux lumineux), bras articulés au coude avec griffes, queue en segments avec flamme ; torse en œuf, points d'attache dans `CatTorsoRig`, repères de la patte dans `CatLegRig`. Tous les réglages de forme dans `Meshes/Player/CatModelSettings.asset` (reconstruction auto). Apparence selon l'état (shader `FleshLit`) : fourrure en bonne santé, qui part par plaques sur la chair cousue, puis à vif ; hémorragie (rouge pulsé, gouttes), infection (veines violacées), hypertrophie (gonfle). Animation : tête qui compense l'inclinaison, bras en balancier / levés pour parer, queue qui ondule et traîne. Scène `AnimationLab` (pause, ralenti, chaque geste du boss, chat pièce par pièce, vie et effet par membre). Tests EditMode : 42/42.

- 2026-11-13 — Boule : elle roule enfin au sol (sa vitesse de roulement est gardée à part, le frottement l'annulait). Marche debout refaite sur un cycle (pied d'appui collé au sol, retour en arc, pattes en opposition, corps qui monte/bascule avec le cycle, démarrage/arrêt fondus) : plus de saccades.

- 2026-11-13 — Marche du chat debout refaite : pieds plantés au sol, pas en arc alternés (une patte à la fois), genou par cinématique inverse, durée des pas calée sur la portée des pattes ; le corps monte à chaque pas, s'écrase à l'impact, bascule et respire à l'arrêt. Debout plus lent (marche 3,5, course 6) pour des pas réalistes (~2,6 pas/s par patte). `GameInput.MoveOverride` pour piloter le joueur en test.

- 2026-11-13 — Vrais modèles 3D du corps (`CatModelBuilder`, shader `Custom/FleshLit` à couleur par sommet) : boule de chair bosselée et cousue qui recouvre entièrement l'âme, créature-chat debout (yeux jaunes, queue de flamme, bras), patte digitigrade articulée au genou. `CatBody` utilise ces prefabs, flashe à la parade, lumière douce sur le corps. Captures refaites dans `References/Evolution/`.

- 2026-11-13 — Retours de tsuki : textes noirs sur noir ou coupés (corrigé dans `GuiDraw` : couleur restaurée, tailles adaptées à l'écran). Nouvelle évolution : âme → boule de chair qui roule (`PlayerForm.Ball`, inertie) → debout sur 2 pattes (`PlayerForm.Biped`, l'âme fait la tête) grâce à l'âme marchande (`SoulMerchant`, bulle sans texte), contre un morceau du tas de chair du Gardien (`GameFlags`). Testé en jeu par script, de la mort du boss à l'échange. Captures : `References/Evolution/`. Tests EditMode : 38/38.

- 2026-11-13 — Phase 1 codée (non testée en jeu, à part un test scripté de bout en bout). Corps à 4 pattes (`Limb.CatLegs`, vie 40, coup sur la patte la plus proche), stats âme/corps dans `PlayerController` (âme 3,5 et flottante, corps 5, course 8 sur Maj, pénalités selon les pattes), `CatBody` (pattes dépareillées, couture, chair à vif, tremblement), `LimbPickup` + recouture (↓ maintenu 1,5 s, ou au sanctuaire), parade de chute (> 4 m, parade 0,15 s avant l'impact), `Sanctuary` (repos, sauvegarde, vent coupé, thème du boss au loin), `BossFlesh` + `MurderVision` (3 plans) + `Atmosphere.Rekindle`, absorption d'âme (`ISoulVessel`, `MeleeEnemy`), parade parfaite (`PerfectParryChain`, `MuffledWorld` : passe-bas, −1 demi-ton, cœur), `BehaviorLedger`, `GameSession` (état rechargé à la mort / Continuer). Parcours : départ x = −50, tutoriel, 3 rencontres, 2 âmes sur le chemin, sanctuaire x = −3, Gardien, barrière qui s'ouvre après le boss, descente en 4 paliers (dont une chute de 5,5 m), 2 rencontres, fin « À suivre ». Debug : F6 tue le boss. Tests EditMode : 35/35.

- 2026-11-13 — 0.3 à 0.6 codés. Entrées : `GameInput` (seul à lire le clavier ; flèches + Espace, X/J parer). Écrans : `GameMenus` (titre avec commandes, pause, fondu de mort, fin). Debug : `DebugTools` (F1 soin, F2 invincible, F3 boss, F4 panneau âme/membres/fenêtres de parade, F5 âme ↔ 4 pattes de test), actif seulement dans l'éditeur et les builds de dev. Membres : `Limb` (vie, effet, arrachage), `PlayerBody` (coups sur les membres tant qu'il en reste, puis sur l'âme). Tests EditMode : 25/25. Reste : test en jeu du critère de sortie, commits.

- 2026-11-13 — 0.2 codé : `SaveSystem` + `SaveData` (JSON versionné dans `persistentDataPath/save.json`, écriture via fichier temporaire, migration prévue). `Checkpoint` provisoire (déclencheur invisible à x = 3 dans `Main`) : enregistre le point de réapparition et sauvegarde. Menu : continuer / nouvelle partie / quitter. Tests EditMode : 16/16.

- 2026-11-13 — 0.1 codé : `GameFlow` (objet dans `Main`), `GameStateMachine` + `GameState`, `TimeControl` (seul à écrire `Time.timeScale`, la pause passe avant les ralentis). Réapparition : rechargement de la scène au dernier point (`GameFlow.SetRespawnPoint`, départ de scène par défaut). Écrans provisoires OnGUI : titre, pause, fin. Tests EditMode : 9/9.

- 2026-11-13 — Outillage : Test Framework 1.6.0, asmdefs (jeu, éditeur, tests EditMode), `unity-mcp` épinglé sur le commit 81542a7 (v10.3.0), `.gitattributes` (YAML Unity + LFS), skill projet `.claude/skills/unity-dev`. Tests `PlayerBody` : 3/3.
- 2026-11-13 — Roadmap créée. Étape 0.3 commencée en avance : l'âme qui se consume est codée (non testée, non commitée).
