using UnityEngine;

namespace ParryEverything.Core
{
    // Options du joueur (son, affichage), gardées dans PlayerPrefs, à part de la partie :
    // effacer la sauvegarde ne touche pas aux options.
    // Les sources de son lisent MusicVolume / SfxVolume ; le volume général passe par AudioListener.
    public static class GameSettings
    {
        private const string KeyMaster = "opt.master";
        private const string KeyMusic = "opt.music";
        private const string KeySfx = "opt.sfx";
        private const string KeyFullscreen = "opt.fullscreen";
        private const string KeyResW = "opt.resW";
        private const string KeyResH = "opt.resH";
        private const string KeyVSync = "opt.vsync";

        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.8f;
        public static float SfxVolume { get; private set; } = 1f;
        public static bool Fullscreen { get; private set; } = true;
        public static bool VSync { get; private set; } = true;
        // Résolution choisie (0 : celle de l'écran)
        public static int ResolutionWidth { get; private set; }
        public static int ResolutionHeight { get; private set; }

        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); ApplyAudio(); }
        public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); }
        public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); }

        public static void SetFullscreen(bool value) { Fullscreen = value; ApplyDisplay(); }
        public static void SetVSync(bool value) { VSync = value; ApplyDisplay(); }

        public static void SetResolution(int width, int height)
        {
            ResolutionWidth = Mathf.Max(0, width);
            ResolutionHeight = Mathf.Max(0, height);
            ApplyDisplay();
        }

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, 0.8f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, 1f));
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            VSync = PlayerPrefs.GetInt(KeyVSync, 1) == 1;
            ResolutionWidth = PlayerPrefs.GetInt(KeyResW, 0);
            ResolutionHeight = PlayerPrefs.GetInt(KeyResH, 0);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
            PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
            PlayerPrefs.SetFloat(KeySfx, SfxVolume);
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
            PlayerPrefs.SetInt(KeyResW, ResolutionWidth);
            PlayerPrefs.SetInt(KeyResH, ResolutionHeight);
            PlayerPrefs.Save();
        }

        public static void ApplyAll()
        {
            ApplyAudio();
            ApplyDisplay();
        }

        private static void ApplyAudio()
        {
            AudioListener.volume = MasterVolume;
        }

        // Dans l'éditeur, la fenêtre Game ne change pas de taille : seule la synchro verticale s'applique
        private static void ApplyDisplay()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
            FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = ResolutionWidth > 0 ? ResolutionWidth : Screen.currentResolution.width;
            int h = ResolutionHeight > 0 ? ResolutionHeight : Screen.currentResolution.height;
            Screen.SetResolution(w, h, mode);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadAtStartup()
        {
            Load();
            ApplyAll();
        }
    }
}
