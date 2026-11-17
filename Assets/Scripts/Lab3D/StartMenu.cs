// 시작 화면 — 로그인 없이 실험 목록에서 골라 바로 시작
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    Transform chip;
    Transform[] orbs;
    Canvas canvas;
    RectTransform head, grid;
    readonly System.Collections.Generic.List<RectTransform> cards = new System.Collections.Generic.List<RectTransform>();
    readonly System.Collections.Generic.List<int> cardIndex = new System.Collections.Generic.List<int>();
    readonly System.Collections.Generic.List<Image> cardImages = new System.Collections.Generic.List<Image>();
    int hovered = -1;
    readonly System.Collections.Generic.List<RectTransform> headers = new System.Collections.Generic.List<RectTransform>();
    int lastW, lastH;

    static readonly string[] Groups = { "논리 회로", "공압", "유압", "공유압" };

    void Start()
    {
        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = UIKit.Bg;
        cam.fieldOfView = 40;
        cam.transform.position = new Vector3(0, 3.2f, -9);
        cam.transform.LookAt(new Vector3(0, 0.2f, 0));

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(50, 30, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.3f, 0.36f, 0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.14f, 0.22f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.09f);
        RenderSettings.reflectionIntensity = 0.3f;

        BuildDecor();
        BuildUI();
    }

    void BuildDecor()
    {
        var kit = Lab3DArt.Load();
        chip = new GameObject("Chip").transform;
        var body = kit.Cube(chip, Vector3.zero, new Vector3(3.2f, 0.8f, 2.2f), kit.Body(new Color(0.05f, 0.07f, 0.11f)));
        Destroy(body.GetComponent<Collider>());
        kit.FlatText(chip, "LOGIC", new Vector3(0, 0.41f, 0.15f), 0.11f, new Color(0.84f, 0.87f, 0.96f));
        kit.FlatText(chip, "공압 · 유압 · 논리", new Vector3(0, 0.41f, -0.45f), 0.05f, UIKit.Neon);
        for (int i = 0; i < 4; i++)
        {
            float x = -1.2f + i * 0.8f;
            foreach (float z in new[] { -1.35f, 1.35f })
            {
                var pin = kit.Sphere(chip, new Vector3(x, -0.1f, z), 0.32f, kit.Emissive(UIKit.Neon, (i + (z > 0 ? 1 : 0)) % 2 == 0 ? 2.5f : 0.2f));
                Destroy(pin.GetComponent<Collider>());
            }
        }
        orbs = new Transform[6];
        for (int i = 0; i < orbs.Length; i++)
        {
            var o = kit.Sphere(null, Vector3.zero, 0.14f, kit.Unlit(i % 2 == 0 ? UIKit.Neon * 1.4f : UIKit.Accent * 1.4f));
            Destroy(o.GetComponent<Collider>());
            kit.Halo(o.transform, i % 2 == 0 ? UIKit.Neon : UIKit.Accent, 0.9f);
            orbs[i] = o.transform;
        }
    }

    void BuildUI()
    {
        canvas = UIKit.MakeCanvas();
        head = UIKit.Rect(canvas.transform, "Head", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -40), new Vector2(560, 150));
        var kicker = UIKit.Label(head, "VIRTUAL LOGIC LABS", 17, UIKit.Neon, TextAnchor.UpperLeft, FontStyle.Bold);
        Place(kicker.rectTransform, 0, 26);
        var title = UIKit.Label(head, "가상 회로 · 공유압 실험실", 42, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
        Place(title.rectTransform, 26, 58);
        var done = Experiments.Done();
        int missions = Experiments.All.Count(e => e.check != ExpCheck.None);
        int cleared = Experiments.All.Count(e => e.check != ExpCheck.None && done.Contains(e.id));
        var sub = UIKit.Label(head, $"실험을 골라 바로 시작하세요 — 로그인 필요 없음 · 미션 {cleared}/{missions} 완료", 18, UIKit.Muted, TextAnchor.UpperLeft);
        Place(sub.rectTransform, 92, 30);

        grid = UIKit.Rect(canvas.transform, "Grid", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -200), new Vector2(760, 480));
        foreach (var g in Groups)
        {
            var list = Experiments.All.Select((e, i) => (e, i)).Where(t => t.e.group == g).ToList();
            if (list.Count == 0) continue;
            var h = UIKit.Rect(grid, "H_" + g, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 26));
            var color = g == "논리 회로" ? UIKit.Neon : g == "유압" ? UIKit.Amber : UIKit.Accent;
            UIKit.Label(h, g == "공유압" ? "자유 실험 (공유압)" : g, 16, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            headers.Add(h);
            foreach (var (e, i) in list) { cards.Add(MakeCard(e, i, color, done.Contains(e.id))); cardIndex.Add(i); }
        }
        var credit = UIKit.Label(canvas.transform, "원작 Rutgers ECE 2018 Group 10 · Unity 6 WebGL · MetaSonGi", 13, UIKit.Muted, TextAnchor.LowerLeft);
        credit.rectTransform.offsetMin = new Vector2(56, 14);
        credit.rectTransform.offsetMax = new Vector2(-20, 0);
    }

    RectTransform MakeCard(Experiment e, int index, Color accent, bool done)
    {
        var rt = UIKit.Rect(grid, e.id, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(244, 70));
        // clicks are hit-tested by hand in Update (the UI raycaster missed these cards in the web build)
        cardImages.Add(UIKit.Box(rt, UIKit.Bg2, UIKit.Border));
        var stripe = UIKit.Rect(rt, "Stripe", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(5, -16));
        stripe.anchoredPosition = new Vector2(6, 0);
        var si = stripe.gameObject.AddComponent<Image>();
        si.color = accent;
        si.raycastTarget = false;
        string mark = done ? "  <color=#00e5a0>완료</color>" : "";
        var t = UIKit.Label(rt, $"<color=#8b98b8>{index + 1:00}</color>  {e.title}{mark}", 16, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
        t.rectTransform.offsetMin = new Vector2(20, 0); t.rectTransform.offsetMax = new Vector2(-10, -12);
        string hint = string.IsNullOrEmpty(e.goal) ? "자유 실험 — 예제부터 시작" : e.goal;
        var s = UIKit.Label(rt, hint, 12, UIKit.Muted, TextAnchor.LowerLeft);
        s.rectTransform.offsetMin = new Vector2(20, 12); s.rectTransform.offsetMax = new Vector2(-10, 0);
        s.horizontalOverflow = HorizontalWrapMode.Overflow;
        return rt;
    }

    static void Place(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -top);
        rt.sizeDelta = new Vector2(0, height);
    }

    void Layout()
    {
        UIKit.FitScaler(canvas);
        bool portrait = Screen.height > Screen.width;
        int cols = portrait ? 2 : 3;
        float cw = portrait ? 330 : 244, ch = 70, gx = 10, gy = 10;
        head.anchoredPosition = portrait ? new Vector2(30, -40) : new Vector2(56, -40);
        head.sizeDelta = portrait ? new Vector2(700, 150) : new Vector2(760, 150);
        grid.anchoredPosition = portrait ? new Vector2(30, -200) : new Vector2(56, -196);
        float y = 0;
        int ci = 0, hi = 0;
        foreach (var g in Groups)
        {
            int count = Experiments.All.Count(e => e.group == g);
            if (count == 0) continue;
            headers[hi++].anchoredPosition = new Vector2(0, -y);
            y += 32;
            for (int k = 0; k < count; k++, ci++)
            {
                var rt = cards[ci];
                rt.sizeDelta = new Vector2(cw, ch);
                rt.anchoredPosition = new Vector2((k % cols) * (cw + gx), -(y + (k / cols) * (ch + gy)));
            }
            y += Mathf.CeilToInt(count / (float)cols) * (ch + gy) + 6;
        }
        chip.position = portrait ? new Vector3(0, -3.6f, 4) : new Vector3(4.6f, 1.4f, 2);
    }

    void Update()
    {
        float t = Time.time;
        if (chip != null) chip.rotation = Quaternion.Euler(18 + Mathf.Sin(t * 0.6f) * 6, t * 22, 0);
        for (int i = 0; i < orbs.Length; i++)
        {
            float a = t * 0.9f + i * Mathf.PI * 2 / orbs.Length;
            orbs[i].position = chip.position + new Vector3(Mathf.Cos(a) * 2.9f, Mathf.Sin(a * 2) * 0.35f, Mathf.Sin(a) * 2.2f);
        }
        if (Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width; lastH = Screen.height;
            Layout();
        }
        Vector2 mp = Input.mousePosition;
        int over = -1;
        for (int i = 0; i < cards.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(cards[i], mp, null)) { over = i; break; }
        if (over != hovered)
        {
            if (hovered >= 0) cardImages[hovered].color = UIKit.Bg2;
            hovered = over;
            if (over >= 0) cardImages[over].color = UIKit.Hex("1a2547");
        }
        if (over >= 0 && Input.GetMouseButtonUp(0))
        {
            Experiments.Current = cardIndex[over];
            SceneManager.LoadScene("Scenes/Lab3D");
        }
    }
}
