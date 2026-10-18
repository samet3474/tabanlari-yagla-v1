#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Test aracı: yalnızca geliştirme derlemesinde ve "-istiklalTest" argümanıyla çalışır; yayın derlemesine girmez.
// İstiklal üretim parçasını (Istiklal01, Addressables "Istanbul-Zones") gerçek koşuda dener. Day temasının bölgeleri
// YALNIZCA bu sürecin belleğinde bu parçayı kullanacak şekilde ayarlanır (dosyalar ve tema değişmez); engeller ve altınlar
// oyunun kendi akışıyla yerleşir. Ölçer: parça birleşimi, engel yerleşimi, altın çakışması, simit sayısı, üçgen/çizici.
// Argümanlar: -istiklalTest [-istiklalShots <klasör>] [-diagSeed <n>]
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class IstiklalTest : MonoBehaviour
{
    const string k_SegmentGuid = "db82ae717e8d4dc4ba51769c4134177a"; // Istiklal01.prefab (üretim; simitsiz)
    const string k_SegmentAddress = "Istiklal01";
    const string k_Simit = "SimitTezgahi";
    const int k_ObstacleMask = 1 << 9;

    string m_ShotDir;
    int m_Seed = 4242;
    int m_Pass, m_Fail, m_Errors;
    TrackManager m_TM;
    CharacterInputController m_CC;
    readonly List<AsyncOperationHandle<GameObject>> m_Held = new List<AsyncOperationHandle<GameObject>>();

    int m_Segments, m_OtherSegments, m_Joins, m_Coins, m_CoinsInObstacle, m_Simits, m_SimitMaxPerSeg, m_AdjacentBlocked;
    float m_MaxGap, m_MaxAngle;
    int m_TriMax, m_TriMaxDecor, m_RendMax; long m_TriSum;
    readonly Dictionary<string, int> m_ObstacleTypes = new Dictionary<string, int>();
    readonly Dictionary<string, int> m_CoinOverlapTypes = new Dictionary<string, int>();
    readonly HashSet<int> m_Seen = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-istiklalTest") < 0) return;
        var go = new GameObject("IstiklalTest");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<IstiklalTest>();
        for (int i = 0; i < args.Length - 1; ++i)
        {
            if (args[i] == "-istiklalShots") t.m_ShotDir = args[i + 1];
            if (args[i] == "-diagSeed") int.TryParse(args[i + 1], out t.m_Seed);
        }
    }

    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }
    void OnLog(string cond, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { m_Errors++; Out("HATA_LOGU " + cond); }
    }

    static void Out(string s) { Debug.Log("ISTIKLAL| " + s); }
    void Check(bool ok, string what) { if (ok) m_Pass++; else m_Fail++; Out((ok ? "GECTI " : "KALDI ") + what); }
    static int Tris(Mesh m) { if (m == null) return 0; long n = 0; for (int i = 0; i < m.subMeshCount; ++i) n += m.GetIndexCount(i); return (int)(n / 3); }
    static string TypeName(Transform o) => o.name.Replace("(Clone)", "").Trim();

    IEnumerator Start()
    {
        Out($"basladi seed={m_Seed}");
        float wait = 0;
        while (!(GameManager.instance != null && GameManager.instance.topState is LoadoutState && CharacterDatabase.loaded && ThemeDatabase.loaded) && wait < 60f)
        { wait += Time.unscaledDeltaTime; yield return null; }
        if (wait >= 60f) { Out("HATA: Loadout hazir olmadi"); Finish(); yield break; }
        yield return new WaitForSecondsRealtime(1f);

        var gs = GameManager.instance.FindState("Game") as GameState;
        m_TM = gs.trackManager;
        m_CC = m_TM.characterController;

        var probe = Addressables.LoadAssetAsync<GameObject>(k_SegmentAddress);
        yield return probe;
        var prefab = probe.Status == AsyncOperationStatus.Succeeded ? probe.Result : null;
        var segPrefab = prefab != null ? prefab.GetComponent<TrackSegment>() : null;
        Check(segPrefab != null, "Istiklal01 Addressables'tan yuklendi (TrackSegment)");
        if (segPrefab == null) { Finish(); yield break; }
        m_Held.Add(probe);
        int decor = 0, rend = 0;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true)) if (mf.GetComponent<MeshRenderer>() != null) { decor += Tris(mf.sharedMesh); rend++; }
        Out($"OLCU parca_engelsiz ucgen={decor} cizici={rend} engel_noktasi=[{string.Join(",", segPrefab.obstaclePositions)}] olasi_engel={segPrefab.possibleObstacles.Length}");
        foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var m in r.sharedMaterials)
                Out($"  malzeme {r.name}: {m.name} shader={m.shader.name} doku={(m.mainTexture != null ? m.mainTexture.width + "x" + m.mainTexture.height : "yok")}");

        // Day temasının bütün bölgeleri bu süreçte yalnız İstiklal parçasını kullansın (bellekte).
        var day = ThemeDatabase.GetThemeData("Day");
        var segRef = new AssetReference(k_SegmentGuid);
        for (int i = 0; i < day.zones.Length; ++i) day.zones[i].prefabList = new[] { segRef };
        Out($"bolge ayari: {day.zones.Length} bolge yalniz Istiklal01 (bellekte)");

        PlayerData.instance.tutorialDone = true;
        m_TM.trackSeed = m_Seed;
        m_TM.invincible = true;
        ((LoadoutState)GameManager.instance.topState).StartGame();
        m_TM.newSegmentCreated = s => StartCoroutine(Measure(s));
        float w = 0;
        while (!m_TM.isMoving && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
        Out($"kosu basladi (bekleme {w:F1}s)");

        // A) Ölçüm (ölümsüz) + görüntüler.
        float t = 0; bool s0 = false, s1 = false, s2 = false, s3 = false;
        while (t < 60f)
        {
            t += Time.unscaledDeltaTime;
            if (m_ShotDir != null)
            {
                if (!s0 && t > 1.5f) { s0 = true; yield return Shot("sokak"); }
                if (ObstacleAhead(out float d, out Transform o, 0.75f, null))
                {
                    if (!s1 && d > 13f && d < 17f) { s1 = true; yield return Shot("engel-uzak"); }
                    else if (s1 && !s2 && d > 4.5f && d < 7f) { s2 = true; yield return Shot("engel-yakin"); }
                }
                if (!s3 && ObstacleAhead(out float ds, out _, 3.5f, k_Simit) && ds > 6f && ds < 10f) { s3 = true; yield return Shot("simit"); }
            }
            yield return null;
        }
        yield return new WaitForSecondsRealtime(2.5f);
        Report();

        // B-D) Sürüşler: bilgi amaçlı (karışık engel: alçak bariyer zıplanır, çöp kutusu zıplanamaz; otomatik pilotun
        // bilinen sınırları var, bkz. SimitTest). Yalnız "dokunmadan" çarpışmanın çalıştığını denetler.
        m_CC.CheatInvincible(false);
        yield return Drive("ziplama", 40f, true, false);
        yield return Drive("serit-degistirme", 30f, false, true);
        yield return Drive("dokunmadan", 25f, false, false);
        Finish();
    }

    IEnumerator Measure(TrackSegment seg)
    {
        yield return new WaitForSecondsRealtime(2f);
        if (seg == null || !m_Seen.Add(seg.GetInstanceID())) yield break;
        if (!seg.name.Contains(k_SegmentAddress)) { m_OtherSegments++; yield break; }
        m_Segments++;

        int idx = m_TM.segments.IndexOf(seg);
        if (idx > 0 && m_TM.segments[idx - 1] != null)
        {
            m_TM.segments[idx - 1].GetPointAt(1f, out Vector3 exitP, out Quaternion exitR);
            seg.GetPointAt(0f, out Vector3 entryP, out Quaternion entryR);
            m_Joins++;
            m_MaxGap = Mathf.Max(m_MaxGap, Vector3.Distance(exitP, entryP));
            m_MaxAngle = Mathf.Max(m_MaxAngle, Quaternion.Angle(exitR, entryR));
        }

        int tris = 0, decor = 0, rend = 0;
        foreach (var mf in seg.GetComponentsInChildren<MeshFilter>(false))
        {
            var r = mf.GetComponent<Renderer>(); if (r == null || !r.enabled || mf.GetComponentInParent<Coin>() != null) continue;
            int n = Tris(mf.sharedMesh); tris += n; rend++;
            if (!mf.transform.IsChildOf(seg.objectRoot)) decor += n;
        }
        m_TriSum += tris; m_TriMax = Mathf.Max(m_TriMax, tris); m_TriMaxDecor = Mathf.Max(m_TriMaxDecor, decor); m_RendMax = Mathf.Max(m_RendMax, rend);

        int simits = 0;
        var half = new Vector3(0.288f, 0.6f, 0.466f); // karakterin çarpışma kutusu
        foreach (Transform o in seg.objectRoot)
        {
            if (o.GetComponent<Collider>() == null) continue; // Collectibles
            string type = TypeName(o);
            m_ObstacleTypes.TryGetValue(type, out int c); m_ObstacleTypes[type] = c + 1;
            if (!type.Contains(k_Simit)) continue;
            simits++; m_Simits++;
            var col = o.GetComponent<Collider>();
            foreach (int side in new[] { -1, 1 })
                foreach (var h in Physics.OverlapBox(o.position + o.right * side * m_TM.laneOffset + Vector3.up * 0.7f, half, o.rotation, k_ObstacleMask, QueryTriggerInteraction.Collide))
                    if (h == col) { m_AdjacentBlocked++; break; }
        }
        m_SimitMaxPerSeg = Mathf.Max(m_SimitMaxPerSeg, simits);

        foreach (Transform coin in seg.collectibleTransform)
        {
            if (!coin.gameObject.activeInHierarchy) continue;
            m_Coins++;
            var hits = Physics.OverlapSphere(coin.position, 0.4f, k_ObstacleMask, QueryTriggerInteraction.Collide);
            if (hits.Length == 0) continue;
            m_CoinsInObstacle++;
            string type = TypeName(hits[0].transform);
            m_CoinOverlapTypes.TryGetValue(type, out int c); m_CoinOverlapTypes[type] = c + 1;
        }
    }

    void Report()
    {
        Out($"OLCUM parca={m_Segments} baska_parca={m_OtherSegments} birlesim={m_Joins} en_buyuk_bosluk={m_MaxGap:F4}m en_buyuk_aci={m_MaxAngle:F2}deg");
        Out($"OLCUM ucgen_parca ort={(m_Segments == 0 ? 0 : m_TriSum / m_Segments)} max={m_TriMax} max_dekor={m_TriMaxDecor} cizici_max={m_RendMax}");
        Out($"OLCUM engeller: {string.Join(" ", m_ObstacleTypes.Select(kv => kv.Key + "=" + kv.Value))}");
        Out($"OLCUM altin={m_Coins} engelle_cakisan={m_CoinsInObstacle} {string.Join(" ", m_CoinOverlapTypes.Select(kv => kv.Key + "=" + kv.Value))}");
        Check(m_Segments >= 20 && m_OtherSegments == 0, $"kosu yalniz Istiklal parcasindan olustu ({m_Segments} parca, baska={m_OtherSegments})");
        Check(m_Joins >= 10 && m_MaxGap < 0.01f && m_MaxAngle < 0.5f, $"parca girisi onceki parcanin cikisina oturuyor (bosluk={m_MaxGap:F4}m aci={m_MaxAngle:F2})");
        Check(m_ObstacleTypes.Count >= 3, $"engeller yerlesiyor ({m_ObstacleTypes.Count} tur)");
        Check(!m_ObstacleTypes.ContainsKey("ObstacleSimitTezgahi"), "uretim parcasinda simit tezgahi yok (Meshy §2.4)");
        Check(m_SimitMaxPerSeg <= 2, $"parca basina en fazla 2 simit ({m_SimitMaxPerSeg})");
        Check(m_AdjacentBlocked == 0, $"simit komsu seridi kapatmiyor (degen={m_AdjacentBlocked}/{m_Simits * 2})");
        Check(m_Coins > 50 && m_CoinsInObstacle == 0, $"altinlar engellerin icine dogmuyor ({m_CoinsInObstacle}/{m_Coins})");
        Check(m_TriMax <= 15000, $"parca basina ucgen hedefte (max={m_TriMax}, engeller dahil)");
    }

    bool ObstacleAhead(out float dist, out Transform hit, float laneTolerance, string filter)
    {
        dist = float.MaxValue; hit = null;
        Transform ct = m_CC.transform;
        float myX = ct.InverseTransformPoint(m_CC.characterCollider.transform.position).x;
        foreach (var seg in m_TM.segments)
        {
            if (seg == null) continue;
            foreach (Transform o in seg.objectRoot)
            {
                var col = o.GetComponent<Collider>(); if (col == null || !col.enabled) continue;
                if (filter != null && !o.name.Contains(filter)) continue;
                Vector3 l = ct.InverseTransformPoint(o.position);
                bool allLane = o.GetComponent<AllLaneObstacle>() != null;
                if ((!allLane && Mathf.Abs(l.x - myX) > laneTolerance) || l.z < -0.5f) continue;
                if (l.z < dist) { dist = l.z; hit = o; }
            }
        }
        return hit != null;
    }

    bool LaneBlocked(int laneDelta)
    {
        Transform ct = m_CC.transform;
        float x = ct.InverseTransformPoint(m_CC.characterCollider.transform.position).x + laneDelta * m_TM.laneOffset;
        foreach (var seg in m_TM.segments)
        {
            if (seg == null) continue;
            foreach (Transform o in seg.objectRoot)
            {
                if (o.GetComponent<Collider>() == null) continue;
                Vector3 l = ct.InverseTransformPoint(o.position);
                if (l.z > -1f && l.z < 25f && (o.GetComponent<AllLaneObstacle>() != null || Mathf.Abs(l.x - x) < 0.75f)) return true;
            }
        }
        return false;
    }

    IEnumerator Drive(string name, float seconds, bool jump, bool dodge)
    {
        var hitTypes = new Dictionary<string, int>();
        int hits = 0, jumps = 0, dodges = 0;
        var logged = new HashSet<int>();
        float t = 0; int life = m_CC.currentLife;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            if (m_CC.currentLife < life)
            {
                hits += life - m_CC.currentLife;
                m_CC.currentLife = m_CC.maxLife;
                foreach (var seg in m_TM.segments)
                    if (seg != null)
                        foreach (Transform o in seg.objectRoot)
                        {
                            var col = o.GetComponent<Collider>();
                            if (col != null && !col.enabled && logged.Add(col.GetInstanceID())) { string k = TypeName(o); hitTypes.TryGetValue(k, out int c); hitTypes[k] = c + 1; }
                        }
            }
            life = m_CC.currentLife;
            if (!m_TM.isMoving)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                if (!m_TM.isMoving) m_TM.StartMove(false);
                continue;
            }
            if (ObstacleAhead(out float d, out Transform o2, 0.75f, null))
            {
                float jumpLen = m_CC.jumpLength * (1f + m_TM.speedRatio);
                if (jump && !m_CC.isJumping && d < jumpLen * 0.5f + 0.2f && d > 0.3f) { m_CC.Jump(); jumps++; }
                if (dodge && d < 12f && d > 2f)
                {
                    int lane = Mathf.RoundToInt(m_CC.transform.InverseTransformPoint(m_CC.characterCollider.transform.position).x / m_TM.laneOffset);
                    foreach (int c in new[] { 1, -1 })
                        if (lane + c >= -1 && lane + c <= 1 && !LaneBlocked(c)) { m_CC.ChangeLane(c); dodges++; yield return new WaitForSecondsRealtime(0.3f); break; }
                }
            }
            yield return null;
        }
        Out($"SURUS {name} (bilgi): carpma={hits} ziplama={jumps} serit_degistirme={dodges} carpilan: {string.Join(" ", hitTypes.Select(kv => kv.Key + "=" + kv.Value))} mesafe={m_TM.worldDistance:F0}m");
        if (name == "dokunmadan") Check(hits > 0, $"dokunmadan engele carpinca can gidiyor (carpma={hits})");
    }

    IEnumerator Shot(string what)
    {
        Directory.CreateDirectory(m_ShotDir);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(m_ShotDir, $"istiklal-{what}.png"));
        Out("goruntu " + what);
        yield return null;
    }

    void Finish()
    {
        Out($"OZET gecti={m_Pass} kaldi={m_Fail} hata_logu={m_Errors}");
        foreach (var h in m_Held) if (h.IsValid()) Addressables.Release(h);
        Application.Quit();
    }
}
#endif
