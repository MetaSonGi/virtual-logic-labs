// 3D 회로 실험실 — 3D 브레드보드, 전원 공급기, 드래그 전선 연결, 진리표
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Lab3D : MonoBehaviour
{
    const float PinY = 0.35f, BW = 24f, BD = 15f;
    const string SaveKey = "lab3d.circuit";
    static readonly PartType[] Palette = { PartType.Switch, PartType.Led, PartType.And, PartType.Or, PartType.Not, PartType.Nand, PartType.Nor, PartType.Xor };
    static readonly Color PinColor = UIKit.Hex("46557a");
    static readonly Color WireOff = UIKit.Hex("34405e");

    class PinView { public Transform t; public Material mat; public int pin; public bool isOut; public Vector3 local; }
    class PartView
    {
        public LPart part; public GameObject go; public Material bodyMat; public Color bodyColor;
        public readonly List<PinView> pins = new List<PinView>();
        public Transform pivot; public float lever;
        public Material capMat, stripMat, ledMat; public Renderer halo; public float drop = -1;
    }
    class WireView
    {
        public LWire w; public GameObject go; public Mesh mesh; public MeshCollider col; public Material mat;
        public Vector3[] pts; public float[] cum; public Transform[] pulses; public int val;
    }

    Lab3DArt art;
    Camera cam;
    LCircuit circuit = new LCircuit();
    bool power;
    SimResult sim;
    bool dirty = true;
    float saveAt = -1;

    readonly Dictionary<int, PartView> pv = new Dictionary<int, PartView>();
    readonly Dictionary<int, WireView> wv = new Dictionary<int, WireView>();

    // power supply
    Material psButtonMat, psDisplayMat;
    TextMesh psText;
    Renderer psHalo;
    readonly List<Material> railMats = new List<Material>();
    readonly List<Material> cableMats = new List<Material>();

    // temp wire
    GameObject tempGo; Mesh tempMesh; Material tempMat;

    // camera
    Vector3 target, tTarget, homeTarget;
    float yaw, pitch, dist, tYaw, tPitch, tDist, homeYaw, homePitch, homeDist;

    // interaction
    enum Drag { None, Camera, Pan, Wire, Part, Nothing }
    Drag drag;
    Vector2 downPos, lastMouse;
    bool moved;
    PinRef dragPin;
    PartView dragPart;
    Vector2 dragOff;
    bool pendingActive;
    PinRef pending;
    Transform hoverPin;
    int selKind, selId;
    float lastClickTime; int lastClickWire = -1;
    float clearArmedUntil;

    // UI
    Canvas canvas;
    RectTransform topBar, palette, side, selBar, toastRt, hintRt;
    readonly List<RectTransform> paletteButtons = new List<RectTransform>();
    Text ttText, ttState, selText, toastText, powerLine, hintText;
    Button delBtn;
    CanvasGroup toastGroup;
    float toastUntil;
    bool sideOpen = true;
    int lastW, lastH;

    // ---------------------------------------------------------------- setup
    void Start()
    {
        art = Lab3DArt.Load();
        SetupCameraAndLight();
        BuildBoard();
        BuildPowerSupply();
        BuildTempWire();
        BuildUI();

        var saved = PlayerPrefs.GetString(SaveKey, "");
        LCircuit loaded = null;
        if (!string.IsNullOrEmpty(saved)) { try { loaded = LCircuit.FromJson(saved); } catch { loaded = null; } }
        circuit = loaded ?? LCircuit.Example();
        SyncAll();
        ResetCamera(true);
        Toast("뒤쪽 전원 공급기의 POWER 버튼을 눌러 전기를 넣어 보세요", false, 5f);
    }

    void SetupCameraAndLight()
    {
        cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = UIKit.Bg;
        cam.fieldOfView = 42;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.95f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.6f;
        sun.transform.rotation = Quaternion.Euler(55, -25, 0);
        var rim = new GameObject("Rim").AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = UIKit.Violet;
        rim.intensity = 0.25f;
        rim.transform.rotation = Quaternion.Euler(20, 150, 0);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.22f, 0.27f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.09f, 0.11f, 0.18f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.035f, 0.06f);
        var dark = Resources.Load<Cubemap>("Lab3D/DarkReflection");
        if (dark != null)
        {
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = dark;
        }
        RenderSettings.reflectionIntensity = 0.5f;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = UIKit.Bg;
        RenderSettings.fogStartDistance = 55;
        RenderSettings.fogEndDistance = 120;
    }

    void BuildBoard()
    {
        var slab = art.Cube(null, new Vector3(0, -0.4f, 0), new Vector3(BW, 0.8f, BD), art.Body(UIKit.Hex("141c30"), 0.1f, 0.25f));
        slab.name = "Board";
        Lab3DArt.NoCollider(slab);

        // hole pattern texture on the top face
        const int ppu = 8;
        int tw = (int)BW * ppu, th = (int)BD * ppu;
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        var basec = UIKit.Hex("10172a");
        var hole = UIKit.Hex("05070d");
        var px = new Color[tw * th];
        for (int i = 0; i < px.Length; i++) px[i] = basec;
        for (int x = -(int)(BW / 2) + 1; x <= (int)(BW / 2) - 1; x++)
            for (int z = -6; z <= 6; z++)
            {
                float cx = (x + BW / 2) * ppu, cz = (z + BD / 2) * ppu;
                for (int yy = (int)cz - 2; yy <= (int)cz + 2; yy++)
                    for (int xx = (int)cx - 2; xx <= (int)cx + 2; xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= tw || yy >= th) continue;
                        float d = Vector2.Distance(new Vector2(xx + 0.5f, yy + 0.5f), new Vector2(cx, cz));
                        if (d < 1.8f) px[yy * tw + xx] = hole;
                    }
            }
        tex.SetPixels(px);
        tex.Apply();
        var topMat = art.Body(Color.white, 0.05f, 0.2f);
        topMat.mainTexture = tex;
        var top = art.Prim(PrimitiveType.Quad, null, new Vector3(0, 0.002f, 0), new Vector3(BW, BD, 1), topMat);
        top.transform.rotation = Quaternion.Euler(90, 0, 0);
        Lab3DArt.NoCollider(top);

        // neon edge
        var edgeMat = art.Emissive(UIKit.Accent, 0.9f);
        foreach (var z in new[] { -BD / 2, BD / 2 })
            Lab3DArt.NoCollider(art.Cube(null, new Vector3(0, 0, z), new Vector3(BW + 0.06f, 0.06f, 0.06f), edgeMat));
        foreach (var x in new[] { -BW / 2, BW / 2 })
            Lab3DArt.NoCollider(art.Cube(null, new Vector3(x, 0, 0), new Vector3(0.06f, 0.06f, BD + 0.06f), edgeMat));

        // power rails (+ red, - blue) along both long edges
        void Rail(float z, Color c)
        {
            var m = art.Emissive(c, 0.3f);
            railMats.Add(m);
            Lab3DArt.NoCollider(art.Cube(null, new Vector3(0, 0.01f, z), new Vector3(BW - 2, 0.03f, 0.1f), m));
        }
        float rz = BD / 2;
        Rail(-(rz - 0.75f), UIKit.Red); Rail(-(rz - 0.4f), UIKit.Accent);
        Rail(rz - 0.75f, UIKit.Red); Rail(rz - 0.4f, UIKit.Accent);

        var floor = art.Prim(PrimitiveType.Quad, null, new Vector3(0, -0.82f, 0), new Vector3(300, 300, 1), art.Body(UIKit.Hex("090e1d"), 0f, 0.05f));
        floor.transform.rotation = Quaternion.Euler(90, 0, 0);
        Lab3DArt.NoCollider(floor);
    }

    void BuildPowerSupply()
    {
        var root = new GameObject("PowerSupply").transform;
        root.position = new Vector3(-6, -0.82f, BD / 2 + 2.0f);
        var caseMat = art.Body(UIKit.Hex("1a2340"), 0.35f, 0.6f);
        var box = art.Cube(root, new Vector3(0, 0.95f, 0), new Vector3(6, 1.9f, 2.4f), caseMat);
        Lab3DArt.NoCollider(box);
        var face = art.Cube(root, new Vector3(0, 0.95f, -1.205f), new Vector3(5.6f, 1.5f, 0.02f), art.Body(UIKit.Hex("0b1020"), 0.2f, 0.7f));
        Lab3DArt.NoCollider(face);

        psDisplayMat = art.Emissive(UIKit.Amber, 0.1f);
        var display = art.Cube(root, new Vector3(-1.3f, 1.15f, -1.22f), new Vector3(2.4f, 0.8f, 0.03f), psDisplayMat);
        Lab3DArt.NoCollider(display);
        psText = art.Text(root, "0.0 V", new Vector3(-1.3f, 1.15f, -1.26f), 0.11f, UIKit.Hex("3a2a10"));
        art.Text(root, "5V 전원 공급기", new Vector3(-1.3f, 0.45f, -1.24f), 0.055f, UIKit.Text);

        psButtonMat = art.Emissive(UIKit.Red, 0.6f);
        var btn = art.Prim(PrimitiveType.Cylinder, root, new Vector3(1.7f, 1.05f, -1.3f), new Vector3(0.95f, 0.12f, 0.95f), psButtonMat);
        btn.transform.localRotation = Quaternion.Euler(90, 0, 0);
        btn.name = "PowerButton";
        btn.AddComponent<Pickable>().kind = 3;
        var ring = art.Prim(PrimitiveType.Cylinder, root, new Vector3(1.7f, 1.05f, -1.22f), new Vector3(1.25f, 0.04f, 1.25f), art.Body(UIKit.Hex("c8d2ee"), 0.9f, 0.8f));
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Lab3DArt.NoCollider(ring);
        art.Text(root, "POWER", new Vector3(1.7f, 0.35f, -1.24f), 0.06f, UIKit.Text);
        psHalo = art.Halo(btn.transform, UIKit.Red, 2.2f);
        psHalo.transform.localPosition = new Vector3(0, -1.2f, 0);

        // cables from the supply to the board rails
        void Cable(Vector3 a, Vector3 b, Color c)
        {
            var m = art.Emissive(c, 0.15f);
            cableMats.Add(m);
            var go = new GameObject("Cable", typeof(MeshFilter), typeof(MeshRenderer));
            var mesh = new Mesh();
            var pts = new Vector3[25];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / 24f;
                pts[i] = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.9f;
            }
            Lab3DArt.Tube(mesh, pts, 0.09f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
        Cable(root.position + new Vector3(2.4f, 0.4f, -1.2f), new Vector3(-1.5f, 0.02f, BD / 2 - 0.75f), UIKit.Red);
        Cable(root.position + new Vector3(2.8f, 0.4f, -1.2f), new Vector3(-0.5f, 0.02f, BD / 2 - 0.4f), UIKit.Accent);
    }

    void BuildTempWire()
    {
        tempGo = new GameObject("TempWire", typeof(MeshFilter), typeof(MeshRenderer));
        tempMesh = new Mesh();
        tempGo.GetComponent<MeshFilter>().sharedMesh = tempMesh;
        tempMat = art.Emissive(UIKit.Violet, 1.2f);
        tempGo.GetComponent<MeshRenderer>().sharedMaterial = tempMat;
        tempGo.SetActive(false);
    }

    // ---------------------------------------------------------------- part views
    PartView MakePartView(LPart p)
    {
        var v = new PartView { part = p, go = new GameObject("Part" + p.id) };
        v.go.transform.position = new Vector3(p.x, 0, p.z);
        var root = v.go.transform;

        GameObject Body(Vector3 pos, Vector3 scale, Material mat)
        {
            var b = art.Cube(root, pos, scale, mat);
            var pk = b.AddComponent<Pickable>();
            pk.kind = 1; pk.id = p.id;
            return b;
        }

        if (LCircuit.IsGate(p.type))
        {
            v.bodyColor = UIKit.Hex("0d111c");
            v.bodyMat = art.Body(v.bodyColor, 0.25f, 0.6f);
            Body(new Vector3(0, 0.425f, 0), new Vector3(2.2f, 0.55f, 1.6f), v.bodyMat);
            art.FlatText(root, LCircuit.Name(p.type), new Vector3(0, 0.706f, 0.18f), p.type.ToString().Length > 3 ? 0.085f : 0.1f, UIKit.Hex("eaf0ff"));
            art.FlatText(root, LCircuit.Expr(p.type), new Vector3(0, 0.706f, -0.45f), 0.05f, UIKit.Neon);
            var dot = art.Sphere(root, new Vector3(-0.88f, 0.7f, 0.58f), 0.12f, art.Body(UIKit.Muted));
            Lab3DArt.NoCollider(dot);
            int n = LCircuit.Inputs(p.type);
            var zs = n == 1 ? new[] { 0f } : new[] { -0.45f, 0.45f };
            for (int i = 0; i < n; i++) AddPin(v, i, false, -1.45f, zs[i], -1.1f);
            AddPin(v, 0, true, 1.45f, 0, 1.1f);
        }
        else if (p.type == PartType.Switch)
        {
            v.bodyColor = UIKit.Hex("161e33");
            v.bodyMat = art.Body(v.bodyColor, 0.3f, 0.5f);
            Body(new Vector3(0, 0.2f, 0), new Vector3(1.8f, 0.4f, 1.4f), v.bodyMat);
            v.pivot = new GameObject("Pivot").transform;
            v.pivot.SetParent(root, false);
            v.pivot.localPosition = new Vector3(0, 0.4f, 0);
            var lever = art.Prim(PrimitiveType.Cylinder, v.pivot, new Vector3(0, 0.4f, 0), new Vector3(0.2f, 0.4f, 0.2f), art.Body(UIKit.Hex("b8c2d8"), 0.9f, 0.7f));
            var lp = lever.AddComponent<Pickable>(); lp.kind = 1; lp.id = p.id;
            v.capMat = art.Emissive(UIKit.Amber, 0f);
            Lab3DArt.SetGlow(v.capMat, UIKit.Hex("3a3320"), UIKit.Amber, 0);
            var cap = art.Sphere(v.pivot, new Vector3(0, 0.8f, 0), 0.42f, v.capMat);
            var cp = cap.AddComponent<Pickable>(); cp.kind = 1; cp.id = p.id;
            v.stripMat = art.Emissive(UIKit.Amber, 0f);
            Lab3DArt.NoCollider(art.Cube(root, new Vector3(0, 0.25f, -0.71f), new Vector3(0.9f, 0.06f, 0.06f), v.stripMat));
            AddPin(v, 0, true, 1.3f, 0, 0.9f);
            Label(root, p.label, UIKit.Amber, 1.75f);
        }
        else
        {
            v.bodyColor = UIKit.Hex("161e33");
            v.bodyMat = art.Body(v.bodyColor, 0.3f, 0.5f);
            Body(new Vector3(0, 0.175f, 0), new Vector3(1.2f, 0.35f, 1.2f), v.bodyMat);
            v.ledMat = art.Emissive(UIKit.Neon, 0.05f);
            var dome = art.Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.38f, 0), new Vector3(0.84f, 0.64f, 0.84f), v.ledMat);
            var dp = dome.AddComponent<Pickable>(); dp.kind = 1; dp.id = p.id;
            v.halo = art.Halo(root, UIKit.Neon, 2.6f);
            v.halo.transform.localPosition = new Vector3(0, 0.55f, 0);
            AddPin(v, 0, false, -1.0f, 0, -0.6f);
            Label(root, p.label, UIKit.Neon, 1.6f);
        }
        return v;
    }

    void Label(Transform root, string text, Color color, float y)
    {
        var t = art.Text(root, text, new Vector3(0, y, 0), 0.13f, color);
        t.fontStyle = FontStyle.Bold;
        t.gameObject.AddComponent<Billboard>();
    }

    void AddPin(PartView v, int pin, bool isOut, float x, float z, float bodyEdge)
    {
        var mat = art.Emissive(PinColor, 0);
        Lab3DArt.SetGlow(mat, PinColor, Color.black, 0);
        var s = art.Sphere(v.go.transform, new Vector3(x, PinY, z), 0.4f, mat);
        ((SphereCollider)s.GetComponent<Collider>()).radius = 0.85f;
        var pk = s.AddComponent<Pickable>();
        pk.kind = 0; pk.id = v.part.id; pk.pin = pin; pk.isOut = isOut;
        float len = Mathf.Abs(x - bodyEdge);
        var leg = art.Cube(v.go.transform, new Vector3((x + bodyEdge) / 2, PinY, z), new Vector3(len, 0.08f, 0.08f), art.Body(UIKit.Hex("b8c2d8"), 0.9f, 0.7f));
        Lab3DArt.NoCollider(leg);
        v.pins.Add(new PinView { t = s.transform, mat = mat, pin = pin, isOut = isOut, local = new Vector3(x, PinY, z) });
    }

    Vector3 PinWorld(PinRef r)
    {
        if (!pv.TryGetValue(r.part, out var v)) return Vector3.zero;
        var p = v.pins.FirstOrDefault(x => x.pin == r.pin && x.isOut == r.isOut);
        return p == null ? v.go.transform.position : v.go.transform.position + p.local;
    }

    // ---------------------------------------------------------------- wire views
    void BuildWire(LWire w)
    {
        var a = PinWorld(new PinRef(w.fromPart, w.fromPin, true));
        var b = PinWorld(new PinRef(w.toPart, w.toPin, false));
        if (!wv.TryGetValue(w.id, out var v))
        {
            v = new WireView { w = w, go = new GameObject("Wire" + w.id, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)) };
            v.mesh = new Mesh();
            v.go.GetComponent<MeshFilter>().sharedMesh = v.mesh;
            v.mat = art.Emissive(WireOff, 0);
            v.go.GetComponent<MeshRenderer>().sharedMaterial = v.mat;
            v.col = v.go.GetComponent<MeshCollider>();
            var pk = v.go.AddComponent<Pickable>(); pk.kind = 2; pk.id = w.id;
            v.pulses = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var s = art.Sphere(null, Vector3.zero, 0.26f, art.Unlit(new Color(0.55f, 1f, 0.85f)));
                Lab3DArt.NoCollider(s);
                art.Halo(s.transform, UIKit.Neon, 1.3f);
                s.SetActive(false);
                v.pulses[i] = s.transform;
            }
            wv[w.id] = v;
        }
        v.pts = Lab3DArt.Bezier(a, b, 40);
        v.cum = Lab3DArt.Cumulative(v.pts);
        Lab3DArt.Tube(v.mesh, v.pts, 0.08f);
        v.col.sharedMesh = null;
        v.col.sharedMesh = v.mesh;
    }

    void RemoveWireView(int id)
    {
        if (!wv.TryGetValue(id, out var v)) return;
        Destroy(v.go);
        foreach (var p in v.pulses) Destroy(p.gameObject);
        wv.Remove(id);
    }

    void UpdateTemp(PinRef from, Vector3 to, bool snapped)
    {
        var a = PinWorld(from);
        var pts = from.isOut ? Lab3DArt.Bezier(a, to, 30) : Lab3DArt.Bezier(to, a, 30);
        Lab3DArt.Tube(tempMesh, pts, 0.065f, 6);
        Lab3DArt.SetGlow(tempMat, snapped ? UIKit.Neon : UIKit.Violet, snapped ? UIKit.Neon : UIKit.Violet, 1.2f);
        tempGo.SetActive(true);
    }

    void HideTemp() => tempGo.SetActive(false);

    // ---------------------------------------------------------------- sync
    void SyncAll()
    {
        foreach (var v in pv.Values) Destroy(v.go);
        pv.Clear();
        foreach (var id in wv.Keys.ToList()) RemoveWireView(id);
        foreach (var p in circuit.parts) pv[p.id] = MakePartView(p);
        foreach (var w in circuit.wires) BuildWire(w);
        Select(0, 0);
        Changed();
    }

    void SyncDiff()
    {
        var ids = new HashSet<int>(circuit.parts.Select(p => p.id));
        foreach (var id in pv.Keys.Where(k => !ids.Contains(k)).ToList()) { Destroy(pv[id].go); pv.Remove(id); }
        foreach (var p in circuit.parts) if (!pv.ContainsKey(p.id)) pv[p.id] = MakePartView(p);
        var wids = new HashSet<int>(circuit.wires.Select(w => w.id));
        foreach (var id in wv.Keys.Where(k => !wids.Contains(k)).ToList()) RemoveWireView(id);
        foreach (var w in circuit.wires) if (!wv.ContainsKey(w.id)) BuildWire(w);
        Changed();
    }

    void RebuildWiresFor(int partId)
    {
        foreach (var w in circuit.wires) if (w.fromPart == partId || w.toPart == partId) BuildWire(w);
    }

    void Changed()
    {
        dirty = true;
        saveAt = Time.time + 0.4f;
    }

    // ---------------------------------------------------------------- placement
    static Vector2 Footprint(PartType t) => t == PartType.Switch ? new Vector2(3.2f, 2f) : t == PartType.Led ? new Vector2(2.8f, 1.8f) : new Vector2(3.8f, 2.2f);

    static bool InBounds(PartType t, int x, int z)
    {
        var f = Footprint(t);
        return Mathf.Abs(x) <= BW / 2 - f.x / 2 && Mathf.Abs(z) <= BD / 2 - f.y / 2 - 0.4f;
    }

    bool Overlaps(PartType t, int x, int z, int ignore)
    {
        var f = Footprint(t);
        foreach (var p in circuit.parts)
        {
            if (p.id == ignore) continue;
            var g = Footprint(p.type);
            if (Mathf.Abs(p.x - x) < (f.x + g.x) / 2 && Mathf.Abs(p.z - z) < (f.y + g.y) / 2) return true;
        }
        return false;
    }

    void AddFromPalette(PartType t)
    {
        for (int r = 0; r < 16; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    if (!InBounds(t, dx, dz) || Overlaps(t, dx, dz, -1)) continue;
                    var p = circuit.Add(t, dx, dz);
                    SyncDiff();
                    Select(1, p.id);
                    pv[p.id].drop = 0;
                    pv[p.id].go.transform.position += Vector3.up * 2.5f;
                    return;
                }
        Toast("보드에 빈 자리가 없어요", true);
    }

    // ---------------------------------------------------------------- input
    bool OverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return es.IsPointerOverGameObject();
    }

    Pickable Pick(Vector2 sp)
    {
        var ray = cam.ScreenPointToRay(sp);
        Pickable best = null;
        float bestScore = float.MaxValue;
        foreach (var h in Physics.RaycastAll(ray, 300f))
        {
            var p = h.collider.GetComponent<Pickable>();
            if (p == null) continue;
            float bonus = p.kind == 0 ? 0.6f : p.kind == 2 ? 0f : 0.25f;
            float s = h.distance - bonus;
            if (s < bestScore) { bestScore = s; best = p; }
        }
        return best;
    }

    bool Ground(Vector2 sp, out Vector3 pt)
    {
        var ray = cam.ScreenPointToRay(sp);
        var plane = new Plane(Vector3.up, new Vector3(0, PinY, 0));
        if (plane.Raycast(ray, out float e)) { pt = ray.GetPoint(e); return true; }
        pt = default;
        return false;
    }

    static PinRef ToPin(Pickable p) => new PinRef(p.id, p.pin, p.isOut);
    static bool CanLink(PinRef a, Pickable h) => h != null && h.kind == 0 && h.isOut != a.isOut && h.id != a.part;

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) DeleteSelected();
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelPending(); Select(0, 0); }
        if (Input.GetKeyDown(KeyCode.R)) ResetCamera(false);
        if (Input.GetKeyDown(KeyCode.Space)) TogglePower();

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && !OverUI()) tDist = Mathf.Clamp(tDist * (1 - wheel * 0.1f), 12, 75);

        if (Input.touchCount >= 2)
        {
            var a = Input.GetTouch(0); var b = Input.GetTouch(1);
            float prev = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
            float cur = Vector2.Distance(a.position, b.position);
            if (prev > 1 && cur > 1) tDist = Mathf.Clamp(tDist * prev / cur, 12, 75);
            Pan((a.deltaPosition + b.deltaPosition) * 0.5f);
            if (drag == Drag.Wire) HideTemp();
            drag = Drag.None;
            return;
        }

        Vector2 mp = Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && !OverUI()) PointerDown(mp);
        else if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && !OverUI()) { drag = Drag.Pan; downPos = lastMouse = mp; moved = false; }

        bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
        if (drag != Drag.None && held) PointerMove(mp);
        else if (drag == Drag.None) Hover(mp);

        if (drag != Drag.None && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))) PointerUp(mp);
    }

    void PointerDown(Vector2 mp)
    {
        downPos = lastMouse = mp;
        moved = false;
        var hit = Pick(mp);
        if (pendingActive)
        {
            var from = pending;
            CancelPending();
            if (hit != null && hit.kind == 0)
            {
                var to = ToPin(hit);
                if (!to.Same(from)) TryConnect(from, to);
                drag = Drag.Nothing;
                return;
            }
        }
        if (hit == null) { Select(0, 0); drag = Drag.Camera; return; }
        switch (hit.kind)
        {
            case 0:
                drag = Drag.Wire;
                dragPin = ToPin(hit);
                break;
            case 1:
                dragPart = pv[hit.id];
                dragOff = Ground(mp, out var pt) ? new Vector2(pt.x - dragPart.part.x, pt.z - dragPart.part.z) : Vector2.zero;
                drag = Drag.Part;
                if (dragPart.part.type != PartType.Switch) Select(1, hit.id);
                break;
            case 2:
                drag = Drag.Nothing;
                if (Time.time - lastClickTime < 0.35f && lastClickWire == hit.id)
                {
                    circuit.RemoveWire(hit.id);
                    Select(0, 0);
                    SyncDiff();
                    lastClickWire = -1;
                    return;
                }
                lastClickTime = Time.time;
                lastClickWire = hit.id;
                Select(2, hit.id);
                break;
            case 3:
                drag = Drag.Nothing;
                TogglePower();
                break;
        }
    }

    void PointerMove(Vector2 mp)
    {
        var delta = mp - lastMouse;
        lastMouse = mp;
        if ((mp - downPos).magnitude > 6) moved = true;
        switch (drag)
        {
            case Drag.Camera:
                if (!moved) break;
                tYaw += delta.x * 0.3f;
                tPitch = Mathf.Clamp(tPitch - delta.y * 0.25f, 15, 85);
                break;
            case Drag.Pan:
                Pan(delta);
                break;
            case Drag.Wire:
                if (!moved) break;
                {
                    var hit = Pick(mp);
                    bool snap = CanLink(dragPin, hit);
                    Vector3 to;
                    if (snap) to = PinWorld(ToPin(hit));
                    else if (!Ground(mp, out to)) break;
                    UpdateTemp(dragPin, to, snap);
                    SetHover(snap ? hit : null);
                }
                break;
            case Drag.Part:
                if (!moved || !Ground(mp, out var pt)) break;
                {
                    var p = dragPart.part;
                    int nx = Mathf.RoundToInt(pt.x - dragOff.x), nz = Mathf.RoundToInt(pt.z - dragOff.y);
                    if ((nx != p.x || nz != p.z) && InBounds(p.type, nx, nz) && !Overlaps(p.type, nx, nz, p.id))
                    {
                        p.x = nx; p.z = nz;
                        dragPart.go.transform.position = new Vector3(nx, 0, nz);
                        RebuildWiresFor(p.id);
                        Changed();
                    }
                    if (selKind != 1 || selId != p.id) Select(1, p.id);
                }
                break;
        }
    }

    void PointerUp(Vector2 mp)
    {
        var d = drag;
        drag = Drag.None;
        if (d == Drag.Wire)
        {
            var hit = Pick(mp);
            if (hit != null && hit.kind == 0 && !ToPin(hit).Same(dragPin)) { HideTemp(); TryConnect(dragPin, ToPin(hit)); }
            else if (!moved)
            {
                pendingActive = true;
                pending = dragPin;
                if (Ground(mp, out var pt)) UpdateTemp(pending, pt, false);
                Toast(dragPin.isOut ? "연결할 입력 핀(부품 왼쪽)을 누르세요" : "연결할 출력 핀(부품 오른쪽)을 누르세요");
            }
            else HideTemp();
            SetHover(null);
        }
        else if (d == Drag.Part && !moved && dragPart.part.type == PartType.Switch)
        {
            dragPart.part.on = !dragPart.part.on;
            Changed();
            if (!power) Toast("전원이 꺼져 있어요 — 뒤쪽 전원 공급기의 POWER 버튼을 누르세요", true);
        }
    }

    void Hover(Vector2 mp)
    {
        if (OverUI()) { SetHover(null); return; }
        var hit = Pick(mp);
        if (pendingActive)
        {
            bool snap = CanLink(pending, hit);
            Vector3 to;
            if (snap) to = PinWorld(ToPin(hit));
            else if (!Ground(mp, out to)) return;
            UpdateTemp(pending, to, snap);
            SetHover(snap ? hit : null);
        }
        else SetHover(hit != null && hit.kind == 0 ? hit : null);
    }

    void SetHover(Pickable hit)
    {
        var t = hit != null && hit.kind == 0 ? hit.transform : null;
        if (t == hoverPin) return;
        if (hoverPin != null) hoverPin.localScale = Vector3.one * 0.4f;
        hoverPin = t;
        if (t != null) t.localScale = Vector3.one * 0.58f;
    }

    void CancelPending()
    {
        pendingActive = false;
        HideTemp();
    }

    void TryConnect(PinRef a, PinRef b)
    {
        if (a.part == b.part) { Toast("같은 부품끼리는 연결할 수 없어요", true); return; }
        if (a.isOut == b.isOut) { Toast(a.isOut ? "출력 핀은 다른 부품의 입력 핀(왼쪽)에 연결하세요" : "입력 핀은 다른 부품의 출력 핀(오른쪽)에 연결하세요", true); return; }
        circuit.Connect(a, b);
        SyncDiff();
    }

    void Pan(Vector2 delta)
    {
        float s = tDist * 0.0016f;
        var right = cam.transform.right; right.y = 0; right.Normalize();
        var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
        tTarget -= (right * delta.x + fwd * delta.y) * s;
        tTarget.x = Mathf.Clamp(tTarget.x, -20, 20);
        tTarget.z = Mathf.Clamp(tTarget.z, -14, 16);
    }

    void TogglePower()
    {
        power = !power;
        Changed();
        Toast(power ? "전원 ON — 켜진 전선을 따라 신호가 흐릅니다" : "전원 OFF — 모든 신호가 꺼졌어요", !power);
    }

    // ---------------------------------------------------------------- selection
    void Select(int kind, int id)
    {
        if (selKind == 1 && pv.TryGetValue(selId, out var old)) Lab3DArt.SetGlow(old.bodyMat, old.bodyColor, Color.black, 0);
        selKind = kind; selId = id;
        if (kind == 1 && pv.TryGetValue(id, out var v)) Lab3DArt.SetGlow(v.bodyMat, v.bodyColor, UIKit.Violet, 0.35f);
        dirty = true;
        if (selBar == null) return;
        selBar.gameObject.SetActive(kind != 0);
        if (kind == 2) selText.text = "전선";
        else if (kind == 1)
        {
            var p = circuit.Get(id);
            selText.text = p == null ? "" : LCircuit.IsGate(p.type) ? $"{LCircuit.Name(p.type)} 게이트" : $"{LCircuit.Name(p.type)} {p.label}";
        }
    }

    void DeleteSelected()
    {
        if (selKind == 2) circuit.RemoveWire(selId);
        else if (selKind == 1) circuit.RemovePart(selId);
        else return;
        Select(0, 0);
        SyncDiff();
    }

    // ---------------------------------------------------------------- camera
    void ResetCamera(bool instant)
    {
        bool portrait = Screen.height > Screen.width;
        homeYaw = portrait ? 90 : 180;
        homePitch = portrait ? 52 : 47;
        if (portrait)
        {
            homeTarget = new Vector3(-0.5f, 0, 1.5f);
            homeDist = 50;
        }
        else
        {
            // fit the board into the gap between the parts palette (left) and the truth table (right)
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float leftPx = 180 * scale, rightPx = (sideOpen ? 324 : 12) * scale;
            float free = Mathf.Clamp(1 - (leftPx + rightPx) / Screen.width, 0.35f, 1f);
            float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            homeDist = Mathf.Clamp(24.5f / (2 * halfTan * aspect * free), 28f, 70f);
            float visibleW = 2 * halfTan * aspect * homeDist;
            float shift = (rightPx - leftPx) / 2 / Screen.width * visibleW;
            homeTarget = new Vector3(shift, 0, 1.2f);
        }
        tTarget = homeTarget; tYaw = homeYaw; tPitch = homePitch; tDist = homeDist;
        if (instant) { target = tTarget; yaw = tYaw; pitch = tPitch; dist = tDist; ApplyCamera(); }
    }

    void UpdateCamera()
    {
        float k = 1 - Mathf.Exp(-Time.deltaTime * 10);
        target = Vector3.Lerp(target, tTarget, k);
        yaw = Mathf.Lerp(yaw, tYaw, k);
        pitch = Mathf.Lerp(pitch, tPitch, k);
        dist = Mathf.Lerp(dist, tDist, k);
        ApplyCamera();
    }

    void ApplyCamera()
    {
        float ry = yaw * Mathf.Deg2Rad, rp = pitch * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Sin(ry) * Mathf.Cos(rp), Mathf.Sin(rp), Mathf.Cos(ry) * Mathf.Cos(rp));
        cam.transform.position = target + dir * dist;
        cam.transform.LookAt(target);
    }

    // ---------------------------------------------------------------- simulation → visuals
    void ApplySim()
    {
        sim = circuit.Simulate(power);
        foreach (var v in pv.Values)
        {
            var id = v.part.id;
            sim.ins.TryGetValue(id, out var ins);
            sim.outs.TryGetValue(id, out var outs);
            foreach (var pn in v.pins)
            {
                int val = pn.isOut ? (outs != null && pn.pin < outs.Length ? outs[pn.pin] : 0) : (ins != null && pn.pin < ins.Length ? ins[pn.pin] : 0);
                if (val == 1) Lab3DArt.SetGlow(pn.mat, UIKit.Neon, UIKit.Neon, 2.2f);
                else Lab3DArt.SetGlow(pn.mat, PinColor, Color.black, 0);
            }
            if (v.part.type == PartType.Led)
            {
                bool lit = ins != null && ins.Length > 0 && ins[0] == 1;
                Lab3DArt.SetGlow(v.ledMat, lit ? UIKit.Hex("40ffc8") : UIKit.Hex("0c3326"), UIKit.Neon, lit ? 3f : 0.04f);
                Lab3DArt.SetHalo(v.halo, UIKit.Neon, lit ? 1f : 0f);
            }
            if (v.part.type == PartType.Switch)
            {
                bool glow = v.part.on && power;
                Lab3DArt.SetGlow(v.capMat, v.part.on ? UIKit.Amber : UIKit.Hex("3a3320"), UIKit.Amber, glow ? 2.6f : 0);
                Lab3DArt.SetGlow(v.stripMat, UIKit.Hex("222222"), UIKit.Amber, glow ? 2.4f : 0);
            }
        }
        foreach (var w in circuit.wires)
        {
            if (!wv.TryGetValue(w.id, out var v)) continue;
            v.val = sim.wire.TryGetValue(w.id, out var val) ? val : 0;
            bool selected = selKind == 2 && selId == w.id;
            if (selected) Lab3DArt.SetGlow(v.mat, UIKit.Amber, UIKit.Amber, 0.8f);
            else if (v.val == 1) Lab3DArt.SetGlow(v.mat, UIKit.Neon, UIKit.Neon, 1.4f);
            else Lab3DArt.SetGlow(v.mat, WireOff, Color.black, 0);
            foreach (var p in v.pulses) p.gameObject.SetActive(v.val == 1);
        }

        // power supply
        psText.text = power ? "5.0 V" : "0.0 V";
        psText.color = power ? UIKit.Hex("2a1800") : UIKit.Hex("3a2a10");
        Lab3DArt.SetGlow(psDisplayMat, power ? UIKit.Amber : UIKit.Hex("2a2210"), UIKit.Amber, power ? 1.6f : 0.08f);
        Lab3DArt.SetGlow(psButtonMat, power ? UIKit.Neon : UIKit.Hex("5a1622"), power ? UIKit.Neon : UIKit.Red, power ? 2.4f : 0.7f);
        for (int i = 0; i < railMats.Count; i++)
        {
            var c = i % 2 == 0 ? UIKit.Red : UIKit.Accent;
            Lab3DArt.SetGlow(railMats[i], c, c, power ? 1.6f : 0.25f);
        }
        for (int i = 0; i < cableMats.Count; i++)
        {
            var c = i == 0 ? UIKit.Red : UIKit.Accent;
            Lab3DArt.SetGlow(cableMats[i], c * 0.6f, c, power ? 1.0f : 0.05f);
        }
        RenderTable();
    }

    void RenderTable()
    {
        if (ttText == null) return;
        var rows = circuit.Table(out var ins, out var outs, out bool stable);
        powerLine.text = power ? "<color=#00e5a0>● 전원 ON</color>  5.0 V 공급 중" : "<color=#ff5470>● 전원 OFF</color>  POWER 버튼(스페이스바)을 누르세요";
        if (ins.Count == 0 || outs.Count == 0)
        {
            ttState.text = "대기";
            ttText.text = "<color=#8b98b8>스위치와 LED를 놓고 전선으로 이으면\n진리표가 자동으로 만들어집니다.</color>";
            return;
        }
        ttState.text = $"입력 {ins.Count} · 출력 {outs.Count}";
        var sb = new System.Text.StringBuilder();
        sb.Append("<color=#8b98b8>");
        foreach (var p in ins) sb.Append(Pad(p.label));
        sb.Append("</color><color=#4a5677>|  </color><color=#00e5a0>");
        foreach (var p in outs) sb.Append(Pad(p.label));
        sb.Append("</color>\n");
        foreach (var r in rows)
        {
            foreach (var b in r.bits) sb.Append(Bit(b));
            sb.Append("<color=#4a5677>|  </color>");
            foreach (var o in r.outs) sb.Append(Bit(o));
            if (r.current && power) sb.Append("<color=#4f7cff>◀ 지금</color>");
            sb.Append('\n');
        }
        if (!stable) sb.Append("<color=#ffb020>피드백 루프 때문에 값이 안정되지 않아요</color>\n");
        if (circuit.parts.Count(p => p.type == PartType.Switch) > 4) sb.Append("<color=#8b98b8>스위치는 A~D까지만 표에 나옵니다</color>\n");
        ttText.text = sb.ToString();
    }

    static string Pad(string s) => s.Length >= 3 ? s + " " : s.PadRight(4);
    static string Bit(int v) => v == 1 ? "<color=#00e5a0><b>1</b></color>    " : "<color=#4a5677>0</color>    ";

    // ---------------------------------------------------------------- per-frame
    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH)
        {
            bool wasPortrait = lastH > lastW;
            lastW = Screen.width; lastH = Screen.height;
            Layout();
            if (wasPortrait != (lastH > lastW)) ResetCamera(true);
        }
        if (!fitted && Time.frameCount > 3) { fitted = true; ResetCamera(true); }
        HandleInput();
        UpdateCamera();
        if (dirty) { dirty = false; ApplySim(); }
        if (saveAt > 0 && Time.time > saveAt)
        {
            saveAt = -1;
            PlayerPrefs.SetString(SaveKey, circuit.ToJson());
            PlayerPrefs.Save();
        }
        Animate();
        if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.time < toastUntil ? 1 : 0, Time.deltaTime * 4);
    }

    void Animate()
    {
        float dt = Time.deltaTime, t = Time.time;
        foreach (var v in pv.Values)
        {
            if (v.drop >= 0)
            {
                v.drop += dt * 3;
                float k = Mathf.Min(1, v.drop);
                var pos = v.go.transform.position;
                pos.y = 2.5f * (1 - k) * (1 - k);
                v.go.transform.position = pos;
                if (k >= 1) { v.drop = -1; pos.y = 0; v.go.transform.position = pos; }
            }
            if (v.pivot != null)
            {
                float goal = v.part.on ? -28 : 28;
                v.lever = Mathf.Lerp(v.lever, goal, 1 - Mathf.Exp(-dt * 14));
                v.pivot.localRotation = Quaternion.Euler(0, 0, v.lever);
            }
        }
        foreach (var v in wv.Values)
        {
            if (v.val != 1 || v.pts == null) continue;
            float len = v.cum[v.cum.Length - 1];
            for (int i = 0; i < v.pulses.Length; i++)
            {
                float u = Mathf.Repeat(t * 5f / Mathf.Max(1, len) + i / 3f, 1f);
                v.pulses[i].position = Lab3DArt.PointAt(v.pts, v.cum, u);
            }
        }
        // the power button breathes while it is off, to invite a click
        Lab3DArt.SetHalo(psHalo, power ? UIKit.Neon : UIKit.Red, power ? 0.8f : 0.35f + 0.35f * Mathf.Sin(t * 3.5f));
    }

    // ---------------------------------------------------------------- UI
    void BuildUI()
    {
        canvas = UIKit.MakeCanvas();
        var c = canvas.transform;

        topBar = UIKit.Rect(c, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 58));
        UIKit.Box(topBar, new Color(0.027f, 0.043f, 0.086f, 0.88f));
        UIKit.Button(UIKit.Rect(topBar, "Menu", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(96, 38)),
            "< 메뉴", 17, UIKit.Bg2, UIKit.Text, () => SceneManager.LoadScene("Scenes/Start"), UIKit.Border);
        var title = UIKit.Label(topBar, "<color=#00e5a0>◆</color> 3D 회로 실험실", 22, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        title.rectTransform.offsetMin = new Vector2(122, 0);
        title.rectTransform.offsetMax = new Vector2(-480, 0);

        string[] names = { "전원", "예제", "초기화", "카메라", "진리표" };
        System.Action[] acts = { TogglePower, LoadExample, ClearBoard, () => ResetCamera(false), () => { sideOpen = !sideOpen; Layout(); ResetCamera(false); } };
        for (int i = 0; i < names.Length; i++)
        {
            var rt = UIKit.Rect(topBar, names[i], new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12 - (names.Length - 1 - i) * 90, 0), new Vector2(84, 38));
            bool isPower = i == 0;
            UIKit.Button(rt, names[i], 16, isPower ? new Color(0.35f, 0.08f, 0.13f) : UIKit.Bg2, UIKit.Text, acts[i], isPower ? UIKit.Red : UIKit.Border);
        }

        palette = UIKit.Rect(c, "Palette", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -70), new Vector2(156, 470));
        UIKit.Box(palette, UIKit.Panel, UIKit.Border);
        var ph = UIKit.Label(palette, "부품", 14, UIKit.Muted, TextAnchor.UpperLeft, FontStyle.Bold);
        ph.rectTransform.offsetMin = new Vector2(14, 0); ph.rectTransform.offsetMax = new Vector2(0, -10);
        string[] subs = { "입력 ON/OFF", "출력 표시", "둘 다 1", "하나라도 1", "뒤집기", "AND의 반대", "OR의 반대", "서로 다르면 1" };
        for (int i = 0; i < Palette.Length; i++)
        {
            var t = Palette[i];
            var rt = UIKit.Rect(palette, t.ToString(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(132, 46));
            Color accent = t == PartType.Switch ? UIKit.Amber : t == PartType.Led ? UIKit.Neon : UIKit.Text;
            var b = UIKit.Button(rt, "", 16, UIKit.Bg2, accent, () => AddFromPalette(t), UIKit.Border);
            var lbl = b.GetComponentInChildren<Text>();
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.text = $"{LCircuit.Name(t)}  <size=12><color=#8b98b8>{subs[i]}</color></size>";
            lbl.rectTransform.offsetMin = new Vector2(12, 0);
            paletteButtons.Add(rt);
        }

        side = UIKit.Rect(c, "Side", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -70), new Vector2(300, 400));
        UIKit.Box(side, UIKit.Panel, UIKit.Border);
        var sh = UIKit.Label(side, "진리표", 18, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
        sh.rectTransform.offsetMin = new Vector2(16, 0); sh.rectTransform.offsetMax = new Vector2(-16, -14);
        ttState = UIKit.Label(side, "-", 14, UIKit.Neon, TextAnchor.UpperRight);
        ttState.rectTransform.offsetMin = new Vector2(16, 0); ttState.rectTransform.offsetMax = new Vector2(-16, -16);
        powerLine = UIKit.Label(side, "", 14, UIKit.Muted, TextAnchor.UpperLeft);
        powerLine.rectTransform.offsetMin = new Vector2(16, 0); powerLine.rectTransform.offsetMax = new Vector2(-16, -46);
        ttText = UIKit.Label(side, "", 19, UIKit.Text, TextAnchor.UpperLeft);
        ttText.rectTransform.offsetMin = new Vector2(20, 12); ttText.rectTransform.offsetMax = new Vector2(-16, -80);
        ttText.lineSpacing = 1.25f;

        selBar = UIKit.Rect(c, "SelBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(330, 48));
        UIKit.Box(selBar, UIKit.Panel, UIKit.Violet);
        selText = UIKit.Label(selBar, "-", 17, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        selText.rectTransform.offsetMin = new Vector2(18, 0); selText.rectTransform.offsetMax = new Vector2(-120, 0);
        delBtn = UIKit.Button(UIKit.Rect(selBar, "Delete", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(104, 36)),
            "삭제", 16, new Color(0.4f, 0.1f, 0.15f), UIKit.Red, DeleteSelected);
        selBar.gameObject.SetActive(false);

        hintRt = UIKit.Rect(c, "Hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(980, 34));
        UIKit.Box(hintRt, new Color(0.027f, 0.043f, 0.086f, 0.75f), UIKit.Border);
        hintText = UIKit.Label(hintRt, "핀(●)을 끌어 다른 부품의 핀에 놓으면 연결 · 스위치 클릭으로 ON/OFF · 빈 곳 드래그로 회전 · 휠로 확대 · 전선 더블클릭으로 삭제", 14, UIKit.Muted, TextAnchor.MiddleCenter);

        toastRt = UIKit.Rect(c, "Toast", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -74), new Vector2(620, 50));
        UIKit.Box(toastRt, UIKit.Panel, UIKit.Neon);
        toastText = UIKit.Label(toastRt, "", 18, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        Layout();
    }

    void Layout()
    {
        if (canvas == null) return;
        UIKit.FitScaler(canvas);
        bool portrait = Screen.height > Screen.width;
        if (portrait)
        {
            palette.anchorMin = new Vector2(0, 0); palette.anchorMax = new Vector2(1, 0); palette.pivot = new Vector2(0.5f, 0);
            palette.anchoredPosition = new Vector2(0, 10); palette.sizeDelta = new Vector2(-20, 128);
            for (int i = 0; i < paletteButtons.Count; i++)
            {
                var rt = paletteButtons[i];
                rt.anchorMin = rt.anchorMax = new Vector2(i % 4 / 4f, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(8, -10 - (i / 4) * 56);
                rt.sizeDelta = new Vector2(168, 48);
            }
            side.anchorMin = new Vector2(0, 1); side.anchorMax = new Vector2(1, 1); side.pivot = new Vector2(0.5f, 1);
            side.anchoredPosition = new Vector2(0, -68); side.sizeDelta = new Vector2(-20, 380);
            hintRt.gameObject.SetActive(false);
            selBar.anchoredPosition = new Vector2(0, 150);
            if (!portraitInit) { sideOpen = false; portraitInit = true; }
        }
        else
        {
            palette.anchorMin = palette.anchorMax = new Vector2(0, 1); palette.pivot = new Vector2(0, 1);
            palette.anchoredPosition = new Vector2(12, -70); palette.sizeDelta = new Vector2(156, 470);
            for (int i = 0; i < paletteButtons.Count; i++)
            {
                var rt = paletteButtons[i];
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(12, -38 - i * 53);
                rt.sizeDelta = new Vector2(132, 46);
            }
            side.anchorMin = side.anchorMax = new Vector2(1, 1); side.pivot = new Vector2(1, 1);
            side.anchoredPosition = new Vector2(-12, -70); side.sizeDelta = new Vector2(300, 400);
            hintRt.gameObject.SetActive(true);
            selBar.anchoredPosition = new Vector2(0, 62);
        }
        side.gameObject.SetActive(sideOpen);
    }
    bool portraitInit;
    bool fitted;

    void LoadExample()
    {
        circuit = LCircuit.Example();
        SyncAll();
        Toast("반가산기 예제 — 전원을 켜고 스위치 A·B를 눌러 보세요");
    }

    void ClearBoard()
    {
        if (Time.time > clearArmedUntil)
        {
            clearArmedUntil = Time.time + 2.5f;
            Toast("한 번 더 누르면 보드를 비웁니다", true);
            return;
        }
        clearArmedUntil = 0;
        circuit = new LCircuit();
        SyncAll();
        Toast("보드를 비웠어요 — 왼쪽 부품을 눌러 놓아 보세요");
    }

    void Toast(string msg, bool warn = false, float seconds = 2.6f)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastRt.GetComponent<Outline>().effectColor = warn ? UIKit.Amber : UIKit.Neon;
        toastUntil = Time.time + seconds;
    }
}
