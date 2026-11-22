using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // Visions du passé du chat : des plans figés sans texte (affiches abstraites : le père en bloc noir, la mère en
    // arche crème, l'enfant en petit bloc crème, le chat en arche noire), séparés par des fondus blancs.
    // Images : Assets/Resources/Visions/NN_nom.png (sources vectorielles : References/Flashbacks/Histoire/).
    // Chapitres :
    //   0  prologue, au lancement d'une nouvelle partie : le coup, le sacrifice du chat (plans rapides),
    //      sa mort, son âme qui descend ;
    //   1… souvenirs, un chapitre à chaque nouvelle partie du corps (chair d'un boss, pattes…), dans l'ordre de
    //      l'histoire : la famille heureuse, la mère, la chute du père. Chapitre vu = flag GameFlags.Memory(n).
    // Nom précédé de « ! » : plan rapide (coupe sèche, éclair rouge, secousse) pour donner du rythme.
    // Joue en temps réel (même le jeu figé).
    public class MurderVision : MonoBehaviour
    {
        public static readonly string[][] Chapters =
        {
            new[] { "11_le_coup", "!11a_le_bond", "!11b_l_impact", "!11c_les_eclats", "!11d_la_chute", "!11e_l_enfant",
                    "12_la_mort_du_chat", "13_l_ame" },
            new[] { "01_la_famille", "02_le_jeu", "03_le_repas" },
            new[] { "04_la_maladie", "05_l_absence", "06_le_deuil" },
            new[] { "07_la_premiere_bouteille", "08_la_decadence" },
            new[] { "09_la_colere", "10_la_nuit" },
        };

        private const float Aspect = 16f / 9f;

        [SerializeField] private float fadeIn = 0.5f;      // depuis le blanc
        [SerializeField] private float hold = 2.6f;        // plan figé
        [SerializeField] private float flash = 0.3f;       // éclair blanc entre deux plans
        [SerializeField] private float fadeOut = 0.8f;     // retour au jeu
        [SerializeField] private float zoom = 0.05f;       // lente avancée dans l'image pendant un plan
        [SerializeField] private float quickHold = 0.42f;  // plan rapide
        [SerializeField] private float quickFlash = 0.1f;
        [SerializeField] private float quickShake = 14f;   // secousse d'un plan rapide (pixels)

        private sealed class Plate
        {
            public Texture2D Texture;
            public bool Quick;
            public float Start, FadeIn, Hold, Flash;
            public float End => Start + FadeIn + Hold + Flash;
        }

        private static readonly Color FlashWhite = new Color(1f, 0.97f, 0.92f);
        private static readonly Color FlashRed = new Color(0.85f, 0.12f, 0.06f);

        private readonly List<Plate> plates = new List<Plate>();
        private float time;
        private Action onDone;
        private bool done;

        public static bool IsPlaying { get; private set; }

        // Prologue (nouvelle partie)
        public static MurderVision PlayPrologue(Action onDone) => Play(Chapters[0], onDone);

        // Prochain souvenir pas encore vu ; s'il n'en reste plus, onDone est appelé tout de suite (pas de vision)
        public static MurderVision PlayNextMemory(Action onDone)
        {
            int chapter = NextMemoryChapter();
            if (chapter < 0)
            {
                onDone?.Invoke();
                return null;
            }
            GameSession.SetFlag(GameFlags.Memory(chapter));
            return Play(Chapters[chapter], onDone);
        }

        // Premier chapitre de souvenirs (1…) pas encore vu ; -1 : tout a été vu
        public static int NextMemoryChapter()
        {
            for (int i = 1; i < Chapters.Length; i++)
            {
                if (!GameSession.HasFlag(GameFlags.Memory(i))) return i;
            }
            return -1;
        }

        // Entrée de Chapters → nom de l'image ; plan rapide ?
        public static string PlateName(string entry) => entry.TrimStart('!');
        public static bool IsQuick(string entry) => entry.StartsWith("!");

        public static MurderVision Play(string[] plateNames, Action onDone)
        {
            GameObject go = new GameObject("MurderVision");
            MurderVision vision = go.AddComponent<MurderVision>();
            vision.onDone = onDone;
            float start = 0f;
            foreach (string entry in plateNames)
            {
                Texture2D t = Resources.Load<Texture2D>("Visions/" + PlateName(entry));
                if (t == null)
                {
                    Debug.LogWarning("Vision introuvable : Resources/Visions/" + PlateName(entry));
                    continue;
                }
                bool quick = IsQuick(entry);
                var plate = new Plate
                {
                    Texture = t,
                    Quick = quick,
                    Start = start,
                    FadeIn = quick ? 0.03f : vision.fadeIn,
                    Hold = quick ? vision.quickHold : vision.hold,
                    Flash = quick ? vision.quickFlash : vision.flash,
                };
                vision.plates.Add(plate);
                start = plate.End;
            }
            IsPlaying = true;
            return vision;
        }

        private float PlatesEnd => plates.Count > 0 ? plates[plates.Count - 1].End : 0f;
        private float TotalDuration => PlatesEnd + fadeOut;

        private void Update()
        {
            time += Time.unscaledDeltaTime;
            if (!done && time >= PlatesEnd)
            {
                done = true;
                onDone?.Invoke();
            }
            if (time >= TotalDuration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            IsPlaying = false;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.depth = -200;
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Color old = GUI.color;

            Plate p = plates.Find(x => time < x.End);
            if (p != null)
            {
                float t = time - p.Start;
                float length = p.End - p.Start;
                // Plan au format 16:9, centré, fond noir autour ; il avance lentement vers le spectateur
                // (plan rapide : avance plus vite et tremble)
                GUI.color = Color.black;
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = Color.white;
                float z = 1f + (p.Quick ? zoom * 1.6f : zoom) * Mathf.SmoothStep(0f, 1f, t / length);
                Rect fit = FitRect(screen);
                float shake = p.Quick ? quickShake * (fit.height / 1080f) * (1f - t / length) : 0f;
                Vector2 jitter = shake > 0f ? new Vector2(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake)) : Vector2.zero;
                Rect zoomed = new Rect(fit.center.x - fit.width * z / 2f + jitter.x, fit.center.y - fit.height * z / 2f + jitter.y, fit.width * z, fit.height * z);
                GUI.BeginGroup(fit);
                GUI.DrawTexture(new Rect(zoomed.x - fit.x, zoomed.y - fit.y, zoomed.width, zoomed.height), p.Texture, ScaleMode.StretchToFill);
                GUI.EndGroup();

                float alpha = 0f;
                if (t < p.FadeIn) alpha = 1f - t / p.FadeIn;
                else if (t > p.FadeIn + p.Hold) alpha = (t - p.FadeIn - p.Hold) / p.Flash;
                // Éclair entre deux plans : rouge autour des plans rapides, blanc sinon
                int i = plates.IndexOf(p);
                bool redFlash = p.Quick
                    || (t > p.FadeIn + p.Hold && i + 1 < plates.Count && plates[i + 1].Quick)
                    || (t < p.FadeIn && i > 0 && plates[i - 1].Quick);
                DrawFlash(screen, alpha, redFlash ? FlashRed : FlashWhite);
            }
            else
            {
                // Retour au jeu : le blanc s'efface
                DrawFlash(screen, 1f - (time - PlatesEnd) / fadeOut, FlashWhite);
            }
            GUI.color = old;
        }

        private static void DrawFlash(Rect screen, float alpha, Color color)
        {
            if (alpha <= 0f) return;
            color.a = Mathf.Clamp01(alpha);
            GUI.color = color;
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
        }

        private static Rect FitRect(Rect screen)
        {
            float w = screen.width, h = w / Aspect;
            if (h > screen.height)
            {
                h = screen.height;
                w = h * Aspect;
            }
            return new Rect((screen.width - w) / 2f, (screen.height - h) / 2f, w, h);
        }
    }
}
