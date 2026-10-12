#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Eğitim girdi teşhisi: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir; DevTimeline
// açıkken (web'de her zaman, masaüstünde "-timeline") çalışır. Günlük satırları "TL|" önekiyle ve ekrandaki katmanda.
//
// SALT OKUR: oyunun dokunma sınıflandırmasını (CharacterInputController.Update, dokunmatik kol) aynı dokunmalardan
// birebir yeniden hesaplar ama TutorialMoveCheck'i ÇAĞIRMAZ (onun bayrak yazan yan etkisi var) ve hiçbir alanı yazmaz.
//   - Update (CharacterInputController'dan önce): bayrağın/seviyenin/yönergenin kare başı değeri + karar anı.
//   - LateUpdate (tüm Update'lerden sonra): bayrağın kare sonu değeri ve karakterde gerçekleşen hareket.
// Kayıtlar:
//   KAYDIR  k#N  sv=seviye  v=karar anındaki fark (ekran genişliğine oranla)  -> sınıflandırma ve oyunun yapacağı eylem
//           bayrak=kare başı->kare sonu  yon=yönerge açık mı  gercek=karakterde görülen hareket
//   KAYDIR-SON k#N  toplam vektör ve karar eksenine göre "AYNI EKSEN" / "FARKLI EKSEN" (yanlış yön seçimi kanıtı)
//   YONERGE acildi  sv=..  bu seviyede yönergeden ÖNCE bayrağı düşüren hareket sayısı (sıfırlanan hareket kanıtı)
//   OZET    sayaçlar
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[DefaultExecutionOrder(-1000)]
public class TutorialInputProbe : MonoBehaviour
{
    // CharacterInputController ile aynı eşik (ekran genişliğinin %1'i).
    const float k_Threshold = 0.01f;

    static readonly BindingFlags k_Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly FieldInfo s_GsPrompt = typeof(GameState).GetField("m_TutorialPromptVisible", k_Inst);
    static readonly FieldInfo s_CicLane = typeof(CharacterInputController).GetField("m_CurrentLane", k_Inst);

    // Oyunun kendi durumunun kopyası (yalnızca bu bileşende).
    bool m_IsSwiping;
    Vector2 m_StartingTouch;

    // Bu karede verilen karar (LateUpdate'te sonucu yazılır).
    bool m_Pending;
    string m_PendingText;
    bool m_PendingVertical;
    bool m_PredFlag;

    // Kare başı anlık görüntü.
    bool m_FlagBefore, m_PromptBefore, m_JumpBefore, m_SlideBefore;
    int m_LaneBefore, m_Level;

    // Dokunma boyunca: karar ekseni ve başlangıç.
    int m_Gesture;
    bool m_Decided, m_DecidedVertical;
    Vector2 m_GestureStart;
    int m_GestureTouchId = -1;

    // Sayaçlar.
    int m_Total, m_AxisMismatch, m_NotCountedInPrompt, m_PreValidated, m_PromptResets;
    bool m_LastPrompt;
    int m_PreValidatedThisLevel = 0;
    float m_LastPreValidatedTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // DevTimeline ile aynı koşul: web test derlemesi ya da masaüstünde "-timeline".
        bool web = Application.platform == RuntimePlatform.WebGLPlayer;
        if (!(web || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-timeline") >= 0)) return;
        var go = new GameObject("TutorialInputProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorialInputProbe>();
    }

    static void Log(string msg)
    {
        if (DevTimeline.instance != null) DevTimeline.instance.Log(msg);
        else Debug.Log("TL| " + msg);
    }

    static bool TryGetContext(out GameState gs, out CharacterInputController chr)
    {
        gs = null; chr = null;
        var gm = GameManager.instance;
        if (gm == null || gm.topState == null || gm.topState.GetName() != "Game") return false;
        gs = gm.FindState("Game") as GameState;
        chr = gs != null && gs.trackManager != null ? gs.trackManager.characterController : null;
        return chr != null;
    }

    // CharacterInputController.IsOverInteractableUI ile aynı mantık.
    static bool IsOverInteractableUI(Vector2 screenPosition)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
        var data = new PointerEventData(es) { position = screenPosition };
        var results = new List<RaycastResult>();
        es.RaycastAll(data, results);
        foreach (var r in results)
        {
            Selectable s = r.gameObject.GetComponentInParent<Selectable>();
            if (s != null && s.IsInteractable()) return true;
        }
        return false;
    }

    void Update()
    {
        m_Pending = false;
        if (!TryGetContext(out GameState gs, out CharacterInputController chr)) { m_IsSwiping = false; return; }

        bool tutorial = gs.trackManager.isTutorial;
        m_Level = chr.currentTutorialLevel;
        m_FlagBefore = chr.tutorialWaitingForValidation;
        m_PromptBefore = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
        m_LaneBefore = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : -1;
        m_JumpBefore = chr.isJumping;
        m_SlideBefore = chr.isSliding;

        // Yönerge açıldı mı? (kare başı gözlem; açılış bir önceki karede GameState.Tick içinde olur)
        if (m_PromptBefore && !m_LastPrompt)
        {
            if (m_PreValidatedThisLevel > 0)
            {
                m_PromptResets++;
                Log($"YONERGE acildi sv={m_Level} bayrak={B(m_FlagBefore)} | bu seviyede yonergeden ONCE bayragi dusuren {m_PreValidatedThisLevel} hareket SAYILMADI (son {Time.realtimeSinceStartup - m_LastPreValidatedTime:F1}s once)");
            }
            else
                Log($"YONERGE acildi sv={m_Level} bayrak={B(m_FlagBefore)}");
        }
        if (!m_PromptBefore && m_LastPrompt)
        {
            Log($"YONERGE kapandi sv={m_Level}");
            m_PreValidatedThisLevel = 0;
        }
        m_LastPrompt = m_PromptBefore;

        // --- CharacterInputController (dokunmatik kol) ile birebir aynı akış ---
        if (ETouch.activeTouches.Count != 1) return;
        ETouch touch = ETouch.activeTouches[0];

        if (m_IsSwiping)
        {
            Vector2 raw = touch.screenPosition - m_StartingTouch;
            Vector2 diff = new Vector2(raw.x / Screen.width, raw.y / Screen.width);
            if (diff.magnitude > k_Threshold)
            {
                bool vertical = Mathf.Abs(diff.y) > Mathf.Abs(diff.x);
                string action;
                bool flag = m_FlagBefore;
                if (vertical)
                {
                    // if (TutorialMoveCheck(2) && diff.y < 0) Slide(); else if (TutorialMoveCheck(1)) Jump();
                    flag = m_Level != 2;
                    bool allow2 = !tutorial || m_Level >= 2;
                    if (allow2 && diff.y < 0) action = "KAY";
                    else
                    {
                        flag = m_Level != 1;
                        action = (!tutorial || m_Level >= 1) ? "ZIPLA" : "YOK (seviye izin vermiyor)";
                    }
                }
                else
                {
                    flag = m_Level != 0;
                    action = "SERIT " + (diff.x < 0 ? "SOL" : "SAG");
                }

                m_Total++;
                m_Pending = true;
                m_PendingVertical = vertical;
                m_PredFlag = flag;
                m_PendingText = $"KAYDIR k#{m_Gesture} sv={m_Level} v=({diff.x:F3},{diff.y:F3}) {(vertical ? (diff.y < 0 ? "DIKEY-ASAGI" : "DIKEY-YUKARI") : "YATAY")} -> {action}";
                m_Decided = true;
                m_DecidedVertical = vertical;
                m_IsSwiping = false;
            }
        }

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            m_StartingTouch = touch.screenPosition;
            m_IsSwiping = !IsOverInteractableUI(touch.screenPosition);
            m_Gesture++;
            m_Decided = false;
            m_GestureStart = touch.screenPosition;
            m_GestureTouchId = touch.touchId;
            if (!m_IsSwiping) Log($"KAYDIR k#{m_Gesture} dugme ustunde basladi, kaydirma sayilmaz");
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
        {
            m_IsSwiping = false;
            if (touch.touchId == m_GestureTouchId)
            {
                Vector2 total = touch.screenPosition - m_GestureStart;
                Vector2 t = new Vector2(total.x / Screen.width, total.y / Screen.width);
                bool totalVertical = Mathf.Abs(t.y) > Mathf.Abs(t.x);
                if (!m_Decided)
                    Log($"KAYDIR-SON k#{m_Gesture} toplam=({t.x:F3},{t.y:F3}) karar YOK (esik asilmadi)");
                else if (totalVertical != m_DecidedVertical)
                {
                    m_AxisMismatch++;
                    Log($"KAYDIR-SON k#{m_Gesture} toplam=({t.x:F3},{t.y:F3}) {(totalVertical ? "DIKEY" : "YATAY")} | karar {(m_DecidedVertical ? "DIKEY" : "YATAY")} -> FARKLI EKSEN");
                }
                else
                    Log($"KAYDIR-SON k#{m_Gesture} toplam=({t.x:F3},{t.y:F3}) AYNI EKSEN");
                m_GestureTouchId = -1;
            }
        }
    }

    void LateUpdate()
    {
        if (!m_Pending) return;
        m_Pending = false;
        if (!TryGetContext(out GameState gs, out CharacterInputController chr)) return;

        bool flagAfter = chr.tutorialWaitingForValidation;
        bool promptAfter = s_GsPrompt != null && (bool)s_GsPrompt.GetValue(gs);
        int lane = s_CicLane != null ? (int)s_CicLane.GetValue(chr) : -1;
        string real = lane != m_LaneBefore ? $"serit {m_LaneBefore}->{lane}"
            : (chr.isJumping && !m_JumpBefore) ? "zipladi"
            : (chr.isSliding && !m_SlideBefore) ? "kaydi"
            : "yok";

        Log($"{m_PendingText} | bayrak {B(m_FlagBefore)}->{B(flagAfter)} (beklenen {B(m_PredFlag)}) yon={(m_PromptBefore ? "acik" : "kapali")}->{(promptAfter ? "acik" : "kapali")} gercek={real}");

        if (gs.trackManager.isTutorial)
        {
            // Yönerge açıkken bayrak düşmediyse bu kaydırma eğitim adımını tamamlamadı.
            if (m_PromptBefore && m_FlagBefore && m_PredFlag)
                m_NotCountedInPrompt++;
            // Yönerge kapalıyken bayrağı düşüren hareket: yönerge açılınca DisplayTutorial(true) bayrağı yeniden kaldırır.
            if (!m_PromptBefore && !m_PredFlag)
            {
                m_PreValidated++;
                m_PreValidatedThisLevel++;
                m_LastPreValidatedTime = Time.realtimeSinceStartup;
            }
        }
        Log($"OZET kaydirma={m_Total} farkli-eksen={m_AxisMismatch} yonergede-sayilmayan={m_NotCountedInPrompt} yonerge-oncesi-gecerli={m_PreValidated} sifirlanan-seviye={m_PromptResets}");
    }

    static string B(bool v) => v ? "T" : "F";
}
#endif
