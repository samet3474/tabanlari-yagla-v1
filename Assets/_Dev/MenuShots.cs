#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Ana menü ekran uyumu denetimi: yalnızca test derlemesinde. Masaüstü (pencereli, -batchmode olmadan):
//   -menuShots <klasör> <ad> [güvenliÜst güvenliAlt güvenliSol güvenliSağ]   (piksel; telefonun çentik/çubuk payları)
// Menü açılınca: öğelerin ekran ve güvenli alan içinde olduğunu, birbirine binmediğini, yazıların kesilmediğini ve
// karakterin görünür olduğunu denetler ("MS|" satırları), ekran görüntüsü alır (menü, eğitim perdesiyle menü, ayarlar,
// görevler) ve çıkar. Kayıt yalnızca bellekte değiştirilir (eğitim tamam/değil), diske yazılmaz.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class MenuShots : MonoBehaviour
{
    string m_Dir, m_Name;
    static int s_MainLoads;
    int m_Fail, m_Pass;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-menuShots");
        if (i < 0 || i + 2 >= args.Length) return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { if (s.name == "Main") s_MainLoads++; };
        var go = new GameObject("MenuShots");
        DontDestroyOnLoad(go);
        var m = go.AddComponent<MenuShots>();
        m.m_Dir = args[i + 1]; m.m_Name = args[i + 2];
        float F(int k) => i + k < args.Length && float.TryParse(args[i + k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        float top = F(3), bottom = F(4), left = F(5), right = F(6);
        if (top + bottom + left + right > 0f)
            SafeAreaFitter.debugSafeArea = new Rect(left, bottom, Screen.width - left - right, Screen.height - top - bottom);
        Directory.CreateDirectory(m.m_Dir);
    }

    static void Out(string s) => Debug.Log("MS| " + s);
    void Check(bool ok, string what) { if (ok) m_Pass++; else m_Fail++; Out((ok ? "GECTI " : "KALDI ") + what); }

    IEnumerator Start()
    {
        // Açılıştaki yükleme ekranı (Start sahnesi): görüntü ve çubuğun dolmaya başladığı
        yield return null;
        yield return null;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main")
        {
            // 1) Yalnız logo: lacivert zemin, ortada logo, çubuk ve ipucu henüz görünmez.
            var sbLogo = FindAnyObjectByType<StartButton>();
            Check(sbLogo != null && sbLogo.logo != null && sbLogo.logo.gameObject.activeInHierarchy && sbLogo.loadingGroup != null && sbLogo.loadingGroup.alpha < 0.01f
                  && Mathf.Abs(sbLogo.logo.anchoredPosition.y) < 1f,
                "acilista once yalniz logo (ortada, cubuk gizli)");
            yield return Shot("acilis-logo");
            // 2) Yükleme ekranı: logo yukarıda, ipucu ve çubuk görünür.
            for (float g = 0f; g < 3f && sbLogo != null && sbLogo.loadingGroup != null && sbLogo.loadingGroup.alpha < 0.99f; g += Time.unscaledDeltaTime)
                yield return null;
        }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main")
        {
            // Önce denetim: görüntü alınırken ana menüye geçilebilir ve bu nesneler silinir.
            var sbLoad = FindAnyObjectByType<StartButton>();
            Check(sbLoad != null && sbLoad.loadingGroup != null && sbLoad.loadingGroup.alpha > 0.99f && sbLoad.logo.anchoredPosition.y > 300f,
                "yukleme ekraninda logo yukarida, cubuk gorunur");
            Check(sbLoad != null && sbLoad.progressFill != null && sbLoad.progressFill.gameObject.activeInHierarchy && sbLoad.tipText != null && sbLoad.tipText.text.Length > 0,
                $"yukleme ekraninda cubuk ve ipucu var ('{(sbLoad != null && sbLoad.tipText != null ? sbLoad.tipText.text : "")}', %{(sbLoad != null && sbLoad.progressFill != null ? sbLoad.progressFill.fillAmount * 100f : -1f):F0})");
            yield return Shot("yukleme-ekrani");
        }
        else Out("yukleme ekrani goruntu icin cok kisa surdu (menu zaten acik)");

        // Ekran boyutu ilk karelerde oturur; güvenli alan payını ona göre yeniden kur.
        yield return new WaitForSecondsRealtime(0.5f);
        var args = System.Environment.GetCommandLineArgs();
        int a = System.Array.IndexOf(args, "-menuShots");
        if (SafeAreaFitter.debugSafeArea.HasValue)
        {
            float P(int k) => float.Parse(args[a + k], System.Globalization.CultureInfo.InvariantCulture);
            SafeAreaFitter.debugSafeArea = new Rect(P(5), P(4), Screen.width - P(5) - P(6), Screen.height - P(3) - P(4));
        }

        // Başlık (yükleme) ekranı dokunmadan ana menüye geçmeli: kimse sahne yüklemez, yalnızca beklenir.
        var startButton = FindAnyObjectByType<StartButton>();
        if (startButton != null)
        {
            var sbButton = startButton.GetComponent<Button>();
            Check(startButton.autoStart && (sbButton == null || !sbButton.interactable), "baslik ekraninda OYNA dugmesi yok (otomatik gecis)");
        }
        float w = 0;
        float mainAt = -1f;
        LoadoutState lo = null;


        while (w < 60f)
        {
            if (mainAt < 0f && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main") mainAt = Time.realtimeSinceStartup;
            lo = GameManager.instance != null ? GameManager.instance.topState as LoadoutState : null;
            if (lo != null && lo.runButton.interactable && CharacterDatabase.loaded && lo.charPosition.childCount > 0) break;
            w += Time.unscaledDeltaTime; yield return null;
        }
        if (lo == null) { Out("HATA menu acilmadi (otomatik gecis olmadi)"); Application.Quit(); yield break; }
        Out($"ACILIS ana menu sahnesi {mainAt:F2}s, menu hazir (OYNA etkin, karakter yuklu) {Time.realtimeSinceStartup:F2}s (uygulama baslangicindan)");
        yield return new WaitForSecondsRealtime(1f);
        Check(s_MainLoads == 1, $"Main sahnesi bir kez yuklendi (yukleme={s_MainLoads}, cift yukleme yok)");
        Check(lo.charPosition.childCount == 1, $"menude tek karakter var ({lo.charPosition.childCount})");
        Out($"ACILIS kayit: tutorialDone={PlayerData.instance.tutorialDone} egitimPerdesi={lo.tutorialBlocker.activeSelf}");
        Check(lo.tutorialBlocker.activeSelf == !PlayerData.instance.tutorialDone, "egitim perdesi yalnizca egitim bitmemisse gorunuyor");

        // 1) Eğitimi bitmiş oyuncunun menüsü (değerler yalnızca görüntü için; Ayarlar kapanırken kayıt diske yazıldığı
        // için hemen ardından asıllarına döndürülür)
        bool savedDone = PlayerData.instance.tutorialDone;
        int savedCoins = PlayerData.instance.coins;
        PlayerData.instance.tutorialDone = true;
        lo.tutorialBlocker.SetActive(false);
        PlayerData.instance.coins = 12345; // sayaç gerçek değeri gösteriyor mu
        yield return new WaitForSecondsRealtime(2.5f); // karakter yüklenip bekleme animasyonuna geçsin
        Out($"ekran {Screen.width}x{Screen.height} guvenli={SafeAreaFitter.CurrentSafeArea()}");
        CheckLayout(lo);
        yield return Shot("menu");
        PlayerData.instance.tutorialDone = savedDone;
        PlayerData.instance.coins = savedCoins;

        // 2) İlk açılış: eğitim perdesi
        lo.tutorialBlocker.SetActive(true);
        lo.tutorialPrompt.SetActive(true);
        yield return Shot("egitim-perdesi");
        lo.tutorialBlocker.SetActive(false);

        // 3) Ayarlar ve görevler pencereleri
        var settings = lo.inventoryCanvas.GetComponentInChildren<SettingPopup>(true);
        settings.Open();
        yield return Shot("ayarlar");
        Check(InsideScreen((RectTransform)settings.transform.GetChild(0)), "ayarlar penceresi ekrana sigiyor");
        settings.Close();
        lo.missionPopup.CallOpen();
        yield return new WaitForSecondsRealtime(1f);
        yield return Shot("gorevler");
        lo.missionPopup.gameObject.SetActive(false);

        // 4) "Karakterler" düğmesi: gerçek tıklama yolu (onClick) mağazayı karakter sekmesiyle açmalı
        var charactersButton = lo.inventoryCanvas.transform.Find("MenuSafeArea/MenuBottomBar/CharactersButton").GetComponent<Button>();
        charactersButton.onClick.Invoke();
        ShopUI shop = null;
        w = 0;
        while (shop == null && w < 10f) { shop = FindAnyObjectByType<ShopUI>(); w += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(1.5f);
        Check(shop != null && shop.characterList.gameObject.activeInHierarchy && !shop.itemList.gameObject.activeInHierarchy,
            "Karakterler dugmesi magazayi karakter sekmesiyle acti");
        yield return Shot("magaza-karakterler");
        if (shop != null) shop.CloseScene();
        yield return new WaitForSecondsRealtime(1f);
        Check(GameManager.instance.topState is LoadoutState && FindAnyObjectByType<ShopUI>() == null, "magaza kapandi, menuye donuldu");

        Out($"OZET {m_Name} gecti={m_Pass} kaldi={m_Fail}");
        // Sonraki açılışı "kayıtlı, eğitimi bitmiş kullanıcı" yapmak için (test betiği kullanıcının kaydını sonra geri koyar).
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-menuKayitTamam") >= 0)
        {
            PlayerData.instance.tutorialDone = true;
            PlayerData.instance.Save();
            Out("KAYIT tutorialDone=true diske yazildi");
        }
        Application.Quit();
    }

    IEnumerator Shot(string what)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(m_Dir, $"{m_Name}-{what}.png"));
        yield return new WaitForSecondsRealtime(0.5f);
    }

    static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c); // tuval ekran üstünde: dünya köşeleri = ekran pikselleri
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    static bool InsideScreen(RectTransform rt)
    {
        Rect r = ScreenRect(rt);
        return r.xMin >= -1 && r.yMin >= -1 && r.xMax <= Screen.width + 1 && r.yMax <= Screen.height + 1;
    }

    static float OverlapArea(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return w > 0 && h > 0 ? w * h : 0f;
    }

    void CheckLayout(LoadoutState lo)
    {
        Transform safe = lo.inventoryCanvas.transform.Find("MenuSafeArea");
        Rect safeRect = SafeAreaFitter.CurrentSafeArea();
        var items = new List<(string, RectTransform)>
        {
            ("logo", (RectTransform)safe.Find("MenuLogo")),
            ("altin", (RectTransform)safe.Find("MenuCoins")),
            ("siralama", (RectTransform)safe.Find("OpenLeaderboard")),
            ("karakter-adi", (RectTransform)safe.Find("CharZone")),
            ("OYNA", (RectTransform)lo.runButton.transform),
        };
        foreach (Transform b in safe.Find("MenuBottomBar")) items.Add(("alt:" + b.name, (RectTransform)b));
        foreach (Transform b in lo.charSelect) items.Add(("ok:" + b.name, (RectTransform)b));

        foreach (var (n, rt) in items)
        {
            Rect r = ScreenRect(rt);
            bool inSafe = r.xMin >= safeRect.xMin - 1 && r.yMin >= safeRect.yMin - 1 && r.xMax <= safeRect.xMax + 1 && r.yMax <= safeRect.yMax + 1;
            Check(inSafe, $"{n} guvenli alan icinde ({r.xMin:F0},{r.yMin:F0})-({r.xMax:F0},{r.yMax:F0})");
        }
        for (int i = 0; i < items.Count; ++i)
            for (int j = i + 1; j < items.Count; ++j)
            {
                if (items[i].Item1.StartsWith("alt:") && items[j].Item1.StartsWith("alt:")) continue; // aynı şeritte yan yana; aşağıda ayrıca
                float o = OverlapArea(ScreenRect(items[i].Item2), ScreenRect(items[j].Item2));
                if (o > 1f) Check(false, $"{items[i].Item1} ile {items[j].Item1} ust uste ({o:F0} px2)");
            }
        Check(true, "ust uste binme taramasi bitti");
        var bar = new List<Rect>();
        foreach (Transform b in safe.Find("MenuBottomBar")) bar.Add(ScreenRect((RectTransform)b));
        bool barOk = true;
        for (int i = 1; i < bar.Count; ++i) barOk &= bar[i].xMin >= bar[i - 1].xMax - 1;
        Check(barOk && bar.Count == 4, $"alt menude 4 dugme yan yana, ust uste degil (en dar dugme {MinWidth(bar):F0} px)");
        // Dokunma alanı: ekran yüksekliğinin 1280'de 88 birimi (~9 mm iPhone'da) altına inmesin
        float unit = Screen.height / 1280f;
        Check(MinWidth(bar) >= 88f * unit * 0.95f, $"alt menu dugmeleri dokunmaya yeterince genis");

        // Yazılar kesilmiyor (en uygun boyutta tamamı görünüyor)
        foreach (var t in safe.GetComponentsInChildren<Text>(false))
        {
            if (string.IsNullOrEmpty(t.text)) continue;
            Canvas.ForceUpdateCanvases();
            int visible = t.cachedTextGenerator.characterCountVisible;
            int needed = t.text.Replace("\n", "").Replace(" ", "").Length;
            Check(visible >= needed, $"yazi tam gorunuyor: '{t.text}' ({t.cachedTextGenerator.fontSizeUsedForBestFit}px)");
            // Yatayda taşmaya izin verilen yazılar (alt şerit) kendi düğmesinin genişliğini aşmamalı.
            if (t.horizontalOverflow == HorizontalWrapMode.Overflow && t.transform.parent is RectTransform owner)
                Check(t.preferredWidth <= owner.rect.width + 1f, $"'{t.text}' dugmesine sigiyor (yazi {t.preferredWidth:F0} / dugme {owner.rect.width:F0} birim)");
        }

        // Karakter: menü kamerasından ekrandaki kutusu; ekranda, üst bilgi/OYNA/alt şerit ile en çok %15 kesişme
        Rect cr = CharacterScreenRect(lo);
        Out($"karakter ekran kutusu ({cr.xMin:F0},{cr.yMin:F0})-({cr.xMax:F0},{cr.yMax:F0}) yukseklik %{100f * cr.height / Screen.height:F0}");
        Check(cr.width > 0 && cr.yMin >= 0 && cr.yMax <= Screen.height && cr.xMin >= 0 && cr.xMax <= Screen.width, "karakter tamamen ekranda");
        foreach (var (n, rt) in items)
        {
            if (n.StartsWith("ok:")) continue;
            float o = OverlapArea(cr, ScreenRect(rt)) / Mathf.Max(1f, cr.width * cr.height);
            Check(o <= 0.15f, $"karakteri {n} en cok %15 kapatiyor (%{100f * o:F0})");
        }
    }

    static float MinWidth(List<Rect> rs) { float m = float.MaxValue; foreach (var r in rs) m = Mathf.Min(m, r.width); return m; }

    static Rect CharacterScreenRect(LoadoutState lo)
    {
        var rs = lo.charPosition.GetComponentsInChildren<Renderer>(false);
        if (rs.Length == 0 || lo.menuCamera == null) return new Rect();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        for (int i = 0; i < 8; ++i)
        {
            Vector3 c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 s = lo.menuCamera.WorldToScreenPoint(c);
            xMin = Mathf.Min(xMin, s.x); yMin = Mathf.Min(yMin, s.y); xMax = Mathf.Max(xMax, s.x); yMax = Mathf.Max(yMax, s.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
#endif
