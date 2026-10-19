using UnityEngine;

// Subway'deki müfettiş ve köpeği gibi: polis ve K9 köpeği koşucunun arkasından gelir. Koşu başında ve her tökezleme ya da
// çarpmadan sonra birkaç saniye hemen arkasında (ekranın altında) görünür, sonra geride kalıp kameranın arkasında kaybolur.
// Yalnız görsel; çarpışması yoktur (yakalanma oyunun mevcut can kuralıyla olur).
public class Kovalayan : MonoBehaviour
{
    public Animator polisAnimator;
    public Transform kopek;
    [Tooltip("Ön sol, ön sağ, arka sol, arka sağ bacak kemikleri (kalçada döner)")]
    public Transform[] kopekBacaklari;
    public float kopekSekme = 0.05f;

    const float k_Yakin = 0.85f, k_Uzak = 9f, k_YakinSure = 3.5f;   // yakın: koşucunun hemen arkası (kamera ~2 m geride)
    CharacterInputController m_Kosucu;
    float m_YakinBitis, m_Mesafe = k_Uzak, m_Yan, m_Yuk;
    int m_SonCan, m_SonTokez;
    Vector3 m_KopekTaban;
    float m_KopekEvre;
    bool m_Basladi;

    public void Baslat(CharacterInputController kosucu)
    {
        m_Kosucu = kosucu;
        transform.SetParent(kosucu.transform, false);
        m_SonCan = kosucu.currentLife; m_SonTokez = kosucu.tokezlemeSayisi;
        m_YakinBitis = Time.time + k_YakinSure;
        m_Mesafe = k_Yakin; m_Yan = 0f; m_Yuk = 0f;
        if (kopek != null) m_KopekTaban = kopek.localPosition;
        Yerlestir();
    }

    void LateUpdate()
    {
        if (m_Kosucu == null) { Destroy(gameObject); return; }
        if (m_Kosucu.currentLife < m_SonCan || m_Kosucu.tokezlemeSayisi != m_SonTokez) m_YakinBitis = Time.time + k_YakinSure + 1.5f;
        m_SonCan = m_Kosucu.currentLife; m_SonTokez = m_Kosucu.tokezlemeSayisi;
        bool kosuyor = m_Kosucu.trackManager != null && m_Kosucu.trackManager.isMoving;
        if (kosuyor && !m_Basladi) { m_Basladi = true; m_YakinBitis = Time.time + k_YakinSure; } // geri sayım sonrası
        if (!m_Basladi) m_YakinBitis = Time.time + k_YakinSure;
        // can bittiyse yetişir (koşu durur): karakterin hemen arkasına gelir
        float hedef = m_Kosucu.currentLife <= 0 ? 1.0f : (Time.time < m_YakinBitis ? k_Yakin : k_Uzak);
        m_Mesafe = Mathf.MoveTowards(m_Mesafe, hedef, (hedef < m_Mesafe ? 6f : 2.5f) * Time.deltaTime);
        Yerlestir();
        if (polisAnimator != null) polisAnimator.speed = kosuyor ? Mathf.Lerp(1f, 1.5f, m_Kosucu.trackManager.speedRatio) : 0.4f;
        if (kopek != null)
        {
            // dörtnala: ön ve arka bacak çiftleri karşıt evrede, çift içinde hafif gecikme; gövde hafif sekip öne-arkaya sallanır
            m_KopekEvre += (kosuyor ? 13f * Mathf.Lerp(1f, 1.4f, m_Kosucu.trackManager.speedRatio) : 0f) * Time.deltaTime;
            float f = m_KopekEvre;
            kopek.localPosition = m_KopekTaban + Vector3.up * (Mathf.Abs(Mathf.Sin(f)) * kopekSekme);
            kopek.localRotation = Quaternion.Euler(Mathf.Sin(f + 1.2f) * 5f, 0f, 0f);
            if (kopekBacaklari != null)
                for (int i = 0; i < kopekBacaklari.Length && i < 4; ++i)
                    if (kopekBacaklari[i] != null)
                        kopekBacaklari[i].localRotation = Quaternion.Euler(Mathf.Sin(f + (i >= 2 ? Mathf.PI : 0f) + (i % 2) * 0.35f) * 38f, 0f, 0f);
        }
        bool gorunur = m_Mesafe < k_Uzak - 0.5f;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = gorunur;
    }

    void Yerlestir()
    {
        // koşucunun şeridini ve yüksekliğini gecikmeli izler
        Vector3 k = m_Kosucu.characterCollider.transform.localPosition;
        m_Yan = Mathf.Lerp(m_Yan, k.x, 1f - Mathf.Exp(-4f * Time.deltaTime));
        m_Yuk = Mathf.Lerp(m_Yuk, m_Kosucu.zeminYuksekligi, 1f - Mathf.Exp(-3f * Time.deltaTime));
        transform.localPosition = new Vector3(m_Yan, m_Yuk, -m_Mesafe);
        transform.localRotation = Quaternion.identity;
    }
}
