# Parry Everything — conventions du projet

Unity 6, jeu 2D en 3D (axe X jouable). Scène principale : `Assets/Scenes/Main.unity`.

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
    Editor/          outils éditeur uniquement (AutoRefreshWatcher)
  Materials/         matériaux (.mat), nommés selon leur usage (Environment/ : décor)
  Meshes/            meshes sauvegardés (Environment/SoulRealmMeshes.asset)
  Prefabs/           prefabs (Environment/SoulRealm.prefab = décor complet)
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
  - `GuiDraw` pour les HUD OnGUI.
- Commentaires en français, identifiants en anglais.
- Décor : vrais objets dans la scène / prefabs, éditables à la main. Pas de génération du décor par script.
