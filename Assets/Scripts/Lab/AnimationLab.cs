using ParryEverything.Boss;
using ParryEverything.Core;
using ParryEverything.Player;
using UnityEngine;

namespace ParryEverything.Lab
{
    // Scène AnimationLab : regarder les animations du chat et du boss une par une.
    // Panneau à gauche :
    //  - Temps  : pause, image par image, ralenti (n'écrase pas les arrêts du temps du jeu) ;
    //  - Chat   : forme (âme, boule, debout, une patte, complet), bras / tête / queue un par un,
    //             allure sur place (tapis roulant : de l'arrêt à très vite), saut, parade, coup reçu,
    //             vie et effet de chaque membre (pour voir fourrure, chair cousue, hémorragie, infection, hypertrophie) ;
    //  - Boss   : phase I à V, chaque geste, rejouer en boucle ;
    //  - Caméra : cible et angle (clic droit : tourner, molette : zoom, clic molette : décaler).
    // Le chat reste jouable au clavier ; il est invincible. Les réglages des scripts se changent en direct dans l'Inspector.
    public class AnimationLab : MonoBehaviour
    {
        [SerializeField] private float testFleshLife = 80f;
        [SerializeField] private float testLegLife = 40f;
        [SerializeField] private float testPartLife = 30f;      // bras, tête, queue
        [SerializeField] private float hurtDamage = 6f;
        [SerializeField] private float maxTreadmillSpeed = 20f; // allure maximale du curseur (m/s ; course du jeu : 6)
        [SerializeField] private float panelWidth = 320f;

        private enum Focus { Cat, Boss, Both }

        private static readonly float[] Speeds = { 0.05f, 0.1f, 0.25f, 0.5f, 1f };
        private static readonly string[] SpeedLabels = { "×0.05", "×0.1", "×0.25", "×0.5", "×1" };
        private static readonly string[] PhaseLabels = { "I", "II", "III", "IV", "V" };
        private static readonly string[] EffectLabels = { "Aucun", "Hémorr.", "Infect.", "Hypertr." };   // ordre de LimbEffect
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
        private CatBody catBody;
        private HellGuardian boss;
        private LabCamera cam;
        private Vector3 playerHome;
        private float treadmillSpeed;   // allure sur place (m/s), 0 : arrêt (le chat reste jouable au clavier)
        private bool paused;
        private bool stepping;
        private int speedIndex = Speeds.Length - 1;
        private int bossPhase;
        private bool loop;
        private int lastMove = -1;
        private Focus focus = Focus.Cat;
        private Vector2 scroll;
        private GUIStyle header;
        private bool collapsed;         // panneau replié (pour voir toute la scène)

        private void Start()
        {
            body = FindFirstObjectByType<PlayerBody>();
            controller = FindFirstObjectByType<PlayerController>();
            catBody = FindFirstObjectByType<CatBody>();
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
            if (catBody != null) catBody.TreadmillSpeed = null;
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

            if (catBody != null) catBody.TreadmillSpeed = treadmillSpeed > 0f ? treadmillSpeed : (float?)null;

            if (loop && lastMove >= 0 && boss != null && !boss.LabBusy && !boss.IsDead)
            {
                boss.LabPlay(BossMoves[lastMove].move);
            }
            if (cam != null) cam.SetFocus(FocusPoint());
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
            // Vue Game étroite : tout le panneau rétrécit pour tenir dans la moitié de la largeur (jusqu'à 55 %)
            float scale = Mathf.Clamp((Screen.width * 0.5f - 16f) / panelWidth, 0.55f, 1f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = Screen.height / scale - 16f;

            // Replié : un seul bouton pour le rouvrir
            if (collapsed)
            {
                if (GUI.Button(new Rect(8f, 8f, 90f, 26f), "Panneau >>")) collapsed = false;
                GUI.matrix = oldMatrix;
                return;
            }

            Rect area = new Rect(8f, 8f, panelWidth, height);
            GuiDraw.FilledRect(area, new Color(0f, 0f, 0f, 0.55f));
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            if (GUILayout.Button("<< Replier le panneau")) collapsed = true;
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
            GUI.matrix = oldMatrix;
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
            if (GUILayout.Button("Complet")) SetForm(4);
            GUILayout.EndHorizontal();

            // Pièces du chat complet, ajoutées ou retirées une par une (il faut la chair)
            GUI.enabled = body.HasBody;
            GUILayout.BeginHorizontal();
            TogglePart("Bras", LimbKind.Arm, () => Limb.CatArms(testPartLife));
            TogglePart("Tête", LimbKind.Head, () => new[] { Limb.CatHead(testPartLife) });
            TogglePart("Queue", LimbKind.Tail, () => new[] { Limb.CatTail(testPartLife) });
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            // Allure sur place : le chat s'anime comme s'il avançait, sans bouger (marche ≈ 3,5 m/s, course du jeu : 6)
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Allure {treadmillSpeed:0.0} m/s", GUILayout.Width(105f));
            treadmillSpeed = GUILayout.HorizontalSlider(treadmillSpeed, 0f, maxTreadmillSpeed);
            if (GUILayout.Button("0", GUILayout.Width(24f))) treadmillSpeed = 0f;
            GUILayout.EndHorizontal();

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
                treadmillSpeed = 0f;
                if (GameFlow.Instance != null) GameFlow.Instance.PlacePlayer(playerHome);
                else body.transform.position = playerHome;
            }
            GUILayout.EndHorizontal();

            LimbStatePanel();
        }

        // 0 âme, 1 boule, 2 debout, 3 debout sur une patte, 4 chat complet (bras, tête, queue)
        private void SetForm(int form)
        {
            body.RemoveAllLimbs();
            if (form >= 1) body.GrantFlesh(Limb.Torso(testFleshLife));
            if (form >= 2) body.AttachLimbs(Limb.CatLegs(testLegLife));
            if (form == 3)
            {
                foreach (Limb limb in body.Limbs)
                {
                    if (limb.Kind != LimbKind.Leg) continue;
                    // Le chat du labo est invincible : on lève l'invincibilité le temps d'arracher la patte
                    body.Invulnerable = false;
                    body.TearLimb(limb);
                    body.Invulnerable = true;
                    break;
                }
            }
            if (form == 4)
            {
                body.AttachLimbs(Limb.CatArms(testPartLife));
                body.AttachLimb(Limb.CatHead(testPartLife));
                body.AttachLimb(Limb.CatTail(testPartLife));
            }
        }

        // Bouton qui ajoute la pièce si elle manque, la retire sinon
        private void TogglePart(string label, LimbKind kind, System.Func<Limb[]> make)
        {
            bool has = false;
            foreach (Limb limb in body.Limbs) has |= limb.Kind == kind && !limb.IsTorn;
            if (!GUILayout.Button((has ? "− " : "+ ") + label)) return;

            // Recompose le corps : même torse et mêmes membres, avec ou sans cette pièce
            var torso = new System.Collections.Generic.List<Limb>();
            var keep = new System.Collections.Generic.List<Limb>();
            foreach (Limb limb in body.Limbs)
            {
                if (limb.IsTorn) continue;
                if (limb.IsTorsoPart) torso.Add(limb);
                else if (!SameGroup(limb.Kind, kind)) keep.Add(limb);
            }
            body.GrantFlesh(torso.ToArray());
            body.AttachLimbs(keep);
            if (!has) body.AttachLimbs(make());
        }

        // Le bras va avec son avant-bras et sa main
        private static bool SameGroup(LimbKind a, LimbKind group)
        {
            if (group == LimbKind.Arm) return a == LimbKind.Arm || a == LimbKind.Forearm || a == LimbKind.Hand;
            return a == group;
        }

        // Vie et effet de chaque membre, pour voir son apparence (fourrure → chair cousue → à vif)
        private void LimbStatePanel()
        {
            if (body.Limbs.Count == 0) return;
            GUILayout.Space(4f);
            GUILayout.Label("Membres : vie et effet (✕ : arracher)");
            Limb toTear = null;
            foreach (Limb limb in body.Limbs)
            {
                if (limb.IsTorn) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(limb.Id, GUILayout.Width(95f));
                float ratio = GUILayout.HorizontalSlider(limb.LifeRatio, 0.02f, 1f);
                if (GUILayout.Button("✕", GUILayout.Width(24f))) toTear = limb;
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(ratio, limb.LifeRatio)) SetLife(limb, ratio);

                int effect = GUILayout.Toolbar((int)limb.Effect, EffectLabels);
                if (effect != (int)limb.Effect) limb.Effect = (LimbEffect)effect;
            }

            // Arraché après la boucle (il emporte ce qui y est cousu : la liste change)
            if (toTear != null)
            {
                body.Invulnerable = false;
                body.TearLimb(toTear);
                body.Invulnerable = true;
            }
        }

        // Règle la vie d'un membre (sans l'arracher) en gardant son effet
        private static void SetLife(Limb limb, float ratio)
        {
            LimbEffect effect = limb.Effect;
            limb.Restore();
            limb.Damage(limb.MaxLife * (1f - ratio));
            limb.Effect = effect;
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
