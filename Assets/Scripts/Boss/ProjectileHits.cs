using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Contacts des projectiles du boss, testés sur le trajet de l'image (un projectile rapide ne saute pas
    // par-dessus sa cible entre deux images) :
    //   - joueur : distance au volume réel du joueur (sa boîte de collision), pas à son origine (ses pieds) ;
    //   - monde : sol, murs, objets du décor (tout collider non déclencheur qui n'est ni le joueur ni le boss).
    // Shatter : le projectile se brise (éclats de roche ou de poterie, poussière, petite secousse).
    public static class ProjectileHits
    {
        private static readonly RaycastHit[] hits = new RaycastHit[16];
        private static readonly Color DustColor = new Color(0.6f, 0.56f, 0.5f);

        // Le segment from → to (rayon 'radius') touche-t-il le joueur ? (plan de jeu : la profondeur est ignorée)
        public static bool TouchesPlayer(PlayerController player, Vector3 from, Vector3 to, float radius)
        {
            if (player == null) return false;
            Bounds b = player.HitBounds;
            from.z = to.z = b.center.z;
            float len = Vector3.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.05f, radius * 0.5f)));
            float r2 = radius * radius;
            for (int i = 0; i <= steps; i++)
            {
                if (b.SqrDistance(Vector3.Lerp(from, to, i / (float)steps)) < r2) return true;
            }
            return false;
        }

        // Le segment from → to (sphère de rayon 'radius') heurte-t-il le monde ? point : où il se brise.
        // groundY : le sol compte même sans collider à cet endroit.
        public static bool HitsWorld(Vector3 from, Vector3 to, float radius, float groundY, out Vector3 point)
        {
            point = to;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len > 1e-5f)
            {
                int n = Physics.SphereCastNonAlloc(from, radius, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    RaycastHit h = hits[i];
                    // Déjà en contact au départ (distance 0) : on l'ignore, sinon un projectile lancé contre un
                    // collider se briserait dans la main
                    if (h.distance <= 0f || IsActor(h.collider)) continue;
                    if (h.distance < best)
                    {
                        best = h.distance;
                        point = h.point;
                    }
                }
                if (best < float.MaxValue) return true;
            }
            if (to.y - radius * 0.5f <= groundY)
            {
                point = new Vector3(to.x, groundY, to.z);
                return true;
            }
            return false;
        }

        // Joueur, boss, autres projectiles : ne brisent pas un projectile
        private static bool IsActor(Collider c)
        {
            return c.GetComponentInParent<PlayerController>() != null
                || c.GetComponentInParent<HellGuardian>() != null
                || c.GetComponentInParent<BossProjectile>() != null
                || c.GetComponentInParent<RallyBubble>() != null
                || c.GetComponentInParent<PushBubble>() != null;
        }

        // Le projectile se brise : éclats de sa matière, poussière, petite secousse
        public static void Shatter(Vector3 at, float size, ProjectileLook look, Color glow)
        {
            ParticleSystem.MinMaxGradient chips = look != null
                ? look.ChipColors
                : new ParticleSystem.MinMaxGradient(glow, Color.white);
            int count = Mathf.RoundToInt(10 + size * 14);
            ParticleBurst.Spawn("ProjectileShatter", at, chips, count,
                lifetime: new Vector2(0.4f, 0.9f),
                speed: new Vector2(2f, 6f + size * 2f),
                size: new Vector2(0.06f, 0.08f + size * 0.14f),
                gravity: 1.6f,
                radius: size * 0.3f);
            ParticleBurst.Spawn("ProjectileDust", at, new ParticleSystem.MinMaxGradient(DustColor, new Color(0.8f, 0.77f, 0.7f, 0.6f)), Mathf.RoundToInt(6 + size * 6),
                lifetime: new Vector2(0.3f, 0.7f),
                speed: new Vector2(0.5f, 2f),
                size: new Vector2(0.15f, 0.35f + size * 0.2f),
                gravity: -0.1f,
                radius: size * 0.4f);
            GameCamera.ShakeMain(0.12f, Mathf.Clamp(0.03f + size * 0.04f, 0.03f, 0.12f));
        }
    }
}
