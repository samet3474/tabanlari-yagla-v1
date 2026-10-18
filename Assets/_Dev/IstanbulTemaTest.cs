#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Test aracı: yalnızca geliştirme derlemesinde ve "-istanbulTemaTest a|b" argümanıyla çalışır; yayın derlemesine girmez.
// İstanbul temasını oyunun kendi akışıyla dener. Kayıt dosyasını DEĞİŞTİRİR; çalıştıran betik kullanıcının kaydını önce
// yedekler, sonra geri koyar (_araclar/istanbul/istanbul_tema_test.ps1).
//  a) tema yükleme, Day varsayılanı (PlayerData.NewSave), mağazadan ücretsiz alma (gerçek mağaza ekranı, 0 altın),
//     menüden seçme, kaydetme.
//  b) yeni süreçte: kayıttan yeniden açma, menüde İstanbul seçili, İstanbul koşusunun başlaması ve parçaları.
// Argümanlar: -istanbulTemaTest a|b [-istanbulTemaShots <klasör>]
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class IstanbulTemaTest : MonoBehaviour
{
    const string k_Theme = "Istanbul";
    const string k_ThemeLabel = "İSTANBUL";
    const string k_Segment = "Istiklal01";
    const string k_SimitGuid = "37cd61e3be182724a8e8edd2c906da90";
    const int k_ObstacleMask = 1 << 9;

    string m_Phase = "a", m_ShotDir;
    int m_Pass, m_Fail, m_Errors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-istanbulTemaTest");
        if (i < 0) return;
        var go = new GameObject("IstanbulTemaTest");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<IstanbulTemaTest>();
        if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) t.m_Phase = args[i + 1];
        int s = System.Array.IndexOf(args, "-istanbulTemaShots");
        if (s >= 0 && s + 1 < args.Length) t.m_ShotDir = args[s + 1];
    }

    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }
    void OnLog(string cond, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { m_Errors++; Out("HATA_LOGU " + cond); }
    }

    static void Out(string s) { Debug.Log("TEMA| " + s); }
    void Check(bool ok, string what) { if (ok) m_Pass++; else m_Fail++; Out((ok ? "GECTI " : "KALDI ") + what); }

    IEnumerator Start()
    {
        Out($"basladi asama={m_Phase}");
        float wait = 0;
        while (!(GameManager.instance != null && GameManager.instance.topState is LoadoutState && CharacterDatabase.loaded && ThemeDatabase.loaded) && wait < 60f)
        { wait += Time.unscaledDeltaTime; yield return null; }
        if (wait >= 60f) { Out("HATA: Loadout hazir olmadi"); Finish(); yield break; }
        yield return new WaitForSecondsRealtime(1.5f);
        if (m_Phase == "b") yield return PhaseB(); else yield return PhaseA();
        Finish();
    }

    IEnumerator PhaseA()
    {
        var loadout = (LoadoutState)GameManager.instance.topState;

        // Tema yükleme.
        var theme = ThemeDatabase.GetThemeData(k_Theme);
        Check(theme != null, "Istanbul temasi ThemeDatabase'de");
        if (theme == null) yield break;
        Check(theme.cost == 0 && theme.premiumCost == 0, $"fiyat 0 ({theme.cost}/{theme.premiumCost})");
        Check(theme.themeIcon != null, $"tema simgesi var ({(theme.themeIcon != null ? theme.themeIcon.name : "-")})");
        Check(Loc.ThemeName(k_Theme) == k_ThemeLabel, $"tema adi '{Loc.ThemeName(k_Theme)}'");
        Check(ThemeDatabase.GetThemeData("Day") != null && ThemeDatabase.GetThemeData("NightTime") != null, "Day ve NightTime temalari yerinde");
        Out($"temalar: {string.Join(", ", ThemeDatabase.dictionnary.Keys)}");
        int zoneSegs = 0, simitRefs = 0;
        bool resolved = true;
        foreach (var zone in theme.zones)
            foreach (var r in zone.prefabList)
            {
                var h = Addressables.LoadAssetAsync<GameObject>(r.RuntimeKey);
                yield return h;
                var seg = h.Status == AsyncOperationStatus.Succeeded && h.Result != null ? h.Result.GetComponent<TrackSegment>() : null;
                if (seg == null) { resolved = false; continue; }
                zoneSegs++;
                simitRefs += seg.possibleObstacles.Count(o => o.AssetGUID == k_SimitGuid);
                Out($"bolge parcasi {h.Result.name}: engel noktasi=[{string.Join(",", seg.obstaclePositions)}] olasi_engel={seg.possibleObstacles.Length}");
                Addressables.Release(h);
            }
        Check(resolved && zoneSegs == 1 && theme.zones.Length == 1 && theme.zones[0].length == 500, $"bolge ve parca referanslari cozuluyor (bolge={theme.zones.Length}, parca={zoneSegs})");
        Check(simitRefs == 0, "uretim parcasinda simit yok (Meshy §2.4)");

        // Day varsayılanı: yeni kayıt.
        PlayerData.NewSave();
        Check(PlayerData.instance.themes.Count == 1 && PlayerData.instance.themes[0] == "Day" && PlayerData.instance.usedTheme == 0,
            $"yeni kayit yalniz Day ile basliyor ({string.Join(",", PlayerData.instance.themes)}, secili={PlayerData.instance.usedTheme})");
        PlayerData.instance.tutorialDone = true;   // magaza ve normal kosu icin (b asamasi)
        PlayerData.instance.Save();
        yield return null;
        Check(!loadout.themeSelect.gameObject.activeSelf, "tek temada tema secici gizli");

        // Mağaza: ücretsiz alma (0 altın).
        loadout.GoToStore();
        ShopUI shop = null;
        for (float t = 0; t < 10f && shop == null; t += Time.unscaledDeltaTime) { shop = FindAnyObjectByType<ShopUI>(); yield return null; }
        Check(shop != null, "magaza acildi");
        if (shop == null) yield break;
        yield return new WaitForSecondsRealtime(1f);
        shop.OpenThemeList();
        ShopItemListItem item = null;
        for (float t = 0; t < 10f && item == null; t += Time.unscaledDeltaTime)
        {
            item = shop.themeList.listRoot.GetComponentsInChildren<ShopItemListItem>(true).FirstOrDefault(x => x.nameText != null && x.nameText.text == k_ThemeLabel);
            yield return null;
        }
        Check(item != null, "magazada tema listesinde ISTANBUL var");
        if (item == null) yield break;
        yield return new WaitForSecondsRealtime(0.5f);
        int coins = PlayerData.instance.coins;
        Check(item.pricetext.text == "0" && item.buyButton.interactable, $"ISTANBUL fiyati '{item.pricetext.text}', 0 altinla alinabilir ({coins} altin)");
        Out("magaza temalari: " + string.Join(" | ", shop.themeList.listRoot.GetComponentsInChildren<ShopItemListItem>(true).Select(x => $"{x.nameText.text}={x.pricetext.text}")));
        yield return Shot("magaza-once");
        item.buyButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        Check(PlayerData.instance.themes.Contains(k_Theme) && PlayerData.instance.themes.Count == 2 && PlayerData.instance.coins == coins,
            $"ucretsiz alindi (temalar={string.Join(",", PlayerData.instance.themes)}, altin {coins}->{PlayerData.instance.coins})");
        // Buy() listeyi Populate() ile yeniden kurar: düğme yeni öğede aranır.
        ShopItemListItem after = null;
        for (float t = 0; t < 5f && after == null; t += Time.unscaledDeltaTime)
        {
            after = shop.themeList.listRoot.GetComponentsInChildren<ShopItemListItem>(false).FirstOrDefault(x => x != item && x.nameText != null && x.nameText.text == k_ThemeLabel);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.3f);
        Check(after != null && !after.buyButton.interactable, $"alindiktan sonra (yeniden kurulan listede) dugme pasif ({(after != null ? after.buyButton.GetComponentInChildren<UnityEngine.UI.Text>().text : "-")})");
        yield return Shot("magaza-sonra");
        shop.CloseScene();
        yield return new WaitForSecondsRealtime(1f);

        // Menü: iki tema, İstanbul'u seç.
        Check(loadout.themeSelect.gameObject.activeSelf, "iki temada menude tema secici gorunur");
        loadout.ChangeTheme(1);
        yield return new WaitForSecondsRealtime(0.5f);
        Check(PlayerData.instance.themes[PlayerData.instance.usedTheme] == k_Theme, $"menuden Istanbul secildi (secili={PlayerData.instance.usedTheme})");
        Check(loadout.themeNameDisplay.text == k_ThemeLabel && loadout.themeIcon.sprite == theme.themeIcon, $"menude ad '{loadout.themeNameDisplay.text}' ve simge Istanbul");
        Check(PlayerData.instance.themes[0] == "Day", "Day listede ilk sirada kaliyor");
        PlayerData.instance.Save();
        yield return Shot("menu-istanbul");
    }

    IEnumerator PhaseB()
    {
        var loadout = (LoadoutState)GameManager.instance.topState;
        var pd = PlayerData.instance;
        Check(pd.themes.Count == 2 && pd.themes[0] == "Day" && pd.themes[1] == k_Theme && pd.usedTheme == 1,
            $"yeniden acilista kayit: temalar={string.Join(",", pd.themes)} secili={pd.usedTheme}");
        yield return new WaitForSecondsRealtime(0.5f);
        Check(loadout.themeNameDisplay.text == k_ThemeLabel && loadout.themeSelect.gameObject.activeSelf, $"menude '{loadout.themeNameDisplay.text}' secili, tema secici gorunur");
        yield return Shot("menu-yeniden-acilis");

        // İstanbul koşusu.
        var gs = GameManager.instance.FindState("Game") as GameState;
        var tm = gs.trackManager;
        tm.invincible = true;
        var segs = new List<TrackSegment>();
        loadout.StartGame();
        tm.newSegmentCreated = s => segs.Add(s);
        float w = 0;
        while (!tm.isMoving && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
        Check(tm.isMoving && !tm.isTutorial, $"kosu basladi (bekleme {w:F1}s, egitim={tm.isTutorial})");
        Check(tm.currentTheme != null && tm.currentTheme.themeName == k_Theme, $"kosunun temasi '{(tm.currentTheme != null ? tm.currentTheme.themeName : "-")}'");
        yield return new WaitForSecondsRealtime(2f);
        yield return Shot("kosu");
        yield return new WaitForSecondsRealtime(23f);

        int other = 0, joins = 0, coins = 0, coinHits = 0, simits = 0; float gap = 0, ang = 0;
        var types = new Dictionary<string, int>();
        for (int i = 0; i < tm.segments.Count; ++i)
        {
            var s = tm.segments[i]; if (s == null) continue;
            if (!s.name.Contains(k_Segment)) other++;
            if (i > 0 && tm.segments[i - 1] != null)
            {
                tm.segments[i - 1].GetPointAt(1f, out Vector3 ep, out Quaternion er);
                s.GetPointAt(0f, out Vector3 np, out Quaternion nr);
                joins++; gap = Mathf.Max(gap, Vector3.Distance(ep, np)); ang = Mathf.Max(ang, Quaternion.Angle(er, nr));
            }
            foreach (Transform o in s.objectRoot)
            {
                if (o.GetComponent<Collider>() == null) continue;
                string n = o.name.Replace("(Clone)", "").Trim();
                types.TryGetValue(n, out int c); types[n] = c + 1;
                if (n.Contains("SimitTezgahi")) simits++;
            }
            foreach (Transform coin in s.collectibleTransform)
            {
                if (!coin.gameObject.activeInHierarchy) continue;
                coins++;
                if (Physics.OverlapSphere(coin.position, 0.4f, k_ObstacleMask, QueryTriggerInteraction.Collide).Length > 0) coinHits++;
            }
        }
        Out($"OLCUM parca_yeni={segs.Count} etkin_parca={tm.segments.Count} mesafe={tm.worldDistance:F0}m engeller: {string.Join(" ", types.Select(kv => kv.Key + "=" + kv.Value))}");
        Check(segs.Count >= 10 && other == 0 && segs.All(s => s == null || s.name.Contains(k_Segment)), $"kosu yalniz Istiklal01 parcalarindan ({segs.Count} parca, baska={other})");
        Check(joins >= 5 && gap < 0.01f && ang < 0.5f, $"parca birlesimleri (bosluk={gap:F4}m aci={ang:F2})");
        Check(simits == 0, "kosuda simit tezgahi yok");
        Check(types.Count >= 2, $"engeller yerlesiyor ({types.Count} tur)");
        Check(coins > 20 && coinHits == 0, $"altinlar engellerin icinde degil ({coinHits}/{coins})");
    }

    IEnumerator Shot(string what)
    {
        if (m_ShotDir == null) yield break;
        Directory.CreateDirectory(m_ShotDir);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(m_ShotDir, $"tema-{what}.png"));
        Out("goruntu " + what);
        yield return null;
    }

    void Finish()
    {
        Out($"OZET asama={m_Phase} gecti={m_Pass} kaldi={m_Fail} hata_logu={m_Errors}");
        Application.Quit();
    }
}
#endif
