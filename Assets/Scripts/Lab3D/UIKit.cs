// 런타임 UI 도우미 — 나눔고딕 폰트, 둥근 패널, 네온 버튼 (성남 AI 랩 팔레트)
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIKit
{
    public static readonly Color Bg = Hex("070b16");
    public static readonly Color Bg2 = Hex("0c1226");
    public static readonly Color Panel = new Color(0.063f, 0.094f, 0.157f, 0.92f);
    public static readonly Color Border = Hex("1f2b45");
    public static readonly Color Text = Hex("eaf0ff");
    public static readonly Color Muted = Hex("8b98b8");
    public static readonly Color Accent = Hex("4f7cff");
    public static readonly Color Neon = Hex("00e5a0");
    public static readonly Color Violet = Hex("a06bff");
    public static readonly Color Amber = Hex("ffb020");
    public static readonly Color Red = Hex("ff5470");

    static Font font;
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("NanumGothic-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    static Sprite rounded;
    /// <summary>9-sliced rounded rectangle, generated once.</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int s = 48, r = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (s - r)));
                    float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (s - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return rounded;
        }
    }

    public static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }

    public static Canvas MakeCanvas()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>Keeps UI readable on portrait phones.</summary>
    public static void FitScaler(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        bool portrait = Screen.height > Screen.width;
        scaler.referenceResolution = portrait ? new Vector2(760, 1280) : new Vector2(1280, 720);
        scaler.matchWidthOrHeight = portrait ? 0f : 0.5f;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static Image Box(RectTransform rt, Color color, Color? border = null)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        if (border.HasValue)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = border.Value;
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var rt = Rect(parent, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static Button Button(RectTransform rt, string text, int size, Color bg, Color fg, Action onClick, Color? border = null)
    {
        var img = Box(rt, bg, border);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.25f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1.4f;
        b.colors = cb;
        var t = Label(rt, text, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.rectTransform.offsetMin = new Vector2(8, 0);
        t.rectTransform.offsetMax = new Vector2(-8, 0);
        b.onClick.AddListener(() => onClick());
        return b;
    }
}
