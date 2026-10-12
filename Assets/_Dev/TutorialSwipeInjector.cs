#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Eğitim girdi teşhisi için yapay kaydırma üretir: yalnızca TY_DEVTOOLS sembollü WEB test derlemesinde ve adreste
// "?kaydirtest=..." varken çalışır (oyunun dokunmatik kolu yalnızca web/mobil derlemede derleniyor).
// Sanal bir Touchscreen aygıtına gerçek dokunma olayları gönderir; oyun bunları gerçek parmak gibi okur
// (CharacterInputController -> TutorialMoveCheck -> GameState). Oyun koduna ve alanlarına dokunmaz.
//
// Senaryolar (eğitimin her seviyesinde: 0 şerit, 1 zıpla, 2 kay):
//   kaydirtest=egik  Yönerge açıkken DOĞRU yöne kaydırır ama ilk ~%1,5'lik kısım öbür eksene kayar
//                    (gerçek parmağın başlangıçtaki küçük sapması). 1,5 sn sonra temiz bir kaydırma yapar.
//   kaydirtest=once  Yönerge AÇILMADAN önce o seviyenin doğru hareketini yapar; yönerge açılınca 1,5 sn bekleyip
//                    temiz kaydırma yapar.
//   kaydirtest=dogru Yönerge açıkken yalnızca temiz kaydırma (karşılaştırma için).
// Sonuçları TutorialInputProbe yazar ("KAYDIR", "KAYDIR-SON", "YONERGE", "OZET").
// Not: bu modda eğitim boyunca çarpışma yok sayılır (yalnızca engellerin geçilmesi için; girdi yolunu etkilemez).
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class TutorialSwipeInjector : MonoBehaviour
{
    static readonly BindingFlags k_Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly FieldInfo s_GsPrompt = typeof(GameState).GetField("m_TutorialPromptVisible", k_Inst);
    static readonly FieldInfo s_CicLane = typeof(CharacterInputController).GetField("m_CurrentLane", k_Inst);

    string m_Mode;
    Touchscreen m_Screen;
    int m_TouchId = 100;

    // Sıradaki kaydırmanın kare kare konumları (null: kaydırma yok).
    readonly Queue<(Vector2 pos, UnityEngine.InputSystem.TouchPhase phase)> m_Steps = new Queue<(Vector2, UnityEngine.InputSystem.TouchPhase)>();
    float m_WaitUntil;
    bool m_LastPrompt;
    int m_PromptLevel = -1;
    int m_PreDoneLevel = -1;
    int m_TiltDoneLevel = -1;
    bool m_CleanPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer) return;
        string url = Application.absoluteURL;
        int i = url.IndexOf("kaydirtest=", System.StringComparison.Ordinal);
        if (i < 0) return;
        string mode = url.Substring(i + "kaydirtest=".Length);
        int amp = mode.IndexOf('&');
        if (amp >= 0) mode = mode.Substring(0, amp);

        var go = new GameObject("TutorialSwipeInjector");
        DontDestroyOnLoad(go);
        var inj = go.AddComponent<TutorialSwipeInjector>();
        inj.m_Mode = mode;
        inj.m_Screen = InputSystem.AddDevice<Touchscreen>("KaydirTestEkrani");
        Log($"KAYDIRTEST basladi mod={mode} (yapay dokunma: gercek girdi yolu kullaniliyor)");
    }

    static void Log(string msg)
    {
        if (DevTimeline.instance != null) DevTimeline.instance.Log(msg);
        else Debug.Log("TL| " + msg);
    }

    void OnDestroy()
    {
        if (m_Screen != null && m_Screen.added) InputSystem.RemoveDevice(m_Screen);
    }

    // Doğru yön: 0 -> boş şeride yatay, 1 -> yukarı, 2 -> aşağı.
    static Vector2 CorrectDir(int level, CharacterInputController chr)
    {
        if (level == 1) return Vector2.up;
        if (level == 2) return Vector2.down;
        int lane = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : 1;
        return lane >= 2 ? Vector2.left : Vector2.right;
    }

    void QueueSwipe(Vector2 dir, bool tilted, string why)
    {
        float w = Screen.width;
        Vector2 start = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
        Vector2 wrong = new Vector2(Mathf.Abs(dir.y), Mathf.Abs(dir.x)); // öbür eksen
        if (dir.x < 0 || dir.y < 0) wrong = -wrong;                       // sapma yönü sabit olmasın diye harekete bağlı

        m_Steps.Clear();
        m_Steps.Enqueue((start, UnityEngine.InputSystem.TouchPhase.Began));
        Vector2 p = start;
        if (tilted)
        {
            // İlk örnek: öbür eksende %1,5, doğru eksende %0,4 -> eşik (%1) yanlış eksende aşılır.
            p = start + wrong * (0.015f * w) + dir * (0.004f * w);
            m_Steps.Enqueue((p, UnityEngine.InputSystem.TouchPhase.Moved));
        }
        for (int i = 1; i <= 5; ++i)
        {
            p += dir * (0.03f * w);
            m_Steps.Enqueue((p, UnityEngine.InputSystem.TouchPhase.Moved));
        }
        m_Steps.Enqueue((p, UnityEngine.InputSystem.TouchPhase.Ended));
        m_TouchId++;
        Log($"KAYDIRTEST {why}: yon=({dir.x:F0},{dir.y:F0}) {(tilted ? "EGIK baslangic" : "temiz")}");
    }

    void Update()
    {
        // Sıradaki dokunma adımını gönder (kare başına bir olay; oyun bir sonraki karede okur).
        if (m_Steps.Count > 0)
        {
            var s = m_Steps.Dequeue();
            InputSystem.QueueStateEvent(m_Screen, new TouchState { touchId = m_TouchId, phase = s.phase, position = s.pos });
            return;
        }

        var gm = GameManager.instance;
        if (gm == null || gm.topState == null || gm.topState.GetName() != "Game") return;
        var gs = gm.FindState("Game") as GameState;
        var chr = gs != null && gs.trackManager != null ? gs.trackManager.characterController : null;
        if (chr == null) return;

        // Yalnızca bu test modunda: eğitim boyunca çarpışma yok sayılır (B'nin web botu gibi), yoksa üretici yalnızca
        // yönergelerde hareket ettiği için engeller "geçildi" sayılmaz ve 1-2. seviyelere ulaşılmaz. Girdi algılamayı ve
        // tutorialWaitingForValidation mantığını etkilemez.
        if (gs.trackManager.isTutorial != chr.IsCheatInvincible())
        {
            chr.CheatInvincible(gs.trackManager.isTutorial);
            Log("KAYDIRTEST carpisma yok sayma " + (gs.trackManager.isTutorial ? "ACIK (yalnizca egitim)" : "kapali"));
        }

        if (!gs.trackManager.isTutorial || !gs.trackManager.isMoving && !AudioListener.pause) return;

        bool prompt = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
        int level = chr.currentTutorialLevel;
        if (level > 2) return;
        float now = Time.realtimeSinceStartup; // yönerge sırasında Time.timeScale = 0

        if (prompt && !m_LastPrompt)
        {
            m_PromptLevel = level;
            m_WaitUntil = now + (m_Mode == "egik" ? 0.5f : 1.5f);
            m_CleanPending = m_Mode != "egik";
        }
        m_LastPrompt = prompt;
        if (now < m_WaitUntil) return;

        if (prompt)
        {
            if (m_Mode == "egik" && m_TiltDoneLevel != level)
            {
                m_TiltDoneLevel = level;
                QueueSwipe(CorrectDir(level, chr), true, $"sv={level} yonerge acik, dogru yone EGIK kaydirma");
                m_WaitUntil = now + 1.5f;
                m_CleanPending = true;
                return;
            }
            if (m_CleanPending)
            {
                m_CleanPending = false;
                QueueSwipe(CorrectDir(level, chr), false, $"sv={level} yonerge acik, temiz kaydirma");
                m_WaitUntil = now + 1.5f;
            }
            return;
        }

        // Yönerge kapalı ve koşu sürüyor: "once" modunda bu seviyenin doğru hareketini yönergeden önce bir kez yap.
        if (m_Mode == "once" && m_PreDoneLevel != level && m_PromptLevel != level && gs.trackManager.isMoving)
        {
            m_PreDoneLevel = level;
            QueueSwipe(CorrectDir(level, chr), false, $"sv={level} yonerge ACILMADAN once dogru hareket");
            m_WaitUntil = now + 0.5f;
        }
    }
}
#endif
