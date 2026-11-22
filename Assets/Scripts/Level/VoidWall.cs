using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Level
{
    // Le Néant : quand le chat passe 'triggerX', un mur de ténèbres part de 'startX' et avance vers la droite.
    // Son bord touche le chat : dégâts, sauf s'il pare — chaque parade le repousse. Il s'arrête à 'endX' (le
    // sanctuaire) et se dissipe. L'objet porte le mur ; son x est piloté ici.
    [ExecuteAlways]
    public class VoidWall : MonoBehaviour
    {
        [SerializeField] private float triggerX = -46f;
        [SerializeField] private float startX = -56f;
        [SerializeField] private float endX = -26f;
        [SerializeField] private float speed = 2.9f;
        [SerializeField] private float pushBack = 4.5f;
        [SerializeField] private float damage = 12f;
        [SerializeField] private float height = 12f;

        private readonly GeneratedObjects generated = new GeneratedObjects();
        private enum State { Waiting, Chasing, Fading, Done }
        private State state;
        private float x;
        private float hitCooldown;
        private float fade = 1f;
        private Material mat;

        private void OnEnable()
        {
            generated.DestroyAll();
            mat = generated.Track(MaterialFactory.Unlit(Color.black));
            Mesh box = generated.Track(LowPoly.Box());
            // Une masse noire, et un bord déchiqueté de blocs qui ondulent
            LevelPlayer.Part(generated, transform, box, mat, new Vector3(-6f, height / 2f - 1f, -0.6f), new Vector3(12f, height, 3f));
            for (int i = 0; i < 10; i++)
            {
                LevelPlayer.Part(generated, transform, box, mat, new Vector3(-0.2f + (i % 2) * 0.35f, i * height / 10f - 0.6f, -0.6f), new Vector3(0.8f, height / 10f + 0.1f, 3f), new Vector3(0f, 0f, (i % 3 - 1) * 15f));
            }
            x = startX;
            state = State.Waiting;
            // Caché jusqu'au déclenchement (visible dans l'éditeur pour le placer)
            fade = Application.isPlaying ? 0f : 0.08f;
            Place();
        }

        private void OnDisable() => generated.DestroyAll();

        private void Place()
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            transform.localScale = new Vector3(1f, fade, 1f);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            PlayerController p = LevelPlayer.Get();
            switch (state)
            {
                case State.Waiting:
                    if (LevelPlayer.Active(p) && p.transform.position.x > triggerX)
                    {
                        state = State.Chasing;
                        GameCamera.ShakeMain(0.6f, 0.12f);
                    }
                    break;
                case State.Chasing:
                    fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime * 1.6f);   // il jaillit du sol
                    x += speed * Time.deltaTime;
                    hitCooldown -= Time.deltaTime;
                    if (LevelPlayer.Active(p) && hitCooldown <= 0f && p.HitBounds.min.x < x + 0.4f)
                    {
                        hitCooldown = 0.45f;
                        if (LevelPlayer.HitParried(p, damage, transform, new Vector3(x, p.HitCenter.y, 0f)))
                        {
                            x -= pushBack;   // paré : le néant recule
                        }
                        else
                        {
                            p.Knockback(new Vector3(8f, 4f, 0f));
                        }
                    }
                    if (x >= endX) state = State.Fading;
                    break;
                case State.Fading:
                    fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime * 0.8f);
                    if (fade <= 0f)
                    {
                        state = State.Done;
                        foreach (Transform c in transform) c.gameObject.SetActive(false);
                    }
                    break;
            }
            // Le bord frémit
            int k = 0;
            foreach (Transform c in transform)
            {
                if (k++ == 0) continue;
                c.localPosition = new Vector3(-0.2f + Mathf.Sin(Time.time * 6f + k) * 0.3f, c.localPosition.y, c.localPosition.z);
            }
            Place();
        }
    }
}
