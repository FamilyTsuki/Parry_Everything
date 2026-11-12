using UnityEngine;

public class player : MonoBehaviour
{
    private Rigidbody rb;
    public float speed = 5.0f;
    public float life = 100.0f;

    // Saut
    public KeyCode jumpKey = KeyCode.UpArrow;
    public float jumpHeight = 2.0f;               // hauteur max (touche maintenue)
    public float fallGravityMultiplier = 2.2f;    // chute plus rapide que la montée : moins flottant
    public float lowJumpGravityMultiplier = 3.0f; // touche relâchée tôt : petit saut
    public float apexGravityMultiplier = 0.6f;    // gravité réduite au sommet (touche maintenue) : contrôle en l'air
    public float apexThreshold = 1.5f;            // vitesse verticale sous laquelle on est "au sommet"
    public float maxFallSpeed = 18f;
    public float coyoteTime = 0.1f;               // saut encore possible juste après avoir quitté un bord
    public float jumpBufferTime = 0.12f;          // appui juste avant d'atterrir : le saut part à l'atterrissage
    [Range(0f, 1f)] public float groundNormalMin = 0.6f; // pente max considérée comme sol

    private bool isGrounded = false;   // contact avec le sol au dernier pas physique
    private bool isJumping = false;    // montée due à un saut (pour le saut court)
    private bool jumpHeld = false;
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;
    public bool IsGrounded { get { return coyoteTimer > 0f; } }
    public float knockbackForce = 20.0f;
    public float knockbackEndSpeed = 1.0f;

    // Arrêt du temps quand on prend un coup
    public float hitStopDuration = 0.2f;
    // Tremblement d'écran quand on prend un coup
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.15f;

    // Parry : fenêtre active après appui sur X, puis temps d'attente si raté
    public KeyCode parryKey = KeyCode.X;
    public float parryWindow = 0.35f;
    public float parryCooldown = 0.4f;

    private float parryTimer = 0f;          // temps restant de la fenêtre de parry
    private float parryCooldownTimer = 0f;  // temps avant de pouvoir re-parry

    // Indicateur de parry : étoile jaune en haut à droite du joueur pendant la fenêtre
    public Vector3 parryStarOffset = new Vector3(0.7f, 0.7f, -0.6f);
    public float parryStarSize = 0.25f;
    public Color parryStarColor = new Color(1f, 0.85f, 0.1f);

    // Particules quand le parry réussit
    public Color parryParticleColor = new Color(1f, 0.9f, 0.4f);
    public int parryParticleCount = 40;

    // Impact quand le parry réussit (plus court que quand on prend un coup)
    public float parryHitStopDuration = 0.08f;
    public float parryShakeDuration = 0.1f;
    public float parryShakeMagnitude = 0.1f;
    public float parryFlashDuration = 0.08f;  // flash blanc du joueur

    private float facing = 1f; // +1 droite, -1 gauche
    public float Facing { get { return facing; } }
    private GameObject parryStar;
    private CatSoul soul; // visuel : âme de chat
    private static Material particleMaterial;

    // Ressenti des dégâts : voile rouge à l'écran, barre de vie, son d'impact, éclats rouges
    public Color hurtColor = new Color(0.85f, 0.05f, 0.08f);
    public float hurtVolume = 0.8f;
    private float maxLife;
    private float displayLife;          // barre claire qui rattrape la vraie vie
    private float hurtTime = -10f;      // temps réel du dernier coup
    private float hurtStrength;
    private static Texture2D vignetteTex;
    private AudioSource sfx;
    private AudioClip hurtClip;

    private bool isKnockedBack = false;
    private bool isHitStopped = false;
    private float moveX = 0f;
    private Vector3 linearVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Mouvement lissé entre deux pas physiques : visuel et caméra sans saccades
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        soul = GetComponent<CatSoul>();
        if (soul == null)
        {
            soul = gameObject.AddComponent<CatSoul>();
        }
        parryStar = CreateStar();
        maxLife = displayLife = life;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        hurtClip = MakeHurtClip();
        parryStar.SetActive(false);
    }

    void Update()
    {
        // Sans touche appuyée, pas de déplacement
        moveX = 0f;
        if (Input.GetKey(KeyCode.RightArrow))
        {
            moveX = 1f;
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            moveX = -1f;
        }
        if (moveX != 0f)
        {
            facing = moveX;
        }

        // Saut : l'appui est mémorisé un court instant, le saut part dans FixedUpdate
        jumpBufferTimer -= Time.deltaTime;
        if (Input.GetKeyDown(jumpKey))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        jumpHeld = Input.GetKey(jumpKey);

        // Parry : ouvre la fenêtre, et bloque un nouvel appui jusqu'à la fin de fenêtre + cooldown
        parryTimer -= Time.deltaTime;
        parryCooldownTimer -= Time.deltaTime;
        if (Input.GetKeyDown(parryKey) && parryCooldownTimer <= 0f)
        {
            parryTimer = parryWindow;
            parryCooldownTimer = parryWindow + parryCooldown;
        }
    }

    // Affiche l'étoile seulement pendant la fenêtre de parry, et la garde collée au joueur
    void LateUpdate()
    {
        bool show = IsParrying();
        parryStar.SetActive(show);
        if (show)
        {
            parryStar.transform.position = transform.position + parryStarOffset;
        }
    }

    // Étoile à 5 branches construite en mesh (pas de collider, pas enfant du joueur pour ne pas tourner avec lui)
    GameObject CreateStar()
    {
        const int points = 5;
        Vector3[] vertices = new Vector3[points * 2 + 1];
        int[] triangles = new int[points * 2 * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < points * 2; i++)
        {
            // Alternance pointe / creux, première pointe vers le haut
            float radius = (i % 2 == 0) ? parryStarSize : parryStarSize * 0.45f;
            float angle = Mathf.PI / 2f + i * Mathf.PI / points;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % (points * 2) + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "ParryStar";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        GameObject star = new GameObject("ParryStar");
        star.AddComponent<MeshFilter>().mesh = mesh;
        // Sprites/Default : non éclairé et visible des deux côtés
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = parryStarColor;
        star.AddComponent<MeshRenderer>().material = mat;
        return star;
    }

    // Gerbe d'étincelles à la position donnée, détruite automatiquement à la fin
    void SpawnParryParticles(Vector3 position)
    {
        SpawnParryParticles(position, parryParticleColor, parryParticleCount, 1f);
    }

    void SpawnParryParticles(Vector3 position, Color color, int count, float power)
    {
        if (particleMaterial == null)
        {
            particleMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        GameObject go = new GameObject("ParryParticles");
        go.transform.position = position;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        // Le système démarre tout seul à l'ajout : on l'arrête pour pouvoir le configurer
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f * power, 9f * power);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f * power, 0.15f * power);
        main.startColor = color;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        // Les étincelles rétrécissent jusqu'à disparaître
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ps.GetComponent<ParticleSystemRenderer>().material = particleMaterial;
        ps.Play();
    }

    // Prévient les autres scripts (ex : musique) à chaque parade réussie
    public static event System.Action OnParry;

    public bool IsParrying()
    {
        return parryTimer > 0f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float g = Physics.gravity.y; // négatif

        // isGrounded vient des collisions du pas précédent ; on le remet à zéro pour ce pas
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= dt;
        }
        isGrounded = false;

        Vector3 v = rb.linearVelocity;

        // Saut : vitesse calculée pour atteindre exactement jumpHeight
        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isKnockedBack)
        {
            v.y = Mathf.Sqrt(2f * -g * jumpHeight);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            isJumping = true;
        }

        // Gravité ajustée (la gravité normale est déjà appliquée par le Rigidbody)
        float gravityScale = 1f;
        if (v.y < 0f)
        {
            gravityScale = fallGravityMultiplier;
            isJumping = false;
        }
        else if (isJumping && !jumpHeld)
        {
            gravityScale = lowJumpGravityMultiplier;
        }
        if (jumpHeld && Mathf.Abs(v.y) < apexThreshold && !IsGrounded)
        {
            gravityScale = apexGravityMultiplier;
        }
        v.y += g * (gravityScale - 1f) * dt;
        v.y = Mathf.Max(v.y, -maxFallSpeed);

        if (isKnockedBack)
        {
            if (Mathf.Abs(v.x) > knockbackEndSpeed)
            {
                rb.linearVelocity = v;
                return;
            }
            isKnockedBack = false;
        }

        rb.linearVelocity = new Vector3(moveX * speed, v.y, 0);
    }

    // Sol = contact dont la normale pointe vers le haut
    void CheckGround(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= groundNormalMin)
            {
                isGrounded = true;
                return;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        CheckGround(collision);
    }

    void OnCollisionEnter(Collision collision)
    {
        CheckGround(collision);

        if (collision.gameObject.CompareTag("enemy"))
        {
            if (TakeDamage(10.0f, collision.transform))
            {
                print("Player hit by enemy! Life: " + life);
            }
        }
    }

    // Renvoie true si les dégâts ont été pris, false si le coup a été paré
    // hitPoint : point de contact du coup (pour les étincelles), optionnel
    public bool TakeDamage(float damage, Transform source = null, Vector3? hitPoint = null)
    {
        if (IsParrying())
        {
            // Parry réussi : aucun dégât, et on peut re-parry tout de suite
            parryTimer = 0f;
            parryCooldownTimer = 0f;
            Debug.Log("Parry !");
            OnParry?.Invoke();

            // Étincelles au point de contact, sinon entre le joueur et l'origine du coup
            Vector3 sparkPos = transform.position + Vector3.right * facing * 0.8f;
            if (hitPoint.HasValue)
            {
                sparkPos = hitPoint.Value;
                sparkPos.z = transform.position.z;
            }
            else if (source != null)
            {
                sparkPos = Vector3.Lerp(transform.position, source.position, 0.5f);
                sparkPos.z = transform.position.z;
            }
            SpawnParryParticles(sparkPos);

            // Impact : flash blanc, petit tremblement, court gel du temps
            if (soul != null)
            {
                soul.OnParrySuccess(parryFlashDuration);
            }
            else
            {
                HitFlash.Play(gameObject, parryFlashDuration);
            }
            ShakeCamera(parryShakeDuration, parryShakeMagnitude);
            if (!isHitStopped)
            {
                StartCoroutine(HitStop(parryHitStopDuration));
            }
            return false;
        }

        life -= damage;
        if (soul != null)
        {
            soul.OnHurt();
        }

        // Ressenti : voile rouge, son, éclats rouges au point d'impact
        hurtTime = Time.unscaledTime;
        hurtStrength = Mathf.Clamp01(0.45f + damage / 40f);
        if (sfx != null && hurtClip != null)
        {
            sfx.pitch = Random.Range(0.9f, 1.08f);
            sfx.PlayOneShot(hurtClip, hurtVolume * Mathf.Clamp(0.6f + damage / 30f, 0.6f, 1.2f));
        }
        Vector3 bloodPos = transform.position;
        if (hitPoint.HasValue) bloodPos = Vector3.Lerp(transform.position, hitPoint.Value, 0.5f);
        bloodPos.z = transform.position.z;
        SpawnParryParticles(bloodPos, hurtColor, 50, 1.3f);

        if (life <= 0)
        {
            Die();
        }
        else if (source != null && rb != null)
        {
            Vector3 dir = transform.position - source.position;
            dir.z = 0f;

            rb.linearVelocity = dir.normalized * knockbackForce;
            isKnockedBack = true;
        }

        if (life > 0)
        {
            // Secousse plus forte pour les gros coups
            ShakeCamera(shakeDuration + 0.1f, shakeMagnitude + damage * 0.012f);

            if (!isHitStopped)
            {
                StartCoroutine(HitStop(hitStopDuration));
            }
        }

        return true;
    }
    // Son d'impact généré une fois : coup sourd grave qui plonge + craquement bref
    AudioClip MakeHurtClip()
    {
        int sr = AudioSettings.outputSampleRate;
        int n = (int)(sr * 0.35f);
        float[] d = new float[n];
        System.Random r = new System.Random(3);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float f = 45f + 110f * Mathf.Exp(-t * 25f);
            ph += f / sr;
            float thump = Mathf.Sin(ph * 6.2831853f) * Mathf.Exp(-t / 0.09f);
            float nz = (float)r.NextDouble() * 2f - 1f;
            lp += (nz - lp) * 0.25f;
            float crunch = lp * Mathf.Exp(-t / 0.035f) * 1.4f;
            float x = (thump + crunch) * 1.6f;
            d[i] = x / (1f + Mathf.Abs(x)); // saturation douce
        }
        AudioClip c = AudioClip.Create("Hurt", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    // Voile rouge sur les bords de l'écran (coup reçu, puis pulsation quand la vie est basse) + barre de vie
    void OnGUI()
    {
        if (maxLife <= 0f) return;
        if (Event.current.type != EventType.Repaint) return;
        displayLife = Mathf.MoveTowards(displayLife, Mathf.Max(life, 0f), maxLife * 0.5f * Time.unscaledDeltaTime);
        if (displayLife < life) displayLife = life;

        float ratio = Mathf.Clamp01(life / maxLife);
        float since = Time.unscaledTime - hurtTime;
        float hit = since < 0.6f ? hurtStrength * (1f - since / 0.6f) : 0f;
        float low = ratio < 0.3f ? (0.3f - ratio) / 0.3f * (0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
        float a = Mathf.Clamp01(Mathf.Max(hit, low));
        Color old = GUI.color;
        if (a > 0.01f)
        {
            if (vignetteTex == null) vignetteTex = MakeVignette();
            GUI.color = new Color(hurtColor.r, hurtColor.g, hurtColor.b, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTex);
        }

        // Barre de vie en haut à gauche, tremble au moment du coup
        float w = 260f, h = 16f;
        float x = 24f, y = 22f;
        if (since < 0.3f)
        {
            x += Random.Range(-4f, 4f) * (1f - since / 0.3f);
            y += Random.Range(-3f, 3f) * (1f - since / 0.3f);
        }
        Bar(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), new Color(0f, 0f, 0f, 0.8f));
        Bar(new Rect(x, y, w, h), new Color(0.15f, 0.03f, 0.05f, 0.9f));
        Bar(new Rect(x, y, w * Mathf.Clamp01(displayLife / maxLife), h), new Color(1f, 0.9f, 0.85f));
        Color lifeC = Color.Lerp(new Color(0.85f, 0.1f, 0.15f), new Color(0.35f, 0.95f, 0.75f), ratio);
        Bar(new Rect(x, y, w * ratio, h), Color.Lerp(lifeC, Color.white, hit * 0.6f));
        Bar(new Rect(x, y, w * ratio, h * 0.35f), new Color(1f, 1f, 1f, 0.2f));
        GUI.color = old;
    }

    static void Bar(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
    }

    // Texture : transparente au centre, opaque sur les bords
    static Texture2D MakeVignette()
    {
        int s = 128;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int j = 0; j < s; j++)
        {
            for (int i = 0; i < s; i++)
            {
                float dx = (i + 0.5f) / s * 2f - 1f, dy = (j + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float al = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                t.SetPixel(i, j, new Color(1f, 1f, 1f, al));
            }
        }
        t.Apply();
        return t;
    }

    // Tremblement de la caméra principale (script camera)
    void ShakeCamera(float duration, float magnitude)
    {
        if (Camera.main != null)
        {
            camera cam = Camera.main.GetComponent<camera>();
            if (cam != null)
            {
                cam.Shake(duration, magnitude);
            }
        }
    }

    // Fige le jeu pendant duration secondes (temps réel), puis le relance
    System.Collections.IEnumerator HitStop(float duration)
    {
        isHitStopped = true;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        isHitStopped = false;
    }

    void Die()
    {
        // Handle player death (e.g., reload scene, show game over screen)
        Debug.Log("Player has died.");
    }
}
