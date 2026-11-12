using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Petit robot sauteur très simple : un cube, un œil lumineux, 2 jambes sur les côtés.
//  - sautille sur place, se tourne vers le joueur
//  - quand le joueur est proche : s'écrase (armement = signal pour parer), puis lui saute dessus
//  - touche le joueur : dégâts, puis rebondit en arrière
//  - paré : meurt (flash, écrasement, éclats)
// Le visuel est généré en code et visible aussi dans l'éditeur hors Play ([ExecuteAlways]).
[ExecuteAlways]
public class jumper : MonoBehaviour
{
    [Header("Forme")]
    public float size = 0.6f;                 // taille globale (diamètre du collider)
    public Color bodyColor = new Color(0.75f, 0.78f, 0.82f);
    public Color legColor = new Color(0.25f, 0.27f, 0.3f);
    public Color eyeIdleColor = new Color(0.2f, 0.9f, 1f);
    public Color eyeAttackColor = new Color(1f, 0.15f, 0.1f);
    public float turnToCamera = 40f;          // degrés : se montre un peu de 3/4
    public float turnSpeed = 720f;            // vitesse pour se retourner (degrés/s)

    [Header("Comportement")]
    public float detectRange = 6f;            // distance à laquelle il repère le joueur
    public float windupDuration = 0.45f;      // écrasement avant le saut (temps pour parer)
    public float jumpHeight = 1.6f;           // hauteur du saut d'attaque
    public float maxJumpDistance = 5f;
    public float cooldown = 1.2f;             // repos entre deux attaques
    public float hopHeight = 0.25f;           // petits sauts au repos
    public float hopInterval = 0.8f;
    public float damage = 10f;
    public float bounceBack = 4f;             // recul après avoir touché le joueur

    [Header("Mort (paré)")]
    public float deathDuration = 0.25f;
    public int deathParticleCount = 25;

    private Rigidbody rb;
    private SphereCollider col;
    private player target;

    // Visuel
    private readonly List<Object> generated = new List<Object>();
    private GameObject visual;
    private Transform body;
    private Material eyeMat;
    private Transform[] legs = new Transform[2];
    private float yaw;
    private float groundTimer;                    // >0 : touche le sol (pour le visuel)

    // État
    private enum State { Idle, Windup, Jumping, Dead }
    private State state = State.Idle;
    private bool grounded;
    private bool hasHit;
    private float timer;
    private float facing = 1f;
    private float squash, squashVel;          // >0 écrasé, <0 étiré

    void OnEnable()
    {
        BuildVisual();
        if (Application.isPlaying)
        {
            SetupPhysics();
        }
    }

    void OnDisable()
    {
        foreach (Object o in generated)
        {
            if (o == null) continue;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
        generated.Clear();
        visual = null;
    }

    void OnValidate()
    {
        // Réglages modifiés dans l'Inspector : on reconstruit le visuel au prochain tick
        // (créer/détruire des objets est interdit pendant OnValidate)
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                OnDisable();
                BuildVisual();
            };
        }
#endif
    }

    void SetupPhysics()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = 1f;

        col = GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.radius = size * 0.5f;

        // Le cube d'origine éventuel ne sert plus
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        target = FindFirstObjectByType<player>();
        timer = hopInterval;
    }

    // ---------- Visuel : robot très simple ----------
    // Un cube pour le corps, un œil carré lumineux, 2 jambes (bâtons) sur les côtés.
    // Repère du modèle : avant = +X, haut = +Y, côtés = ±Z. Unité = size.

    void BuildVisual()
    {
        if (visual != null) return;

        visual = new GameObject("JumperVisual");
        Track(visual);
        visual.transform.SetParent(transform, false);

        Material bodyMat = MakeMat(bodyColor);
        Material legMat = MakeMat(legColor);
        eyeMat = MakeMat(eyeIdleColor);
        eyeMat.EnableKeyword("_EMISSION");

        body = Cube("Body", visual.transform, bodyMat);
        Transform eye = Cube("Eye", body, eyeMat);
        eye.localPosition = new Vector3(0.51f, 0.12f, 0f);   // collé à la face avant du cube
        eye.localScale = new Vector3(0.05f, 0.22f, 0.3f);

        for (int i = 0; i < 2; i++)
        {
            legs[i] = Cube("Leg", visual.transform, legMat);
        }
        yaw = YawFor(facing);
        UpdateVisual(0f);
    }

    Material MakeMat(Color c)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = c;
        Track(m);
        return m;
    }

    Transform Cube(string name, Transform parent, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Track(go);
        go.name = name;
        Object c = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    void Track(Object o)
    {
        // Objets générés : jamais sauvegardés dans la scène
        o.hideFlags = HideFlags.HideAndDontSave;
        generated.Add(o);
    }

    float YawFor(float f)
    {
        // Tourné vers sa cible, et un peu vers la caméra (-Z) pour voir les deux jambes
        return f > 0f ? turnToCamera : 180f - turnToCamera;
    }

    void UpdateVisual(float dt)
    {
        if (visual == null) return;
        float S = size;
        bool inAir = Application.isPlaying && groundTimer <= 0f;

        // Accroupi pendant l'armement (ressort = petit rebond)
        float target = state == State.Windup ? 1f : 0f;
        if (dt > 0f)
        {
            squashVel += (250f * (target - squash) - 14f * squashVel) * dt;
            squash += squashVel * dt;
            yaw = Mathf.MoveTowardsAngle(yaw, YawFor(facing), turnSpeed * dt);
        }
        else
        {
            yaw = YawFor(facing);
        }
        visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // Corps : cube posé sur les jambes, plus bas quand il s'accroupit, penché pendant l'attaque
        float bodySize = 0.6f * S;
        float bottom = -0.5f * S;                                  // le sol (bas du collider)
        float legLen = inAir ? 0.12f * S : (0.22f - 0.12f * squash) * S;
        float bodyY = bottom + legLen + bodySize * 0.5f;
        body.localPosition = new Vector3(0f, bodyY, 0f);
        body.localScale = Vector3.one * bodySize;
        body.localRotation = Quaternion.Euler(0f, 0f, state == State.Jumping ? -15f : 0f);

        // Œil : cyan au repos, rouge pendant l'armement et l'attaque
        Color ec = (state == State.Windup || state == State.Jumping) ? eyeAttackColor : eyeIdleColor;
        eyeMat.color = ec;
        eyeMat.SetColor("_EmissionColor", ec * 2f);

        // Jambes : bâtons sous les côtés du corps, du bas du cube jusqu'au sol
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? 1f : -1f;
            Transform leg = legs[i];
            leg.localScale = new Vector3(0.1f * S, legLen, 0.1f * S);
            leg.localPosition = new Vector3(0f, bodyY - bodySize * 0.5f - legLen * 0.5f, side * bodySize * 0.35f);
        }
    }

    // ---------- Comportement ----------

    void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateVisual(0f);
            return;
        }
        if (target != null && state != State.Dead && state != State.Jumping)
        {
            facing = target.transform.position.x < transform.position.x ? -1f : 1f;
        }
        groundTimer -= Time.deltaTime;
        UpdateVisual(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (!Application.isPlaying || rb == null || state == State.Dead) return;

        timer -= Time.fixedDeltaTime;
        float dist = target != null ? Mathf.Abs(target.transform.position.x - transform.position.x) : Mathf.Infinity;

        switch (state)
        {
            case State.Idle:
                if (grounded && timer <= 0f)
                {
                    if (dist <= detectRange)
                    {
                        state = State.Windup;
                        timer = windupDuration;
                    }
                    else
                    {
                        Jump(0f, hopHeight);
                        timer = hopInterval;
                    }
                }
                break;

            case State.Windup:
                if (timer <= 0f) Attack();
                break;

            case State.Jumping:
                CheckHit();
                if (grounded && rb.linearVelocity.y <= 0.01f)
                {
                    state = State.Idle;
                    timer = cooldown;
                }
                break;
        }
        grounded = false; // recalculé par les collisions du prochain pas
    }

    // Saut en cloche qui retombe sur la position actuelle du joueur
    void Attack()
    {
        float g = -Physics.gravity.y;
        float vy = Mathf.Sqrt(2f * g * jumpHeight);
        float flightTime = 2f * vy / g;
        float dx = target != null ? target.transform.position.x - transform.position.x : 0f;
        dx = Mathf.Clamp(dx, -maxJumpDistance, maxJumpDistance);
        rb.linearVelocity = new Vector3(dx / flightTime, vy, 0f);

        state = State.Jumping;
        hasHit = false;
        grounded = false;
    }

    void Jump(float vx, float height)
    {
        float vy = Mathf.Sqrt(2f * -Physics.gravity.y * height);
        rb.linearVelocity = new Vector3(vx, vy, 0f);
        squashVel -= 4f; // petit étirement au décollage
    }

    void CheckHit()
    {
        if (hasHit) return;
        Collider[] hits = Physics.OverlapSphere(transform.position, size * 0.6f);
        foreach (Collider hit in hits)
        {
            player p = hit.GetComponentInParent<player>();
            if (p == null) continue;

            hasHit = true;
            Vector3 contact = Vector3.Lerp(transform.position, p.transform.position, 0.5f);
            if (p.TakeDamage(damage, transform, contact))
            {
                // Touché : rebondit en arrière
                float away = transform.position.x < p.transform.position.x ? -1f : 1f;
                rb.linearVelocity = new Vector3(away * bounceBack, bounceBack, 0f);
            }
            else
            {
                StartCoroutine(Die());
            }
            return;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.6f)
            {
                grounded = true;
                groundTimer = 0.08f;
                return;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        OnCollisionStay(collision);
    }

    // Paré : flash blanc, écrasement, éclats, puis disparition
    IEnumerator Die()
    {
        state = State.Dead;
        rb.isKinematic = true;
        col.enabled = false;

        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
        {
            HitFlash.Play(r.gameObject, deathDuration);
        }

        // Temps réel : l'arrêt du temps du parry ne bloque pas l'animation
        float t = 0f;
        Vector3 start = visual.transform.localScale;
        while (t < deathDuration)
        {
            float k = t / deathDuration;
            visual.transform.localScale = new Vector3(start.x * (1f + k), start.y * (1f - k), start.z * (1f + k));
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        SpawnDeathParticles();
        Destroy(gameObject);
    }

    void SpawnDeathParticles()
    {
        GameObject go = new GameObject("JumperDeath");
        go.transform.position = transform.position;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = bodyColor;
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)deathParticleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = size * 0.3f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ps.GetComponent<ParticleSystemRenderer>().material = new Material(Shader.Find("Sprites/Default"));
        ps.Play();
    }
}
