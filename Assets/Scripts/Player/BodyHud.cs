using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // HUD du corps : petite silhouette du chat (vue de face) en haut à droite, découpée en membres.
    // Chaque membre a sa forme (tête à oreilles, poitrine large aux épaules, bassin large aux hanches,
    // bras et pattes qui s'affinent, pied, queue effilée), en polygones aux coins arrondis.
    // Il se colore selon sa vie (clair → orange → rouge), clignote blanc quand il est touché,
    // est grisé s'il est arraché ; son effet (hémorragie, infection, hypertrophie) est une pastille de couleur.
    // Pas de chiffres. Un membre que le chat n'a jamais eu n'est pas dessiné. Rien en âme (pas de corps).
    public class BodyHud : MonoBehaviour
    {
        [SerializeField] private float heightAt1080p = 230f;      // hauteur de la silhouette (pixels en 1080p)
        [SerializeField] private float margin = 24f;
        [SerializeField] private Color healthy = new Color(0.78f, 0.84f, 0.95f);
        [SerializeField] private Color hurt = new Color(1f, 0.55f, 0.15f);
        [SerializeField] private Color critical = new Color(0.85f, 0.08f, 0.08f);
        [SerializeField] private Color tornColor = new Color(0.25f, 0.25f, 0.3f, 0.45f);
        [SerializeField] private Color outlineColor = new Color(0.03f, 0.03f, 0.06f, 0.85f);
        [SerializeField] private float hitFlashTime = 0.35f;

        // Formes : polygones dans une boîte 0..1 (x vers la droite, y vers le bas), coins arrondis (part de la largeur)
        private sealed class Shape
        {
            public readonly Vector2[] points;
            public readonly float rounding;
            public Shape(float rounding, params float[] xy)
            {
                this.rounding = rounding;
                points = new Vector2[xy.Length / 2];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            }
        }

        // Tête : crâne large aux joues, deux oreilles pointues
        private static readonly Shape HeadShape = new Shape(0.1f,
            0.06f, 0f, 0.34f, 0.3f, 0.66f, 0.3f, 0.94f, 0f, 0.98f, 0.5f, 1f, 0.72f, 0.8f, 1f, 0.2f, 1f, 0f, 0.72f, 0.02f, 0.5f);
        private static readonly Shape ChestShape = new Shape(0.22f, 0f, 0f, 1f, 0f, 0.86f, 1f, 0.14f, 1f);      // épaules
        private static readonly Shape BellyShape = new Shape(0.3f, 0.04f, 0f, 0.96f, 0f, 0.96f, 1f, 0.04f, 1f);
        private static readonly Shape PelvisShape = new Shape(0.3f, 0.1f, 0f, 0.9f, 0f, 1f, 1f, 0f, 1f);        // hanches
        private static readonly Shape ArmShape = new Shape(0.45f, 0f, 0f, 1f, 0f, 0.85f, 1f, 0.15f, 1f);
        private static readonly Shape ForearmShape = new Shape(0.45f, 0.05f, 0f, 0.95f, 0f, 0.8f, 1f, 0.2f, 1f);
        private static readonly Shape HandShape = new Shape(0.5f, 0.1f, 0f, 0.9f, 0f, 1f, 0.6f, 0.8f, 1f, 0.2f, 1f, 0f, 0.6f);
        private static readonly Shape ThighShape = new Shape(0.4f, 0f, 0f, 1f, 0f, 0.82f, 1f, 0.18f, 1f);
        private static readonly Shape ShinShape = new Shape(0.45f, 0.05f, 0f, 0.95f, 0f, 0.8f, 1f, 0.2f, 1f);
        private static readonly Shape FootShape = new Shape(0.45f, 0.1f, 0f, 0.75f, 0f, 1f, 0.55f, 1f, 1f, 0f, 1f, 0f, 0.4f);
        private static readonly Shape TailShape = new Shape(0.5f, 0f, 0f, 1f, 0f, 0.7f, 1f, 0.3f, 1f);          // base en haut, pointe en bas

        // Place de chaque membre dans la silhouette (unités : boîte de 100 × 200, origine en haut à gauche) :
        // centre, taille, rotation (degrés). Côté proche à gauche de l'image, côté loin à droite (forme retournée).
        private struct Slot
        {
            public Vector2 center, size;
            public float angle;
            public Shape shape;
            public Slot(Shape shape, float x, float y, float w, float h, float a = 0f)
            {
                this.shape = shape;
                center = new Vector2(x, y);
                size = new Vector2(w, h);
                angle = a;
            }
        }

        // Membres séparés par un vide (≥ 4 unités, plus que les deux contours) : aucun ne chevauche son voisin
        private static readonly Slot HeadSlot = new Slot(HeadShape, 50f, 19f, 36f, 34f);
        private static readonly Slot ChestSlot = new Slot(ChestShape, 50f, 55f, 40f, 30f);
        private static readonly Slot BellySlot = new Slot(BellyShape, 50f, 84.5f, 34f, 21f);
        private static readonly Slot PelvisSlot = new Slot(PelvisShape, 50f, 107.5f, 38f, 17f);
        private static readonly Slot ArmSlot = new Slot(ArmShape, 20f, 51.5f, 12f, 23f);        // haut du bras
        private static readonly Slot ForearmSlot = new Slot(ForearmShape, 20f, 75.5f, 11f, 19f);
        private static readonly Slot HandSlot = new Slot(HandShape, 20f, 92f, 12f, 9f);
        private static readonly Slot ThighSlot = new Slot(ThighShape, 39f, 133.5f, 16f, 27f);
        private static readonly Slot ShinSlot = new Slot(ShinShape, 39f, 163f, 11f, 24f);
        private static readonly Slot FootSlot = new Slot(FootShape, 38.5f, 184f, 17f, 10f);
        private static readonly Slot TailSlot = new Slot(TailShape, 78f, 125f, 8f, 50f);   // pend à côté de la patte loin

        private PlayerBody body;
        private Texture2D dot;
        private int textureHeight;      // hauteur d'écran des textures en cache (refaites si elle change)
        private readonly Dictionary<(Shape, int, int, bool), Texture2D> textures = new Dictionary<(Shape, int, int, bool), Texture2D>();
        private readonly Dictionary<Limb, float> lastLife = new Dictionary<Limb, float>();
        private readonly Dictionary<Limb, float> hitTime = new Dictionary<Limb, float>();

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
        }

        private void OnDestroy()
        {
            if (dot != null) Destroy(dot);
            foreach (Texture2D tex in textures.Values) Destroy(tex);
            textures.Clear();
        }

        private void Update()
        {
            if (body == null) return;
            // Membre touché : sa vie a baissé depuis la dernière image
            foreach (Limb limb in body.Limbs)
            {
                if (lastLife.TryGetValue(limb, out float before) && limb.Life < before - 0.001f) hitTime[limb] = Time.unscaledTime;
                lastLife[limb] = limb.Life;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (body == null || !body.HasBody || !GameFlow.IsPlaying) return;
            if (textureHeight != Screen.height)
            {
                foreach (Texture2D tex in textures.Values) Destroy(tex);
                textures.Clear();
                textureHeight = Screen.height;
            }
            if (dot == null) dot = MakeTexture(new Shape(1f, 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f), 16, 16, 0f);   // disque

            float unit = heightAt1080p * Screen.height / 1080f / 200f;
            float pad = margin * Screen.height / 1080f;
            var origin = new Vector2(Screen.width - pad - 100f * unit, pad);

            // Queue derrière, puis tronc, tête, bras et pattes
            foreach (Limb limb in body.Limbs)
            {
                if (limb.Kind == LimbKind.Tail) Draw(limb, TailSlot, false, origin, unit);
            }
            foreach (Limb limb in body.Limbs)
            {
                bool far = limb.Anchor.z > 0f;
                switch (limb.Kind)
                {
                    case LimbKind.Chest: Draw(limb, ChestSlot, false, origin, unit); break;
                    case LimbKind.Flesh: Draw(limb, BellySlot, false, origin, unit); break;
                    case LimbKind.Pelvis: Draw(limb, PelvisSlot, false, origin, unit); break;
                    case LimbKind.Head: Draw(limb, HeadSlot, false, origin, unit); break;
                    case LimbKind.Arm: Draw(limb, ArmSlot, far, origin, unit); break;
                    case LimbKind.Forearm: Draw(limb, ForearmSlot, far, origin, unit); break;
                    case LimbKind.Hand: Draw(limb, HandSlot, far, origin, unit); break;
                    case LimbKind.Leg: Draw(limb, ThighSlot, far, origin, unit); break;
                    case LimbKind.Shin: Draw(limb, ShinSlot, far, origin, unit); break;
                    case LimbKind.Foot: Draw(limb, FootSlot, far, origin, unit); break;
                }
            }
        }

        // Un membre : contour sombre, remplissage selon la vie, pastille d'effet
        private void Draw(Limb limb, Slot slot, bool mirror, Vector2 origin, float unit)
        {
            Vector2 c = slot.center;
            float angle = slot.angle;
            if (mirror)
            {
                c.x = 100f - c.x;
                angle = -angle;
            }
            Vector2 center = origin + c * unit;
            Vector2 size = slot.size * unit;
            int outline = Mathf.Max(1, Mathf.RoundToInt(1.6f * unit));

            Matrix4x4 old = GUI.matrix;
            if (angle != 0f) GUIUtility.RotateAroundPivot(angle, center);
            int w = Mathf.Max(4, Mathf.RoundToInt(size.x)), h = Mathf.Max(4, Mathf.RoundToInt(size.y));
            // Texture du contour : la forme grossie de « outline » pixels, dans une boîte plus grande d'autant
            Texture2D border = Texture(slot.shape, w + 2 * outline, h + 2 * outline, outline);
            Texture2D fill = Texture(slot.shape, w, h, 0f);
            Image(border, new Rect(center.x - w * 0.5f - outline, center.y - h * 0.5f - outline, w + 2 * outline, h + 2 * outline), outlineColor, mirror);
            Image(fill, new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h), FillColor(limb), mirror);
            GUI.matrix = old;

            if (!limb.IsTorn && limb.Effect != LimbEffect.None)
            {
                float d = Mathf.Min(Mathf.Max(6f, 8f * unit), Mathf.Min(size.x, size.y) * 0.7f);
                Vector2 at = center;   // au milieu du membre : ne déborde pas sur le voisin
                Image(dot, new Rect(at.x - d * 0.5f - 1f, at.y - d * 0.5f - 1f, d + 2f, d + 2f), outlineColor, false);
                Image(dot, new Rect(at.x - d * 0.5f, at.y - d * 0.5f, d, d), EffectColor(limb.Effect), false);
            }
        }

        private Color FillColor(Limb limb)
        {
            if (limb.IsTorn) return tornColor;
            float r = limb.LifeRatio;
            Color color = r > 0.5f ? Color.Lerp(hurt, healthy, (r - 0.5f) * 2f) : Color.Lerp(critical, hurt, r * 2f);
            // Vie basse : bat doucement
            if (r < 0.25f) color = Color.Lerp(color, Color.black, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)));
            if (hitTime.TryGetValue(limb, out float t))
            {
                float flash = 1f - (Time.unscaledTime - t) / hitFlashTime;
                if (flash > 0f) color = Color.Lerp(color, Color.white, flash);
            }
            return color;
        }

        private static Color EffectColor(LimbEffect effect)
        {
            switch (effect)
            {
                case LimbEffect.Bleeding: return new Color(0.9f, 0.05f, 0.1f);
                case LimbEffect.Infection: return new Color(0.45f, 0.85f, 0.2f);
                case LimbEffect.Hypertrophy: return new Color(0.7f, 0.35f, 0.95f);
                default: return Color.white;
            }
        }

        private static void Image(Texture2D texture, Rect rect, Color color, bool mirror)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, texture, mirror ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f));
            GUI.color = old;
        }

        // Texture de la forme à sa taille exacte en pixels (coins nets, sans étirement), gardée en cache
        private Texture2D Texture(Shape shape, int w, int h, float grow)
        {
            var key = (shape, w, h, grow > 0f);
            if (textures.TryGetValue(key, out Texture2D tex) && tex != null) return tex;
            tex = MakeTexture(shape, w, h, grow);
            textures[key] = tex;
            return tex;
        }

        // Polygone aux coins arrondis : distance signée au polygone rentré du rayon d'arrondi, moins ce rayon.
        // grow : la forme est dessinée dans une boîte agrandie de grow pixels de chaque côté, et grossie d'autant (contour)
        private static Texture2D MakeTexture(Shape shape, int w, int h, float grow)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            float iw = w - 2f * grow, ih = h - 2f * grow;   // taille de la forme elle-même
            float radius = shape.rounding * Mathf.Min(iw, ih) * 0.5f;
            // Sommets en pixels, rentrés vers le centre du rayon d'arrondi
            var pts = new Vector2[shape.points.Length];
            Vector2 mid = new Vector2(w * 0.5f, h * 0.5f);
            float sx = Mathf.Max(0f, iw - 2f * radius) / Mathf.Max(1f, iw), sy = Mathf.Max(0f, ih - 2f * radius) / Mathf.Max(1f, ih);
            for (int i = 0; i < pts.Length; i++)
            {
                // y de la forme vers le bas, y de la texture vers le haut
                Vector2 p = new Vector2(grow + shape.points[i].x * iw, grow + (1f - shape.points[i].y) * ih);
                pts[i] = mid + Vector2.Scale(p - mid, new Vector2(sx, sy));
            }

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = SignedDistance(new Vector2(x + 0.5f, y + 0.5f), pts) - radius - grow;
                    pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Distance signée d'un point à un polygone quelconque (négative dedans)
        private static float SignedDistance(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x)) inside = !inside;
            }
            float d = Mathf.Sqrt(best);
            return inside ? -d : d;
        }
    }
}
