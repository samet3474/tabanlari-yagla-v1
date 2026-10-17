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

    // Safety net only: if a database never reports loaded, go on anyway (the menu waits for it itself).
    const float k_MaxWaitForDatabases = 20f;
    const float k_BarSpeed = 5f;   // fraction of the bar per second while catching up with the real progress

    bool m_Started;
    float m_Shown;

    IEnumerator Start()
    {
        if (!autoStart)
            yield break;

        ShowTip();
        SetProgress(0f);

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
