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
| 0.1 | **Flux de jeu** (`GameFlow`) : états menu, jeu, pause, mort, fin ; réapparition. | 🔶 codé, testé en EditMode ; à tester en jeu |
| 0.2 | **Sauvegarde** (`SaveSystem`) : fichier JSON, versionné (pour changer le format sans casser les anciennes sauvegardes). | 🔶 codé, testé en EditMode ; à tester en jeu |
| 0.3 | **Santé du joueur** (`PlayerBody`) : âme qui se consume et rétrécit ; structure prête pour les membres (vie par membre, emplacement d'effet). | 🔶 âme + structure des membres (`Limb`, `LimbEffect`) codées, testées en EditMode ; à tester en jeu |
| 0.4 | **Entrées** centralisées dans une seule classe (clavier ; manette plus tard sans tout réécrire). | ✅ `GameInput` + `GameAction` |
| 0.5 | **Menus de base** : titre, pause, écran de fin (OnGUI via `GuiDraw`, provisoire). | 🔶 `GameMenus` codé ; à tester en jeu |
| 0.6 | **Outils de debug** : touches pour se soigner, changer d'état (âme / corps), sauter au boss, afficher les fenêtres de parade. | 🔶 `DebugTools` (F1 à F5) codé ; à tester en jeu |
| 0.7 | **Contrôle de version** : commits réguliers, une branche par phase. | 🔶 branche `phase-0` ; commits en attente |

**Décisions à prendre avant :** aucune.

**Critères de sortie :** lancer le jeu depuis le menu, mourir, réapparaître, quitter, reprendre la partie au même endroit.

---

## Phase 1 — Tranche verticale (L)

Zone 1 jouable d'un bout à l'autre : arrivée en âme, Gardien des Enfers, couture des pattes, descente avec le nouveau corps. Le détail est dans GAME_DESIGN.md, section 9 « Tranche verticale ».

| # | Tâche | Statut |
|---|---|---|
| 1.1 | **Sanctuaire** : sauvegarde, recouture, musique d'ambiance coupée, thème du boss au loin. | 🔶 codé ; à tester en jeu |
| 1.2 | **Corps** : boule de chair (vie 80) puis 2 pattes (vie 40 chacune) ; le membre le plus proche du coup est touché, arraché à 0, perte visible et ressentie. | 🔶 codé ; à tester en jeu |
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
