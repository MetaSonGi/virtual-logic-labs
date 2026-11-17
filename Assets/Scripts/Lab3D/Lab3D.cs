// 3D 회로 실험실 — 실험별 3D 보드, 전원 공급기, 전선·호스 연결, 공유압 시뮬레이션, 미션 채점
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Lab3D : MonoBehaviour
{
    const float PinY = 0.35f, BW = 24f, BD = 15f;
    static readonly Color PinColor = UIKit.Hex("46557a");
    static readonly Color PortColor = UIKit.Hex("5b7db8");
    static readonly Color WireOff = UIKit.Hex("34405e");
    static readonly Color AirOff = UIKit.Hex("2b3a5c");
    static readonly Color OilOff = UIKit.Hex("4a3418");

    class PinView { public Transform t; public Material mat; public int pin; public bool isOut; public Vector3 local; }
    class PortView { public Transform t; public Material mat; public int port; public Vector3 local; }
    class PartView
    {
        public LPart part; public GameObject go; public Material bodyMat; public Color bodyColor;
        public readonly List<PinView> pins = new List<PinView>();
        public readonly List<PortView> ports = new List<PortView>();
        public Transform pivot; public float lever;
        public Material capMat, stripMat, ledMat, coilMat, onMat, offMat, motorMat; public Renderer halo; public float drop = -1;
        public Transform rod, head, fan; public float tip0, stroke;
    }
    class WireView
    {
        public GameObject go; public Mesh mesh; public MeshCollider col; public Material mat;
        public Vector3[] pts; public float[] cum; public Transform[] pulses; public int val;
    }

    Experiment exp;
    Lab3DArt art;
    Camera cam;
    LCircuit circuit = new LCircuit();
    LabState state = new LabState();
    bool power;
    SimResult sim;
    FluidResult fluid;
    bool dirty = true;
    float panelTimer;
    float saveAt = -1;
    bool passed;

    readonly Dictionary<int, PartView> pv = new Dictionary<int, PartView>();
    readonly Dictionary<int, WireView> wv = new Dictionary<int, WireView>();
    readonly Dictionary<int, WireView> hv = new Dictionary<int, WireView>();

    // power supply
    Material psButtonMat, psDisplayMat;
    TextMesh psText;
    Renderer psHalo;
    readonly List<Material> railMats = new List<Material>();
    readonly List<Material> cableMats = new List<Material>();

    // temp wire / hose
    GameObject tempGo; Mesh tempMesh; Material tempMat;

    // camera
    Vector3 target, tTarget, homeTarget;
    float yaw, pitch, dist, tYaw, tPitch, tDist, homeYaw, homePitch, homeDist;

    // interaction
    enum Drag { None, Camera, Pan, Wire, Hose, Part, Nothing }
    Drag drag;
    Vector2 downPos, lastMouse;
    bool moved;
    PinRef dragPin;
    PortRef dragPort;
    PartView dragPart;
    Vector2 dragOff;
    int pendingKind; // 0 none, 1 pin, 2 port
    PinRef pendingPin;
    PortRef pendingPort;
    Transform hoverT; float hoverBase;
    int selKind, selId; // 1 part, 2 wire, 5 hose
    float lastClickTime; int lastClickId = -1;
    float clearArmedUntil;

    // UI
    Canvas canvas;
    RectTransform topBar, palette, side, selBar, toastRt, hintRt, goalBox;
    readonly List<RectTransform> paletteButtons = new List<RectTransform>();
    Text ttText, ttState, ttHead, selText, toastText, groupText, titleText, goalText, descText, allowedText;
    Button delBtn, checkBtn, nextBtn;
    CanvasGroup toastGroup;
    float toastUntil;
    bool sideOpen = true;
    bool portraitInit, fitted;
    int lastW, lastH;

    string SaveKey => "lab3d.exp." + exp.id;
    bool IsMission => exp.check != ExpCheck.None;

    // ---------------------------------------------------------------- setup
    void Start()
    {
        exp = Experiments.Cur;
        art = Lab3DArt.Load();
        SetupCameraAndLight();
        BuildBoard();
        BuildPowerSupply();
        BuildTempWire();
        BuildUI();

        LCircuit loaded = null;
        var saved = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(saved)) { try { loaded = LCircuit.FromJson(saved); } catch { loaded = null; } }
        if (loaded != null && IsMission && !Experiments.HasFixedParts(loaded, exp)) loaded = null;
        circuit = loaded ?? exp.setup();
        passed = Experiments.Done().Contains(exp.id);
        SyncAll();
        ResetCamera(true);
        Toast(exp.fluid ? "POWER를 켜면 압축기·펌프도 돌아가요 — 호스는 포트(●)끼리 끌어서 이어요" : "뒤쪽 전원 공급기의 POWER 버튼을 눌러 전기를 넣어 보세요", false, 5f);
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
                        if (Vector2.Distance(new Vector2(xx + 0.5f, yy + 0.5f), new Vector2(cx, cz)) < 1.8f) px[yy * tw + xx] = hole;
                    }
            }
        tex.SetPixels(px);
        tex.Apply();
        var topMat = art.Body(Color.white, 0.05f, 0.2f);
        topMat.mainTexture = tex;
        var top = art.Prim(PrimitiveType.Quad, null, new Vector3(0, 0.002f, 0), new Vector3(BW, BD, 1), topMat);
        top.transform.rotation = Quaternion.Euler(90, 0, 0);
        Lab3DArt.NoCollider(top);

        var edgeMat = art.Emissive(UIKit.Accent, 0.9f);
        foreach (var z in new[] { -BD / 2, BD / 2 })
            Lab3DArt.NoCollider(art.Cube(null, new Vector3(0, 0, z), new Vector3(BW + 0.06f, 0.06f, 0.06f), edgeMat));
        foreach (var x in new[] { -BW / 2, BW / 2 })
            Lab3DArt.NoCollider(art.Cube(null, new Vector3(x, 0, 0), new Vector3(0.06f, 0.06f, BD + 0.06f), edgeMat));

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
        Lab3DArt.NoCollider(art.Cube(root, new Vector3(0, 0.95f, 0), new Vector3(6, 1.9f, 2.4f), art.Body(UIKit.Hex("1a2340"), 0.35f, 0.6f)));
        Lab3DArt.NoCollider(art.Cube(root, new Vector3(0, 0.95f, -1.205f), new Vector3(5.6f, 1.5f, 0.02f), art.Body(UIKit.Hex("0b1020"), 0.2f, 0.7f)));

        psDisplayMat = art.Emissive(UIKit.Amber, 0.1f);
        Lab3DArt.NoCollider(art.Cube(root, new Vector3(-1.3f, 1.15f, -1.22f), new Vector3(2.4f, 0.8f, 0.03f), psDisplayMat));
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

        GameObject Body(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var b = art.Prim(type, root, pos, scale, mat);
            var pk = b.AddComponent<Pickable>();
            pk.kind = 1; pk.id = p.id;
            return b;
        }
        Material Dark(string hex, float metal = 0.3f, float smooth = 0.5f)
        {
            v.bodyColor = UIKit.Hex(hex);
            v.bodyMat = art.Body(v.bodyColor, metal, smooth);
            return v.bodyMat;
        }

        switch (p.type)
        {
            case PartType.Switch:
                {
                    Body(PrimitiveType.Cube, new Vector3(0, 0.2f, 0), new Vector3(1.8f, 0.4f, 1.4f), Dark("161e33"));
                    v.pivot = new GameObject("Pivot").transform;
                    v.pivot.SetParent(root, false);
                    v.pivot.localPosition = new Vector3(0, 0.4f, 0);
                    var lever = art.Prim(PrimitiveType.Cylinder, v.pivot, new Vector3(0, 0.4f, 0), new Vector3(0.2f, 0.4f, 0.2f), art.Body(UIKit.Hex("b8c2d8"), 0.9f, 0.7f));
                    var lp = lever.AddComponent<Pickable>(); lp.kind = 1; lp.id = p.id;
                    v.capMat = art.Emissive(UIKit.Amber, 0f);
                    var cap = art.Sphere(v.pivot, new Vector3(0, 0.8f, 0), 0.42f, v.capMat);
                    var cp = cap.AddComponent<Pickable>(); cp.kind = 1; cp.id = p.id;
                    v.stripMat = art.Emissive(UIKit.Amber, 0f);
                    Lab3DArt.NoCollider(art.Cube(root, new Vector3(0, 0.25f, -0.71f), new Vector3(0.9f, 0.06f, 0.06f), v.stripMat));
                    AddPin(v, 0, true, new Vector3(1.3f, PinY, 0), new Vector3(0.9f, PinY, 0));
                    Label(root, p.label, p.locked ? UIKit.Violet : UIKit.Amber, 1.75f);
                    break;
                }
            case PartType.Led:
                {
                    Body(PrimitiveType.Cube, new Vector3(0, 0.175f, 0), new Vector3(1.2f, 0.35f, 1.2f), Dark("161e33"));
                    v.ledMat = art.Emissive(UIKit.Neon, 0.05f);
                    Body(PrimitiveType.Sphere, new Vector3(0, 0.38f, 0), new Vector3(0.84f, 0.64f, 0.84f), v.ledMat);
                    v.halo = art.Halo(root, UIKit.Neon, 2.6f);
                    v.halo.transform.localPosition = new Vector3(0, 0.55f, 0);
                    AddPin(v, 0, false, new Vector3(-1.0f, PinY, 0), new Vector3(-0.6f, PinY, 0));
                    Label(root, p.label, p.locked ? UIKit.Violet : UIKit.Neon, 1.6f);
                    break;
                }
            case PartType.Compressor:
                {
                    Body(PrimitiveType.Cube, new Vector3(0, 0.25f, 0), new Vector3(2.6f, 0.5f, 1.6f), Dark("22304f"));
                    var tank = Body(PrimitiveType.Cylinder, new Vector3(0.2f, 0.85f, 0), new Vector3(0.9f, 0.9f, 0.9f), art.Body(UIKit.Hex("2b4fa0"), 0.55f, 0.7f));
                    tank.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    Body(PrimitiveType.Cube, new Vector3(-0.95f, 0.8f, 0), new Vector3(0.7f, 0.6f, 0.8f), art.Body(UIKit.Hex("0d111c"), 0.3f, 0.5f));
                    v.fan = new GameObject("Fan").transform;
                    v.fan.SetParent(root, false);
                    v.fan.localPosition = new Vector3(-1.34f, 0.8f, 0);
                    var bladeMat = art.Body(UIKit.Hex("b8c2d8"), 0.8f, 0.6f);
                    Lab3DArt.NoCollider(art.Cube(v.fan, Vector3.zero, new Vector3(0.05f, 0.55f, 0.1f), bladeMat));
                    Lab3DArt.NoCollider(art.Cube(v.fan, Vector3.zero, new Vector3(0.05f, 0.1f, 0.55f), bladeMat));
                    v.motorMat = art.Emissive(UIKit.Accent, 0);
                    Lab3DArt.NoCollider(art.Sphere(root, new Vector3(0.9f, 0.62f, -0.55f), 0.22f, v.motorMat));
                    AddPort(v, 0, "P", new Vector3(1.6f, PinY, 0), new Vector3(1.3f, PinY, 0), new Vector3(1.6f, 0.02f, -0.45f));
                    Label(root, "압축기", p.locked ? UIKit.Violet : UIKit.Text, 1.75f);
                    break;
                }
            case PartType.Pump:
                {
                    Body(PrimitiveType.Cube, new Vector3(0, 0.25f, 0), new Vector3(2.8f, 0.5f, 1.8f), Dark("3a2a14"));
                    Body(PrimitiveType.Cube, new Vector3(-0.45f, 0.95f, 0), new Vector3(1.6f, 0.9f, 1.4f), art.Body(UIKit.Hex("6a4518"), 0.5f, 0.6f));
                    Body(PrimitiveType.Cylinder, new Vector3(0.85f, 0.85f, 0), new Vector3(0.6f, 0.35f, 0.6f), art.Body(UIKit.Hex("0d111c"), 0.4f, 0.5f));
                    v.motorMat = art.Emissive(UIKit.Amber, 0);
                    v.fan = new GameObject("Rotor").transform;
                    v.fan.SetParent(root, false);
                    v.fan.localPosition = new Vector3(0.85f, 1.22f, 0);
                    Lab3DArt.NoCollider(art.Cube(v.fan, Vector3.zero, new Vector3(0.5f, 0.04f, 0.1f), v.motorMat));
                    AddPort(v, 0, "P", new Vector3(1.7f, PinY, -0.45f), new Vector3(1.4f, PinY, -0.45f), new Vector3(2.15f, 0.02f, -0.45f));
                    AddPort(v, 1, "T", new Vector3(1.7f, PinY, 0.45f), new Vector3(1.4f, PinY, 0.45f), new Vector3(2.15f, 0.02f, 0.45f));
                    Label(root, "유압 펌프", p.locked ? UIKit.Violet : UIKit.Amber, 1.9f);
                    break;
                }
            case PartType.Valve52:
            case PartType.Valve32:
                {
                    bool five = p.type == PartType.Valve52;
                    float w = five ? 2.6f : 2.0f;
                    Body(PrimitiveType.Cube, new Vector3(0, 0.42f, 0), new Vector3(w, 0.55f, 1.3f), Dark("1d2a48"));
                    v.coilMat = art.Emissive(UIKit.Violet, 0);
                    Lab3DArt.SetGlow(v.coilMat, UIKit.Hex("14182a"), UIKit.Violet, 0);
                    Body(PrimitiveType.Cube, new Vector3(-w / 2 - 0.35f, 0.45f, 0), new Vector3(0.7f, 0.65f, 0.9f), v.coilMat);
                    float half = w / 2 - 0.1f;
                    v.onMat = art.Emissive(UIKit.Neon, 0);
                    v.offMat = art.Emissive(UIKit.Accent, 0);
                    Lab3DArt.NoCollider(art.Cube(root, new Vector3(-half / 2, 0.705f, 0.1f), new Vector3(half - 0.08f, 0.02f, 0.7f), v.onMat));
                    Lab3DArt.NoCollider(art.Cube(root, new Vector3(half / 2, 0.705f, 0.1f), new Vector3(half - 0.08f, 0.02f, 0.7f), v.offMat));
                    art.FlatText(root, "ON", new Vector3(-half / 2, 0.72f, 0.1f), 0.04f, UIKit.Text);
                    art.FlatText(root, "OFF", new Vector3(half / 2, 0.72f, 0.1f), 0.04f, UIKit.Text);
                    art.FlatText(root, five ? "5/2" : "3/2", new Vector3(0, 0.72f, -0.45f), 0.045f, UIKit.Muted);
                    AddPin(v, 0, false, new Vector3(-w / 2 - 1.05f, PinY, 0), new Vector3(-w / 2 - 0.7f, PinY, 0));
                    if (five)
                    {
                        AddPort(v, 0, "P", new Vector3(-0.5f, PinY, -1.0f), new Vector3(-0.5f, PinY, -0.65f), new Vector3(-0.5f, 0.02f, -1.5f));
                        AddPort(v, 1, "A", new Vector3(-0.5f, PinY, 1.0f), new Vector3(-0.5f, PinY, 0.65f), new Vector3(-0.5f, 0.02f, 1.5f));
                        AddPort(v, 2, "B", new Vector3(0.5f, PinY, 1.0f), new Vector3(0.5f, PinY, 0.65f), new Vector3(0.5f, 0.02f, 1.5f));
                        AddPort(v, 3, "R", new Vector3(0.5f, PinY, -1.0f), new Vector3(0.5f, PinY, -0.65f), new Vector3(0.5f, 0.02f, -1.5f));
                    }
                    else
                    {
                        AddPort(v, 0, "P", new Vector3(-0.4f, PinY, -1.0f), new Vector3(-0.4f, PinY, -0.65f), new Vector3(-0.4f, 0.02f, -1.5f));
                        AddPort(v, 1, "A", new Vector3(0, PinY, 1.0f), new Vector3(0, PinY, 0.65f), new Vector3(0, 0.02f, 1.5f));
                        AddPort(v, 2, "R", new Vector3(0.4f, PinY, -1.0f), new Vector3(0.4f, PinY, -0.65f), new Vector3(0.4f, 0.02f, -1.5f));
                    }
                    break;
                }
            case PartType.CylDouble:
            case PartType.CylSingle:
                {
                    bool dbl = p.type == PartType.CylDouble;
                    float len = dbl ? 3.2f : 2.4f, left = dbl ? -2.4f : -1.8f, right = left + len;
                    v.bodyColor = UIKit.Hex("8a94ad");
                    v.bodyMat = art.Body(v.bodyColor, 0.8f, 0.7f);
                    var barrel = Body(PrimitiveType.Cylinder, new Vector3((left + right) / 2, 0.5f, 0), new Vector3(0.75f, len / 2, 0.75f), v.bodyMat);
                    barrel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    var capMat = art.Body(UIKit.Hex("1d2a48"), 0.4f, 0.5f);
                    Body(PrimitiveType.Cube, new Vector3(left, 0.5f, 0), new Vector3(0.3f, 0.85f, 0.85f), capMat);
                    Body(PrimitiveType.Cube, new Vector3(right, 0.5f, 0), new Vector3(0.3f, 0.85f, 0.85f), capMat);
                    Lab3DArt.NoCollider(art.Cube(root, new Vector3((left + right) / 2, 0.08f, 0), new Vector3(len, 0.16f, 0.5f), capMat));
                    v.stroke = dbl ? 1.6f : 1.2f;
                    v.tip0 = right + 0.6f;
                    var rodMat = art.Body(UIKit.Hex("dfe6f5"), 0.95f, 0.85f);
                    var rod = art.Prim(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.18f, 1.2f, 0.18f), rodMat);
                    rod.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    Lab3DArt.NoCollider(rod);
                    v.rod = rod.transform;
                    var head = art.Cube(root, Vector3.zero, new Vector3(0.35f, 0.5f, 0.5f), capMat);
                    Lab3DArt.NoCollider(head);
                    v.head = head.transform;
                    float ax = left + 0.4f, bx = right - 0.4f;
                    // hose ports on top of the barrel so the default camera can see and click them
                    AddPort(v, 0, "A", new Vector3(ax, 1.15f, 0), new Vector3(ax, 0.85f, 0), new Vector3(ax - 0.5f, 1.25f, 0), true);
                    if (dbl) AddPort(v, 1, "B", new Vector3(bx, 1.15f, 0), new Vector3(bx, 0.85f, 0), new Vector3(bx + 0.5f, 1.25f, 0), true);
                    AddPin(v, 0, true, new Vector3(ax, PinY, -0.85f), new Vector3(ax, PinY, -0.4f));
                    AddPin(v, 1, true, new Vector3(bx, PinY, -0.85f), new Vector3(bx, PinY, -0.4f));
                    art.FlatText(root, "S0", new Vector3(ax, 0.02f, -1.35f), 0.05f, UIKit.Muted);
                    art.FlatText(root, "S1", new Vector3(bx, 0.02f, -1.35f), 0.05f, UIKit.Muted);
                    Label(root, p.label, p.locked ? UIKit.Violet : UIKit.Text, 1.6f);
                    PlaceRod(v, 0);
                    break;
                }
            default:
                {
                    // logic chips (gates and the SR latch)
                    Body(PrimitiveType.Cube, new Vector3(0, 0.425f, 0), new Vector3(2.2f, 0.55f, 1.6f), Dark("0d111c", 0.25f, 0.6f));
                    string name = p.type == PartType.Latch ? "SR" : LCircuit.Name(p.type);
                    art.FlatText(root, name, new Vector3(0, 0.706f, 0.18f), name.Length > 3 ? 0.085f : 0.1f, UIKit.Hex("eaf0ff"));
                    art.FlatText(root, LCircuit.Expr(p.type), new Vector3(0, 0.706f, -0.45f), p.type == PartType.Latch ? 0.035f : 0.05f, UIKit.Neon);
                    Lab3DArt.NoCollider(art.Sphere(root, new Vector3(-0.88f, 0.7f, 0.58f), 0.12f, art.Body(UIKit.Muted)));
                    int n = LCircuit.Inputs(p.type);
                    var zs = n == 1 ? new[] { 0f } : new[] { -0.45f, 0.45f };
                    for (int i = 0; i < n; i++) AddPin(v, i, false, new Vector3(-1.45f, PinY, zs[i]), new Vector3(-1.1f, PinY, zs[i]));
                    AddPin(v, 0, true, new Vector3(1.45f, PinY, 0), new Vector3(1.1f, PinY, 0));
                    if (p.type == PartType.Latch)
                    {
                        art.FlatText(root, "S", new Vector3(-1.45f, 0.02f, -0.85f), 0.045f, UIKit.Muted);
                        art.FlatText(root, "R", new Vector3(-1.45f, 0.02f, 0.85f), 0.045f, UIKit.Muted);
                        art.FlatText(root, "Q", new Vector3(1.45f, 0.02f, -0.45f), 0.045f, UIKit.Muted);
                    }
                    break;
                }
        }
        return v;
    }

    void PlaceRod(PartView v, float pos)
    {
        float tip = v.tip0 + pos * v.stroke;
        v.rod.localPosition = new Vector3(tip - 1.2f, 0.5f, 0);
        v.head.localPosition = new Vector3(tip + 0.15f, 0.5f, 0);
    }

    void Label(Transform root, string text, Color color, float y)
    {
        var t = art.Text(root, text, new Vector3(0, y, 0), 0.13f, color);
        t.fontStyle = FontStyle.Bold;
        t.gameObject.AddComponent<Billboard>();
    }

    GameObject Leg(PartView v, Vector3 pos, Vector3 edge)
    {
        var mid = (pos + edge) / 2;
        float dx = Mathf.Abs(pos.x - edge.x), dz = Mathf.Abs(pos.z - edge.z), dy = Mathf.Abs(pos.y - edge.y);
        var scale = dx < 0.01f && dz < 0.01f ? new Vector3(0.08f, dy, 0.08f)
                  : dx >= dz ? new Vector3(dx, 0.08f, 0.08f) : new Vector3(0.08f, 0.08f, dz);
        var leg = art.Cube(v.go.transform, mid, scale, art.Body(UIKit.Hex("b8c2d8"), 0.9f, 0.7f));
        Lab3DArt.NoCollider(leg);
        return leg;
    }

    void AddPin(PartView v, int pin, bool isOut, Vector3 pos, Vector3 edge)
    {
        var mat = art.Emissive(PinColor, 0);
        Lab3DArt.SetGlow(mat, PinColor, Color.black, 0);
        var s = art.Sphere(v.go.transform, pos, 0.4f, mat);
        ((SphereCollider)s.GetComponent<Collider>()).radius = 0.85f;
        var pk = s.AddComponent<Pickable>();
        pk.kind = 0; pk.id = v.part.id; pk.pin = pin; pk.isOut = isOut;
        Leg(v, pos, edge);
        v.pins.Add(new PinView { t = s.transform, mat = mat, pin = pin, isOut = isOut, local = pos });
    }

    void AddPort(PartView v, int port, string name, Vector3 pos, Vector3 edge, Vector3 labelPos, bool billboard = false)
    {
        var mat = art.Emissive(PortColor, 0);
        Lab3DArt.SetGlow(mat, PortColor, Color.black, 0);
        var s = art.Sphere(v.go.transform, pos, 0.46f, mat);
        ((SphereCollider)s.GetComponent<Collider>()).radius = 0.85f;
        var pk = s.AddComponent<Pickable>();
        pk.kind = 4; pk.id = v.part.id; pk.pin = port;
        var ring = art.Prim(PrimitiveType.Cylinder, v.go.transform, pos - Vector3.up * 0.2f, new Vector3(0.62f, 0.03f, 0.62f), art.Body(UIKit.Hex("c8d2ee"), 0.9f, 0.8f));
        Lab3DArt.NoCollider(ring);
        var leg = Leg(v, pos, edge);
        var ls = leg.transform.localScale;
        leg.transform.localScale = new Vector3(ls.x <= 0.08f ? 0.16f : ls.x, ls.y <= 0.08f ? 0.16f : ls.y, ls.z <= 0.08f ? 0.16f : ls.z);
        if (billboard)
        {
            var t = art.Text(v.go.transform, name, labelPos, 0.07f, UIKit.Hex("8fb3ff"));
            t.fontStyle = FontStyle.Bold;
            t.gameObject.AddComponent<Billboard>();
        }
        else art.FlatText(v.go.transform, name, labelPos, 0.06f, UIKit.Hex("8fb3ff"));
        v.ports.Add(new PortView { t = s.transform, mat = mat, port = port, local = pos });
    }

    Vector3 PinWorld(PinRef r)
    {
        if (!pv.TryGetValue(r.part, out var v)) return Vector3.zero;
        var p = v.pins.FirstOrDefault(x => x.pin == r.pin && x.isOut == r.isOut);
        return p == null ? v.go.transform.position : v.go.transform.position + p.local;
    }

    Vector3 PortWorld(PortRef r)
    {
        if (!pv.TryGetValue(r.part, out var v)) return Vector3.zero;
        var p = v.ports.FirstOrDefault(x => x.port == r.port);
        return p == null ? v.go.transform.position : v.go.transform.position + p.local;
    }

    // ---------------------------------------------------------------- wire and hose views
    WireView NewLine(string name, int kind, int id, Color off, int pulses, float pulseSize, Color pulseColor)
    {
        var v = new WireView { go = new GameObject(name + id, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)) };
        v.mesh = new Mesh();
        v.go.GetComponent<MeshFilter>().sharedMesh = v.mesh;
        v.mat = art.Emissive(off, 0);
        Lab3DArt.SetGlow(v.mat, off, Color.black, 0);
        v.go.GetComponent<MeshRenderer>().sharedMaterial = v.mat;
        v.col = v.go.GetComponent<MeshCollider>();
        var pk = v.go.AddComponent<Pickable>(); pk.kind = kind; pk.id = id;
        v.pulses = new Transform[pulses];
        for (int i = 0; i < pulses; i++)
        {
            var s = art.Sphere(null, Vector3.zero, pulseSize, art.Unlit(pulseColor));
            Lab3DArt.NoCollider(s);
            art.Halo(s.transform, pulseColor, pulseSize * 5f);
            s.SetActive(false);
            v.pulses[i] = s.transform;
        }
        return v;
    }

    void SetLine(WireView v, Vector3[] pts, float radius)
    {
        v.pts = pts;
        v.cum = Lab3DArt.Cumulative(pts);
        Lab3DArt.Tube(v.mesh, pts, radius);
        v.col.sharedMesh = null;
        v.col.sharedMesh = v.mesh;
    }

    void BuildWire(LWire w)
    {
        if (!wv.TryGetValue(w.id, out var v)) wv[w.id] = v = NewLine("Wire", 2, w.id, WireOff, 3, 0.26f, new Color(0.55f, 1f, 0.85f));
        var a = PinWorld(new PinRef(w.fromPart, w.fromPin, true));
        var b = PinWorld(new PinRef(w.toPart, w.toPin, false));
        SetLine(v, Lab3DArt.Bezier(a, b, 40), 0.08f);
    }

    void BuildHose(LHose h)
    {
        bool oil = circuit.IsOil;
        if (!hv.TryGetValue(h.id, out var v)) hv[h.id] = v = NewLine("Hose", 5, h.id, oil ? OilOff : AirOff, 2, 0.3f, oil ? UIKit.Amber : new Color(0.55f, 0.75f, 1f));
        SetLine(v, Lab3DArt.Arc(PortWorld(new PortRef(h.aPart, h.aPort)), PortWorld(new PortRef(h.bPart, h.bPort)), 36), 0.12f);
    }

    void RemoveLine(Dictionary<int, WireView> map, int id)
    {
        if (!map.TryGetValue(id, out var v)) return;
        Destroy(v.go);
        foreach (var p in v.pulses) Destroy(p.gameObject);
        map.Remove(id);
    }

    void UpdateTempPin(PinRef from, Vector3 to, bool snapped)
    {
        var a = PinWorld(from);
        var pts = from.isOut ? Lab3DArt.Bezier(a, to, 30) : Lab3DArt.Bezier(to, a, 30);
        ShowTemp(pts, 0.065f, snapped ? UIKit.Neon : UIKit.Violet);
    }

    void UpdateTempPort(PortRef from, Vector3 to, bool snapped)
    {
        ShowTemp(Lab3DArt.Arc(PortWorld(from), to, 30), 0.1f, snapped ? UIKit.Neon : UIKit.Accent);
    }

    void ShowTemp(Vector3[] pts, float r, Color c)
    {
        Lab3DArt.Tube(tempMesh, pts, r, 6);
        Lab3DArt.SetGlow(tempMat, c, c, 1.2f);
        tempGo.SetActive(true);
    }

    void HideTemp() => tempGo.SetActive(false);

    // ---------------------------------------------------------------- sync
    void SyncAll()
    {
        foreach (var v in pv.Values) Destroy(v.go);
        pv.Clear();
        foreach (var id in wv.Keys.ToList()) RemoveLine(wv, id);
        foreach (var id in hv.Keys.ToList()) RemoveLine(hv, id);
        state = new LabState();
        foreach (var p in circuit.parts) pv[p.id] = MakePartView(p);
        foreach (var w in circuit.wires) BuildWire(w);
        foreach (var h in circuit.hoses) BuildHose(h);
        Select(0, 0);
        Changed();
    }

    void SyncDiff()
    {
        var ids = new HashSet<int>(circuit.parts.Select(p => p.id));
        foreach (var id in pv.Keys.Where(k => !ids.Contains(k)).ToList()) { Destroy(pv[id].go); pv.Remove(id); }
        foreach (var p in circuit.parts) if (!pv.ContainsKey(p.id)) pv[p.id] = MakePartView(p);
        var wids = new HashSet<int>(circuit.wires.Select(w => w.id));
        foreach (var id in wv.Keys.Where(k => !wids.Contains(k)).ToList()) RemoveLine(wv, id);
        foreach (var w in circuit.wires) if (!wv.ContainsKey(w.id)) BuildWire(w);
        var hids = new HashSet<int>(circuit.hoses.Select(h => h.id));
        foreach (var id in hv.Keys.Where(k => !hids.Contains(k)).ToList()) RemoveLine(hv, id);
        foreach (var h in circuit.hoses) if (!hv.ContainsKey(h.id)) BuildHose(h);
        Changed();
    }

    void RebuildLinesFor(int partId)
    {
        foreach (var w in circuit.wires) if (w.fromPart == partId || w.toPart == partId) BuildWire(w);
        foreach (var h in circuit.hoses) if (h.aPart == partId || h.bPart == partId) BuildHose(h);
    }

    void Changed()
    {
        dirty = true;
        saveAt = Time.time + 0.4f;
    }

    // ---------------------------------------------------------------- placement
    static Vector2 Footprint(PartType t)
    {
        switch (t)
        {
            case PartType.Switch: return new Vector2(3.2f, 2f);
            case PartType.Led: return new Vector2(2.8f, 1.8f);
            case PartType.Compressor: return new Vector2(3.6f, 2.2f);
            case PartType.Pump: return new Vector2(4.4f, 2.4f);
            case PartType.Valve52: return new Vector2(5.2f, 3.4f);
            case PartType.Valve32: return new Vector2(4.6f, 3.4f);
            case PartType.CylDouble: return new Vector2(6.6f, 3.2f);
            case PartType.CylSingle: return new Vector2(5.4f, 3.2f);
            default: return new Vector2(3.8f, 2.2f);
        }
    }

    static bool InBounds(PartType t, int x, int z)
    {
        var f = Footprint(t);
        return Mathf.Abs(x) <= BW / 2 - f.x / 2 + 0.3f && Mathf.Abs(z) <= BD / 2 - f.y / 2 - 0.2f;
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
        Toast("보드에 빈 자리가 없어요 — 부품을 옮기거나 지워 보세요", true);
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
            float bonus = p.kind == 0 || p.kind == 4 ? 0.6f : p.kind == 2 || p.kind == 5 ? 0f : 0.25f;
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
    static PortRef ToPort(Pickable p) => new PortRef(p.id, p.pin);
    static bool CanLink(PinRef a, Pickable h) => h != null && h.kind == 0 && h.isOut != a.isOut && h.id != a.part;
    static bool CanHose(PortRef a, Pickable h) => h != null && h.kind == 4 && h.id != a.part;

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
            if (drag == Drag.Wire || drag == Drag.Hose) HideTemp();
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
        if (pendingKind != 0)
        {
            int kind = pendingKind;
            var fromPin = pendingPin; var fromPort = pendingPort;
            CancelPending();
            if (kind == 1 && hit != null && hit.kind == 0)
            {
                var to = ToPin(hit);
                if (!to.Same(fromPin)) TryConnect(fromPin, to);
                drag = Drag.Nothing;
                return;
            }
            if (kind == 2 && hit != null && hit.kind == 4)
            {
                var to = ToPort(hit);
                if (!to.Same(fromPort)) TryHose(fromPort, to);
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
            case 4:
                drag = Drag.Hose;
                dragPort = ToPort(hit);
                break;
            case 1:
                dragPart = pv[hit.id];
                dragOff = Ground(mp, out var pt) ? new Vector2(pt.x - dragPart.part.x, pt.z - dragPart.part.z) : Vector2.zero;
                drag = Drag.Part;
                if (dragPart.part.type != PartType.Switch) Select(1, hit.id);
                break;
            case 2:
            case 5:
                drag = Drag.Nothing;
                int key = hit.kind * 100000 + hit.id;
                if (Time.time - lastClickTime < 0.35f && lastClickId == key)
                {
                    if (hit.kind == 2) circuit.RemoveWire(hit.id); else circuit.RemoveHose(hit.id);
                    Select(0, 0);
                    SyncDiff();
                    lastClickId = -1;
                    return;
                }
                lastClickTime = Time.time;
                lastClickId = key;
                Select(hit.kind, hit.id);
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
                    UpdateTempPin(dragPin, to, snap);
                    SetHover(snap ? hit : null);
                }
                break;
            case Drag.Hose:
                if (!moved) break;
                {
                    var hit = Pick(mp);
                    bool snap = CanHose(dragPort, hit);
                    Vector3 to;
                    if (snap) to = PortWorld(ToPort(hit));
                    else if (!Ground(mp, out to)) break;
                    UpdateTempPort(dragPort, to, snap);
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
                        RebuildLinesFor(p.id);
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
                pendingKind = 1;
                pendingPin = dragPin;
                if (Ground(mp, out var pt)) UpdateTempPin(pendingPin, pt, false);
                Toast(dragPin.isOut ? "연결할 입력 핀을 누르세요" : "연결할 출력 핀을 누르세요");
            }
            else HideTemp();
            SetHover(null);
        }
        else if (d == Drag.Hose)
        {
            var hit = Pick(mp);
            if (hit != null && hit.kind == 4 && !ToPort(hit).Same(dragPort)) { HideTemp(); TryHose(dragPort, ToPort(hit)); }
            else if (!moved)
            {
                pendingKind = 2;
                pendingPort = dragPort;
                if (Ground(mp, out var pt)) UpdateTempPort(pendingPort, pt, false);
                Toast("호스를 이을 다른 부품의 포트를 누르세요");
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
        if (pendingKind == 1)
        {
            bool snap = CanLink(pendingPin, hit);
            Vector3 to;
            if (snap) to = PinWorld(ToPin(hit));
            else if (!Ground(mp, out to)) return;
            UpdateTempPin(pendingPin, to, snap);
            SetHover(snap ? hit : null);
        }
        else if (pendingKind == 2)
        {
            bool snap = CanHose(pendingPort, hit);
            Vector3 to;
            if (snap) to = PortWorld(ToPort(hit));
            else if (!Ground(mp, out to)) return;
            UpdateTempPort(pendingPort, to, snap);
            SetHover(snap ? hit : null);
        }
        else SetHover(hit != null && (hit.kind == 0 || hit.kind == 4) ? hit : null);
    }

    void SetHover(Pickable hit)
    {
        var t = hit != null && (hit.kind == 0 || hit.kind == 4) ? hit.transform : null;
        if (t == hoverT) return;
        if (hoverT != null) hoverT.localScale = Vector3.one * hoverBase;
        hoverT = t;
        if (t != null) { hoverBase = t.localScale.x; t.localScale = Vector3.one * hoverBase * 1.45f; }
    }

    void CancelPending()
    {
        pendingKind = 0;
        HideTemp();
    }

    void TryConnect(PinRef a, PinRef b)
    {
        if (a.part == b.part) { Toast("같은 부품끼리는 연결할 수 없어요", true); return; }
        if (a.isOut == b.isOut) { Toast(a.isOut ? "출력 핀은 다른 부품의 입력 핀에 연결하세요" : "입력 핀은 다른 부품의 출력 핀에 연결하세요", true); return; }
        circuit.Connect(a, b);
        SyncDiff();
    }

    void TryHose(PortRef a, PortRef b)
    {
        if (a.part == b.part) { Toast("같은 부품의 포트끼리는 이을 수 없어요", true); return; }
        if (circuit.ConnectHose(a, b) == null) { Toast("이미 이어진 호스예요", true); return; }
        SyncDiff();
        RebuildAllHoses();
    }

    void RebuildAllHoses()
    {
        // hose colors follow the medium (air / oil), which changes when a pump is added or removed
        foreach (var id in hv.Keys.ToList()) RemoveLine(hv, id);
        foreach (var h in circuit.hoses) BuildHose(h);
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
        Toast(power ? (exp.fluid ? "전원 ON — 압축기·펌프가 돌고 신호가 흐릅니다" : "전원 ON — 켜진 전선을 따라 신호가 흐릅니다") : "전원 OFF — 모든 신호와 압력이 꺼졌어요", !power);
    }

    // ---------------------------------------------------------------- selection
    void Select(int kind, int id)
    {
        if (selKind == 1 && pv.TryGetValue(selId, out var old) && old.bodyMat != null) Lab3DArt.SetGlow(old.bodyMat, old.bodyColor, Color.black, 0);
        selKind = kind; selId = id;
        if (kind == 1 && pv.TryGetValue(id, out var v) && v.bodyMat != null) Lab3DArt.SetGlow(v.bodyMat, v.bodyColor, UIKit.Violet, 0.35f);
        dirty = true;
        if (selBar == null) return;
        selBar.gameObject.SetActive(kind != 0);
        delBtn.interactable = true;
        if (kind == 2) selText.text = "전선";
        else if (kind == 5) selText.text = "호스";
        else if (kind == 1)
        {
            var p = circuit.Get(id);
            if (p == null) return;
            string name = LCircuit.IsGate(p.type) ? $"{LCircuit.Name(p.type)} 게이트" : string.IsNullOrEmpty(p.label) ? LCircuit.Name(p.type) : $"{LCircuit.Name(p.type)} {p.label}";
            selText.text = p.locked ? name + " · 실험 고정" : name;
            delBtn.interactable = !p.locked;
        }
    }

    void DeleteSelected()
    {
        if (selKind == 2) circuit.RemoveWire(selId);
        else if (selKind == 5) circuit.RemoveHose(selId);
        else if (selKind == 1)
        {
            var p = circuit.Get(selId);
            if (p == null) return;
            if (p.locked) { Toast("실험에 고정된 부품은 지울 수 없어요", true); return; }
            bool wasPump = p.type == PartType.Pump;
            circuit.RemovePart(selId);
            Select(0, 0);
            SyncDiff();
            if (wasPump) RebuildAllHoses();
            return;
        }
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
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float leftPx = 180 * scale, rightPx = (sideOpen ? 344 : 12) * scale;
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
    void StepAndApply(float dt)
    {
        sim = circuit.Step(state, power, dt, null, out fluid);
        bool oil = fluid.oil;
        bool anyMoving = false;

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
            foreach (var po in v.ports)
            {
                long key = LCircuit.Key(id, po.port);
                bool pr = fluid.pressured.Contains(key) && !fluid.exhaust.Contains(key);
                var c = oil ? UIKit.Amber : UIKit.Accent;
                if (pr) Lab3DArt.SetGlow(po.mat, c, c, 1.8f);
                else Lab3DArt.SetGlow(po.mat, PortColor, Color.black, 0);
            }
            switch (v.part.type)
            {
                case PartType.Led:
                    {
                        bool lit = ins != null && ins.Length > 0 && ins[0] == 1;
                        Lab3DArt.SetGlow(v.ledMat, lit ? UIKit.Hex("40ffc8") : UIKit.Hex("0c3326"), UIKit.Neon, lit ? 3f : 0.04f);
                        Lab3DArt.SetHalo(v.halo, UIKit.Neon, lit ? 1f : 0f);
                        break;
                    }
                case PartType.Switch:
                    {
                        bool glow = v.part.on && power;
                        Lab3DArt.SetGlow(v.capMat, v.part.on ? UIKit.Amber : UIKit.Hex("3a3320"), UIKit.Amber, glow ? 2.6f : 0);
                        Lab3DArt.SetGlow(v.stripMat, UIKit.Hex("222222"), UIKit.Amber, glow ? 2.4f : 0);
                        break;
                    }
                case PartType.Valve52:
                case PartType.Valve32:
                    {
                        bool on = fluid.valveOn.TryGetValue(id, out var o) && o;
                        Lab3DArt.SetGlow(v.coilMat, on ? UIKit.Violet : UIKit.Hex("14182a"), UIKit.Violet, on ? 2f : 0);
                        Lab3DArt.SetGlow(v.onMat, UIKit.Hex("10302a"), UIKit.Neon, on ? 1.6f : 0.05f);
                        Lab3DArt.SetGlow(v.offMat, UIKit.Hex("101a30"), UIKit.Accent, on ? 0.05f : 1.3f);
                        break;
                    }
                case PartType.Compressor:
                case PartType.Pump:
                    {
                        var c = v.part.type == PartType.Pump ? UIKit.Amber : UIKit.Accent;
                        Lab3DArt.SetGlow(v.motorMat, c * 0.5f, c, power ? 2f : 0);
                        break;
                    }
                case PartType.CylDouble:
                case PartType.CylSingle:
                    {
                        state.cyl.TryGetValue(id, out var pos);
                        PlaceRod(v, pos);
                        if (fluid.cylDir.TryGetValue(id, out var dir) && dir != 0 && pos > 0.001f && pos < 0.999f) anyMoving = true;
                        break;
                    }
            }
        }

        foreach (var w in circuit.wires)
        {
            if (!wv.TryGetValue(w.id, out var v)) continue;
            v.val = sim.wire.TryGetValue(w.id, out var val) ? val : 0;
            if (selKind == 2 && selId == w.id) Lab3DArt.SetGlow(v.mat, UIKit.Amber, UIKit.Amber, 0.8f);
            else if (v.val == 1) Lab3DArt.SetGlow(v.mat, UIKit.Neon, UIKit.Neon, 1.4f);
            else Lab3DArt.SetGlow(v.mat, WireOff, Color.black, 0);
            foreach (var p in v.pulses) p.gameObject.SetActive(v.val == 1);
        }
        foreach (var h in circuit.hoses)
        {
            if (!hv.TryGetValue(h.id, out var v)) continue;
            long a = LCircuit.Key(h.aPart, h.aPort), b = LCircuit.Key(h.bPart, h.bPort);
            bool pr = fluid.pressured.Contains(a) && fluid.pressured.Contains(b);
            v.val = pr ? 1 : 0;
            var c = oil ? UIKit.Amber : UIKit.Accent;
            if (selKind == 5 && selId == h.id) Lab3DArt.SetGlow(v.mat, UIKit.Violet, UIKit.Violet, 0.9f);
            else if (pr) Lab3DArt.SetGlow(v.mat, c, c, 1.3f);
            else Lab3DArt.SetGlow(v.mat, oil ? OilOff : AirOff, Color.black, 0);
            foreach (var p in v.pulses) p.gameObject.SetActive(pr && anyMoving);
        }

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
    }

    // ---------------------------------------------------------------- panel
    static string Bit(int v) => v == 1 ? "<color=#00e5a0><b>1</b></color>   " : "<color=#4a5677>0</color>   ";
    static string Pad(string s) => s.Length >= 3 ? s + " " : s.PadRight(4);

    void RenderPanel()
    {
        if (ttText == null) return;
        string powerLine = power ? "<color=#00e5a0>● 전원 ON</color>  5.0 V" : "<color=#ff5470>● 전원 OFF</color>  POWER 버튼(스페이스바)";
        if (exp.fluid) { RenderFluidPanel(powerLine); return; }
        ttHead.text = "진리표";
        var sb = new System.Text.StringBuilder();
        sb.Append(powerLine).Append('\n');
        if (exp.check == ExpCheck.Truth)
        {
            var tc = Experiments.CheckTruth(circuit, exp);
            ttState.text = $"{tc.okRows}/{tc.rows.Count} 일치";
            ttState.color = tc.okRows == tc.rows.Count && tc.rows.Count > 0 ? UIKit.Neon : UIKit.Amber;
            sb.Append("<color=#8b98b8>");
            foreach (var l in exp.inputs) sb.Append(Pad(l));
            sb.Append("</color><color=#4a5677>|  </color><color=#00e5a0>");
            foreach (var l in exp.outputs) sb.Append(Pad(l));
            sb.Append("</color>\n");
            for (int r = 0; r < tc.rows.Count; r++)
            {
                var row = tc.rows[r];
                foreach (var b in row.bits) sb.Append(Bit(b));
                sb.Append("<color=#4a5677>|  </color>");
                for (int j = 0; j < row.outs.Length; j++)
                {
                    sb.Append(Bit(row.outs[j]));
                }
                bool ok = tc.expected[r].SequenceEqual(row.outs);
                sb.Append(ok ? "<color=#00e5a0>○</color>" : $"<color=#ff5470>× → {string.Join("", tc.expected[r])}</color>");
                if (row.current && power) sb.Append(" <color=#4f7cff>◀</color>");
                sb.Append('\n');
            }
            if (!tc.stable) sb.Append("<color=#ffb020>피드백 루프 때문에 값이 안정되지 않아요</color>\n");
        }
        else
        {
            var rows = circuit.Table(out var ins, out var outs, out bool stable);
            if (ins.Count == 0 || outs.Count == 0)
            {
                ttState.text = "대기";
                ttState.color = UIKit.Muted;
                sb.Append("<color=#8b98b8>스위치와 LED를 놓고 전선으로 이으면 진리표가 자동으로 만들어집니다.</color>");
                ttText.text = sb.ToString();
                return;
            }
            ttState.text = $"입력 {ins.Count} · 출력 {outs.Count}";
            ttState.color = UIKit.Neon;
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
        }
        ttText.text = sb.ToString();
    }

    void RenderFluidPanel(string powerLine)
    {
        ttHead.text = "공유압 상태";
        var sb = new System.Text.StringBuilder();
        sb.Append(powerLine).Append('\n');
        if (fluid == null) { ttText.text = sb.ToString(); return; }
        if (!fluid.HasSource) sb.Append("<color=#ffb020>압축기나 유압 펌프가 없어요</color>\n");
        else if (power) sb.Append(fluid.oil ? "<color=#ffb020>● 유압 70 bar</color> 공급 중\n" : "<color=#4f7cff>● 공기압 6 bar</color> 공급 중\n");
        else sb.Append("<color=#8b98b8>압력 없음 — 전원을 켜면 공급돼요</color>\n");
        if (fluid.leak) sb.Append("<color=#ff5470>※ 공급(P)이 배기(R/T)로 바로 이어져 압력이 새요</color>\n");

        foreach (var p in circuit.parts.Where(p => p.type == PartType.Switch).OrderBy(p => p.label))
            sb.Append($"스위치 {p.label}  {(p.on ? "<color=#ffb020>ON</color>" : "<color=#4a5677>OFF</color>")}\n");
        foreach (var p in circuit.parts.Where(p => LCircuit.IsValve(p.type)))
        {
            bool on = fluid.valveOn.TryGetValue(p.id, out var o) && o;
            string path = p.type == PartType.Valve52 ? (on ? "P→A, B→R" : "P→B, A→R") : (on ? "P→A" : "A→R");
            sb.Append($"{LCircuit.Name(p.type)}  {(on ? "<color=#a06bff>코일 ON</color>" : "<color=#4a5677>코일 OFF</color>")}  <color=#8b98b8>{path}</color>\n");
        }
        int moving = 0;
        foreach (var p in circuit.parts.Where(p => LCircuit.IsCylinder(p.type)))
        {
            state.cyl.TryGetValue(p.id, out var pos);
            fluid.cylDir.TryGetValue(p.id, out var dir);
            int filled = Mathf.RoundToInt(pos * 8);
            string bar = new string('■', filled) + new string('□', 8 - filled);
            string st = pos >= 0.98f ? "전진 끝" : pos <= 0.02f ? "후진 끝" : dir > 0 ? "전진 중" : dir < 0 ? "후진 중" : "정지";
            if (dir != 0 && pos > 0.02f && pos < 0.98f) moving++;
            string s0 = power && pos <= 0.02f ? "<color=#00e5a0>S0●</color>" : "<color=#4a5677>S0○</color>";
            string s1 = power && pos >= 0.98f ? "<color=#00e5a0>S1●</color>" : "<color=#4a5677>S1○</color>";
            sb.Append($"<b>{p.label}</b> {LCircuit.Name(p.type)}\n<color=#8fb3ff>{bar}</color> {Mathf.RoundToInt(pos * 100)}%  {st}  {s0} {s1}\n");
        }
        ttState.text = moving > 0 ? "움직이는 중" : power ? "대기" : "OFF";
        ttState.color = moving > 0 ? UIKit.Neon : UIKit.Muted;
        ttText.text = sb.ToString();
    }

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

        StepAndApply(Mathf.Min(Time.deltaTime, 0.05f));
        panelTimer -= Time.deltaTime;
        if (dirty || (exp.fluid && panelTimer <= 0))
        {
            dirty = false;
            panelTimer = 0.2f;
            RenderPanel();
        }
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
            if (v.fan != null && power)
            {
                if (v.part.type == PartType.Compressor) v.fan.localRotation = Quaternion.Euler(t * 720f, 0, 0);
                else v.fan.localRotation = Quaternion.Euler(0, t * 540f, 0);
            }
        }
        foreach (var map in new[] { wv, hv })
            foreach (var v in map.Values)
            {
                if (v.pts == null || v.pulses.Length == 0 || !v.pulses[0].gameObject.activeSelf) continue;
                float len = v.cum[v.cum.Length - 1];
                for (int i = 0; i < v.pulses.Length; i++)
                {
                    float u = Mathf.Repeat(t * 5f / Mathf.Max(1, len) + i / (float)v.pulses.Length, 1f);
                    v.pulses[i].position = Lab3DArt.PointAt(v.pts, v.cum, u);
                }
            }
        Lab3DArt.SetHalo(psHalo, power ? UIKit.Neon : UIKit.Red, power ? 0.8f : 0.35f + 0.35f * Mathf.Sin(t * 3.5f));
    }

    // ---------------------------------------------------------------- UI
    void BuildUI()
    {
        canvas = UIKit.MakeCanvas();
        var c = canvas.transform;

        topBar = UIKit.Rect(c, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 58));
        UIKit.Box(topBar, new Color(0.027f, 0.043f, 0.086f, 0.88f));
        UIKit.Button(UIKit.Rect(topBar, "Menu", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(124, 38)),
            "< 실험 목록", 16, UIKit.Bg2, UIKit.Text, () => SceneManager.LoadScene("Scenes/Start"), UIKit.Border);
        var title = UIKit.Label(topBar, $"<color=#00e5a0>◆</color> {exp.title}", 21, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        title.rectTransform.offsetMin = new Vector2(150, 0);
        title.rectTransform.offsetMax = new Vector2(-390, 0);

        string[] names = { "전원", "초기화", "카메라", "패널" };
        System.Action[] acts = { TogglePower, ClearBoard, () => ResetCamera(false), () => { sideOpen = !sideOpen; Layout(); ResetCamera(false); } };
        for (int i = 0; i < names.Length; i++)
        {
            var rt = UIKit.Rect(topBar, names[i], new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12 - (names.Length - 1 - i) * 90, 0), new Vector2(84, 38));
            bool isPower = i == 0;
            UIKit.Button(rt, names[i], 16, isPower ? new Color(0.35f, 0.08f, 0.13f) : UIKit.Bg2, UIKit.Text, acts[i], isPower ? UIKit.Red : UIKit.Border);
        }

        // parts palette
        palette = UIKit.Rect(c, "Palette", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -70), new Vector2(156, 470));
        UIKit.Box(palette, UIKit.Panel, UIKit.Border);
        var ph = UIKit.Label(palette, "부품", 14, UIKit.Muted, TextAnchor.UpperLeft, FontStyle.Bold);
        ph.rectTransform.offsetMin = new Vector2(14, 0); ph.rectTransform.offsetMax = new Vector2(0, -10);
        foreach (var t in exp.palette)
        {
            var type = t;
            var rt = UIKit.Rect(palette, t.ToString(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(132, 46));
            Color accent = t == PartType.Switch ? UIKit.Amber : t == PartType.Led ? UIKit.Neon : LCircuit.IsFluid(t) ? UIKit.Hex("8fb3ff") : UIKit.Text;
            var b = UIKit.Button(rt, LCircuit.Name(t), 15, UIKit.Bg2, accent, () => AddFromPalette(type), UIKit.Border);
            b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            paletteButtons.Add(rt);
        }

        // mission / status side panel
        side = UIKit.Rect(c, "Side", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -70), new Vector2(320, -84));
        UIKit.Box(side, UIKit.Panel, UIKit.Border);
        int idx = System.Array.IndexOf(Experiments.All, exp) + 1;
        groupText = TopText(side, $"{exp.group} · 실험 {idx}/{Experiments.All.Length}", 13, UIKit.Violet, 14, 18, FontStyle.Bold);
        titleText = TopText(side, exp.title + (passed ? "  <color=#00e5a0>· 완료</color>" : ""), 19, UIKit.Text, 34, 26, FontStyle.Bold);
        float y = 66;
        if (!string.IsNullOrEmpty(exp.goal))
        {
            goalBox = UIKit.Rect(side, "Goal", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(-32, 34));
            UIKit.Box(goalBox, UIKit.Hex("04110c"), UIKit.Hex("0d3b2b"));
            goalText = UIKit.Label(goalBox, exp.goal, 15, UIKit.Neon, TextAnchor.MiddleLeft, FontStyle.Bold);
            goalText.rectTransform.offsetMin = new Vector2(10, 0);
            y += 42;
        }
        descText = TopText(side, exp.desc, 13, UIKit.Muted, y, 76, FontStyle.Normal);
        descText.lineSpacing = 1.1f;
        y += 80;
        if (IsMission && exp.palette.Length < 9)
        {
            allowedText = TopText(side, "사용 가능: " + string.Join(" · ", exp.palette.Select(LCircuit.Name)), 12, UIKit.Amber, y, 18, FontStyle.Normal);
            y += 24;
        }
        ttHead = TopText(side, "진리표", 16, UIKit.Text, y, 22, FontStyle.Bold);
        ttState = TopText(side, "-", 13, UIKit.Neon, y + 2, 20, FontStyle.Normal);
        ttState.alignment = TextAnchor.UpperRight;
        ttText = UIKit.Label(side, "", 16, UIKit.Text, TextAnchor.UpperLeft);
        ttText.rectTransform.offsetMin = new Vector2(18, IsMission ? 116 : 64);
        ttText.rectTransform.offsetMax = new Vector2(-14, -(y + 28));
        ttText.lineSpacing = 1.2f;

        if (IsMission)
        {
            checkBtn = UIKit.Button(UIKit.Rect(side, "Check", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(-32, 46)),
                "채점하기", 18, UIKit.Neon, UIKit.Hex("04110c"), CheckMission);
        }
        nextBtn = UIKit.Button(UIKit.Rect(side, "Next", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(-32, 40)),
            "다음 실험 >", 16, UIKit.Bg2, UIKit.Text, NextExperiment, UIKit.Border);

        selBar = UIKit.Rect(c, "SelBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(340, 48));
        UIKit.Box(selBar, UIKit.Panel, UIKit.Violet);
        selText = UIKit.Label(selBar, "-", 16, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        selText.rectTransform.offsetMin = new Vector2(18, 0); selText.rectTransform.offsetMax = new Vector2(-120, 0);
        delBtn = UIKit.Button(UIKit.Rect(selBar, "Delete", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(104, 36)),
            "삭제", 16, new Color(0.4f, 0.1f, 0.15f), UIKit.Red, DeleteSelected);
        selBar.gameObject.SetActive(false);

        hintRt = UIKit.Rect(c, "Hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 14), new Vector2(760, 34));
        UIKit.Box(hintRt, new Color(0.027f, 0.043f, 0.086f, 0.75f), UIKit.Border);
        UIKit.Label(hintRt, exp.fluid
            ? "핀(●)끼리 끌면 전선 · 포트(파란 고리)끼리 끌면 호스 · 스위치 클릭 ON/OFF · 빈 곳 드래그 회전 · 더블클릭 삭제"
            : "핀(●)을 끌어 다른 부품의 핀에 놓으면 연결 · 스위치 클릭 ON/OFF · 빈 곳 드래그 회전 · 휠 확대 · 더블클릭 삭제", 13, UIKit.Muted, TextAnchor.MiddleCenter);

        toastRt = UIKit.Rect(c, "Toast", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-170, -74), new Vector2(640, 50));
        UIKit.Box(toastRt, UIKit.Panel, UIKit.Neon);
        toastText = UIKit.Label(toastRt, "", 17, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        toastText.rectTransform.offsetMin = new Vector2(12, 0); toastText.rectTransform.offsetMax = new Vector2(-12, 0);
        toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        Layout();
    }

    static Text TopText(RectTransform parent, string text, int size, Color color, float top, float height, FontStyle style)
    {
        var t = UIKit.Label(parent, text, size, color, TextAnchor.UpperLeft, style);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -top);
        rt.sizeDelta = new Vector2(-32, height);
        return t;
    }

    void Layout()
    {
        if (canvas == null) return;
        UIKit.FitScaler(canvas);
        bool portrait = Screen.height > Screen.width;
        int n = paletteButtons.Count;
        if (portrait)
        {
            palette.anchorMin = new Vector2(0, 0); palette.anchorMax = new Vector2(1, 0); palette.pivot = new Vector2(0.5f, 0);
            int rows = Mathf.CeilToInt(n / 4f);
            palette.anchoredPosition = new Vector2(0, 10); palette.sizeDelta = new Vector2(-20, 20 + rows * 54);
            for (int i = 0; i < n; i++)
            {
                var rt = paletteButtons[i];
                rt.anchorMin = rt.anchorMax = new Vector2(i % 4 / 4f, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(6, -10 - (i / 4) * 54);
                rt.sizeDelta = new Vector2(172, 46);
            }
            side.anchorMin = new Vector2(0, 1); side.anchorMax = new Vector2(1, 1); side.pivot = new Vector2(0.5f, 1);
            side.anchoredPosition = new Vector2(0, -68); side.sizeDelta = new Vector2(-20, 620);
            hintRt.gameObject.SetActive(false);
            selBar.anchoredPosition = new Vector2(0, 40 + rows * 54);
            toastRt.anchoredPosition = new Vector2(0, -74);
            if (!portraitInit) { sideOpen = false; portraitInit = true; }
        }
        else
        {
            bool two = n > 8;
            int rows = two ? Mathf.CeilToInt(n / 2f) : n;
            float bw = two ? 98 : 132, bh = two ? 44 : 46, gap = two ? 50 : 53;
            palette.anchorMin = palette.anchorMax = new Vector2(0, 1); palette.pivot = new Vector2(0, 1);
            palette.anchoredPosition = new Vector2(12, -70); palette.sizeDelta = new Vector2(two ? 220 : 156, 46 + rows * gap);
            for (int i = 0; i < n; i++)
            {
                var rt = paletteButtons[i];
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                int col = two ? i % 2 : 0, row = two ? i / 2 : i;
                rt.anchoredPosition = new Vector2(10 + col * (bw + 4), -38 - row * gap);
                rt.sizeDelta = new Vector2(bw, bh);
            }
            side.anchorMin = new Vector2(1, 0); side.anchorMax = new Vector2(1, 1); side.pivot = new Vector2(1, 1);
            side.anchoredPosition = new Vector2(-12, -70); side.sizeDelta = new Vector2(320, -84);
            hintRt.gameObject.SetActive(true);
            selBar.anchoredPosition = new Vector2(-170, 62);
            toastRt.anchoredPosition = new Vector2(-170, -74);
        }
        side.gameObject.SetActive(sideOpen);
    }

    void CheckMission()
    {
        var msg = Experiments.Check(circuit, exp, out bool ok);
        if (ok)
        {
            Experiments.MarkDone(exp.id);
            passed = true;
            titleText.text = exp.title + "  <color=#00e5a0>· 완료</color>";
            int left = Experiments.All.Count(e => e.check != ExpCheck.None && !Experiments.Done().Contains(e.id));
            Toast(msg + (left > 0 ? $" · 남은 미션 {left}개 — '다음 실험'으로 이어가세요" : " · 모든 미션 완료!"), false, 4f);
            nextBtn.GetComponent<Image>().color = UIKit.Violet;
        }
        else Toast(msg, true, 4f);
    }

    void NextExperiment()
    {
        Experiments.Current = (System.Array.IndexOf(Experiments.All, exp) + 1) % Experiments.All.Length;
        SceneManager.LoadScene("Scenes/Lab3D");
    }

    void ClearBoard()
    {
        if (Time.time > clearArmedUntil)
        {
            clearArmedUntil = Time.time + 2.5f;
            Toast("한 번 더 누르면 처음 상태로 되돌립니다", true);
            return;
        }
        clearArmedUntil = 0;
        circuit = exp.setup();
        SyncAll();
        Toast("처음 상태로 되돌렸어요");
    }

    void Toast(string msg, bool warn = false, float seconds = 2.6f)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastRt.GetComponent<Outline>().effectColor = warn ? UIKit.Amber : UIKit.Neon;
        toastUntil = Time.time + seconds;
    }
}
