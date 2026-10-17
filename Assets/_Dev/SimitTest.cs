#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Test aracı: yalnızca geliştirme derlemesinde ve "-simitTest" argümanıyla çalışır; yayın derlemesine girmez.
// İstanbul simit tezgâhı test engelini (ObstacleSimitTezgahi, Addressables "Istanbul-Test") gerçek koşuda dener.
// Day temasının parçaları YALNIZCA bu sürecin belleğinde simit tezgâhı kullanacak şekilde ayarlanır (dosyalar değişmez),
// böylece engel ve altınlar oyunun kendi akışıyla (SpawnObstaclesThenCollectibles → SimpleBarricade → altınlar) yerleşir.
// Argümanlar: -simitTest [-simitShots <klasör>] [-diagSeed <n>]
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class SimitTest : MonoBehaviour
{
    // Varsayılan: ObstacleSimitTezgahi.prefab. "-simitKoni": aynı sürüşü mevcut trafik konisiyle yapar (karşılaştırma).
    string k_SimitGuid = "37cd61e3be182724a8e8edd2c906da90";
    string k_Address = "ObstacleSimitTezgahi";
    bool m_Cone;
    string k_Name = "SimitTezgahi";
    const int k_ObstacleMask = 1 << 9;

    string m_ShotDir;
    int m_Seed = 4242;
    int m_Pass, m_Fail, m_Errors;
    TrackManager m_TM;
    CharacterInputController m_CC;
    readonly List<AsyncOperationHandle<GameObject>> m_Held = new List<AsyncOperationHandle<GameObject>>();

    // Ölçüm
    int m_Simits, m_SimitSegments, m_AdjacentBlocked, m_SameLaneFree, m_Coins, m_CoinsInSimit, m_CoinsInOther;
    int m_SegTriMax, m_SegCount; long m_SegTriSum; int m_SimitTriMax;
    readonly HashSet<int> m_Seen = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-simitTest") < 0) return;
        var go = new GameObject("SimitTest");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<SimitTest>();
        for (int i = 0; i < args.Length - 1; ++i)
        {
            if (args[i] == "-simitShots") t.m_ShotDir = args[i + 1];
            if (args[i] == "-diagSeed") int.TryParse(args[i + 1], out t.m_Seed);
        }
        if (System.Array.IndexOf(args, "-simitKoni") >= 0)
        {
            t.m_Cone = true;
            t.k_SimitGuid = "f0070c0f7d7cdcb4eb7176be4a87caa9";
            t.k_Address = "ObstacleRoadworksCone";
            t.k_Name = "RoadworksCone";
        }
    }

    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }
    void OnLog(string cond, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { m_Errors++; Out("HATA_LOGU " + cond); }
    }

    static void Out(string s) { Debug.Log("SIMIT| " + s); }
    // Okuma izni (Read/Write) gerektirmeden üçgen sayısı.
    static int Tris(Mesh m)
    {
        if (m == null) return 0;
        long n = 0;
        for (int i = 0; i < m.subMeshCount; ++i) n += m.GetIndexCount(i);
        return (int)(n / 3);
    }
    void Check(bool ok, string what) { if (ok) m_Pass++; else m_Fail++; Out((ok ? "GECTI " : "KALDI ") + what); }

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

        // Simit prefab'ı Addressables'tan (adresiyle) yüklenebiliyor mu?
        Out("engel: " + k_Address + (m_Cone ? " (KARSILASTIRMA: mevcut koni)" : ""));
        var probe = Addressables.LoadAssetAsync<GameObject>(k_Address);
        yield return probe;
        GameObject simitPrefab = probe.Status == AsyncOperationStatus.Succeeded ? probe.Result : null;
        Check(simitPrefab != null && simitPrefab.GetComponent<SimpleBarricade>() != null && simitPrefab.layer == 9,
            k_Address + " Addressables'tan yuklendi (SimpleBarricade, katman 9)");
        if (simitPrefab == null) { Finish(); yield break; }
        m_Held.Add(probe);
        var box = simitPrefab.GetComponent<BoxCollider>();
        Out($"OLCU carpisma={box.size.x:F2}x{box.size.y:F2}x{box.size.z:F2} merkez_y={box.center.y:F2}");
        int simitTris = 0;
        foreach (var mf in simitPrefab.GetComponentsInChildren<MeshFilter>(true)) simitTris += Tris(mf.sharedMesh);
        var tex = simitPrefab.GetComponentInChildren<Renderer>().sharedMaterial.mainTexture;
        Out($"OLCU ucgen={simitTris} doku={(tex != null ? tex.width + "x" + tex.height : "yok")} shader={simitPrefab.GetComponentInChildren<Renderer>().sharedMaterial.shader.name}");
        if (tex == null && !m_Cone) { Check(false, "simit dokusu yok"); Finish(); yield break; }
        if (!m_Cone)
        {
            Check(simitTris >= 1500 && simitTris <= 3000, $"ucgen sayisi hedefte ({simitTris})");
            Check(tex.width <= 1024 && tex.height <= 1024, $"doku en fazla 1024 ({tex.width}x{tex.height})");
            Check(simitPrefab.GetComponentInChildren<Renderer>().sharedMaterial.shader.name == "Unlit/CurvedUnlit", "malzeme CurvedUnlit");
        }

        // Day temasının engel alan parçaları bu süreçte yalnız simit tezgâhı kullansın (bellekte; dosya değişmez).
        var day = ThemeDatabase.GetThemeData("Day");
        var simitRef = new AssetReference(k_SimitGuid);
        int patched = 0;
        foreach (var zone in day.zones)
            foreach (var r in zone.prefabList)
            {
                var h = Addressables.LoadAssetAsync<GameObject>(r.RuntimeKey);
                yield return h;
                m_Held.Add(h);
                var seg = h.Result != null ? h.Result.GetComponent<TrackSegment>() : null;
                if (seg != null && seg.obstaclePositions != null && seg.obstaclePositions.Length > 0)
                { seg.possibleObstacles = new[] { simitRef }; patched++; }
            }
        Out($"parca ayari: {patched} parca yalniz simit kullanacak (bellekte)");

        // A) Ölçüm: yerleşim, komşu şerit, altınlar, parça üçgenleri, görüntüler. Ölümsüz.
        yield return StartRun(true);
        float t = 0; bool farShot = false, nearShot = false;
        while (t < 45f)
        {
            t += Time.unscaledDeltaTime;
            if (m_ShotDir != null && (!farShot || !nearShot))
            {
                if (SimitAhead(out float d, out _, 0.75f))
                {
                    if (!farShot && d > 13f && d < 17f) { farShot = true; yield return Shot("kosu-uzak"); }
                    else if (farShot && !nearShot && d > 4.5f && d < 7f) { nearShot = true; yield return Shot("kosu-yakin"); }
                }
            }
            yield return null;
        }
        ReportMeasure();

        // B) Zıplama: önündeki tezgâhın üstünden atlar; çarpma olmamalı.
        m_CC.CheatInvincible(false);
        yield return Drive("ziplama", 50f, true, false);
        // C) Yan şeride geçme: tezgâh varsa boş şeride geçer; çarpma olmamalı.
        yield return Drive("serit-degistirme", 40f, false, true);
        // D) Hiçbir şey yapmadan: tezgâha çarpmalı (çarpışma gerçekten çalışıyor mu).
        yield return Drive("dokunmadan", 30f, false, false);

        Finish();
    }

    IEnumerator StartRun(bool invincible)
    {
        PlayerData.instance.tutorialDone = true;
        m_TM.trackSeed = m_Seed;
        m_TM.invincible = invincible;
        ((LoadoutState)GameManager.instance.topState).StartGame();
        m_TM.newSegmentCreated = OnSegmentCreated;
        float w = 0;
        while (!m_TM.isMoving && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
        Out($"kosu basladi (bekleme {w:F1}s)");
    }

    void OnSegmentCreated(TrackSegment seg) { StartCoroutine(Measure(seg)); }

    IEnumerator Measure(TrackSegment seg)
    {
        yield return new WaitForSecondsRealtime(2f); // engel ve altınlar yerleşsin
        if (seg == null || !m_Seen.Add(seg.GetInstanceID())) yield break;

        int tris = 0;
        foreach (var mf in seg.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.GetComponentInParent<Coin>() != null || mf.sharedMesh == null) continue;
            var r = mf.GetComponent<Renderer>(); if (r == null || !r.enabled) continue;
            tris += Tris(mf.sharedMesh);
        }
        m_SegCount++; m_SegTriSum += tris; if (tris > m_SegTriMax) m_SegTriMax = tris;

        var simits = new List<Transform>();
        foreach (Transform o in seg.objectRoot) if (o.name.Contains(k_Name)) simits.Add(o);
        if (simits.Count > 0) { m_SimitSegments++; if (tris > m_SimitTriMax) m_SimitTriMax = tris; }

        float lane = m_TM.laneOffset;
        var half = new Vector3(0.288f, 0.6f, 0.466f); // karakterin çarpışma kutusu (Main: CharacterSlot 0,58 x 1,2 x 0,93)
        foreach (var s in simits)
        {
            m_Simits++;
            var col = s.GetComponent<BoxCollider>();
            foreach (int side in new[] { -1, 1 })
            {
                // Komşu şeridin ortasında duran karakter tezgâha değiyor mu?
                Vector3 p = s.position + s.right * side * lane + Vector3.up * 0.7f;
                foreach (var h in Physics.OverlapBox(p, half, s.rotation, k_ObstacleMask, QueryTriggerInteraction.Collide))
                    if (h == col) { m_AdjacentBlocked++; break; }
            }
            bool same = false;
            foreach (var h in Physics.OverlapBox(s.position + Vector3.up * 0.7f, half, s.rotation, k_ObstacleMask, QueryTriggerInteraction.Collide))
                if (h == col) same = true;
            if (!same) m_SameLaneFree++;
        }

        foreach (Transform coin in seg.collectibleTransform)
        {
            if (!coin.gameObject.activeInHierarchy) continue;
            m_Coins++;
            foreach (var h in Physics.OverlapSphere(coin.position, 0.4f, k_ObstacleMask, QueryTriggerInteraction.Collide))
            {
                if (h.name.Contains(k_Name)) { m_CoinsInSimit++; break; }
                m_CoinsInOther++; break;
            }
        }
    }

    void ReportMeasure()
    {
        Out($"OLCUM parca={m_SegCount} simitli_parca={m_SimitSegments} simit={m_Simits} altin={m_Coins}");
        Out($"OLCUM parca_ucgen ort={(m_SegCount == 0 ? 0 : m_SegTriSum / m_SegCount)} max={m_SegTriMax} simitli_parca_max={m_SimitTriMax}");
        Check(m_Simits >= 10, $"kosuda yeterli simit tezgahi dogdu ({m_Simits})");
        Check(m_AdjacentBlocked == 0, $"komsu serit kapanmiyor (komsu seritte degen={m_AdjacentBlocked}/{m_Simits * 2})");
        Check(m_SameLaneFree == 0, $"kendi seridindeki karakter tezgaha degiyor (degmeyen={m_SameLaneFree})");
        Check(m_Coins > 50 && m_CoinsInSimit == 0, $"altinlar tezgahin icine dogmuyor (icinde={m_CoinsInSimit}/{m_Coins}, diger engel={m_CoinsInOther})");
    }

    // Karakterin şeridinde, önündeki en yakın tezgâh (d: ileri mesafe).
    bool SimitAhead(out float dist, out Transform simit, float laneTolerance)
    {
        dist = float.MaxValue; simit = null;
        Transform ct = m_CC.transform;
        Vector3 me = m_CC.characterCollider.transform.position;
        foreach (var seg in m_TM.segments)
        {
            if (seg == null) continue;
            foreach (Transform o in seg.objectRoot)
            {
                if (!o.name.Contains(k_Name)) continue;
                var col = o.GetComponent<Collider>(); if (col == null || !col.enabled) continue;
                Vector3 l = ct.InverseTransformPoint(o.position);
                float lat = l.x - ct.InverseTransformPoint(me).x;
                if (Mathf.Abs(lat) > laneTolerance || l.z < -0.5f) continue;
                if (l.z < dist) { dist = l.z; simit = o; }
            }
        }
        return simit != null;
    }

    bool LaneHasSimit(int laneDelta)
    {
        Transform ct = m_CC.transform;
        float myX = ct.InverseTransformPoint(m_CC.characterCollider.transform.position).x;
        foreach (var seg in m_TM.segments)
        {
            if (seg == null) continue;
            foreach (Transform o in seg.objectRoot)
            {
                if (!o.name.Contains(k_Name)) continue;
                Vector3 l = ct.InverseTransformPoint(o.position);
                if (l.z > -1f && l.z < 25f && Mathf.Abs(l.x - (myX + laneDelta * m_TM.laneOffset)) < 0.75f) return true;
            }
        }
        return false;
    }

    IEnumerator Drive(string name, float seconds, bool jump, bool dodge)
    {
        int hits = 0, passed = 0, jumps = 0, dodges = 0;
        Transform last = null;
        float t = 0;
        int life = m_CC.currentLife;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            // Çarpma, olduğu karede yazılır (önceki karedeki otomatik pilot durumu ile birlikte).
            if (m_CC.currentLife < life)
            {
                hits += life - m_CC.currentLife;
                m_CC.currentLife = m_CC.maxLife;
                LogHit(name);
                Out("  onceki_kare: " + m_Prev);
            }
            life = m_CC.currentLife;
            if (!m_TM.isMoving)
            {
                // Çarpmadan sonra koşu kendiliğinden sürmezse yeniden başlat (can geri verildi; oyun bitmez).
                yield return new WaitForSecondsRealtime(1.5f);
                if (!m_TM.isMoving) m_TM.StartMove(false);
                last = null;
                continue;
            }
            {
                bool ahead = SimitAhead(out float pd, out _, 0.75f);
                m_Prev = $"onde_tezgah={(ahead ? pd.ToString("F2") : "-")} zipliyor={m_CC.isJumping} y={m_CC.characterCollider.transform.localPosition.y:F2} " +
                         $"serit_gecisi={Mathf.Abs(m_CC.offsetFromTarget.x):F2} hiz={m_TM.speed:F1} ziplama_boyu={m_CC.jumpLength * (1f + m_TM.speedRatio):F2}";
            }

            if (SimitAhead(out float d, out Transform s, 0.75f))
            {
                if (s != last && last != null) passed++;
                last = s;
                float jumpLen = m_CC.jumpLength * (1f + m_TM.speedRatio);
                if (jump && !m_CC.isJumping && d < jumpLen * 0.5f + 0.2f && d > 0.3f) { m_CC.Jump(); jumps++; }
                if (dodge && d < 12f && d > 2f)
                {
                    int lane = Mathf.RoundToInt(m_CC.transform.InverseTransformPoint(m_CC.characterCollider.transform.position).x / m_TM.laneOffset);
                    int dir = 0;
                    foreach (int c in new[] { 1, -1 })
                        if (lane + c >= -1 && lane + c <= 1 && !LaneHasSimit(c)) { dir = c; break; }
                    if (dir != 0) { m_CC.ChangeLane(dir); dodges++; yield return new WaitForSecondsRealtime(0.3f); }
                }
            }
            yield return null;
        }
        Out($"SURUS {name}: tezgah={passed} carpma={hits} ziplama={jumps} serit_degistirme={dodges} mesafe={m_TM.worldDistance:F0}m");
        if (name == "dokunmadan") Check(hits > 0, $"dokunmadan tezgaha carpinca can gidiyor (carpma={hits})");
        else Check(passed >= 5 && hits == 0, $"{name}: tezgahlardan carpmadan gecildi (tezgah={passed}, carpma={hits})");
    }

    // Çarpılan engel: CharacterCollider çarpışmada engelin collider'ını kapatır. Karaktere göre konumu, karakterin
    // durumu (zıplıyor mu, yüksekliği, şerit geçişi sürüyor mu) ve hız yazılır.
    readonly HashSet<int> m_LoggedHits = new HashSet<int>();
    string m_Prev = "-";
    void LogHit(string phase)
    {
        Transform ct = m_CC.transform;
        Vector3 me = ct.InverseTransformPoint(m_CC.characterCollider.transform.position);
        foreach (var seg in m_TM.segments)
        {
            if (seg == null) continue;
            foreach (Transform o in seg.objectRoot)
            {
                var col = o.GetComponent<Collider>();
                if (col == null || col.enabled || !m_LoggedHits.Add(col.GetInstanceID())) continue;
                Vector3 l = ct.InverseTransformPoint(o.position);
                Out($"CARPMA {phase}: engel={o.name.Replace("(Clone)", "")} ileri={l.z - me.z:F2} yanal={l.x - me.x:F2} " +
                    $"karakter_y={me.y:F2} zipliyor={m_CC.isJumping} serit_gecisi={Mathf.Abs(m_CC.offsetFromTarget.x):F2} hiz={m_TM.speed:F1} mesafe={m_TM.worldDistance:F0}");
                return;
            }
        }
        Out($"CARPMA {phase}: engel bulunamadi karakter_y={me.y:F2} zipliyor={m_CC.isJumping} hiz={m_TM.speed:F1}");
    }

    IEnumerator Shot(string what)
    {
        Directory.CreateDirectory(m_ShotDir);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(m_ShotDir, $"simit-{what}.png"));
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
