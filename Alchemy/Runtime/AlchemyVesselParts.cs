using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the pieces of a station's 3D contents and props from primitives at runtime (no assets needed): the liquid
/// column and its surface, layers, froth, sediment, chunks, the fire bed's fuel, the wooden spoon, the bellows and
/// the filter stand - each with its own material, opaque or see-through - and the particle systems (bubbles, steam,
/// smoke, specks, flames). <see cref="AlchemyStationVisuals"/> places and colours them from the preparation's state.
/// Projects with their own art assign their own renderers and materials instead.
/// </summary>
public static class AlchemyVesselParts
{
    private static Shader lit, particles;

    /// <summary>A new object of <paramref name="type"/> under <paramref name="parent"/>, without a collider (props are seen, not hit).</summary>
    public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color, bool transparent = false, float smoothness = 0.3f)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider c = go.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }
        go.transform.SetParent(parent, false);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = NewMaterial(color, transparent, smoothness);
        r.shadowCastingMode = transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
        return go;
    }

    /// <summary>An empty child named <paramref name="name"/>.</summary>
    public static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    /// <summary>A lit material of its own (opaque, or alpha-blended for liquids, froth and glass).</summary>
    public static Material NewMaterial(Color color, bool transparent, float smoothness = 0.3f)
    {
        if (lit == null)
        {
            lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Sprites/Default");
        }
        var m = new Material(lit) { name = transparent ? "Alchemy Liquid" : "Alchemy Prop", hideFlags = HideFlags.DontSave };
        if (transparent) MakeTransparent(m);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        SetColor(m, color);
        return m;
    }

    /// <summary>Switches a URP Lit or Standard material to alpha blending.</summary>
    public static void MakeTransparent(Material m)
    {
        if (m.HasProperty("_Surface"))
        {
            // URP Lit.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
        }
        else if (m.HasProperty("_Mode"))
        {
            // Standard.
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    public static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    /// <summary>Makes <paramref name="m"/> glow with <paramref name="c"/> (embers, a glowing brew); black turns it off.</summary>
    public static void SetEmission(Material m, Color c)
    {
        if (m == null || !m.HasProperty("_EmissionColor")) return;
        bool on = c.maxColorComponent > 0.01f;
        if (on) m.EnableKeyword("_EMISSION");
        else m.DisableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c);
    }

    /// <summary>
    /// A particle system that emits from a disc (<paramref name="radius"/>) upward: bubbles, steam, smoke, flames,
    /// specks. Rates are set every frame by the visuals.
    /// </summary>
    public static ParticleSystem Particles(string name, Transform parent, Color color, float radius, float lifetime, float speed, float size,
        float gravity = 0f, int max = 300, bool local = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit along +Y of the parent
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = max;
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy; // sized with the vessel it is in
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone; // a disc emitting straight along its axis
        shape.angle = 4f;
        shape.radius = Mathf.Max(0.001f, radius);
        shape.radiusThickness = 1f;
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    private static Material particleMaterial;

    /// <summary>A soft, unlit, alpha-blended particle material (the built-in default particle texture).</summary>
    public static Material ParticleMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;
        if (particles == null)
        {
            particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particles == null) particles = Shader.Find("Particles/Standard Unlit");
            if (particles == null) particles = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (particles == null) particles = Shader.Find("Sprites/Default");
        }
        particleMaterial = new Material(particles) { name = "Alchemy Particles", hideFlags = HideFlags.DontSave };
        if (particleMaterial.HasProperty("_Surface"))
        {
            particleMaterial.SetFloat("_Surface", 1f);
            particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        if (particleMaterial.HasProperty("_SrcBlend")) particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (particleMaterial.HasProperty("_DstBlend")) particleMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (particleMaterial.HasProperty("_ZWrite")) particleMaterial.SetFloat("_ZWrite", 0f);
        particleMaterial.renderQueue = (int)RenderQueue.Transparent;
        Texture2D soft = SoftDot();
        if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", soft);
        if (particleMaterial.HasProperty("_MainTex")) particleMaterial.SetTexture("_MainTex", soft);
        return particleMaterial;
    }

    private static Texture2D softDot;

    /// <summary>A round, soft-edged white dot (particles).</summary>
    public static Texture2D SoftDot()
    {
        if (softDot != null)
            return softDot;
        const int n = 32;
        softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Alchemy Soft Dot", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        softDot.SetPixels(px);
        softDot.Apply();
        return softDot;
    }

    /// <summary>
    /// An open cone (a funnel) of <paramref name="topRadius"/> narrowing to <paramref name="bottomRadius"/> over
    /// <paramref name="height"/>, both faces drawn, its tip at the origin.
    /// </summary>
    public static Mesh Funnel(float topRadius, float bottomRadius, float height, int segments = 24)
    {
        var mesh = new Mesh { name = "Alchemy Funnel" };
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * bottomRadius);
            v.Add(dir * topRadius + Vector3.up * height);
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            t.AddRange(new[] { a, b, c, c, b, d }); // outside
            t.AddRange(new[] { a, c, b, c, d, b }); // inside
        }
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// An open, round-bellied cauldron (no lid: what is in it shows) turned on a lathe: <paramref name="bottomRadius"/>
    /// at the foot, <paramref name="bellyRadius"/> at the widest, <paramref name="rimRadius"/> at the brim, walls
    /// <paramref name="wall"/> thick, its outer bottom at the origin. Its inner bottom is at height
    /// <c>wall + height × 0.02</c>.
    /// </summary>
    public static Mesh OpenVessel(float rimRadius, float bellyRadius, float bottomRadius, float height, float wall, int segments = 32)
    {
        float lip = wall * 0.8f;
        var profile = new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(bottomRadius, height * 0.02f), new Vector2(bellyRadius, height * 0.42f),
            new Vector2(rimRadius, height * 0.93f), new Vector2(rimRadius + lip, height), new Vector2(rimRadius - wall, height),
            new Vector2(bellyRadius - wall, height * 0.42f), new Vector2(Mathf.Max(0.01f, bottomRadius - wall), height * 0.02f + wall),
            new Vector2(0f, height * 0.02f + wall),
        };
        var mesh = new Mesh { name = "Alchemy Cauldron" };
        var v = new List<Vector3>();
        var t = new List<int>();
        int rings = segments + 1;
        foreach (Vector2 p in profile)
            for (int i = 0; i < rings; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
            }
        for (int k = 0; k < profile.Count - 1; k++)
            for (int i = 0; i < segments; i++)
            {
                int a = k * rings + i, b = (k + 1) * rings + i;
                t.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
            }
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>An object drawing <paramref name="mesh"/> with a material of its own.</summary>
    public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Color color, bool transparent = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = NewMaterial(color, transparent);
        return go;
    }

    /// <summary>A wooden spoon (handle along +Y from the bowl at the origin), sized for a vessel of <paramref name="radius"/>.</summary>
    public static Transform Spoon(Transform parent, float radius)
    {
        Transform root = Group("Wooden Spoon", parent);
        var wood = new Color(0.55f, 0.38f, 0.22f);
        float length = Mathf.Max(0.4f, radius * 2.4f);
        GameObject handle = Primitive(PrimitiveType.Cylinder, "Handle", root, wood);
        handle.transform.localScale = new Vector3(length * 0.035f, length * 0.5f, length * 0.035f);
        handle.transform.localPosition = new Vector3(0f, length * 0.5f + length * 0.04f, 0f);
        GameObject bowl = Primitive(PrimitiveType.Sphere, "Bowl", root, wood * 0.92f);
        bowl.transform.localScale = new Vector3(length * 0.11f, length * 0.035f, length * 0.16f);
        bowl.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        return root;
    }

    /// <summary>
    /// Bellows: two boards hinged at the nozzle (+Z points to the fire), a leather body between them; returns the root
    /// and the top board's hinge (rotate it about X to open the bellows).
    /// </summary>
    public static Transform Bellows(Transform parent, float size, out Transform topBoard, out Transform leather, out Transform nozzle)
    {
        Transform root = Group("Bellows", parent);
        var wood = new Color(0.45f, 0.3f, 0.18f);
        GameObject bottom = Primitive(PrimitiveType.Cube, "Bottom Board", root, wood);
        bottom.transform.localScale = new Vector3(size * 0.5f, size * 0.04f, size);
        bottom.transform.localPosition = new Vector3(0f, 0f, -size * 0.5f);
        topBoard = Group("Top Hinge", root);
        topBoard.localPosition = new Vector3(0f, size * 0.03f, 0f);
        GameObject top = Primitive(PrimitiveType.Cube, "Top Board", topBoard, wood * 1.08f);
        top.transform.localScale = new Vector3(size * 0.5f, size * 0.04f, size);
        top.transform.localPosition = new Vector3(0f, 0f, -size * 0.5f);
        GameObject handle = Primitive(PrimitiveType.Cylinder, "Handle", topBoard, wood * 0.8f);
        handle.transform.localScale = new Vector3(size * 0.06f, size * 0.18f, size * 0.06f);
        handle.transform.localPosition = new Vector3(0f, 0f, -size * 1.12f);
        handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject body = Primitive(PrimitiveType.Cube, "Leather", root, new Color(0.3f, 0.2f, 0.14f));
        leather = body.transform;
        leather.localPosition = new Vector3(0f, size * 0.05f, -size * 0.55f);
        leather.localScale = new Vector3(size * 0.46f, size * 0.08f, size * 0.85f);
        GameObject tip = Primitive(PrimitiveType.Cylinder, "Nozzle", root, new Color(0.35f, 0.33f, 0.3f), false, 0.7f);
        nozzle = tip.transform;
        nozzle.localScale = new Vector3(size * 0.07f, size * 0.22f, size * 0.07f);
        nozzle.localRotation = Quaternion.Euler(90f, 0f, 0f);
        nozzle.localPosition = new Vector3(0f, size * 0.02f, size * 0.2f);
        return root;
    }
}
