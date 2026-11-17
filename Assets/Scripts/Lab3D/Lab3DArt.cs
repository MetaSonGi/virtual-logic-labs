// 3D 회로 실험실 — 재질·도형·글자·빛 번짐·전선 튜브 메시 도우미
using UnityEngine;

public class Lab3DArt
{
    Material body, emissive, unlit, glow;
    Texture2D glowTex;
    public Font font;

    public static Lab3DArt Load()
    {
        var k = new Lab3DArt();
        k.body = Resources.Load<Material>("Lab3D/Body") ?? new Material(Shader.Find("Standard"));
        k.emissive = Resources.Load<Material>("Lab3D/Emissive") ?? k.body;
        k.unlit = Resources.Load<Material>("Lab3D/Unlit") ?? new Material(Shader.Find("Unlit/Color"));
        k.glow = Resources.Load<Material>("Lab3D/Glow") ?? k.unlit;
        k.font = UIKit.Font;
        k.glowTex = MakeGlowTexture();
        return k;
    }

    static Texture2D MakeGlowTexture()
    {
        const int s = 64;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.2f);
                px[y * s + x] = new Color(a, a, a, a);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    public Material Body(Color c, float metal = 0.25f, float smooth = 0.55f)
    {
        var m = new Material(body);
        m.color = c;
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    public Material Emissive(Color c, float intensity)
    {
        var m = new Material(emissive);
        SetGlow(m, c, c, intensity);
        return m;
    }

    public static void SetGlow(Material m, Color baseColor, Color glowColor, float intensity)
    {
        m.color = baseColor;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glowColor * intensity);
    }

    public Material Unlit(Color c)
    {
        var m = new Material(unlit) { color = c };
        return m;
    }

    static Mesh cylinderMesh;
    // referenced so web builds keep the native class that CreatePrimitive looks up
    public static readonly System.Type KeepCapsule = typeof(CapsuleCollider);

    public GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go;
        if (type == PrimitiveType.Cylinder)
        {
            // CreatePrimitive(Cylinder) wants CapsuleCollider, which web builds strip
            if (cylinderMesh == null) cylinderMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            go = new GameObject("Cylinder", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = cylinderMesh;
            go.AddComponent<BoxCollider>();
        }
        else go = GameObject.CreatePrimitive(type);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        // web builds can strip CapsuleCollider; cylinders still need something to click
        if (go.GetComponent<Collider>() == null && type != PrimitiveType.Quad) go.AddComponent<BoxCollider>();
        return go;
    }

    public GameObject Cube(Transform parent, Vector3 pos, Vector3 scale, Material mat) => Prim(PrimitiveType.Cube, parent, pos, scale, mat);
    public GameObject Sphere(Transform parent, Vector3 pos, float diameter, Material mat) => Prim(PrimitiveType.Sphere, parent, pos, Vector3.one * diameter, mat);

    public static void NoCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
    }

    /// <summary>Text lying flat on a surface (readable from the default camera on -z).</summary>
    public TextMesh FlatText(Transform parent, string text, Vector3 pos, float charSize, Color color)
    {
        var tm = Text(parent, text, pos, charSize, color);
        tm.transform.localRotation = Quaternion.Euler(90, 0, 0);
        return tm;
    }

    public TextMesh Text(Transform parent, string text, Vector3 pos, float charSize, Color color)
    {
        var go = new GameObject("Text");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return tm;
    }

    /// <summary>Additive camera-facing glow sprite.</summary>
    public Renderer Halo(Transform parent, Color color, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        NoCollider(go);
        go.name = "Halo";
        go.transform.SetParent(parent, false);
        float ps = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
        go.transform.localScale = Vector3.one * (size / ps);
        var r = go.GetComponent<Renderer>();
        var m = new Material(glow) { mainTexture = glowTex };
        r.sharedMaterial = m;
        SetHalo(r, color, 1);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.AddComponent<Billboard>();
        return r;
    }

    public static void SetHalo(Renderer r, Color c, float strength)
    {
        var m = r.sharedMaterial;
        if (m == null) return;
        var col = new Color(c.r * strength, c.g * strength, c.b * strength, Mathf.Clamp01(strength));
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", col * 0.5f);
        else m.color = col;
        r.enabled = strength > 0.01f;
    }

    // ---------- wires ----------
    public static Vector3[] Bezier(Vector3 a, Vector3 b, int n)
    {
        float d = Vector3.Distance(a, b);
        float h = 0.7f + d * 0.13f;
        float dx = Mathf.Max(1.2f, Mathf.Abs(b.x - a.x) * 0.45f);
        var p1 = new Vector3(a.x + dx, a.y + h, a.z);
        var p2 = new Vector3(b.x - dx, b.y + h, b.z);
        var pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1 - t;
            pts[i] = u * u * u * a + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * b;
        }
        return pts;
    }

    /// <summary>Hose shape: a soft arch between two ports in any direction.</summary>
    public static Vector3[] Arc(Vector3 a, Vector3 b, int n)
    {
        float d = Vector3.Distance(a, b);
        float h = 0.6f + d * 0.12f;
        var p1 = a + Vector3.up * h;
        var p2 = b + Vector3.up * h;
        var pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1 - t;
            pts[i] = u * u * u * a + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * b;
        }
        return pts;
    }

    public static void Tube(Mesh mesh, Vector3[] pts, float radius, int radial = 8)
    {
        int n = pts.Length;
        var verts = new Vector3[n * (radial + 1)];
        var normals = new Vector3[verts.Length];
        var tris = new int[(n - 1) * radial * 6];
        for (int i = 0; i < n; i++)
        {
            var tan = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            var side = Vector3.Cross(tan, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            var up = Vector3.Cross(side, tan).normalized;
            for (int j = 0; j <= radial; j++)
            {
                float a = j / (float)radial * Mathf.PI * 2;
                var nrm = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                verts[i * (radial + 1) + j] = pts[i] + nrm * radius;
                normals[i * (radial + 1) + j] = nrm;
            }
        }
        int k = 0;
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < radial; j++)
            {
                int a = i * (radial + 1) + j, b = a + radial + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
            }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }

    public static float[] Cumulative(Vector3[] pts)
    {
        var c = new float[pts.Length];
        for (int i = 1; i < pts.Length; i++) c[i] = c[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        return c;
    }

    public static Vector3 PointAt(Vector3[] pts, float[] cum, float u)
    {
        float target = u * cum[cum.Length - 1];
        int i = 1;
        while (i < cum.Length - 1 && cum[i] < target) i++;
        float seg = cum[i] - cum[i - 1];
        float t = seg > 1e-5f ? (target - cum[i - 1]) / seg : 0;
        return Vector3.Lerp(pts[i - 1], pts[i], t);
    }
}
