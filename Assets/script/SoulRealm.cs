using System.Collections.Generic;
using UnityEngine;

// Décor low-poly : "l'endroit où vont les âmes".
//  - sol de pierre à facettes (visuel ; la collision reste celle du Plane)
//  - cristaux lumineux, pics rocheux, îles flottantes, montagnes au loin
//  - une grande lune-portail au fond, vers laquelle montent des âmes (particules)
//  - ciel sombre + brouillard pour la profondeur
// Tout est généré en code (graine fixe) et visible dans l'éditeur hors Play ([ExecuteAlways]).
[ExecuteAlways]
public class SoulRealm : MonoBehaviour
{
    [Header("Zone")]
    public float width = 120f;          // longueur du décor sur X (centré sur l'objet)
    public float groundY = -0.45f;      // hauteur du sol jouable (dessus du Plane)
    public int seed = 12345;

    [Header("Couleurs")]
    public Color skyColor = new Color(0.06f, 0.05f, 0.14f);
    public Color groundColor = new Color(0.2f, 0.19f, 0.3f);
    public Color rockColor = new Color(0.26f, 0.24f, 0.4f);
    public Color mountainColor = new Color(0.14f, 0.12f, 0.27f);
    public Color crystalColorA = new Color(0.35f, 0.85f, 1f);
    public Color crystalColorB = new Color(0.7f, 0.45f, 1f);
    public Color moonColor = new Color(0.85f, 0.92f, 1f);
    public Color soulColor = new Color(0.55f, 0.85f, 1f);

    [Header("Quantités")]
    public int crystalCount = 40;
    public int spireCount = 30;
    public int islandCount = 10;
    public int soulCount = 60;          // âmes visibles en même temps (environ)

    [Header("Ambiance")]
    public bool applyAtmosphere = true; // fond de caméra, brouillard, lumière ambiante
    public float fogStart = 12f;
    public float fogEnd = 160f;

    private readonly List<Object> generated = new List<Object>();
    private GameObject root;
    private Transform moon;
    private readonly List<Transform> islands = new List<Transform>();
    private readonly List<float> islandBaseY = new List<float>();
    private System.Random rng;

    void OnEnable()
    {
        Build();
    }

    void OnDisable()
    {
        foreach (Object o in generated)
        {
            if (o == null) continue;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
        generated.Clear();
        islands.Clear();
        islandBaseY.Clear();
        root = null;
    }

    void OnValidate()
    {
        // Reconstruit au prochain tick (créer/détruire est interdit pendant OnValidate)
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                OnDisable();
                Build();
            };
        }
#endif
    }

    void Update()
    {
        if (root == null) return;
        float t = Application.isPlaying ? Time.time : 0f;

        // Îles flottantes : lente lévitation
        for (int i = 0; i < islands.Count; i++)
        {
            Vector3 p = islands[i].localPosition;
            p.y = islandBaseY[i] + Mathf.Sin(t * 0.6f + i * 1.7f) * 0.3f;
            islands[i].localPosition = p;
        }

        // La lune reste au fond de l'écran : elle suit presque la caméra (très loin = presque immobile)
        Camera cam = Camera.main;
        if (moon != null && cam != null && Application.isPlaying)
        {
            Vector3 p = moon.position;
            p.x = cam.transform.position.x * 0.85f + transform.position.x * 0.15f;
            moon.position = p;
        }
    }

    // ---------- Construction ----------

    void Build()
    {
        if (root != null) return;
        rng = new System.Random(seed);

        root = new GameObject("SoulRealmVisual");
        Track(root);
        root.transform.SetParent(transform, false);

        if (applyAtmosphere) ApplyAtmosphere();

        Material groundMat = MakeMat(groundColor, 0.05f, false);
        Material rockMat = MakeMat(rockColor, 0.1f, false);
        Material mountainMat = MakeMat(mountainColor, 0f, false);
        Material crystalA = MakeMat(crystalColorA, 0.8f, true);
        Material crystalB = MakeMat(crystalColorB, 0.8f, true);
        // Lune : non éclairée et sans brouillard (Sprites/Default), pour briller au fond
        Material moonMat = new Material(Shader.Find("Sprites/Default")) { color = moonColor };
        Track(moonMat);

        BuildGround(groundMat);

        // Cristaux posés sur le sol, derrière la zone de jeu
        for (int i = 0; i < crystalCount; i++)
        {
            Vector3 pos = new Vector3(Range(-width / 2f, width / 2f), groundY, Range(1.8f, 7f));
            AddCrystalCluster(pos, Range(0.4f, 1.1f), Chance(0.5f) ? crystalA : crystalB);
        }

        // Pics rocheux en arrière-plan proche
        for (int i = 0; i < spireCount; i++)
        {
            float z = Range(9f, 22f);
            float h = Range(3f, 9f) * (z / 15f);
            Vector3 pos = new Vector3(Range(-width / 2f, width / 2f), groundY - 0.5f, z);
            MeshPart("Spire", Prism(6, Range(0.8f, 1.6f), Range(0.05f, 0.25f), h, 0.18f), rockMat, pos, Quaternion.Euler(Range(-6f, 6f), Range(0f, 360f), Range(-6f, 6f)));
        }

        // Îles flottantes avec un cristal dessus
        for (int i = 0; i < islandCount; i++)
        {
            Vector3 pos = new Vector3(Range(-width / 2f, width / 2f), Range(4f, 10f), Range(18f, 40f));
            float r = Range(1.5f, 3.5f);
            Transform island = MeshPart("Island", FloatingIsland(r, r * Range(1.2f, 2f)), rockMat, pos, Quaternion.Euler(0f, Range(0f, 360f), 0f));
            GameObject top = new GameObject("Crystals");
            Track(top);
            top.transform.SetParent(island, false);
            AddCrystalCluster(Vector3.zero, Range(0.8f, 1.6f), Chance(0.5f) ? crystalA : crystalB, top.transform);
            islands.Add(island);
            islandBaseY.Add(pos.y);
        }

        // Montagnes au loin : deux chaînes, la plus lointaine plus haute
        MeshPart("Mountains", Ridge(width + 160f, 50f, 8f, 22f, 4f), mountainMat, new Vector3(0f, groundY - 2f, 50f), Quaternion.identity);
        MeshPart("MountainsFar", Ridge(width + 260f, 80f, 14f, 38f, 7f), mountainMat, new Vector3(0f, groundY - 4f, 85f), Quaternion.identity);

        // Lune-portail : là où vont les âmes
        moon = MeshPart("SoulMoon", Icosphere(1, 0.04f), moonMat, new Vector3(0f, 22f, 120f), Quaternion.identity);
        moon.localScale = Vector3.one * 22f;

        BuildSouls();
    }

    void ApplyAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = skyColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(skyColor, soulColor, 0.35f);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyColor;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 300f);
        }
    }

    // Sol : bande à facettes. Plat là où on joue (z < 1.5), bosselé derrière, falaise devant.
    void BuildGround(Material mat)
    {
        float[] rowsZ = { -2.5f, -1f, 0.5f, 1.5f, 3f, 5f, 8f, 12f };
        float step = 1.2f;
        int cols = Mathf.CeilToInt(width / step) + 1;
        int rows = rowsZ.Length;
        float x0 = -width / 2f;

        Vector3[,] top = new Vector3[cols, rows];
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                float z = rowsZ[r];
                float bump = z > 1.5f ? Range(0f, 0.25f + (z - 1.5f) * 0.12f) : 0f;
                float jx = (c > 0 && c < cols - 1) ? Range(-0.35f, 0.35f) : 0f;
                float jz = z > 1.5f ? Range(-0.4f, 0.4f) : 0f;
                top[c, r] = new Vector3(x0 + c * step + jx, groundY + bump, z + jz);
            }
        }

        List<Vector3> v = new List<Vector3>();
        for (int c = 0; c < cols - 1; c++)
        {
            for (int r = 0; r < rows - 1; r++)
            {
                Quad(v, top[c, r], top[c + 1, r], top[c + 1, r + 1], top[c, r + 1], Vector3.up);
            }
            // Falaise à l'avant, vers le bas
            Vector3 a = top[c, 0], b = top[c + 1, 0];
            Vector3 a2 = new Vector3(a.x, groundY - Range(2.5f, 4f), a.z - Range(0.2f, 0.6f));
            Vector3 b2 = new Vector3(b.x, groundY - Range(2.5f, 4f), b.z - Range(0.2f, 0.6f));
            Quad(v, a, b, b2, a2, Vector3.back);
        }
        MeshPart("Ground", FromTriangles("Ground", v), mat, Vector3.zero, Quaternion.identity);
    }

    void AddCrystalCluster(Vector3 pos, float size, Material mat, Transform parent = null)
    {
        int n = rng.Next(1, 4);
        for (int i = 0; i < n; i++)
        {
            float h = size * Range(0.6f, 1.4f);
            Vector3 p = pos + new Vector3(Range(-0.3f, 0.3f), -0.05f, Range(-0.3f, 0.3f)) * size;
            Quaternion rot = Quaternion.Euler(Range(-25f, 25f), Range(0f, 360f), Range(-25f, 25f));
            MeshPart("Crystal", Crystal(h * 0.25f, h), mat, p, rot, parent);
        }
    }

    void BuildSouls()
    {
        GameObject go = new GameObject("Souls");
        Track(go);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(0f, groundY, 8f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = soulColor;
        main.maxParticles = soulCount * 2;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = soulCount / 11f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.5f, 14f);

        // Montent doucement en ondulant, vers le fond (la lune)
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        vel.z = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.3f;

        // Apparaissent et disparaissent en douceur
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Material m = new Material(Shader.Find("Sprites/Default"));
        Track(m);
        ps.GetComponent<ParticleSystemRenderer>().material = m;
        ps.Play();
    }

    // ---------- Formes low-poly ----------

    // Prisme effilé à n côtés (base rayon r0, sommet rayon r0*topRatio), sommets cabossés
    Mesh Prism(int n, float r0, float topRatio, float h, float jitter)
    {
        List<Vector3> v = new List<Vector3>();
        Vector3[] bottom = new Vector3[n], topRing = new Vector3[n];
        float off = Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < n; i++)
        {
            float a = off + i * Mathf.PI * 2f / n;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            bottom[i] = d * r0 * (1f + Range(-jitter, jitter));
            topRing[i] = d * r0 * topRatio * (1f + Range(-jitter, jitter)) + Vector3.up * h * (1f + Range(-0.08f, 0.08f));
        }
        Vector3 tip = Vector3.up * h * 1.08f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Quad(v, bottom[i], bottom[j], topRing[j], topRing[i], (bottom[i] + bottom[j]).normalized);
            Tri(v, topRing[i], topRing[j], tip, Vector3.up);
        }
        return FromTriangles("Prism", v);
    }

    // Cristal : bipyramide hexagonale allongée
    Mesh Crystal(float r, float h)
    {
        List<Vector3> v = new List<Vector3>();
        int n = 5;
        Vector3 top = Vector3.up * h, bottom = Vector3.down * h * 0.15f;
        for (int i = 0; i < n; i++)
        {
            float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r + Vector3.up * h * 0.55f;
            Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r + Vector3.up * h * 0.55f;
            Vector3 center = Vector3.up * h * 0.45f;
            Tri(v, p0, p1, top, (p0 + p1 + top) / 3f - center);
            Tri(v, p0, p1, bottom, (p0 + p1 + bottom) / 3f - center);
        }
        return FromTriangles("Crystal", v);
    }

    // Île flottante : dessus plat, dessous en pointe
    Mesh FloatingIsland(float r, float depth)
    {
        List<Vector3> v = new List<Vector3>();
        int n = 7;
        Vector3[] ring = new Vector3[n], mid = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            ring[i] = d * r * Range(0.8f, 1.15f) + Vector3.up * Range(-0.1f, 0.1f);
            mid[i] = d * r * Range(0.45f, 0.65f) + Vector3.down * depth * Range(0.35f, 0.5f);
        }
        Vector3 center = Vector3.up * 0.05f, tip = Vector3.down * depth;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Tri(v, ring[i], ring[j], center, Vector3.up);
            Vector3 outward = new Vector3(ring[i].x + ring[j].x, 0f, ring[i].z + ring[j].z);
            Quad(v, ring[i], ring[j], mid[j], mid[i], outward - Vector3.up * 0.3f);
            Tri(v, mid[i], mid[j], tip, outward - Vector3.up);
        }
        return FromTriangles("Island", v);
    }

    // Chaîne de montagnes : silhouette dentelée face à la caméra, avec un peu d'épaisseur
    Mesh Ridge(float w, float depth, float minH, float maxH, float step)
    {
        List<Vector3> v = new List<Vector3>();
        int n = Mathf.CeilToInt(w / step);
        float x0 = -w / 2f;
        Vector3 prevTop = Vector3.zero, prevBase = Vector3.zero, prevBack = Vector3.zero;
        for (int i = 0; i <= n; i++)
        {
            float x = x0 + i * step + (i > 0 && i < n ? Range(-step * 0.3f, step * 0.3f) : 0f);
            // Pics et cols en alternance
            float h = (i % 2 == 0) ? Range(minH * 0.4f, minH) : Range(minH, maxH);
            Vector3 topP = new Vector3(x, h, Range(-depth * 0.1f, depth * 0.15f));
            Vector3 baseP = new Vector3(x, 0f, -depth * 0.2f);
            Vector3 backP = new Vector3(x, h * 0.5f, depth * 0.5f);
            if (i > 0)
            {
                Quad(v, prevBase, baseP, topP, prevTop, Vector3.back);
                Quad(v, prevTop, topP, backP, prevBack, Vector3.up);
            }
            prevTop = topP; prevBase = baseP; prevBack = backP;
        }
        return FromTriangles("Ridge", v);
    }

    Mesh Icosphere(int sub, float jitter)
    {
        float p = (1f + Mathf.Sqrt(5f)) / 2f;
        List<Vector3> pts = new List<Vector3>
        {
            new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
            new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
            new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
        };
        List<int> t = new List<int>
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        for (int s = 0; s < sub; s++)
        {
            Dictionary<long, int> cache = new Dictionary<long, int>();
            List<int> nt = new List<int>();
            for (int i = 0; i < t.Count; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                int ab = Mid(pts, cache, a, b), bc = Mid(pts, cache, b, c), ca = Mid(pts, cache, c, a);
                nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            t = nt;
        }
        for (int i = 0; i < pts.Count; i++)
        {
            pts[i] = pts[i].normalized * 0.5f * (1f + Range(-jitter, jitter));
        }
        List<Vector3> v = new List<Vector3>();
        for (int i = 0; i < t.Count; i += 3)
        {
            Vector3 a = pts[t[i]], b = pts[t[i + 1]], c = pts[t[i + 2]];
            Tri(v, a, b, c, a + b + c);
        }
        return FromTriangles("Icosphere", v);
    }

    static int Mid(List<Vector3> v, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int i)) return i;
        v.Add((v[a] + v[b]) * 0.5f);
        cache[key] = v.Count - 1;
        return v.Count - 1;
    }

    // ---------- Outils ----------

    // Ajoute un triangle orienté pour que sa face visible regarde du côté de 'facing'
    static void Tri(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
        {
            Vector3 t = b; b = c; c = t;
        }
        v.Add(a); v.Add(b); v.Add(c);
    }

    static void Quad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
    {
        Tri(v, a, b, c, facing);
        Tri(v, a, c, d, facing);
    }

    // Mesh à facettes plates (chaque triangle a ses propres sommets)
    Mesh FromTriangles(string name, List<Vector3> v)
    {
        int[] tris = new int[v.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        Mesh m = new Mesh { name = name };
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        Track(m);
        return m;
    }

    Transform MeshPart(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        Track(go);
        go.transform.SetParent(parent != null ? parent : root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return go.transform;
    }

    Material MakeMat(Color c, float gloss, bool glow)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.SetFloat("_Glossiness", gloss);
        if (glow)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.5f);
        }
        Track(m);
        return m;
    }

    void Track(Object o)
    {
        // Objets générés : jamais sauvegardés dans la scène
        o.hideFlags = HideFlags.HideAndDontSave;
        generated.Add(o);
    }

    float Range(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    bool Chance(float p)
    {
        return rng.NextDouble() < p;
    }
}
