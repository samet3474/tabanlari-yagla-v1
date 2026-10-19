using UnityEngine;
using UnityEngine.UI;

// Subway tarzı ana menü: sol üstte "EN YÜKSEK" kutusu (karakter simgesi + rekor), logo ortada büyük, OYNA düğmesi
// "OYNAMAK İÇİN DOKUN" yazısına döner (nabız gibi büyüyüp küçülür) ve ekranın boş yerine dokunmak da koşuyu başlatır;
// alt şerit arka plansız, sarı yazılı büyük simgeler. Menü düzeni (MenuUygula) sahnede kalır, burada yalnız yerleri ve
// görünüş değişir; OYNA'nın kutusu eğitim perdesi için aynı yerde kalır.
public class SubwayMenu : MonoBehaviour
{
    const float k_SolUst = 14f, k_RekorEn = 236f, k_RekorBoy = 104f;
    const float k_LogoUst = 128f, k_LogoEn = 300f, k_LogoBoy = 169f, k_AdUst = 304f;
    static Sprite s_Hap;
    LoadoutState m_Lo;
    Text m_Rekor, m_OynaYazi;
    Image m_Simge;
    float m_Nabiz;

    public static void Uygula(LoadoutState lo)
    {
        if (lo == null || lo.inventoryCanvas == null) return;
        var safe = lo.inventoryCanvas.transform.Find("MenuSafeArea") as RectTransform;
        if (safe == null) return;
        var m = safe.GetComponent<SubwayMenu>();
        if (m == null) { m = safe.gameObject.AddComponent<SubwayMenu>(); m.Kur(lo, safe); }
        m.Tazele();
    }

    void Kur(LoadoutState lo, RectTransform safe)
    {
        m_Lo = lo;
        if (s_Hap == null) s_Hap = HapSprite();
        Font font = lo.runButton.GetComponentInChildren<Text>().font;

        // ---- boş yere dokunma: koşuyu başlatır (en altta; düğmeler üstünde kalır)
        var dokun = new GameObject("DokunOyna", typeof(RectTransform), typeof(Image), typeof(Button));
        var dr = (RectTransform)dokun.transform; dr.SetParent(safe, false); dr.SetAsFirstSibling();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
        dokun.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        dokun.GetComponent<Button>().transition = Selectable.Transition.None;
        dokun.GetComponent<Button>().onClick.AddListener(() => { if (m_Lo.runButton.interactable) m_Lo.runButton.onClick.Invoke(); });

        // ---- logo: ortada, büyük
        var logo = safe.Find("MenuLogo") as RectTransform;
        if (logo != null) Yer(logo, new Vector2(0.5f, 1f), new Vector2(0f, -k_LogoUst), new Vector2(k_LogoEn, k_LogoBoy));
        var ad = safe.Find("CharZone") as RectTransform;
        if (ad != null) Yer(ad, new Vector2(0.5f, 1f), new Vector2(0f, -k_AdUst), new Vector2(ad.sizeDelta.x, 72f));

        // ---- sol üst: en yüksek skor kutusu
        var kutu = Resim("EnYuksek", safe, s_Hap, new Color(0.08f, 0.1f, 0.22f, 0.82f));
        Yer(kutu.rectTransform, new Vector2(0f, 1f), new Vector2(k_SolUst, -k_SolUst), new Vector2(k_RekorEn, k_RekorBoy));
        kutu.type = Image.Type.Sliced; kutu.raycastTarget = false;
        var cerceve = Resim("SimgeCerceve", kutu.rectTransform, s_Hap, new Color(0.25f, 0.55f, 1f));
        cerceve.type = Image.Type.Sliced;
        Yer(cerceve.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(84f, 84f), new Vector2(0f, 0.5f));
        m_Simge = Resim("Simge", cerceve.rectTransform, null, Color.white);
        Yer(m_Simge.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f), new Vector2(0.5f, 0.5f));
        m_Simge.preserveAspect = true;
        var baslik = Yazi("Baslik", kutu.rectTransform, font, 22, new Color(1f, 0.84f, 0.3f), TextAnchor.UpperLeft);
        baslik.text = Loc.Get("hud.highScore", "EN YÜKSEK");
        Yer(baslik.rectTransform, new Vector2(0f, 1f), new Vector2(104f, -14f), new Vector2(124f, 30f));
        m_Rekor = Yazi("Deger", kutu.rectTransform, font, 34, Color.white, TextAnchor.LowerLeft);
        Yer(m_Rekor.rectTransform, new Vector2(0f, 0f), new Vector2(104f, 12f), new Vector2(124f, 46f), new Vector2(0f, 0f));

        // ---- OYNA: "OYNAMAK İÇİN DOKUN"
        var oyna = lo.runButton;
        var bg = oyna.GetComponent<Image>();
        if (bg != null) bg.color = new Color(1f, 1f, 1f, 0f);
        m_OynaYazi = oyna.GetComponentInChildren<Text>();
        if (m_OynaYazi != null)
        {
            // LuckiestGuy dinamik değil: en uygun boyut çalışmaz, sabit boy ve tek satır
            m_OynaYazi.resizeTextForBestFit = false; m_OynaYazi.fontSize = 38;
            m_OynaYazi.horizontalOverflow = HorizontalWrapMode.Overflow; m_OynaYazi.verticalOverflow = VerticalWrapMode.Overflow;
            m_OynaYazi.color = Color.white;
            var o = m_OynaYazi.GetComponent<Outline>() ?? m_OynaYazi.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.08f, 0.25f, 0.9f); o.effectDistance = new Vector2(3f, -3f);
        }

        // ---- alt şerit: arka plansız, sarı yazı
        var bar = safe.Find("MenuBottomBar");
        if (bar != null)
        {
            var bi = bar.GetComponent<Image>(); if (bi != null) bi.color = new Color(0f, 0f, 0f, 0f);
            foreach (Transform b in bar)
            {
                var ust = b.GetComponent<Image>();
                if (ust != null) { ust.sprite = s_Hap; ust.type = Image.Type.Sliced; ust.color = new Color(0.16f, 0.36f, 0.85f, 0.95f); }
                foreach (var t in b.GetComponentsInChildren<Text>(true))
                {
                    t.color = new Color(1f, 0.84f, 0.18f);
                    var o = t.GetComponent<Outline>() ?? t.gameObject.AddComponent<Outline>();
                    o.effectColor = new Color(0.05f, 0.05f, 0.2f, 1f); o.effectDistance = new Vector2(2f, -2f);
                }
            }
        }
    }

    void Tazele()
    {
        var pd = PlayerData.instance;
        if (pd == null) return;
        int rekor = pd.highscores.Count > 0 ? pd.highscores[0].score : 0;
        if (m_Rekor != null) m_Rekor.text = rekor.ToString();
        Character c = pd.characters.Count > pd.usedCharacter ? CharacterDatabase.GetCharacter(pd.characters[pd.usedCharacter]) : null;
        if (m_Simge != null) { m_Simge.sprite = c != null ? c.icon : null; m_Simge.enabled = m_Simge.sprite != null; }
    }

    void Update()
    {
        if (m_Lo == null || m_OynaYazi == null) return;
        bool hazir = m_Lo.runButton.interactable;
        if (hazir && m_OynaYazi.text != Loc.Get("ui.tapToPlay", "OYNAMAK İÇİN DOKUN")) m_OynaYazi.text = Loc.Get("ui.tapToPlay", "OYNAMAK İÇİN DOKUN");
        m_Nabiz += Time.unscaledDeltaTime;
        float s = hazir ? 1f + 0.06f * Mathf.Sin(m_Nabiz * 4f) : 1f;
        m_OynaYazi.transform.localScale = new Vector3(s, s, 1f);
        // karakter değiştirilince simge yenilenir
        if (Time.frameCount % 20 == 0) Tazele();
    }

    static void Yer(RectTransform r, Vector2 capa, Vector2 konum, Vector2 boyut, Vector2? pivot = null)
    {
        r.anchorMin = r.anchorMax = capa; r.pivot = pivot ?? capa; r.anchoredPosition = konum; r.sizeDelta = boyut;
    }

    static Image Resim(string ad, Transform ebeveyn, Sprite s, Color renk)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Image)); go.transform.SetParent(ebeveyn, false);
        var im = go.GetComponent<Image>(); im.sprite = s; im.color = renk; im.raycastTarget = false; return im;
    }

    static Text Yazi(string ad, Transform ebeveyn, Font f, int boy, Color renk, TextAnchor hiza)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Text)); go.transform.SetParent(ebeveyn, false);
        var t = go.GetComponent<Text>(); t.font = f; t.fontSize = boy; t.color = renk; t.alignment = hiza; t.raycastTarget = false;
        t.resizeTextForBestFit = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = go.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.5f); o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static Sprite HapSprite()
    {
        const int n = 64, yar = 20;
        var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
        {
            float dx = Mathf.Max(0f, Mathf.Max(yar - x, x - (n - 1 - yar))), dy = Mathf.Max(0f, Mathf.Max(yar - y, y - (n - 1 - yar)));
            tx.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(yar + 0.5f - Mathf.Sqrt(dx * dx + dy * dy))));
        }
        tx.Apply();
        return Sprite.Create(tx, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(yar, yar, yar, yar));
    }
}
