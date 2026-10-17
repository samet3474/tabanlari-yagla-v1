#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Tutorial otomatik testi: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir.
//   Masaüstü: "-tutorialTest" -> tüm senaryolar (sonuçlar "TT|" önekiyle günlüğe yazılır, sonunda uygulama kapanır)
//   Web:      "?autotest=tutorial" -> yalnızca bot tutorial'ı oynar (START/Run'a elle basılır)
// Bot eğitim hareketlerini oyunun kendi girdi kapısından (CharacterInputController.HandleMoveInput) verir: klavye ve
// dokunmatik kolun çağırdığı yol budur, yani yanlış/erken hareketler gerçek oyuncudaki gibi yok sayılır veya tutulur.
// Çarpışmalar oyunun kendi fiziğiyle olur (hiçbir senaryoda eğitim sırasında dokunulmazlık açılmaz).
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
    static readonly string[] k_ExpectedObstacles = { "ObstacleBin", "ObstacleBin", "ObstacleLowBarrier", "ObstacleHighBarrier" };

    bool m_ScenarioMode;
    public bool botEnabled = true;
    public float botReaction = 0.0f; // eğitimde yönerge göründükten sonra hareket etmeden önce bekleme (gerçek zaman, sn)

    string m_Phase = "boot";
    int m_Errors;
    readonly Dictionary<string, int> m_PhaseErrors = new Dictionary<string, int>();
    readonly List<string> m_ErrorSamples = new List<string>();
    int m_Pass, m_Fail;
    readonly List<string> m_Summary = new List<string>();

    GameState m_GS;
    TrackManager m_TM;

    // ---- Eğitim boyunca her karede gözlenenler ----
    class Crossing { public int step, attempt, lane; public float y; public bool jumping, sliding, hitBefore; public string obstacle; }
    int m_ObsStep = -2, m_ObsAttempt = -1;
    bool m_ObsPrompt;
    float m_PromptTime;
    readonly List<int> m_StepSequence = new List<int>();
    readonly List<int> m_LaneSequence = new List<int>();
    readonly List<float> m_PlacementDistances = new List<float>();
    readonly List<float> m_PromptDistances = new List<float>();
    readonly List<Crossing> m_Crossings = new List<Crossing>();
    readonly List<string> m_PlacedNames = new List<string>();
    readonly List<float> m_PlacedLaneX = new List<float>();
    GameObject m_ObsObstacle;
    float m_ObsLastZ = float.MaxValue;
    int m_Jumps, m_Slides, m_LifeLost, m_RandomObstacles;
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
        m_ObsStep = -2; m_ObsAttempt = -1; m_ObsPrompt = false; m_ObsObstacle = null; m_ObsLastZ = float.MaxValue;
        m_StepSequence.Clear(); m_LaneSequence.Clear(); m_PlacementDistances.Clear(); m_PromptDistances.Clear();
        m_Crossings.Clear(); m_PlacedNames.Clear(); m_PlacedLaneX.Clear();
        m_Jumps = m_Slides = m_LifeLost = m_RandomObstacles = 0; m_WasJumping = m_WasSliding = false;
    }

    void Observe(GameState gs, TrackManager tm)
    {
        var chr = tm.characterController;
        if (chr == null || chr.character == null || !gs.isTutorialRun) return;
        int step = gs.tutorialStep;
        if (step < 0 || step >= gs.tutorialStepCount) return;

        if (step != m_ObsStep || gs.tutorialAttempts != m_ObsAttempt)
        {
            if (step != m_ObsStep) m_StepSequence.Add(step);
            m_ObsStep = step; m_ObsAttempt = gs.tutorialAttempts; m_ObsPrompt = false; m_ObsObstacle = null; m_ObsLastZ = float.MaxValue;
        }
        if (m_LaneSequence.Count == 0 || m_LaneSequence[m_LaneSequence.Count - 1] != chr.currentLane) m_LaneSequence.Add(chr.currentLane);
        if (chr.isJumping && !m_WasJumping) m_Jumps++;
        if (chr.isSliding && !m_WasSliding) m_Slides++;
        m_WasJumping = chr.isJumping; m_WasSliding = chr.isSliding;
        if (chr.currentLife < chr.maxLife) m_LifeLost++;

        // Eğitim engelleri dışında pistte engel var mı? (rastgele üretim kapalı olmalı)
        int all = 0;
        for (int i = 0; i < tm.segments.Count; ++i) all += tm.segments[i].objectRoot.GetComponentsInChildren<Obstacle>(true).Length;
        int own = 0;
        for (int i = 0; i < gs.tutorialStepCount; ++i) if (gs.GetTutorialObstacle(i) != null) own++;
        if (all > own + 2) m_RandomObstacles = Mathf.Max(m_RandomObstacles, all - own); // +2: çarpılıp geride kalanlar

        var obstacle = gs.GetTutorialObstacle(step);
        if (obstacle != null && obstacle != m_ObsObstacle)
        {
            m_ObsObstacle = obstacle;
            m_PlacementDistances.Add(gs.GetTutorialObstacleDistance(step));
            m_PlacedNames.Add(obstacle.name.Replace("(Clone)", "").Trim());
            m_PlacedLaneX.Add(chr.transform.InverseTransformPoint(obstacle.transform.position).x);
        }
        if (gs.tutorialPromptVisible && !m_ObsPrompt)
        {
            m_ObsPrompt = true; m_PromptTime = Time.realtimeSinceStartup;
            m_PromptDistances.Add(gs.GetTutorialObstacleDistance(step));
            m_ShotNear = m_ShotCross = false;
            Shot($"adim{step}-a-yonerge");
        }
        if (m_ShotDir != null && m_ObsPrompt)
        {
            float d = gs.GetTutorialObstacleDistance(step);
            if (!m_ShotNear && d <= 5f) { m_ShotNear = true; Shot($"adim{step}-b-5m"); }
            if (!m_ShotCross && d <= 0.3f) { m_ShotCross = true; Shot($"adim{step}-c-gecis"); }
        }
        // Engelin önünden geçtiği an (karakter yerel z'si pozitiften sıfıra/negatife): o anki duruş.
        if (m_ObsObstacle != null)
        {
            float z = chr.transform.InverseTransformPoint(m_ObsObstacle.transform.position).z;
            if (m_ObsLastZ > 0f && z <= 0f)
                m_Crossings.Add(new Crossing { step = step, attempt = gs.tutorialAttempts, lane = chr.currentLane, y = chr.characterCollider.transform.localPosition.y,
                    jumping = chr.isJumping, sliding = chr.isSliding, hitBefore = chr.characterCollider.tutorialHitObstacle, obstacle = m_ObsObstacle.name });
            m_ObsLastZ = z;
        }
    }

    string m_ShotDir;
    bool m_ShotNear, m_ShotCross;
    void Shot(string name)
    {
        if (m_ShotDir == null) return;
        ScreenCapture.CaptureScreenshot(Path.Combine(m_ShotDir, name + ".png"));
        Out("GORUNTU " + name);
    }

    void BotStep(GameState gs, TrackManager tm)
    {
        var chr = tm.characterController;
        if (chr == null || chr.character == null) return;

        if (gs.isTutorialRun)
        {
            int step = gs.tutorialStep;
            if (step >= 0 && step < k_Steps.Length && gs.tutorialPromptVisible && Time.realtimeSinceStartup - m_PromptTime >= botReaction)
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
                if (n.Contains("LowBarrier")) { if (local.z < 1.6f && !chr.isJumping) chr.Jump(); }
                else if (n.Contains("HighBarrier")) { if (local.z < 1.8f && !chr.isSliding) chr.Slide(); }
                else if (o is PatrollingObstacle) { if (local.z < 1.6f && !chr.isJumping) chr.Jump(); }
                else blocked[Mathf.Clamp(Mathf.RoundToInt(local.x / tm.laneOffset) + 1, 0, 2)] = true;
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

        // Başlık ekranı artık kendiliğinden ana menüye geçer (StartButton.autoStart); zorla yüklemek çift yükleme olur.
        float wl = 0;
        while (SceneManager.GetActiveScene().name != "Main" && wl < 30f) { wl += Time.unscaledDeltaTime; yield return null; }
        if (SceneManager.GetActiveScene().name != "Main") { Out("UYARI: otomatik gecis olmadi, Main elle yukleniyor"); SceneManager.LoadScene("Main"); }
        float w = 0;
        while (!(GameManager.instance != null && GameManager.instance.topState is LoadoutState && CharacterDatabase.loaded && ThemeDatabase.loaded) && w < 60f)
        { w += Time.unscaledDeltaTime; yield return null; }
        if (w >= 60f) { Out("HATA: Loadout hazir olmadi"); Application.Quit(); yield break; }
        yield return new WaitForSecondsRealtime(1f);

        m_GS = GameManager.instance.FindState("Game") as GameState;
        m_TM = m_GS.trackManager;

        // "-tutorialShots <klasör>": yalnızca S1, her adımda ekran görüntüsü (pencereli çalıştırılmalı; -batchmode'da çizim yok).
        var args = System.Environment.GetCommandLineArgs();
        int shots = System.Array.IndexOf(args, "-tutorialShots");
        if (shots >= 0 && shots + 1 < args.Length)
        {
            m_ShotDir = args[shots + 1];
            Directory.CreateDirectory(m_ShotDir);
            yield return S1_RealObstaclesStepByStep();
            Out("bitti");
            Application.Quit();
            yield break;
        }

        yield return S1_RealObstaclesStepByStep();
        yield return S2_NextRunIsNormal();
        yield return S3_DeletedSaveAbandonPauseRestart();
        yield return S4_DoubleFinish();
        yield return S5_DeathSecondWindGameOver();
        yield return S6_MenuReturnAndRestart();
        yield return S7_QuitMidJumpAndSlide();
        yield return S8_RapidInputs();
        yield return S9_Duration();
        yield return S10_CrashAndRetry();
        yield return S11_IdlePlayerNeverLocks();

        m_Phase = "son";
        foreach (var s in m_Summary) Out("OLCUM " + s);
        Out($"OZET gecti={m_Pass} kaldi={m_Fail} hata_logu={m_Errors}");
        foreach (var kv in m_PhaseErrors) Out($"HATALAR {kv.Key}: {kv.Value}");
        foreach (var e in m_ErrorSamples) Out("HATA " + e);
        Out("bitti");
        Time.timeScale = 1f;
        Application.Quit();
    }

    string Top() => GameManager.instance.topState.GetName();
    int Spawned() => s_TmSpawned != null ? (int)s_TmSpawned.GetValue(m_TM) : -1;
    CharacterInputController Chr => m_TM.characterController;
    float Dist(int step) => m_GS.GetTutorialObstacleDistance(step);

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
        if (o == null || !o.gameObject.activeInHierarchy) return "";
        var t = o.transform.Find("Strip/Label");
        return t != null ? t.GetComponent<Text>().text : "";
    }
    static bool ArrowVisible() { var o = Overlay(); var a = o != null ? o.transform.Find("Arrow") : null; return a != null && a.gameObject.activeInHierarchy; }

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

    IEnumerator WaitUntil(System.Func<bool> cond, float timeout = 20f)
    {
        float w = 0;
        while (!cond() && Top() == "Game" && w < timeout) { w += Time.unscaledDeltaTime; yield return null; }
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
        Out($"TEMIZLIK {when}: sayac={Spawned()} liste={m_TM.segments.Count} parca_nesnesi={segs} yetim_engel={orphanObs} isMoving={m_TM.isMoving} timeScale={Time.timeScale}");
        Check(Spawned() == 0 && m_TM.segments.Count == 0 && segs == 0 && !m_TM.isMoving && Time.timeScale == 1f, $"{when}: sayac/liste/parca sifir, hareket durdu, zaman olcegi 1");
    }

    string Pose() => $"serit={Chr.currentLane} zipliyor={Chr.isJumping} kayiyor={Chr.isSliding} adim={m_GS.tutorialStep} deneme={m_GS.tutorialAttempts}";

    // Adımın beklemediği hareketleri dener: hiçbiri adımı ilerletmemeli ve karakteri oynatmamalı.
    IEnumerator TryWrongMoves(int step)
    {
        int lane = Chr.currentLane;
        foreach (Move m in new[] { Move.Left, Move.Right, Move.Up, Move.Down })
        {
            if (m == k_Steps[step]) continue;
            Chr.HandleMoveInput(m);
            yield return null;
        }
        yield return null;
        Check(m_GS.tutorialStep == step && Chr.currentLane == lane && !Chr.isJumping && !Chr.isSliding && Chr.tutorialHeldMove == Move.None,
            $"adim {step}: yanlis hareketler adimi ilerletmedi ve karakteri oynatmadi ({Pose()})");
    }

    // Bir engel adımı: yönergeyi bekler, yanlış hareketleri dener, doğru hareketi verir; engel geçilmeden adımın
    // geçmediğini ve geçişteki duruşu doğrular.
    IEnumerator DoStep(int step, float inputAtDistance)
    {
        yield return WaitUntil(() => m_GS.tutorialPromptVisible || m_GS.tutorialStep != step, 20f);
        Check(m_GS.tutorialStep == step && m_GS.tutorialPromptVisible, $"adim {step}: yonerge acildi ('{OverlayText()}', engele {Dist(step):F1} m)");
        yield return TryWrongMoves(step);
        yield return WaitUntil(() => Dist(step) <= inputAtDistance, 10f);
        Chr.HandleMoveInput(k_Steps[step]);
        float inputDist = Dist(step);
        yield return null;
        yield return WaitUntil(() => m_GS.tutorialMoveStarted, 5f);
        float startDist = Dist(step);
        Check(m_GS.tutorialMoveStarted && m_GS.tutorialStep == step && Dist(step) > 0f,
            $"adim {step}: dogru hareket basladi (giris {inputDist:F1} m, hareket {startDist:F1} m), engel henuz gecilmedigi icin adim ilerlemedi");
        // Hareket bitip engel geçilene kadar adım ilerlememeli.
        while (m_GS.tutorialStep == step && Dist(step) > -1.0f && Top() == "Game")
        {
            if (m_GS.tutorialStep != step) break;
            yield return null;
        }
        bool stillHere = m_GS.tutorialStep == step;
        yield return WaitUntil(() => m_GS.tutorialStep != step || !m_GS.isTutorialRun, 5f);
        Check(stillHere, $"adim {step}: engel karakterin hemen arkasindayken adim hala acik (gecis ve hareketin bitmesi bekleniyor)");
    }

    Crossing LastCrossing(int step) { for (int i = m_Crossings.Count - 1; i >= 0; --i) if (m_Crossings[i].step == step) return m_Crossings[i]; return null; }

    IEnumerator S1_RealObstaclesStepByStep()
    {
        m_Phase = "S1-gercek-engeller";
        PlayerData.NewSave();
        Check(!PlayerData.instance.tutorialDone && !DiskTutorialDone(), "kayit sifirlandi (bellek ve disk tutorialDone=false)");
        yield return StartFromLoadout();

        // Geri sayım: karakter ortada, kontrol yok; ilk engel (çöp kutusu) orta şeritte, önde ve görünür.
        yield return WaitUntil(() => m_GS.GetTutorialObstacle(0) != null, 10f);
        var first = m_GS.GetTutorialObstacle(0);
        float firstX = first != null ? Chr.transform.InverseTransformPoint(first.transform.position).x : 99f;
        Check(!m_TM.isMoving && Chr.currentLane == 1 && !OverlayVisible(), $"1) egitim orta seritte basliyor, geri sayimda yonerge yok ({Pose()})");
        Check(first != null && first.name.Contains("ObstacleBin") && Mathf.Abs(firstX) < 0.2f && Dist(0) > 20f,
            $"2) ilk engel cop kutusu, orta seritte (x={firstX:F2}), {Dist(0):F1} m onde ({(first != null ? first.name : "yok")})");
        foreach (Move m in new[] { Move.Left, Move.Right, Move.Up, Move.Down }) Chr.HandleMoveInput(m);
        yield return Frames(3);
        Check(Chr.currentLane == 1 && !Chr.isJumping && !Chr.isSliding, $"geri sayimda kontrolsuz hareket yok ({Pose()})");

        yield return WaitMoving();
        // Adım 1: SAĞA (sağ şeride geçiş yönergeden hemen sonra, 10 m kala)
        yield return DoStep(0, 10f);
        var c0 = LastCrossing(0);
        Check(c0 != null && c0.lane == 2 && !c0.hitBefore && m_GS.tutorialStep == 1 && m_GS.tutorialFailures == 0,
            $"3) saga kaydirma: karakter engelin yanindan sag seritte gecti, carpma yok ({(c0 != null ? "serit " + c0.lane : "gecis yok")})");

        // Adım 2: ikinci çöp kutusu sağ şeritte
        yield return WaitUntil(() => m_GS.GetTutorialObstacle(1) != null, 10f);
        var second = m_GS.GetTutorialObstacle(1);
        float secondX = second != null ? Chr.transform.InverseTransformPoint(second.transform.position).x : 99f;
        Check(second != null && second.name.Contains("ObstacleBin") && Mathf.Abs(secondX - m_TM.laneOffset) < 0.25f && Chr.currentLane == 2,
            $"4) ikinci cop kutusu sag seritte (x={secondX:F2}), oyuncu sag seritte");
        yield return DoStep(1, 10f);
        var c1 = LastCrossing(1);
        Check(c1 != null && c1.lane == 1 && !c1.hitBefore && m_GS.tutorialStep == 2 && m_GS.tutorialFailures == 0,
            $"5) sola kaydirma: orta seride donuldu, engel carpmadan gecildi ({(c1 != null ? "serit " + c1.lane : "gecis yok")})");

        // Adım 3: alçak bariyer; yukarı kaydırma erken (12 m) yapılır: tutulur ve doğru anda zıplanır.
        yield return WaitUntil(() => m_GS.GetTutorialObstacle(2) != null, 10f);
        var third = m_GS.GetTutorialObstacle(2);
        Check(third != null && third.name.Contains("ObstacleLowBarrier"), $"ziplama engeli alcak bariyer ({(third != null ? third.name : "yok")})");
        yield return WaitUntil(() => m_GS.tutorialPromptVisible && m_GS.tutorialStep == 2, 20f);
        yield return TryWrongMoves(2);
        Chr.HandleMoveInput(Move.Up);
        yield return null;
        Check(Chr.tutorialHeldMove == Move.Up && !Chr.isJumping, $"erken yukari kaydirma ({Dist(2):F1} m) tutuldu, hemen ziplanmadi");
        yield return WaitUntil(() => Chr.isJumping, 5f);
        float jumpAt = Dist(2);
        Check(Chr.isJumping && jumpAt > 2.5f && jumpAt <= 3.7f, $"tutulan zipla engelin asilacagi mesafede yapildi ({jumpAt:F1} m)");
        yield return WaitUntil(() => Dist(2) < -0.5f, 5f);
        Check(m_GS.tutorialStep == 2 && Chr.isJumping, $"9) engelin ustunden gecildi ama karakter havada: adim ilerlemedi ({Pose()})");
        yield return WaitUntil(() => m_GS.tutorialStep != 2, 5f);
        var c2 = LastCrossing(2);
        Check(c2 != null && c2.jumping && c2.y > 0.62f && !c2.hitBefore && m_GS.tutorialStep == 3 && m_GS.tutorialFailures == 0,
            $"6) zipla: engel gercekten ziplanarak asildi (gecisteki yukseklik {(c2 != null ? c2.y : -1f):F2} m > bariyer 0,61 m), yere inince adim gecti");

        // Adım 4: yüksek bariyer
        yield return WaitUntil(() => m_GS.GetTutorialObstacle(3) != null, 10f);
        var fourth = m_GS.GetTutorialObstacle(3);
        Check(fourth != null && fourth.name.Contains("ObstacleHighBarrier"), $"egilme engeli yuksek bariyer ({(fourth != null ? fourth.name : "yok")})");
        yield return DoStep(3, 8f);
        var c3 = LastCrossing(3);
        Check(c3 != null && c3.sliding && !c3.hitBefore && m_GS.tutorialFailures == 0,
            $"7) asagi kaydir: engelin altindan kayarak gecildi (gecis aninda kayiyor={(c3 != null && c3.sliding)})");

        Check(!m_GS.isTutorialRun && Top() == "Game" && OverlayVisible() && OverlayText() == "HAZIRSIN!", $"12) dort engel gecildi, HAZIRSIN gosterildi ('{OverlayText()}')");
        yield return new WaitForSecondsRealtime(0.3f);
        Shot("hazirsin");
        Check(PlayerData.instance.tutorialDone && DiskTutorialDone(), "egitim tamamlandi bilgisi bellekte ve diskte");
        Check(m_LifeLost == 0 && m_RandomObstacles == 0, $"egitimde can kaybi yok, rastgele engel yok (rastgele={m_RandomObstacles})");
        Check(m_PlacementDistances.Count == 4 && m_PlacementDistances.TrueForAll(d => d >= 25f),
            $"engeller ani degil, uzakta yerlesti: {string.Join(", ", m_PlacementDistances.ConvertAll(d => d.ToString("F0") + " m"))}");
        Check(m_PromptDistances.TrueForAll(d => d > 12.5f && d <= 14.1f), $"yonergeler engele ~14 m kala: {string.Join(", ", m_PromptDistances.ConvertAll(d => d.ToString("F1")))}");
        m_Summary.Add($"S1 engellerin yerlesme mesafeleri: {string.Join(", ", m_PlacementDistances.ConvertAll(d => d.ToString("F0") + " m"))}; adlar: {string.Join(", ", m_PlacedNames)}; yanal x: {string.Join(", ", m_PlacedLaneX.ConvertAll(x => x.ToString("F2")))}");

        // 13) Normal koşu kesintisiz
        float tDone = Time.realtimeSinceStartup;
        yield return new WaitForSecondsRealtime(2.0f);
        Check(!OverlayVisible() && Top() == "Game" && m_TM.isMoving && Time.timeScale == 1f, "13) HAZIRSIN kapandi, ayni kosu normal hizda suruyor (menu yok)");
        Chr.HandleMoveInput(Move.Left); yield return Frames(15);
        bool left = Chr.currentLane == 0;
        Chr.HandleMoveInput(Move.Right); yield return Frames(15);
        bool right = Chr.currentLane == 1;
        Chr.HandleMoveInput(Move.Up); yield return null;
        bool up = Chr.isJumping;
        Chr.HandleMoveInput(Move.Down); yield return null;
        bool down = Chr.isSliding;
        Check(left && right && up && down, $"normal kosuda dort yon de calisiyor (sol={left} sag={right} yukari={up} asagi={down})");

        // 14) Normal engel ve altın üretimi
        botEnabled = true;
        int lives = Chr.currentLife;
        float firstNormal = -1f;
        while (Time.realtimeSinceStartup - tDone < 15f && Top() == "Game")
        {
            if (firstNormal < 0f && NearestObstacleAhead() < 10f) firstNormal = Time.realtimeSinceStartup - tDone;
            if (firstNormal >= 0f) break;
            yield return null;
        }
        Check(firstNormal >= 0f, $"14) normal engeller geldi (egitim bitisinden {firstNormal:F1} sn sonra 10 m yakinda)");
        m_Summary.Add($"egitim bitisinden ilk normal engelin 10 m yakina gelmesi: {firstNormal:F1} sn");
        Chr.CheatInvincible(true);
        float speed0 = m_TM.speed;
        yield return new WaitForSecondsRealtime(15f);
        int activeCoins = 0;
        foreach (var c in FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) activeCoins++;
        Check(Top() == "Game" && m_TM.isMoving && m_TM.speed > speed0 && activeCoins > 0 && m_TM.segments.Count >= 9 && m_TM.score > 0,
            $"14) normal kosu: hizlanma {speed0:F1}->{m_TM.speed:F1}, altin={activeCoins}, parca={m_TM.segments.Count}, skor={m_TM.score}");
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
                if (z > 0.5f && z < nearest) nearest = z;
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
        Check(m_GS.isTutorialRun && m_GS.tutorialStep == 0, "kayit silindikten sonra egitim bastan basladi");
        botEnabled = true;
        yield return WaitUntil(() => m_GS.tutorialStep >= 2, 30f);
        botEnabled = false;
        Out($"yarida birakma aninda: {Pose()}");
        yield return QuitViaPauseMenu();
        Check(Top() == "Loadout" && !PlayerData.instance.tutorialDone, "egitim yarida birakildi, tutorialDone hala false");
        CheckClean("S3 yarida birakma sonrasi");

        m_Phase = "S3-yeniden-baslat";
        yield return StartFromLoadout();
        yield return WaitUntil(() => m_GS.GetTutorialObstacle(0) != null, 10f);
        Check(m_GS.isTutorialRun && m_GS.tutorialStep == 0 && m_GS.tutorialAttempts == 1 && Chr.currentLane == 1 && m_GS.GetTutorialObstacle(1) == null,
            $"yeniden baslatmada egitim ilk adim/ilk engelden, orta seritten ({Pose()})");

        m_Phase = "S3-duraklatma";
        yield return WaitMoving();
        yield return WaitUntil(() => m_GS.tutorialPromptVisible, 10f);
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
        yield return WaitUntil(() => m_GS.tutorialStep >= 1, 10f);
        Check(m_GS.tutorialStep == 1 && m_GS.tutorialFailures == 0, "devamdan sonra engel gecildi, adim tamamlandi");

        m_GS.SendMessage("OnApplicationFocus", false);
        yield return new WaitForSecondsRealtime(1.0f);
        Check(AudioListener.pause && m_GS.pauseMenu.gameObject.activeSelf, "odak kaybinda duraklatma menusu acildi ve kaldi");
        m_GS.Resume();
        yield return Frames(2);
        Check(m_TM.isMoving && m_GS.tutorialStep == 1 && Chr.currentLane == 2, $"odak kaybindan sonra devam, konum bozulmadi ({Pose()})");

        m_Phase = "S3-tamamla";
        yield return PlayUntilTutorialDone(60f);
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

    IEnumerator S5_DeathSecondWindGameOver()
    {
        m_Phase = "S5-olum-ikinci-sans-oyunsonu";
        botEnabled = false;
        int coins0 = PlayerData.instance.coins;
        yield return StartFromLoadout();
        yield return WaitMoving();
        Check(!m_TM.isTutorial, "egitim bittikten sonra kosu egitimsiz");
        Chr.CheatInvincible(false);
        float w = 0;
        while (!m_GS.gameOverPopup.activeSelf && w < 180f) { w += Time.unscaledDeltaTime; yield return null; }
        Check(m_GS.gameOverPopup.activeSelf, $"uc carpmada can bitti, ikinci sans penceresi acildi ({w:F0}s)");
        float dist0 = m_TM.worldDistance;
        m_GS.SecondWind();
        yield return WaitMoving();
        yield return new WaitForSecondsRealtime(1f);
        Check(Top() == "Game" && m_TM.isMoving && !m_TM.isTutorial && Chr.currentLife == 1 && m_TM.worldDistance >= dist0,
            $"ikinci sans: ayni kosu 1 canla devam etti, egitim acilmadi (mesafe {dist0:F0}->{m_TM.worldDistance:F0})");
        w = 0;
        while (Top() != "GameOver" && w < 180f) { if (m_GS.gameOverPopup.activeSelf) m_GS.GameOver(); w += Time.unscaledDeltaTime; yield return null; }
        Check(Top() == "GameOver", "ikinci olumde oyun sonu ekrani");
        var go = GameManager.instance.FindState("GameOver") as GameOverState;
        go.RunAgain();
        yield return WaitMoving();
        Check(Top() == "Game" && m_TM.isMoving && !m_TM.isTutorial, "oyun sonundan tekrar kos (egitim acilmadi)");
        w = 0;
        while (Top() != "GameOver" && w < 180f) { if (m_GS.gameOverPopup.activeSelf) m_GS.GameOver(); w += Time.unscaledDeltaTime; yield return null; }
        go.GoToLoadout();
        yield return new WaitForSecondsRealtime(2f);
        Check(Top() == "Loadout", "oyun sonundan menuye");
        Check(PlayerData.instance.tutorialDone && DiskTutorialDone() && PlayerData.instance.coins >= coins0, $"kayit korunuyor: tutorialDone=true, altin {coins0}->{PlayerData.instance.coins}");
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

    // 10) Oyuncu dört yönü beklemeden, her karede art arda gönderir: adımlar sırayla, birer kez ve çarpmadan ilerlemeli.
    IEnumerator S8_RapidInputs()
    {
        m_Phase = "S8-hizli-art-arda";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        float t0 = Time.realtimeSinceStartup;
        while (m_GS.isTutorialRun && Time.realtimeSinceStartup - t0 < 60f)
        {
            foreach (Move m in k_Steps) Chr.HandleMoveInput(m);
            yield return null;
        }
        string steps = string.Join(",", m_StepSequence);
        Check(!m_GS.isTutorialRun && PlayerData.instance.tutorialDone, $"art arda girdiyle egitim tamamlandi ({Time.realtimeSinceStartup - t0:F1} sn, geri sayim dahil)");
        Check(steps == "0,1,2,3", $"adimlar sirayla, atlanmadan ilerledi ({steps})");
        Check(string.Join(",", m_LaneSequence) == "1,2,1" && m_Jumps == 1 && m_Slides == 1 && m_GS.tutorialFailures == 0,
            $"her hareket bir kez, carpma yok: serit {string.Join(",", m_LaneSequence)}, zipla={m_Jumps}, kay={m_Slides}, carpma={m_GS.tutorialFailures}");
        int crossedOk = 0;
        foreach (var c in m_Crossings) if (!c.hitBefore && ((c.step == 0 && c.lane == 2) || (c.step == 1 && c.lane == 1) || (c.step == 2 && c.jumping) || (c.step == 3 && c.sliding))) crossedOk++;
        Check(crossedOk == 4, $"dort engel de dogru hareketle gecildi ({crossedOk}/4)");
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
            yield return PlayUntilTutorialDone(60f);
            botEnabled = false;
            botReaction = 0f;
            float tDone = Time.realtimeSinceStartup;
            Check(!m_GS.isTutorialRun && Top() == "Game" && m_GS.tutorialFailures == 0, $"egitim carpmadan tamamlandi (tepki {reaction:F1} sn, carpma={m_GS.tutorialFailures})");
            m_Summary.Add($"tepki {reaction:F1} sn: OYNA'dan kosuya {tMoving - tStart:F1} sn + dort engel {tDone - tMoving:F1} sn + HAZIRSIN 1,6 sn = toplam {tDone - tStart + 1.6f:F1} sn");
            yield return QuitViaPauseMenu();
            CheckClean($"S9 tepki {reaction:F1} sonrasi");
        }
    }

    // 11) Çarpma ve yeniden deneme: hareket yapılmazsa engele çarpılır (normal fizik), can gitmez, adım yeni engelle
    // yeniden denenir; sonra doğru hareketle eğitim sürer.
    IEnumerator S10_CrashAndRetry()
    {
        m_Phase = "S10-carpma-yeniden-deneme";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        // Adım 1'de hiçbir şey yapma
        yield return WaitUntil(() => m_GS.tutorialFailures > 0, 20f);
        yield return null;
        Check(m_GS.tutorialFailures == 1 && m_GS.tutorialStep == 0 && m_GS.tutorialAttempts == 2, $"hareketsiz oyuncu cop kutusuna carpti, adim 1 yeniden deneniyor ({Pose()})");
        Check(OverlayText() == "TEKRAR DENE!" && !ArrowVisible(), $"TEKRAR DENE mesaji gosterildi ('{OverlayText()}')");
        Check(Chr.currentLife == Chr.maxLife && Time.timeScale == 1f, "egitimde carpma can goturmedi, yavas cekim kapandi");
        yield return WaitUntil(() => m_GS.GetTutorialObstacle(0) != null, 10f);
        float retryDist = Dist(0);
        var retryObs = m_GS.GetTutorialObstacle(0);
        float rx = retryObs != null ? Chr.transform.InverseTransformPoint(retryObs.transform.position).x : 99f;
        Check(retryObs != null && retryDist > 30f && Mathf.Abs(rx) < 0.2f && Chr.currentLane == 1, $"yeni cop kutusu {retryDist:F0} m onde, orta seritte; karakter orta seritte");
        botEnabled = true;
        yield return WaitUntil(() => m_GS.tutorialStep >= 2, 30f);
        // Adım 3'te (zıpla) hiçbir şey yapma: alçak bariyere çarp
        botEnabled = false;
        yield return WaitUntil(() => m_GS.tutorialFailures >= 2, 30f);
        Check(m_GS.tutorialFailures == 2 && m_GS.tutorialStep == 2, $"zipla adiminda carpma -> ayni adim yeniden ({Pose()})");
        Check(m_GS.GetTutorialObstacle(3) == null, "carpinca sonraki adimin engeli konmamis/kaldirilmis");
        botEnabled = true;
        yield return PlayUntilTutorialDone(60f);
        Check(!m_GS.isTutorialRun && Top() == "Game" && PlayerData.instance.tutorialDone && Chr.currentLife == Chr.maxLife,
            "yeniden denemelerden sonra egitim tamamlandi, kosu suruyor, can tam");
        botEnabled = false;
        yield return QuitViaPauseMenu();
        CheckClean("S10 cikis sonrasi");
    }

    // 11) Hiç hareket etmeyen oyuncu: her denemede çarpar, eğitim kilitlenmez (yeni engel gelir, koşu sürer).
    IEnumerator S11_IdlePlayerNeverLocks()
    {
        m_Phase = "S11-hareketsiz-oyuncu";
        PlayerData.NewSave();
        yield return StartFromLoadout();
        yield return WaitMoving();
        yield return new WaitForSecondsRealtime(25f);
        Check(m_GS.isTutorialRun && m_GS.tutorialStep == 0 && m_GS.tutorialFailures >= 2 && Chr.currentLife == Chr.maxLife && Top() == "Game",
            $"25 sn hareketsiz: {m_GS.tutorialFailures} carpma, her seferinde yeni deneme, can tam, oyun suruyor ({Pose()})");
        yield return WaitUntil(() => m_TM.isMoving && m_GS.GetTutorialObstacle(0) != null, 10f);
        Check(m_TM.isMoving && m_GS.GetTutorialObstacle(0) != null, "kosu ve yeni engel hazir (kilit yok)");
        botEnabled = true;
        yield return PlayUntilTutorialDone(60f);
        botEnabled = false;
        Check(!m_GS.isTutorialRun && Top() == "Game", "sonra dogru hareketlerle egitim tamamlandi");
        yield return QuitViaPauseMenu();
        CheckClean("S11 cikis sonrasi");
    }
}
#endif
