#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Geliştirme zaman çizelgesi: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir; web'de otomatik, masaüstünde "-timeline" ile açılır.
// Ekranda ve konsolda ("TL|" önekiyle) oyun durumu, tutorial aşaması, dokunmalar, buton tıklamaları ve kayıt durumu.
// Web URL parametreleri: ?kayitsil=1 (açılışta kaydı sıfırlar)  ?tl=0 (katman gizli başlar). İki parmakla dokunma katmanı açar/kapatır.
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class DevTimeline : MonoBehaviour
{
    public static DevTimeline instance { get; private set; }

    const int k_MaxLines = 16;
    readonly List<string> m_Lines = new List<string>();
    bool m_Visible = true;

    string m_LastState = "";
    string m_LastClick = ""; float m_LastClickTime = -10f;
    readonly HashSet<Button> m_HookedButtons = new HashSet<Button>();
    float m_NextButtonScan;

    // izlenen değerler (değişince günlüğe yazılır)
    int m_TutLevel = -1, m_Cleared = -1, m_Lane = -1;
    bool m_IsTutorial, m_Prompt, m_Paused, m_PauseMenu, m_Jumping, m_Sliding, m_MemDone, m_DiskDone;
    string m_DiskInfo = "?"; float m_NextDiskRead;

    readonly Dictionary<int, Vector2> m_TouchStart = new Dictionary<int, Vector2>();
    readonly Dictionary<int, float> m_TouchStartTime = new Dictionary<int, float>();

    static readonly BindingFlags k_Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static FieldInfo F(System.Type t, string n) => t.GetField(n, k_Inst);
    static readonly FieldInfo s_GsIsTutorial = F(typeof(GameState), "m_IsTutorial");
    static readonly FieldInfo s_GsCleared = F(typeof(GameState), "m_TutorialClearedObstacle");
    static readonly FieldInfo s_GsCount = F(typeof(GameState), "m_CountObstacles");
    static readonly FieldInfo s_GsPrompt = F(typeof(GameState), "m_TutorialPromptVisible");
    static readonly FieldInfo s_CicLane = F(typeof(CharacterInputController), "m_CurrentLane");
    static readonly FieldInfo s_TmSpawned = F(typeof(TrackManager), "_spawnedSegments");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        bool web = Application.platform == RuntimePlatform.WebGLPlayer;
        var args = System.Environment.GetCommandLineArgs();
        if (!(web || System.Array.IndexOf(args, "-timeline") >= 0)) return; // bu kod yalnızca test derlemelerinde var

        string url = web ? Application.absoluteURL : "";
        if (url.Contains("kayitsil=1"))
        {
            PlayerData.Create();
            PlayerData.NewSave();
        }

        var go = new GameObject("DevTimeline");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevTimeline>();
        instance.m_Visible = !url.Contains("tl=0");
        if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        instance.Log("ACILIS " + (web ? "web " + url : "masaustu") + (url.Contains("kayitsil=1") ? " (kayit sifirlandi)" : ""));
    }

    public void Log(string msg)
    {
        string line = $"{Time.realtimeSinceStartup,7:F2} {msg}";
        m_Lines.Add(line);
        if (m_Lines.Count > k_MaxLines) m_Lines.RemoveAt(0);
        Debug.Log("TL| " + line);
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        for (int i = 0; i < 2 && t.parent != null; ++i) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    void ScanButtons()
    {
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!m_HookedButtons.Add(b)) continue;
            Button captured = b;
            b.onClick.AddListener(() =>
            {
                m_LastClick = PathOf(captured.transform);
                m_LastClickTime = Time.realtimeSinceStartup;
                Log("TIKLAMA " + m_LastClick);
            });
        }
    }

    string UIUnder(Vector2 pos)
    {
        var es = EventSystem.current;
        if (es == null) return "-";
        var data = new PointerEventData(es) { position = pos };
        var results = new List<RaycastResult>();
        es.RaycastAll(data, results);
        foreach (var r in results)
        {
            var s = r.gameObject.GetComponentInParent<Selectable>();
            if (s != null && s.IsInteractable()) return "BUTON:" + s.name;
        }
        return results.Count > 0 ? "ui:" + results[0].gameObject.name : "-";
    }

    void Update()
    {
        if (Time.realtimeSinceStartup >= m_NextButtonScan) { ScanButtons(); m_NextButtonScan = Time.realtimeSinceStartup + 1f; }

        // İki parmakla dokunma katmanı açar/kapatır (oyun yalnızca tek parmak hareketlerini okur).
        if (ETouch.activeTouches.Count == 2 && (ETouch.activeTouches[0].phase == UnityEngine.InputSystem.TouchPhase.Began || ETouch.activeTouches[1].phase == UnityEngine.InputSystem.TouchPhase.Began))
        {
            m_Visible = !m_Visible;
            Log("KATMAN " + (m_Visible ? "acildi" : "gizlendi") + " (iki parmak)");
        }

        // Dokunmalar
        foreach (var t in ETouch.activeTouches)
        {
            int id = t.touchId;
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                m_TouchStart[id] = t.screenPosition; m_TouchStartTime[id] = Time.realtimeSinceStartup;
                Log($"DOKUNMA basla #{id} ({t.screenPosition.x:F0},{t.screenPosition.y:F0}) {UIUnder(t.screenPosition)}");
            }
            else if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                m_TouchStart.TryGetValue(id, out var s); m_TouchStartTime.TryGetValue(id, out var st);
                Vector2 d = t.screenPosition - s;
                string dir = d.magnitude < Screen.width * 0.01f ? "dokunma" : (Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? "SAG" : "SOL") : (d.y > 0 ? "YUKARI" : "ASAGI"));
                Log($"DOKUNMA bitti #{id} {t.phase} {dir} d=({d.x:F0},{d.y:F0}) {1000f * (Time.realtimeSinceStartup - st):F0}ms");
                m_TouchStart.Remove(id); m_TouchStartTime.Remove(id);
            }
        }

        var gm = GameManager.instance;
        string state = gm != null && gm.topState != null ? gm.topState.GetName() : "(yok)";
        var gs = gm != null ? gm.FindState("Game") as GameState : null;
        var tm = gs != null ? gs.trackManager : null;
        var chr = tm != null ? tm.characterController : null;

        if (state != m_LastState)
        {
            string reason = "";
            if (m_LastState == "Game" && state == "GameOver")
            {
                var de = chr != null ? chr.characterCollider.deathData : default;
                reason = $" NEDEN: olum (engel={de.obstacleType})";
            }
            else if (m_LastState != "" && state == "Loadout")
                reason = Time.realtimeSinceStartup - m_LastClickTime < 1.5f ? $" NEDEN: tiklama '{m_LastClick}'" : " NEDEN: tiklama yok (kod tetikledi)";
            Log($"DURUM {m_LastState} -> {state}{reason}");
            m_LastState = state;
        }

        if (PlayerData.instance != null && PlayerData.instance.tutorialDone != m_MemDone)
        {
            m_MemDone = PlayerData.instance.tutorialDone;
            Log("KAYIT bellek tutorialDone=" + m_MemDone);
        }
        if (Time.realtimeSinceStartup >= m_NextDiskRead) { ReadDisk(); m_NextDiskRead = Time.realtimeSinceStartup + 1f; }

        if (gs == null || tm == null || chr == null) return;

        bool isTut = s_GsIsTutorial != null && (bool)s_GsIsTutorial.GetValue(gs);
        int cleared = s_GsCleared != null ? (int)s_GsCleared.GetValue(gs) : -1;
        bool prompt = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
        bool paused = AudioListener.pause;
        bool pauseMenu = gs.pauseMenu != null && gs.pauseMenu.gameObject.activeSelf;

        if (isTut != m_IsTutorial) { m_IsTutorial = isTut; Log("TUTORIAL " + (isTut ? "basladi" : "bitti/kapali")); }
        if (state == "Game" && isTut)
        {
            if (chr.currentTutorialLevel != m_TutLevel) { m_TutLevel = chr.currentTutorialLevel; Log("TUTORIAL asama=" + m_TutLevel); }
            if (cleared != m_Cleared) { m_Cleared = cleared; Log($"TUTORIAL gecilen={cleared}/3"); }
        }
        if (prompt != m_Prompt) { m_Prompt = prompt; Log("TUTORIAL komut ekrani " + (prompt ? "acik" : "kapandi")); }
        if (paused != m_Paused || pauseMenu != m_PauseMenu)
        {
            m_Paused = paused; m_PauseMenu = pauseMenu;
            Log($"DURAKLAMA {(paused ? "evet" : "hayir")} menu={(pauseMenu ? "acik" : "kapali")} timeScale={Time.timeScale}");
        }

        if (state == "Game")
        {
            int lane = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : -1;
            if (lane != m_Lane) { if (m_Lane >= 0) Log($"HAREKET serit {m_Lane}->{lane}"); m_Lane = lane; }
            if (chr.isJumping && !m_Jumping) Log("HAREKET zipla");
            if (chr.isSliding && !m_Sliding) Log("HAREKET kay");
            m_Jumping = chr.isJumping; m_Sliding = chr.isSliding;
        }
    }

    void ReadDisk()
    {
        try
        {
            string path = Application.persistentDataPath + "/save.bin";
            if (!File.Exists(path)) { m_DiskInfo = "yok"; return; }
            byte[] b = File.ReadAllBytes(path);
            int ver = b.Length >= 4 ? System.BitConverter.ToInt32(b, 0) : -1;
            // v12'de tutorialDone dosyanın sonundadır (OpenOrCreate dosyayı kısaltmadığı için yalnızca yeni yazılmış dosyada güvenilir)
            bool done = ver >= 12 && b.Length > 0 && b[b.Length - 1] != 0;
            if (done != m_DiskDone) { m_DiskDone = done; Log("KAYIT disk tutorialDone=" + done); }
            m_DiskInfo = $"v{ver} {b.Length}B done={done}";
        }
        catch (System.Exception e) { m_DiskInfo = "hata " + e.GetType().Name; }
    }

    GUIStyle m_Style;
    Texture2D m_Back;
    void OnGUI()
    {
        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(11, Screen.height / 64), wordWrap = false };
            m_Style.normal.textColor = Color.white;
            m_Back = new Texture2D(1, 1);
            m_Back.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            m_Back.Apply();
        }

        // IMGUI butonları bu projede girdi almıyor (yalnızca yeni Input System); aç/kapa iki parmakla dokunarak yapılır.
        if (!m_Visible) return;

        var gm = GameManager.instance;
        var gs = gm != null ? gm.FindState("Game") as GameState : null;
        var tm = gs != null ? gs.trackManager : null;
        string top = gm != null && gm.topState != null ? gm.topState.GetName() : "-";
        bool pauseMenu = gs != null && gs.pauseMenu != null && gs.pauseMenu.gameObject.activeInHierarchy;
        string head = $"DEV {(gm != null && gm.topState != null ? gm.topState.GetName() : "-")} | tut={m_IsTutorial} asama={m_TutLevel} gecilen={m_Cleared} | bellek done={(PlayerData.instance != null && PlayerData.instance.tutorialDone)} disk {m_DiskInfo}";
        if (tm != null) head += $" | hareket={tm.isMoving} parca={tm.segments.Count} sayac={(s_TmSpawned != null ? s_TmSpawned.GetValue(tm) : "?")} hiz={tm.speed:F1}";

        float lh = m_Style.fontSize + 4;
        // Butonların üstüne binmesin: koşuda HUD'un altında, duraklatma menüsünde altta, menülerde en üstte.
        float y = pauseMenu ? Screen.height * 0.64f : (top == "Game" ? Screen.height * 0.13f : Screen.height * 0.02f);
        GUI.DrawTexture(new Rect(0, y - 2, Screen.width, lh * (m_Lines.Count + 3) + 4), m_Back, ScaleMode.StretchToFill);
        var headStyle = new GUIStyle(m_Style) { wordWrap = true };
        GUI.Label(new Rect(4, y, Screen.width - 8, lh * 3), head, headStyle);
        y += lh * 3;
        foreach (var l in m_Lines) { GUI.Label(new Rect(4, y, Screen.width - 8, lh), l, m_Style); y += lh; }
    }
}
#endif
