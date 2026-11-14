using ParryEverything.Boss;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Lab
{
    // Scène AnimationLab : regarder les animations du chat et du boss une par une.
    // Panneau à gauche :
    //  - Temps  : pause, image par image, ralenti (n'écrase pas les arrêts du temps du jeu) ;
    //  - Chat   : forme (âme, boule, debout, une patte), marche / course auto en aller-retour, saut, parade, coup reçu ;
    //  - Boss   : phase I à V, chaque geste, rejouer en boucle ;
    //  - Caméra : cible et angle (clic droit : tourner, molette : zoom, clic molette : décaler).
    // Le chat reste jouable au clavier ; il est invincible. Les réglages des scripts se changent en direct dans l'Inspector.
    public class AnimationLab : MonoBehaviour
    {
        [SerializeField] private float testFleshLife = 80f;
        [SerializeField] private float testLegLife = 40f;
        [SerializeField] private float hurtDamage = 6f;
        [SerializeField] private float walkRange = 4f;          // aller-retour de la marche auto autour du départ
        [SerializeField] private float panelWidth = 320f;

        private enum Focus { Cat, Boss, Both }

        private static readonly float[] Speeds = { 0.05f, 0.1f, 0.25f, 0.5f, 1f };
        private static readonly string[] SpeedLabels = { "×0.05", "×0.1", "×0.25", "×0.5", "×1" };
        private static readonly string[] PhaseLabels = { "I", "II", "III", "IV", "V" };
        private static readonly (HellGuardian.LabMove move, string label)[] BossMoves =
        {
            (HellGuardian.LabMove.Swing, "Faux"),
            (HellGuardian.LabMove.Orbs, "Orbes"),
            (HellGuardian.LabMove.Slam, "Écrasement"),
            (HellGuardian.LabMove.Pillars, "Piliers"),
            (HellGuardian.LabMove.MeteorRain, "Météores"),
            (HellGuardian.LabMove.Dash, "Charge"),
            (HellGuardian.LabMove.Judgment, "Jugement"),
            (HellGuardian.LabMove.Roar, "Rugissement"),
            (HellGuardian.LabMove.Stagger, "Chancelle"),
            (HellGuardian.LabMove.PhaseTransition, "Transition"),
            (HellGuardian.LabMove.Hit, "Touché"),
            (HellGuardian.LabMove.Die, "Mort"),
        };

        private PlayerBody body;
        private PlayerController controller;
        private HellGuardian boss;
        private LabCamera cam;
        private Vector3 playerHome;
        private int walkMode;           // 0 : arrêt, 1 : marche, 2 : course
        private float walkDir = 1f;
        private bool paused;
        private bool stepping;
        private int speedIndex = Speeds.Length - 1;
        private int bossPhase;
        private bool loop;
        private int lastMove = -1;
        private Focus focus = Focus.Cat;
        private Vector2 scroll;
        private GUIStyle header;

        private void Start()
        {
            body = FindFirstObjectByType<PlayerBody>();
            controller = FindFirstObjectByType<PlayerController>();
            boss = FindFirstObjectByType<HellGuardian>();
            cam = FindFirstObjectByType<LabCamera>();
            if (body != null)
            {
                body.Invulnerable = true;
                playerHome = body.transform.position;
            }
            if (boss != null && !boss.IsLab) Debug.LogWarning("AnimationLab : cocher « Lab Mode » sur le boss.", boss);
            SetFocus(Focus.Cat);
        }

        private void OnDisable()
        {
            GameInput.MoveOverride = null;
            GameInput.SimulateHold(GameAction.Run, false);
            TimeControl.SetInspectScale(1f);
            TimeControl.SetPaused(false);
        }

        private void Update()
        {
            // Image par image : le temps a avancé d'une image, on refige
            if (stepping)
            {
                stepping = false;
                TimeControl.SetPaused(true);
            }

            UpdateAutoWalk();

            if (loop && lastMove >= 0 && boss != null && !boss.LabBusy && !boss.IsDead)
            {
                boss.LabPlay(BossMoves[lastMove].move);
            }
            if (cam != null) cam.SetFocus(FocusPoint());
        }

        // Aller-retour autour du point de départ
        private void UpdateAutoWalk()
        {
            if (controller == null || walkMode == 0)
            {
                GameInput.MoveOverride = null;
                GameInput.SimulateHold(GameAction.Run, false);
                return;
            }
            float x = controller.transform.position.x - playerHome.x;
            if (x > walkRange) walkDir = -1f;
            else if (x < -walkRange) walkDir = 1f;
            GameInput.MoveOverride = walkDir;
            GameInput.SimulateHold(GameAction.Run, walkMode == 2);
        }

        private Vector3 FocusPoint()
        {
            Vector3 cat = controller != null ? controller.transform.position + Vector3.up * 0.4f : Vector3.zero;
            if (boss == null) return cat;
            switch (focus)
            {
                case Focus.Boss: return boss.CorePosition;
                case Focus.Both: return (cat + boss.CorePosition) * 0.5f;
                default: return cat;
            }
        }

        private void SetFocus(Focus f)
        {
            focus = f;
            if (cam == null) return;
            cam.Frame(f == Focus.Cat ? 5f : f == Focus.Boss ? 14f : 20f);
        }

        // =====================================================================
        //  Panneau
        // =====================================================================

        private void OnGUI()
        {
            if (header == null)
            {
                header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            }
            Rect area = new Rect(8f, 8f, panelWidth, Screen.height - 16f);
            GuiDraw.FilledRect(area, new Color(0f, 0f, 0f, 0.55f));
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            scroll = GUILayout.BeginScrollView(scroll);

            TimePanel();
            CatPanel();
            BossPanel();
            CameraPanel();

            GUILayout.Space(6f);
            GUILayout.Label("Clavier : flèches, Espace, X (parade), Maj (course).\n" +
                            "Réglages en direct : sélectionner player ou boss dans la Hierarchy.");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void TimePanel()
        {
            GUILayout.Label("TEMPS", header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Reprendre" : "Pause"))
            {
                paused = !paused;
                stepping = false;
                TimeControl.SetPaused(paused);
            }
            GUI.enabled = paused;
            if (GUILayout.Button("Image +1"))
            {
                stepping = true;
                TimeControl.SetPaused(false);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            int s = GUILayout.Toolbar(speedIndex, SpeedLabels);
            if (s != speedIndex)
            {
                speedIndex = s;
                TimeControl.SetInspectScale(Speeds[s]);
            }
        }

        private void CatPanel()
        {
            if (body == null) return;
            GUILayout.Space(8f);
            GUILayout.Label($"CHAT — {body.Form}", header);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Âme")) SetForm(0);
            if (GUILayout.Button("Boule")) SetForm(1);
            if (GUILayout.Button("Debout")) SetForm(2);
            if (GUILayout.Button("1 patte")) SetForm(3);
            GUILayout.EndHorizontal();

            walkMode = GUILayout.Toolbar(walkMode, new[] { "Immobile", "Marche", "Course" });

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Saut")) GameInput.SimulatePress(GameAction.Jump);
            if (GUILayout.Button("Parade")) GameInput.SimulatePress(GameAction.Parry);
            if (GUILayout.Button("Coup reçu")) Hurt();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Soigner"))
            {
                body.Heal(float.MaxValue);
                body.RestoreLimbs();
            }
            if (GUILayout.Button("Replacer"))
            {
                walkMode = 0;
                if (GameFlow.Instance != null) GameFlow.Instance.PlacePlayer(playerHome);
                else body.transform.position = playerHome;
            }
            GUILayout.EndHorizontal();
        }

        // 0 âme, 1 boule, 2 debout, 3 debout sur une patte
        private void SetForm(int form)
        {
            body.RemoveAllLimbs();
            if (form >= 1) body.GrantFlesh(Limb.Flesh(testFleshLife));
            if (form >= 2) body.AttachLimbs(Limb.CatLegs(testLegLife));
            if (form == 3)
            {
                foreach (Limb limb in body.Limbs)
                {
                    if (limb.Kind != LimbKind.Leg) continue;
                    body.TearLimb(limb);
                    break;
                }
            }
        }

        // Coup reçu : l'invincibilité est levée le temps du coup
        private void Hurt()
        {
            if (controller == null) return;
            body.Invulnerable = false;
            controller.TakeDamage(hurtDamage, boss != null ? boss.transform : null, controller.transform.position);
            body.Invulnerable = true;
        }

        private void BossPanel()
        {
            if (boss == null) return;
            GUILayout.Space(8f);
            GUILayout.Label("BOSS" + (boss.LabBusy ? " — en cours" : ""), header);

            int p = GUILayout.Toolbar(bossPhase, PhaseLabels);
            if (p != bossPhase)
            {
                bossPhase = p;
                boss.LabSetPhase(p);
            }

            for (int i = 0; i < BossMoves.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, BossMoves.Length); j++)
                {
                    if (GUILayout.Button(BossMoves[j].label))
                    {
                        lastMove = j;
                        boss.LabPlay(BossMoves[j].move);
                    }
                }
                GUILayout.EndHorizontal();
            }
            loop = GUILayout.Toggle(loop, " Rejouer le dernier geste en boucle");
        }

        private void CameraPanel()
        {
            if (cam == null) return;
            GUILayout.Space(8f);
            GUILayout.Label("CAMÉRA", header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Chat")) SetFocus(Focus.Cat);
            if (boss != null && GUILayout.Button("Boss")) SetFocus(Focus.Boss);
            if (boss != null && GUILayout.Button("Les deux")) SetFocus(Focus.Both);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Jeu")) cam.SetView(0f, 8f);
            if (GUILayout.Button("3/4")) cam.SetView(-40f, 18f);
            if (GUILayout.Button("Côté")) cam.SetView(-90f, 5f);
            if (GUILayout.Button("Dessus")) cam.SetView(0f, 80f);
            GUILayout.EndHorizontal();
        }
    }
}
