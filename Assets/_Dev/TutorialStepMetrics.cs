#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Eğitim adımı ölçümleri: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir; DevTimeline
// açıkken (web'de her zaman, masaüstünde "-timeline") çalışır. Satırlar "TL|" önekiyle ve ekrandaki katmanda.
// SALT OKUR: oyun alanlarını yalnızca okur (yansıma), hiçbir şey yazmaz; fizik sorgusu (CheckSphere) yan etkisizdir.
//
//   ADIM yonerge-acildi  sv, beklenen hareket, engele kalan mesafe (m) ve süre (sn), oyuncunun şeridi, engelli şeritler,
//                        zıplıyor/kayıyor mu, "oyuncu zaten güvende mi" (doğru şeritte ya da engeli aşacak harekette)
//   ADIM dogrulandi      tutorialWaitingForValidation T->F oldu (doğru hareket algılandı)
//   ADIM yonerge-kapandi / gecilen / seviye / bitti   + önceki olaydan beri geçen süre
//   KARE  her olayda: önceki olaydan beri kare sayısı, ortalama/en uzun kare (ms), >33 ms ve >50 ms kare sayısı,
//         çöp toplama (GC) sayısı farkı, yönetilen bellek (MB)
//   SICRAMA  50 ms'yi aşan tek kare (o karede segment sayısı / yönerge değiştiyse yazılır)
using System.Reflection;
using UnityEngine;

public class TutorialStepMetrics : MonoBehaviour
{
    static readonly BindingFlags k_Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly FieldInfo s_GsPrompt = typeof(GameState).GetField("m_TutorialPromptVisible", k_Inst);
    static readonly FieldInfo s_GsCleared = typeof(GameState).GetField("m_TutorialClearedObstacle", k_Inst);
    static readonly FieldInfo s_GsObstacleIndex = typeof(GameState).GetField("m_CurrentSegmentObstacleIndex", k_Inst);
    static readonly FieldInfo s_GsIsTutorial = typeof(GameState).GetField("m_IsTutorial", k_Inst);
    static readonly FieldInfo s_CicLane = typeof(CharacterInputController).GetField("m_CurrentLane", k_Inst);
    static readonly FieldInfo s_CicJumpStart = typeof(CharacterInputController).GetField("m_JumpStart", k_Inst);
    static readonly FieldInfo s_CicSlideStart = typeof(CharacterInputController).GetField("m_SlideStart", k_Inst);
    static readonly FieldInfo s_GsDisplay = typeof(GameState).GetField("m_DisplayTutorial", k_Inst);
    static readonly FieldInfo s_GsCount = typeof(GameState).GetField("m_CountObstacles", k_Inst);

    const int k_ObstacleLayerMask = 1 << 9; // TrackManager.SpawnCoinAndPowerup ile aynı engel katmanı

    bool m_Active, m_Prompt, m_Flag, m_Display, m_Hit;
    int m_Level = -1, m_Cleared = -1, m_Segments = -1;
    float m_LastEventTime;

    // Olaylar arası kare istatistiği (gerçek zaman; yönerge sırasında Time.timeScale = 0).
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

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        float dt = m_LastFrameTime > 0f ? now - m_LastFrameTime : 0f;
        m_LastFrameTime = now;

        var gm = GameManager.instance;
        var gs = gm != null && gm.topState != null && gm.topState.GetName() == "Game" ? gm.FindState("Game") as GameState : null;
        var tm = gs != null ? gs.trackManager : null;
        var chr = tm != null ? tm.characterController : null;
        bool tutorial = chr != null && s_GsIsTutorial != null && (bool)s_GsIsTutorial.GetValue(gs);

        if (!tutorial)
        {
            if (m_Active) { Event("bitti/kapali"); m_Active = false; }
            return;
        }
        if (!m_Active)
        {
            m_Active = true; m_Level = -1; m_Cleared = -1; m_Prompt = false; m_Flag = chr.tutorialWaitingForValidation;
            m_Segments = tm.segments.Count;
            m_LastEventTime = now;
            ResetStats();
            Log("ADIM egitim-basladi");
        }

        // Kare istatistiği
        if (dt > 0f)
        {
            m_Frames++; m_SumDt += dt; if (dt > m_MaxDt) m_MaxDt = dt;
            if (dt > 0.033f) m_Over33++;
            if (dt > 0.050f)
            {
                m_Over50++;
                string ctx = "";
                if (tm.segments.Count != m_Segments) ctx += $" segment {m_Segments}->{tm.segments.Count}";
                bool p = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
                if (p != m_Prompt) ctx += " yonerge-degisti";
                Log($"SICRAMA {1000f * dt:F0}ms sv={chr.currentTutorialLevel}{ctx}");
            }
        }
        m_Segments = tm.segments.Count;

        bool prompt = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
        bool flag = chr.tutorialWaitingForValidation;
        int cleared = s_GsCleared != null ? (int)s_GsCleared.GetValue(gs) : -1;

        if (chr.currentTutorialLevel != m_Level) { m_Level = chr.currentTutorialLevel; Event($"seviye={m_Level}"); }
        if (cleared != m_Cleared) { if (m_Cleared >= 0) Event($"gecilen={cleared}/3"); m_Cleared = cleared; }
        // Eğitimde çarpma (can gitmez, engel "geçilmedi" sayılır): o anki durumla birlikte.
        bool hit = chr.characterCollider.tutorialHitObstacle;
        if (hit && !m_Hit)
        {
            int idx = s_GsObstacleIndex != null ? (int)s_GsObstacleIndex.GetValue(gs) : 0;
            var seg = tm.currentSegment;
            string next = seg != null && idx < seg.obstaclePositions.Length ? $"{seg.obstaclePositions[idx] * seg.worldLength - tm.currentSegmentDistance:F1}m" : "segmentte yok";
            float sl = chr.isSliding && s_CicSlideStart != null ? (tm.worldDistance - (float)s_CicSlideStart.GetValue(chr)) / (chr.slideLength * (1f + tm.speedRatio)) : -1f;
            float jr = chr.isJumping && s_CicJumpStart != null ? (tm.worldDistance - (float)s_CicJumpStart.GetValue(chr)) / (chr.jumpLength * (1f + tm.speedRatio)) : -1f;
            Log($"CARPMA sv={chr.currentTutorialLevel} sayim={(s_GsCount != null ? s_GsCount.GetValue(gs) : "?")} siradakiEngel={next} serit={(s_CicLane != null ? s_CicLane.GetValue(chr) : "?")} kayma={sl:F2} ziplama={jr:F2} kutuY={chr.characterCollider.transform.localPosition.y:F2}");
        }
        m_Hit = hit;

        // Yönerge açılmadan "gösterilecek" bayrağı düştüyse adım atlandı (oyuncu hareketi zaten yapmış sayıldı).
        bool display = s_GsDisplay != null && (bool)s_GsDisplay.GetValue(gs);
        if (m_Display && !display && !prompt && !m_Prompt) Event("yonerge-atlandi (oyuncu zaten yapmis sayildi) " + Situation(gs, tm, chr));
        m_Display = display;
        if (prompt && !m_Prompt) Event("yonerge-acildi " + Situation(gs, tm, chr));
        if (m_Prompt && flag != m_Flag && !flag) Event("dogrulandi");
        if (!prompt && m_Prompt) Event("yonerge-kapandi");
        m_Prompt = prompt; m_Flag = flag;
    }

    // Yönerge açıldığı andaki durum: engele kalan mesafe/süre, oyuncunun şeridi ve engelli şeritler.
    string Situation(GameState gs, TrackManager tm, CharacterInputController chr)
    {
        var seg = tm.currentSegment;
        int idx = s_GsObstacleIndex != null ? (int)s_GsObstacleIndex.GetValue(gs) : 0;
        if (seg == null || idx >= seg.obstaclePositions.Length) return "(engel bilgisi yok)";

        float t = seg.obstaclePositions[idx];
        float dist = t * seg.worldLength - tm.currentSegmentDistance;
        float secs = tm.speed > 0.01f ? dist / tm.speed : -1f;

        seg.GetPointAt(t, out Vector3 pos, out Quaternion rot);
        string blocked = "";
        for (int lane = 0; lane < 3; ++lane)
        {
            Vector3 p = pos + (lane - 1) * tm.laneOffset * (rot * Vector3.right) + Vector3.up * 0.5f;
            blocked += Physics.CheckSphere(p, 0.4f, k_ObstacleLayerMask) ? "X" : "_";
        }
        int myLane = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : -1;

        string move = chr.currentTutorialLevel == 0 ? "SERIT" : chr.currentTutorialLevel == 1 ? "ZIPLA" : chr.currentTutorialLevel == 2 ? "KAY" : "BITIS";
        string safe = "?";
        if (chr.currentTutorialLevel == 0 && myLane >= 0 && myLane < 3)
            safe = blocked[myLane] == '_' ? "EVET (oyuncu zaten bos seritte)" : "hayir";
        else if (chr.currentTutorialLevel == 1)
        {
            float j = chr.jumpLength * (1f + tm.speedRatio);
            float r = chr.isJumping && s_CicJumpStart != null ? (tm.worldDistance + dist - (float)s_CicJumpStart.GetValue(chr)) / j : -1f;
            safe = chr.isJumping ? $"zipliyor (engelde atlayis orani={r:F2})" : "hayir";
        }
        else if (chr.currentTutorialLevel == 2)
        {
            float s = chr.slideLength * (1f + tm.speedRatio);
            float r = chr.isSliding && s_CicSlideStart != null ? (tm.worldDistance + dist - (float)s_CicSlideStart.GetValue(chr)) / s : -1f;
            safe = chr.isSliding ? $"kayiyor (engelde kayma orani={r:F2})" : "hayir";
        }

        return $"beklenen={move} mesafe={dist:F1}m sure={secs:F2}s hiz={tm.speed:F1} segment={seg.worldLength:F1}m oyuncuSerit={myLane} engel=[{blocked}] guvende={safe}";
    }
}
#endif
