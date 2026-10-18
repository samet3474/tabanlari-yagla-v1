#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Parkur parça test akışı: yalnızca geliştirme derlemesinde veya TY_DEVTOOLS sembollü test derlemesinde derlenir;
// yayın derlemesine girmez. Adreste/argümanda "parcatest" yoksa hiçbir şey yapmaz.
//   Web:      ?parcatest=sira      İ1 → İ6 sırayla, döngüde
//             ?parcatest=karisik   gerçek İstanbul bölgesi (Istiklal01 + İ1–İ6 rastgele; üretimdeki gibi)
//             ?parcatest=I3        yalnız tek parça (I1…I6)
//             &hiz=10|20|30        (isteğe bağlı) koşu bu hızda sabit kalır
//             &olumsuz=1           (isteğe bağlı) çarpınca can gitmez (TrackManager.invincible)
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
    // IstiklalI1…I6 prefab GUID'leri (Assets/_TabanlariYagla/Istanbul/Istiklal/Tasarim)
    static readonly string[] k_Guid =
    {
        "572b95a7e11900047ae79005a1f26bae", "5e36f94e1c2710744a9fc1b00e470734", "8fc8f33e7dbcf5747a64e37dcb173b60",
        "b44396b3a6a4b934ba54390e5291066b", "981c09d3751b80641b6d612ca005c334", "0afa23de69ba19e46934858ffd479a21",
    };
    static readonly string[] k_Baslik = { "I1 Tek engel", "I2 Kaldirim kapaniyor", "I3 Bariyer atla", "I4 Kayarak gec", "I5 Risk-odul", "I6 Yol calismasi" };

    string m_Mod;
    float m_Hiz;
    bool m_Olumsuz, m_Hazir;
    string m_Durum = "PARCA TEST: hazirlaniyor";
    TrackManager m_TM;
    FieldInfo m_SpeedField;
    TrackSegment m_SonParca;
    int m_ParcaGiris, m_ParcaTasarim, m_Carpma, m_SonCan = -1;
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
            // Spawn k, bölge (k+1)%6'yı kullanır (ilk spawn'da da bölge değişir); ilk 2 güvenli segmentten sonra İ1 gelsin.
            var zones = new ThemeZone[6];
            for (int z = 0; z < 6; ++z)
                zones[z] = new ThemeZone { length = -1, prefabList = new[] { new AssetReference(k_Guid[(z + 3) % 6]) } };
            ist.zones = zones;
            ozet = "sira I1->I6 (dongu)";
        }
        else if (m_Mod.Length == 2 && m_Mod[0] == 'i' && m_Mod[1] >= '1' && m_Mod[1] <= '6')
        {
            int n = m_Mod[1] - '1';
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
        var loadout = (LoadoutState)GameManager.instance.topState;
        loadout.Refresh();

        var gs = GameManager.instance.FindState("Game") as GameState;
        m_TM = gs.trackManager;
        if (m_Olumsuz) m_TM.invincible = true;
        m_SpeedField = typeof(TrackManager).GetField("m_Speed", BindingFlags.Instance | BindingFlags.NonPublic);
        m_Hazir = true;
        m_Durum = $"PARCA TEST: {ozet}{(m_Hiz > 0 ? $" | hiz {m_Hiz:F0} m/s sabit" : "")}{(m_Olumsuz ? " | olumsuz" : "")} | OYNA'ya bas";
        Debug.Log("PARCATEST| " + m_Durum);
    }

    void Update()
    {
        if (!m_Hazir || m_TM == null || !m_TM.isMoving) return;
        if (m_Hiz > 0f && m_SpeedField != null) m_SpeedField.SetValue(m_TM, Mathf.Clamp(m_Hiz, m_TM.minSpeed, m_TM.maxSpeed));

        var cc = m_TM.characterController;
        if (m_SonCan >= 0 && cc.currentLife < m_SonCan) m_Carpma++;
        m_SonCan = cc.currentLife;

        TrackSegment seg = null;
        try { seg = m_TM.currentSegment; } catch { }
        if (seg != null && seg != m_SonParca)
        {
            if (m_SonParca != null && m_ParcaTasarim > 0)
                m_OncekiOzet = $"onceki {m_ParcaAd}: altin {cc.coins - m_ParcaGiris}/{m_ParcaTasarim}";
            m_SonParca = seg;
            m_ParcaGiris = cc.coins;
            string ad = seg.name.Replace("(Clone)", "").Trim();
            int i = System.Array.FindIndex(k_Baslik, b => ad == "IstiklalI" + b.Substring(1, 1));
            m_ParcaAd = i >= 0 ? k_Baslik[i] : ad;
            var tas = seg.GetComponent<TasarimliSatir>();
            m_ParcaTasarim = 0;
            if (tas != null)
                foreach (var a in tas.altinlar)
                    for (float m = a.baslangic; m <= a.bitis + 0.001f; m += TasarimliSatir.k_AltinAraligi) m_ParcaTasarim++;
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
            metin = $"PARCA: {m_ParcaAd} | altin {m_TM.characterController.coins - m_ParcaGiris}/{m_ParcaTasarim} | carpma {m_Carpma} | hiz {m_TM.speed:F0}\n{m_OncekiOzet}";
        var r = new Rect(8, Screen.height * 0.16f, Screen.width - 16, Screen.height * 0.12f);
        GUI.color = new Color(0, 0, 0, 0.55f);
        GUI.DrawTexture(new Rect(r.x - 4, r.y - 2, r.width + 8, m_Stil.CalcHeight(new GUIContent(metin), r.width) + 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, metin, m_Stil);
    }
}
#endif
