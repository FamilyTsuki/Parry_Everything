#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Détecte automatiquement la sauvegarde de scripts depuis VS Code / éditeurs externes
/// et déclenche la recompilation Unity sans avoir besoin de faire Ctrl+R.
/// </summary>
[InitializeOnLoad]
public static class AutoRefreshWatcher
{
    private static FileSystemWatcher watcher;
    private static double lastChangeTime = 0;
    private static bool pendingRefresh = false;
    private const double DebounceSeconds = 0.35;

    static AutoRefreshWatcher()
    {
        // Permet à Unity de traiter les événements même quand la fenêtre n'a pas le focus sous Hyprland
        Application.runInBackground = true;

        SetupWatcher();

        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
    }

    private static void SetupWatcher()
    {
        try
        {
            string assetsPath = Application.dataPath;
            watcher = new FileSystemWatcher(assetsPath)
            {
                IncludeSubdirectories = true,
                Filter = "*.cs",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Renamed += OnFileRenamed;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AutoRefreshWatcher] Impossible de démarrer FileSystemWatcher : " + ex.Message);
        }
    }

    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Ignorer les fichiers temporaires, caches ou cachés
        if (string.IsNullOrEmpty(e.Name) || e.Name.StartsWith(".") || e.Name.EndsWith("~")) return;
        TriggerRefresh();
    }

    private static void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        TriggerRefresh();
    }

    private static void TriggerRefresh()
    {
        lastChangeTime = EditorApplication.timeSinceStartup;
        pendingRefresh = true;
    }

    private static void OnEditorUpdate()
    {
        if (pendingRefresh)
        {
            if (EditorApplication.timeSinceStartup - lastChangeTime >= DebounceSeconds)
            {
                pendingRefresh = false;

                if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
                {
                    AssetDatabase.Refresh();
                }
            }
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Si l'utilisateur clique sur Play alors qu'un refresh était en attente
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (pendingRefresh)
            {
                pendingRefresh = false;
                AssetDatabase.Refresh();
            }
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;

        if (watcher != null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            watcher = null;
        }
    }
}
#endif
