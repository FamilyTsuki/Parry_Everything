using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Environment
{
    // Fin de la tranche verticale : au bas de la descente, l'écran « À suivre ».
    [RequireComponent(typeof(Collider))]
    public class SliceEnd : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerBody>() == null || GameFlow.Instance == null) return;
            GameFlow.Instance.ReachEnd();
        }
    }
}
