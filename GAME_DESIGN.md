# Parry Everything — Document de game design

Document vivant : on y met toutes les idées, puis on les trie pour définir le jeu et son scope.

**Comment l'utiliser**
- Écris tes idées en vrac dans **Idées en vrac** (section 12), sans te soucier de l'ordre ni de la forme.
- Les autres sections se remplissent au fur et à mesure qu'on trie et qu'on décide.
- Les lignes `> ...` sont des questions pour guider : réponds en dessous, puis supprime-les.
- Une décision prise va dans **Décisions** (section 14), avec la date.

---

## 1. Pitch

> En une ou deux phrases : c'est quoi le jeu, pour qui, et qu'est-ce qui le rend unique ?

**Titre :** Parry Everything
**Genre :**
**Pitch :**

## 2. Piliers

> Les 3 à 5 règles qui guident toutes les décisions. Une idée qui ne sert aucun pilier est hors scope.
> Exemple : « La parade est la seule façon de blesser les ennemis. »

1.
2.
3.

## 3. Expérience visée

> Qu'est-ce que le joueur doit ressentir ? Durée d'une partie, durée totale du jeu, difficulté, public visé.

- **Ressenti principal :**
- **Durée d'une session :**
- **Durée totale :**
- **Difficulté :**
- **Public :**
- **Références / inspirations (ressenti, pas copie) :**

---

## 4. État actuel (ce qui existe déjà)

> Pré-rempli à partir du code le 12/11/2026. À corriger si besoin.

**Moteur :** Unity 6, pipeline Built-in. Jeu 2D jouable sur l'axe X, rendu en 3D low-poly.

**Joueur — âme de chat bleue (CatSoul)**
- Déplacement gauche/droite (flèches), saut (flèche haut, hauteur réglée), parade (X).
- Parade : fenêtre active après l'appui, puis un temps d'attente si elle rate. Une parade réussie annule les dégâts et permet de re-parer tout de suite.
- Ressenti : arrêt du temps (hit stop), étincelles, tremblement d'écran, flash et compression de l'âme.
- Barre de vie, voile rouge quand le joueur est touché.

**Ennemis**
- **MeleeEnemy** : arme un coup puis balaie un arc devant lui. Paré : l'arme repart et l'ennemi est étourdi.
- **JumperEnemy** : petit robot sauteur. S'accroupit (signal pour parer) puis saute sur le joueur. Paré : meurt.

**Boss — Gardien des Enfers (HellGuardian)**
- Ne peut être blessé **qu'en parant**. 5 phases, chacune ajoute une attaque et le métamorphose :
  - I. Le Gardien s'éveille : coup de faux, orbes de feu
  - II. Colère des Abysses : + écrasement, ondes de choc, fente de faux, piliers de feu
  - III. Pluie d'Âmes : + météores
  - IV. Le Faucheur : + charge, combo de faux
  - V. Jugement Final : + orbe du Jugement (échange de parades)
- Projectiles : orbe, onde de choc, météore, jugement, fente, pilier.
- Musique synthétisée en temps réel, qui évolue avec les phases et les parades.

**Zone — Royaume des âmes, « enfer éteint »**
- Décor : plaine de basalte, fissures de lave figée, ruines, arbres calcinés, pieux et crânes, îles flottantes avec chaînes et cages, foyers mal éteints.
- Monde vivant : âmes damnées errantes, silhouettes qui ont leur propre routine, événements lointains (éboulements, lueurs rouges, cris, âmes qui tombent du ciel).
- L'ambiance (lumière, brouillard, effet d'écran, son) bascule vers le rouge pendant le combat de boss.
- Son d'ambiance synthétisé : vent, drone, chuchotements, craquements, silences.

**Ce qui manque à ce jour**
- Pas de menu, ni de game over ni d'écran de victoire structurés.
- Une seule scène, une seule zone.
- Pas de progression, ni de sauvegarde.

---

## 5. Boucle de jeu

> Que fait le joueur minute par minute (boucle courte), niveau par niveau (boucle moyenne), sur tout le jeu (boucle longue) ?

- **Boucle courte (secondes) :**
- **Boucle moyenne (un niveau / une zone) :**
- **Boucle longue (tout le jeu) :**

## 6. Mécaniques

> Pour chaque mécanique : ce qu'elle fait, pourquoi elle existe (quel pilier), et son statut (existe / à faire / idée).

| Mécanique | Description | Pilier | Statut |
|---|---|---|---|
| Parade | | | existe |
| Saut | | | existe |
| | | | |

## 7. Progression

> Le joueur devient-il plus fort ? Comment ? Nouvelles capacités, améliorations, déblocages, rien du tout ?

## 8. Monde et zones

> Combien de zones ? Quel thème et quelle ambiance pour chacune ? Comment passe-t-on de l'une à l'autre ?

| Zone | Thème / ambiance | Ennemis | Boss | Statut |
|---|---|---|---|---|
| Royaume des âmes (enfer éteint) | froid, triste, malaisant | Melee, Jumper | Gardien des Enfers | en cours |
| | | | | |

## 9. Ennemis et boss

> Pour chaque ennemi : comportement, signal qui annonce son attaque (pour parer), ce qui se passe quand on le pare.

## 10. Histoire et univers

> Qui est l'âme de chat ? Pourquoi est-elle en enfer ? Que cherche-t-elle ? Comment l'histoire est-elle racontée (textes, décor, rien) ?

## 11. Direction artistique et audio

> Style visuel, palette par zone, style musical, place du son dans le gameplay.

---

## 12. Idées en vrac

> Tout ce qui te passe par la tête. Une idée par ligne. On triera ensemble.

-

---

## 13. Scope

> On classe chaque idée dans une de ces trois cases. Le MVP doit être petit et jouable du début à la fin.

### MVP (version minimale jouable du début à la fin)
-

### Version complète
-

### Hors scope (bonnes idées, mais pas pour ce jeu / pas maintenant)
-

## 14. Décisions

> Format : `AAAA-MM-JJ — décision — pourquoi`

-

## 15. Questions ouvertes

-
