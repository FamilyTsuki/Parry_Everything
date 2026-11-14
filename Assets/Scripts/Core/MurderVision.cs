using System;
using UnityEngine;

namespace ParryEverything.Core
{
    // Vision du meurtre, quand le chat se coud la chair d'un boss : 3 plans figés, sans texte,
    // en ombres chinoises sur fond rouge, séparés par des fondus blancs.
    //  1. l'enfant recroquevillé, une porte ouverte et une ombre qui s'allonge vers lui ;
    //  2. le père qui lève le bras, une bouteille dans l'autre main ;
    //  3. le chat qui feule entre l'enfant et l'ombre du père.
    // Les images sont dessinées en code (pas d'asset), en temps réel : la vision joue même le jeu figé.
    public class MurderVision : MonoBehaviour
    {
        private const int Width = 480;
        private const int Height = 270;
        private const float Aspect = (float)Width / Height;

        [SerializeField] private float fadeIn = 0.35f;     // depuis le blanc
        [SerializeField] private float hold = 1.6f;        // plan figé
        [SerializeField] private float flash = 0.25f;      // éclair blanc entre deux plans
        [SerializeField] private float fadeOut = 0.8f;     // retour au jeu

        private static readonly Color Silhouette = new Color(0.03f, 0f, 0f);

        private Texture2D[] plates;
        private float time;
        private Action onDone;
        private bool done;

        public static bool IsPlaying { get; private set; }

        // Lance la vision ; onDone est appelé à la fin (avant le retour au jeu en fondu)
        public static MurderVision Play(Action onDone)
        {
            GameObject go = new GameObject("MurderVision");
            MurderVision vision = go.AddComponent<MurderVision>();
            vision.onDone = onDone;
            IsPlaying = true;
            return vision;
        }

        private float PlateDuration => fadeIn + hold + flash;
        private float TotalDuration => plates.Length * PlateDuration + fadeOut;

        private void Awake()
        {
            plates = new[] { PaintPlate(DrawChildAlone), PaintPlate(DrawFatherRaisesArm), PaintPlate(DrawCatHisses) };
        }

        private void Update()
        {
            time += Time.unscaledDeltaTime;
            if (!done && time >= plates.Length * PlateDuration)
            {
                done = true;
                onDone?.Invoke();
            }
            if (time >= TotalDuration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            IsPlaying = false;
            if (plates == null) return;
            foreach (Texture2D t in plates) if (t != null) Destroy(t);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || plates == null) return;
            GUI.depth = -200;
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Color old = GUI.color;

            int index = Mathf.Min((int)(time / PlateDuration), plates.Length);
            if (index < plates.Length)
            {
                float t = time - index * PlateDuration;
                // Plan au format 16:9, centré, fond noir autour
                GUI.color = Color.black;
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(FitRect(screen), plates[index], ScaleMode.StretchToFill);

                float white = 0f;
                if (t < fadeIn) white = 1f - t / fadeIn;
                else if (t > fadeIn + hold) white = (t - fadeIn - hold) / flash;
                DrawWhite(screen, white);
            }
            else
            {
                // Retour au jeu : le blanc s'efface
                DrawWhite(screen, 1f - (time - plates.Length * PlateDuration) / fadeOut);
            }
            GUI.color = old;
        }

        private static void DrawWhite(Rect screen, float alpha)
        {
            if (alpha <= 0f) return;
            GUI.color = new Color(1f, 0.97f, 0.92f, Mathf.Clamp01(alpha));
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

        // ---------- Peinture ----------
        // Coordonnées : x de 0 à Aspect (gauche à droite), y de 0 à 1 (bas en haut).

        private delegate void Painter(Canvas canvas);

        private sealed class Canvas
        {
            public readonly Color[] Pixels = new Color[Width * Height];

            public void Fill(Func<float, float, bool> inside, Color color)
            {
                for (int py = 0; py < Height; py++)
                {
                    float y = (py + 0.5f) / Height;
                    for (int px = 0; px < Width; px++)
                    {
                        float x = (px + 0.5f) / Height;
                        if (inside(x, y)) Pixels[py * Width + px] = color;
                    }
                }
            }

            public void Circle(float cx, float cy, float r, Color c) =>
                Fill((x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r, c);

            public void Ellipse(float cx, float cy, float rx, float ry, Color c) =>
                Fill((x, y) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1f, c);

            public void Rect(float x0, float y0, float x1, float y1, Color c) =>
                Fill((x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1, c);

            // Trait épais de (ax, ay) à (bx, by)
            public void Line(float ax, float ay, float bx, float by, float width, Color c)
            {
                float dx = bx - ax, dy = by - ay;
                float len2 = dx * dx + dy * dy;
                float r2 = width * width / 4f;
                Fill((x, y) =>
                {
                    float t = len2 > 0f ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / len2) : 0f;
                    float ex = ax + t * dx - x, ey = ay + t * dy - y;
                    return ex * ex + ey * ey <= r2;
                }, c);
            }

            public void Polygon(Color c, params float[] xy)
            {
                Fill((x, y) =>
                {
                    bool inside = false;
                    int n = xy.Length / 2;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                        if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
                    }
                    return inside;
                }, c);
            }
        }

        private static Texture2D PaintPlate(Painter painter)
        {
            var canvas = new Canvas();
            // Fond : rouge sombre en bas, plus clair en haut, bords assombris
            for (int py = 0; py < Height; py++)
            {
                for (int px = 0; px < Width; px++)
                {
                    float u = (px + 0.5f) / Width, v = (py + 0.5f) / Height;
                    Color c = Color.Lerp(new Color(0.3f, 0.02f, 0.02f), new Color(0.78f, 0.16f, 0.08f), v * 0.85f);
                    float vignette = Mathf.Clamp01(1.2f - ((u - 0.5f) * (u - 0.5f) * 2.2f + (v - 0.55f) * (v - 0.55f) * 2f));
                    canvas.Pixels[py * Width + px] = c * vignette;
                }
            }
            // Sol
            canvas.Rect(0f, 0f, Aspect, 0.16f, Silhouette);
            painter(canvas);

            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(canvas.Pixels);
            tex.Apply();
            return tex;
        }

        // Enfant recroquevillé, assis, les bras autour des genoux. (x, ground) = bas du dos ; s = échelle.
        private static void DrawChild(Canvas c, float x, float s, bool facingRight = true)
        {
            float f = facingRight ? 1f : -1f;
            float g = 0.16f;
            c.Ellipse(x, g + 0.1f * s, 0.1f * s, 0.1f * s, Silhouette);                 // dos arrondi
            c.Ellipse(x + 0.1f * s * f, g + 0.09f * s, 0.05f * s, 0.085f * s, Silhouette); // genoux
            c.Circle(x + 0.03f * s * f, g + 0.22f * s, 0.055f * s, Silhouette);         // tête baissée
            c.Rect(Mathf.Min(x, x + 0.17f * s * f), g, Mathf.Max(x, x + 0.17f * s * f), g + 0.03f * s, Silhouette); // pieds
        }

        // Plan 1 : l'enfant seul ; une porte ouverte, la lumière et une ombre qui s'allonge vers lui
        private static void DrawChildAlone(Canvas c)
        {
            Color light = new Color(1f, 0.7f, 0.45f);
            c.Rect(1.3f, 0.16f, 1.5f, 0.78f, light);                                           // porte éclairée
            c.Polygon(new Color(1f, 0.55f, 0.35f), 1.3f, 0.16f, 1.5f, 0.16f, 1.2f, 0.0f, 0.75f, 0.0f); // flaque de lumière
            c.Polygon(Silhouette, 1.36f, 0.16f, 1.44f, 0.16f, 0.98f, 0.02f, 0.88f, 0.02f);     // ombre qui avance
            c.Rect(1.37f, 0.16f, 1.43f, 0.62f, Silhouette);                                   // quelqu'un dans la porte
            c.Circle(1.4f, 0.67f, 0.035f, Silhouette);
            DrawChild(c, 0.55f, 1.3f);
        }

        // Plan 2 : le père lève le bras ; l'enfant au sol devant lui
        private static void DrawFatherRaisesArm(Canvas c)
        {
            float x = 1.15f;
            c.Circle(x, 0.8f, 0.06f, Silhouette);                                              // tête
            c.Polygon(Silhouette, x - 0.1f, 0.73f, x + 0.1f, 0.73f, x + 0.07f, 0.43f, x - 0.07f, 0.43f); // torse
            c.Polygon(Silhouette, x - 0.07f, 0.44f, x - 0.01f, 0.44f, x - 0.03f, 0.16f, x - 0.1f, 0.16f); // jambes
            c.Polygon(Silhouette, x + 0.01f, 0.44f, x + 0.07f, 0.44f, x + 0.11f, 0.16f, x + 0.04f, 0.16f);
            c.Line(x - 0.08f, 0.71f, x - 0.22f, 0.93f, 0.045f, Silhouette);                   // bras levé
            c.Line(x - 0.22f, 0.93f, x - 0.27f, 0.97f, 0.06f, Silhouette);                    // poing
            c.Line(x + 0.09f, 0.71f, x + 0.15f, 0.5f, 0.04f, Silhouette);                     // autre bras
            c.Rect(x + 0.135f, 0.36f, x + 0.175f, 0.49f, Silhouette);                         // bouteille
            c.Rect(x + 0.148f, 0.49f, x + 0.162f, 0.54f, Silhouette);
            DrawChild(c, 0.62f, 1f, false);
        }

        // Plan 3 : le chat feule devant l'enfant ; l'ombre immense du père arrive par la droite
        private static void DrawCatHisses(Canvas c)
        {
            c.Ellipse(1.75f, 0.7f, 0.32f, 0.62f, Silhouette);                                  // ombre du père
            c.Line(1.5f, 0.9f, 1.26f, 1.02f, 0.09f, Silhouette);                               // son bras

            DrawChild(c, 0.42f, 1f, true);

            float x = 0.9f, g = 0.16f;
            c.Ellipse(x, g + 0.16f, 0.13f, 0.065f, Silhouette);                                // corps
            c.Ellipse(x - 0.01f, g + 0.2f, 0.09f, 0.06f, Silhouette);                          // dos arqué
            for (int i = 0; i < 5; i++)                                                        // poils hérissés
            {
                float px = x - 0.08f + i * 0.04f;
                c.Polygon(Silhouette, px - 0.015f, g + 0.24f, px + 0.015f, g + 0.24f, px, g + 0.3f);
            }
            c.Circle(x + 0.15f, g + 0.19f, 0.05f, Silhouette);                                 // tête vers l'ombre
            c.Polygon(Silhouette, x + 0.11f, g + 0.22f, x + 0.12f, g + 0.3f, x + 0.15f, g + 0.23f); // oreilles
            c.Polygon(Silhouette, x + 0.16f, g + 0.23f, x + 0.19f, g + 0.3f, x + 0.19f, g + 0.21f);
            c.Polygon(new Color(0.9f, 0.3f, 0.15f), x + 0.17f, g + 0.18f, x + 0.205f, g + 0.2f, x + 0.205f, g + 0.16f); // gueule ouverte
            c.Line(x - 0.12f, g + 0.17f, x - 0.2f, g + 0.36f, 0.03f, Silhouette);              // queue dressée
            foreach (float lx in new[] { x - 0.09f, x - 0.05f, x + 0.05f, x + 0.09f })         // pattes tendues
            {
                c.Line(lx, g + 0.13f, lx + 0.01f, g, 0.022f, Silhouette);
            }
        }
    }
}
