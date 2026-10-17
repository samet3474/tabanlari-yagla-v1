using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif
#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

public class StartButton : MonoBehaviour
{
    // Start screen as a loading screen: no button press. It shows real progress (save and character/theme databases,
    // whose loading starts here in PlayerData.Create via MusicPlayer, then the main scene loading in the background),
    // a tip, and opens the main menu by itself when everything is ready. No wait is added beyond the actual loading;
    // the bar only glides to each real step instead of jumping.
    public bool autoStart;
    public Image progressFill;   // Image.Type.Filled, horizontal
    public Text progressText;    // "86%"
    public Text tipText;
    public string[] tipKeys;     // text table keys; one is picked at random
    // Same opening as the page's own loading screen (WebGLTemplates/TabanlariYagla/index.html): first only the logo
    // in the middle, then the logo moves up and the tip and bar (this group) appear; the bar fills from 0 to 100.
    public RectTransform logo;
    public CanvasGroup loadingGroup;

    // Safety net only: if a database never reports loaded, go on anyway (the menu waits for it itself).
    const float k_MaxWaitForDatabases = 20f;
    const float k_BarSpeed = 5f;   // fraction of the bar per second while catching up with the real progress
    const float k_LogoOnly = 0.8f; // as the page: logo alone, then the loading screen (loading goes on meanwhile)
    const float k_LogoMove = 0.45f;
    static readonly Vector2 k_LogoLoadingPos = new Vector2(0f, 358f); // centre at 22% from the top (canvas height 1280)
    const float k_LogoLoadingScale = 0.9f;

    bool m_Started;
    float m_Shown;

#if UNITY_WEBGL && !UNITY_EDITOR
    // In the browser there is one loading screen only: the page's own (index.html), which stays up until the main
    // menu opens. This screen's bar and tip are hidden and its progress fills the page's bar from 80% to 100%.
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void TY_LoadingProgress(float fraction);
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void TY_LoadingDone();
    const bool k_PageLoadingScreen = true;
#else
    static void TY_LoadingProgress(float fraction) { }
    static void TY_LoadingDone() { }
    const bool k_PageLoadingScreen = false;
#endif

    static void OnMainLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Main")
            return;
        SceneManager.sceneLoaded -= OnMainLoaded;
        TY_LoadingDone();
    }

    IEnumerator Start()
    {
        if (!autoStart)
            yield break;

        // The plain navy background covers the whole screen: the 3D street behind it is not drawn.
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.cullingMask = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1B, 0x25, 0x50, 0xFF);
        }

        ShowTip();
        SetProgress(0f);

        if (k_PageLoadingScreen)
        {
            // The page's loading screen is on top until the main menu opens; it already showed the logo stage.
            if (loadingGroup != null) loadingGroup.alpha = 0f;
        }
        else
        {
            if (loadingGroup != null) loadingGroup.alpha = 0f;
            // Counted in drawn frames (long first frames capped), so a slow first launch still shows the logo stage.
            for (float shown = 0f; shown < k_LogoOnly; shown += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
                yield return null;
            Vector2 from = logo != null ? logo.anchoredPosition : Vector2.zero;
            for (float t = 0f; t < 1f; )
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / k_LogoMove);
                float e = 1f - (1f - t) * (1f - t);
                if (logo != null)
                {
                    logo.anchoredPosition = Vector2.Lerp(from, k_LogoLoadingPos, e);
                    logo.localScale = Vector3.one * Mathf.Lerp(1f, k_LogoLoadingScale, e);
                }
                if (loadingGroup != null) loadingGroup.alpha = t;
                yield return null;
            }
        }

        float waited = 0f;
        while (!(PlayerData.instance != null && CharacterDatabase.loaded && ThemeDatabase.loaded) && waited < k_MaxWaitForDatabases)
        {
            // Save ready 10%, each database 25%.
            float target = (PlayerData.instance != null ? 0.1f : 0f) + (CharacterDatabase.loaded ? 0.25f : 0f) + (ThemeDatabase.loaded ? 0.25f : 0f);
            Glide(target);
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (PlayerData.instance == null)
            PlayerData.Create();

        AsyncOperation load = Begin(true);
        if (load == null)
            yield break;

        // Main scene: 60% -> 100%. Activation waits until the bar shows the real end, so the menu never opens early
        // and the bar never claims more than is done.
        load.allowSceneActivation = false;
        while (load.progress < 0.9f || m_Shown < 0.999f)
        {
            Glide(0.6f + 0.4f * Mathf.Clamp01(load.progress / 0.9f));
            yield return null;
        }
        SceneManager.sceneLoaded += OnMainLoaded;
        load.allowSceneActivation = true;
    }

    void Glide(float target)
    {
        SetProgress(Mathf.MoveTowards(m_Shown, Mathf.Max(m_Shown, target), k_BarSpeed * Time.unscaledDeltaTime));
    }

    void SetProgress(float value)
    {
        m_Shown = value;
        // In the browser the page's own loading screen already filled the same bar to 80% while downloading the game
        // (WebGLTemplates/TabanlariYagla/index.html); this screen carries on from there.
        float shown = Application.platform == RuntimePlatform.WebGLPlayer ? 0.8f + 0.2f * value : value;
        TY_LoadingProgress(shown);
        if (progressFill != null)
            progressFill.fillAmount = shown;
        if (progressText != null)
            progressText.text = Mathf.FloorToInt(shown * 100f) + "%";
    }

    void ShowTip()
    {
        if (tipText == null || tipKeys == null || tipKeys.Length == 0)
            return;
        string key = tipKeys[Random.Range(0, tipKeys.Length)];
        tipText.text = "<color=#FFD24D>" + Loc.Get("tip.prefix", "İPUCU:") + "</color> " + Loc.Get(key, "");
    }

    public void StartGame()
    {
        Begin(false);
    }

    AsyncOperation Begin(bool async)
    {
        // Once only: the hidden button and the automatic start must not load the main scene twice.
        if (m_Started)
            return null;
        m_Started = true;

        if (PlayerData.instance.ftueLevel == 0)
        {
            PlayerData.instance.ftueLevel = 1;
            PlayerData.instance.Save();
#if UNITY_ANALYTICS
            AnalyticsEvent.FirstInteraction("start_button_pressed");
#endif
        }

#if UNITY_PURCHASING
        var module = StandardPurchasingModule.Instance();
#endif
        // In the background, so the loading screen keeps drawing instead of freezing while the main scene loads.
        if (async)
            return SceneManager.LoadSceneAsync("main");
        SceneManager.LoadScene("main");
        return null;
    }
}
