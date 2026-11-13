---
name: unity-dev
description: Workflow de dev Unity pour Parry Everything — à charger avant toute modification de code, de scène ou d'asset du projet. Couvre le feu vert avant de coder, la compilation via Unity MCP, la lecture de la console, les tests EditMode, la vérification de la scène et la mise à jour de la roadmap.
---

# Dev Unity — Parry Everything

## Avant de coder

1. **Feu vert explicite.** Ne pas écrire de code tant que tsuki ne l'a pas demandé. Valider une idée de design n'est pas un feu vert. En cas de doute, proposer et attendre.
2. Lire la tâche concernée dans `ROADMAP.md` et la section liée de `GAME_DESIGN.md`. Si une décision marquée ⚠️ bloque la tâche, la poser à tsuki avant de coder.
3. Respecter les conventions de `AGENTS.md` (namespaces, `[SerializeField] private`, commentaires en français, utilitaires de `Core/`, `[FormerlySerializedAs]` en cas de renommage).

## Structure

- Code du jeu : `Assets/Scripts/` → assembly `ParryEverything` (asmdef).
- Outils éditeur : `Assets/Scripts/Editor/` → assembly `ParryEverything.Editor` (éditeur seulement).
- Tests : `Assets/Tests/EditMode/` → assembly `ParryEverything.Tests.EditMode` (NUnit, Test Framework 1.6.0).
- Tout code `UnityEditor` dans l'assembly du jeu doit rester sous `#if UNITY_EDITOR`.
- Scène unique : `Assets/Scenes/Main.unity`.

## Boucle de travail

1. Écrire ou modifier le code (Edit / Write).
2. Compiler : `refresh_unity` avec `mode: force`, `scope: all`, `compile: request`. Un **nouveau fichier** n'est vu qu'avec `scope: all` (pas `scripts`).
3. Lire la console : `read_console` avec `types: ["error","warning"]`. L'avertissement `MCP-FOR-UNITY ... WebSocket is not initialised` après un rechargement est normal.
4. Tests : `run_tests` (`mode: EditMode`, `assembly_names: ["ParryEverything.Tests.EditMode"]`), puis `get_test_job` avec `wait_timeout: 60`.
   - En EditMode, `Awake` / `Start` / `Update` ne sont pas appelés : prévoir une méthode d'init publique (ex : `PlayerBody.Revive()`), ou un test PlayMode.
   - Ajouter un test pour toute logique pure (santé, sauvegarde, compteurs, calculs de parade).
5. Après un changement de scène ou de composant : `manage_scene` `validate` (0 script manquant), puis `manage_scene` `save`.
6. Ce qui ne se teste qu'en jouant (ressenti, visuel) : le dire à tsuki avec les étapes précises à tester en mode Play.

## Après une tâche

- Mettre à jour le statut dans `ROADMAP.md` (⬜ → 🔶 → ✅) et son journal.
- Ne pas commiter sans demande. Commits en français, une branche par phase.

## Pièges connus

- Le hook rtk réécrit les commandes Bash ; en mode auto, chaque commande est refusée une fois puis doit être renvoyée telle que le hook l'a réécrite. Préférer Read / Grep / outils MCP quand c'est possible.
- `unity-mcp` est épinglé sur un commit dans `Packages/manifest.json` : ne pas le remettre sur `#main`.
- Git LFS est déclaré dans `.gitattributes` ; vérifier `git lfs version` avant d'ajouter des fichiers lourds.
