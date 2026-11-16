using System;
using UnityEngine;

namespace ParryEverything.Core
{
    // Un membre dans la sauvegarde (voir Player/Limb)
    [Serializable]
    public class LimbSave
    {
        public int kind;              // LimbKind : 0 ventre, 1 cuisse, 2 bras, 3 tête, 4 queue, 5 poitrine, 6 bassin, 7 jarret, 8 pied, 9 avant-bras, 10 main
        public string id;
        public string parentId;       // membre sur lequel il est cousu (version 2)
        public float maxLife;
        public float life;
        public int effect;            // LimbEffect
        public Vector3 anchor;
        public bool pocketed;
    }
}
