using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Keeps this RectTransform inside the screen's safe area (notch, Dynamic Island, camera hole, home indicator,
/// gesture bar), so whatever is laid out inside it is never cut off. Anchors follow the safe rect; offsets are zero.
///
/// Native builds read Screen.safeArea. In the browser the web page is laid out with viewport-fit=cover, so the
/// canvas reaches under the notch and the home indicator: there the insets come from CSS env(safe-area-inset-*)
/// (SafeArea.jslib), converted to canvas pixels.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    // Test builds only (see Assets/_Dev): replaces the device's safe area to check layouts for phones the test
    // machine is not, e.g. new Rect(0, 102, 1170, 2290) for an iPhone 13 web app. Never set by game code.
    public static Rect? debugSafeArea;

    const float k_RecheckInterval = 0.5f; // rotation/resize, browser toolbars showing or hiding

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern float TY_SafeAreaInset(int side); // 0 top, 1 right, 2 bottom, 3 left
#endif

    RectTransform m_Rect;
    Rect m_Applied;
    int m_ScreenWidth, m_ScreenHeight;
    float m_NextCheck;

    void OnEnable()
    {
        m_Rect = (RectTransform)transform;
        Apply(true);
    }

    void Update()
    {
        if (Time.unscaledTime < m_NextCheck)
            return;
        m_NextCheck = Time.unscaledTime + k_RecheckInterval;
        Apply(false);
    }

    void Apply(bool force)
    {
        Rect safe = CurrentSafeArea();
        if (!force && safe == m_Applied && Screen.width == m_ScreenWidth && Screen.height == m_ScreenHeight)
            return;
        m_Applied = safe;
        m_ScreenWidth = Screen.width;
        m_ScreenHeight = Screen.height;
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        m_Rect.anchorMin = min;
        m_Rect.anchorMax = max;
        m_Rect.offsetMin = Vector2.zero;
        m_Rect.offsetMax = Vector2.zero;
    }

    public static Rect CurrentSafeArea()
    {
        if (debugSafeArea.HasValue)
            return debugSafeArea.Value;
#if UNITY_WEBGL && !UNITY_EDITOR
        float top = TY_SafeAreaInset(0), right = TY_SafeAreaInset(1), bottom = TY_SafeAreaInset(2), left = TY_SafeAreaInset(3);
        return new Rect(left, bottom, Mathf.Max(0f, Screen.width - left - right), Mathf.Max(0f, Screen.height - top - bottom));
#else
        return Screen.safeArea;
#endif
    }
}
