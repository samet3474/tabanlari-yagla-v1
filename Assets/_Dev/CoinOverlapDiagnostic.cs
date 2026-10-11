#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Faz 0.2 teşhis aracı: yalnızca geliştirme derlemesinde ve "-coinDiag" argümanıyla çalışır. Commit edilmez.
// Argümanlar: -coinDiag -diagSeconds <s> -diagSeed <n> -diagPhases measure|full
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

public class CoinOverlapDiagnostic : MonoBehaviour
{
    const int k_ObstacleMask = 1 << 9;
    const float k_Radius = 0.4f;

    float m_Seconds = 240f;
    int m_Seed = 12345;
    bool m_Full;

    string m_Phase = "boot";
    readonly Dictionary<string, int> m_Errors = new Dictionary<string, int>();
    int m_ErrorTotal;
    readonly Dictionary<string, int> m_PhaseErrors = new Dictionary<string, int>();

    // Ölçüm sayaçları
    int m_Segments, m_SegmentsNoPickups, m_SegmentsReleasedBeforeMeasure;
    int m_Coins, m_CoinsStaticOverlap, m_CoinsMovingOverlap, m_CoinsUnderHigh, m_CoinsNearLow;
    int m_Powerups, m_Premiums, m_PickupsStaticOverlap;
    readonly Dictionary<string, int> m_StaticOverlapByType = new Dictionary<string, int>();
    readonly List<int> m_LatencyFrames = new List<int>();
    readonly List<float> m_LatencySeconds = new List<float>();
    int m_MinAheadIndex = int.MaxValue;
    bool m_Measuring;
    int m_Frames; float m_FrameTimeSum, m_FrameTimeMax; int m_Hitches50;

    TrackManager m_TM;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-coinDiag") < 0) return;
        var go = new GameObject("CoinOverlapDiagnostic");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<CoinOverlapDiagnostic>();
        for (int i = 0; i < args.Length - 1; ++i)
        {
            if (args[i] == "-diagSeconds") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d.m_Seconds);
            if (args[i] == "-diagSeed") int.TryParse(args[i + 1], out d.m_Seed);
            if (args[i] == "-diagPhases") d.m_Full = args[i + 1] == "full";
        }
    }

    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }

    void OnLog(string cond, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (cond.StartsWith("DIAG|")) return;
        string key = m_Phase + " :: " + type + " :: " + (cond.Length > 160 ? cond.Substring(0, 160) : cond);
        m_Errors.TryGetValue(key, out int c); m_Errors[key] = c + 1;
        m_PhaseErrors.TryGetValue(m_Phase, out int p); m_PhaseErrors[m_Phase] = p + 1;
        m_ErrorTotal++;
    }

    static void Out(string s) { Debug.Log("DIAG| " + s); }

    void Update()
    {
        if (!m_Measuring) return;
        float dt = Time.unscaledDeltaTime;
        m_Frames++; m_FrameTimeSum += dt; if (dt > m_FrameTimeMax) m_FrameTimeMax = dt; if (dt > 0.05f) m_Hitches50++;
    }

    IEnumerator Start()
    {
        Out($"basladi seed={m_Seed} sure={m_Seconds}s full={m_Full}");
        if (SceneManager.GetActiveScene().name != "Main")
        {
            yield return null;
            SceneManager.LoadScene("Main");
        }

        float wait = 0;
        while (!(GameManager.instance != null && GameManager.instance.topState is LoadoutState && CharacterDatabase.loaded && ThemeDatabase.loaded) && wait < 60f)
        {
            wait += Time.unscaledDeltaTime; yield return null;
        }
        if (wait >= 60f) { Out("HATA: Loadout hazir olmadi"); Finish(); yield break; }
        yield return new WaitForSecondsRealtime(1f);

        var gs = GameManager.instance.FindState("Game") as GameState;
        m_TM = gs.trackManager;

        m_Phase = "A-olcum";
        yield return StartRun();
        m_Measuring = true;
        float t = 0;
        while (t < m_Seconds) { t += Time.unscaledDeltaTime; yield return null; }
        m_Measuring = false;
        yield return new WaitForSecondsRealtime(3f); // bekleyen olcumler bitsin
        ReportMeasure();

        if (m_Full)
        {
            m_Phase = "B-yukleme-hatasi";
            yield return FailureInjection();
            m_Phase = "C-parca-serbest";
            yield return ReleaseMidSpawn();
            m_Phase = "D-menuye-donus";
            yield return QuitAndRestart();
        }

        ReportErrors();
        Finish();
    }

    IEnumerator StartRun()
    {
        PlayerData.instance.tutorialDone = true;
        m_TM.trackSeed = m_Seed;
        m_TM.invincible = true;
        m_TM.newSegmentCreated = OnSegmentCreated;
        ((LoadoutState)GameManager.instance.topState).StartGame();
        float w = 0;
        while (!m_TM.isMoving && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
        Out($"kosu basladi (bekleme {w:F1}s)");
    }

    void OnSegmentCreated(TrackSegment seg)
    {
        if (m_Measuring || m_Phase == "A-olcum") StartCoroutine(MeasureSegment(seg));
    }

    static bool HasPickups(TrackSegment seg)
    {
        if (seg.collectibleTransform.childCount > 0) return true;
        for (int i = 0; i < seg.transform.childCount; ++i)
        {
            var c = seg.transform.GetChild(i);
            if (c.GetComponent<Consumable>() != null || c.GetComponent<Coin>() != null) return true;
        }
        return false;
    }

    IEnumerator MeasureSegment(TrackSegment seg)
    {
        int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
        bool appeared = false;
        while (Time.realtimeSinceStartup - t0 < 8f)
        {
            if (seg == null) { m_SegmentsReleasedBeforeMeasure++; yield break; }
            if (HasPickups(seg))
            {
                appeared = true;
                m_LatencyFrames.Add(Time.frameCount - f0);
                m_LatencySeconds.Add(Time.realtimeSinceStartup - t0);
                int idx = m_TM.segments.IndexOf(seg);
                if (idx >= 0 && idx < m_MinAheadIndex) m_MinAheadIndex = idx;
                break;
            }
            yield return null;
        }
        if (!appeared) { m_SegmentsNoPickups++; yield break; }

        yield return new WaitForSecondsRealtime(1.5f); // engellerin tamamen olusmasi icin
        if (seg == null) { m_SegmentsReleasedBeforeMeasure++; yield break; }
        m_Segments++;

        var highs = new List<Transform>(); var lows = new List<Transform>();
        foreach (Transform o in seg.objectRoot)
        {
            if (o.name.Contains("HighBarrier")) highs.Add(o);
            else if (o.name.Contains("LowBarrier")) lows.Add(o);
        }

        foreach (Transform coin in seg.collectibleTransform)
        {
            if (!coin.gameObject.activeInHierarchy) continue;
            m_Coins++;
            ClassifyOverlap(coin.position, false);
            foreach (var h in highs) { var l = h.InverseTransformPoint(coin.position); if (Mathf.Abs(l.z) < 0.6f && Mathf.Abs(l.x) < 2.6f) { m_CoinsUnderHigh++; break; } }
            foreach (var lo in lows) { var l = lo.InverseTransformPoint(coin.position); if (Mathf.Abs(l.z) < 0.6f && Mathf.Abs(l.x) < 2.6f) { m_CoinsNearLow++; break; } }
        }

        for (int i = 0; i < seg.transform.childCount; ++i)
        {
            var c = seg.transform.GetChild(i);
            if (c.GetComponent<Consumable>() != null) { m_Powerups++; if (ClassifyOverlap(c.position, true)) m_PickupsStaticOverlap++; }
            else { var coin = c.GetComponent<Coin>(); if (coin != null && coin.isPremium) { m_Premiums++; if (ClassifyOverlap(c.position, true)) m_PickupsStaticOverlap++; } }
        }
    }

    // true: sabit engelle cakisiyor
    bool ClassifyOverlap(Vector3 pos, bool pickupOnly)
    {
        var hits = Physics.OverlapSphere(pos, k_Radius, k_ObstacleMask, QueryTriggerInteraction.Collide);
        bool st = false, mv = false; string stType = null;
        foreach (var h in hits)
        {
            var ob = h.GetComponentInParent<Obstacle>();
            if (ob is PatrollingObstacle || ob is Missile) mv = true;
            else { st = true; stType = ob != null ? ob.name.Replace("(Clone)", "") : h.name; }
        }
        if (!pickupOnly)
        {
            if (st) { m_CoinsStaticOverlap++; m_StaticOverlapByType.TryGetValue(stType, out int n); m_StaticOverlapByType[stType] = n + 1; }
            else if (mv) m_CoinsMovingOverlap++;
        }
        return st;
    }

    void ReportMeasure()
    {
        var sb = new StringBuilder();
        foreach (var kv in m_StaticOverlapByType) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
        m_LatencyFrames.Sort(); m_LatencySeconds.Sort();
        string Pct(List<int> l, float p) => l.Count == 0 ? "-" : l[Mathf.Clamp((int)(l.Count * p), 0, l.Count - 1)].ToString();
        string PctF(List<float> l, float p) => l.Count == 0 ? "-" : l[Mathf.Clamp((int)(l.Count * p), 0, l.Count - 1)].ToString("F3");
        Out($"OLCUM mesafe={m_TM.worldDistance:F0}m olculen_parca={m_Segments} toplamasiz_parca={m_SegmentsNoPickups} olcum_oncesi_serbest={m_SegmentsReleasedBeforeMeasure}");
        Out($"OLCUM altin={m_Coins} sabit_engel_cakisma={m_CoinsStaticOverlap} hareketli_engel_cakisma={m_CoinsMovingOverlap} oran={(m_Coins == 0 ? 0 : 100f * m_CoinsStaticOverlap / m_Coins):F2}% tur: {sb}");
        Out($"OLCUM yuksek_bariyer_alti_altin={m_CoinsUnderHigh} alcak_bariyer_yaninda_altin={m_CoinsNearLow}");
        Out($"OLCUM guclendirme={m_Powerups} premium={m_Premiums} sabit_engelle_cakisan_toplanabilir={m_PickupsStaticOverlap}");
        Out($"OLCUM altin_gecikme_kare: min={Pct(m_LatencyFrames, 0)} medyan={Pct(m_LatencyFrames, 0.5f)} p95={Pct(m_LatencyFrames, 0.95f)} max={Pct(m_LatencyFrames, 1f)} | saniye: medyan={PctF(m_LatencySeconds, 0.5f)} max={PctF(m_LatencySeconds, 1f)} | en_yakin_parca_indeksi={(m_MinAheadIndex == int.MaxValue ? -1 : m_MinAheadIndex)}");
        Out($"OLCUM kare: adet={m_Frames} ort={(m_Frames == 0 ? 0 : 1000f * m_FrameTimeSum / m_Frames):F2}ms max={1000f * m_FrameTimeMax:F1}ms >50ms={m_Hitches50}");
    }

    static void ClearPickups(TrackSegment seg)
    {
        while (seg.collectibleTransform.childCount > 0)
        {
            var t = seg.collectibleTransform.GetChild(0);
            t.SetParent(null);
            Coin.coinPool.Free(t.gameObject);
        }
    }

    TrackSegment PickSegmentWithObstacles(int fromEnd)
    {
        var list = m_TM.segments;
        for (int i = list.Count - 1 - fromEnd; i >= 2; --i)
            if (list[i] != null && list[i].obstaclePositions.Length > 0 && list[i].possibleObstacles.Length > 0) return list[i];
        return null;
    }

    IEnumerator FailureInjection()
    {
        yield return new WaitForSecondsRealtime(2f);
        var seg = PickSegmentWithObstacles(0);
        if (seg == null) { Out("B ATLANDI: uygun parca yok"); yield break; }
        ClearPickups(seg);
        int before = m_ErrorTotal;
        seg.possibleObstacles = new[] { new AssetReference("0123456789abcdef0123456789abcdef") };
        int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
        m_TM.SpawnObstacle(seg);
        bool ok = false;
        while (Time.realtimeSinceStartup - t0 < 10f)
        {
            if (seg == null) break;
            if (HasPickups(seg)) { ok = true; break; }
            yield return null;
        }
        Out($"B yukleme_hatasi: altin_uretildi={ok} gecikme={Time.frameCount - f0}kare/{Time.realtimeSinceStartup - t0:F2}s engel_pozisyonu={seg?.obstaclePositions.Length} yeni_hata_logu={m_ErrorTotal - before}");
    }

    IEnumerator ReleaseMidSpawn()
    {
        yield return new WaitForSecondsRealtime(2f);
        var seg = PickSegmentWithObstacles(0);
        if (seg == null) { Out("C ATLANDI: uygun parca yok"); yield break; }
        ClearPickups(seg);
        int before = m_ErrorTotal;
        m_TM.segments.Remove(seg);
        m_TM.SpawnObstacle(seg);
        Addressables.ReleaseInstance(seg.gameObject);
        yield return new WaitForSecondsRealtime(4f);
        int orphans = 0;
        foreach (var c in FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (c.transform.parent == null) orphans++;
        Out($"C parca_serbest: yeni_hata_logu={m_ErrorTotal - before} sahipsiz_aktif_altin={orphans} oyun_devam={m_TM.isMoving}");
    }

    IEnumerator QuitAndRestart()
    {
        int before = m_ErrorTotal;
        var gs = GameManager.instance.topState as GameState;
        if (gs == null) { Out("D ATLANDI: GameState aktif degil"); yield break; }
        gs.QuitToLoadout(); // kosu ortasinda, parcalar uretilirken
        yield return new WaitForSecondsRealtime(3f);
        bool inLoadout = GameManager.instance.topState is LoadoutState;
        m_Phase = "D-yeniden-baslat";
        yield return StartRun();
        yield return new WaitForSecondsRealtime(20f);
        var gs2 = GameManager.instance.topState as GameState;
        if (gs2 != null) gs2.QuitToLoadout();
        yield return new WaitForSecondsRealtime(3f);
        Out($"D menuye_donus: loadouta_dondu={inLoadout} ikinci_donus_loadout={(GameManager.instance.topState is LoadoutState)} yeni_hata_logu={m_ErrorTotal - before}");
    }

    void ReportErrors()
    {
        Out($"HATALAR toplam={m_ErrorTotal}");
        foreach (var kv in m_PhaseErrors) Out($"HATALAR faz {kv.Key}: {kv.Value}");
        foreach (var kv in m_Errors) Out($"HATA x{kv.Value} {kv.Key}");
    }

    void Finish()
    {
        Out("bitti");
        Application.Quit();
    }
}
#endif
