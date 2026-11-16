# Parry Everything — conventions du projet

Unity 6, jeu 2D en 3D (axe X jouable). Scène principale : `Assets/Scenes/Main.unity`.
Scène `Assets/Scenes/AnimationLab.unity` : visionneuse des animations (chat + boss en `labMode`, panneau `Lab/AnimationLab`, caméra orbitale `Lab/LabCamera`). Nouveau geste du boss : l'ajouter à `HellGuardian.LabMove` et au panneau.

## Arborescence

```
Assets/
  Scenes/            Main.unity
  Scripts/
    Core/            caméra, utilitaires partagés (ParticleBurst, MaterialFactory, GeneratedObjects, GuiDraw, LowPoly, HitFlash)
    Player/          PlayerController, CatSoul (visuel du joueur)
    Enemies/         MeleeEnemy, JumperEnemy
    Boss/            HellGuardian, BossProjectile
    Audio/           BossMusic, AmbientWind (synthèse temps réel)
    Environment/     petits comportements de décor (FloatingBob, CameraParallax)
    Lab/             scène AnimationLab (AnimationLab, LabCamera)
    Editor/          outils éditeur uniquement (AutoRefreshWatcher, CatModelBuilder + MeshDraft : modèles du chat)
  Materials/         matériaux (.mat), nommés selon leur usage (Environment/ : décor)
  Textures/          textures (Environment/ : particules de neige/brume, dégradé du ciel)
  Meshes/            meshes sauvegardés (Environment/SoulRealmMeshes.asset ; Player/CatModels.asset = corps du chat)
  Prefabs/           prefabs (Environment/SoulRealm.prefab = décor complet ; Player/ = FleshBall, FleshBody, CatLeg)
  PhysicsMaterials/  physic materials
  Resources/Shaders/ shaders chargés par Shader.Find (doivent rester dans Resources)
  ThirdParty/        assets externes (Asset Store) — ne pas modifier
```

## Code

- Namespace `ParryEverything.<Dossier>` (ex : `ParryEverything.Player`). Un fichier = une classe, même nom.
- Classes, méthodes, propriétés : PascalCase. Champs : camelCase.
- Champs réglables dans l'Inspector : `[SerializeField] private`, jamais `public`. Accès externe via une propriété en lecture (`public float Life => life;`).
- Renommer un champ sérialisé : ajouter `[FormerlySerializedAs("ancienNom")]`, sinon la valeur de la scène est perdue.
- Renommer/déplacer un script : passer par l'éditeur Unity (ou `AssetDatabase.MoveAsset`) pour garder le `.meta` (GUID), sinon les composants deviennent "Missing".
- Modificateurs explicites (`private void Update()`).
- Réutiliser les utilitaires de `Core/` au lieu de dupliquer :
  - `ParticleBurst.Spawn(...)` pour les gerbes de particules ;
  - `MaterialFactory.Unlit/Lit` au lieu de `new Material(Shader.Find(...))` ;
  - `GeneratedObjects` pour les objets créés en code (HideAndDontSave + destruction groupée) ;
  - `GameCamera.ShakeMain(...)` / `GameCamera.Main` pour la caméra ;
  - `GuiDraw` pour les HUD OnGUI ;
  - `GameInput.Pressed/Held(GameAction.X)` pour les entrées, jamais `Input.GetKey` (nouvelle touche : l'ajouter dans `GameAction` et `GameInput`) ;
  - `TimeControl.SetEffectScale(...)` pour arrêts du temps et ralentis, jamais `Time.timeScale` directement (sinon la pause casse) ;
  - `GameFlow.IsPlaying` pour savoir si le joueur peut agir ; `GameFlow.Instance.SaveAtSanctuary(...)` / `BossDefeated(...)` pour sauvegarder la progression ;
  - `GameSession` pour l'état de la partie qui survit au rechargement (boss vaincus, membres, sanctuaire) : un objet de la scène qui dépend de la progression le lit dans son `Start` ;
  - `SaveSystem` / `SaveData` pour la sauvegarde (nouveau champ : valeur par défaut sensée ; changement de sens : monter `CurrentVersion` + migration).
- Commentaires en français, identifiants en anglais.
- Décor : vrais objets dans la scène / prefabs, éditables à la main. Pas de génération du décor par script.
- Modèles du chat (torse, pattes, bras, tête, queue) : générés par Editor/CatModelBuilder. Forme, placement, couleurs : asset `Meshes/Player/CatModelSettings.asset` (menu `Parry Everything > Modèle du chat : réglages`) ; chaque modification reconstruit les meshes et prefabs en place. Repères écrits sur les prefabs (`CatLegRig`, `CatTorsoRig`) et lus par CatBody. Patte : os CatLeg (hanche), Knee, Ankle + `HipAnchor` (haut de cuisse/fesse, au-dessus de la hanche : CatBody annule la rotation de la cuisse, la fesse reste collée au corps). Bras rigide : pivot de l'épaule recalculé par l'import (centre des 40 % du haut du bras, `CatBlenderImport.ShoulderJoint`), l'origine de UpperArm dans Blender n'a pas besoin d'être exacte. Placement et animation en jeu : composant CatBody. Apparence selon l'état du membre : shader `FleshLit` (fourrure en UV1, veines en UV2) piloté par CatBody. Style retenu : low-poly (`style = Facets` → shader `FleshFacet`, maillages à facettes unies) ; les autres styles (`FleshFur`, `FleshToon`) partagent le calcul de `FleshCore.cginc`.
- Formes ET pose du chat modelées dans Blender : `Assets/Models/Cat/cat.blend` (`CatModelSettings.source = Blender`), relu par `Editor/CatBlenderImport` à chaque enregistrement. Le fichier montre le chat debout immobile tel qu'en jeu (origine = centre du joueur, sol `Ground` = pieds). Origine de chaque pièce = son attache (Head = cou, UpperArm = épaule, Forearm = coude, Leg = hanche, TailSeg0 = queue) ; place et rotation des pièces = pose de repos, écrite sur les prefabs (`CatTorsoRig` : hauteur/inclinaison du torse ; `CatLegRig` : écart, cheville ; rotations de CatHead, CatArm, Elbow, Seg*) ; CatBody ajoute les mouvements par-dessus. Garder les noms d'objets (Torso, Head, UpperArm, Forearm, Leg, TailSeg0…N ; Chest/Belly/Pelvis sous Torso, Thigh/Shin/Foot sous Leg ; repères Knee, Ankle, Eye, Nose ; Ground). `Far_*` = côté loin, vrai miroir (forme retournée, pilotes Blender ; `Tools/Blender/cat_mirror.py` à relancer après ajout d'un morceau), ignoré par Unity ; `Ear_L` = miroir de `Ear_R` (même script, relue retournée : triangles inversés à l'import) ; en jeu, bras et patte du côté loin sont retournés de la même façon (échelle −1 en profondeur). Tout autre objet rangé sous une pièce (Skull, Ear_R, Ear_L…) est recollé avec elle ; `Hand` (sous Forearm) est une pièce à part (membre main : `CatHand`, enfant `Elbow/Hand` du prefab CatArm). Yeux : objets `Eye_R` (côté caméra) et `Eye_L` (miroir, cat_mirror.py) sous Head, créés par `Tools/Blender/cat_eyes.py`, repris tels quels avec leurs couleurs (sRGB) dans le matériau lumineux ; sans eux, Unity génère les yeux au repère Eye (l'export vers Blender ne les recrée pas : relancer cat_eyes.py). Couleur des sommets = fourrure ; chair, plaques, veines, truffe et poids de la patte sont refaits par Unity. Sans `Ground` (ancien fichier) : pose prise dans `CatModelSettings`. Menu *Exporter le chat vers Blender* : `Cat.fbx` puis `cat.blend` refait par Blender (`Tools/Blender/cat_blend_setup.py`), ancien fichier copié dans `References/Backups/`.
- Queue : les `TailSeg0…N` de Blender (forme + pose) sont recollés à l'import en un seul maillage `CatTail` déformé par 24 os réguliers (`Editor/CatTailBuilder`, prefab `CatTail` : `Bone0 → … → Bone23` + `Tail` SkinnedMeshRenderer). Animation dans `CatBody.PlaceTail` : courbure intégrée os par os (gestes au repos `TailGestures`, vent en course, ressorts plus mous vers le bout).
- Sutures entre membres détachables (`Editor/CatSutures`, appelé par CatBlenderImport) : bourrelet de chair à vif + points de fil sombre, calculés là où la pièce enfant traverse son parent dans le fichier Blender (repli : coupe du bout de l'enfant si elles se touchent à peine), ajoutés au maillage de l'enfant (part avec lui, suit ses os). Réglages : section « Sutures » de `CatModelSettings`. Shader : UV2.y = bourrelet (luit quand `_Damage` monte), UV1.a = 2 (pas de fourrure). Modèle généré (source ≠ Blender) : pas de sutures.
- Nouveau type de membre : l'ajouter À LA FIN de `LimbKind` (la sauvegarde stocke le numéro).
