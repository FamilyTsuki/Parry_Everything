# Parry Everything — Document de game design

> **Copie locale.** La version de référence est le doc Claude : https://claude.ai/artifact/7ywaEeSmEjZvzwcMMQYqXS
> Modifier le doc en priorité ; ce fichier est remis à jour depuis le doc sur demande.

Document vivant : on y met toutes les idées, puis on les trie pour définir le jeu et son scope.
Ce qui est marqué *(proposition)* vient de Claude et attend ta validation.
Ton premier jet d'origine est conservé tel quel en annexe, en bas du fichier.

---

## 1. Pitch

**Titre :** Parry Everything
**Genre :** action-plateforme 2D (rendu 3D low-poly), combats de boss, progression par évolution du personnage.
**Cadre :** projet solo, sortie PC.

**Pitch** *(proposition)* : un chat tué injustement se réveille en enfer, réduit à une âme fragile. Pour se venger, il traverse des zones très différentes en parant tout ce qui le menace : les coups, les chutes, les explosions, et jusqu'au temps lui-même. Au fil du voyage, il se reconstruit un corps : de nouvelles capacités, mais aussi de nouvelles façons d'être blessé.

## 2. Piliers

1. **Tout se pare.** Les attaques, mais aussi les dégâts de chute, les explosions, et même le temps. La parade est l'action centrale du jeu.
2. **L'âme se reconstruit un corps.** Le personnage commence en âme lente et molle, puis récupère un corps. Chaque étape apporte des capacités (saut mural, course, parade plus rapide) **et** de nouvelles vulnérabilités.
3. **Des boss épiques.** De nombreux boss, chacun avec sa propre musique épique et adaptative.
4. **Des lieux très différents.** Chaque zone a son identité visuelle, sonore et mécanique.

## 3. Expérience visée

- **Ressenti principal :** ⚠️ à définir
- **Durée totale :** ⚠️ à définir
- **Difficulté :** exigeante mais juste. Chaque attaque est annoncée par un signal clair à parer. Mourir coûte peu grâce aux sanctuaires, mais un combat se gagne par la maîtrise.
- **Ton :** sombre, avec un côté gore assumé (perte de membres).

---

## 4. Histoire

Un chat se fait tuer injustement et finit en enfer. Son objectif : **se venger**, tout en découvrant les lieux qu'il traverse.

**Validé — base mythologique : le bakeneko / nekomata (Japon).** Dans les légendes japonaises, un chat peut devenir un esprit (yokai) après une mort injuste, et revenir se venger. Le chat du jeu s'inscrit dans cette tradition. Piste à creuser : la queue du nekomata se fend en deux, ce qui pourrait marquer une étape de l'évolution.

**Validé — le dernier boss est le tueur, encore vivant.** Arrivé au bout de l'enfer, le chat doit arrêter le temps pour franchir la frontière avec le monde des vivants. La parade du temps, sommet de l'évolution, est aussi la clé de la fin.

**Validé — le meurtre.** Le chat a vu grandir un enfant. Le père de cet enfant est devenu violent après une suite de malheurs (la mort de sa femme, puis l'alcool). Un jour, le chat s'est interposé pour défendre l'enfant, et le père l'a tué. Le chat s'est sacrifié.
- **Le tueur est donc le père**, toujours vivant : c'est le dernier boss.
- ⚠️ Sujets lourds (deuil, alcoolisme, violence envers un enfant) : à traiter avec retenue, par le décor et les visions plutôt que montrés frontalement.

**Validé — le retour chez les vivants.** Quand le chat remonte, l'enfant est en mauvais état, mais vivant, et sur le point d'être frappé très fort. Cette fois, le chat se bat avec force. La première fois, il pensait que la violence n'était pas une solution : il s'était contenté de feuler et de se gonfler.

**Validé — trois fins, au choix du joueur :**
1. **Pacifisme.** Le chat refuse de se battre. Ce choix l'expose à un retour en enfer.
2. **Combat, puis épargner le père**, en pensant à l'avenir de l'enfant.
3. **Combat, puis tuer le père**, pour les mêmes raisons : l'avenir de l'enfant.

Les trois choix partent du même souci (protéger l'enfant). Aucun n'est présenté comme « le bon ».

**Validé — quatre fins :**

| Fin | Ce qui se passe | Choix qui y mène |
|---|---|---|
| **Sacrifice** | le chat se sacrifie, l'enfant est sauvé | ⚠️ à relier (pacifisme ?) |
| **Le père meurt** | le chat tue le père | combat, puis tuer |
| **Tout le monde est sauvé** | le père prend conscience de ses actes ; personne ne meurt | ⚠️ à relier (pacifisme d'un joueur resté bon ? épargner ?) |
| **Mauvaise fin** | l'enfant meurt | le chat ne cherche plus que sa vengeance et **oublie l'enfant** (il attaque le père alors que l'enfant est encore en danger, au lieu de le protéger d'abord) ; ou le joueur **échoue** (perd le combat final, ou met trop de temps à choisir pendant le ralenti) |

*(remarque)* Le jeu lui-même raconte ce dilemme : le chat ne blesse qu'en parant, il ne frappe jamais le premier. Pendant tout le jeu, sa force reste défensive. La fin pose la question de franchir cette limite.

**Validé — le pacifisme dépend du parcours du joueur.** L'issue du choix pacifiste dépend de la façon dont le joueur s'est comporté pendant tout le jeu :
- s'il est **resté bon**, le père peut prendre conscience de ses actes ;
- s'il a fait preuve d'**hypocrisie** (violent pendant le jeu, pacifiste seulement à la fin), l'issue est différente ⚠️ (à définir).

**Validé — mesurer « bon » ou « hypocrite ».**
- **Épargner ou achever** les ennemis parés et étourdis : ça compte, mais ce ne doit pas être la seule mesure, sinon ça n'a pas de sens.
- **Les âmes errantes** : les bousculer ou les écraser compte comme de la cruauté ; les contourner, non.
- **L'hypocrisie, c'est l'écart** entre le comportement pendant le jeu et le choix final. Elle ne force pas la mauvaise fin : elle rend le **combat final bien plus dur**.

**Validé — la présentation du choix.** Pas de texte. Le temps ralentit, et deux boutons apparaissent à l'écran : **parer**, du côté de l'enfant, et **attaquer**, du côté du père.

*(remarque)* Le chat n'a pas de bouton d'attaque pendant tout le jeu : il ne blesse qu'en parant. Ce bouton « attaquer » serait donc le premier de toute la partie, ce qui rend le choix très fort.

Questions ouvertes :
- *(plus tard, pas prioritaire)* D'autres mesures du comportement, en plus d'épargner ou achever, et des âmes errantes ?
- Quel chemin exact mène à chaque fin (voir le tableau des quatre fins) ?

**Piste retenue — les boss sont les douleurs du père.** Chaque boss incarne ce qui a brisé le père : le deuil (l'Arbre pendu), la culpabilité et le jugement (le Juge), le temps perdu (l'Horloger)… Le chat traverse ce qui a mené à son meurtre, sans que les boss soient de vraies personnes du passé.

**Validé — pas de texte, ou presque.** L'histoire passe par le décor, les silhouettes, la chair des boss, et par des visions du meurtre au moment de recoudre un nouveau membre : on revoit la mort du chat par fragments.

## 5. Structure du monde

**Zones linéaires** : une suite de zones, chacune se termine par un boss. On avance sans revenir en arrière.

**Validé — une descente par étapes.** Comme les 9 cercles de l'Enfer de Dante ou les 9 niveaux du Mictlan aztèque, le voyage est une descente : chaque zone est une épreuve, plus profonde que la précédente.

| # | Zone | Ambiance | Couleur | Boss | Statut |
|---|---|---|---|---|---|
| 1 | L'enfer éteint (Royaume des âmes) | froid, triste, malaisant ; arrivée du chat en âme | bleu froid | Gardien des Enfers | en cours |
| 2 | La forêt des suicidés (Dante) | verdure malade ; arbres qui saignent ; harpies dans le ciel (parer vers le haut et en l'air) | vert malade | l'Arbre pendu | zone validée, place proposée |
| 3 | Le Tribunal des morts (Diyu chinois + pesée du cœur égyptienne) | ville administrative de l'enfer : couloirs sans fin, registres, juges masqués, files d'âmes ; absurde et oppressant | or et papier jauni | le Juge | validé |
| 4 | ⚠️ à définir | ⚠️ (piste : la zone des explosions) | | ⚠️ | idée |
| 5 | La Tour de l'Horloge | mécanisme immense, rythme imposé ; on y découvre la parade du temps | cuivre et laiton | ⚠️ | validé (fin de jeu, pas la dernière) |
| 6 | Le seuil des vivants | le lieu réel du meurtre (maison, rue), vu depuis la mort : figé, décoloré, objets du quotidien immenses | couleurs délavées | le tueur | proposé |

**Validé — 6 zones au total**, dont **3 dans la démo** (zones 1, 2 et 3). La zone 4 reste à définir.

- **Validé — zone de verdure : la forêt des suicidés** (Enfer de Dante). Une forêt dont les arbres saignent et parlent quand on casse une branche, habitée par des harpies. Verte, gore et triste. ⚠️ Place dans le voyage, ennemis et boss à définir.
- **Tour de l'Horloge :** contient l'horloge qui fait découvrir la parade du temps (voir 6.2). Zone de fin de jeu, mais **pas la zone finale**.

**Validé — un sanctuaire avant chaque boss.** Un lieu calme, sans ennemi, où l'on recoud ses membres. La musique d'ambiance s'arrête et l'on entend déjà, au loin, le thème du boss. Il sert de point de sauvegarde et marque une pause entre la tension et l'épique.

**Validé — la zone réagit à la mort du boss.** Une fois le boss vaincu, l'ambiance de sa zone change pour de bon (par exemple, l'enfer éteint se rallume ou s'effondre). Le monde garde la trace du passage du chat.

## 6. Mécaniques

### 6.1 La parade (pilier 1)

| Ce qu'on pare | Effet | Statut |
|---|---|---|
| Attaques des ennemis | aucun dégât ; selon l'ennemi : étourdi, renvoyé, tué | existe |
| Projectiles du boss | renvoyés ou brisés | existe |
| Dégâts de chute | atterrissage sans dégât (parade au moment de l'impact) | idée |
| Explosions | ⚠️ effet à définir (souffle annulé ? renvoyé ?) | idée |
| Le temps | voir 6.2 | idée |

**Validé :** les dégâts de chute n'existent que quand le personnage a un corps : l'âme flotte. La parade de chute devient donc utile **grâce** à l'évolution, et relie les deux piliers.

### 6.2 Parer le temps

- Utilisable **à tout moment**, à condition de parer assez vite et assez fort.
- **Découverte : l'horloge.** L'horloge n'est pas partout. C'est un élément d'**une zone de fin de jeu**, qui fait découvrir la mécanique : une grande horloge dont l'aiguille se repousse à chaque parade. Une fois la mécanique comprise, elle s'utilise partout, sans horloge.
- Au bout de **3 parades au timing parfait**, le temps commence à être affecté :
  1. le temps s'arrête 0,1 s ;
  2. puis 0,2 s ;
  3. puis des tremblements ;
  4. au bout d'un moment, le temps s'arrête complètement.

**Validé :** comme il faut parer vite, l'âme molle du début ne peut pas y arriver. La parade du temps devient possible naturellement quand le corps permet de « parer plus vite ». C'est le sommet de l'évolution.

**Validé — le monde ralentit au son.** Hors de la zone de l'horloge, chaque parade parfaite enchaînée assourdit un peu le son et fait baisser la musique d'un demi-ton. Au bout de 3, on n'entend plus que le battement du cœur du chat : le joueur sent qu'il touche au temps.

**Validé — le temps arrêté a un prix.** Pendant l'arrêt complet, le chat peut agir, mais son corps vieillit et se fissure. Rester trop longtemps arrache une partie du corps (le corps est la barre de vie, voir 6.4).

**Validé, sans trop le pousser — durée liée au corps.** Plus le corps est complet, plus il résiste longtemps au temps arrêté (par exemple de 2 s à 6 s). À ne pas mettre en avant : lors d'une première partie, c'est une mécanique de fin de jeu. En réalité, un joueur qui la connaît peut la réussir bien plus tôt.

Questions ouvertes :
- Les 3 parades parfaites : à la suite sur n'importe quoi, ou dans un délai précis ?

### 6.3 Évolution : de l'âme au corps (pilier 2)

**Départ :** une âme, avec un déplacement lent et un gameplay un peu mou.
**Au fil du jeu :** elle se reconstitue un corps.
- **Les grandes parties du corps** sont gagnées en battant les boss.
- **Les petites améliorations** se trouvent en explorant les zones.

**Validé — la chair des boss :** un boss vaincu redevient de la chair inerte. Le chat s'en sert, de façon un peu gore, pour reconstruire son corps.

| Étape | Gain | Nouvelle vulnérabilité | Source |
|---|---|---|---|
| Âme (départ) | flotte | — | — |
| Pattes *(proposé)* | marcher, courir, parade de chute | dégâts de chute | Gardien des Enfers (zone 1) |
| Queue *(proposé)* | équilibre, saut sur les murs, mouvement en l'air | ⚠️ | l'Arbre pendu (zone 2) |
| Yeux *(validé)* — ou griffes *(possible)* | yeux : lire les attaques plus tôt (signal de parade plus visible), voir les murs fragiles. Griffes : contre-attaque juste après une parade parfaite | yeux : aveuglé par les flashs et les éclats | le Juge (zone 3) |
| Cœur *(proposé)* | parade rapide (ouvre la parade du temps) ; corps complet | ⚠️ | Tour de l'Horloge (zone 5) |

Capacités citées : saut sur les murs, courir, parer plus vite.

### 6.4 Perte de membres (côté gore)

**Validé — le corps sert de barre de vie.** Pas de barre de vie classique : chaque coup reçu arrache une partie du corps, et la capacité liée est perdue avec elle. Quand tout le corps est perdu, on redevient une âme. Un coup reçu en âme, c'est la mort.

- **Choix sacrifice :** au bord de la mort, le joueur peut choisir de se couper un membre (un bras, une jambe) pour survivre.
- En échange, il perd la capacité liée à ce membre.

**Validé — recoudre ses membres.** Un membre arraché reste au sol. On peut revenir le ramasser et le recoudre (au sanctuaire, ou sur place ⚠️ à préciser).
- ⚠️ **Il faut un autre moyen de récupérer un membre** quand il est perdu pour de bon (tombé dans la lave ou dans un gouffre). À trouver.

**Validé, pour 1 ou 2 types d'ennemis seulement — les voleurs de membres.** Certains ennemis emportent le membre qu'ils arrachent. Il faut les battre (en parant) pour le récupérer. Les autres ennemis ne le font pas.

**Validé — le corps se voit.** Les parties prises aux boss gardent leur aspect : chair d'un autre, coutures grossières, couleurs dépareillées. On lit l'état du chat directement sur son corps (il manque une patte : ça se voit).
- ⚠️ **Attention à ne pas saturer le HUD.** Le corps doit porter l'information ; le HUD reste minimal.

**Récupérer un membre perdu pour de bon** (tombé dans la lave ou dans un gouffre) :
- **Validé — la chair de remplacement.** Les ennemis vaincus laissent parfois de la chair inerte. Au sanctuaire, on s'en fabrique un membre de fortune, moins bon que l'original, jusqu'au prochain boss. ⚠️ **Ne doit être ni frustrant, ni bloquant.**
- **Validé — le marché des âmes.** Une âme marchande, dans certains sanctuaires, échange des membres contre des fragments trouvés en explorant, ou contre une partie d'un autre membre.

**Les voleurs de membres (validé) :**
- **Le charognard :** créature rapide et fuyante. Elle profite d'un coup reçu pour ramasser le membre au sol et s'enfuit. Il faut la rattraper et parer sa morsure quand elle est acculée.
- **Le collectionneur :** ennemi lent et massif, couvert de membres volés cousus sur lui. Le battre rend le membre volé, parfois avec un fragment bonus. Miroir inquiétant du chat lui-même.

### 6.5 Murs et sols destructibles

- **Une des façons de les détruire :** parer une explosion pour renvoyer son souffle contre le mur ou le sol.
- **Validé — renvoyer un ennemi lourd :** parer la charge d'un gros ennemi le projette dans le mur, qui cède.
- **Validé — les boss cassent l'arène :** certaines attaques du boss détruisent le sol ou les murs. L'arène change à chaque phase, et ça ouvre parfois le chemin vers la suite.
- D'autres mécaniques pourront s'ajouter.

Questions ouvertes :
- À quoi ça sert : passages cachés, raccourcis, pièges, phases de boss ?

### 6.6 À redéfinir plus tard

Bonnes bases, mais pas encore satisfaisantes : à retravailler.

- **Gardes masqués (Tribunal).** Ennemis en armure qui frappent fort, candidats pour « l'ennemi lourd qu'on renvoie dans un mur ». À retravailler.

- **Zones 2 et 3 : Abattoirs et Fonderie.** Proposées : les Abattoirs (chair, crochets, chaînes ; boss : le Boucher) et la Fonderie des Damnés (forges, mines, explosions ; boss : une machine-forge). Pas convaincant : les deux se ressemblent trop. À revoir, en intégrant la zone de verdure souhaitée.
- **Le Battement (Tour de l'Horloge).** Le mécanisme de la tour lui-même, un cœur immense qui bat. Le vaincre donnerait au chat son propre cœur. Pas mal, à retravailler (l'Horloger est retenu comme boss de la tour).

- **Chaque partie du corps change la parade.**
  - Pattes : parade à l'atterrissage (parade de chute).
  - Queue : parade en l'air, ou choix de la direction de renvoi d'un projectile.
  - Griffes : contre-attaque après une parade parfaite.
  - Cœur ou réflexes : parade plus rapide, ce qui ouvre la parade du temps.

---

## 7. Boss

- **Nombreux boss**, chacun avec une **musique différente, épique et adaptative** (qui évolue selon la phase du combat et les actions du joueur).

| Boss | Zone | Idée | Musique | Statut |
|---|---|---|---|---|
| Gardien des Enfers | 1 — enfer éteint | 5 phases ; blessé uniquement par la parade | Ré mineur, cors et taikos, évolue par phase | existe |
| L'Arbre pendu | 2 — forêt des suicidés | arbre immense qui retient des âmes ; racines et branches attaquent ; combat du sol à la cime | ⚠️ | proposé |
| Le Juge | 3 — Tribunal des morts | pèse le cœur du chat ; parer = refuser la sentence | ⚠️ | proposé |
| ⚠️ | 4 — à définir | | ⚠️ | idée |
| L'Horloger | 5 — Tour de l'Horloge | a arrêté son propre temps : intouchable ; attaque au rythme d'une grande horloge ; chaque attaque le rapproche de sa mort ; pas de barre de vie | ⚠️ | validé |
| Le père (le tueur) | 6 — seuil des vivants | humain vivant, brisé et violent ; combat centré sur le temps arrêté | ⚠️ | validé |

### Ennemis par zone

**Zone 2 — forêt des suicidés**
- **Arbres-âmes** *(validé)* : immobiles, ils attrapent ce qui passe trop près. Les parer leur arrache une branche qui saigne.
- **Harpies** *(validé)* : elles plongent du ciel, il faut parer vers le haut. Une harpie parée tombe au sol, étourdie.
- **Pendus** *(validé)* : âmes suspendues aux branches, qui se balancent sur le passage. Leur balancement est un rythme à apprendre.

**Zone 3 — Tribunal des morts**
- **Greffiers** *(validé)* : ils lancent des liasses de registres et des sceaux, qu'on renvoie en parant.
- **File d'âmes** *(validé)* : foule immobile qui attend sa sentence. On la traverse sans la toucher, sinon elle s'agite et bloque le passage.
- **Gardes masqués** : à retravailler (voir 6.6).

**Partout (voir 6.4)** : le charognard et le collectionneur, voleurs de membres.

### L'Horloger (zone 5, Tour de l'Horloge) — validé

- Il a **arrêté son propre temps** : on ne peut pas l'attaquer, et ses attaques sont **inévitables**, sauf à les parer.
- Il attaque **au rythme d'une grande horloge**, visible au fond de l'arène. C'est ce rythme qui permet de parer.
- **Aucun signal d'attaque** comme pour les autres ennemis : seulement le rythme de l'horloge. Le joueur doit l'écouter et le suivre.
- **Pour attaquer, il doit relancer son temps** une fraction de seconde. Chaque attaque le rapproche donc de sa mort imminente.
- **Pas de barre de vie.** Le combat doit être étrange : on voit l'Horloger dépérir par à-coups, à chaque attaque qu'il lance.

---

## 8. État actuel (ce qui existe déjà)

*Relevé dans le code le 12/11/2026.*

**Moteur :** Unity 6, pipeline Built-in. Jeu 2D jouable sur l'axe X, rendu en 3D low-poly.

**Joueur — âme de chat bleue (CatSoul)**
- Déplacement gauche/droite (flèches), saut (flèche haut), parade (X).
- Parade : fenêtre active après l'appui, puis un temps d'attente si elle rate. Réussie : aucun dégât, et on peut re-parer tout de suite.
- Ressenti : arrêt du temps (hit stop), étincelles, tremblement d'écran, flash, compression de l'âme.
- Barre de vie, voile rouge quand le joueur est touché.

**Ennemis**
- **MeleeEnemy** : arme un coup puis balaie un arc. Paré : étourdi.
- **JumperEnemy** : robot sauteur. S'accroupit (signal) puis saute. Paré : meurt.

**Boss — Gardien des Enfers** : 5 phases (faux, orbes, ondes de choc, fente, piliers, météores, charge, orbe du Jugement), métamorphose à chaque phase, musique synthétisée adaptative.

**Zone 1 — enfer éteint** : décor complet, monde vivant (âmes errantes, silhouettes, événements lointains), ambiance qui bascule au rouge pendant le boss.

**Manque :** menu, game over, écran de victoire, sauvegarde, évolution du personnage, zones 2 et 3.

---

## 9. Scope

### Démo (MVP) — 3 zones, 3 boss, jouable du début à la fin
- Zone 1 (enfer éteint) + Gardien des Enfers *(existe en grande partie)*
- Zones 2 et 3 + leurs boss *(à concevoir)*
- Évolution : l'âme du départ, puis au moins 2 étapes de corps
- Parade : attaques, projectiles, chute (+ explosions ?)
- Perte de membre par sacrifice
- Menu, game over, victoire, sauvegarde par point de contrôle
- ⚠️ À trancher : la parade du temps est-elle dans le MVP ?

### Version complète
- Toutes les zones et tous les boss
- Toutes les étapes du corps et les améliorations à trouver en explorant
- Parade du temps complète
- Murs et sols destructibles

### Hors scope (pour l'instant)
-

⚠️ **Point d'attention :** en solo, 2-3 zones et 2-3 boss du niveau du Gardien des Enfers représentent plusieurs mois de travail. *(proposition)* Faire d'abord une **tranche verticale** (zone 1 complète, avec l'âme puis la 1re partie du corps) pour valider que les deux piliers sont amusants, avant de produire les zones suivantes.

## 10. Décisions

- 2026-11-12 — Monde en zones linéaires, chacune finie par un boss — plus simple à produire en solo.
- 2026-11-12 — Les parties du corps viennent des boss, les petites améliorations de l'exploration.
- 2026-11-12 — La perte de membre est un choix de sacrifice pour survivre.
- 2026-11-12 — Parer le temps est possible à tout moment, si l'on pare assez vite (horloge à 3 parades parfaites).
- 2026-11-12 — Projet solo, sortie PC.
- 2026-11-12 — Pas de dégâts de chute pour l'âme (elle flotte) : la parade de chute n'a de sens qu'avec un corps.
- 2026-11-12 — La parade du temps demande la vitesse de parade d'un corps évolué : c'est le sommet de l'évolution.
- 2026-11-12 — Le corps sert de barre de vie : un coup arrache une partie ; sans corps, un coup en âme tue.
- 2026-11-12 — Les boss vaincus redeviennent de la chair inerte, que le chat utilise (de façon gore) pour reconstruire son corps.
- 2026-11-12 — L'horloge n'apparaît que dans une zone de fin de jeu, pour faire découvrir la parade du temps.
- 2026-11-12 — Parer une explosion contre un mur le détruit, mais ce n'est qu'une des façons de détruire le décor.
- 2026-11-12 — Un membre arraché reste au sol et peut être recousu ; un autre moyen de récupération reste à trouver.
- 2026-11-12 — Seuls 1 ou 2 types d'ennemis emportent les membres arrachés.
- 2026-11-12 — L'état du corps se lit sur le personnage ; le HUD reste minimal.
- 2026-11-12 — Parades parfaites enchaînées : le son s'assourdit et la musique baisse, jusqu'au battement de cœur.
- 2026-11-12 — Le temps arrêté use le corps : rester trop longtemps arrache une partie.
- 2026-11-12 — Parer la charge d'un ennemi lourd le projette dans un mur, qui cède.
- 2026-11-12 — Les attaques des boss détruisent l'arène au fil des phases.
- 2026-11-12 — Le dernier boss est le tueur, encore vivant ; arrêter le temps permet de franchir la frontière des vivants.
- 2026-11-12 — Un sanctuaire (sauvegarde, recouture, pause) précède chaque boss.
- 2026-11-12 — La zone change pour de bon après la mort de son boss.
- 2026-11-12 — Difficulté exigeante mais juste ; narration presque sans texte (décor, visions du meurtre en recousant un membre).
- 2026-11-12 — Membre perdu pour de bon : chair de remplacement au sanctuaire et marché des âmes ; jamais frustrant ni bloquant.
- 2026-11-12 — Voleurs de membres : le charognard et le collectionneur.
- 2026-11-12 — La durée du temps arrêté dépend du corps ; mécanique de fin de jeu en première partie, faisable plus tôt par un joueur qui la connaît.
- 2026-11-12 — La Tour de l'Horloge est une zone de fin de jeu, mais pas la zone finale.
- 2026-11-12 — Le jeu doit avoir une zone de verdure.
- 2026-11-12 — La zone de verdure est la forêt des suicidés (Dante).
- 2026-11-12 — Le chat s'inspire du bakeneko / nekomata japonais (chat devenu esprit vengeur).
- 2026-11-12 — Le voyage est une descente par étapes (Dante, Mictlan).
- 2026-11-12 — La zone 3 est le Tribunal des morts (Diyu + pesée du cœur), boss : le Juge.
- 2026-11-12 — Le chat est mort en défendant un enfant contre son père, devenu violent après la mort de sa femme et l'alcool. Le père est le tueur et le dernier boss.
- 2026-11-12 — Le Juge donne les yeux (les griffes restent possibles).
- 2026-11-12 — Ennemis validés : arbres-âmes, harpies, pendus (zone 2) ; greffiers, file d'âmes (zone 3).
- 2026-11-12 — 6 zones au total, dont 3 dans la démo.
- 2026-11-12 — Au retour, l'enfant est vivant mais en danger ; cette fois, le chat se bat (la première fois, il n'avait fait que feuler).
- 2026-11-12 — Trois fins au choix : pacifisme (risque de retour en enfer), combat puis épargner, combat puis tuer — toutes pour l'avenir de l'enfant.
- 2026-11-12 — L'issue du pacifisme dépend du comportement du joueur pendant le jeu (bon : le père prend conscience ; hypocrite : issue différente).
- 2026-11-12 — Choix final sans texte : temps ralenti, bouton « parer » côté enfant, bouton « attaquer » côté père.
- 2026-11-12 — Quatre fins : sacrifice du chat, mort du père, tout le monde est sauvé, et une mauvaise fin où l'enfant meurt.
- 2026-11-12 — Mauvaise fin : le chat ne cherche que sa vengeance et oublie l'enfant, ou le joueur échoue au combat final.
- 2026-11-12 — L'hypocrisie (écart entre le jeu et le choix final) rend le combat final bien plus dur, sans forcer la mauvaise fin.
- 2026-11-12 — Boss de la Tour de l'Horloge : l'Horloger, intouchable, attaque au rythme de l'horloge, dépérit à chaque attaque, sans barre de vie.
- 2026-11-12 — Piste retenue : chaque boss incarne une douleur du père.
- 2026-11-12 — « Oublier l'enfant » = attaquer le père alors que l'enfant est encore en danger, au lieu de le protéger d'abord.

## 11. Idées en vrac

> Ajoute ici les nouvelles idées, une par ligne. On les triera ensemble.

-

## 12. Idées à part

Idées mises de côté : pas rejetées, mais pas retenues pour l'instant.

- **Redevenir âme comme avantage.** L'âme ne prend pas de dégâts de chute : on pourrait sacrifier son corps exprès pour descendre un gouffre, puis le récupérer plus loin.
- **Une zone par type de parade.** Zone 1 : attaques ; zone 2 (falaises, puits) : chutes ; zone 3 (forge, mines) : explosions ; fin : le temps.
- **Rebond sur parade de chute parfaite.** Une parade de chute au timing parfait renvoie vers le haut : technique de déplacement vertical.
- **L'âme voit ce que le corps ne voit pas.** En âme, on voit des plateformes et des âmes cachées ; avec un corps, on perd cette vision.
- **Parer pendant le temps arrêté.** Les projectiles figés peuvent être parés pendant l'arrêt et repartent tous ensemble quand le temps reprend.

- **Zone 4 — les champs de cendre et de poudre.** Plaine minée sous un ciel de feu, guerres sans fin ; parer les explosions, casser les murs. À voir plus tard.
- **Zone 4 — les marais du Styx** (Dante). Marais d'eau noire où les âmes coléreuses se battent dans la boue. À voir plus tard.

## 13. Idées refusées

Gardées pour mémoire, pour ne pas les reproposer.

- **Le marché des âmes compte contre le joueur** s'il échange des membres arrachés à d'autres âmes.
- **Le membre perdu revient au sanctuaire suivant** (abîmé, à atteindre sans lui).


## 14. Recherche : mythes, légendes, religions

Base de travail pour définir les zones, les boss et l'histoire. Premier relevé, à approfondir.

### 14.1 Au-delà et enfers

| Tradition | Lieux et éléments marquants | Pistes pour le jeu |
|---|---|---|
| **Enfer de Dante** (Divine Comédie) | 9 cercles qui descendent. Limbes (prairie verte et château) ; tempête éternelle des luxurieux ; pluie glacée et boue gardées par Cerbère ; marais du Styx ; cité de Dis aux tombeaux en feu ; fleuve de sang bouillant (Phlégéthon) ; **forêt des suicidés** (arbres qui saignent et parlent quand on casse une branche, harpies) ; désert de sable brûlant sous une pluie de feu ; Malebolge (10 fosses) ; **Cocyte, lac gelé** au fond, où Lucifer est pris dans la glace. Le Purgatoire est une montagne, avec le **Paradis terrestre** (jardin) au sommet. | La forêt des suicidés est une **zone de verdure** corrompue et gore. Le Cocyte rejoint la zone 1 froide. Structure en descente. |
| **Grèce antique** (Hadès) | Le Styx et le passeur Charon ; Cerbère ; **prairie des Asphodèles** (plaine grise où errent les âmes ordinaires) ; **Champs Élysées** (paradis verdoyant) ; Tartare (prison des titans) ; le Léthé, fleuve de l'oubli. | Prairie des Asphodèles proche de la zone 1 (âmes qui errent). Élysée comme verdure inaccessible. Un passeur. |
| **Égypte antique** (Douat) | Voyage nocturne dangereux, portes gardées ; **pesée du cœur** face à la plume de Maât ; Ammit dévore les cœurs trop lourds ; **champ des Roseaux** (Aaru), paradis agricole verdoyant. | Pesée du cœur = boss-jugement. Champ des Roseaux = verdure. |
| **Aztèques** (Mictlan) | **9 niveaux** à traverser en 4 ans ; épreuves : montagnes qui s'entrechoquent, vent de couteaux d'obsidienne, fleuve à franchir ; le mort est **guidé par un chien** (xoloitzcuintli) ; Mictlantecuhtli règne au fond. | Zones-épreuves successives (proche de nos zones linéaires). Obsidienne déjà présente. Un chien guide… face à un chat. |
| **Chine** (Diyou) | Enfer bureaucratique : 10 tribunaux et leurs rois juges ; supplices adaptés à chaque faute ; **Meng Po** sert la soupe de l'oubli avant la réincarnation ; pont de l'impuissance. | Boss-juges, administration infernale. La soupe de l'oubli s'oppose à la vengeance (le chat refuse d'oublier). |
| **Bouddhisme / hindouisme** (Naraka) | Enfers **brûlants** et **8 enfers glacés**, où le froid fait éclater la chair ; Yama juge les morts. | Les enfers glacés collent à la zone 1 (froid + gore). |
| **Japon** (Yomi, Jigoku) | Yomi : monde de pourriture (Izanami) ; **rivière Sanzu** à traverser ; **Sai no Kawara** : rive où des enfants morts empilent des pierres que des démons renversent ; Enma juge les morts. | Rive de pierres empilées : décor dérangeant et triste. |
| **Nordiques** (Hel) | Helheim, froid et brumeux (Niflheim) ; pont Gjallarbrú gardé par Módgud ; chien Garm ; Nástrond, rivage des serpents. | Brume froide, pont gardé. |
| **Celtes** (Annwn) | Autre-monde riche et verdoyant, festins, chaudron magique de renaissance. | Verdure trompeuse ; un chaudron qui reconstitue les corps. |

### 14.2 Chats et au-delà

| Tradition | Élément | Pistes pour le jeu |
|---|---|---|
| **Japon** | **Bakeneko** et **nekomata** : chats devenus esprits ou yokai, souvent après une mort injuste, parfois vengeurs (la queue du nekomata se fend en deux). | Colle directement à l'histoire : un chat tué injustement qui revient se venger. La queue fendue peut marquer une étape d'évolution. |
| **Japon** | **Kasha** : démon-chat qui vole les cadavres pendant les funérailles. | Le collectionneur ou le charognard (voleurs de membres) ; ou ce que le chat devient lui-même en se servant de la chair des boss. |
| **Égypte antique** | **Bastet**, déesse chat protectrice ; chats momifiés pour l'au-delà ; **Mafdet**, déesse féline de la justice qui exécute les coupables. | Mafdet : chat justicier, thème de la vengeance juste. |
| **Nordiques** | Le char de **Freyja** est tiré par deux chats. | Allié ou boss possible. |
| **Croyance populaire** | Le chat a **neuf vies**. | 9 vies = 9 zones ? Ou les vies comme membres du corps. |

### 14.3 Pistes qui ressortent

- ✅ **Zone de verdure :** la forêt des suicidés (Dante), le champ des Roseaux (Égypte), les Élysées (Grèce) ou Annwn (Celtes). La forêt des suicidés est la plus en accord avec le ton : verte, gore, triste.
- ✅ **Structure :** l'enfer de Dante et le Mictlan sont tous deux des **descentes par étapes** avec des épreuves, comme nos zones linéaires.
- ✅ **Histoire :** le **bakeneko** donne une base mythologique directe au chat vengeur.

À approfondir : vérifier les sources, choisir 1 ou 2 traditions principales (ou un mélange assumé).

---

## Annexe — Premier jet (texte d'origine)

```
ipe de base il y an a 2 le premier on peux tout parry et le desieme le personage evolu ce qui signifi que on peux parry loes attaque mais aussi les dega de chut mais sa vas lpus loin on peux parri les explosion , e meme te temp de plus on evolu c a dire que on commance par une ame qui a un deplasment lan et un gamplay uyn peux mou puiss on ce reconstitu un cor au fur et a mesur de qui donne plus de contrinde comme d'autre fasson de prandre des dega pais aussi la possiblilider de fair des saut sur les mur , courrir ou parry plyus vit

l'istoir c un chat qui c fair tuer injustement et qui fini an anfere et son objectife est de ce vanger tout an decouvran les lieux

idee :
on peux parry le temp mais pour sa ou faux pouvoir parry asser fort et asser rapodement pour guidee le joueur il faudra une grosse orloge avec une eguille qui c erepoucerais et au pout de 3 au timin parfais le temp commance a etre impacter

un cotter asser gore ex on peux perdre un bras ou devoi c ecouper une jambe pour survivre

ler jeux doir avoir de nombreux boss avec des music diferant , epic et adaptative

plusieur zone tres diferant

des mure / sol destructible
```
