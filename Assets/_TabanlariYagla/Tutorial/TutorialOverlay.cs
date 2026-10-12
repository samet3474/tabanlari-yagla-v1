using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial prompt drawn over the running game: a short instruction strip, a big animated direction arrow and one dot
/// per step. Nothing covers the track and nothing catches touches (no raycast targets), so swipes reach the game as in a
/// normal run. Built once from code (sprites generated on first use), then only toggled and animated with unscaled time:
/// no allocation or loading while the tutorial runs.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    // Canvas units (the game canvas scales a 720x1280 reference by height; an iPhone in portrait shows ~590 units wide).
    const float k_StripWidth = 520f;
    const float k_StripHeight = 100f;
    const float k_StripAccentHeight = 7f;
    const float k_StripAnchorY = 0.76f;
    const int k_LabelSize = 54;
    const int k_ReadyLabelSize = 76;
    const float k_DotSize = 22f;
    const float k_DotSpacing = 38f;
    const float k_DotsOffsetY = -84f;
    const float k_ArrowSize = 160f;
    // Between the strip and the horizon: above the road, so the arrow never covers the obstacle or the character.
    const float k_ArrowAnchorY = 0.585f;

    const float k_ArrowCycle = 0.85f;      // one slide of the arrow in the swipe direction
    const float k_ArrowTravel = 100f;
    const float k_FadeInTime = 0.15f;
    const float k_PopTime = 0.2f;
    const float k_PopScale = 1.15f;
    const float k_DotPopScale = 1.7f;
    const float k_ReadyHoldTime = 1.3f;
    const float k_PraiseHoldTime = 0.6f;
    const float k_ReadyFadeTime = 0.3f;

    // Istanbul palette: night-blue strip with the red of the Istiklal tram underneath, gold for what to do and what is done.
    static readonly Color k_StripColor = new Color(0.07f, 0.09f, 0.19f, 0.55f);
    static readonly Color k_AccentColor = new Color(0.84f, 0.13f, 0.16f, 0.95f);
    static readonly Color k_Gold = new Color(1.0f, 0.80f, 0.24f, 1.0f);
    static readonly Color k_DotPending = new Color(0.07f, 0.09f, 0.19f, 0.6f);
    static readonly Color k_ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);
    static readonly Color k_RetryColor = new Color(1.0f, 0.55f, 0.45f, 1.0f);

    static Sprite s_ArrowSprite, s_DotSprite, s_RoundedSprite;

    CanvasGroup m_Group;
    RectTransform m_Strip, m_Arrow;
    Image m_ArrowImage;
    Text m_Label;
    Image[] m_Dots;
    RectTransform[] m_DotRects;

    Vector2 m_ArrowDirection;
    float m_ShownTime;
    int m_PoppedDot = -1;
    float m_DotPopTime;
    bool m_Message;
    float m_MessageHold;

    public static TutorialOverlay Create(RectTransform parent, Font font, int stepCount)
    {
        var root = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)root.transform;
        rt.SetParent(parent, false);
        Stretch(rt);
        // Drawn last in its parent: above the HUD, below nothing that takes input (it never blocks raycasts).
        rt.SetAsLastSibling();

        var overlay = root.AddComponent<TutorialOverlay>();
        overlay.Build(font, stepCount);
        root.SetActive(false);
        return overlay;
    }

    void Build(Font font, int stepCount)
    {
        CreateSprites();

        m_Group = GetComponent<CanvasGroup>();
        m_Group.interactable = false;
        m_Group.blocksRaycasts = false;

        // Instruction strip
        m_Strip = NewRect("Strip", transform, new Vector2(0.5f, k_StripAnchorY), new Vector2(k_StripWidth, k_StripHeight));
        Image strip = m_Strip.gameObject.AddComponent<Image>();
        strip.sprite = s_RoundedSprite;
        strip.type = Image.Type.Sliced;
        strip.color = k_StripColor;
        strip.raycastTarget = false;

        RectTransform accent = NewRect("Accent", m_Strip, new Vector2(0.5f, 0.0f), new Vector2(k_StripWidth - 60f, k_StripAccentHeight));
        accent.anchoredPosition = new Vector2(0.0f, k_StripAccentHeight * 0.5f + 4f);
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.sprite = s_RoundedSprite;
        accentImage.type = Image.Type.Sliced;
        accentImage.color = k_AccentColor;
        accentImage.raycastTarget = false;

        RectTransform labelRect = NewRect("Label", m_Strip, new Vector2(0.5f, 0.5f), new Vector2(k_StripWidth - 30f, k_StripHeight));
        labelRect.anchoredPosition = new Vector2(0.0f, 3f);
        m_Label = labelRect.gameObject.AddComponent<Text>();
        m_Label.font = font;
        m_Label.fontSize = k_LabelSize;
        m_Label.alignment = TextAnchor.MiddleCenter;
        m_Label.horizontalOverflow = HorizontalWrapMode.Overflow;
        m_Label.verticalOverflow = VerticalWrapMode.Overflow;
        m_Label.color = Color.white;
        m_Label.raycastTarget = false;
        AddShadow(m_Label.gameObject, new Vector2(2f, -3f));

        // Step dots
        m_Dots = new Image[stepCount];
        m_DotRects = new RectTransform[stepCount];
        float firstX = -0.5f * (stepCount - 1) * k_DotSpacing;
        for (int i = 0; i < stepCount; ++i)
        {
            RectTransform dot = NewRect("Dot" + i, m_Strip, new Vector2(0.5f, 0.5f), new Vector2(k_DotSize, k_DotSize));
            dot.anchoredPosition = new Vector2(firstX + i * k_DotSpacing, k_DotsOffsetY);
            Image image = dot.gameObject.AddComponent<Image>();
            image.sprite = s_DotSprite;
            image.raycastTarget = false;
            AddShadow(dot.gameObject, new Vector2(2f, -2f));
            m_Dots[i] = image;
            m_DotRects[i] = dot;
        }

        // Direction arrow
        m_Arrow = NewRect("Arrow", transform, new Vector2(0.5f, k_ArrowAnchorY), new Vector2(k_ArrowSize, k_ArrowSize));
        m_ArrowImage = m_Arrow.gameObject.AddComponent<Image>();
        m_ArrowImage.sprite = s_ArrowSprite;
        m_ArrowImage.color = k_Gold;
        m_ArrowImage.raycastTarget = false;
        AddShadow(m_Arrow.gameObject, new Vector2(4f, -5f));
    }

    /// <summary>Shows step <paramref name="index"/>: its instruction and an arrow sliding in the swipe direction.</summary>
    public void ShowStep(int index, string text, Vector2 direction)
    {
        gameObject.SetActive(true);
        m_Message = false;
        m_Label.text = text;
        m_Label.fontSize = k_LabelSize;
        m_Label.color = Color.white;

        m_ArrowDirection = direction;
        m_Arrow.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        m_Arrow.gameObject.SetActive(true);

        for (int i = 0; i < m_Dots.Length; ++i)
            m_Dots[i].color = i < index ? k_Gold : i == index ? Color.white : k_DotPending;

        m_ShownTime = Time.unscaledTime;
        UpdateAnimation();
    }

    /// <summary>Marks a step as done (its dot fills and pops).</summary>
    public void MarkDone(int index)
    {
        if (index < 0 || index >= m_Dots.Length)
            return;
        m_Dots[index].color = k_Gold;
        m_PoppedDot = index;
        m_DotPopTime = Time.unscaledTime;
    }

    /// <summary>Shows the closing message (all dots done), then fades out and turns itself off.</summary>
    public void ShowReady(string text)
    {
        for (int i = 0; i < m_Dots.Length; ++i)
            m_Dots[i].color = k_Gold;
        ShowMessage(text, k_Gold, k_ReadyLabelSize, k_ReadyHoldTime);
    }

    /// <summary>Short praise after a step, then fades out (the next prompt comes with the next obstacle).</summary>
    public void ShowPraise(string text)
    {
        ShowMessage(text, k_Gold, k_LabelSize, k_PraiseHoldTime);
    }

    /// <summary>After a crash: stays until the step's prompt shows again for the new obstacle.</summary>
    public void ShowRetry(string text)
    {
        ShowMessage(text, k_RetryColor, k_LabelSize, -1.0f);
    }

    // A line of text without the arrow. holdTime < 0: stays until the next ShowStep/Hide.
    void ShowMessage(string text, Color color, int fontSize, float holdTime)
    {
        gameObject.SetActive(true);
        m_Message = true;
        m_MessageHold = holdTime;
        m_Label.text = text;
        m_Label.fontSize = fontSize;
        m_Label.color = color;
        m_Arrow.gameObject.SetActive(false);

        m_ShownTime = Time.unscaledTime;
        UpdateAnimation();
    }

    public void Hide()
    {
        m_PoppedDot = -1;
        gameObject.SetActive(false);
    }

    void Update()
    {
        UpdateAnimation();
    }

    void UpdateAnimation()
    {
        float t = Time.unscaledTime - m_ShownTime;

        float alpha = Mathf.Clamp01(t / k_FadeInTime);
        if (m_Message && m_MessageHold >= 0.0f && t > m_MessageHold)
        {
            alpha = 1.0f - Mathf.Clamp01((t - m_MessageHold) / k_ReadyFadeTime);
            if (alpha <= 0.0f)
            {
                Hide();
                return;
            }
        }
        m_Group.alpha = alpha;

        // New instruction pops in.
        float pop = Mathf.Clamp01(t / k_PopTime);
        m_Strip.localScale = Vector3.one * Mathf.Lerp(m_Message ? k_PopScale * 1.1f : k_PopScale, 1.0f, EaseOut(pop));

        // Arrow slides along the swipe direction and fades at both ends of the slide, then starts again.
        if (!m_Message)
        {
            float cycle = Mathf.Repeat(t, k_ArrowCycle) / k_ArrowCycle;
            float travel = EaseOut(cycle);
            m_Arrow.anchoredPosition = m_ArrowDirection * ((travel - 0.35f) * k_ArrowTravel);
            Color c = k_Gold;
            c.a = Mathf.Clamp01(cycle / 0.15f) * Mathf.Clamp01((1.0f - cycle) / 0.3f);
            m_ArrowImage.color = c;
        }

        // Dot of the step just done.
        if (m_PoppedDot >= 0)
        {
            float d = Mathf.Clamp01((Time.unscaledTime - m_DotPopTime) / k_PopTime);
            m_DotRects[m_PoppedDot].localScale = Vector3.one * Mathf.Lerp(k_DotPopScale, 1.0f, EaseOut(d));
            if (d >= 1.0f)
                m_PoppedDot = -1;
        }
    }

    static float EaseOut(float x)
    {
        float inv = 1.0f - x;
        return 1.0f - inv * inv * inv;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AddShadow(GameObject go, Vector2 distance)
    {
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = k_ShadowColor;
        shadow.effectDistance = distance;
    }

    // ---- Generated sprites (white, tinted by the Image color), made once per app run ----

    const int k_ArrowTexSize = 128;
    const int k_DotTexSize = 64;
    const int k_RoundedTexSize = 64;
    const float k_RoundedRadius = 24f;
    const int k_Supersample = 4;

    static void CreateSprites()
    {
        if (s_ArrowSprite != null)
            return;

        s_ArrowSprite = MakeSprite(k_ArrowTexSize, ArrowCoverage, Vector4.zero);
        s_DotSprite = MakeSprite(k_DotTexSize, (x, y) => (x - 32f) * (x - 32f) + (y - 32f) * (y - 32f) <= 30f * 30f, Vector4.zero);
        float b = k_RoundedRadius + 2f;
        s_RoundedSprite = MakeSprite(k_RoundedTexSize, RoundedCoverage, new Vector4(b, b, b, b));
    }

    // Right-pointing arrow: a thick shaft and a wide head, in a 128x128 box.
    static bool ArrowCoverage(float x, float y)
    {
        const float shaftStart = 12f, headStart = 64f, tip = 120f, mid = 64f;
        const float shaftHalf = 19f, headHalf = 50f;
        float dy = Mathf.Abs(y - mid);
        if (x >= shaftStart && x <= headStart + 2f && dy <= shaftHalf)
            return true;
        if (x >= headStart && x <= tip)
            return dy <= headHalf * (tip - x) / (tip - headStart);
        return false;
    }

    static bool RoundedCoverage(float x, float y)
    {
        float s = k_RoundedTexSize - 1;
        float cx = Mathf.Clamp(x, k_RoundedRadius, s - k_RoundedRadius);
        float cy = Mathf.Clamp(y, k_RoundedRadius, s - k_RoundedRadius);
        return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= k_RoundedRadius * k_RoundedRadius;
    }

    static Sprite MakeSprite(int size, System.Func<float, float, bool> inside, Vector4 border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        float step = 1.0f / k_Supersample;
        for (int py = 0; py < size; ++py)
        {
            for (int px = 0; px < size; ++px)
            {
                int hits = 0;
                for (int sy = 0; sy < k_Supersample; ++sy)
                    for (int sx = 0; sx < k_Supersample; ++sx)
                        if (inside(px + (sx + 0.5f) * step, py + (sy + 0.5f) * step))
                            hits++;
                byte a = (byte)(255 * hits / (k_Supersample * k_Supersample));
                pixels[py * size + px] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
