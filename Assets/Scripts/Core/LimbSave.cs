using System;
using UnityEngine;

namespace ParryEverything.Core
{
    // Un membre dans la sauvegarde (voir Player/Limb)
    [Serializable]
    public class LimbSave
    {
        public int kind;              // LimbKind (0 : chair, 1 : patte)
        public string id;
        public float maxLife;
        public float life;
        public int effect;            // LimbEffect
        public Vector3 anchor;
        public bool pocketed;
    }
}
