#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Tutorial otomatik testi: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir.
//   Masaüstü: "-tutorialTest" -> tüm senaryolar (sonuçlar "TT|" önekiyle günlüğe yazılır, sonunda uygulama kapanır)
//   Web:      "?autotest=tutorial" -> yalnızca bot tutorial'ı oynar (START/Run'a elle basılır)
// Bot eğitim hareketlerini oyunun kendi girdi kapısından (CharacterInputController.HandleMoveInput) verir: klavye ve
// dokunmatik kolun çağırdığı yol budur, yani yanlış/erken hareketler gerçek oyuncudaki gibi yok sayılır.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Move = CharacterInputController.MoveInput;

public class TutorialAutoTest : MonoBehaviour
{
    static readonly BindingFlags k_Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly FieldInfo s_GsCompleted = typeof(GameState).GetField("m_TutorialCompleted", k_Inst);
    static readonly FieldInfo s_TmSpawned = typeof(TrackManager).GetField("_spawnedSegments", k_Inst);

    static readonly Move[] k_Steps = { Move.Right, Move.Left, Move.Up, Move.Down };

    bool m_ScenarioMode;
    public bool botEnabled = true;
    public float botReaction = 0.0f; // eğitimde yönerge göründükten sonra hareket etmeden önce bekleme (sn)

    string m_Phase = "boot";
    int m_Errors;
    readonly Dictionary<string, int> m_PhaseErrors = new Dictionary<string, int>();
    readonly List<string> m_ErrorSamples = new List<string>();
    int m_Pass, m_Fail;
    readonly List<string> m_Summary = new List<string>();

    GameState m_GS;
    TrackManager m_TM;

    // Eğitim boyunca gözlenenler (her karede güncellenir).
    int m_ObsStep = -2;
    float m_ObsStepTime;
    int m_TutorialHits, m_TutorialObstacles, m_TutorialLifeLost, m_Jumps, m_Slides;
    readonly List<int> m_StepSequence = new List<int>();
    readonly List<int> m_LaneSequence = new List<int>();
    bool m_WasJumping, m_WasSliding;

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
        if (!scenarios) t.botReaction = 0.6f;
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

    // ---------------- Gözlem ve bot ----------------

    void Update()
    {
        var gm = GameManager.instance;
        if (gm == null) return;
        var gs = gm.topState as GameState;
        if (gs == null) return;
        Observe(gs, gs.trackManager);
        if (botEnabled) BotStep(gs, gs.trackManager);
    }

    void ResetObservation()
    {
        m_ObsStep = -2; m_TutorialHits = 0; m_TutorialObstacles = 0; m_TutorialLifeLost = 0; m_Jumps = 0; m_Slides = 0;
        m_StepSequence.Clear(); m_LaneSequence.Clear(); m_WasJumping = false; m_WasSliding = false;
    }

    void Observe(GameState gs, TrackManager tm)
    {
        var chr = tm.characterController;
        if (chr == null || chr.character == null || !gs.isTutorialRun) return;

        if (gs.tutorialStep != m_ObsStep) { m_ObsStep = gs.tutorialStep; m_ObsStepTime = Time.realtimeSinceStartup; m_StepSequence.Add(m_ObsStep); }
        if (m_LaneSequence.Count == 0 || m_LaneSequence[m_LaneSequence.Count - 1] != chr.currentLane) m_LaneSequence.Add(chr.currentLane);
        if (chr.isJumping && !m_WasJumping) m_Jumps++;
        if (chr.isSliding && !m_WasSliding) m_Slides++;
        m_WasJumping = chr.isJumping; m_WasSliding = chr.isSliding;
        if (chr.characterCollider.tutorialHitObstacle) { m_TutorialHits++; chr.characterCollider.tutorialHitObstacle = false; }
        if (chr.currentLife < chr.maxLife) m_TutorialLifeLost++;
        int obstacles = 0;
        for (int i = 0; i < tm.segments.Count; ++i) obstacles += tm.segments[i].objectRoot.GetComponentsInChildren<Obstacle>(true).Length;
        if (obstacles > m_TutorialObstacles) m_TutorialObstacles = obstacles;
    }

    void BotStep(GameState gs, TrackManager tm)
    {
        var chr = tm.characterController;
        if (chr == null || chr.character == null) return;

        if (gs.isTutorialRun)
        {
            int step = gs.tutorialStep;
            if (step >= 0 && step < k_Steps.Length && Time.realtimeSinceStartup - m_ObsStepTime >= botReaction)
                chr.HandleMoveInput(k_Steps[step]);
            return;
        }

        if (!tm.isMoving) return;

        // Normal koşu: önündeki engellerden kaçar.
        int myLane = chr.currentLane;
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

        yield return S1_StepByStepAndWrongMoves();
        yield return S2_NextRunIsNormal();
        yield return S3_DeletedSaveAbandonPauseRestart();
        yield return S4_DoubleFinish();
        yield return S5_DeathAndGameOverFlows();
        yield return S6_MenuReturnAndRestart();
        yield return S7_QuitMidJumpAndSlide();
        yield return S8_RapidInputs();
        yield return S9_Duration();
        yield return S10_IdleTutorialIsSafe();

        m_Phase = "son";
        foreach (var s in m_Summary) Out("OLCUM " + s);
        Out($"OZET gecti={m_Pass} kaldi={m_Fail} hata_logu={m_Errors}");
        foreach (var kv in m_PhaseErrors) Out($"HATALAR {kv.Key}: {kv.Value}");
        foreach (var e in m_ErrorSamples) Out("HATA " + e);
        Out("bitti");
        Application.Quit();
    }

    string Top() => GameManager.instance.topState.GetName();
    int Spawned() => s_TmSpawned != null ? (int)s_TmSpawned.GetValue(m_TM) : -1;
    CharacterInputController Chr => m_TM.characterController;

    static bool DiskTutorialDone()
    {
        string path = Application.persistentDataPath + "/save.bin";
        if (!File.Exists(path)) return false;
        byte[] b = File.ReadAllBytes(path);
        return b.Length > 4 && System.BitConverter.ToInt32(b, 0) >= 12 && b[b.Length - 1] != 0;
    }

    static TutorialOverlay Overlay() => FindAnyObjectByType<TutorialOverlay>(FindObjectsInactive.Include);
    static bool OverlayVisible() { var o = Overlay(); return o != null && o.gameObject.activeInHierarchy; }
    static string OverlayText()
    {
        var o = Overlay();
        if (o == null) return "";
        var t = o.transform.Find("Strip/Label");
        return t != null ? t.GetComponent<Text>().text : "";
    }

    IEnumerator StartFromLoadout()
    {
        float w = 0;
        while (!(GameManager.instance.topState is LoadoutState) && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(1f);
        ResetObservation();
        ((LoadoutState)GameManager.instance.topState).StartGame();
        yield return null;
    }

    IEnumerator WaitMoving(float timeout = 30f)
    {
        float w = 0;
        while (!m_TM.isMoving && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
    }

    IEnumerator WaitStep(int step, float timeout = 10f)
    {
        float w = 0;
        while (m_GS.isTutorialRun && m_GS.tutorialStep < step && Top() == "Game" && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
    }

    IEnumerator Frames(int n) { for (int i = 0; i < n; ++i) yield return null; }

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
        while (m_GS.isTutorialRun && Top() == "Game" && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
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

    string Pose() => $"serit={Chr.currentLane} zipliyor={Chr.isJumping} kayiyor={Chr.isSliding} adim={m_GS.tutorialStep}";

    // Adımın beklemediği hareketleri dener: hiçbiri adımı ilerletmemeli ve karakteri oynatmamalı.
    IEnumerator TryWrongMoves(int step)
    {
        int lane = Chr.currentLane;
        foreach (Move m in new[] { Move.Left, Move.Right, Move.Up, Move.Down })
        {
            if (m == k_Steps[step]) continue;
            Chr.HandleMoveInput(m);
            yield return Frames(3);
        }
        yield return new WaitForSecondsRealtime(0.3f);
        Check(m_GS.tutorialStep == step && Chr.currentLane == lane && !Chr.isJumping && !Chr.isSliding,
            $"adim {step}: yanlis hareketler adimi ilerletmedi ve karakteri oynatmadi ({Pose()})");
    }

    IEnumerator S1_StepByStepAndWrongMoves()
    {
        m_Phase = "S1-adim-adim";
        PlayerData.NewSave();
        Check(!PlayerData.instance.tutorialDone && !DiskTutorialDone(), "kayit sifirlandi (bellek ve disk tutorialDone=false)");
        yield return StartFromLoadout();

        // Geri sayım: kontrol yok, yönerge yok, karakter ortada.
        yield return new WaitForSecondsRealtime(0.5f);
        Check(m_GS.isTutorialRun && !m_TM.isMoving && m_GS.tutorialStep == -1 && Chr.currentLane == 1 && !OverlayVisible(),
            $"geri sayimda egitim hazir: orta serit, yonerge yok ({Pose()})");
        foreach (Move m in new[] { Move.Left, Move.Right, Move.Up, Move.Down }) { Chr.HandleMoveInput(m); Chr.ChangeLane(m == Move.Left ? -1 : 1); }
        yield return Frames(3);
        Check(Chr.currentLane == 1 && !Chr.isJumping && !Chr.isSliding, $"geri sayimda kontrolsuz hareket yok ({Pose()})");

        yield return WaitMoving();
        yield return null;
        Check(m_GS.tutorialStep == 0 && Chr.tutorialAllowedMove == Move.Right && Chr.currentLane == 1 && OverlayVisible() && OverlayText() == "SAĞA KAYDIR",
            $"kosu baslayinca ilk yonerge: '{OverlayText()}' izin={Chr.tutorialAllowedMove} ({Pose()})");

        // Adım 0: SAĞA
        yield return TryWrongMoves(0);
        Chr.HandleMoveInput(Move.Right);
        yield return null;
        Check(Chr.currentLane == 2 && m_GS.tutorialStep == 0, "saga kaydir: sag seride geciliyor, varmadan adim gecmiyor");
        yield return WaitStep(1);
        Check(m_GS.tutorialStep == 1 && Chr.currentLane == 2 && Mathf.Abs(Chr.offsetFromTarget.x) < 0.03f && OverlayText() == "SOLA KAYDIR",
            $"sag seride varinca adim 1 (SOLA) ({Pose()}) '{OverlayText()}'");

        // Adım 1: SOLA (orta şeride dönüş)
        yield return TryWrongMoves(1);
        Chr.HandleMoveInput(Move.Left);
        yield return null;
        Chr.HandleMoveInput(Move.Left); // ikinci sola kaydırma sol şeride geçirmemeli
        yield return WaitStep(2);
        Check(m_GS.tutorialStep == 2 && Chr.currentLane == 1 && OverlayText() == "YUKARI KAYDIR", $"sola kaydir: orta seride donuldu, ikinci sola yok sayildi ({Pose()})");

        // Adım 2: YUKARI (zıplama bitene kadar adım geçmez)
        yield return TryWrongMoves(2);
        Chr.HandleMoveInput(Move.Up);
        yield return null;
        Check(Chr.isJumping, "yukari kaydir: zipliyor");
        yield return new WaitForSecondsRealtime(0.25f);
        Check(Chr.isJumping && m_GS.tutorialStep == 2 && Chr.offsetFromTarget.y > 0.3f, $"havadayken adim gecmedi (y={Chr.offsetFromTarget.y:F2})");
        yield return WaitStep(3);
        Check(m_GS.tutorialStep == 3 && !Chr.isJumping && Chr.offsetFromTarget.y < 0.03f && OverlayText() == "AŞAĞI KAYDIR", $"yere inince adim 3 (ASAGI) ({Pose()})");

        // Adım 3: AŞAĞI (kayma bitene kadar adım geçmez)
        yield return TryWrongMoves(3);
        Chr.HandleMoveInput(Move.Down);
        yield return null;
        Check(Chr.isSliding, "asagi kaydir: egiliyor/kayiyor");
        yield return new WaitForSecondsRealtime(0.25f);
        Check(Chr.isSliding && m_GS.isTutorialRun, "kayarken egitim bitmedi");

        int loadoutVisits = 0;
        float w = 0;
        while (m_GS.isTutorialRun && w < 5f) { if (Top() == "Loadout") loadoutVisits++; w += Time.unscaledDeltaTime; yield return null; }
        float tDone = Time.realtimeSinceStartup;
        Check(!m_GS.isTutorialRun && !Chr.isSliding && Top() == "Game" && loadoutVisits == 0, "kayma bitince egitim tamamlandi, oyun Game durumunda (menu yok)");
        Check(OverlayVisible() && OverlayText() == "HAZIRSIN!", $"HAZIRSIN mesaji gosterildi ('{OverlayText()}')");
        Check(PlayerData.instance.tutorialDone && DiskTutorialDone(), "tutorialDone bellekte ve diskte true");
        Check(m_TutorialObstacles == 0 && m_TutorialHits == 0 && m_TutorialLifeLost == 0,
            $"egitim boyunca pistte engel yok, carpma yok, can kaybi yok (engel={m_TutorialObstacles} carpma={m_TutorialHits})");
        Check(m_Jumps == 1 && m_Slides == 1 && string.Join(",", m_LaneSequence) == "1,2,1",
            $"egitimde yalnizca istenen hareketler: serit {string.Join(",", m_LaneSequence)}, zipla={m_Jumps}, kay={m_Slides}");

        // HAZIRSIN kısa sürer, sonra arayüz kapanır.
        yield return new WaitForSecondsRealtime(2.0f);
        Check(!OverlayVisible(), "HAZIRSIN ~1,6 sn sonra kapandi");

        // Normal koşu: tüm hareketler serbest.
        Chr.HandleMoveInput(Move.Left); yield return Frames(15);
        bool left = Chr.currentLane == 0;
        Chr.HandleMoveInput(Move.Right); yield return Frames(15);
        bool right = Chr.currentLane == 1;
        Chr.HandleMoveInput(Move.Up); yield return null;
        bool up = Chr.isJumping;
        Chr.HandleMoveInput(Move.Down); yield return null;
        bool down = Chr.isSliding;
        Check(left && right && up && down, $"normal kosuda dort yon de calisiyor (sol={left} sag={right} yukari={up} asagi={down})");

        // Geçiş: engel ve altınlar gelir, karakter bot olmadan da geçişte çarpmaz.
        int lives = Chr.currentLife;
        float firstObstacleTime = -1f;
        while (Time.realtimeSinceStartup - tDone < 15f && Top() == "Game")
        {
            if (NearestObstacleAhead() < 10f) { firstObstacleTime = Time.realtimeSinceStartup - tDone; break; }
            yield return null;
        }
        Check(firstObstacleTime >= 0f, $"normal kosuda engeller geldi (ilk engel egitim bitisinden {firstObstacleTime:F1} sn sonra 10 m yakinda)");
        Check(Chr.currentLife == lives, "egitimden normal kosuya geciste carpma olmadi");
        m_Summary.Add($"egitim bitisinden ilk engelin 10 m yakina gelmesine: {firstObstacleTime:F1} sn");

        Chr.CheatInvincible(true);
        botEnabled = true;
        float speed0 = m_TM.speed;
        yield return new WaitForSecondsRealtime(20f);
        int activeCoins = 0;
        foreach (var c in FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) activeCoins++;
        Out($"normal kosu: hiz {speed0:F2}->{m_TM.speed:F2} parca={m_TM.segments.Count} aktif_altin={activeCoins} mesafe={m_TM.worldDistance:F0}");
        Check(Top() == "Game" && m_TM.isMoving, "20 sn sonra kosu kesintisiz devam ediyor");
        Check(m_TM.speed > speed0, "normal kosuda hizlanma basladi");
        Check(activeCoins > 0, "normal kosuda altin uretiliyor");
        Check(m_TM.segments.Count >= 9, "onde 9+ parca (normal kosu parca sayisi)");
        Chr.CheatInvincible(false);
        botEnabled = false;

        yield return QuitViaPauseMenu();
        Check(Top() == "Loadout", "duraklatma menusunden istenerek menuye donuldu");
        CheckClean("S1 cikis sonrasi");
    }

    float NearestObstacleAhead()
    {
        float nearest = float.MaxValue;
        Transform pivot = Chr.transform;
        for (int i = 0; i < m_TM.segments.Count; ++i)
            foreach (var o in m_TM.segments[i].objectRoot.GetComponentsInChildren<Obstacle>())
            {
                float z = pivot.InverseTransformPoint(o.transform.position).z;
                if (z > 0f && z < nearest) nearest = z;
            }
        return nearest;
    }

    IEnumerator S2_NextRunIsNormal()
    {
        m_Phase = "S2-sonraki-kosu";
        yield return StartFromLoadout();
        yield return WaitMoving();
        yield return null;
        Check(!m_TM.isTutorial && !m_GS.isTutorialRun && !OverlayVisible(), "sonraki kosu egitimsiz basladi, yonerge yok");
        Check(m_TM.newSegmentCreated == null && m_TM.currentSegementChanged == null, "tutorial geri cagrilari normal kosuda temiz");
        Chr.HandleMoveInput(Move.Left); yield return Frames(2);
        Check(Chr.currentLane == 0, "normal kosuda ilk hareket hemen kabul edildi");
        Chr.CheatInvincible(true);
        botEnabled = true;
        yield return new WaitForSecondsRealtime(10f);
        Check(Top() == "Game" && m_TM.isMoving, "normal kosu 10 sn devam etti");
        Chr.CheatInvincible(false);
        botEnabled = false;
        yield return QuitViaPauseMenu();
        CheckClean("S2 cikis sonrasi");
    }

    IEnumerator S3_DeletedSaveAbandonPauseRestart()
    {
        m_Phase = "S3-kayit-sil-yarida-birak";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        yield return null;
        Check(m_GS.isTutorialRun && m_GS.tutorialStep == 0, "kayit silindikten sonra egitim bastan basladi");

        botEnabled = true;
        yield return WaitStep(2);
        botEnabled = false;
        Out($"yarida birakma aninda: {Pose()}");
        yield return QuitViaPauseMenu();
        Check(Top() == "Loadout" && !PlayerData.instance.tutorialDone, "egitim yarida birakildi, tutorialDone hala false");
        CheckClean("S3 yarida birakma sonrasi");

        m_Phase = "S3-yeniden-baslat";
        yield return StartFromLoadout();
        yield return WaitMoving();
        yield return null;
        Check(m_GS.isTutorialRun && m_GS.tutorialStep == 0 && Chr.currentLane == 1 && OverlayText() == "SAĞA KAYDIR",
            $"yeniden baslatmada egitim ilk adimdan ve orta seritten ({Pose()})");

        m_Phase = "S3-duraklatma";
        m_GS.Pause();
        yield return null;
        Chr.HandleMoveInput(Move.Right);
        yield return new WaitForSecondsRealtime(1.0f);
        Check(AudioListener.pause && m_GS.pauseMenu.gameObject.activeSelf && !m_TM.isMoving && !OverlayVisible(), "duraklatmada oyun durdu ve yonerge gizlendi");
        Check(Chr.currentLane == 1 && m_GS.tutorialStep == 0, "duraklatma menusundeyken hareket kabul edilmedi");
        m_GS.Resume();
        yield return Frames(2);
        Check(!AudioListener.pause && m_TM.isMoving && OverlayVisible() && Chr.tutorialAllowedMove == Move.Right, "devam edince ayni adim ve yonerge geri geldi");
        Chr.HandleMoveInput(Move.Right);
        yield return WaitStep(1);
        Check(m_GS.tutorialStep == 1, "devamdan sonra adim tamamlandi");

        m_GS.SendMessage("OnApplicationFocus", false);
        yield return new WaitForSecondsRealtime(1.0f);
        Check(AudioListener.pause && m_GS.pauseMenu.gameObject.activeSelf, "odak kaybinda duraklatma menusu acildi ve kaldi");
        m_GS.Resume();
        yield return Frames(2);
        Check(m_TM.isMoving && m_GS.tutorialStep == 1 && Chr.currentLane == 2, $"odak kaybindan sonra devam, konum bozulmadi ({Pose()})");

        m_Phase = "S3-tamamla";
        yield return PlayUntilTutorialDone(30f);
        Check(!m_GS.isTutorialRun && Top() == "Game" && PlayerData.instance.tutorialDone, "yeniden baslatilan egitim tamamlandi, kosu devam ediyor");
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
        int premium0 = PlayerData.instance.premium;
        yield return StartFromLoadout();
        yield return WaitMoving();
        Chr.CheatInvincible(false);
        float w = 0;
        while (Top() != "GameOver" && w < 180f)
        {
            if (m_GS.gameOverPopup.activeSelf) m_GS.GameOver();
            w += Time.unscaledDeltaTime; yield return null;
        }
        Check(Top() == "GameOver", $"engele carpinca oyun sonu ekrani ({w:F0}s)");
        var go = GameManager.instance.FindState("GameOver") as GameOverState;
        go.RunAgain();
        yield return WaitMoving();
        Check(Top() == "Game" && m_TM.isMoving && !m_TM.isTutorial, "oyun sonundan tekrar kos (egitim acilmadi)");
        w = 0;
        while (Top() != "GameOver" && w < 180f) { if (m_GS.gameOverPopup.activeSelf) m_GS.GameOver(); w += Time.unscaledDeltaTime; yield return null; }
        go.GoToLoadout();
        yield return new WaitForSecondsRealtime(2f);
        Check(Top() == "Loadout", "oyun sonundan menuye");
        Check(PlayerData.instance.tutorialDone && DiskTutorialDone(), "oyun sonu ve kayit sonrasi tutorialDone hala true");
        CheckClean("S5 sonrasi");
    }

    IEnumerator S7_QuitMidJumpAndSlide()
    {
        botEnabled = false;
        for (int i = 0; i < 2; ++i)
        {
            bool slide = i == 1;
            m_Phase = slide ? "S7-kayarken-cikis" : "S7-ziplarken-cikis";
            yield return StartFromLoadout();
            yield return WaitMoving();
            Chr.CheatInvincible(true);
            yield return new WaitForSecondsRealtime(1f);
            float colliderY = Chr.characterCollider.collider.size.y;
            if (slide) Chr.Slide(); else Chr.Jump();
            yield return null;
            Check(slide ? Chr.isSliding : Chr.isJumping, slide ? "kayma basladi" : "ziplama basladi");
            yield return QuitViaPauseMenu();
            int before = m_Errors;
            yield return StartFromLoadout();
            yield return WaitMoving();
            yield return new WaitForSecondsRealtime(2f);
            Check(m_Errors == before, $"yeni kosu hatasiz basladi (yeni hata={m_Errors - before})");
            Check(!Chr.isJumping && !Chr.isSliding && Mathf.Abs(Chr.characterCollider.collider.size.y - colliderY) < 0.001f,
                $"yeni kosu yerde ve carpisma kutusu normal (y={Chr.characterCollider.collider.size.y:F2}, once={colliderY:F2})");
            Chr.CheatInvincible(false);
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
        Chr.CheatInvincible(true);
        yield return new WaitForSecondsRealtime(15f);
        Check(Top() == "Game" && m_TM.isMoving && m_TM.segments.Count >= 9, $"tekrarlardan sonra normal kosu (parca={m_TM.segments.Count})");
        Chr.CheatInvincible(false);
        yield return QuitViaPauseMenu();
        CheckClean("S6 son");
    }

    // Oyuncu dört hareketi beklemeden, her karede art arda gönderir: adımlar sırayla ve birer kez ilerlemeli.
    IEnumerator S8_RapidInputs()
    {
        m_Phase = "S8-hizli-art-arda";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        yield return null;

        float t0 = Time.realtimeSinceStartup;
        int frames = 0;
        while (m_GS.isTutorialRun && Time.realtimeSinceStartup - t0 < 10f)
        {
            // Her karede dört yön de (doğru sıra dahil) gönderilir.
            foreach (Move m in k_Steps) Chr.HandleMoveInput(m);
            frames++;
            yield return null;
        }
        float dur = Time.realtimeSinceStartup - t0;
        string steps = string.Join(",", m_StepSequence);
        Check(!m_GS.isTutorialRun && PlayerData.instance.tutorialDone, $"art arda girdiyle egitim tamamlandi ({dur:F2} sn, {frames} kare)");
        // Son adım tamamlandığı karede eğitim de biter; gözlem eğitim süresince yapıldığı için dizi 3'te biter.
        Check(steps == "-1,0,1,2,3" || steps == "0,1,2,3", $"adimlar sirayla, atlanmadan ilerledi ({steps})");
        Check(string.Join(",", m_LaneSequence) == "1,2,1" && m_Jumps == 1 && m_Slides == 1,
            $"her hareket bir kez yapildi: serit {string.Join(",", m_LaneSequence)}, zipla={m_Jumps}, kay={m_Slides}");
        m_Summary.Add($"en hizli egitim (her karede dogru girdi): {dur:F2} sn");
        yield return QuitViaPauseMenu();
        CheckClean("S8 cikis sonrasi");
    }

    // Toplam süre: insan gibi tepki süreleriyle (yönergeden sonra 0,5 / 1,0 sn bekleyerek).
    IEnumerator S9_Duration()
    {
        foreach (float reaction in new[] { 0.5f, 1.0f })
        {
            m_Phase = $"S9-sure-tepki-{reaction:F1}";
            PlayerData.NewSave();
            yield return StartFromLoadout();
            float tStart = Time.realtimeSinceStartup;
            yield return WaitMoving();
            float tMoving = Time.realtimeSinceStartup;
            botReaction = reaction;
            yield return PlayUntilTutorialDone(30f);
            botEnabled = false;
            botReaction = 0f;
            float tDone = Time.realtimeSinceStartup;
            Check(!m_GS.isTutorialRun && Top() == "Game", $"egitim tamamlandi (tepki {reaction:F1} sn)");
            float total = tDone - tMoving + 1.6f; // + HAZIRSIN
            m_Summary.Add($"tepki {reaction:F1} sn: geri sayim {tMoving - tStart:F1} sn + dort adim {tDone - tMoving:F1} sn + HAZIRSIN 1,6 sn = koşu başından {total:F1} sn");
            Check(total <= 15f, $"egitim (HAZIRSIN dahil) 15 sn icinde: {total:F1} sn");
            yield return QuitViaPauseMenu();
            CheckClean($"S9 tepki {reaction:F1} sonrasi");
        }
    }

    // Oyuncu hiçbir şey yapmazsa: koşu sürer, eğitim ilk adımda bekler, pistte engel yok, çarpma yok.
    IEnumerator S10_IdleTutorialIsSafe()
    {
        m_Phase = "S10-bekleyen-oyuncu";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        yield return new WaitForSecondsRealtime(20f);
        Check(m_GS.isTutorialRun && m_GS.tutorialStep == 0 && Chr.currentLane == 1 && m_TM.isMoving && OverlayVisible(),
            $"20 sn hareketsiz: egitim ilk adimda bekliyor, kosu suruyor ({Pose()}, mesafe={m_TM.worldDistance:F0} m)");
        Check(m_TutorialObstacles == 0 && m_TutorialHits == 0 && Chr.currentLife == Chr.maxLife,
            $"bekleme boyunca pistte engel yok, carpma yok (engel={m_TutorialObstacles} carpma={m_TutorialHits})");
        yield return QuitViaPauseMenu();
        CheckClean("S10 cikis sonrasi");
    }
}
#endif
