// 시작 화면 — 입력 없이 버튼 하나로 들어가기
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    Transform chip;
    Transform[] orbs;
    Canvas canvas;
    RectTransform card;
    int lastW, lastH;

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
        RenderSettings.ambientSkyColor = new Color(0.35f, 0.42f, 0.65f);
        RenderSettings.ambientEquatorColor = new Color(0.14f, 0.16f, 0.26f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.09f);
        RenderSettings.reflectionIntensity = 0.15f;

        BuildDecor();
        BuildUI();
    }

    void BuildDecor()
    {
        var kit = Lab3DArt.Load();
        chip = new GameObject("Chip").transform;
        chip.position = new Vector3(2.6f, 0, 0);
        var body = kit.Cube(chip, new Vector3(0, 0, 0), new Vector3(3.2f, 0.8f, 2.2f), kit.Body(new Color(0.05f, 0.07f, 0.11f)));
        Destroy(body.GetComponent<Collider>());
        kit.FlatText(chip, "LOGIC", new Vector3(0, 0.41f, 0.15f), 0.11f, new Color(0.84f, 0.87f, 0.96f));
        kit.FlatText(chip, "A·B  A+B  A'", new Vector3(0, 0.41f, -0.45f), 0.05f, UIKit.Neon);
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
            var o = kit.Sphere(null, Vector3.zero, 0.14f, kit.Unlit(UIKit.Neon * 1.4f));
            Destroy(o.GetComponent<Collider>());
            kit.Halo(o.transform, UIKit.Neon, 0.9f);
            orbs[i] = o.transform;
        }
    }

    void BuildUI()
    {
        canvas = UIKit.MakeCanvas();
        card = UIKit.Rect(canvas.transform, "Card", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(520, 560));

        var kicker = UIKit.Label(card, "VIRTUAL LOGIC LABS", 18, UIKit.Neon, TextAnchor.UpperLeft, FontStyle.Bold);
        Place(kicker.rectTransform, 0, 40);
        var title = UIKit.Label(card, "가상 논리회로 실험실", 48, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
        Place(title.rectTransform, 30, 70);
        var sub = UIKit.Label(card, "3D 브레드보드에 칩을 놓고 전선을 이은 뒤\n전원을 켜서 신호가 흐르는 것을 확인해 보세요.", 20, UIKit.Muted, TextAnchor.UpperLeft);
        Place(sub.rectTransform, 100, 70);

        MakeButton("3D 회로 실험실  시작", "전원 · 전선 연결 · 진리표", 200, UIKit.Neon, new Color(0.02f, 0.07f, 0.05f), true, () => SceneManager.LoadScene("Scenes/Lab3D"));
        MakeButton("학생 실습", "원작 Lab 1 · Lab 2 (영문)", 300, UIKit.Bg2, UIKit.Text, false, () => SceneManager.LoadScene("Scenes/StudentSubsystem"));
        MakeButton("관리자 화면", "원작 Admin (영문)", 390, UIKit.Bg2, UIKit.Text, false, () => SceneManager.LoadScene("Scenes/AdminSubsystem"));

        var credit = UIKit.Label(card, "로그인 없이 바로 시작합니다 · 원작 Rutgers ECE 2018 Group 10 · Unity 6 WebGL", 14, UIKit.Muted, TextAnchor.UpperLeft);
        Place(credit.rectTransform, 490, 40);
    }

    static void Place(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -top);
        rt.sizeDelta = new Vector2(0, height);
    }

    void MakeButton(string text, string hint, float top, Color bg, Color fg, bool primary, System.Action onClick)
    {
        float h = primary ? 82 : 74;
        var rt = UIKit.Rect(card, text, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -top), new Vector2(0, h));
        var b = UIKit.Button(rt, "", 24, bg, fg, onClick, primary ? (Color?)null : UIKit.Border);
        var main = UIKit.Label(rt, text, primary ? 26 : 23, fg, TextAnchor.UpperLeft, FontStyle.Bold);
        main.rectTransform.offsetMin = new Vector2(24, 0); main.rectTransform.offsetMax = new Vector2(-24, -14);
        var small = UIKit.Label(rt, hint, 15, primary ? new Color(0.02f, 0.2f, 0.14f) : UIKit.Muted, TextAnchor.LowerLeft);
        small.rectTransform.offsetMin = new Vector2(24, 12); small.rectTransform.offsetMax = new Vector2(-24, 0);
        var arrow = UIKit.Label(rt, ">", 28, fg, TextAnchor.MiddleRight, FontStyle.Bold);
        arrow.rectTransform.offsetMax = new Vector2(-22, 0);
        b.GetComponentInChildren<Text>().text = "";
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
            UIKit.FitScaler(canvas);
            bool portrait = Screen.height > Screen.width;
            card.anchorMin = card.anchorMax = portrait ? new Vector2(0.5f, 0.42f) : new Vector2(0, 0.5f);
            card.pivot = portrait ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f);
            card.anchoredPosition = portrait ? Vector2.zero : new Vector2(70, 0);
            card.sizeDelta = portrait ? new Vector2(660, 560) : new Vector2(520, 560);
            chip.position = portrait ? new Vector3(0, 2.6f, 2) : new Vector3(2.6f, 0, 0);
        }
    }
}
