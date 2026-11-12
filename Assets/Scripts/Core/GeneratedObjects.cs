using System.Collections.Generic;
using UnityEngine;

namespace ParryEverything.Core
{
    // Objets créés en code (visuels, meshes, matériaux) : jamais sauvegardés dans la scène,
    // ni affichés dans la Hierarchy, et détruits ensemble quand le composant est désactivé.
    public sealed class GeneratedObjects
    {
        private readonly List<Object> objects = new List<Object>();

        public T Track<T>(T obj) where T : Object
        {
            obj.hideFlags = HideFlags.HideAndDontSave;
            objects.Add(obj);
            return obj;
        }

        public void DestroyAll()
        {
            foreach (Object obj in objects)
            {
                if (obj != null)
                {
                    SafeDestroy(obj);
                }
            }
            objects.Clear();
        }

        // Destroy en Play, DestroyImmediate dans l'éditeur hors Play
        public static void SafeDestroy(Object obj)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
