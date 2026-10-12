#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Eğitim adımı ölçümleri: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir; DevTimeline
// açıkken (web'de her zaman, masaüstünde "-timeline") çalışır. Satırlar "TL|" önekiyle ve ekrandaki katmanda.
// SALT OKUR: oyunun durumunu yalnızca okur, hiçbir şey yazmaz.
//
//   ADIM egitim-basladi / adim=N yonerge (beklenen hareket) / adim=N hareket-basladi (tepki süresi) /
//        adim=N tamam (yönergeden beri süre) / bitti (ilk yönergeden ve koşu başından beri toplam süre)
//   KARE  her olayda: önceki olaydan beri kare sayısı, ortalama/en uzun kare (ms), >33 ms ve >50 ms kare sayısı,
//         çöp toplama (GC) sayısı farkı, yönetilen bellek (MB)
//   SICRAMA  50 ms'yi aşan tek kare (o karede segment sayısı değiştiyse yazılır)
//   GECIS  eğitim bittiğinde: öndeki parçalar, ilk engelin mesafesi ve süresi; sonra ilk engel/altın görülünce
using UnityEngine;

public class TutorialStepMetrics : MonoBehaviour
{
    bool m_Active, m_Moving, m_Started, m_WatchAfter;
    int m_Step = -2, m_Segments = -1;
    float m_LastEventTime, m_StepShownTime, m_FirstPromptTime, m_RunStartTime, m_DoneTime;

    // Olaylar arası kare istatistiği (gerçek zaman).
    int m_Frames, m_Over33, m_Over50, m_GcAtEvent;
    float m_SumDt, m_MaxDt, m_LastFrameTime;

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
        m_GcAtEvent = System.GC.CollectionCount(0);
    }

    void Event(string what)
    {
        float now = Time.realtimeSinceStartup;
        float avg = m_Frames > 0 ? 1000f * m_SumDt / m_Frames : 0f;
        Log($"ADIM {what} (+{now - m_LastEventTime:F2}s)");
        Log($"KARE {m_Frames} kare ort={avg:F1}ms enUzun={1000f * m_MaxDt:F0}ms >33ms={m_Over33} >50ms={m_Over50} GC={System.GC.CollectionCount(0) - m_GcAtEvent} bellek={System.GC.GetTotalMemory(false) / 1048576f:F1}MB");
        m_LastEventTime = now;
        ResetStats();
    }

    static readonly string[] k_StepNames = { "SAGA", "SOLA", "YUKARI", "ASAGI" };

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
                Event($"bitti toplam: ilk yonergeden {now - m_FirstPromptTime:F2}s, kosu basindan {now - m_RunStartTime:F2}s");
                LogTransition(tm);
                m_WatchAfter = true;
                m_DoneTime = now;
            }
            return;
        }
        if (!m_Active)
        {
            m_Active = true; m_Step = -2; m_Moving = false; m_WatchAfter = false;
            m_FirstObstacleSeen = m_FirstCoinSeen = -1f;
            m_Segments = tm.segments.Count;
            m_LastEventTime = now;
            ResetStats();
            Log("ADIM egitim-basladi");
        }

        if (dt > 0f)
        {
            m_Frames++; m_SumDt += dt; if (dt > m_MaxDt) m_MaxDt = dt;
            if (dt > 0.033f) m_Over33++;
            if (dt > 0.050f)
            {
                m_Over50++;
                string ctx = tm.segments.Count != m_Segments ? $" segment {m_Segments}->{tm.segments.Count}" : "";
                Log($"SICRAMA {1000f * dt:F0}ms adim={gs.tutorialStep}{ctx}");
            }
        }
        m_Segments = tm.segments.Count;

        if (tm.isMoving && !m_Moving) { m_Moving = true; m_RunStartTime = now; }

        int step = gs.tutorialStep;
        if (step != m_Step)
        {
            if (m_Step >= 0 && m_Step < k_StepNames.Length)
                Event($"adim={m_Step} tamam ({k_StepNames[m_Step]}) yonergeden beri {now - m_StepShownTime:F2}s serit={chr.currentLane}");
            m_Step = step;
            m_Started = false;
            if (step >= 0 && step < k_StepNames.Length)
            {
                m_StepShownTime = now;
                if (step == 0) m_FirstPromptTime = now;
                Event($"adim={step} yonerge beklenen={k_StepNames[step]} izin={chr.tutorialAllowedMove} serit={chr.currentLane} zipliyor={chr.isJumping} kayiyor={chr.isSliding}");
            }
        }

        if (step >= 0 && step < k_StepNames.Length && !m_Started)
        {
            bool started = step == 0 ? chr.currentLane == 2 : step == 1 ? chr.currentLane == 1 : step == 2 ? chr.isJumping : chr.isSliding;
            if (started)
            {
                m_Started = true;
                Log($"ADIM adim={step} hareket-basladi tepki={now - m_StepShownTime:F2}s");
            }
        }
    }

    // Eğitim bittiği an: ileride ilk engel nerede, kaç saniye sonra gelir, öndeki parçaların engel/altın durumu.
    void LogTransition(TrackManager tm)
    {
        float start = -tm.currentSegmentDistance;
        string segs = "";
        for (int i = 0; i < tm.segments.Count; ++i)
        {
            var seg = tm.segments[i];
            int obstacles = seg.objectRoot.GetComponentsInChildren<Obstacle>(true).Length;
            segs += $" [{start:F0}m engel={obstacles}]";
            start += seg.worldLength;
        }
        Log($"GECIS hiz={tm.speed:F1} parca={tm.segments.Count}{segs}");
    }

    float m_FirstObstacleSeen = -1f, m_FirstCoinSeen = -1f;

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
                    if (z > 0f && z < nearest) nearest = z;
                }
            if (nearest < float.MaxValue)
            {
                m_FirstObstacleSeen = now;
                Log($"GECIS ilk engel olustu/goruldu: {nearest:F0} m onde, egitim bitisinden {now - m_DoneTime:F2}s sonra (oyuncuya ~{nearest / Mathf.Max(tm.speed, 0.1f):F1}s)");
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
