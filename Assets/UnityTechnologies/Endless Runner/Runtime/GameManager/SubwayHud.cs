using UnityEngine;
using UnityEngine.UI;

// Subway tarzı oyun içi gösterge: sol üstte duraklat, sağ üstte mor çarpan (x5 ★) + 6 haneli skor hapı, altında altın
// hapı, altında "EN YÜKSEK" kutusu (karakter simgesi + rekor). Bonus süreleri sol altta. GameState'in mevcut Text
// alanları (scoreText, multiplierText, coinText) yeni haplara taşınır; GameState onları eskisi gibi günceller.
// Mesafe ve ikinci para (premium) koşu ekranında gizlenir. Bir kez kurulur; sahne yeniden yüklenmez.
public class SubwayHud : MonoBehaviour
{
    static Sprite s_Hap, s_Yildiz, s_Altin;
    Text m_Rekor;
    Image m_Simge;
    GameState m_Gs;

    public static void Uygula(GameState gs)
    {
        if (gs == null || gs.wholeUI == null) return;
        var var = gs.wholeUI.GetComponentInChildren<SubwayHud>(true);
        if (var == null) { var = new GameObject("SubwayHud", typeof(RectTransform)).AddComponent<SubwayHud>(); var.Kur(gs); }
        var.Tazele();
    }

    void Kur(GameState gs)
    {
        m_Gs = gs;
        var rt = (RectTransform)transform;
        rt.SetParent(gs.wholeUI, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();
        if (s_Hap == null) { s_Hap = HapSprite(); s_Yildiz = YildizSprite(); s_Altin = AltinSprite(); }
        Font font = gs.scoreText.font;

        // eski kapları sonra gizlemek için not al
        Transform eskiSkor = gs.scoreText.transform.parent, eskiCarpan = gs.multiplierText.transform.parent, eskiAltin = gs.coinText.transform.parent;

        // skor hapı (sağ üst)
        var skor = Hap("Skor", new Vector2(-24f, -24f), new Vector2(310f, 92f));
        Tasi(gs.scoreText, skor, TextAnchor.MiddleCenter, 62, Color.white);
        // çarpan hapı (skorun solu): mor "x5" + sarı yıldız
        var carpan = Hap("Carpan", new Vector2(-346f, -24f), new Vector2(170f, 92f));
        Tasi(gs.multiplierText, carpan, TextAnchor.MiddleLeft, 58, new Color(0.78f, 0.38f, 1f), new Vector2(18f, 0f), new Vector2(-64f, 0f));
        Resim("Yildiz", carpan, s_Yildiz, new Vector2(-10f, 0f), new Vector2(58f, 58f), new Vector2(1f, 0.5f));
        // altın hapı (skorun altı)
        var altin = Hap("Altin", new Vector2(-24f, -128f), new Vector2(200f, 76f));
        Tasi(gs.coinText, altin, TextAnchor.MiddleRight, 50, Color.white, new Vector2(12f, 0f), new Vector2(-74f, 0f));
        Resim("AltinSimge", altin, s_Altin, new Vector2(-12f, 0f), new Vector2(58f, 58f), new Vector2(1f, 0.5f));
        // en yüksek skor kutusu
        var rekor = Hap("Rekor", new Vector2(-24f, -216f), new Vector2(170f, 200f));
        var baslik = YeniText("Baslik", rekor, font, 26, new Color(1f, 0.82f, 0.2f), TextAnchor.UpperCenter);
        baslik.text = Loc.Get("hud.highScore", "EN YÜKSEK"); Yerles(baslik.rectTransform, new Vector2(0f, -8f), new Vector2(160f, 32f), new Vector2(0.5f, 1f));
        var simgeKutu = Resim("SimgeKutu", rekor, s_Hap, new Vector2(0f, -44f), new Vector2(96f, 96f), new Vector2(0.5f, 1f));
        simgeKutu.color = new Color(0.25f, 0.55f, 1f, 1f);
        m_Simge = Resim("Simge", (RectTransform)simgeKutu.transform, null, Vector2.zero, new Vector2(84f, 84f), new Vector2(0.5f, 0.5f));
        m_Simge.preserveAspect = true;
        m_Rekor = YeniText("Deger", rekor, font, 34, Color.white, TextAnchor.LowerCenter);
        Yerles(m_Rekor.rectTransform, new Vector2(0f, 8f), new Vector2(160f, 44f), new Vector2(0.5f, 0f));

        // eski kaplar: içlerinde başka görev yoksa gizlenir (PUAN etiketi, balık kılçığı paneli vb.)
        foreach (var eski in new[] { eskiSkor, eskiCarpan, eskiAltin })
            if (eski != null && eski != gs.wholeUI && !IcindeGerekli(eski, gs)) eski.gameObject.SetActive(false);
        if (gs.distanceText != null) gs.distanceText.gameObject.SetActive(false);
        if (gs.premiumText != null && !IcindeGerekli(gs.premiumText.transform.parent, gs)) gs.premiumText.transform.parent.gameObject.SetActive(false);

        // canlar: duraklatın altında küçük; bonuslar: sol alt
        if (gs.lifeRectTransform != null)
        {
            var l = gs.lifeRectTransform;
            l.anchorMin = l.anchorMax = new Vector2(0f, 1f); l.pivot = new Vector2(0f, 1f);
            l.anchoredPosition = new Vector2(28f, -140f); l.localScale = Vector3.one * 0.6f;
        }
        if (gs.powerupZone != null)
        {
            var p = gs.powerupZone;
            p.anchorMin = p.anchorMax = new Vector2(0f, 0f); p.pivot = new Vector2(0f, 0f);
            p.anchoredPosition = new Vector2(28f, 40f);
        }
    }

    void Tazele()
    {
        int rekor = PlayerData.instance != null && PlayerData.instance.highscores.Count > 0 ? PlayerData.instance.highscores[0].score : 0;
        if (m_Rekor != null) m_Rekor.text = rekor.ToString();
        if (m_Simge != null && m_Gs != null && m_Gs.trackManager != null && m_Gs.trackManager.characterController.character != null)
            m_Simge.sprite = m_Gs.trackManager.characterController.character.icon;
        if (m_Simge != null) m_Simge.enabled = m_Simge.sprite != null;
    }

    void Update()
    {
        // simge karakter yüklendikten sonra gelir
        if (m_Simge != null && m_Simge.sprite == null) Tazele();
    }

    bool IcindeGerekli(Transform t, GameState gs)
    {
        foreach (var g in new Component[] { gs.pauseButton, gs.lifeRectTransform, gs.powerupZone, gs.countdownText, gs.inventoryIcon, gs.scoreText, gs.multiplierText, gs.coinText })
            if (g != null && g.transform.IsChildOf(t)) return true;
        return false;
    }

    RectTransform Hap(string ad, Vector2 konum, Vector2 boyut)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Image));
        var r = (RectTransform)go.transform; r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = konum; r.sizeDelta = boyut;
        var im = go.GetComponent<Image>(); im.sprite = s_Hap; im.type = Image.Type.Sliced; im.color = new Color(0.1f, 0.1f, 0.16f, 0.62f); im.raycastTarget = false;
        return r;
    }

    static void Tasi(Text t, RectTransform hap, TextAnchor hiza, int boy, Color renk, Vector2? solAlt = null, Vector2? sagUst = null)
    {
        var r = t.rectTransform; r.SetParent(hap, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = solAlt ?? new Vector2(10f, 0f); r.offsetMax = sagUst ?? new Vector2(-10f, 0f);
        r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        t.alignment = hiza; t.fontSize = boy; t.color = renk; t.resizeTextForBestFit = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        if (t.GetComponent<Outline>() == null) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.45f); o.effectDistance = new Vector2(2f, -2f); }
    }

    static Text YeniText(string ad, RectTransform ebeveyn, Font f, int boy, Color renk, TextAnchor hiza)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(ebeveyn, false);
        var t = go.GetComponent<Text>(); t.font = f; t.fontSize = boy; t.color = renk; t.alignment = hiza; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var o = go.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.45f); o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static Image Resim(string ad, RectTransform ebeveyn, Sprite s, Vector2 konum, Vector2 boyut, Vector2 capa)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Image));
        var r = (RectTransform)go.transform; r.SetParent(ebeveyn, false);
        r.anchorMin = r.anchorMax = capa; r.pivot = capa; r.anchoredPosition = konum; r.sizeDelta = boyut;
        var im = go.GetComponent<Image>(); im.sprite = s; im.raycastTarget = false;
        if (s == s_Hap) im.type = Image.Type.Sliced;
        return im;
    }

    static void Yerles(RectTransform r, Vector2 konum, Vector2 boyut, Vector2 capa)
    {
        r.anchorMin = r.anchorMax = capa; r.pivot = capa; r.anchoredPosition = konum; r.sizeDelta = boyut;
    }

    // ---- çalışma anında üretilen yumuşak kenarlı görseller (dokusuz, keskin)
    static Sprite HapSprite()
    {
        const int n = 64, yar = 24;
        var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
        {
            float dx = Mathf.Max(0f, Mathf.Max(yar - x, x - (n - 1 - yar))), dy = Mathf.Max(0f, Mathf.Max(yar - y, y - (n - 1 - yar)));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            tx.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(yar + 0.5f - d)));
        }
        tx.Apply();
        return Sprite.Create(tx, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(yar, yar, yar, yar));
    }

    static Sprite YildizSprite()
    {
        const int n = 128;
        var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var dis = new Color(0.85f, 0.55f, 0.05f); var ic = new Color(1f, 0.86f, 0.2f);
        for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
        {
            Vector2 p = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f) / (n / 2f);
            float a = Mathf.Atan2(p.x, p.y), r = p.magnitude;
            // beş köşeli yıldız: köşede 0,95, iç girintide 0,42 yarıçap (doğrusal)
            float u = Mathf.Abs(Mathf.Repeat(a, 2f * Mathf.PI / 5f) / (2f * Mathf.PI / 5f) - 0.5f) * 2f; // 1: köşe, 0: girinti
            float sinir = Mathf.Lerp(0.42f, 0.95f, u);
            var c = Color.Lerp(dis, ic, Mathf.Clamp01((sinir - r) * 5f)); c.a = Mathf.Clamp01((sinir - r) * n * 0.4f);
            tx.SetPixel(x, y, c);
        }
        tx.Apply();
        return Sprite.Create(tx, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    static Sprite AltinSprite()
    {
        const int n = 128;
        var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
        {
            float r = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
            Color c = r > 0.78f ? new Color(0.86f, 0.52f, 0.08f) : r > 0.66f ? new Color(1f, 0.8f, 0.25f) : new Color(0.98f, 0.68f, 0.12f);
            float isik = Mathf.Clamp01(1f - new Vector2(x - n * 0.38f, y - n * 0.62f).magnitude / (n * 0.35f)) * 0.35f;
            c = Color.Lerp(c, Color.white, isik); c.a = Mathf.Clamp01((1f - r) * n * 0.5f);
            tx.SetPixel(x, y, c);
        }
        tx.Apply();
        return Sprite.Create(tx, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}
