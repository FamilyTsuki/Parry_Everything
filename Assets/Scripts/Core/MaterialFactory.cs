using UnityEngine;

namespace ParryEverything.Core
{
    // Création des matériaux générés en code (un seul endroit pour les noms de shaders).
    public static class MaterialFactory
    {
        private const string UnlitShader = "Sprites/Default";
        private const string LitShader = "Standard";

        private static Material sharedUnlit;

        // Non éclairé, visible des deux côtés (particules, traînées, indicateurs)
        public static Material Unlit(Color color)
        {
            return new Material(Shader.Find(UnlitShader)) { color = color };
        }

        // Matériau éclairé standard
        public static Material Lit(Color color)
        {
            return new Material(Shader.Find(LitShader)) { color = color };
        }

        // Matériau d'un shader du projet (Resources/Shaders), null si le shader est introuvable
        public static Material FromShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            return shader != null ? new Material(shader) : null;
        }

        // Matériau non éclairé blanc partagé : la couleur vient des particules elles-mêmes
        public static Material SharedUnlit
        {
            get
            {
                if (sharedUnlit == null)
                {
                    sharedUnlit = Unlit(Color.white);
                }
                return sharedUnlit;
            }
        }
    }
}
