using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif
#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

public class StartButton : MonoBehaviour
{
    // Start screen as a loading screen: no button press. As soon as the save and the character/theme databases are
    // ready (their loading starts here, in PlayerData.Create via MusicPlayer), the main scene is loaded in the
    // background and opens by itself on the main menu. No wait is added beyond the actual loading.
    public bool autoStart;
    // Safety net only: if a database never reports loaded, go on anyway (the menu waits for it itself).
    const float k_MaxWaitForDatabases = 20f;

    bool m_Started;

    IEnumerator Start()
    {
        if (!autoStart)
            yield break;

        float waited = 0f;
        while (!(PlayerData.instance != null && CharacterDatabase.loaded && ThemeDatabase.loaded) && waited < k_MaxWaitForDatabases)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (PlayerData.instance == null)
            PlayerData.Create();
        Begin(true);
    }

    public void StartGame()
    {
        Begin(false);
    }

    void Begin(bool async)
    {
        // Once only: the hidden button and the automatic start must not load the main scene twice.
        if (m_Started)
            return;
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
            SceneManager.LoadSceneAsync("main");
        else
            SceneManager.LoadScene("main");
    }
}
