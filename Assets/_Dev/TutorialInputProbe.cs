#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Kaydırma teşhisi: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir; DevTimeline
// açıkken (web'de her zaman, masaüstünde "-timeline") çalışır. Günlük satırları "TL|" önekiyle ve ekrandaki katmanda.
//
// SALT OKUR: oyunun dokunma sınıflandırmasını (CharacterInputController.Update, dokunmatik kol) aynı dokunmalardan
// birebir yeniden hesaplar; hiçbir alanı yazmaz.
//   - Update (CharacterInputController'dan önce): kare başı durum + karar anı (yön, eğitimin o an izin verdiği hareket).
//   - LateUpdate (tüm Update'lerden sonra): karakterde gerçekleşen hareket.
// Kayıtlar:
//   KAYDIR  k#N  adim=eğitim adımı  izin=adımın kabul ettiği hareket  v=karar anındaki fark (ekran genişliğine oranla)
//           -> yön ve "KABUL" / "YOK SAYILDI"  gercek=karakterde görülen hareket  (+karar anına kadar geçen süre)
//   KAYDIR-SON k#N  toplam vektör ve karar eksenine göre "AYNI EKSEN" / "FARKLI EKSEN" (yanlış yön seçimi kanıtı)
//   OZET    sayaçlar
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[DefaultExecutionOrder(-1000)]
public class TutorialInputProbe : MonoBehaviour
{
    // CharacterInputController ile aynı eşik (ekran genişliğinin %1'i).
    const float k_Threshold = 0.01f;

    bool m_IsSwiping;
    Vector2 m_StartingTouch;
    float m_StartTime;

    bool m_Pending;
    string m_PendingText;
    int m_LaneBefore;
    bool m_JumpBefore, m_SlideBefore;

    int m_Gesture;
    bool m_Decided, m_DecidedVertical;
    Vector2 m_GestureStart;
    int m_GestureTouchId = -1;

    int m_Total, m_AxisMismatch, m_Ignored;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
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
        return chr != null && chr.character != null;
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

        m_LaneBefore = chr.currentLane;
        m_JumpBefore = chr.isJumping;
        m_SlideBefore = chr.isSliding;

        if (ETouch.activeTouches.Count != 1) return;
        ETouch touch = ETouch.activeTouches[0];

        if (m_IsSwiping)
        {
            Vector2 raw = touch.screenPosition - m_StartingTouch;
            Vector2 diff = new Vector2(raw.x / Screen.width, raw.y / Screen.width);
            if (diff.magnitude > k_Threshold)
            {
                bool vertical = Mathf.Abs(diff.y) > Mathf.Abs(diff.x);
                var move = vertical ? (diff.y < 0 ? CharacterInputController.MoveInput.Down : CharacterInputController.MoveInput.Up)
                                    : (diff.x < 0 ? CharacterInputController.MoveInput.Left : CharacterInputController.MoveInput.Right);
                bool tutorial = gs.trackManager.isTutorial;
                bool accepted = !tutorial || move == chr.tutorialAllowedMove;
                if (!accepted) m_Ignored++;

                m_Total++;
                m_Pending = true;
                m_PendingText = $"KAYDIR k#{m_Gesture} adim={(tutorial ? gs.tutorialStep.ToString() : "-")} izin={(tutorial ? chr.tutorialAllowedMove.ToString() : "hepsi")} v=({diff.x:F3},{diff.y:F3}) {(Time.realtimeSinceStartup - m_StartTime) * 1000f:F0}ms -> {move} {(accepted ? "KABUL" : "YOK SAYILDI")}";
                m_Decided = true;
                m_DecidedVertical = vertical;
                m_IsSwiping = false;
            }
        }

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            m_StartingTouch = touch.screenPosition;
            m_StartTime = Time.realtimeSinceStartup;
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

        string real = chr.currentLane != m_LaneBefore ? $"serit {m_LaneBefore}->{chr.currentLane}"
            : (chr.isJumping && !m_JumpBefore) ? "zipladi"
            : (chr.isSliding && !m_SlideBefore) ? "kaydi"
            : "yok";
        Log($"{m_PendingText} gercek={real}");
        Log($"OZET kaydirma={m_Total} yok-sayilan={m_Ignored} farkli-eksen={m_AxisMismatch}");
    }
}
#endif
