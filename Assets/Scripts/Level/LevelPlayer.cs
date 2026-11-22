using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Accès commun au joueur pour les mécaniques du niveau (« tout se pare » dans le décor).
    // Principe : un danger du décor frappe par PlayerController.TakeDamage ; s'il renvoie false, le joueur a paré
    // (étincelles et arrêt sur image déjà joués) et la mécanique réagit (renvoi, blocage, ouverture…).
    public static class LevelPlayer
    {
        private static PlayerController cached;

        public static PlayerController Get()
        {
            if (cached == null) cached = Object.FindFirstObjectByType<PlayerController>();
            return cached;
        }

        // Le joueur peut-il être touché / agir (vivant, en jeu) ?
        public static bool Active(PlayerController p) => p != null && !p.IsDead && GameFlow.IsPlaying;

        // Frappe le joueur ; renvoie true s'il a paré
        public static bool HitParried(PlayerController p, float damage, Transform source, Vector3 point)
        {
            return !p.TakeDamage(damage, source, point);
        }

        // Matériau de l'Inspector, ou une couleur unie à défaut
        public static Material Or(Material m, Color fallback, GeneratedObjects generated)
        {
            return m != null ? m : generated.Track(MaterialFactory.Lit(fallback));
        }

        // Ajoute une pièce de visuel (maillage + matériau) sous 'parent'
        public static Transform Part(GeneratedObjects generated, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            GameObject go = generated.Track(new GameObject("Part"));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
