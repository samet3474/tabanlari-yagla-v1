#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Parkur parça test akışı: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir;
// yayın derlemesine girmez. Adreste/argümanda "parcatest" yoksa hiçbir şey yapmaz.
//   Web:      ?parcatest=sira      tüm tasarlanmış parçalar sırayla (İ1–İ6, K01–K13), döngüde
//             ?parcatest=karisik   gerçek İstanbul bölgeleri (Giriş → Orta → Zor; üretimdeki gibi rastgele)
//             ?parcatest=K05       yalnız tek parça (I1…I6, K01…K13)
//             &hiz=10|20|30        (isteğe bağlı) koşu bu hızda sabit kalır
//             &olumsuz=1           (isteğe bağlı) çarpınca can gitmez (TrackManager.invincible)
//             &otobasla=1          (isteğe bağlı) OYNA'ya basmadan koşu başlar (otomatik test)
//   Masaüstü: -parcatest <değer> [-hiz <n>] [-olumsuz]
// Yalnız bu sürecin belleğinde: İstanbul teması seçilir (yoksa eklenir), eğitim tamam sayılır, İstanbul bölgeleri ayarlanır.
// Kayıt, tema dosyası ve prefablar değişmez (oyun kendi akışında kaydederse bu test adresinin deposuna yazılır).
// Sıralı modda her bölgenin uzunluğu -1'dir: TrackManager.SpawnNewSegment her segmentte bölge değiştirir, böylece aynı
// karede üretilen ilk segmentler de sırayla gelir. Oyuncu OYNA'ya kendisi basar. Ekranın üstünde parça adı, parçada
// toplanan/tasarlanan altın, çarpma ve hız gösterilir.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class ParcaTestAkisi : MonoBehaviour
{
    // Tasarlanmış parçalar (ad, başlık, GUID): ParcaTestListesi, _araclar/parkur/kayit_uygula.js üretir.
    static string[] k_Guid => ParcaTestListesi.guid;
    static string[] k_Baslik => ParcaTestListesi.baslik;

    string m_Mod;
    float m_Hiz;
    bool m_Olumsuz, m_Hazir, m_Tavanda;
    string m_Durum = "PARCA TEST: hazirlaniyor";
    TrackManager m_TM;
    FieldInfo m_SpeedField;
    TrackSegment m_SonParca;
    int m_ParcaGiris, m_ParcaTasarim, m_Carpma, m_SonCan = -1, m_SonTokez;
    string m_ParcaAd = "-", m_OncekiOzet = "";
    GUIStyle m_Stil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        string mod = Param("parcatest");
        if (string.IsNullOrEmpty(mod) || Object.FindAnyObjectByType<ParcaTestAkisi>() != null) return;
        var go = new GameObject("ParcaTestAkisi");
        DontDestroyOnLoad(go);
        var p = go.AddComponent<ParcaTestAkisi>();
        p.m_Mod = mod.ToLowerInvariant();
        float.TryParse(Param("hiz"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out p.m_Hiz);
        p.m_Olumsuz = Param("olumsuz") == "1";
    }

    // Web: adres sorgusu; masaüstü: komut satırı (-anahtar değer, değersiz anahtar "1").
    static string Param(string ad)
    {
        string url = Application.platform == RuntimePlatform.WebGLPlayer ? Application.absoluteURL : "";
        int q = url.IndexOf('?');
        if (q >= 0)
            foreach (string kv in url.Substring(q + 1).Split('&', '#'))
            {
                int e = kv.IndexOf('=');
                string k = e >= 0 ? kv.Substring(0, e) : kv;
                if (k == ad) return e >= 0 ? UnityEngine.Networking.UnityWebRequest.UnEscapeURL(kv.Substring(e + 1)) : "1";
            }
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-" + ad);
        if (i < 0) return null;
        return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "1";
    }

    // "k05" / "i3" → listedeki sırası (IstiklalK05 / IstiklalI3); yoksa -1.
    static int TekParca(string mod) => System.Array.FindIndex(ParcaTestListesi.ad, a => a.ToLowerInvariant() == "istiklal" + mod);

    IEnumerator Start()
    {
        float bekle = 0f;
        while (!(GameManager.instance != null && GameManager.instance.topState is LoadoutState && ThemeDatabase.loaded && PlayerData.instance != null) && bekle < 60f)
        { bekle += Time.unscaledDeltaTime; yield return null; }
        if (bekle >= 60f) { m_Durum = "PARCA TEST: HATA menu hazir olmadi"; yield break; }

        var ist = ThemeDatabase.GetThemeData("Istanbul");
        if (ist == null) { m_Durum = "PARCA TEST: HATA Istanbul temasi yok"; Debug.Log("PARCATEST| HATA Istanbul temasi yok"); yield break; }

        string ozet;
        if (m_Mod == "sira")
        {
            // Spawn k, bölge (k+1)%N'yi kullanır (ilk spawn'da da bölge değişir); ilk 2 güvenli segmentten sonra ilk parça gelsin.
            int n = k_Guid.Length;
            var zones = new ThemeZone[n];
            for (int z = 0; z < n; ++z)
                zones[z] = new ThemeZone { length = -1, prefabList = new[] { new AssetReference(k_Guid[((z - 3) % n + n) % n]) } };
            ist.zones = zones;
            ozet = $"sira {n} parca (dongu)";
        }
        else if (TekParca(m_Mod) >= 0)
        {
            int n = TekParca(m_Mod);
            ist.zones = new[] { new ThemeZone { length = 500, prefabList = new[] { new AssetReference(k_Guid[n]) } } };
            ozet = "tek parca " + k_Baslik[n];
        }
        else
        {
            ozet = "karisik (uretimdeki Istanbul bolgesi)";
        }

        var pd = PlayerData.instance;
        if (!pd.themes.Contains("Istanbul")) pd.themes.Add("Istanbul");
        pd.usedTheme = pd.themes.IndexOf("Istanbul");
        pd.tutorialDone = true;
        // &karakter=Samet / -karakter Berivan: koşu bu karakterle (test kaydında yoksa eklenir)
        string karakter = Param("karakter");
        if (!string.IsNullOrEmpty(karakter) && karakter != "1")
        {
            if (!pd.characters.Contains(karakter)) pd.characters.Add(karakter);
            pd.usedCharacter = pd.characters.IndexOf(karakter);
            Debug.Log("PARCATEST| karakter " + karakter);
        }
        var loadout = (LoadoutState)GameManager.instance.topState;
        loadout.Refresh();

        var gs = GameManager.instance.FindState("Game") as GameState;
        m_TM = gs.trackManager;
        if (m_Olumsuz) m_TM.invincible = true;
        m_SpeedField = typeof(TrackManager).GetField("m_Speed", BindingFlags.Instance | BindingFlags.NonPublic);
        m_Hazir = true;
        m_Durum = $"PARCA TEST: {ozet}{(m_Hiz > 0 ? $" | hiz {m_Hiz:F0} m/s sabit" : "")}{(m_Olumsuz ? " | olumsuz" : "")} | OYNA'ya bas";
        Debug.Log("PARCATEST| " + m_Durum);
        // &otobasla=1 / -otobasla: OYNA'ya basılmış gibi koşuyu başlat (otomatik testler için)
        if (Param("otobasla") == "1") { yield return new WaitForSecondsRealtime(1f); loadout.StartGame(); Debug.Log("PARCATEST| otomatik basladi"); }
        // -kare <klasör>: geri sayım sırasında da 3 kare (karakter beklemede mi, koşuyor mu)
        string kk = Param("kare");
        if (Param("otobasla") == "1" && kk != null && kk != "1")
            for (int i = 0; i < 3; ++i) { yield return new WaitForSecondsRealtime(0.7f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(kk, "baslangic-" + i + ".png")); }
    }

    int m_AyakkabiAsama; float m_AyakkabiZaman;
    int m_KareSayisi; float m_KareZaman;
    int m_HareketAsama, m_HareketKare;

    void Update()
    {
        if (!m_Hazir || m_TM == null || !m_TM.isMoving) return;
        if (m_Hiz > 0f && m_SpeedField != null) m_SpeedField.SetValue(m_TM, Mathf.Clamp(m_Hiz, m_TM.minSpeed, m_TM.maxSpeed));

        var cc = m_TM.characterController;
        // -hareket <klasör>: 25 m'de zıpla, 45 m'de kay; her birinde 0,1 sn arayla 8 kare (Windows)
        string hk = Param("hareket");
        if (hk != null && hk != "1")
        {
            if (m_HareketAsama == 0 && m_TM.worldDistance > 25f) { cc.Jump(); m_HareketAsama = 1; m_HareketKare = 0; m_KareZaman = Time.time; Debug.Log("PARCATEST| hareket zipla"); }
            else if (m_HareketAsama == 1 && m_HareketKare >= 8 && m_TM.worldDistance > 45f) { cc.Slide(); m_HareketAsama = 2; m_HareketKare = 0; m_KareZaman = Time.time; Debug.Log("PARCATEST| hareket kay"); }
            if ((m_HareketAsama == 1 || m_HareketAsama == 2) && m_HareketKare < 8 && Time.time - m_KareZaman >= 0.1f)
            {
                m_KareZaman = Time.time;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(hk, (m_HareketAsama == 1 ? "zipla-" : "kay-") + (m_HareketKare++).ToString("00") + ".png"));
            }
        }
        // -kare <klasör>: 25 m'den sonra 0,1 sn arayla 12 kare (koşu animasyonunu incelemek için, Windows)
        if (m_KareSayisi < 12 && Param("kare") != null && Param("kare") != "1" && m_TM.worldDistance > 25f && Time.time - m_KareZaman >= 0.1f)
        {
            m_KareZaman = Time.time;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Param("kare"), "kosu-" + (m_KareSayisi++).ToString("00") + ".png"));
        }
        // &ayakkabi=1 / -ayakkabi <klasör>: koşunun başında süper ayakkabıyı giydirir; Windows'ta klasöre 3 ekran görüntüsü
        if (m_AyakkabiAsama == 0 && Param("ayakkabi") != null && m_TM.worldDistance > 20f && m_TM.currentTheme.superAyakkabi != null)
        {
            cc.SuperAyakkabiGiy(m_TM.currentTheme.superAyakkabi.GetComponent<SuperAyakkabi>());
            m_AyakkabiAsama = 1; m_AyakkabiZaman = Time.time;
            Debug.Log("PARCATEST| super ayakkabi giydirildi");
        }
        else if (m_AyakkabiAsama >= 1 && m_AyakkabiAsama <= 3 && Time.time - m_AyakkabiZaman > m_AyakkabiAsama * 0.9f)
        {
            if (m_AyakkabiAsama == 2) cc.Jump();
            string klasor = Param("ayakkabi");
            if (klasor != "1" && Application.platform != RuntimePlatform.WebGLPlayer)
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(klasor, "ayakkabi-oyun-" + m_AyakkabiAsama + ".png"));
            Debug.Log("PARCATEST| ayakkabi kalan=" + cc.superAyakkabiKalan.ToString("F1") + " zemin=" + cc.zeminYuksekligi.ToString("F2"));
            m_AyakkabiAsama++;
        }
        if (m_SonCan >= 0 && cc.currentLife < m_SonCan) { m_Carpma++; Debug.Log($"PARCATEST| carpma {m_ParcaAd} mesafe={m_TM.worldDistance:F0}m"); }
        m_SonCan = cc.currentLife;
        if (cc.tokezlemeSayisi != m_SonTokez)
        {
            m_SonTokez = cc.tokezlemeSayisi;
            if (m_SonTokez > 0) Debug.Log($"PARCATEST| tokezleme {m_ParcaAd} mesafe={m_TM.worldDistance:F0}m (yandan carpma, geri sekti)");
        }
        bool tavan = cc.zeminYuksekligi > 0.5f;
        if (tavan != m_Tavanda)
        {
            m_Tavanda = tavan;
            Debug.Log($"PARCATEST| tavan {(tavan ? "cikti" : "indi")} {m_ParcaAd} mesafe={m_TM.worldDistance:F0}m yukseklik={cc.zeminYuksekligi:F2}");
        }

        TrackSegment seg = null;
        try { seg = m_TM.currentSegment; } catch { }
        if (seg != null && seg != m_SonParca)
        {
            // önceki segment bu sırada bırakılmış olabilir (uzun parçalar): Unity null karşılaştırması yerine sayaç
            if (m_ParcaTasarim > 0)
                m_OncekiOzet = $"onceki {m_ParcaAd}: altin {cc.coins - m_ParcaGiris}/{m_ParcaTasarim}";
            m_SonParca = seg;
            m_ParcaGiris = cc.coins;
            string ad = seg.name.Replace("(Clone)", "").Trim();
            int i = System.Array.IndexOf(ParcaTestListesi.ad, ad);
            m_ParcaAd = i >= 0 ? k_Baslik[i] : ad;
            var tas = seg.GetComponent<TasarimliSatir>();
            m_ParcaTasarim = 0;
            if (tas != null)
                foreach (var a in tas.altinlar)
                    foreach (var _ in TasarimliSatir.Konumlar(a)) m_ParcaTasarim++;
            Debug.Log($"PARCATEST| parca {m_ParcaAd} mesafe={m_TM.worldDistance:F0}m hiz={m_TM.speed:F1} {m_OncekiOzet}");
        }
    }

    void OnGUI()
    {
        if (m_Stil == null)
        {
            m_Stil = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 45), wordWrap = true };
            m_Stil.normal.textColor = Color.yellow;
        }
        string metin = m_Durum;
        if (m_TM != null && m_TM.isMoving)
            metin = $"PARCA: {m_ParcaAd} | altin {m_TM.characterController.coins - m_ParcaGiris}/{m_ParcaTasarim} | carpma {m_Carpma} | tokez {m_TM.characterController.tokezlemeSayisi} | hiz {m_TM.speed:F0}{(m_TM.characterController.zeminYuksekligi > 0.5f ? " | TAVAN" : "")}\n{m_OncekiOzet}";
        var r = new Rect(8, Screen.height * 0.16f, Screen.width - 16, Screen.height * 0.12f);
        GUI.color = new Color(0, 0, 0, 0.55f);
        GUI.DrawTexture(new Rect(r.x - 4, r.y - 2, r.width + 8, m_Stil.CalcHeight(new GUIContent(metin), r.width) + 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, metin, m_Stil);
    }
}
#endif
