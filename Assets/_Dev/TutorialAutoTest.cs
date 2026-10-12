#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Tutorial otomatik testi: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir.
//   Masaüstü: "-tutorialTest" -> tüm senaryolar (sonuçlar "TT|" önekiyle günlüğe yazılır, sonunda uygulama kapanır)
//   Web:      "?autotest=tutorial" -> yalnızca bot tutorial'ı oynar (START/Run'a elle basılır)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialAutoTest : MonoBehaviour
{
    static readonly BindingFlags k_Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly FieldInfo s_GsPrompt = typeof(GameState).GetField("m_TutorialPromptVisible", k_Inst);
    static readonly FieldInfo s_GsCleared = typeof(GameState).GetField("m_TutorialClearedObstacle", k_Inst);
    static readonly FieldInfo s_GsCount = typeof(GameState).GetField("m_CountObstacles", k_Inst);
    static readonly FieldInfo s_GsCompleted = typeof(GameState).GetField("m_TutorialCompleted", k_Inst);
    static readonly FieldInfo s_CicLane = typeof(CharacterInputController).GetField("m_CurrentLane", k_Inst);
    static readonly FieldInfo s_TmSpawned = typeof(TrackManager).GetField("_spawnedSegments", k_Inst);

    bool m_ScenarioMode;
    public bool botEnabled = true;

    string m_Phase = "boot";
    int m_Errors;
    readonly Dictionary<string, int> m_PhaseErrors = new Dictionary<string, int>();
    readonly List<string> m_ErrorSamples = new List<string>();
    int m_Pass, m_Fail;

    GameState m_GS;
    TrackManager m_TM;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        bool web = Application.platform == RuntimePlatform.WebGLPlayer;
        bool scenarios = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tutorialTest") >= 0;
        bool webBot = web && Application.absoluteURL.Contains("autotest=tutorial");
        if (!(scenarios || webBot)) return; // bu kod yalnızca test derlemelerinde var

        var go = new GameObject("TutorialAutoTest");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<TutorialAutoTest>();
        t.m_ScenarioMode = scenarios;
        Out(scenarios ? "senaryo modu" : "web bot modu");
    }

    static void Out(string s) => Debug.Log("TT| " + s);

    void OnEnable() => Application.logMessageReceived += OnLog;
    void OnDisable() => Application.logMessageReceived -= OnLog;

    void OnLog(string cond, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        m_Errors++;
        m_PhaseErrors.TryGetValue(m_Phase, out int n); m_PhaseErrors[m_Phase] = n + 1;
        if (m_ErrorSamples.Count < 10) m_ErrorSamples.Add($"[{m_Phase}] {type}: {(cond.Length > 160 ? cond.Substring(0, 160) : cond)} @ {FirstFrame(stack)}");
    }

    static string FirstFrame(string stack)
    {
        if (string.IsNullOrEmpty(stack)) return "?";
        int nl = stack.IndexOf('\n');
        return nl > 0 ? stack.Substring(0, nl) : stack;
    }

    void Check(bool ok, string what)
    {
        if (ok) m_Pass++; else m_Fail++;
        Out($"{(ok ? "GECTI" : "KALDI")} [{m_Phase}] {what}");
    }

    // ---------------- Bot ----------------

    void Update()
    {
        if (!botEnabled) return;
        var gm = GameManager.instance;
        if (gm == null) return;
        var gs = gm.topState as GameState;
        if (gs == null) return;
        BotStep(gs, gs.trackManager);
    }

    void BotStep(GameState gs, TrackManager tm)
    {
        var chr = tm.characterController;
        if (chr == null || chr.character == null) return;

        // Web bot: tarayıcıda kare hızı düşük olabildiği için zamanlamaya güvenmeden akışı test etmek üzere
        // tutorial boyunca dokunulmaz (tutorial'da dokunulmazken çarpışma "geçildi" sayılır).
        if (!m_ScenarioMode && tm.isTutorial != chr.IsCheatInvincible()) chr.CheatInvincible(tm.isTutorial);

        bool prompt = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
        if (prompt && AudioListener.pause)
        {
            // Gerçek girdi yolundaki TutorialMoveCheck'in yaptığı gibi doğru hareket onayı verir.
            chr.tutorialWaitingForValidation = false;
            switch (chr.currentTutorialLevel)
            {
                case 0: chr.ChangeLane(FreeLaneDirection(tm, chr)); break;
                case 1: chr.Jump(); break;
                case 2: chr.Slide(); break;
            }
            return;
        }

        if (!tm.isMoving) return;

        int myLane = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : 1;
        bool[] blocked = new bool[3];
        Transform pivot = chr.transform;
        int segCount = Mathf.Min(3, tm.segments.Count);
        for (int s = 0; s < segCount; ++s)
        {
            var seg = tm.segments[s];
            if (seg == null) continue;
            foreach (var o in seg.objectRoot.GetComponentsInChildren<Obstacle>())
            {
                Vector3 local = pivot.InverseTransformPoint(o.transform.position);
                if (local.z < 0.1f || local.z > 4.5f) continue;
                string n = o.name;
                if (n.Contains("LowBarrier"))
                {
                    if (local.z < 1.6f && !chr.isJumping) chr.Jump();
                }
                else if (n.Contains("HighBarrier"))
                {
                    if (local.z < 1.8f && !chr.isSliding) chr.Slide();
                }
                else if (o is PatrollingObstacle)
                {
                    if (local.z < 1.6f && !chr.isJumping) chr.Jump();
                }
                else
                {
                    int lane = Mathf.Clamp(Mathf.RoundToInt(local.x / tm.laneOffset) + 1, 0, 2);
                    blocked[lane] = true;
                }
            }
        }

        if (blocked[myLane])
        {
            if (myLane > 0 && !blocked[myLane - 1]) chr.ChangeLane(-1);
            else if (myLane < 2 && !blocked[myLane + 1]) chr.ChangeLane(1);
            else if (myLane == 0 && !blocked[2]) chr.ChangeLane(1);
            else if (myLane == 2 && !blocked[0]) chr.ChangeLane(-1);
        }
    }

    static int FreeLaneDirection(TrackManager tm, CharacterInputController chr)
    {
        int myLane = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : 1;
        return myLane >= 2 ? -1 : 1;
    }

    // ---------------- Senaryolar ----------------

    IEnumerator Start()
    {
        if (!m_ScenarioMode) yield break;
        botEnabled = false;

        if (SceneManager.GetActiveScene().name != "Main") { yield return null; SceneManager.LoadScene("Main"); }
        float w = 0;
        while (!(GameManager.instance != null && GameManager.instance.topState is LoadoutState && CharacterDatabase.loaded && ThemeDatabase.loaded) && w < 60f)
        { w += Time.unscaledDeltaTime; yield return null; }
        if (w >= 60f) { Out("HATA: Loadout hazir olmadi"); Application.Quit(); yield break; }
        yield return new WaitForSecondsRealtime(1f);

        m_GS = GameManager.instance.FindState("Game") as GameState;
        m_TM = m_GS.trackManager;

        yield return S1_FirstTutorialToNormalRun();
        yield return S2_NextRunIsNormal();
        yield return S3_DeletedSaveAbandonAndRestart();
        yield return S4_DoubleFinish();
        yield return S5_DeathAndGameOverFlows();
        yield return S6_MenuReturnAndRestart();
        yield return S7_QuitMidJumpAndSlide();
        yield return S8_MovesBeforePrompt();
        yield return S9_EarlyMoveStillPrompts();

        m_Phase = "son";
        Out($"OZET gecti={m_Pass} kaldi={m_Fail} hata_logu={m_Errors}");
        foreach (var kv in m_PhaseErrors) Out($"HATALAR {kv.Key}: {kv.Value}");
        foreach (var e in m_ErrorSamples) Out("HATA " + e);
        Out("bitti");
        Application.Quit();
    }

    string Top() => GameManager.instance.topState.GetName();
    bool PromptVisible() => s_GsPrompt != null && (bool)s_GsPrompt.GetValue(m_GS);
    int Cleared() => s_GsCleared != null ? (int)s_GsCleared.GetValue(m_GS) : -1;
    bool CountObstacles() => s_GsCount != null && (bool)s_GsCount.GetValue(m_GS);
    int Spawned() => s_TmSpawned != null ? (int)s_TmSpawned.GetValue(m_TM) : -1;

    static bool DiskTutorialDone()
    {
        string path = Application.persistentDataPath + "/save.bin";
        if (!File.Exists(path)) return false;
        byte[] b = File.ReadAllBytes(path);
        return b.Length > 4 && System.BitConverter.ToInt32(b, 0) >= 12 && b[b.Length - 1] != 0;
    }

    IEnumerator StartFromLoadout()
    {
        float w = 0;
        while (!(GameManager.instance.topState is LoadoutState) && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(1f);
        ((LoadoutState)GameManager.instance.topState).StartGame();
        yield return null;
    }

    IEnumerator WaitMoving(float timeout = 30f)
    {
        float w = 0;
        while (!m_TM.isMoving && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
    }

    IEnumerator QuitViaPauseMenu()
    {
        var gs = GameManager.instance.topState as GameState;
        if (gs == null) yield break;
        if (!gs.pauseMenu.gameObject.activeSelf) gs.Pause();
        yield return null;
        gs.QuitToLoadout();
        yield return new WaitForSecondsRealtime(2f);
    }

    IEnumerator PlayUntilTutorialDone(float timeout)
    {
        botEnabled = true;
        float w = 0;
        while (m_TM.isTutorial && Top() == "Game" && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
    }

    void CheckClean(string when)
    {
        int orphanObs = 0;
        foreach (var o in FindObjectsByType<Obstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (o.GetComponentInParent<TrackSegment>(true) == null) orphanObs++;
        int segs = FindObjectsByType<TrackSegment>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int coins = FindObjectsByType<Coin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Out($"TEMIZLIK {when}: sayac={Spawned()} liste={m_TM.segments.Count} parca_nesnesi={segs} yetim_engel={orphanObs} altin_nesnesi={coins} isMoving={m_TM.isMoving}");
        Check(Spawned() == 0 && m_TM.segments.Count == 0 && segs == 0 && !m_TM.isMoving, $"{when}: sayac/liste/parca sifir, hareket durdu");
    }

    // ---- Yönerge zamanlaması ve "hareketi zaten yaptı" senaryoları (S8, S9) ----

    static readonly FieldInfo s_GsObstacleIndex = typeof(GameState).GetField("m_CurrentSegmentObstacleIndex", k_Inst);

    // GameState'in kullandığı engel: o anki segmentteki sıradaki engel. Yoksa float.MaxValue.
    float DistanceToNextObstacle()
    {
        var seg = m_TM.currentSegment;
        int idx = s_GsObstacleIndex != null ? (int)s_GsObstacleIndex.GetValue(m_GS) : 0;
        if (seg == null || idx >= seg.obstaclePositions.Length) return float.MaxValue;
        return seg.obstaclePositions[idx] * seg.worldLength - m_TM.currentSegmentDistance;
    }

    // Sıradaki engel en fazla 'max' m uzakta olana kadar bekler.
    IEnumerator WaitObstacleWithin(float max, float timeout = 30f)
    {
        float w = 0;
        while (DistanceToNextObstacle() > max && Top() == "Game" && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
    }

    IEnumerator BotUntilLevel(int level, float timeout = 120f)
    {
        botEnabled = true;
        float w = 0;
        while (m_TM.isTutorial && m_TM.characterController.currentTutorialLevel < level && Top() == "Game" && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
        botEnabled = false;
    }

    // Sıradaki engel sayılana (geçilene ya da çarpılana) kadar izler: yönerge açıldı mı, kaç m kala, engel geçildi mi?
    IEnumerator WatchObstacle(System.Action<bool, float, bool> done)
    {
        int cleared0 = Cleared();
        int idx0 = s_GsObstacleIndex != null ? (int)s_GsObstacleIndex.GetValue(m_GS) : 0;
        var seg0 = m_TM.currentSegment;
        bool promptSeen = false; float promptDist = -1f; float w = 0;
        while (Top() == "Game" && w < 20f)
        {
            if (!promptSeen && PromptVisible()) { promptSeen = true; promptDist = DistanceToNextObstacle(); }
            int idx = s_GsObstacleIndex != null ? (int)s_GsObstacleIndex.GetValue(m_GS) : 0;
            if (m_TM.currentSegment != seg0 || idx != idx0) break;
            w += Time.unscaledDeltaTime;
            yield return null;
        }
        done(promptSeen, promptDist, Cleared() != cleared0);
    }

    IEnumerator S8_MovesBeforePrompt()
    {
        m_Phase = "S8-yonerge-oncesi-hareket";
        var chr = m_TM.characterController;

        // İlk segment rastgele: kısa (18 m) segmentte koşu başladığında engel zaten yönerge mesafesinin (8 m) içinde
        // olduğu için yönergeden önce hareket aralığı yoktur (yönerge hemen açılır). Uzun segment gelene kadar yeniden başlat.
        for (int attempt = 1; ; ++attempt)
        {
            PlayerData.NewSave();
            yield return StartFromLoadout();
            yield return WaitMoving();
            float startDist = DistanceToNextObstacle();
            if (startDist > 9f || attempt >= 4) { Out($"ilk engel koşu başında {startDist:F1} m (deneme {attempt})"); break; }
            Out($"kisa ilk segment (engel {startDist:F1} m), yonergeden once hareket araligi yok; yeniden baslatiliyor");
            botEnabled = true; // açılan yönergeyi onaylasın
            yield return new WaitForSecondsRealtime(0.5f);
            botEnabled = false;
            yield return QuitViaPauseMenu();
        }
        Check(m_TM.isTutorial && chr.currentTutorialLevel == 0, "tutorial basladi (asama 0)");

        // Şerit: ilk engel ortada tek başına; oyuncu yönerge mesafesinden (8 m) önce sağa geçer.
        yield return WaitObstacleWithin(9.5f);
        Out($"serit hareketi engele {DistanceToNextObstacle():F1} m kala");
        chr.ChangeLane(1);
        bool p = false, ok = false; float d = 0;
        yield return WatchObstacle((a, b, c) => { p = a; d = b; ok = c; });
        Check(!p, "serit: oyuncu yonergeden once bos seride gecti -> yonerge acilmadi");
        Check(ok, "serit: engel gecildi (sayac artti)");

        yield return BotUntilLevel(1);
        Check(chr.currentTutorialLevel == 1, "asama 1'e gecildi");

        // Zıplama: engeli aşacak mesafede (4,3 m; yönerge 3,9 m) yönergeden önce zıplar.
        yield return WaitObstacleWithin(4.3f);
        Out($"zipla engele {DistanceToNextObstacle():F1} m kala");
        chr.Jump();
        yield return WatchObstacle((a, b, c) => { p = a; d = b; ok = c; });
        Check(!p, "zipla: engeli asacak ziplama zaten suruyordu -> yonerge acilmadi");
        Check(ok, "zipla: engel gecildi (carpma yok)");

        yield return BotUntilLevel(2);
        Check(chr.currentTutorialLevel == 2, "asama 2'ye gecildi");

        // Kayma: 6,2 m'de (yönerge 5,5 m) kayar; 7 m'lik kayma engeli aşar.
        yield return WaitObstacleWithin(6.2f);
        Out($"kay engele {DistanceToNextObstacle():F1} m kala");
        chr.Slide();
        yield return WatchObstacle((a, b, c) => { p = a; d = b; ok = c; });
        Check(!p, "kay: engeli asacak kayma zaten suruyordu -> yonerge acilmadi");
        Check(ok, "kay: engel gecildi (carpma yok)");

        yield return PlayUntilTutorialDone(180f);
        botEnabled = false;
        Check(!m_TM.isTutorial && Top() == "Game", "tutorial tamamlandi");
        yield return QuitViaPauseMenu();
        CheckClean("S8 cikis sonrasi");
    }

    IEnumerator S9_EarlyMoveStillPrompts()
    {
        m_Phase = "S9-erken-hareket-ve-zamanlama";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        var chr = m_TM.characterController;

        // Şerit: oyuncu ortada kalır -> yönerge açılmalı, engelden ~8 m önce (eskiden ~2 m).
        bool p = false, ok = false; float d = 0;
        botEnabled = true; // yönergeyi onaylar
        yield return WatchObstacle((a, b, c) => { p = a; d = b; ok = c; });
        Out($"serit yonergesi engele {d:F1} m kala acildi");
        Check(p && d > 6.5f && d <= 8.2f, $"serit: yonerge zamaninda acildi ({d:F1} m, beklenen ~8 m)");
        Check(ok, "serit: yonergeden sonra engel gecildi");

        yield return BotUntilLevel(1);

        // Zıplama: çok erken zıplar (9 m) -> bu zıplama engeli aşamaz; yönerge açılmalı ve zıplamayı bitirmeli.
        yield return WaitObstacleWithin(9f);
        chr.Jump();
        float w = 0;
        while (!PromptVisible() && Top() == "Game" && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
        bool jumpingAtPrompt = chr.isJumping;
        d = DistanceToNextObstacle();
        Out($"zipla yonergesi engele {d:F1} m kala acildi, ziplama suruyor mu={jumpingAtPrompt}");
        Check(PromptVisible() && d > 3.4f && d <= 4.0f, $"zipla: erken ziplamaya ragmen yonerge zamaninda acildi ({d:F1} m, beklenen ~3,9 m)");
        Check(!jumpingAtPrompt, "zipla: engeli asamayacak erken ziplama yonergede bitirildi");
        botEnabled = true; // yönergeyi onaylar ve zıplar
        yield return WatchObstacle((a, b, c) => { ok = c; });
        Check(ok, "zipla: yonergedeki ziplama engeli asti (carpma yok)");

        yield return BotUntilLevel(2);
        botEnabled = true;
        yield return WatchObstacle((a, b, c) => { p = a; d = b; ok = c; });
        Out($"kay yonergesi engele {d:F1} m kala acildi");
        Check(p && d > 5.0f && d <= 5.6f, $"kay: yonerge zamaninda acildi ({d:F1} m, beklenen ~5,5 m)");
        Check(ok, "kay: yonergedeki kayma engeli asti (carpma yok)");

        yield return PlayUntilTutorialDone(180f);
        botEnabled = false;
        Check(!m_TM.isTutorial && Top() == "Game", "tutorial tamamlandi");
        yield return QuitViaPauseMenu();
        CheckClean("S9 cikis sonrasi");
    }

    IEnumerator S1_FirstTutorialToNormalRun()
    {
        m_Phase = "S1-ilk-tutorial";
        PlayerData.NewSave();
        Check(!PlayerData.instance.tutorialDone && !DiskTutorialDone(), "kayit sifirlandi (bellek ve disk tutorialDone=false)");
        yield return StartFromLoadout();
        yield return WaitMoving();
        Check(m_TM.isTutorial && m_TM.characterController.currentTutorialLevel == 0 && Cleared() == 0 && CountObstacles(), "ilk kosu tutorial, asama 0, sayac 0");

        float t0 = Time.realtimeSinceStartup;
        int loadoutVisits = 0;
        string lastTop = Top();
        botEnabled = true;
        while (m_TM.isTutorial && Time.realtimeSinceStartup - t0 < 300f)
        {
            if (Top() != lastTop) { lastTop = Top(); if (lastTop == "Loadout") loadoutVisits++; }
            if (Top() != "Game") break;
            yield return null;
        }
        Out($"tutorial suresi {Time.realtimeSinceStartup - t0:F0}s");
        Check(!m_TM.isTutorial && Top() == "Game", "tutorial tamamlandi ve oyun Game durumunda kaldi (menu acilmadi)");
        Check(loadoutVisits == 0, "tutorial sirasinda menuye gecilmedi");
        Check(PlayerData.instance.tutorialDone && DiskTutorialDone(), "tutorialDone bellekte ve diskte true");

        m_TM.characterController.CheatInvincible(true);
        float speed0 = m_TM.speed;
        yield return new WaitForSecondsRealtime(30f);
        int activeCoins = 0;
        foreach (var c in FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) activeCoins++;
        Out($"normal kosu: hiz {speed0:F2}->{m_TM.speed:F2} parca={m_TM.segments.Count} aktif_altin={activeCoins} mesafe={m_TM.worldDistance:F0}");
        Check(Top() == "Game" && m_TM.isMoving, "30 sn sonra kosu kesintisiz devam ediyor");
        Check(m_TM.speed > speed0, "normal kosuda hizlanma basladi");
        Check(activeCoins > 0, "normal kosuda altin uretiliyor");
        Check(m_TM.segments.Count >= 9, "onde 9+ parca (normal kosu parca sayisi)");
        m_TM.characterController.CheatInvincible(false);
        botEnabled = false;

        yield return QuitViaPauseMenu();
        Check(Top() == "Loadout", "duraklatma menusunden istenerek menuye donuldu");
        CheckClean("S1 cikis sonrasi");
    }

    IEnumerator S2_NextRunIsNormal()
    {
        m_Phase = "S2-sonraki-kosu";
        yield return StartFromLoadout();
        yield return WaitMoving();
        Check(!m_TM.isTutorial && !PromptVisible(), "sonraki kosu tutorial'siz basladi");
        Check(m_TM.newSegmentCreated == null && m_TM.currentSegementChanged == null, "tutorial geri cagrilari normal kosuda temiz");
        m_TM.characterController.CheatInvincible(true);
        yield return new WaitForSecondsRealtime(10f);
        Check(Top() == "Game" && m_TM.isMoving, "normal kosu 10 sn devam etti");
        m_TM.characterController.CheatInvincible(false);
        yield return QuitViaPauseMenu();
        CheckClean("S2 cikis sonrasi");
    }

    IEnumerator S3_DeletedSaveAbandonAndRestart()
    {
        m_Phase = "S3-kayit-sil-yarida-birak";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        Check(m_TM.isTutorial && m_TM.characterController.currentTutorialLevel == 0 && Cleared() == 0 && CountObstacles(), "kayit silindikten sonra tutorial bastan basladi");

        botEnabled = true;
        float t0 = Time.realtimeSinceStartup;
        while (m_TM.characterController.currentTutorialLevel < 1 && Time.realtimeSinceStartup - t0 < 120f) yield return null;
        yield return new WaitForSecondsRealtime(1f);
        Out($"yarida birakma aninda: asama={m_TM.characterController.currentTutorialLevel} gecilen={Cleared()} sayim={CountObstacles()}");
        botEnabled = false;
        yield return QuitViaPauseMenu();
        Check(Top() == "Loadout" && !PlayerData.instance.tutorialDone, "tutorial yarida birakildi, tutorialDone hala false");
        CheckClean("S3 yarida birakma sonrasi");

        m_Phase = "S3-yeniden-baslat";
        yield return StartFromLoadout();
        yield return WaitMoving();
        Check(m_TM.isTutorial && m_TM.characterController.currentTutorialLevel == 0 && Cleared() == 0 && CountObstacles() && !PromptVisible(),
            $"yeniden baslatmada durum sifirlandi (asama={m_TM.characterController.currentTutorialLevel} gecilen={Cleared()} sayim={CountObstacles()})");

        // Tutorial sırasında, komut ekranı yokken duraklatma
        m_Phase = "S3-duraklatma";
        float w = 0;
        while ((PromptVisible() || !m_TM.isMoving) && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
        m_GS.Pause();
        yield return new WaitForSecondsRealtime(1.5f);
        Check(AudioListener.pause && m_GS.pauseMenu.gameObject.activeSelf && !m_TM.isMoving, "tutorial'da duraklatma 1.5 sn sonra hala aktif (kendiliginden devam etmedi)");
        m_GS.Resume();
        yield return new WaitForSecondsRealtime(0.5f);
        Check(!AudioListener.pause && m_TM.isMoving, "devam et ile kosu surdu");

        w = 0;
        while ((PromptVisible() || !m_TM.isMoving) && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
        m_GS.SendMessage("OnApplicationFocus", false);
        yield return new WaitForSecondsRealtime(1.5f);
        Check(AudioListener.pause && m_GS.pauseMenu.gameObject.activeSelf, "odak kaybinda duraklatma menusu acildi ve kaldi");
        m_GS.Resume();
        yield return new WaitForSecondsRealtime(0.5f);
        // Devam ettikten hemen sonra tutorial komut ekranı oyunu kasıtlı olarak durdurabilir.
        Check(m_TM.isMoving || (PromptVisible() && !m_GS.pauseMenu.gameObject.activeSelf), $"odak kaybindan sonra devam (hareket={m_TM.isMoving} komut={PromptVisible()})");

        m_Phase = "S3-tamamla";
        yield return PlayUntilTutorialDone(300f);
        Check(!m_TM.isTutorial && Top() == "Game" && PlayerData.instance.tutorialDone, "yeniden baslatilan tutorial tamamlandi, kosu devam ediyor");
        botEnabled = false;
    }

    IEnumerator S4_DoubleFinish()
    {
        m_Phase = "S4-cift-tamamlama";
        int before = m_Errors;
        m_GS.FinishTutorial();
        m_GS.FinishTutorial();
        yield return new WaitForSecondsRealtime(1f);
        bool completed = s_GsCompleted != null && (bool)s_GsCompleted.GetValue(m_GS);
        Check(Top() == "Game" && m_TM.isMoving && completed && m_Errors == before, "FinishTutorial iki kez cagrildi: menu acilmadi, hata yok, kosu suruyor");
        yield return QuitViaPauseMenu();
        CheckClean("S4 cikis sonrasi");
    }

    IEnumerator S5_DeathAndGameOverFlows()
    {
        m_Phase = "S5-olum-oyunsonu";
        botEnabled = false;
        yield return StartFromLoadout();
        yield return WaitMoving();
        m_TM.characterController.CheatInvincible(false);
        float w = 0;
        while (Top() != "GameOver" && w < 180f)
        {
            // Oyun sonu penceresi açılınca ikinci şans yerine oyun sonuna geç
            if (m_GS.gameOverPopup.activeSelf) m_GS.GameOver();
            w += Time.unscaledDeltaTime; yield return null;
        }
        Check(Top() == "GameOver", $"engele carpinca oyun sonu ekrani ({w:F0}s)");
        var go = GameManager.instance.FindState("GameOver") as GameOverState;
        go.RunAgain();
        yield return WaitMoving();
        Check(Top() == "Game" && m_TM.isMoving && !m_TM.isTutorial, "oyun sonundan tekrar kos");
        w = 0;
        while (Top() != "GameOver" && w < 180f) { if (m_GS.gameOverPopup.activeSelf) m_GS.GameOver(); w += Time.unscaledDeltaTime; yield return null; }
        go.GoToLoadout();
        yield return new WaitForSecondsRealtime(2f);
        Check(Top() == "Loadout", "oyun sonundan menuye");
        CheckClean("S5 sonrasi");
    }

    IEnumerator S7_QuitMidJumpAndSlide()
    {
        botEnabled = false;
        var chr = m_TM.characterController;
        for (int i = 0; i < 2; ++i)
        {
            bool slide = i == 1;
            m_Phase = slide ? "S7-kayarken-cikis" : "S7-ziplarken-cikis";
            yield return StartFromLoadout();
            yield return WaitMoving();
            chr.CheatInvincible(true);
            yield return new WaitForSecondsRealtime(1f);
            float colliderY = chr.characterCollider.collider.size.y;
            if (slide) chr.Slide(); else chr.Jump();
            yield return null;
            Check(slide ? chr.isSliding : chr.isJumping, slide ? "kayma basladi" : "ziplama basladi");
            yield return QuitViaPauseMenu();
            int before = m_Errors;
            yield return StartFromLoadout();
            yield return WaitMoving();
            yield return new WaitForSecondsRealtime(2f);
            Check(m_Errors == before, $"yeni kosu hatasiz basladi (yeni hata={m_Errors - before})");
            Check(!chr.isJumping && !chr.isSliding && Mathf.Abs(chr.characterCollider.collider.size.y - colliderY) < 0.001f,
                $"yeni kosu yerde ve carpisma kutusu normal (y={chr.characterCollider.collider.size.y:F2}, once={colliderY:F2})");
            chr.CheatInvincible(false);
            yield return QuitViaPauseMenu();
        }
    }

    IEnumerator S6_MenuReturnAndRestart()
    {
        m_Phase = "S6-menu-don-tekrar";
        for (int i = 0; i < 3; ++i)
        {
            yield return StartFromLoadout();
            if (i == 1) yield return new WaitForSecondsRealtime(1.5f); // geri sayım sırasında
            else yield return WaitMoving();
            if (i == 2) yield return new WaitForSecondsRealtime(5f);
            yield return QuitViaPauseMenu();
            CheckClean($"S6 tekrar {i}");
        }
        yield return StartFromLoadout();
        yield return WaitMoving();
        m_TM.characterController.CheatInvincible(true);
        yield return new WaitForSecondsRealtime(15f);
        Check(Top() == "Game" && m_TM.isMoving && m_TM.segments.Count >= 9, $"tekrarlardan sonra normal kosu (parca={m_TM.segments.Count})");
        m_TM.characterController.CheatInvincible(false);
        yield return QuitViaPauseMenu();
        CheckClean("S6 son");
    }
}
#endif
