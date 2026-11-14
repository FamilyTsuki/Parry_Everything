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
- Modèles du chat : générés par Editor/CatModelBuilder. Forme, placement, couleurs : asset `Meshes/Player/CatModelSettings.asset` (menu `Parry Everything > Modèle du chat : réglages`) ; chaque modification reconstruit les meshes et prefabs en place. Les repères de la patte sont écrits sur le prefab (`CatLegRig`) et lus par CatBody. Placement en jeu : composant CatBody.
