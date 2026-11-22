using System.Collections.Generic;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Boss
{
    // Bras de fer : un gros rocher (Look ; null : bulle lumineuse), toujours sur la ligne entre le boss et le joueur,
    // avance vers le joueur.
    // Chaque parade la repousse plus près du boss que la précédente (1/N du chemin, puis 2/N…) ; elle revient
    // ensuite, un peu plus vite à chaque fois. La N-ième parade l'envoie dans le boss.
    // Touche le joueur sans être parée : elle éclate sur lui.
    public class PushBubble : MonoBehaviour
    {
        private static readonly HashSet<PushBubble> active = new HashSet<PushBubble>();
        private static readonly Color ReflectColor = new Color(0.4f, 0.8f, 1f);

        public static int ActiveCount => active.Count;

        private HellGuardian owner;
        private PlayerController target;
        private float damage;
        private float reflectDamage;
        private float size;
        private float speed;            // vitesse d'avance vers le joueur
        private float speedUp;          // ajoutée à chaque parade
        private float pushSpeed;        // vitesse de recul après une parade
        private int parriesNeeded;      // parades pour l'envoyer dans le boss
        private int parries;
        private float gap;              // distance entre la bulle et le joueur
        private bool pushed;            // en train de reculer vers son point de renvoi
        private float hold;             // pause au point de renvoi avant de revenir
        private float life = 30f;
        private Color color;
        private ProjectileLook look;
        private float baseEmission = 2.5f;
        private Material mat;
        private Transform visual;

        public static PushBubble Spawn(HellGuardian owner, float damage, float reflectDamage, float size, float speed, float speedUp, float pushSpeed, int parriesNeeded, Color color, ProjectileLook look = null)
        {
            GameObject go = new GameObject("PushBubble");
            PushBubble b = go.AddComponent<PushBubble>();
            b.owner = owner;
            b.target = FindFirstObjectByType<PlayerController>();
            b.damage = damage;
            b.reflectDamage = reflectDamage;
            b.size = size;
            b.speed = speed;
            b.speedUp = speedUp;
            b.pushSpeed = pushSpeed;
            b.parriesNeeded = Mathf.Max(1, parriesNeeded);
            b.color = color;
            b.look = look;
            if (b.target != null)
                b.gap = Flat(owner.CorePosition - b.target.transform.position).magnitude - 1f;
            b.Build();
            b.Place();
            return b;
        }

        private void Build()
        {
            if (look != null)
            {
                mat = look.CreateMaterial();
                color = look.BaseColor(mat);
                baseEmission = 0f;
            }
            else
            {
                mat = MaterialFactory.Lit(color);
                mat.EnableKeyword("_EMISSION");
            }
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * (look != null ? look.Scale(size) : size);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = look != null ? look.Mesh() : LowPoly.Icosahedron(0.12f, Random.Range(0, 999));
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
            SetColor(color, baseEmission);
        }

        private void OnEnable() { active.Add(this); }
        private void OnDisable() { active.Remove(this); }

        private void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (owner == null || owner.IsDead || target == null || life <= 0f)
            {
                Burst();
                return;
            }
            visual.Rotate(60f * dt, 90f * dt, 0f);

            float lineLength = Flat(owner.CorePosition - target.HitCenter).magnitude;
            if (pushed)
            {
                // Repoussée jusqu'à son point de renvoi, de plus en plus près du boss
                float pushTarget = lineLength * parries / parriesNeeded;
                gap = Mathf.MoveTowards(gap, pushTarget, pushSpeed * dt);
                if (parries >= parriesNeeded && gap >= lineLength - 0.9f)
                {
                    // Dernière parade : elle frappe le boss
                    owner.TakeHit(reflectDamage);
                    Burst();
                    return;
                }
                if (gap >= pushTarget - 0.01f)
                {
                    pushed = false;
                    hold = 0.2f;
                }
            }
            else if (hold > 0f)
            {
                // Pause au point de renvoi : elle tremble, puis revient
                hold -= dt;
                visual.localPosition = Random.insideUnitSphere * 0.05f;
            }
            else
            {
                visual.localPosition = Vector3.zero;
                gap -= speed * dt;
                // La couleur de menace revient à mesure qu'elle approche
                SetColor(Color.Lerp(ReflectColor, color, Mathf.Clamp01(1.3f - gap / 4f)), baseEmission);
            }
            gap = Mathf.Min(gap, lineLength);
            Place();

            // Contact avec le joueur
            float radius = 0.35f + size * 0.5f;
            if (!pushed && hold <= 0f && gap <= radius)
            {
                Vector3 pp = target.HitCenter;
                if (target.TakeDamage(damage, transform, Vector3.Lerp(transform.position, pp, 0.5f)))
                {
                    Burst();
                    return;
                }
                // Parée : repoussée plus près du boss ; elle reviendra plus vite
                parries++;
                pushed = true;
                speed += speedUp;
                SetColor(ReflectColor, 3f);
                if (parries < parriesNeeded) owner.ReturnOrb();
            }
        }

        // Toujours sur la ligne joueur → boss, à 'gap' du joueur
        private void Place()
        {
            if (target == null || owner == null) return;
            // Sur la ligne corps du joueur → boss (pas ses pieds : il ne traîne pas dans le sol)
            Vector3 pp = target.HitCenter;
            Vector3 dir = Flat(owner.CorePosition - pp).normalized;
            transform.position = pp + dir * Mathf.Max(gap, 0f);
        }

        private void Burst()
        {
            if (look != null)
            {
                ProjectileHits.Shatter(transform.position, size, look, color);
                Destroy(gameObject);
                return;
            }
            Color c = mat != null ? mat.color : color;
            ParticleBurst.Spawn("PushBubbleBurst", transform.position, new ParticleSystem.MinMaxGradient(c, Color.white), 24,
                lifetime: new Vector2(0.4f, 0.9f),
                speed: new Vector2(2f, 7f),
                size: new Vector2(0.08f, 0.25f),
                gravity: 0.5f,
                radius: size * 0.4f);
            Destroy(gameObject);
        }

        private void SetColor(Color c, float emission)
        {
            mat.color = c;
            mat.SetColor("_EmissionColor", c * emission);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.z = 0f;
            return v;
        }

        private void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
