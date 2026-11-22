using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Apparence d'un projectile du boss : caillou (roche grise, sans lueur) ou morceau du bol (maillage d'un tesson,
    // matériau de la poterie). Sans apparence (null) : l'orbe lumineuse d'origine.
    // Chaque projectile crée sa propre copie du matériau (il change de couleur quand il est paré) et la détruit.
    public sealed class ProjectileLook
    {
        private static readonly Color RockColor = new Color(0.36f, 0.34f, 0.33f);
        private static readonly Color DustColor = new Color(0.55f, 0.52f, 0.48f, 0.55f);
        private static Mesh[] rockMeshes;

        private readonly Mesh mesh;              // null : caillou au hasard
        private readonly Material template;      // null : roche unie
        private readonly float meshScale;        // taille du maillage pour un projectile de taille 1

        private ProjectileLook(Mesh mesh, Material template, float meshScale)
        {
            this.mesh = mesh;
            this.template = template;
            this.meshScale = meshScale;
        }

        // Caillou : un des cailloux partagés, tiré au hasard (le même pour la main qui le tient et le projectile)
        public static ProjectileLook Rock() => new ProjectileLook(RandomRock(), null, 1f);

        // Morceau de bol : mis à l'échelle de sa zone de contact (demi-longueur ≈ 0,45 × la taille du projectile),
        // sinon un grand tesson semble traverser ce qu'il touche
        public static ProjectileLook Shard(Mesh shard, Material pottery) =>
            shard != null ? new ProjectileLook(shard, pottery, 0.45f / Mathf.Max(0.01f, MaxExtent(shard))) : Rock();

        private static float MaxExtent(Mesh m)
        {
            Vector3 e = m.bounds.extents;
            return Mathf.Max(e.x, Mathf.Max(e.y, e.z));
        }

        public Color TrailColor => DustColor;

        // Éclats quand il se brise : roche grise, ou terre cuite et intérieur sombre (morceau du bol)
        public ParticleSystem.MinMaxGradient ChipColors => template != null
            ? new ParticleSystem.MinMaxGradient(new Color(0.74f, 0.45f, 0.28f), new Color(0.22f, 0.2f, 0.18f))
            : new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.27f, 0.26f), new Color(0.55f, 0.53f, 0.5f));

        public Mesh Mesh() => mesh != null ? mesh : RandomRock();

        private static Mesh RandomRock()
        {
            if (rockMeshes == null)
            {
                // Quelques cailloux bosselés, partagés par tous les projectiles (jamais détruits)
                rockMeshes = new Mesh[5];
                for (int i = 0; i < rockMeshes.Length; i++)
                {
                    rockMeshes[i] = LowPoly.Icosahedron(0.22f, 100 + i * 13);
                    rockMeshes[i].hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return rockMeshes[Random.Range(0, rockMeshes.Length)];
        }

        // Échelle à donner au maillage pour un projectile de diamètre 'size'
        public float Scale(float size) => size * meshScale;

        public Material CreateMaterial()
        {
            Material m = template != null ? new Material(template) : MaterialFactory.Lit(RockColor);
            m.EnableKeyword("_EMISSION");                // lueur quand il est paré
            m.SetColor("_EmissionColor", Color.black);
            return m;
        }

        public Color BaseColor(Material m) => m.HasProperty("_Color") ? m.color : RockColor;
    }
}
