#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Eğitim girdi teşhisi için yapay kaydırma üretir: yalnızca TY_DEVTOOLS sembollü WEB test derlemesinde ve adreste
// "?kaydirtest=..." varken çalışır (oyunun dokunmatik kolu yalnızca web/mobil derlemede derleniyor).
// Sanal bir Touchscreen aygıtına gerçek dokunma olayları gönderir; oyun bunları gerçek parmak gibi okur
// (CharacterInputController dokunmatik kolu -> HandleMoveInput -> GameState). Oyun koduna ve alanlarına dokunmaz.
//
// Senaryolar (eğitimin her adımında: sağ, sol, yukarı, aşağı):
//   kaydirtest=dogru   Yönerge göründükten 0,6 sn sonra temiz, doğru kaydırma.
//   kaydirtest=yanlis  Önce YANLIŞ yöne kaydırır (adım ilerlememeli, karakter yerinde kalmalı), 1 sn sonra doğru yöne.
//   kaydirtest=egik    Doğru yöne ama ilk ~%1,5'lik kısmı öbür eksene kayan kaydırma (parmağın başlangıç sapması),
//                      1,5 sn sonra adım geçmediyse temiz kaydırma.
//   kaydirtest=hizli   Kalan doğru kaydırmaları yönergeyi beklemeden art arda (~7 kare arayla) yapar; erken gelenler
//                      yok sayılmalı, hiçbir adım atlanmamalı. Sonra kalan adımları "dogru" gibi tamamlar.
// Sonuçları TutorialInputProbe ("KAYDIR") ve TutorialStepMetrics ("ADIM") yazar.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class TutorialSwipeInjector : MonoBehaviour
{
    string m_Mode;
    Touchscreen m_Screen;
    int m_TouchId = 100;

    // Sıradaki kaydırmaların kare kare dokunma olayları (phase None: o kare dokunma yok).
    readonly Queue<(Vector2 pos, UnityEngine.InputSystem.TouchPhase phase)> m_Steps = new Queue<(Vector2, UnityEngine.InputSystem.TouchPhase)>();
    float m_WaitUntil;
    int m_SeenStep = -2;
    int m_Phase;          // adım içinde: 0 ilk kaydırma bekliyor, 1 ikinci kaydırma bekliyor, 2 bitti
    bool m_BurstDone;

    static readonly Vector2[] k_StepDirs = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

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

    static Vector2 WrongDir(Vector2 dir) => dir == Vector2.up ? Vector2.down : dir == Vector2.right ? Vector2.left : Vector2.up;

    void QueueSwipe(Vector2 dir, bool tilted, string why)
    {
        float w = Screen.width;
        Vector2 start = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
        Vector2 wrong = new Vector2(Mathf.Abs(dir.y), Mathf.Abs(dir.x)); // öbür eksen
        if (dir.x < 0 || dir.y < 0) wrong = -wrong;

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
        Log($"KAYDIRTEST {why}: yon=({dir.x:F0},{dir.y:F0}) {(tilted ? "EGIK baslangic" : "temiz")}");
    }

    void Update()
    {
        // Sıradaki dokunma olayını gönder (kare başına bir olay; oyun bir sonraki karede okur). Her kaydırma ayrı dokunma.
        if (m_Steps.Count > 0)
        {
            var s = m_Steps.Dequeue();
            if (s.phase == UnityEngine.InputSystem.TouchPhase.None) return;
            if (s.phase == UnityEngine.InputSystem.TouchPhase.Began) m_TouchId++;
            InputSystem.QueueStateEvent(m_Screen, new TouchState { touchId = m_TouchId, phase = s.phase, position = s.pos });
            return;
        }

        var gm = GameManager.instance;
        if (gm == null || gm.topState == null || gm.topState.GetName() != "Game") return;
        var gs = gm.FindState("Game") as GameState;
        if (gs == null || !gs.isTutorialRun || !gs.trackManager.isMoving) return;

        int step = gs.tutorialStep;
        if (step < 0 || step >= gs.tutorialStepCount) return;
        float now = Time.realtimeSinceStartup;

        if (m_Mode == "hizli" && !m_BurstDone)
        {
            m_BurstDone = true;
            for (int i = step; i < k_StepDirs.Length; ++i)
            {
                QueueSwipe(k_StepDirs[i], false, $"adim={step} art arda kaydirma {i + 1}/{k_StepDirs.Length}");
                for (int k = 0; k < 7; ++k) m_Steps.Enqueue((Vector2.zero, UnityEngine.InputSystem.TouchPhase.None));
            }
            m_SeenStep = -2;
            return;
        }

        if (step != m_SeenStep)
        {
            m_SeenStep = step;
            m_Phase = 0;
            m_WaitUntil = now + 0.6f;
        }
        if (now < m_WaitUntil || m_Phase >= 2) return;

        Vector2 dir = k_StepDirs[step];
        if (m_Phase == 0 && m_Mode == "yanlis")
        {
            QueueSwipe(WrongDir(dir), false, $"adim={step} YANLIS yone kaydirma (adim ilerlememeli)");
            m_Phase = 1; m_WaitUntil = now + 1.0f;
        }
        else if (m_Phase == 0 && m_Mode == "egik")
        {
            QueueSwipe(dir, true, $"adim={step} dogru yone EGIK kaydirma");
            m_Phase = 1; m_WaitUntil = now + 1.5f;
        }
        else
        {
            QueueSwipe(dir, false, $"adim={step} dogru yone temiz kaydirma");
            m_Phase = 2;
        }
    }
}
#endif
