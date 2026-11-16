using ParryEverything.Boss;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Core
{
    // Outils de debug, actifs seulement dans l'éditeur et les builds de développement.
    // F1 : soigner   F2 : invincible   F3 : aller au boss   F4 : panneau (état, âme, membres, fenêtres de parade)
    // F5 : âme → boule → debout (2 pattes) → âme   F6 : tuer le boss. Touches définies dans GameInput.
    public class DebugTools : MonoBehaviour
    {
        [SerializeField] private float bossApproachDistance = 8f;   // distance au boss après F3 (il s'active sous 16)
        [SerializeField] private float testFleshLife = 80f;         // vie de la chair donnée par F5
        [SerializeField] private float testLegLife = 40f;           // vie des pattes données par F5
        [SerializeField] private bool overlayOnStart;                // panneau visible dès le lancement

        private PlayerBody body;
        private PlayerController controller;
        private HellGuardian boss;
        private bool overlay;
        private string message;
        private float messageTime = -10f;
        private GUIStyle style;

        private static bool Available => Debug.isDebugBuild;

        private void Start()
        {
            body = FindFirstObjectByType<PlayerBody>();
            controller = FindFirstObjectByType<PlayerController>();
            boss = FindFirstObjectByType<HellGuardian>();
            overlay = overlayOnStart;
        }

        private void Update()
        {
            if (!Available) return;

            if (GameInput.Pressed(GameAction.DebugOverlay))
            {
                overlay = !overlay;
            }
            if (!GameFlow.IsPlaying || body == null) return;

            if (GameInput.Pressed(GameAction.DebugHeal))
            {
                body.Heal(float.MaxValue);
                body.RestoreLimbs();
                Say("Âme et membres restaurés");
            }
            if (GameInput.Pressed(GameAction.DebugToggleBody))
            {
                // Âme → boule de chair → debout sur 2 pattes → âme
                switch (body.Form)
                {
                    case PlayerForm.Soul:
                        body.GrantFlesh(Limb.Torso(testFleshLife));
                        Say("Boule de chair");
                        break;
                    case PlayerForm.Ball:
                        body.AttachLimbs(Limb.CatLegs(testLegLife));
                        Say("Debout : 2 pattes");
                        break;
                    default:
                        body.RemoveAllLimbs();
                        Say("Âme seule");
                        break;
                }
            }
            if (GameInput.Pressed(GameAction.DebugKillBoss) && boss != null && !boss.IsDead)
            {
                boss.DebugKill();
                Say("Boss tué");
            }
            if (GameInput.Pressed(GameAction.DebugGodMode))
            {
                body.Invulnerable = !body.Invulnerable;
                Say(body.Invulnerable ? "Invincible : oui" : "Invincible : non");
            }
            if (GameInput.Pressed(GameAction.DebugToBoss) && boss != null && GameFlow.Instance != null)
            {
                Vector3 p = body.transform.position;
                p.x = boss.transform.position.x - bossApproachDistance;
                GameFlow.Instance.PlacePlayer(p);
                Say("Téléporté devant le boss");
            }
        }

        private void Say(string text)
        {
            message = text;
            messageTime = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (!Available || Event.current.type != EventType.Repaint) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, clipping = TextClipping.Overflow };

            Color old = GUI.color;
            float x = 12f, y = 12f;

            // Message bref après une action
            if (Time.unscaledTime - messageTime < 1.5f)
            {
                GuiDraw.ShadowLabel(new Rect(x, Screen.height - 40f, 400f, 24f), message, style, Color.yellow);
            }
            if (!overlay)
            {
                GUI.color = old;
                return;
            }

            int limbCount = body != null ? body.Limbs.Count : 0;
            GuiDraw.FilledRect(new Rect(x - 6f, y - 6f, 470f, 170f + 20f * limbCount), new Color(0f, 0f, 0f, 0.6f));
            GUI.color = Color.white;

            Line(ref y, x, "État : " + (GameFlow.Instance != null ? GameFlow.Instance.State.ToString() : "sans GameFlow"));
            if (body != null)
            {
                Line(ref y, x, $"Âme : {body.SoulRatio * 100f:0}%" + (body.Invulnerable ? "   (invincible)" : ""));
                foreach (Limb limb in body.Limbs)
                {
                    Bar(ref y, x, limb.IsTorn ? "arrachée" : "membre", limb.LifeRatio, new Color(0.85f, 0.2f, 0.2f));
                }
            }
            if (controller != null)
            {
                // Fenêtre de parade : jaune quand elle est ouverte ; attente : gris
                float window = controller.ParryWindow > 0f ? controller.ParryTimeLeft / controller.ParryWindow : 0f;
                Bar(ref y, x, "Parade", window, new Color(1f, 0.85f, 0.1f));
                float cooldown = controller.ParryTimeLeft > 0f || controller.ParryCooldown <= 0f
                    ? 0f : controller.ParryCooldownLeft / controller.ParryCooldown;
                Bar(ref y, x, "Attente", cooldown, Color.gray);
            }
            if (boss != null)
            {
                Line(ref y, x, $"Boss : {(boss.IsDead ? "mort" : boss.IsActive ? "phase " + boss.Phase : "en attente")}");
            }
            Line(ref y, x, $"{1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f):0} images/s");
            Line(ref y, x, "F1 soin  F2 invincible  F3 boss  F4 panneau  F5 corps  F6 tuer boss");

            GUI.color = old;
        }

        private void Line(ref float y, float x, string text)
        {
            GuiDraw.ShadowLabel(new Rect(x, y, 300f, 20f), text, style, Color.white);
            y += 20f;
        }

        private void Bar(ref float y, float x, string label, float ratio, Color color)
        {
            GuiDraw.ShadowLabel(new Rect(x, y, 70f, 20f), label, style, Color.white);
            GuiDraw.FilledRect(new Rect(x + 70f, y + 5f, 180f, 10f), new Color(1f, 1f, 1f, 0.15f));
            GuiDraw.FilledRect(new Rect(x + 70f, y + 5f, 180f * Mathf.Clamp01(ratio), 10f), color);
            GUI.color = Color.white;
            y += 20f;
        }
    }
}
