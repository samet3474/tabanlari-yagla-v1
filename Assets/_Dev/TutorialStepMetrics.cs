#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Eğitim adımı ölçümleri: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir; DevTimeline
// açıkken (web'de her zaman, masaüstünde "-timeline") çalışır. Satırlar "TL|" önekiyle ve ekrandaki katmanda.
// SALT OKUR: oyunun durumunu yalnızca okur, hiçbir şey yazmaz.
//
//   ADIM egitim-basladi / adim=N engel-yerlesti (karaktere mesafe, şerit) / adim=N yonerge (engele mesafe) /
//        adim=N bekletilen-giris (erken kaydırma tutuldu) / adim=N hareket-basladi (engele mesafe, tepki süresi) /
//        adim=N tamam (yönergeden beri süre, deneme) / adim=N CARPMA ve TEKRAR / bitti (toplam süreler)
//   KARE  her olayda: önceki olaydan beri kare sayısı, ortalama/en uzun kare (ms), >33 ms ve >50 ms kare sayısı,
//         çöp toplama (GC) sayısı farkı, yönetilen bellek (MB), Time.timeScale (yavaş çekim)
//   SICRAMA  50 ms'yi aşan tek kare
//   GECIS  eğitim bittiğinde öndeki parçalar; sonra ilk normal engel ve altın görülünce
using UnityEngine;

public class TutorialStepMetrics : MonoBehaviour
{
    bool m_Active, m_Moving, m_Prompt, m_Started, m_Held, m_WatchAfter, m_Placed;
    int m_Step = -2, m_Attempts = -1, m_Failures;
    float m_LastEventTime, m_PromptTime, m_RunStartTime, m_DoneTime, m_MinTimeScale = 1f;
    float m_FirstObstacleSeen = -1f, m_FirstCoinSeen = -1f;

    int m_Frames, m_Over33, m_Over50, m_GcAtEvent;
    float m_SumDt, m_MaxDt, m_LastFrameTime;

    static readonly string[] k_StepNames = { "SAGA", "SOLA", "YUKARI", "ASAGI" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        bool web = Application.platform == RuntimePlatform.WebGLPlayer;
        if (!(web || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-timeline") >= 0)) return;
        var go = new GameObject("TutorialStepMetrics");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorialStepMetrics>();
    }

    static void Log(string msg)
    {
        if (DevTimeline.instance != null) DevTimeline.instance.Log(msg);
        else Debug.Log("TL| " + msg);
    }

    void ResetStats()
    {
        m_Frames = m_Over33 = m_Over50 = 0;
        m_SumDt = m_MaxDt = 0f;
        m_MinTimeScale = 1f;
        m_GcAtEvent = System.GC.CollectionCount(0);
    }

    void Event(string what)
    {
        float now = Time.realtimeSinceStartup;
        float avg = m_Frames > 0 ? 1000f * m_SumDt / m_Frames : 0f;
        Log($"ADIM {what} (+{now - m_LastEventTime:F2}s)");
        Log($"KARE {m_Frames} kare ort={avg:F1}ms enUzun={1000f * m_MaxDt:F0}ms >33ms={m_Over33} >50ms={m_Over50} GC={System.GC.CollectionCount(0) - m_GcAtEvent} bellek={System.GC.GetTotalMemory(false) / 1048576f:F1}MB enDusukZamanOlcegi={m_MinTimeScale:F2}");
        m_LastEventTime = now;
        ResetStats();
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        float dt = m_LastFrameTime > 0f ? now - m_LastFrameTime : 0f;
        m_LastFrameTime = now;

        var gm = GameManager.instance;
        var gs = gm != null && gm.topState != null && gm.topState.GetName() == "Game" ? gm.FindState("Game") as GameState : null;
        var tm = gs != null ? gs.trackManager : null;
        var chr = tm != null ? tm.characterController : null;
        if (chr == null || chr.character == null) { m_Active = false; m_WatchAfter = false; return; }

        if (m_WatchAfter) WatchAfterTutorial(tm, now);

        if (!gs.isTutorialRun)
        {
            if (m_Active)
            {
                m_Active = false;
                Event($"bitti toplam: kosu basindan {now - m_RunStartTime:F2}s, carpma={m_Failures}");
                LogTransition(tm);
                m_WatchAfter = true;
                m_DoneTime = now;
            }
            return;
        }
        if (!m_Active)
        {
            m_Active = true; m_Step = -2; m_Attempts = -1; m_Moving = false; m_WatchAfter = false; m_Failures = 0;
            m_FirstObstacleSeen = m_FirstCoinSeen = -1f;
            m_LastEventTime = now;
            ResetStats();
            Log("ADIM egitim-basladi");
        }

        if (dt > 0f)
        {
            m_Frames++; m_SumDt += dt; if (dt > m_MaxDt) m_MaxDt = dt;
            if (dt > 0.033f) m_Over33++;
            if (dt > 0.050f) { m_Over50++; Log($"SICRAMA {1000f * dt:F0}ms adim={gs.tutorialStep}"); }
        }
        if (Time.timeScale < m_MinTimeScale) m_MinTimeScale = Time.timeScale;

        if (tm.isMoving && !m_Moving) { m_Moving = true; m_RunStartTime = now; }

        int step = gs.tutorialStep;
        if (step < 0 || step >= k_StepNames.Length) return;
        float dist = gs.GetTutorialObstacleDistance(step);

        if (step != m_Step || gs.tutorialAttempts != m_Attempts)
        {
            if (step != m_Step && m_Step >= 0)
                Event($"adim={m_Step} tamam ({k_StepNames[m_Step]}) yonergeden beri {now - m_PromptTime:F2}s serit={chr.currentLane}");
            else if (step == m_Step)
            {
                m_Failures++;
                Event($"adim={step} CARPMA -> TEKRAR (deneme {gs.tutorialAttempts}) serit={chr.currentLane} zipliyor={chr.isJumping} kayiyor={chr.isSliding}");
            }
            m_Step = step; m_Attempts = gs.tutorialAttempts;
            m_Prompt = m_Started = m_Held = m_Placed = false;
        }

        if (!m_Placed && gs.GetTutorialObstacle(step) != null)
        {
            m_Placed = true;
            var o = gs.GetTutorialObstacle(step);
            float x = chr.transform.InverseTransformPoint(o.transform.position).x;
            Log($"ADIM adim={step} engel-yerlesti {o.name.Replace("(Clone)", "")} karaktere {dist:F1} m, yanal x={x:F2} (serit {(Mathf.RoundToInt(x / tm.laneOffset) + 1)})");
        }
        if (gs.tutorialPromptVisible && !m_Prompt)
        {
            m_Prompt = true; m_PromptTime = now;
            Event($"adim={step} yonerge beklenen={k_StepNames[step]} engele {dist:F1} m serit={chr.currentLane}");
        }
        if (m_Prompt && !m_Held && chr.tutorialHeldMove != CharacterInputController.MoveInput.None)
        {
            m_Held = true;
            Log($"ADIM adim={step} bekletilen-giris engele {dist:F1} m (erken kaydirma, dogru anda yapilacak)");
        }
        if (m_Prompt && !m_Started && gs.tutorialMoveStarted)
        {
            m_Started = true;
            Log($"ADIM adim={step} hareket-basladi engele {dist:F1} m tepki={now - m_PromptTime:F2}s");
        }
    }

    void LogTransition(TrackManager tm)
    {
        float start = -tm.currentSegmentDistance;
        string segs = "";
        for (int i = 0; i < tm.segments.Count; ++i)
        {
            var seg = tm.segments[i];
            segs += $" [{start:F0}m engel={seg.objectRoot.GetComponentsInChildren<Obstacle>(true).Length}]";
            start += seg.worldLength;
        }
        Log($"GECIS hiz={tm.speed:F1} zamanOlcegi={Time.timeScale:F2} parca={tm.segments.Count}{segs}");
    }

    void WatchAfterTutorial(TrackManager tm, float now)
    {
        if (now - m_DoneTime > 30f) { m_WatchAfter = false; return; }
        Transform pivot = tm.characterController.transform;
        if (m_FirstObstacleSeen < 0f)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < tm.segments.Count; ++i)
                foreach (var o in tm.segments[i].objectRoot.GetComponentsInChildren<Obstacle>())
                {
                    float z = pivot.InverseTransformPoint(o.transform.position).z;
                    if (z > 2f && z < nearest) nearest = z;
                }
            if (nearest < float.MaxValue)
            {
                m_FirstObstacleSeen = now;
                Log($"GECIS ilk normal engel: {nearest:F0} m onde, egitim bitisinden {now - m_DoneTime:F2}s sonra (oyuncuya ~{nearest / Mathf.Max(tm.speed, 0.1f):F1}s)");
            }
        }
        if (m_FirstCoinSeen < 0f && FindAnyObjectByType<Coin>() != null)
        {
            m_FirstCoinSeen = now;
            Log($"GECIS ilk altin olustu: egitim bitisinden {now - m_DoneTime:F2}s sonra");
        }
        if (m_FirstObstacleSeen >= 0f && m_FirstCoinSeen >= 0f) m_WatchAfter = false;
    }
}
#endif
