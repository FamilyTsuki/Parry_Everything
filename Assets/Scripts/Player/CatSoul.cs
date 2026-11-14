using System.Collections.Generic;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Player
{
    // Visuel du joueur : âme de chat bleue en forme de flamme, qui lévite.
    // Le mesh est généré puis déformé chaque frame :
    //  - oreilles de chat et queue de flamme qui monte
    //  - effet de l'air : la queue, les oreilles et l'arrière du corps traînent derrière le mouvement (ressort = rebond à l'arrêt)
    //  - parry : l'âme se comprime en sphère, puis rebondit en reprenant sa forme
    // Visible et animé aussi dans l'éditeur hors Play ([ExecuteAlways]) : les réglages s'appliquent en direct.
    // Ajouté automatiquement par PlayerController si absent. Le visuel n'est pas enfant du joueur (il ne tourne pas avec le Rigidbody).
    [ExecuteAlways]
    public class CatSoul : MonoBehaviour
    {
        [Header("Forme (en rayons)")]
        [SerializeField] private float radius = 0.45f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.1f, 0f);   // position du visuel par rapport au joueur
        [Range(2, 5)] public int subdivisions = 4;
        [SerializeField] private float earLength = 0.8f;
        [SerializeField] private float earAngle = 0.42f;      // largeur de la base des oreilles (radians)
        [SerializeField] private float tailLength = 1.3f;
        [SerializeField] private float tailAngle = 0.95f;     // largeur de la base de la queue (radians)
        [SerializeField] private float faceTurn = 35f;        // degrés : la tête regarde entre la caméra et la direction
        [SerializeField] private bool hideBaseRenderer = true; // cache le mesh d'origine du joueur (capsule)

        [Header("Couleurs")]
        [SerializeField] private Color coreColor = new Color(0.75f, 0.95f, 1f);
        [SerializeField] private Color rimColor = new Color(0.1f, 0.4f, 1f);
        [SerializeField] private Color eyeColor = new Color(0.02f, 0.05f, 0.2f);
        [SerializeField] private Color lightColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color hurtColor = new Color(1f, 0.3f, 0.4f);

        [Header("Lévitation")]
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 2.2f;

        [Header("Effet de l'air")]
        [SerializeField] private float dragStiffness = 60f;   // ressort qui suit la vitesse : plus bas = plus de retard
        [SerializeField] private float dragDamping = 9f;      // plus bas = plus de rebond à l'arrêt
        [SerializeField] private float bodyDrag = 0.06f;
        [SerializeField] private float earDrag = 0.08f;
        [SerializeField] private float tailDrag = 0.22f;
        [SerializeField] private float maxDrag = 12f;
        [SerializeField] private float leanAngle = 3f;        // inclinaison (degrés) par unité de vitesse horizontale
        [SerializeField] private float flicker = 0.12f;       // ondulation de la flamme
        [SerializeField] private float buoyancy = 0.35f;      // la queue monte comme une flamme

        [Header("Parry")]
        [SerializeField] private float sphereScale = 0.8f;    // taille de la sphère comprimée (en rayons)
        [SerializeField] private float compressStiffness = 900f;
        [SerializeField] private float compressDamping = 30f;
        [SerializeField] private float releaseStiffness = 220f;
        [SerializeField] private float releaseDamping = 9f;   // bas = rebond élastique en sortie de parry
        [SerializeField] private float parryGlow = 1.8f;

        [Header("Rendu (optionnel, sinon trouvé automatiquement)")]
        [SerializeField] private Shader soulShader;

        [Header("Aperçu dans l'éditeur (hors Play)")]
        [SerializeField] private bool previewAnimate = true;              // lévitation, flamme, clignements
        [Range(0f, 1f)] public float previewCompress = 0f; // 1 = sphère du parry
        [SerializeField] private Vector2 previewVelocity = Vector2.zero;  // simule le déplacement (effet de l'air)
        [SerializeField] private bool previewFaceLeft = false;

        // Directions locales (la face regarde -Z local)
        private static readonly Vector3[] earDirs =
        {
            new Vector3(-0.55f, 0.8f, 0.05f).normalized,
            new Vector3(0.55f, 0.8f, 0.05f).normalized,
        };
        private static readonly Vector3[] earTips =
        {
            new Vector3(-0.4f, 1f, 0.1f).normalized,
            new Vector3(0.4f, 1f, 0.1f).normalized,
        };
        private static readonly Vector3[] eyeDirs =
        {
            new Vector3(-0.33f, 0.1f, -0.94f).normalized,
            new Vector3(0.33f, 0.1f, -0.94f).normalized,
        };
        private static readonly Vector3 tailDir = new Vector3(0f, 0.15f, 1f).normalized;
        private static readonly Vector3 tailSide = Vector3.right;

        private PlayerController owner;
        private PlayerBody body;
        private Rigidbody rb;
        private float soulScale = 1f;           // taille affichée : rattrape PlayerBody.SoulScale (l'âme se consume)

        // Pose imposée par le corps (CatBody) : enfermée dans la boule, ou tête posée sur le torse
        private Vector3 poseOffset, poseOffsetTarget;
        private float poseScale = 1f, poseScaleTarget = 1f;
        private float poseBob = 1f, poseBobTarget = 1f;
        private bool hidden;

        private GameObject visual;
        private Mesh mesh;
        private Material soulMat;
        private Vector3[] baseDirs;
        private Vector3[] verts;
        private Color[] colors;
        private Transform[] eyes = new Transform[2];
        private TrailRenderer trail;
        private ParticleSystem embers;
        private Light glowLight;

        private float time;
        private float yaw;
        // Ressorts
        private Vector3 drag, dragVel;          // vitesse "ressentie" par la flamme (monde)
        private float compress, compressVel;    // 0 = forme normale, 1 = sphère
        private float punch, punchVel;          // pulsation d'échelle quand le parry réussit
        private float[] earTwitch = new float[2], earTwitchVel = new float[2];
        private float twitchTimer, blinkTimer, blinkLeft;
        private float flashLeft, flashDuration = 0.1f;
        private Color flashColor = Color.white;

        // Valeurs locales du frame en cours (utilisées par Shape)
        private Vector3 dragLocal, upLocal;

        // Objets générés, détruits quand le composant est désactivé
        private readonly GeneratedObjects generated = new GeneratedObjects();
        private bool needsRebuild;
        private float lastRealtime;

        private void OnEnable()
        {
            owner = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody>();
            BuildVisual();
            twitchTimer = Random.Range(2f, 5f);
            blinkTimer = Random.Range(2f, 4f);
            yaw = -Facing() * faceTurn;
            lastRealtime = Time.realtimeSinceStartup;
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
    #endif
        }

        private void OnDisable()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
    #endif
            DestroyVisual();
            SetBaseRendererHidden(false);
        }

        // Un réglage a changé dans l'Inspector : on reconstruit au prochain frame
        private void OnValidate()
        {
            needsRebuild = true;
        }

    #if UNITY_EDITOR
        private double lastTick;

        // Hors Play, Unity ne rafraîchit pas tout seul : on force ~30 images/s pour l'aperçu animé
        private void EditorTick()
        {
            if (Application.isPlaying || !previewAnimate)
            {
                return;
            }
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - lastTick < 1.0 / 30.0)
            {
                return;
            }
            lastTick = now;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
    #endif

        private void SetBaseRendererHidden(bool hidden)
        {
            Renderer baseRend = GetComponent<Renderer>();
            if (baseRend != null)
            {
                baseRend.enabled = !hidden;
            }
        }

        // Âme cachée : la chair la recouvre entièrement (boule, corps debout)
        public void SetHidden(bool value)
        {
            hidden = value;
        }

        // Le corps place l'âme : décalage, taille, lévitation (0 = immobile). Transition douce.
        public void SetPose(Vector3 extraOffset, float scale, float bob)
        {
            poseOffsetTarget = extraOffset;
            poseScaleTarget = scale;
            poseBobTarget = bob;
        }

        // Parry réussi : flash blanc + pulsation
        public void OnParrySuccess(float flash)
        {
            Flash(Color.white, flash);
            punchVel += 7f;
        }

        // Coup encaissé : flash rouge + écrasement
        public void OnHurt()
        {
            // Plus long que le gel de l'impact (0,2 s) : le flash reste visible quand le jeu repart
            Flash(hurtColor, 0.5f);
            punchVel -= 9f;
        }

        private void Flash(Color color, float duration)
        {
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, duration);
            flashLeft = flashDuration;
        }

        private float Facing()
        {
            if (!Application.isPlaying)
            {
                return previewFaceLeft ? -1f : 1f;
            }
            return owner != null ? owner.Facing : 1f;
        }

        private void LateUpdate()
        {
            if (needsRebuild)
            {
                needsRebuild = false;
                DestroyVisual();
                BuildVisual();
            }
            if (visual == null)
            {
                return;
            }
            if (visual.activeSelf == hidden) visual.SetActive(!hidden);

            bool playing = Application.isPlaying;
            float dt;
            bool parrying;
            Vector3 vel;
            if (playing)
            {
                dt = Time.deltaTime;
                parrying = owner != null && owner.IsParrying();
                vel = rb != null ? rb.linearVelocity : Vector3.zero;
            }
            else
            {
                // Aperçu éditeur : temps réel, valeurs prises dans l'Inspector
                float now = Time.realtimeSinceStartup;
                dt = previewAnimate ? Mathf.Clamp(now - lastRealtime, 0f, 0.1f) : 0f;
                lastRealtime = now;
                parrying = previewCompress > 0.5f;
                vel = previewVelocity;
            }
            vel.z = 0f;

            // Animations figées pendant le hit stop (dt = 0), sauf le flash (temps réel)
            if (dt > 0f)
            {
                time += dt;

                Vector3 targetDrag = Vector3.ClampMagnitude(vel, maxDrag);
                SpringVec(ref drag, ref dragVel, targetDrag, dragStiffness, dragDamping, dt);

                if (parrying)
                {
                    Spring(ref compress, ref compressVel, 1f, compressStiffness, compressDamping, dt);
                }
                else
                {
                    Spring(ref compress, ref compressVel, 0f, releaseStiffness, releaseDamping, dt);
                }
                compress = Mathf.Clamp(compress, -0.35f, 1.15f);

                Spring(ref punch, ref punchVel, 0f, 300f, 10f, dt);

                // Petit mouvement d'oreille de temps en temps
                twitchTimer -= dt;
                if (twitchTimer <= 0f)
                {
                    int e = Random.Range(0, 2);
                    earTwitchVel[e] -= 6f;
                    twitchTimer = Random.Range(2f, 6f);
                }
                for (int e = 0; e < 2; e++)
                {
                    Spring(ref earTwitch[e], ref earTwitchVel[e], 0f, 400f, 12f, dt);
                }

                // Clignement des yeux
                blinkTimer -= dt;
                if (blinkTimer <= 0f)
                {
                    blinkLeft = 0.12f;
                    blinkTimer = Random.Range(2.5f, 5f);
                }
                blinkLeft -= dt;

                yaw = Mathf.Lerp(yaw, -Facing() * faceTurn, 1f - Mathf.Exp(-10f * dt));
            }
            flashLeft -= Time.unscaledDeltaTime;

            // Aperçu éditeur : les curseurs s'appliquent directement, sans ressort
            if (!playing)
            {
                drag = Vector3.ClampMagnitude(vel, maxDrag);
                dragVel = Vector3.zero;
                compress = previewCompress;
                compressVel = 0f;
                if (!previewAnimate)
                {
                    yaw = -Facing() * faceTurn;
                }
            }

            // Pose du corps (transition douce, en temps réel)
            float pk = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            poseOffset = Vector3.Lerp(poseOffset, poseOffsetTarget, pk);
            poseScale = Mathf.Lerp(poseScale, poseScaleTarget, pk);
            poseBob = Mathf.Lerp(poseBob, poseBobTarget, pk);

            // Position : suit le joueur, lévite
            float bob = Mathf.Sin(time * bobSpeed) * bobHeight * poseBob;
            visual.transform.position = transform.position + offset + poseOffset + Vector3.up * bob;

            // Rotation : tête tournée vers la direction, penchée dans le mouvement
            float lean = -drag.x * leanAngle;
            Quaternion rot = Quaternion.Euler(0f, 0f, lean) * Quaternion.Euler(0f, yaw, 0f);
            visual.transform.rotation = rot;
            // L'âme consumée rétrécit, puis regrossit en se régénérant
            if (playing)
            {
                if (body == null) body = GetComponent<PlayerBody>();
                float targetScale = body != null ? body.SoulScale : 1f;
                soulScale = Mathf.Lerp(soulScale, targetScale, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            }
            else
            {
                soulScale = 1f;
            }
            visual.transform.localScale = Vector3.one * ((soulScale + punch) * poseScale);

            Quaternion inv = Quaternion.Inverse(rot);
            dragLocal = inv * drag;
            upLocal = inv * Vector3.up;

            // Déformation du mesh
            for (int i = 0; i < baseDirs.Length; i++)
            {
                float tw;
                verts[i] = Shape(baseDirs[i], out tw);
                colors[i] = new Color(1f, 1f, 1f, 1f - tw * tw * 0.85f);
            }
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Yeux posés sur la surface ; plissés pendant le parry, fermés pendant un clignement
            float squint = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(compress));
            float blink = blinkLeft > 0f ? 0.1f : 1f;
            for (int e = 0; e < 2; e++)
            {
                float tw;
                Vector3 d = eyeDirs[e];
                eyes[e].localPosition = Shape(d, out tw) + d * (0.02f * radius);
                eyes[e].localRotation = Quaternion.LookRotation(d);
                eyes[e].localScale = new Vector3(0.22f, 0.32f * squint * blink, 0.08f) * radius;
            }

            // Matériau : plus lumineux en parry, flash
            float flash = flashLeft > 0f ? flashLeft / flashDuration : 0f;
            soulMat.SetFloat("_Glow", Mathf.Lerp(1f, parryGlow, Mathf.Clamp01(compress)));
            soulMat.SetFloat("_Flash", flash);
            soulMat.SetColor("_FlashColor", flashColor);

            // Traînée à la pointe de la queue quand on va vite
            float speed = vel.magnitude;
            float tailTw;
            trail.transform.localPosition = Shape(tailDir, out tailTw);
            trail.emitting = speed > 2f && compress < 0.5f;

            var emission = embers.emission;
            emission.rateOverTime = parrying ? 0f : 8f + speed * 5f;

            glowLight.intensity = (1.2f + (Mathf.PerlinNoise(time * 4f, 0f) - 0.5f) * 0.4f)
                                  * Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(compress)) + flash * 2f;
        }

        // Position locale d'un point de la surface, pour la direction d donnée.
        // tw = poids "queue" (0 = corps, 1 = pointe de la queue)
        private Vector3 Shape(Vector3 d, out float tw)
        {
            Vector3 p = d;
            p.y *= 0.92f;

            float n = Mathf.PerlinNoise(d.x * 1.8f + d.z * 0.7f + 10f + time * 0.7f, d.y * 1.8f - time * 3f) - 0.5f;

            // Oreilles : cônes poussés hors de la sphère, balayés par l'air
            float earW = 0f;
            for (int e = 0; e < 2; e++)
            {
                float a = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, earDirs[e]), -1f, 1f));
                float w = Mathf.Clamp01(1f - a / earAngle);
                if (w <= 0f)
                {
                    continue;
                }
                p += earTips[e] * (w * earLength * (1f + earTwitch[e] * 0.3f));
                p -= dragLocal * (earDrag * w * w);
                earW = Mathf.Max(earW, w);
            }

            // Queue de flamme : monte, traîne derrière le mouvement, ondule
            float ta = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, tailDir), -1f, 1f));
            tw = Mathf.Clamp01(1f - ta / tailAngle);
            if (tw > 0f)
            {
                float t2 = tw * tw;
                p += tailDir * (tailLength * Mathf.Pow(tw, 1.6f));
                p += upLocal * (buoyancy * t2);
                p -= dragLocal * (tailDrag * t2);
                p += tailSide * (Mathf.Sin(time * 6f - tw * 4f) * 0.12f * t2);
            }

            // Arrière du corps poussé par l'air
            float dragMag = dragLocal.magnitude;
            if (dragMag > 0.001f)
            {
                float back = Mathf.Clamp01(Vector3.Dot(d, -dragLocal / dragMag));
                p -= dragLocal * (bodyDrag * back);
            }

            // Ondulation de flamme, plus forte sur la queue et les oreilles
            p += d * (n * flicker * (0.4f + 1.6f * tw + earW));

            // Parry : tout se resserre en sphère
            p = Vector3.LerpUnclamped(p, d * sphereScale, compress);
            return p * radius;
        }

        // Ressort amorti, sous-pas pour rester stable avec un gros dt
        private static void Spring(ref float x, ref float v, float target, float k, float damping, float dt)
        {
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += ((target - x) * k - v * damping) * h;
                x += v * h;
            }
        }

        private static void SpringVec(ref Vector3 x, ref Vector3 v, Vector3 target, float k, float damping, float dt)
        {
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += ((target - x) * k - v * damping) * h;
                x += v * h;
            }
        }

        private void DestroyVisual()
        {
            generated.DestroyAll();
            visual = null;
        }

        // Ni sauvegardé dans la scène, ni affiché dans la Hierarchy (recréé à chaque chargement)
        private T Track<T>(T o) where T : Object
        {
            return generated.Track(o);
        }

        private void BuildVisual()
        {
            SetBaseRendererHidden(hideBaseRenderer);
            visual = Track(new GameObject("CatSoulVisual"));

            // Mesh déformable
            int[] tris;
            BuildIcosphere(subdivisions, out baseDirs, out tris);
            verts = new Vector3[baseDirs.Length];
            colors = new Color[baseDirs.Length];
            mesh = Track(new Mesh());
            mesh.name = "CatSoul";
            mesh.MarkDynamic();
            mesh.vertices = baseDirs;
            mesh.triangles = tris;

            if (soulShader == null)
            {
                soulShader = Shader.Find("Custom/CatSoul");
            }
            if (soulShader == null)
            {
                Debug.LogWarning("CatSoul: shader 'Custom/CatSoul' introuvable, repli sur Sprites/Default.");
                soulShader = Shader.Find("Sprites/Default");
            }
            soulMat = Track(new Material(soulShader));
            soulMat.SetColor("_CoreColor", coreColor);
            soulMat.SetColor("_RimColor", rimColor);
            soulMat.color = coreColor;

            visual.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.material = soulMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Yeux : petits ovales sombres dessinés par-dessus le corps
            Material eyeMat = Track(MaterialFactory.Unlit(Color.white));
            eyeMat.color = eyeColor;
            eyeMat.renderQueue = 3001;
            for (int e = 0; e < 2; e++)
            {
                GameObject eye = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                eye.name = e == 0 ? "EyeL" : "EyeR";
                GeneratedObjects.SafeDestroy(eye.GetComponent<Collider>());
                eye.GetComponent<MeshRenderer>().material = eyeMat;
                eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eye.transform.SetParent(visual.transform, false);
                eyes[e] = eye.transform;
            }

            Material fxMat = Track(MaterialFactory.Unlit(Color.white));
            fxMat.mainTexture = Track(SoftCircle());

            // Traînée d'air à la pointe de la queue
            GameObject trailGo = Track(new GameObject("TailTrail"));
            trailGo.transform.SetParent(visual.transform, false);
            trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, radius * 0.5f, 1f, 0f);
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(coreColor, 0f), new GradientColorKey(rimColor, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.material = fxMat;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;

            // Braises qui montent et restent derrière quand on bouge
            GameObject emberGo = Track(new GameObject("Embers"));
            emberGo.transform.SetParent(visual.transform, false);
            embers = emberGo.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = embers.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.1f, radius * 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(rimColor, coreColor);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.7f;
            var col = embers.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            var size = embers.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            ParticleSystemRenderer psr = emberGo.GetComponent<ParticleSystemRenderer>();
            psr.material = fxMat;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            embers.Play();

            // Lumière bleue autour de l'âme
            GameObject lightGo = Track(new GameObject("SoulLight"));
            lightGo.transform.SetParent(visual.transform, false);
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = lightColor;
            glowLight.range = 3f;
            glowLight.intensity = 1.2f;
            glowLight.shadows = LightShadows.None;
        }

        // Texture ronde et douce pour les particules et la traînée
        private static Texture2D SoftCircle()
        {
            const int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f;
                    float dy = (y + 0.5f) / s * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            return tex;
        }

        // Icosphère : sommets répartis régulièrement, sans couture (normales lisses)
        private static void BuildIcosphere(int sub, out Vector3[] dirs, out int[] tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            List<Vector3> v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++)
            {
                v[i] = v[i].normalized;
            }
            List<int> f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int s = 0; s < sub; s++)
            {
                Dictionary<long, int> cache = new Dictionary<long, int>();
                List<int> nf = new List<int>(f.Count * 4);
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Midpoint(a, b, v, cache);
                    int bc = Midpoint(b, c, v, cache);
                    int ca = Midpoint(c, a, v, cache);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }

            // Unity : faces avant dans le sens horaire. On retourne si la normale pointe vers l'intérieur.
            Vector3 n = Vector3.Cross(v[f[1]] - v[f[0]], v[f[2]] - v[f[0]]);
            if (Vector3.Dot(n, v[f[0]] + v[f[1]] + v[f[2]]) < 0f)
            {
                for (int i = 0; i < f.Count; i += 3)
                {
                    int tmp = f[i + 1];
                    f[i + 1] = f[i + 2];
                    f[i + 2] = tmp;
                }
            }

            dirs = v.ToArray();
            tris = f.ToArray();
        }

        private static int Midpoint(int a, int b, List<Vector3> v, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int idx;
            if (cache.TryGetValue(key, out idx))
            {
                return idx;
            }
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            idx = v.Count - 1;
            cache[key] = idx;
            return idx;
        }
    }
}
