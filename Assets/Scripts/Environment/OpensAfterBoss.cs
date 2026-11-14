using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Environment
{
    // Obstacle qui bloque le chemin tant que le boss n'est pas vaincu, puis s'effondre (pour de bon).
    // Ex : le bord de l'arène du Gardien, qui ouvre la descente.
    public class OpensAfterBoss : MonoBehaviour
    {
        [SerializeField] private string bossId = "HellGuardian";
        [SerializeField] private Color crumbleColor = new Color(0.35f, 0.25f, 0.22f);

        private void Start()
        {
            // Déjà vaincu au chargement : le passage est ouvert
            if (GameSession.IsBossDefeated(bossId)) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!GameSession.IsBossDefeated(bossId)) return;
            ParticleBurst.Spawn("PassageOpens", transform.position, crumbleColor, 80,
                lifetime: new Vector2(0.5f, 1.2f), speed: new Vector2(1f, 4f), size: new Vector2(0.08f, 0.25f),
                gravity: 1f, radius: 1.5f, duration: 0.5f);
            GameCamera.ShakeMain(0.4f, 0.2f);
            gameObject.SetActive(false);
        }
    }
}
