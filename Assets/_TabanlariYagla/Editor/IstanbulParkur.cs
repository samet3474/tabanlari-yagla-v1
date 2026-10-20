// İstanbul parkuru — tramvay/rampa engelleri ve çok sıralı kombinasyon parçaları
// (batchmode: -executeMethod IstanbulParkur.Kur ; görseller grafik açıkken: -executeMethod IstanbulParkur.Gorseller).
//
// Kaynak modeller: Meshy ile bu proje için üretilmiş tramvay ve rampa (../_araclar/parkur/meshy/, görselden 3D,
// meshy-t2.5). Kopyalanır, CurvedUnlit malzemesiyle (oyunun diğer modelleri gibi) kullanılır, doku en fazla 1024.
//
// Engeller (Assets/_TabanlariYagla/Istanbul/Parkur/Engeller), hepsi ObstacleRoadworksCone kopyası (SimpleBarricade,
// ses, katman 9 tetikleyici; şerit başına bir örnek):
//   ObstacleTramvay          şeridi 9 m kapatır; üstünden atlanmaz, altından kayılmaz. Tavanı yürünür (katman 11).
//   ObstacleTramvayHareketli aynısı + HareketliTramvay (oyuncu 45 m kala 6 m/s ile ona doğru gelir).
//   ObstacleRampaliTramvay   7,5 m rampa + tramvay: rampanın şeridinden tavana koşulur; rampaya yandan girilemez.
//   ObstacleSeritZipla       tek şerit alçak bariyer (yalnız zıplanır)  — mevcut LowBarrier görseli, şerit genişliğinde
//   ObstacleSeritKay         tek şerit yüksek bariyer (yalnız kayılır)  — mevcut HighBarrier görseli
//   ObstacleSeritZiplaKay    tek şerit orta bariyer (zıplanır ya da kayılır) — HighBarrier görseli alçaltılmış
// Kombinasyonlar (…/Parkur/Kombinasyon/IstiklalK01…K13): Istiklal01 sokağının 3–4 kopyasıyla (tek sayılılar aynalı)
// 54/72 m uzun parça + TasarimliSatir (birden çok engel sırası ve altın deseni). Kaydı ve tema bölgelerini bu araç
// değiştirmez; ../_araclar/parkur/kombinasyon-kayit.json yazar (Addressables/tema elle, metin olarak eklenir).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

public static class IstanbulParkur
{
    const string k_Kok = "Assets/_TabanlariYagla/Istanbul/Parkur";
    const string k_Modeller = k_Kok + "/Modeller";
    const string k_EngelKlasoru = k_Kok + "/Engeller";
    const string k_KomboKlasoru = k_Kok + "/Kombinasyon";
    const string k_Kaynak = "../_araclar/parkur/meshy";
    const string k_Kayit = "../_araclar/parkur/kombinasyon-kayit.json";
    const string k_Gorsel = "../_araclar/parkur/kombinasyon-gorsel";
    const string k_Koni = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/Obstacles/ObstacleRoadworksCone.prefab";
    const string k_Engeller = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/Obstacles/";
    const string k_Istiklal = "Assets/_TabanlariYagla/Istanbul/Istiklal/Istiklal01.prefab";
    const string k_Altin = "Assets/UnityTechnologies/Endless Runner/Prefabs/Pickup.prefab";
    const string k_DayTheme = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/themeData.asset";

    public const float k_Tavan = 2.4f;        // tramvay tavanı (yürünen yüzey)
    public const float k_TramvayBoy = 9f;
    public const float k_SeritAraligi = 2.3f;  // İstanbul şerit aralığı (IstanbulTheme.seritAraligi ile aynı; Subway gibi geniş)
    const float k_TramvayEn = 2.0f;           // şeridi neredeyse doldurur
    public const float k_RampaBoy = 7.5f;
    const float k_SeritEn = 2.1f;
    const int k_EngelKatmani = 9, k_ZeminKatmani = 11;
    const int k_Sol = 1, k_Orta = 2, k_Sag = 4;

    static int s_Hata;
    static void Out(string s) { Debug.Log("PARKURKUR| " + s); }
    static void Fail(string s) { s_Hata++; Debug.LogError("PARKURKUR| HATA: " + s); }

    // ------------------------------------------------------------------------------------------------ tasarımlar
    class Kombo
    {
        public string ad, baslik, karar; public int kat = 3; public string cevre = "sokak";
        public (float m, string engel, int maske)[] satir;
        public TasarimliSatir.Altin[] altin;
    }
    static TasarimliSatir.Altin Duz(int s, float a, float b, float h = 0f) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Duz, serit = s, bitisSeridi = s, baslangic = a, bitis = b, yukseklik = h };
    static TasarimliSatir.Altin Yay(int s, float a, float b, float tepe) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Yay, serit = s, bitisSeridi = s, baslangic = a, bitis = b, yukseklik = tepe };
    static TasarimliSatir.Altin Capraz(int s0, int s1, float a, float b, float h = 0f) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Capraz, serit = s0, bitisSeridi = s1, baslangic = a, bitis = b, yukseklik = h };
    // Rampa üzerinde: rampa başlangıcı r0 (m), yüzey yüksekliği eğimle.
    static TasarimliSatir.Altin RampaAltin(int s, float r0, float a, float b) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Egim, serit = s, bitisSeridi = s, baslangic = a, bitis = b, yukseklik = (a - r0) * k_Tavan / k_RampaBoy, bitisYukseklik = (b - r0) * k_Tavan / k_RampaBoy };

    // Tavan üstünde dikey sikke sütunu: z konumunda, tavandan 0,5 m yukarıdan başlayıp zıplamayla ulaşılan yüksekliğe kadar.
    static TasarimliSatir.Altin Sutun(int s, float z) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Sutun, serit = s, bitisSeridi = s, baslangic = z, bitis = z, yukseklik = k_Tavan + 0.5f, bitisYukseklik = k_Tavan + 2.0f };

    // Engel adları: kendi prefablarımız ya da mevcut Default engelleri (Obstacle<ad>).
    static readonly Kombo[] k_Kombolar =
    {
        new Kombo { ad = "IstiklalK01", baslik = "K01 Tramvay hatti", karar = "ortada duran tramvay: yana gec; sonra soldaki cop kutusu",
            satir = new[] { (12f, "Tramvay", k_Orta), (36f, "Bin", k_Sol) },
            altin = new[] { Duz(1, 6f, 27f), Capraz(1, 0, 28.5f, 33f), Duz(0, 34.5f, 51f) } },
        new Kombo { ad = "IstiklalK02", baslik = "K02 Zikzak", karar = "sag -> sol (iki serit) -> sag/orta: uc ardisik serit degisimi",
            satir = new[] { (10f, "Bin", k_Sol | k_Orta), (29f, "WheelyBin", k_Orta | k_Sag), (47f, "RoadworksBarrier", k_Sol), (47f, "RoadworksCone", k_Orta) },
            altin = new[] { Duz(1, 0f, 7.5f), Capraz(1, -1, 13.5f, 24f), Duz(-1, 25.5f, 30f), Capraz(-1, 1, 32f, 42f), Duz(1, 43.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK03", baslik = "K03 Atla ve sap", karar = "tum serit alcak bariyer: zipla; inince sola gec; sonra soldaki tramvaydan kac",
            satir = new[] { (12f, "LowBarrier", k_Orta), (24f, "Bin", k_Orta), (24f, "WheelyBin", k_Sag), (38f, "Tramvay", k_Sol) },
            altin = new[] { Duz(0, 3f, 6f), Yay(0, 7.5f, 16.5f, 1.9f), Capraz(0, -1, 17f, 22f), Duz(-1, 23.5f, 32f), Capraz(-1, 0, 33.5f, 36.5f), Duz(0, 38f, 52.5f) } },
        new Kombo { ad = "IstiklalK04", baslik = "K04 Kay ve sap", karar = "tum serit yuksek bariyer: kay; sonra orta tramvay + sag koni: sola gec (ya da koniyi atla)",
            satir = new[] { (12f, "HighBarrier", k_Orta), (30f, "Tramvay", k_Orta), (30f, "RoadworksCone", k_Sag) },
            altin = new[] { Duz(0, 3f, 21f), Capraz(0, -1, 22.5f, 27f), Duz(-1, 28.5f, 48f) } },
        new Kombo { ad = "IstiklalK05", baslik = "K05 Zipla-kay koridoru", karar = "iki yanda tramvay; ortada once zipla, 14 m sonra kay; sonra orta cop kutusundan yana gec",
            satir = new[] { (8f, "Tramvay", k_Sol | k_Sag), (12f, "SeritZipla", k_Orta), (21f, "Tramvay", k_Sol | k_Sag), (26f, "SeritKay", k_Orta), (44f, "Bin", k_Orta) },
            altin = new[] { Duz(0, 0f, 7.5f), Yay(0, 8f, 16f, 1.6f), Duz(0, 18f, 30f), Capraz(0, -1, 33f, 39f), Duz(-1, 40.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK06", baslik = "K06 Uc secenek", karar = "ayni sirada sol zipla / orta kay / sag tramvay; sonra sag acik ya da ortadaki koniyi atla",
            satir = new[] { (14f, "SeritZipla", k_Sol), (14f, "SeritKay", k_Orta), (14f, "Tramvay", k_Sag), (34f, "Bin", k_Sol), (34f, "RoadworksCone", k_Orta) },
            altin = new[] { Duz(0, 4.5f, 19.5f), Yay(-1, 10f, 18f, 1.6f), Capraz(0, 1, 25.5f, 30f), Duz(1, 31.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK07", baslik = "K07 Kademeli kapanma", karar = "sol, sonra orta, sonra sag tramvay kapanir: soldaki tramvay bitince sola gec",
            satir = new[] { (6f, "Tramvay", k_Sol), (27f, "Tramvay", k_Orta), (40f, "Tramvay", k_Sag) },
            altin = new[] { Duz(0, 0f, 13.5f), Capraz(0, -1, 16.5f, 21f), Duz(-1, 22.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK08", baslik = "K08 Cift engel acik kacis", karar = "iki serit kapali -> sag; sonra sag kapali -> orta koniyi atla ya da sola; sonra iki serit kay",
            satir = new[] { (10f, "Bin", k_Sol), (10f, "WheelyBin", k_Orta), (26f, "RoadworksBarrier", k_Sag), (26f, "RoadworksCone", k_Orta), (42f, "SeritKay", k_Sol | k_Orta) },
            altin = new[] { Duz(1, 0f, 19.5f), Capraz(1, 0, 21f, 24f), Yay(0, 24f, 30f, 1.6f), Duz(0, 31.5f, 37.5f), Duz(0, 39f, 45f), Duz(0, 46.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK09", baslik = "K09 Rampa ve tavan", karar = "orta: rampadan iki vagonun tavanina cik (cok altin); ya da solda bariyeri atla",
            satir = new[] { (14f, "RampaliTramvay", k_Orta), (23.5f, "Tramvay", k_Orta), (20f, "SeritZipla", k_Sol), (18f, "Bin", k_Sag), (44f, "Bin", k_Sol | k_Sag) },
            altin = new[] { RampaAltin(0, 14f - k_RampaBoy, 8f, 12.5f), Duz(0, 15f, 31.5f, k_Tavan), Sutun(0, 27f), Yay(-1, 16f, 24f, 1.6f), Duz(0, 36f, 52.5f) } },
        new Kombo { ad = "IstiklalK10", cevre = "kopru", baslik = "K10 Yol calismasi", karar = "bariyer+koni, kutu+bariyer, koni+kutu: her sirada farkli acik serit ya da zipla",
            satir = new[] { (10f, "RoadworksBarrier", k_Sol), (10f, "RoadworksCone", k_Orta), (24f, "WheelyBin", k_Sag), (24f, "RoadworksBarrier", k_Orta), (38f, "RoadworksCone", k_Sol), (38f, "Bin", k_Orta) },
            altin = new[] { Yay(0, 7f, 13f, 1.6f), Capraz(0, -1, 15f, 21f), Duz(-1, 22.5f, 34f), Yay(-1, 35f, 41f, 1.6f), Duz(-1, 43.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK11", cevre = "kopru", baslik = "K11 Dar gecit", karar = "iki yanda tramvay: ortada kay; ikinci gecitte zipla ya da kay; sonra tum serit kay",
            satir = new[] { (8f, "Tramvay", k_Sol | k_Sag), (13f, "SeritKay", k_Orta), (26f, "Tramvay", k_Sol | k_Sag), (31f, "SeritZiplaKay", k_Orta), (46f, "HighBarrier", k_Orta) },
            altin = new[] { Duz(0, 3f, 24f), Yay(0, 27f, 35f, 1.9f), Duz(0, 37.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK12", cevre = "kopru", baslik = "K12 Uzun kombinasyon", kat = 4, karar = "sag'a gec; sagda rampadan tavana cik ya da ortada kay; tum serit bariyeri atla; sonra sola ya da koniyi atla",
            satir = new[] { (10f, "Bin", k_Sol | k_Orta), (22f, "Tramvay", k_Sol), (24f, "RampaliTramvay", k_Sag), (28f, "SeritKay", k_Orta), (52f, "LowBarrier", k_Orta), (64f, "WheelyBin", k_Orta), (64f, "RoadworksCone", k_Sag) },
            altin = new[] { Duz(1, 0f, 13.5f), RampaAltin(1, 24f - k_RampaBoy, 18f, 22.5f), Duz(1, 25.5f, 31.5f, k_Tavan), Sutun(1, 29f), Duz(0, 27f, 30f), Yay(1, 48f, 56f, 1.9f), Yay(1, 61f, 67f, 1.6f), Duz(1, 68f, 70.5f) } },
        new Kombo { ad = "IstiklalK13", cevre = "kopru", baslik = "K13 Karsidan tramvay", karar = "sagda duran tramvay, ortada kutu; soldan karsidan gelen tramvay: saga gec",
            satir = new[] { (14f, "Tramvay", k_Sag), (30f, "Bin", k_Orta), (40f, "TramvayHareketli", k_Sol) },
            altin = new[] { Duz(0, 3f, 25.5f), Capraz(0, 1, 25.5f, 28.5f), Duz(1, 30f, 52.5f) } },
        // Subway Surfers tarzı uzun / paralel katarlar (vagonlar 9,5 m arayla; tavanlar aynı yükseklikte, tavandan tavana geçilir)
        new Kombo { ad = "IstiklalK14", cevre = "kopru", baslik = "K14 Tren tarlasi", karar = "uc seritte kademeli katarlar: soldaki rampadan tavana cik, tavandan tavana gecerek sikkeleri topla",
            satir = new[] { (16f, "RampaliTramvay", k_Sol), (25.5f, "Tramvay", k_Sol), (35f, "Tramvay", k_Sol), (20f, "Tramvay", k_Orta), (29.5f, "Tramvay", k_Orta), (24f, "Tramvay", k_Sag), (33.5f, "Tramvay", k_Sag) },
            altin = new[] { RampaAltin(-1, 16f - k_RampaBoy, 10f, 14.5f), Duz(-1, 17f, 22f, k_Tavan), Capraz(-1, 0, 23.5f, 26.5f, k_Tavan), Duz(0, 28f, 32f, k_Tavan), Sutun(0, 30f), Capraz(0, 1, 33.5f, 36.5f, k_Tavan), Duz(1, 38f, 41f, k_Tavan) } },
        new Kombo { ad = "IstiklalK15", cevre = "tunel", baslik = "K15 Katar ve secim", karar = "ortada uc vagon: solda kay+zipla ya da sagdaki rampadan tavana cik",
            satir = new[] { (8f, "Tramvay", k_Orta), (17.5f, "Tramvay", k_Orta), (27f, "Tramvay", k_Orta), (14f, "SeritKay", k_Sol), (28f, "SeritZipla", k_Sol), (24f, "RampaliTramvay", k_Sag) },
            altin = new[] { Duz(-1, 3f, 18f), Yay(-1, 24f, 32f, 1.6f), RampaAltin(1, 24f - k_RampaBoy, 18f, 22.5f), Duz(1, 25f, 32f, k_Tavan), Duz(0, 40f, 52f) } },
        new Kombo { ad = "IstiklalK16", cevre = "tunel", baslik = "K16 Karsidan tren ve katar", karar = "sagda iki vagon, solda karsidan gelen tramvay, ortada kutu: katar bitince saga gec",
            satir = new[] { (10f, "Tramvay", k_Sag), (19.5f, "Tramvay", k_Sag), (36f, "Bin", k_Orta), (46f, "TramvayHareketli", k_Sol) },
            altin = new[] { Duz(0, 3f, 31.5f), Capraz(0, 1, 31.5f, 34.5f), Duz(1, 36f, 52.5f) } },
        new Kombo { ad = "IstiklalK17", cevre = "tunel", baslik = "K17 Catidan catiya", karar = "ortadaki rampadan tavana cik; ortadaki katar bitmeden sol ya da sag katarin tavanina gec",
            satir = new[] { (14f, "RampaliTramvay", k_Orta), (23.5f, "Tramvay", k_Orta), (26f, "Tramvay", k_Sol | k_Sag), (35.5f, "Tramvay", k_Sol | k_Sag) },
            altin = new[] { RampaAltin(0, 14f - k_RampaBoy, 8f, 12.5f), Duz(0, 15f, 24f, k_Tavan), Sutun(0, 20f), Capraz(0, -1, 27f, 30f, k_Tavan), Duz(-1, 31.5f, 43f, k_Tavan) } },
        // İstanbul'a özgü çevre bölümleri (aynı adalet ölçüsüyle denetlenen düzenler)
        new Kombo { ad = "IstiklalK18", baslik = "K18 Tunel", cevre = "tunel", karar = "karo duvarli tunel: tum serit bariyeri atla, kutulardan sola gec, soldaki tramvaydan kac",
            satir = new[] { (12f, "LowBarrier", k_Orta), (24f, "Bin", k_Orta), (24f, "WheelyBin", k_Sag), (38f, "Tramvay", k_Sol) },
            altin = new[] { Duz(0, 3f, 6f), Yay(0, 7.5f, 16.5f, 1.9f), Capraz(0, -1, 17f, 22f), Duz(-1, 23.5f, 32f), Capraz(-1, 0, 33.5f, 36.5f), Duz(0, 38f, 52.5f) } },
        new Kombo { ad = "IstiklalK19", baslik = "K19 Galata Koprusu", cevre = "kopru", karar = "Halic uzerinde: sagda duran tramvay, ortada kutu, soldan karsidan gelen tramvay: saga gec",
            satir = new[] { (14f, "Tramvay", k_Sag), (30f, "Bin", k_Orta), (40f, "TramvayHareketli", k_Sol) },
            altin = new[] { Duz(0, 3f, 25.5f), Capraz(0, 1, 25.5f, 28.5f), Duz(1, 30f, 52.5f) } },
    };

    // ------------------------------------------------------------------------------------------------ giriş
    public static void Kur()
    {
        s_Hata = 0;
        try
        {
            foreach (var k in new[] { k_Kok, k_Modeller, k_EngelKlasoru, k_KomboKlasoru })
                if (!AssetDatabase.IsValidFolder(k)) AssetDatabase.CreateFolder(Path.GetDirectoryName(k).Replace('\\', '/'), Path.GetFileName(k));
            var tramvayMat = ModelAl("tramvay/Tramvay.fbx", "tramvay/Tramvay_base_color.png", "Tramvay");
            var rampaMat = ModelAl("rampa/Rampa.fbx", "rampa/Rampa_base_color.png", "Rampa");
            if (File.Exists(Path.Combine(k_Kaynak, "altin/Altin.fbx"))) s_AltinMat = ModelAl("altin/Altin.fbx", "altin/Altin_base_color.png", "Altin");
            // Subway gibi parlak, net altın
            var ap = Shader.Find("TabanlariYagla/AltinParlak"); if (s_AltinMat != null && ap != null) { s_AltinMat.shader = ap; EditorUtility.SetDirty(s_AltinMat); }
            if (File.Exists(Path.Combine(k_Kaynak, "tunel/Tunel.fbx"))) s_TunelMat = ModelAl("tunel/Tunel.fbx", "tunel/Tunel_base_color.png", "Tunel");
            if (File.Exists(Path.Combine(k_Kaynak, "kopru/Kopru.fbx"))) s_KopruMat = ModelAl("kopru/Kopru.fbx", "kopru/Kopru_base_color.png", "Kopru");
            if (File.Exists(Path.Combine(k_Kaynak, "galata/Galata.fbx"))) s_GalataMat = ModelAl("galata/Galata.fbx", "galata/Galata_base_color.png", "Galata");
            if (File.Exists(Path.Combine(k_Kaynak, "yenicami/YeniCami.fbx"))) s_CamiMat = ModelAl("yenicami/YeniCami.fbx", "yenicami/YeniCami_base_color.png", "YeniCami");
            if (File.Exists(Path.Combine(k_Kaynak, "katener/Katener.fbx"))) s_KatenerMat = ModelAl("katener/Katener.fbx", "katener/Katener_base_color.png", "Katener");
            if (File.Exists(Path.Combine(k_Kaynak, "ayakkabi/Ayakkabi.fbx"))) AyakkabiKur();
            KenneyAl();
            AltinYap();
            KatenerEkle();
            var engeller = new Dictionary<string, string>();
            engeller["Tramvay"] = TramvayPrefab("ObstacleTramvay", tramvayMat, false, false);
            engeller["TramvayHareketli"] = TramvayPrefab("ObstacleTramvayHareketli", tramvayMat, true, false);
            engeller["RampaliTramvay"] = TramvayPrefab("ObstacleRampaliTramvay", tramvayMat, false, true, rampaMat);
            // Meshy bariyerleri (Subway tarzı, kırmızı-beyaz çizgili): tetikleyiciler aynı, adalet ölçüsü değişmez
            s_BariyerModel = new Dictionary<string, (string, float, float, bool)>
            {
                ["ObstacleSeritZipla"] = ("BariyerIkiKat", 0.85f, 0f, false),      // zıplanır: iki katlı alçak çit
                ["ObstacleSeritKay"] = ("BariyerYuksek", 2.45f, 0f, false),        // kayılır: tabela 0,96..2,1 m
                ["ObstacleSeritZiplaKay"] = ("BariyerAlcak", 0.93f, 0.4f, true),   // ikisi de: ayaklar üstünde 0,65..1,15 m
            };
            foreach (var (k, fbx) in new[] { ("BariyerAlcak", "bariyer-alcak/BariyerAlcak"), ("BariyerYuksek", "bariyer-yuksek/BariyerYuksek"), ("BariyerIkiKat", "bariyer-ikikat/BariyerIkiKat") })
                if (File.Exists(Path.Combine(k_Kaynak, fbx + ".fbx"))) s_BariyerMat[k] = ModelAl(fbx + ".fbx", fbx + "_base_color.png", k);
            engeller["SeritZipla"] = SeritBariyeri("ObstacleSeritZipla", "ObstacleLowBarrier", 1f, 0f, 0.6f);
            engeller["SeritKay"] = SeritBariyeri("ObstacleSeritKay", "ObstacleHighBarrier", 1f, 1.01f, 1.51f);
            engeller["SeritZiplaKay"] = SeritBariyeri("ObstacleSeritZiplaKay", "ObstacleHighBarrier", 0.72f, 0.75f, 1.05f);
            AssetDatabase.SaveAssets();
            var kayit = new StringBuilder("{\n  \"engeller\": [\n");
            kayit.Append(string.Join(",\n", engeller.Select(e => $"    {{\"ad\": \"{Path.GetFileNameWithoutExtension(e.Value)}\", \"guid\": \"{AssetDatabase.AssetPathToGUID(e.Value)}\"}}")));
            kayit.Append("\n  ],\n  \"parcalar\": [\n");
            var parca = new List<string>();
            foreach (var k in k_Kombolar)
            {
                string yol = KomboPrefab(k, engeller);
                if (yol != null) parca.Add($"    {{\"ad\": \"{k.ad}\", \"baslik\": \"{k.baslik}\", \"guid\": \"{AssetDatabase.AssetPathToGUID(yol)}\", \"karar\": \"{k.karar}\"}}");
            }
            kayit.Append(string.Join(",\n", parca)).Append("\n  ]\n}\n");
            AssetDatabase.SaveAssets();
            File.WriteAllText(k_Kayit, kayit.ToString(), new UTF8Encoding(false));
            Out("kayit: " + Path.GetFullPath(k_Kayit));
        }
        catch (System.Exception e) { Fail(e.ToString()); }
        Out($"bitti hata={s_Hata}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Hata == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------------------------------------ modeller
    static Material ModelAl(string fbx, string png, string ad)
    {
        string hedefFbx = $"{k_Modeller}/{ad}.fbx", hedefPng = $"{k_Modeller}/{ad}_renk.png", hedefMat = $"{k_Modeller}/{ad}.mat";
        if (!File.Exists(hedefFbx)) { File.Copy(Path.Combine(k_Kaynak, fbx), hedefFbx); AssetDatabase.ImportAsset(hedefFbx); }
        if (!File.Exists(hedefPng)) { File.Copy(Path.Combine(k_Kaynak, png), hedefPng); AssetDatabase.ImportAsset(hedefPng); }
        var mi = (ModelImporter)AssetImporter.GetAtPath(hedefFbx);
        mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.isReadable = false;
        mi.SaveAndReimport();
        var ti = (TextureImporter)AssetImporter.GetAtPath(hedefPng);
        ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.SaveAndReimport();
        var shader = Shader.Find("Unlit/CurvedUnlit");
        if (shader == null) { Fail("Unlit/CurvedUnlit yok"); return null; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(hedefMat);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, hedefMat); }
        mat.shader = shader; mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(hedefPng);
        EditorUtility.SetDirty(mat);
        Out($"model {ad}: {hedefFbx} doku 1024");
        return mat;
    }

    static List<Vector3> Noktalar(GameObject go)
    {
        var l = new List<Vector3>();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh; if (m == null) continue;
            foreach (var v in m.vertices) l.Add(mf.transform.TransformPoint(v));
        }
        return l;
    }
    static Bounds Kutu(List<Vector3> n) { var b = new Bounds(n[0], Vector3.zero); foreach (var p in n) b.Encapsulate(p); return b; }

    // Görseli yerleştirir: uzun eksen z, taban y=0, x ortada, z [z0, z0+boy]; genişlik en, yükseklik: ölçülen yüzey = yuzey.
    // yuksekUcIleri: rampa — yüksek ucu +z'ye çevir. Dönüş: görsel kök.
    static GameObject Yerlestir(string fbx, Material mat, Transform ebeveyn, string ad, float en, float boy, float z0, float yuzey, bool rampa)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var kap = new GameObject(ad).transform; kap.SetParent(ebeveyn, false);
        var vis = (GameObject)Object.Instantiate(model, kap);
        vis.name = ad + "Model";
        foreach (var t in vis.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        var b = Kutu(Noktalar(vis.gameObject));
        Out($"{ad} ham olcu {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}");
        // yükseklik en kısa eksen değilse (yatık model) ve uzunluk x ise döndür
        if (b.size.y > b.size.z && b.size.y > b.size.x) vis.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * vis.transform.localRotation;
        b = Kutu(Noktalar(vis.gameObject));
        if (b.size.x > b.size.z) vis.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * vis.transform.localRotation;
        var n = Noktalar(vis.gameObject); b = Kutu(n);
        if (rampa)
        {
            float uc = b.size.z * 0.2f;
            float on = n.Where(p => p.z < b.min.z + uc).Average(p => p.y), arka = n.Where(p => p.z > b.max.z - uc).Average(p => p.y);
            if (on > arka) vis.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * vis.transform.localRotation;
        }
        n = Noktalar(vis.gameObject); b = Kutu(n);
        // ölçülen yüzey: tramvay — orta bölgenin tavanı; rampa — en yüksek nokta
        float olculen;
        if (rampa) olculen = b.max.y - b.min.y;
        else
        {
            // tavan: gövdenin ortasında (boyun 3 noktası) yukarıdan atılan ışının değdiği yer, gerçek üçgenlerle
            // (düşük poligonlu tavanın köşe noktaları uçlardadır; uçtaki tabela hesaba girmez)
            var gecici = vis.GetComponentsInChildren<MeshFilter>(true).Select(mf => mf.gameObject.AddComponent<MeshCollider>()).ToList();
            Physics.SyncTransforms();
            var fizik = vis.scene.GetPhysicsScene(); // prefab içerikleri ayrı (önizleme) sahnede
            var tavanlar = new List<float>();
            foreach (float o in new[] { 0.35f, 0.5f, 0.65f })
                if (fizik.Raycast(new Vector3(b.center.x, b.max.y + 1f, Mathf.Lerp(b.min.z, b.max.z, o)), Vector3.down, out RaycastHit hit, b.size.y + 2f))
                    tavanlar.Add(hit.point.y);
            foreach (var c in gecici) Object.DestroyImmediate(c);
            tavanlar.Sort();
            olculen = (tavanlar.Count > 0 ? tavanlar[tavanlar.Count / 2] : b.max.y) - b.min.y;
            Out($"{ad} olculen tavan (ham) {olculen:F3} / en yuksek {b.size.y:F3} ({tavanlar.Count} isin)");
        }
        // ölçek dönmeyen kapsayıcıda (dünya eksenlerinde); model kapsayıcının içinde dönmüş durur
        kap.localScale = new Vector3(en / b.size.x, yuzey / olculen, boy / b.size.z);
        b = Kutu(Noktalar(vis.gameObject));
        kap.localPosition += new Vector3(-b.center.x, -b.min.y, z0 - b.min.z);
        b = Kutu(Noktalar(vis.gameObject));
        Out($"{ad} gorsel {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} z={b.min.z:F2}..{b.max.z:F2} (yuzey {yuzey}, en yuksek {b.max.y:F2})");
        return kap.gameObject;
    }

    static GameObject KoniKopyasi(string ad, out string yol)
    {
        yol = $"{k_EngelKlasoru}/{ad}.prefab";
        if (!File.Exists(yol) && !AssetDatabase.CopyAsset(k_Koni, yol)) { Fail("koni kopyalanamadi " + ad); return null; }
        var root = PrefabUtility.LoadPrefabContents(yol);
        root.name = ad;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        for (int i = root.transform.childCount - 1; i >= 0; --i) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        var boxes = root.GetComponents<BoxCollider>();
        for (int i = 1; i < boxes.Length; ++i) Object.DestroyImmediate(boxes[i]);
        var anim = root.GetComponent<Animation>(); if (anim != null) Object.DestroyImmediate(anim);
        foreach (var h in root.GetComponents<HareketliTramvay>()) Object.DestroyImmediate(h);
        return root;
    }

    static BoxCollider Tetik(GameObject go, Vector3 merkez, Vector3 boyut, BoxCollider mevcut = null)
    {
        var b = mevcut != null ? mevcut : go.AddComponent<BoxCollider>();
        b.isTrigger = true; b.center = merkez; b.size = boyut;
        return b;
    }

    static GameObject Zemin(Transform ebeveyn, string ad, Vector3 konum, Quaternion don, Vector3 boyut)
    {
        var z = new GameObject(ad); z.layer = k_ZeminKatmani;
        z.transform.SetParent(ebeveyn, false); z.transform.localPosition = konum; z.transform.localRotation = don;
        var b = z.AddComponent<BoxCollider>(); b.isTrigger = true; b.size = boyut; b.center = new Vector3(0f, -boyut.y * 0.5f, 0f);
        return z;
    }

    // Tramvay: burun z=0, gövde z 0..9. Engel tetikleyicisi tavanın 0,25 m altında biter (tavanda koşan çarpmaz).
    static string TramvayPrefab(string ad, Material mat, bool hareketli, bool rampali, Material rampaMat = null)
    {
        var root = KoniKopyasi(ad, out string yol); if (root == null) return null;
        try
        {
            root.layer = k_EngelKatmani;
            Yerlestir($"{k_Modeller}/Tramvay.fbx", mat, root.transform, "Tramvay", k_TramvayEn, k_TramvayBoy, 0f, k_Tavan, false);
            float govde = k_Tavan - 0.25f;
            Tetik(root, new Vector3(0f, govde * 0.5f, k_TramvayBoy * 0.5f), new Vector3(k_TramvayEn - 0.1f, govde, k_TramvayBoy), root.GetComponent<BoxCollider>());
            Zemin(root.transform, "TavanZemin", new Vector3(0f, k_Tavan, k_TramvayBoy * 0.5f), Quaternion.identity, new Vector3(k_TramvayEn, 0.2f, k_TramvayBoy));
            if (rampali)
            {
                Yerlestir($"{k_Modeller}/Rampa.fbx", rampaMat, root.transform, "Rampa", k_TramvayEn, k_RampaBoy, -k_RampaBoy, k_Tavan, true);
                float aci = Mathf.Atan2(k_Tavan, k_RampaBoy) * Mathf.Rad2Deg, hip = Mathf.Sqrt(k_Tavan * k_Tavan + k_RampaBoy * k_RampaBoy);
                Zemin(root.transform, "RampaZemin", new Vector3(0f, k_Tavan * 0.5f, -k_RampaBoy * 0.5f), Quaternion.Euler(-aci, 0f, 0f), new Vector3(k_TramvayEn, 0.1f, hip));
                // Rampaya yandan girilemez: yüzeyin tırmanma adımını (0,6 m) aştığı kısımda basamaklı tetikleyiciler,
                // her biri başladığı yerdeki yüzeyin 0,3 m altında biter (rampada koşan çarpmaz).
                float egim = k_Tavan / k_RampaBoy, bas = -k_RampaBoy + 0.6f / egim;
                const int adim = 4; float uz = -bas / adim;
                for (int i = 0; i < adim; ++i)
                {
                    float z0 = bas + i * uz, ust = (z0 + k_RampaBoy) * egim - 0.3f;
                    Tetik(root, new Vector3(0f, ust * 0.5f, z0 + uz * 0.5f), new Vector3(k_TramvayEn - 0.1f, ust, uz));
                }
            }
            if (hareketli)
            {
                var h = root.AddComponent<HareketliTramvay>(); h.hiz = 8f; h.baslamaMesafesi = 45f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, yol);
            Out($"engel {ad}: tetikleyici={root.GetComponents<BoxCollider>().Length} hareketli={hareketli} rampali={rampali}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return yol;
    }

    // Tek şerit bariyeri: mevcut tüm-şerit bariyerin görseli şerit genişliğine (1,4 m) daraltılır; tetikleyici y0..y1.
    static Dictionary<string, (string, float, float, bool)> s_BariyerModel = new Dictionary<string, (string, float, float, bool)>();
    static readonly Dictionary<string, Material> s_BariyerMat = new Dictionary<string, Material>();

    static string SeritBariyeri(string ad, string kaynak, float yOlcek, float y0, float y1)
    {
        var root = KoniKopyasi(ad, out string yol); if (root == null) return null;
        try
        {
            if (s_BariyerModel.TryGetValue(ad, out var bm) && s_BariyerMat.TryGetValue(bm.Item1, out var bmat) && ModelVar(bm.Item1))
            {
                // Meshy modeli: uzun kenarı x (şerit eni k_SeritEn), boyu bm.Item2, tabanı bm.Item3 yükseklikte
                var hb = HamKutu(bm.Item1, out _);
                var don = hb.size.z > hb.size.x ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                var g = new GameObject("Gorsel").transform; g.SetParent(root.transform, false);
                var k = ModelKoy(bm.Item1, bmat, g, don, 1, bm.Item2, new Vector3(0f, bm.Item3, 0f));
                var kb = KutuKap(k, k.GetChild(0).gameObject);
                k.localScale = new Vector3(k.localScale.x * k_SeritEn / kb.size.x, k.localScale.y, k.localScale.z);
                kb = KutuKap(k, k.GetChild(0).gameObject); k.localPosition += new Vector3(-kb.center.x, bm.Item3 - kb.min.y, -kb.center.z);
                if (bm.Item4)
                {
                    // yükseltilmiş bariyerin ayakları: şerit kenarında gri direkler
                    var yp = new Yapici();
                    yp.Cift(k_SeritEn * 0.5f - 0.14f, k_SeritEn * 0.5f - 0.02f, 0f, bm.Item3 + 0.08f, -0.08f, 0.08f, P_TasKoyu);
                    Cizici(g, "Ayak", yp.Bitir(k_Kok + "/Sokak", ad + "_Ayak"), PaletMalzemesi());
                }
                Tetik(root, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(k_SeritEn - 0.1f, y1 - y0, 0.3f), root.GetComponent<BoxCollider>());
                root.layer = k_EngelKatmani;
                var mgb = Kutu(Noktalar(g.gameObject));
                Out($"engel {ad}: Meshy {bm.Item1} x={mgb.min.x:F2}..{mgb.max.x:F2} y={mgb.min.y:F2}..{mgb.max.y:F2}, tetikleyici y={y0}..{y1}");
                PrefabUtility.SaveAsPrefabAsset(root, yol);
                return yol;
            }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(k_Engeller + kaynak + ".prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var b = Kutu(Noktalar(inst));
            var kap = new GameObject("Gorsel").transform; kap.SetParent(root.transform, false);
            for (int i = inst.transform.childCount - 1; i >= 0; --i) inst.transform.GetChild(i).SetParent(kap, true);
            Object.DestroyImmediate(inst);
            foreach (var t in kap.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 0;
                foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
                foreach (var a in t.GetComponents<Animation>()) Object.DestroyImmediate(a);
            }
            kap.localScale = new Vector3(k_SeritEn / b.size.x, yOlcek, 1f);
            kap.localPosition = new Vector3(-b.center.x * kap.localScale.x, 0f, 0f);
            Tetik(root, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(k_SeritEn - 0.1f, y1 - y0, 0.3f), root.GetComponent<BoxCollider>());
            root.layer = k_EngelKatmani;
            var gb = Kutu(Noktalar(kap.gameObject));
            Out($"engel {ad}: gorsel x={gb.min.x:F2}..{gb.max.x:F2} y={gb.min.y:F2}..{gb.max.y:F2}, tetikleyici y={y0}..{y1}");
            PrefabUtility.SaveAsPrefabAsset(root, yol);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return yol;
    }

    // ------------------------------------------------------------------------------------------------ kombinasyonlar
    static string EngelYolu(string ad, Dictionary<string, string> kendi) => kendi.TryGetValue(ad, out var y) ? y : k_Engeller + "Obstacle" + ad + ".prefab";

    static string KomboPrefab(Kombo k, Dictionary<string, string> kendi)
    {
        var temel = AssetDatabase.LoadAssetAtPath<GameObject>(k_Istiklal);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(temel);
        try
        {
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = k.ad;
            var seg = inst.GetComponent<TrackSegment>();
            Transform giris = seg.pathParent.GetChild(0), cikis = seg.pathParent.GetChild(seg.pathParent.childCount - 1);
            Vector3 adim = cikis.localPosition - giris.localPosition;
            var kaynaklar = new List<Transform>();
            for (int i = 0; i < inst.transform.childCount; ++i) if (inst.transform.GetChild(i) != seg.pathParent) kaynaklar.Add(inst.transform.GetChild(i));
            var kokMf = inst.GetComponent<MeshFilter>(); var kokMr = inst.GetComponent<MeshRenderer>();
            for (int kat = 1; kat < k.kat; ++kat)
            {
                var kopya = new GameObject($"Kat{kat}").transform; kopya.SetParent(inst.transform, false);
                kopya.localPosition = adim * kat;
                if (kat % 2 == 1) kopya.localScale = new Vector3(-1f, 1f, 1f); // görsel çeşitlilik
                foreach (var s in kaynaklar) { var c = Object.Instantiate(s.gameObject, kopya); c.name = s.name; c.transform.localPosition = s.localPosition; c.transform.localRotation = s.localRotation; c.transform.localScale = s.localScale; }
                if (kokMf != null)
                {
                    var zg = new GameObject("Yol"); zg.transform.SetParent(kopya, false);
                    zg.AddComponent<MeshFilter>().sharedMesh = kokMf.sharedMesh;
                    var mr = zg.AddComponent<MeshRenderer>(); mr.sharedMaterials = kokMr.sharedMaterials;
                    mr.shadowCastingMode = kokMr.shadowCastingMode; mr.receiveShadows = kokMr.receiveShadows;
                    mr.lightProbeUsage = kokMr.lightProbeUsage; mr.reflectionProbeUsage = kokMr.reflectionProbeUsage;
                }
            }
            cikis.localPosition = giris.localPosition + adim * k.kat;
            CevreUygula(inst, k.cevre, giris.localPosition.z, cikis.localPosition.z);
            seg.possibleObstacles = new AssetReference[0];
            seg.obstaclePositions = new float[0];
            float boy = adim.magnitude * k.kat;
            var tas = inst.AddComponent<TasarimliSatir>();
            tas.satirlar = k.satir.Select(s =>
            {
                string guid = AssetDatabase.AssetPathToGUID(EngelYolu(s.engel, kendi));
                if (string.IsNullOrEmpty(guid)) Fail($"{k.ad}: engel yok {s.engel}");
                if (s.m < 0 || s.m > boy) Fail($"{k.ad}: satir parcanin disinda {s.m}");
                return new TasarimliSatir.Satir { konum = s.m, engel = new AssetReference(guid), seritler = s.maske };
            }).ToArray();
            tas.altinlar = k.altin;
            foreach (var a in k.altin) if (a.bitis > boy) Fail($"{k.ad}: altin parcanin disinda {a.bitis}");
            string yol = $"{k_KomboKlasoru}/{k.ad}.prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, yol, out bool ok);
            if (!ok) { Fail("kaydedilemedi " + yol); return null; }
            Out($"parca {k.ad} ({k.baslik}) boy={boy:F2} m satir={k.satir.Length} altin_deseni={k.altin.Length} guid={AssetDatabase.AssetPathToGUID(yol)}");
            return yol;
        }
        finally { Object.DestroyImmediate(inst); }
    }

    // Sokak genişletme + raylar (-executeMethod IstanbulParkur.SokakGenislet): Istiklal01'in ağlarının kopyaları,
    // kök uzayında x ekseninde çarpıtılır: |x| <= R yol bölgesi (R+D)/R oranında esner, dışı (binalar) D kadar dışarı
    // kayar; yazılar (sprite) de kayar. Sonra her şeride ray yatağı + traversler + raylar eklenir (Subway gibi üç hat).
    // Orijinal Day/Night ağları değişmez (kopyalar …/Parkur/Sokak altında). Tekrar çalıştırılırsa (Raylar varsa) atlanır.
    const float k_SokakR = 3.3f, k_SokakD = 1.3f;
    static float Cozgu(float x) => Mathf.Abs(x) <= k_SokakR ? x * (k_SokakR + k_SokakD) / k_SokakR : x + Mathf.Sign(x) * k_SokakD;

    public static void SokakGenislet()
    {
        s_Hata = 0;
        string klasor = k_Kok + "/Sokak";
        if (!AssetDatabase.IsValidFolder(klasor)) AssetDatabase.CreateFolder(k_Kok, "Sokak");
        var root = PrefabUtility.LoadPrefabContents(k_Istiklal);
        try
        {
            if (root.transform.Find("Raylar") != null) Out("sokak zaten genis (Raylar var), atlandi");
            else GenisletVeRayEkle(root);
        }
        catch (System.Exception e) { Fail(e.ToString()); }
        finally { PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets(); }
        Out($"bitti hata={s_Hata}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Hata == 0 ? 0 : 1);
    }

    static void GenisletVeRayEkle(GameObject root)
    {
        string klasor = k_Kok + "/Sokak";
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh; if (src == null) continue;
                var m = Object.Instantiate(src); m.name = src.name + "_genis";
                var v = m.vertices;
                Matrix4x4 kok = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix, geri = kok.inverse;
                for (int i = 0; i < v.Length; ++i) { var p = kok.MultiplyPoint3x4(v[i]); p.x = Cozgu(p.x); v[i] = geri.MultiplyPoint3x4(p); }
                m.vertices = v; m.RecalculateBounds();
                string yol = $"{klasor}/{m.name}.asset";
                AssetDatabase.DeleteAsset(yol); AssetDatabase.CreateAsset(m, yol);
                mf.sharedMesh = m;
                Out($"ag genisletildi {src.name} -> {yol} x={m.bounds.min.x:F2}..{m.bounds.max.x:F2}");
            }
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var p = root.transform.InverseTransformPoint(sr.transform.position); p.x = Cozgu(p.x);
                sr.transform.position = root.transform.TransformPoint(p);
            }
            var seg = root.GetComponent<TrackSegment>();
            float z0 = seg.pathParent.GetChild(0).localPosition.z, z1 = seg.pathParent.GetChild(seg.pathParent.childCount - 1).localPosition.z;
            var raylar = new GameObject("Raylar"); raylar.transform.SetParent(root.transform, false);
            raylar.AddComponent<MeshFilter>().sharedMesh = RayAgi(klasor, z0, z1);
            var mr = raylar.AddComponent<MeshRenderer>(); mr.sharedMaterial = RayMalzemesi(klasor);
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            PrefabUtility.SaveAsPrefabAsset(root, k_Istiklal);
            Out($"Istiklal01 genisletildi (R={k_SokakR} D={k_SokakD}), raylar z={z0:F2}..{z1:F2}, serit={k_SeritAraligi}");
        }
    }

    // Renk atlası (2x2): ray çeliği, travers, ray yatağı (balast), boş.
    static Material RayMalzemesi(string klasor)
    {
        string png = $"{klasor}/RayRenk.png", matYol = $"{klasor}/Ray.mat";
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.SetPixels(new[] { new Color(0.62f, 0.62f, 0.66f), new Color(0.36f, 0.25f, 0.18f), new Color(0.50f, 0.40f, 0.33f), new Color(0.5f, 0.5f, 0.5f) });
        tex.Apply(); File.WriteAllBytes(png, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(png);
        var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        if (mat == null) { mat = new Material(Shader.Find("Unlit/CurvedUnlit")); AssetDatabase.CreateAsset(mat, matYol); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png); EditorUtility.SetDirty(mat);
        return mat;
    }

    // Her şerit: balast yatağı (1,9 m), 0,75 m'de bir travers, iki ray (1,1 m açıklık).
    static Mesh RayAgi(string klasor, float z0, float z1, string ad = "Raylar")
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        void Kutu(Vector3 min, Vector3 max, Vector2 renk)
        {
            if (max.z - min.z > 3f) { int n = Mathf.CeilToInt((max.z - min.z) / 3f); for (int i = 0; i < n; ++i) Kutu(new Vector3(min.x, min.y, Mathf.Lerp(min.z, max.z, i / (float)n)), new Vector3(max.x, max.y, Mathf.Lerp(min.z, max.z, (i + 1) / (float)n)), renk); return; }
            Vector3[] k = { new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                            new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z) };
            int[][] yuz = { new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 } }; // üst, sol, sağ, ön, arka
            foreach (var y in yuz) { int b = v.Count; foreach (int i in y) { v.Add(k[i]); uv.Add(renk); } t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 }); }
        }
        Vector2 celik = new Vector2(0.25f, 0.25f), travers = new Vector2(0.75f, 0.25f), balast = new Vector2(0.25f, 0.75f);
        for (int l = -1; l <= 1; ++l)
        {
            float x = l * k_SeritAraligi;
            Kutu(new Vector3(x - 0.95f, 0.005f, z0), new Vector3(x + 0.95f, 0.02f, z1), balast);
            for (float z = z0 + 0.3f; z < z1 - 0.1f; z += 0.75f) Kutu(new Vector3(x - 0.8f, 0.02f, z - 0.11f), new Vector3(x + 0.8f, 0.05f, z + 0.11f), travers);
            foreach (float r in new[] { -0.55f, 0.55f }) Kutu(new Vector3(x + r - 0.04f, 0.05f, z0), new Vector3(x + r + 0.04f, 0.11f, z1), celik);
        }
        var m = new Mesh { name = "Raylar" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        m.name = ad; string yol = $"{klasor}/{ad}.asset"; AssetDatabase.DeleteAsset(yol); AssetDatabase.CreateAsset(m, yol);
        return m;
    }

    // ------------------------------------------------------------------------------------------------ çevre ve altın
    // Renk paleti (4x4, nokta süzme): her kutu bir renk hücresinin ortasını örnekler.
    static readonly Color[] k_Palet =
    {
        new Color(0.35f, 0.36f, 0.40f), new Color(0.15f, 0.15f, 0.17f), new Color(0.93f, 0.88f, 0.76f), new Color(0.13f, 0.36f, 0.66f),
        new Color(0.30f, 0.28f, 0.30f), new Color(1.00f, 0.97f, 0.82f), new Color(0.62f, 0.57f, 0.50f), new Color(0.45f, 0.41f, 0.36f),
        new Color(0.20f, 0.38f, 0.33f), new Color(0.16f, 0.45f, 0.62f), new Color(0.30f, 0.62f, 0.78f), new Color(0.27f, 0.27f, 0.29f),
        new Color(0.60f, 0.60f, 0.62f), new Color(0.10f, 0.10f, 0.12f), new Color(0.75f, 0.15f, 0.15f), new Color(1f, 1f, 1f),
    };
    const int P_Direk = 0, P_Tel = 1, P_Krem = 2, P_Iznik = 3, P_Tavan = 4, P_Isik = 5, P_Tas = 6, P_TasKoyu = 7,
              P_Yesil = 8, P_Su = 9, P_SuAcik = 10, P_Asfalt = 11, P_Bordur = 12, P_Siyah = 13;

    class Yapici
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
        static readonly int[][] k_Yuz = { new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 } };
        public void Kutu(Vector3 a, Vector3 b, int renk)
        {
            Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
            // kavisli dünya köşe noktalarını büker: uzun kutular z'de 3 m'lik parçalara bölünür
            if (max.z - min.z > 3f) { int n = Mathf.CeilToInt((max.z - min.z) / 3f); for (int i = 0; i < n; ++i) Kutu(new Vector3(min.x, min.y, Mathf.Lerp(min.z, max.z, i / (float)n)), new Vector3(max.x, max.y, Mathf.Lerp(min.z, max.z, (i + 1) / (float)n)), renk); return; }
            Vector3[] k = { new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                            new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z) };
            var c = new Vector2(((renk % 4) + 0.5f) / 4f, ((renk / 4) + 0.5f) / 4f);
            foreach (var y in k_Yuz) { int s = v.Count; foreach (int i in y) { v.Add(k[i]); uv.Add(c); } t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
        }
        public void Ucgen(Vector3 a, Vector3 b, Vector3 c, int renk)
        {
            var r = new Vector2(((renk % 4) + 0.5f) / 4f, ((renk / 4) + 0.5f) / 4f); int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); uv.Add(r); uv.Add(r); uv.Add(r); t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 1 });
        }
        // dikey çokgen prizma (silindir) ve koni: uzak silüetler için
        public void Silindir(Vector3 alt, float r, float h, int n, int renk)
        {
            var c = new Vector2(((renk % 4) + 0.5f) / 4f, ((renk / 4) + 0.5f) / 4f);
            for (int i = 0; i < n; ++i)
            {
                float a0 = i * 2f * Mathf.PI / n, a1 = (i + 1) * 2f * Mathf.PI / n;
                Vector3 p0 = alt + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), p1 = alt + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                int s = v.Count; v.AddRange(new[] { p0, p0 + Vector3.up * h, p1 + Vector3.up * h, p1 }); uv.AddRange(new[] { c, c, c, c });
                t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3, s, s + 2, s + 1, s, s + 3, s + 2 });
                Ucgen(alt + Vector3.up * h, p0 + Vector3.up * h, p1 + Vector3.up * h, renk);
            }
        }
        public void Koni(Vector3 alt, float r, float h, int n, int renk)
        {
            for (int i = 0; i < n; ++i)
            {
                float a0 = i * 2f * Mathf.PI / n, a1 = (i + 1) * 2f * Mathf.PI / n;
                Ucgen(alt + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), alt + Vector3.up * h, alt + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r), renk);
            }
        }
        // yarım küreye yakın kubbe: daralan halkalar
        public void Kubbe(Vector3 alt, float r, int renk)
        {
            const int k = 5; float y0 = 0;
            for (int i = 0; i < k; ++i)
            {
                float a = (i + 1) * Mathf.PI / 2f / k, ri = r * Mathf.Cos(i * Mathf.PI / 2f / k), yi = r * Mathf.Sin(a) - y0;
                Silindir(alt + Vector3.up * y0, ri, yi, 16, renk); y0 += yi;
            }
        }
        // x'e göre simetrik çift kutu (sol ve sağ)
        public void Cift(float x0, float x1, float y0, float y1, float z0, float z1, int renk)
        { Kutu(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), renk); Kutu(new Vector3(-x0, y0, z0), new Vector3(-x1, y1, z1), renk); }
        public Mesh Bitir(string klasor, string ad)
        {
            var m = new Mesh { name = ad, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            string yol = $"{klasor}/{ad}.asset"; AssetDatabase.DeleteAsset(yol); AssetDatabase.CreateAsset(m, yol);
            return m;
        }
    }

    // İstiklal tramvayına özgü hafif katener: direkler arasında gergi teli (10,3 m), her şeridin üstünde kontak teli (9,9 m)
    // ve aralarında askılar. Ağır kiriş yok; kameranın (koşuda ~4,8 m, tavanda ~6 m) üstünde kalır, görüşü kapatmaz.
    static void IstiklalKatener(Yapici y, float z0, float z1, float direkX)
    {
        for (float z = z0 + 4.5f; z < z1; z += 9f)
        {
            y.Kutu(new Vector3(-direkX, 10.28f, z - 0.02f), new Vector3(direkX, 10.32f, z + 0.02f), P_Tel);
            for (int l = -1; l <= 1; ++l) y.Kutu(new Vector3(l * k_SeritAraligi - 0.015f, 9.9f, z - 0.015f), new Vector3(l * k_SeritAraligi + 0.015f, 10.3f, z + 0.015f), P_Tel);
        }
        for (int l = -1; l <= 1; ++l) y.Kutu(new Vector3(l * k_SeritAraligi - 0.022f, 9.88f, z0), new Vector3(l * k_SeritAraligi + 0.022f, 9.93f, z1), P_Tel);
    }

    static Material PaletMalzemesi()
    {
        string klasor = k_Kok + "/Sokak", png = $"{klasor}/IstanbulPalet.png", matYol = $"{klasor}/IstanbulPalet.mat";
        if (!AssetDatabase.IsValidFolder(klasor)) AssetDatabase.CreateFolder(k_Kok, "Sokak");
        var tex = new Texture2D(4, 4, TextureFormat.RGB24, false); tex.SetPixels(k_Palet); tex.Apply();
        File.WriteAllBytes(png, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(png);
        var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        if (mat == null) { mat = new Material(Shader.Find("Unlit/CurvedUnlit")); AssetDatabase.CreateAsset(mat, matYol); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png); EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Cizici(Transform ebeveyn, string ad, Mesh m, Material mat)
    {
        var go = new GameObject(ad); go.transform.SetParent(ebeveyn, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go;
    }

    // Katener: sokağın iki yanında direkler (9 m arayla), üstte kiriş, her şeridin üstünde kontak teli ve taşıyıcı tel.
    // Tramvay tavanında zıplayan karakterin tepesi ~4,9 m; teller 5,2 m ve üstünde (çarpışma yok, yalnız görsel).
    static Mesh KatenerAgi(float z0, float z1, bool model = false, float telY = 5.2f)
    {
        var y = new Yapici();
        for (float z = z0 + 1f; z < z1 && !model; z += 9f)
        {
            y.Cift(4.1f, 4.3f, 0f, 6.0f, z - 0.1f, z + 0.1f, P_Direk);
            y.Kutu(new Vector3(-4.3f, 5.65f, z - 0.08f), new Vector3(4.3f, 5.8f, z + 0.08f), P_Direk);
            for (int l = -1; l <= 1; ++l) y.Kutu(new Vector3(l * k_SeritAraligi - 0.02f, 5.25f, z - 0.02f), new Vector3(l * k_SeritAraligi + 0.02f, 5.65f, z + 0.02f), P_Tel);
        }
        for (int l = -1; l <= 1; ++l)
        {
            float x = l * k_SeritAraligi;
            y.Kutu(new Vector3(x - 0.025f, telY, z0), new Vector3(x + 0.025f, telY + 0.04f, z1), P_Tel);
            y.Kutu(new Vector3(x - 0.02f, telY + 0.35f, z0), new Vector3(x + 0.02f, telY + 0.38f, z1), P_Tel);
        }
        return y.Bitir(k_Kok + "/Sokak", "Katener");
    }

    // Tünel kabuğu (karo duvarlar, İznik mavisi şerit, basamaklı tavan, şerit başına tavan ışıkları, taş portaller).
    static Mesh TunelAgi(float z0, float z1, string ad)
    {
        var y = new Yapici();
        y.Cift(4.5f, 4.9f, 0f, 5.6f, z0, z1, P_Krem);
        y.Cift(4.45f, 4.5f, 1.0f, 1.5f, z0 + 1.2f, z1 - 1.2f, P_Iznik);
        y.Cift(4.45f, 4.5f, 2.6f, 2.9f, z0 + 1.2f, z1 - 1.2f, P_Iznik);
        y.Cift(3.4f, 4.9f, 5.6f, 6.0f, z0, z1, P_Krem);
        y.Cift(2.6f, 4.9f, 6.0f, 6.6f, z0, z1, P_Krem);
        y.Kutu(new Vector3(-4.9f, 7.2f, z0), new Vector3(4.9f, 7.5f, z1), P_Tavan);
        y.Cift(1.4f, 4.9f, 6.6f, 7.2f, z0, z1, P_Tavan);
        for (float z = z0 + 3f; z < z1 - 2f; z += 4f)
            for (int l = -1; l <= 1; ++l) y.Kutu(new Vector3(l * k_SeritAraligi - 0.15f, 7.1f, z), new Vector3(l * k_SeritAraligi + 0.15f, 7.2f, z + 1.5f), P_Isik);
        foreach (float pz in new[] { z0, z1 - 1.2f }) // Meshy portalı kapalı bir kütle (açıklığı yok), yolu kapatıyor: iki uçta da taş portal
        {
            y.Cift(4.9f, 6.2f, 0f, 8.6f, pz, pz + 1.2f, P_Tas);
            y.Kutu(new Vector3(-6.2f, 7.3f, pz), new Vector3(6.2f, 8.6f, pz + 1.2f), P_Tas);
            y.Kutu(new Vector3(-6.2f, 8.6f, pz - 0.1f), new Vector3(6.2f, 8.9f, pz + 1.3f), P_TasKoyu);
        }
        return y.Bitir(k_Kok + "/Sokak", ad);
    }

    // Galata Köprüsü: güverte, kaldırım, yeşil korkuluk, fener direkleri, alttan kirişler, iki yanda Haliç suyu.
    static Mesh KopruAgi(float z0, float z1, string ad)
    {
        var y = new Yapici();
        y.Kutu(new Vector3(-4.9f, -0.3f, z0), new Vector3(4.9f, 0.0f, z1), P_Asfalt);
        y.Cift(3.5f, 4.7f, 0.0f, 0.12f, z0, z1, P_Bordur);
        bool model = ModelVar("Kopru");
        if (!model) y.Cift(4.7f, 4.95f, 0.0f, 1.0f, z0, z1, P_Yesil);
        y.Cift(4.95f, 5.1f, -1.4f, 0.0f, z0, z1, P_Yesil);
        for (float z = z0 + 4.5f; z < z1 && !model; z += 9f)
        {
            y.Cift(4.75f, 4.9f, 1.0f, 4.4f, z - 0.08f, z + 0.08f, P_Siyah);
            y.Cift(4.3f, 4.95f, 4.2f, 4.5f, z - 0.18f, z + 0.18f, P_Siyah);
            y.Cift(4.35f, 4.6f, 3.95f, 4.2f, z - 0.15f, z + 0.15f, P_Isik);
        }
        // köprünün iki yanında ince direkler ve İstiklal'deki gibi hafif katener
        for (float z = z0 + 4.5f; z < z1; z += 9f) y.Cift(3.4f, 3.56f, 0f, 10.6f, z - 0.08f, z + 0.08f, P_Yesil);
        IstiklalKatener(y, z0, z1, 3.46f);
        // Haliç silüeti: çift parçalarda Galata Kulesi (sağ), tek parçalarda Yeni Cami (sol) — uzakta
        float zm = z1 + 15f; // ileride ama dünya eğriliğiyle batmayacak kadar yakın, dar portre görüşe girer
        if (SiluetModeli) { } // Meshy modelleri CevreUygula'da konur
        else if (GalataSirasi(ad))
        {
            var k = new Vector3(20f, -4f, zm);
            y.Silindir(k, 4.2f, 30f, 16, P_Tas); y.Silindir(k + Vector3.up * 30f, 4.9f, 1.2f, 16, P_TasKoyu);
            y.Silindir(k + Vector3.up * 31.2f, 4.0f, 4.5f, 16, P_Krem); y.Koni(k + Vector3.up * 35.7f, 4.6f, 10f, 16, P_TasKoyu);
        }
        else
        {
            var c = new Vector3(-26f, -4f, zm);
            y.Kutu(c + new Vector3(-12f, 0, -9f), c + new Vector3(12f, 14f, 9f), P_Tas);
            y.Kubbe(c + Vector3.up * 14f, 8.5f, P_Direk);
            foreach (float dz in new[] { -6f, 6f }) foreach (float dx in new[] { -9f, 9f }) y.Kubbe(c + new Vector3(dx, 14f, dz), 3f, P_Direk);
            foreach (float dx in new[] { -13.5f, 13.5f }) { var m = c + new Vector3(dx, 0, -9.5f); y.Silindir(m, 0.9f, 34f, 10, P_Krem); y.Silindir(m + Vector3.up * 26f, 1.3f, 0.6f, 10, P_Tas); y.Koni(m + Vector3.up * 34f, 1.0f, 6f, 10, P_Direk); }
        }
        y.Cift(5.1f, 70f, -4.0f, -3.9f, z0, z1, P_Su);
        for (float z = z0 + 2f; z < z1; z += 6f) y.Cift(8f, 40f, -3.89f, -3.88f, z, z + 0.4f, P_SuAcik);
        return y.Bitir(k_Kok + "/Sokak", ad);
    }

    // Türk altını (lale motifli) — İstanbul temasının toplanan sikkesi; Day/Night balık kılçığı aynı kalır.
    const string k_AltinKlasor = k_Kok + "/Altin";
    const string k_IstanbulTema = "Assets/_TabanlariYagla/Istanbul/IstanbulTheme.asset";
    public static void AltinYap()
    {
        if (!AssetDatabase.IsValidFolder(k_AltinKlasor)) AssetDatabase.CreateFolder(k_Kok, "Altin");
        // doku atlası 256x128: solda yüz (lale, nokta halkası, kabartma kenar), sağda tırtıllı yan yüz
        const int W = 256, H = 128;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
        Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
        Color acik = Hex(255, 226, 122), koyu = Hex(227, 161, 28), cizgi = Hex(176, 116, 12), kenar = Hex(255, 211, 77), motif = Hex(190, 128, 14);
        bool Elips(float x, float yy, float cx, float cy, float rx, float ry) { float dx = (x - cx) / rx, dy = (yy - cy) / ry; return dx * dx + dy * dy <= 1f; }
        for (int py = 0; py < H; ++py)
            for (int px = 0; px < W; ++px)
            {
                Color c;
                if (px < 128)
                {
                    float x = (px + 0.5f) / 64f - 1f, yy = (py + 0.5f) / 64f - 1f, d = Mathf.Sqrt(x * x + yy * yy);
                    c = Color.Lerp(acik, koyu, Mathf.Clamp01(d * 0.9f + yy * 0.15f));
                    if (d > 0.80f && d <= 0.86f) c = cizgi;
                    else if (d > 0.86f) c = Color.Lerp(kenar, koyu, (d - 0.86f) / 0.14f);
                    else
                    {
                        bool lale = Elips(x, yy, 0f, 0.18f, 0.16f, 0.30f) || Elips(x, yy, -0.17f, 0.10f, 0.13f, 0.26f) || Elips(x, yy, 0.17f, 0.10f, 0.13f, 0.26f)
                                    || (Elips(x, yy, 0f, 0.0f, 0.24f, 0.18f) && yy < 0.06f)
                                    || (Mathf.Abs(x + yy * 0.08f) < 0.028f && yy < -0.05f && yy > -0.58f)
                                    || Elips(x, yy, -0.16f, -0.36f, 0.15f, 0.055f) || Elips(x, yy, 0.16f, -0.30f, 0.14f, 0.05f);
                        if (lale) c = motif;
                        for (int k = 0; k < 12 && !lale; ++k)
                        {
                            float a = k * Mathf.PI / 6f;
                            if (Elips(x, yy, Mathf.Cos(a) * 0.68f, Mathf.Sin(a) * 0.68f, 0.04f, 0.04f)) { c = cizgi; break; }
                        }
                    }
                }
                else c = ((px / 4) % 2 == 0) ? Hex(217, 152, 26) : Hex(242, 198, 64);
                tex.SetPixel(px, py, c);
            }
        tex.Apply();
        string png = $"{k_AltinKlasor}/AltinDoku.png"; File.WriteAllBytes(png, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(png);
        var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 256; ti.SaveAndReimport();
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{k_AltinKlasor}/Altin.mat");
        if (mat == null) { mat = new Material(Shader.Find("Unlit/CurvedUnlit")); AssetDatabase.CreateAsset(mat, $"{k_AltinKlasor}/Altin.mat"); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png); EditorUtility.SetDirty(mat);

        // ağ: z ekseninde yüzü olan disk (yarıçap 0,28, kalınlık 0,09); her üçgen iki yönlü (arkadan da görünür)
        const int N = 28; const float R = 0.28f, h = 0.045f;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        void Ucgen(int a, int b, int c) { tr.AddRange(new[] { a, b, c, a, c, b }); }
        foreach (float z in new[] { h, -h })
        {
            int m0 = v.Count; v.Add(new Vector3(0, 0, z)); uv.Add(new Vector2(0.25f, 0.5f));
            for (int i = 0; i <= N; ++i) { float a = i * 2f * Mathf.PI / N; float cx = Mathf.Cos(a), cy = Mathf.Sin(a); v.Add(new Vector3(cx * R, cy * R, z)); uv.Add(new Vector2(0.25f + (z > 0 ? -cx : cx) * 0.25f, 0.5f + cy * 0.5f)); }
            for (int i = 0; i < N; ++i) Ucgen(m0, m0 + 1 + i, m0 + 2 + i);
        }
        int r0 = v.Count;
        for (int i = 0; i <= N; ++i)
        {
            float a = i * 2f * Mathf.PI / N; var p = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0);
            v.Add(p + Vector3.forward * h); uv.Add(new Vector2(0.5f + 0.5f * i / N, 0.25f));
            v.Add(p - Vector3.forward * h); uv.Add(new Vector2(0.5f + 0.5f * i / N, 0.75f));
        }
        for (int i = 0; i < N; ++i) { int a = r0 + i * 2; Ucgen(a, a + 2, a + 3); Ucgen(a, a + 3, a + 1); }
        var mesh = new Mesh { name = "TurkAltini" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string meshYol = $"{k_AltinKlasor}/TurkAltini.asset"; AssetDatabase.DeleteAsset(meshYol); AssetDatabase.CreateAsset(mesh, meshYol);

        // prefab: Pickup kopyası (Coin, çarpıştırıcı, katman 8, gölge) — balık kılçığı yerine dönen altın
        string yol = $"{k_AltinKlasor}/IstanbulAltin.prefab";
        if (!File.Exists(yol) && !AssetDatabase.CopyAsset(k_Altin, yol)) { Fail("Pickup kopyalanamadi"); return; }
        var root = PrefabUtility.LoadPrefabContents(yol);
        try
        {
            root.name = "IstanbulAltin";
            var eski = root.transform.Find("Fishbone"); var eskiAltin = root.transform.Find("Altin");
            Vector3 konum = eski != null ? eski.localPosition : (eskiAltin != null ? eskiAltin.localPosition : new Vector3(0, 0.46f, 0));
            if (eski != null) Object.DestroyImmediate(eski.gameObject);
            if (eskiAltin != null) Object.DestroyImmediate(eskiAltin.gameObject);
            GameObject g;
            if (ModelVar("Altin") && s_AltinMat != null)
            {
                g = new GameObject("Altin"); g.transform.SetParent(root.transform, false); g.transform.localPosition = konum;
                var hb = HamKutu("Altin", out _);
                int ince = hb.size.x < hb.size.y && hb.size.x < hb.size.z ? 0 : hb.size.y < hb.size.z ? 1 : 2;
                var don = ince == 0 ? Quaternion.Euler(0, 90, 0) : ince == 1 ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
                ModelKoy("Altin", s_AltinMat, g.transform, don, 1, 0.56f, new Vector3(0, -0.28f, 0));
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            }
            else { g = Cizici(root.transform, "Altin", mesh, mat); g.transform.localPosition = konum; }
            g.layer = root.layer; g.AddComponent<AltinDon>();
            PrefabUtility.SaveAsPrefabAsset(root, yol);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var tema = AssetDatabase.LoadAssetAtPath<ThemeData>(k_IstanbulTema);
        tema.collectiblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(yol);
        EditorUtility.SetDirty(tema); AssetDatabase.SaveAssets();
        Out($"altin: {yol} (lale motifli, donen) -> IstanbulTheme.collectiblePrefab");
    }

    // Katener'i Istiklal01'e ekler (yoksa). SokakGenislet'ten sonra çağrılır; tekrar çalıştırılabilir.
    static void KatenerEkle()
    {
        var root = PrefabUtility.LoadPrefabContents(k_Istiklal);
        try
        {
            foreach (var ad in new[] { "Katener", "Raylar", "Kaldirim", "SokakSusu", "TurkBayragi" })
            { var eski = root.transform.Find(ad); if (eski != null) Object.DestroyImmediate(eski.gameObject); }
            var seg = root.GetComponent<TrackSegment>();
            float z0 = seg.pathParent.GetChild(0).localPosition.z, z1 = seg.pathParent.GetChild(seg.pathParent.childCount - 1).localPosition.z;
            // İstiklal Caddesi: Arnavut kaldırımı, gömülü raylar, ince katener, bayraklar (Meshy direk geçidi kullanılmaz)
            TasliSokak(root, z0, z1);
            Out("istiklal tabani: tasli + ince katener");
            PrefabUtility.SaveAsPrefabAsset(root, k_Istiklal);
            Out("katener eklendi");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Kombinasyonun çevresini değiştirir: tünel (binalar, tabelalar, direkler, katener kalkar; tünel kabuğu) ya da
    // köprü (binalar kalkar, yol yerine güverte, korkuluk, fener, Haliç suyu; uzak siluet ve katener kalır).
    static void CevreUygula(GameObject inst, string cevre, float z0, float z1)
    {
        if (cevre == "sokak") return;
        if (cevre == "tasli") return; // taban sokak zaten taşlı
        var sil = new List<GameObject>();
        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            bool susle = n == "Kaldirim" || n == "SokakSusu" || n == "TurkBayragi" || n == "Katener" || n == "Raylar";
            bool tunelSil = susle || n == "IstiklalSokak" || n == "FarBackBuilding02" || n.StartsWith("Graffiti") || n == "StreetLight";
            bool kopruSil = susle || n == "IstiklalSokak" || n.StartsWith("Graffiti") || n == "StreetLight" || n == "Road02" || n == "Yol";
            if (cevre == "tunel" ? tunelSil : kopruSil) sil.Add(t.gameObject);
        }
        foreach (var g in sil) if (g != null) Object.DestroyImmediate(g);
        var mat = PaletMalzemesi();
        // köprü ve tünelde Subway gibi balastlı üç ray hattı
        Cizici(inst.transform, "Raylar", RayAgi(k_Kok + "/Sokak", z0, z1, inst.name + "_Raylar"), RayMalzemesi(k_Kok + "/Sokak"));
        if (cevre == "tunel")
        {
            Cizici(inst.transform, "Tunel", TunelAgi(z0, z1, inst.name + "_Tunel"), mat);
            if (false && ModelVar("Tunel") && s_TunelMat != null) // bkz. TunelAgi: model yolu kapatıyor
            {
                // Meshy portalının cephesi +z'de ve modelin içinde ~28 m tünel var: 180° çevir, kemer açıklığı üç şeridi
                // (tramvay dahil ±3,3 m) rahat geçirsin diye toplam genişlik 16,7 m; zemini yolun biraz altına göm.
                var k = ModelKoy("Tunel", s_TunelMat, inst.transform, Quaternion.Euler(0, 180, 0), 0, 16.7f, new Vector3(0, -0.4f, z0));
                // model ~28 m derin bir dağ kütlesi: içi görüşü kapatmasın diye yalnızca girişte 1,5 m'lik cephe olarak bırakılır
                var b0 = KutuKap(k, k.GetChild(0).gameObject); k.localScale = Vector3.Scale(k.localScale, new Vector3(1f, 1f, 1.5f / b0.size.z));
                var b = KutuKap(k, k.GetChild(0).gameObject); k.localPosition += new Vector3(0, 0, z0 - 0.5f - b.min.z);
            }
        }
        else
        {
            var kokR = inst.GetComponent<MeshRenderer>(); if (kokR != null) kokR.enabled = false; // kök yol ağı
            Cizici(inst.transform, "Kopru", KopruAgi(z0, z1, inst.name + "_Kopru"), mat);
            if (SiluetModeli)
            {
                // Haliç silüeti (Meshy): ileride, dünya eğriliğiyle batmayacak kadar yakın, dar portre görüşe girer
                float zm = z1 + 15f;
                // modelin gerçek taban izi ölçülüp yoldan en az 9 m uzağa itilir (cami modelinin avlusu yola taşıp önü kapatıyordu)
                Transform siluet;
                if (GalataSirasi(inst.name + "_Kopru")) siluet = ModelKoy("Galata", s_GalataMat, inst.transform, Quaternion.identity, 1, 46f, new Vector3(13f, -4f, zm));
                else siluet = ModelKoy("YeniCami", s_CamiMat, inst.transform, Quaternion.Euler(0, 90, 0), 1, 34f, new Vector3(-30f, -4f, zm - 5f));
                var iz = KutuKap(siluet, siluet.GetChild(0).gameObject);
                float it = siluet.localPosition.x > 0 ? Mathf.Max(0f, 9f - iz.min.x) : -Mathf.Max(0f, iz.max.x + 9f);
                siluet.localPosition += new Vector3(it, 0f, 0f);
                Out($"silüet {siluet.name}: x {iz.min.x:F1}..{iz.max.x:F1} -> itme {it:F1}");
            }
            if (ModelVar("Kopru") && s_KopruMat != null)
            {
                var hb = HamKutu("Kopru", out var hn);
                var don = hb.size.x > hb.size.z ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                // korkuluk yüksek tarafta: döndürülmüş noktalarda x'e göre ortalama yükseklik
                float art = 0, eks = 0; int na = 0, ne = 0; var c = don * hb.center;
                foreach (var p in hn) { var q = don * p; if (q.x > c.x) { art += q.y; na++; } else { eks += q.y; ne++; } }
                if (ne > 0 && na > 0 && eks / ne > art / na) don = Quaternion.Euler(0, 180, 0) * don; // korkuluk +x'te olsun
                for (float z = z0 + 4.5f; z < z1; z += 9f)
                    foreach (int yon in new[] { 1, -1 })
                    {
                        var k = ModelKoy("Kopru", s_KopruMat, inst.transform, yon > 0 ? don : Quaternion.Euler(0, 180, 0) * don, 2, 9f, new Vector3(0, 0, z));
                        var b = KutuKap(k, k.GetChild(0).gameObject);
                        k.localPosition += new Vector3(yon > 0 ? 3.5f - b.min.x : -3.5f - b.max.x, 0, 0);
                    }
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ Meshy çevre modelleri
    // Meshy modelleri (…/Parkur/Modeller): Altin, Tunel (portal), Kopru (kenar modülü), Katener (direk geçidi).
    // Yoksa kodla çizilen hâller kullanılır.
    static Material s_TunelMat, s_KopruMat, s_KatenerMat, s_AltinMat, s_GalataMat, s_CamiMat;
    static bool SiluetModeli => ModelVar("Galata") && ModelVar("YeniCami") && s_GalataMat != null && s_CamiMat != null;
    // köprü parçası başına bir silüet (çift/tek ad toplamına göre): Galata Kulesi sağda ya da Yeni Cami solda
    static bool GalataSirasi(string ad) { int h = 0; foreach (char ch in ad) h += ch; return h % 2 == 0; }
    // ------------------------------------------------------------------------------------------------ Süper ayakkabı
    // Meshy "Ayakkabi" modelinden iki prefab: yolda dönen, haleli bonus (SuperAyakkabi, güç katmanı 10) ve ayaklara
    // takılan iri ayakkabı. İstanbul temasının superAyakkabi alanına bağlanır.
    static void AyakkabiKur()
    {
        var mat = ModelAl("ayakkabi/Ayakkabi.fbx", "ayakkabi/Ayakkabi_base_color.png", "Ayakkabi");
        if (mat == null) return;
        string klasor = k_Kok + "/Bonus";
        if (!AssetDatabase.IsValidFolder(klasor)) AssetDatabase.CreateFolder(k_Kok, "Bonus");

        // yön: uzun yatay eksen z olsun; burun (alçak taraf) +z'ye baksın
        var hb = HamKutu("Ayakkabi", out var hn);
        var don = hb.size.x > hb.size.z ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
        float on = 0, arka = 0; int no = 0, na = 0; var c = don * hb.center;
        foreach (var p in hn) { var q = don * p; if (q.z > c.z) { on += q.y; no++; } else { arka += q.y; na++; } }
        if (no > 0 && na > 0 && on / no > arka / na) don = Quaternion.Euler(0, 180, 0) * don;

        // simge: Meshy görseli (beyaz zemin) → sprite
        string ikonYol = $"{klasor}/AyakkabiIkon.png";
        if (!File.Exists(ikonYol)) { File.Copy(Path.Combine(k_Kaynak, "ayakkabi-gorsel.png"), ikonYol); AssetDatabase.ImportAsset(ikonYol); }
        var ti = (TextureImporter)AssetImporter.GetAtPath(ikonYol);
        ti.textureType = TextureImporterType.Sprite; ti.maxTextureSize = 256; ti.SaveAndReimport();
        var ikon = AssetDatabase.LoadAssetAtPath<Sprite>(ikonYol);

        // hale: yumuşak sarı ışık dairesi
        string haleYol = $"{klasor}/AyakkabiHale.png";
        var tx = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; ++y) for (int x = 0; x < 128; ++x)
        {
            float d = new Vector2(x - 63.5f, y - 63.5f).magnitude / 63.5f;
            float a = Mathf.Clamp01(1f - d); a = a * a * 0.85f;
            tx.SetPixel(x, y, new Color(1f, 0.85f, 0.25f, a));
        }
        File.WriteAllBytes(haleYol, tx.EncodeToPNG()); Object.DestroyImmediate(tx); AssetDatabase.ImportAsset(haleYol);
        var hti = (TextureImporter)AssetImporter.GetAtPath(haleYol);
        hti.textureType = TextureImporterType.Sprite; hti.alphaIsTransparency = true; hti.textureCompression = TextureImporterCompression.Uncompressed; hti.SaveAndReimport();
        var hale = AssetDatabase.LoadAssetAtPath<Sprite>(haleYol);

        // ayağa takılan: 0,40 m boy (ayağı saran iri spor ayakkabı), tabanı y=0 (ayak kemiğinin biraz altında)
        var giy = new GameObject("IstanbulAyakkabiGiyilen");
        // burun +z (giyilirken SuperAyakkabi.KemigeBagla karakterin ilerisine çevirir; yan görünüşte doğrulandı)
        ModelKoy("Ayakkabi", mat, giy.transform, don, 2, 0.40f, Vector3.zero);
        var giyPrefab = PrefabUtility.SaveAsPrefabAsset(giy, $"{klasor}/IstanbulAyakkabiGiyilen.prefab");
        Object.DestroyImmediate(giy);

        // yoldaki bonus: 1,1 m boy, yerden 0,5 m yukarıda döner; arkasında hale
        var bon = new GameObject("IstanbulSuperAyakkabi");
        bon.layer = 10;
        var col = bon.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.8f; col.center = new Vector3(0, 0.9f, 0);
        var gorsel = new GameObject("Gorsel").transform; gorsel.SetParent(bon.transform, false);
        ModelKoy("Ayakkabi", mat, gorsel, Quaternion.Euler(0, 90, 0) * don, 0, 1.1f, new Vector3(0, 0.5f, 0)); // yana dönük: boy x ekseninde
        var haleGo = new GameObject("Hale"); haleGo.transform.SetParent(bon.transform, false); haleGo.transform.localPosition = new Vector3(0, 0.95f, 0.3f);
        var sr = haleGo.AddComponent<SpriteRenderer>(); sr.sprite = hale; sr.sortingOrder = -1;
        var sa = bon.AddComponent<SuperAyakkabi>();
        sa.giyilenModel = giyPrefab; sa.ikon = ikon; sa.gorsel = gorsel; sa.hale = haleGo.transform;
        var bonPrefab = PrefabUtility.SaveAsPrefabAsset(bon, $"{klasor}/IstanbulSuperAyakkabi.prefab");
        Object.DestroyImmediate(bon);

        AyakKemikleriniAc();

        var tema = AssetDatabase.LoadAssetAtPath<ThemeData>("Assets/_TabanlariYagla/Istanbul/IstanbulTheme.asset");
        if (tema != null) { tema.superAyakkabi = bonPrefab; EditorUtility.SetDirty(tema); AssetDatabase.SaveAssets(); }
        Out($"super ayakkabi: bonus + giyilen prefab, tema={(tema != null)}");
    }

    // Karakter modelleri "Optimize Game Objects" ile gelir: kemikler gizli. Ayakkabı ayağı izlesin diye yalnız iki ayak
    // kemiği dışarı açılır (extraExposedTransformPaths) ve karakter prefablarında Animator'ın altına aynı adla boş nesne konur
    // (Animator bunları kendisi sürer). Görünüm ve animasyon değişmez.
    static void AyakKemikleriniAc()
    {
        const string kok = "Assets/UnityTechnologies/Endless Runner";
        foreach (var (fbx, prefab) in new[] { ($"{kok}/Art/Models/Cat.fbx", $"{kok}/Prefabs/Characters/Cat/character.prefab"), ($"{kok}/Art/Models/Racoon.fbx", $"{kok}/Prefabs/Characters/Raccoon/character.prefab") })
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (mi == null) { Out("model yok " + fbx); continue; }
            var ayaklar = mi.transformPaths.Where(p => p.EndsWith("/LeftFoot") || p.EndsWith("/RightFoot")).ToArray();
            var acik = mi.extraExposedTransformPaths.Union(ayaklar).ToArray();
            if (acik.Length != mi.extraExposedTransformPaths.Length) { mi.extraExposedTransformPaths = acik; mi.SaveAndReimport(); }
            var ic = PrefabUtility.LoadPrefabContents(prefab);
            var an = ic.GetComponentInChildren<Animator>(true);
            bool degisti = false;
            foreach (var yol in ayaklar)
            {
                string ad = yol.Substring(yol.LastIndexOf('/') + 1);
                if (an.transform.Find(ad) == null) { new GameObject(ad).transform.SetParent(an.transform, false); degisti = true; }
            }
            if (degisti) PrefabUtility.SaveAsPrefabAsset(ic, prefab);
            PrefabUtility.UnloadPrefabContents(ic);
            Out($"ayak kemikleri {Path.GetFileName(fbx)}: {string.Join(", ", ayaklar)}");
        }
    }

    // Süper ayakkabı önizlemesi (-executeMethod IstanbulParkur.AyakkabiGorsel, grafik açık): yoldaki bonus ve kedide giyilmiş hâli
    public static void AyakkabiGorsel()
    {
        Directory.CreateDirectory(k_Gorsel);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Shader.SetGlobalFloat("_CurveStrength", 0f); RenderSettings.fog = false;
        RenderSettings.ambientLight = Color.white;
        var bon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_Kok + "/Bonus/IstanbulSuperAyakkabi.prefab"));
        bon.transform.position = new Vector3(-1.2f, 0, 0);
        bon.GetComponent<SuperAyakkabi>().hale.LookAt(bon.GetComponent<SuperAyakkabi>().hale.position + Vector3.forward);
        var kedi = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityTechnologies/Endless Runner/Prefabs/Characters/Cat/character.prefab"));
        kedi.transform.position = new Vector3(1.0f, 0, 0); kedi.transform.rotation = Quaternion.Euler(0, 200, 0);
        var giy = AssetDatabase.LoadAssetAtPath<GameObject>(k_Kok + "/Bonus/IstanbulAyakkabiGiyilen.prefab");
        var an = kedi.GetComponentInChildren<Animator>();
        an.Rebind(); an.Update(0.2f);
        foreach (var ad in new[] { "LeftFoot", "RightFoot" })
        {
            var ayak = kedi.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(ad));
            if (ayak == null) { Out("ayak yok " + ad); continue; }
            var g = (GameObject)Object.Instantiate(giy, kedi.transform, false);
            g.transform.position = ayak.position + Vector3.down * 0.04f; g.transform.rotation = kedi.transform.rotation;
            Out("ayak " + ad + " " + ayak.position);
        }
        Cek("ayakkabi-onizleme", 900, 700, new Vector3(0, 1.0f, -3.6f), Quaternion.Euler(8, 0, 0), 45f, 0f, new Color(0.55f, 0.75f, 0.95f));
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------------------------------------ Kenney (CC0)
    // Kenney Train Kit / City Kit Roads / Commercial: düz renk paletli (colormap) temiz modeller, CC0 (kenney.nl).
    // Palet dokusu sıkıştırmasız ve mipmap'siz: renkler keskin kalır, uzakta bulanıklaşmaz.
    const string k_Kenney = "../_araclar/kenney";
    static Material s_KenneyMat;
    static readonly string[] k_KenneyParca = { "roads/Models/FBX format/traffic-light.fbx", "roads/Models/FBX format/light-curved-double.fbx", "roads/Models/FBX format/construction-light.fbx" };

    static void KenneyAl()
    {
        if (!Directory.Exists(k_Kenney)) return;
        string klasor = k_Modeller + "/Kenney";
        if (!AssetDatabase.IsValidFolder(klasor)) AssetDatabase.CreateFolder(k_Modeller, "Kenney");
        string palet = klasor + "/colormap.png";
        if (!File.Exists(palet)) { File.Copy(Path.Combine(k_Kenney, "roads/Models/FBX format/Textures/colormap.png"), palet); AssetDatabase.ImportAsset(palet); }
        var ti = (TextureImporter)AssetImporter.GetAtPath(palet);
        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; ti.filterMode = FilterMode.Bilinear; ti.SaveAndReimport();
        foreach (var p in k_KenneyParca)
        {
            string hedef = $"{k_Modeller}/Kenney_{Path.GetFileNameWithoutExtension(p)}.fbx";
            if (!File.Exists(hedef)) { File.Copy(Path.Combine(k_Kenney, p), hedef); AssetDatabase.ImportAsset(hedef); }
            var mi = (ModelImporter)AssetImporter.GetAtPath(hedef);
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
        }
        string matYol = klasor + "/Kenney.mat";
        s_KenneyMat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        if (s_KenneyMat == null) { s_KenneyMat = new Material(Shader.Find("Unlit/CurvedUnlit")); AssetDatabase.CreateAsset(s_KenneyMat, matYol); }
        s_KenneyMat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(palet); EditorUtility.SetDirty(s_KenneyMat);
        Out($"kenney: {k_KenneyParca.Length} parca, palet sikistirmasiz");
    }

    // Kenney modelini yolun iki yanına aralıkla dizer (boy: metre, x: merkezden yan uzaklık)
    static void KenneyDiz(Transform ebeveyn, string ad, float z0, float z1, float ara, float x, float boy)
    {
        if (s_KenneyMat == null || !ModelVar("Kenney_" + ad)) return;
        for (float z = z0; z < z1; z += ara)
            foreach (int yon in new[] { -1, 1 })
            {
                var k = ModelKoy("Kenney_" + ad, s_KenneyMat, ebeveyn, Quaternion.Euler(0, 90, 0), 1, boy, new Vector3(yon * x, 0f, z)); // lambalar koşucuya bakar
                k.name = "Kenney_" + ad;
            }
    }

    static bool ModelVar(string ad) => File.Exists($"{k_Modeller}/{ad}.fbx");

    // Modeli bir kapsayıcıya koyar: önce döndür, sonra eşit ölçekle (eksendeki boyu = boy), tabanı (x,z orta, y alt) hedefe.
    static Transform ModelKoy(string ad, Material mat, Transform ebeveyn, Quaternion donus, int eksen, float boy, Vector3 taban)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_Modeller}/{ad}.fbx");
        var kap = new GameObject(ad).transform; kap.SetParent(ebeveyn, false);
        var vis = (GameObject)Object.Instantiate(model, kap); vis.name = ad + "Model";
        foreach (var t in vis.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        vis.transform.localRotation = donus * vis.transform.localRotation;
        var b = KutuKap(kap, vis); float s = boy / b.size[eksen];
        kap.localScale = Vector3.one * s;
        b = KutuKap(kap, vis);
        kap.localPosition += taban - new Vector3(b.center.x, b.min.y, b.center.z);
        return kap;
    }
    // görselin, kapsayıcının ebeveyni uzayındaki sınırları
    static Bounds KutuKap(Transform kap, GameObject vis)
    {
        var n = Noktalar(vis); var p = kap.parent;
        var b = new Bounds(p.InverseTransformPoint(n[0]), Vector3.zero);
        foreach (var q in n) b.Encapsulate(p.InverseTransformPoint(q));
        return b;
    }
    // ham modelin dönüşsüz sınırları (yön kararları için)
    static Bounds HamKutu(string ad, out List<Vector3> n)
    {
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{k_Modeller}/{ad}.fbx"));
        n = Noktalar(go); var b = Kutu(n); Object.DestroyImmediate(go); return b;
    }

    // Katener direk geçidi: genişlik x (direkten direğe 8,8 m); kiriş altı yüksekliği döndürür.
    static float KatenerModelYuksekligi;
    static Quaternion KatenerDonus()
    {
        var b = HamKutu("Katener", out _);
        return b.size.z > b.size.x ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
    }

    // Taşlı (Arnavut kaldırımı) sokak: döşeme, gömülü raylar, yeşil direkler, üçgen bayrak dizileri ve Türk bayrağı.
    static Material TasMalzemesi()
    {
        string klasor = k_Kok + "/Sokak", png = $"{klasor}/KaldirimTasi.png", matYol = $"{klasor}/KaldirimTasi.mat";
        const int S = 256; var tex = new Texture2D(S, S, TextureFormat.RGB24, true);
        var rnd = new System.Random(7);
        var renkler = new Color[64];
        for (int i = 0; i < renkler.Length; ++i) { float g = 0.66f + (float)rnd.NextDouble() * 0.16f; renkler[i] = new Color(g * 0.93f, g * 0.95f, g * 1.04f); }
        for (int y = 0; y < S; ++y)
            for (int x = 0; x < S; ++x)
            {
                int sira = y / 16, kayma = (sira % 2) * 16, sx = (x + kayma) % S, tas = (sx / 32) + sira * 8;
                int ix = (x + kayma) % 32, iy = y % 16;
                bool harc = ix < 3 || iy < 3;
                var c = harc ? new Color(0.30f, 0.31f, 0.36f) : renkler[tas % renkler.Length];
                if (!harc && (ix > 28 || iy > 13)) c *= 0.88f; // taş kenarı gölgesi
                tex.SetPixel(x, y, c);
            }
        tex.Apply(); File.WriteAllBytes(png, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(png);
        var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.anisoLevel = 9; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        if (mat == null) { mat = new Material(Shader.Find("Unlit/CurvedUnlit")); AssetDatabase.CreateAsset(mat, matYol); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png); EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material BayrakMalzemesi()
    {
        string klasor = k_Kok + "/Sokak", png = $"{klasor}/TurkBayragi.png", matYol = $"{klasor}/TurkBayragi.mat";
        const int W = 120, H = 80; var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
        Color kirmizi = new Color(0.89f, 0.04f, 0.09f), beyaz = Color.white;
        // ölçüler bayrak eninin (G=H) oranları: dış ay merkezi 0,5G, çap 0,5G; iç ay 0,625G, çap 0,4G; yıldız merkezi 0,8 G'de
        float G = H;
        Vector2 dis = new Vector2(0.5f * G, H / 2f), ic = new Vector2(0.5625f * G, H / 2f), yildiz = new Vector2(0.8f * G, H / 2f);
        bool Yildiz(Vector2 p)
        {
            var d = p - yildiz; float r = d.magnitude, R = 0.125f * G; if (r > R) return false;
            float a = Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f; float k = Mathf.Repeat(a, 2f * Mathf.PI / 5f) - Mathf.PI / 5f;
            float ic5 = R * 0.382f; float sinir = ic5 * R / (R * Mathf.Cos(k) + 0.0001f);
            return r <= Mathf.Lerp(R, ic5, Mathf.Abs(k) / (Mathf.PI / 5f)) * 1.0f;
        }
        for (int y = 0; y < H; ++y)
            for (int x = 0; x < W; ++x)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool ay = (p - dis).magnitude <= 0.25f * G && (p - ic).magnitude > 0.2f * G;
                tex.SetPixel(x, y, ay || Yildiz(p) ? beyaz : kirmizi);
            }
        tex.Apply(); File.WriteAllBytes(png, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(png);
        var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        if (mat == null) { mat = new Material(Shader.Find("Unlit/CurvedUnlit")); AssetDatabase.CreateAsset(mat, matYol); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png); EditorUtility.SetDirty(mat);
        return mat;
    }

    static void TasliSokak(GameObject inst, float z0, float z1)
    {
        string klasor = k_Kok + "/Sokak";
        // döşeme (doku metre başına tekrar), gömülü raylar ayrı ağda
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float x0 = -4.55f, x1 = 4.55f, y = 0.05f;
        // kavisli dünya gölgelendiricisi köşe noktalarını büker: döşeme küçük karelere bölünür (1 m x 1,5 m)
        int nx = Mathf.CeilToInt((x1 - x0) / 1f), nz = Mathf.CeilToInt((z1 - z0) / 1.5f);
        for (int iz = 0; iz <= nz; ++iz)
            for (int ix = 0; ix <= nx; ++ix)
            {
                float x = Mathf.Lerp(x0, x1, ix / (float)nx), z = Mathf.Lerp(z0, z1, iz / (float)nz);
                v.Add(new Vector3(x, y, z)); uv.Add(new Vector2(x / 3.6f, z / 4.0f)); // taş ~0,45 x 0,25 m
            }
        for (int iz = 0; iz < nz; ++iz)
            for (int ix = 0; ix < nx; ++ix)
            {
                int a = iz * (nx + 1) + ix, b = a + nx + 1;
                t.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
            }
        var m = new Mesh { name = inst.name + "_Kaldirim" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        string yol = $"{klasor}/{m.name}.asset"; AssetDatabase.DeleteAsset(yol); AssetDatabase.CreateAsset(m, yol);
        Cizici(inst.transform, "Kaldirim", m, TasMalzemesi());

        var yp = new Yapici();
        // yol yalnız üç şerit genişliğinde görünsün: dış şeridin 1,15 m ötesinden itibaren 25 cm yüksek koyu kaldırım
        yp.Cift(k_SeritAraligi + 1.15f, 4.6f, 0.05f, 0.3f, z0, z1, P_TasKoyu);
        yp.Cift(k_SeritAraligi + 1.15f, k_SeritAraligi + 1.3f, 0.05f, 0.34f, z0, z1, P_Krem);
        // Subway gibi her şerit ayrı bir hat: koyu yatak, traversler, birbirine yakın iki ray (şerit aralarında taş kalır;
        // eşit aralıklı ray çizgileri yolu 4-5 şeritli gösteriyordu)
        for (int l = -1; l <= 1; ++l)
        {
            float cx = l * k_SeritAraligi;
            yp.Kutu(new Vector3(cx - 0.85f, 0.05f, z0), new Vector3(cx + 0.85f, 0.056f, z1), P_TasKoyu);
            for (float z = z0 + 0.3f; z < z1; z += 0.7f)
                yp.Kutu(new Vector3(cx - 0.72f, 0.056f, z), new Vector3(cx + 0.72f, 0.07f, z + 0.2f), P_Direk);
            foreach (float r in new[] { -0.42f, 0.42f })
            {
                float x = cx + r;
                yp.Kutu(new Vector3(x - 0.06f, 0.07f, z0), new Vector3(x + 0.06f, 0.08f, z1), P_Tel);
                yp.Kutu(new Vector3(x - 0.03f, 0.08f, z0), new Vector3(x + 0.03f, 0.11f, z1), P_Isik);
            }
        }
        for (float z = z0 + 4.5f; z < z1; z += 9f)
        {
            yp.Cift(4.18f, 4.34f, 0f, 10.6f, z - 0.08f, z + 0.08f, P_Yesil);
            yp.Cift(4.14f, 4.38f, 10.6f, 10.85f, z - 0.12f, z + 0.12f, P_Isik);
            // üçgen bayrak dizisi: iki direk arasında sarkan ip (9,6 m → ortada 9,2 m), altında büyük üçgenler; tren tavanındaki kameranın (~6 m) da epey üstünde
            const int parca = 16; Vector3 Ip(float u) => new Vector3(Mathf.Lerp(-4.2f, 4.2f, u), 9.6f - 0.4f * Mathf.Sin(u * Mathf.PI), z);
            for (int i = 0; i < parca; ++i)
            {
                Vector3 a = Ip(i / (float)parca), b = Ip((i + 1) / (float)parca);
                yp.Kutu(a + new Vector3(0, -0.02f, -0.02f), new Vector3(b.x, a.y + 0.02f, z + 0.02f), P_Tel);
                int[] renk = { 14, P_Isik, P_Yesil, P_Iznik, 15 };
                Vector3 orta = (a + b) * 0.5f;
                yp.Ucgen(a + new Vector3(0.04f, 0, 0), b - new Vector3(0.04f, 0, 0), orta + new Vector3(0, -0.6f, 0), renk[i % renk.Length]);
            }
        }
        IstiklalKatener(yp, z0, z1, 4.26f);
        Cizici(inst.transform, "SokakSusu", yp.Bitir(klasor, inst.name + "_SokakSusu"), PaletMalzemesi());
        // Türk bayrağı: her ikinci dizinin ortasında, ipten sarkan
        var bv = new Yapici();
        var bayrak = new List<Vector3>(); var buv = new List<Vector2>(); var bt = new List<int>();
        for (float z = z0 + 4.5f; z < z1; z += 18f)
        {
            int s = bayrak.Count; float yy = 9.25f;
            bayrak.AddRange(new[] { new Vector3(-0.75f, yy - 1.02f, z), new Vector3(-0.75f, yy - 0.02f, z), new Vector3(0.75f, yy - 0.02f, z), new Vector3(0.75f, yy - 1.02f, z) });
            buv.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            bt.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3, s, s + 2, s + 1, s, s + 3, s + 2 });
        }
        var bm = new Mesh { name = inst.name + "_Bayrak" }; bm.SetVertices(bayrak); bm.SetUVs(0, buv); bm.SetTriangles(bt, 0); bm.RecalculateNormals(); bm.RecalculateBounds();
        string byol = $"{klasor}/{bm.name}.asset"; AssetDatabase.DeleteAsset(byol); AssetDatabase.CreateAsset(bm, byol);
        Cizici(inst.transform, "TurkBayragi", bm, BayrakMalzemesi());
    }

    // Tünel portal modeli önden (-z) ve arkadan (+z) (-executeMethod IstanbulParkur.PortalGorsel, grafik açık)
    public static void PortalGorsel()
    {
        Directory.CreateDirectory(k_Gorsel);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{k_Modeller}/Tunel.mat");
        var kok = new GameObject("Kok").transform;
        var hb = HamKutu("Tunel", out _);
        Out($"portal ham {hb.size.x:F2} x {hb.size.y:F2} x {hb.size.z:F2}");
        ModelKoy("Tunel", mat, kok, Quaternion.identity, 0, 12.6f, Vector3.zero);
        Shader.SetGlobalFloat("_CurveStrength", 0f); RenderSettings.fog = false;
        Cek("portal-on", 800, 600, new Vector3(0, 4f, -18f), Quaternion.identity, 50f, 0f, Color.cyan);
        Cek("portal-arka", 800, 600, new Vector3(0, 4f, 18f), Quaternion.Euler(0, 180, 0), 50f, 0f, Color.cyan);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // Salt okunur ölçüm (-executeMethod IstanbulParkur.SokakOlc): Istiklal01'in her çizicisinin kök uzayındaki x/y/z sınırları.
    public static void SokakOlc()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(k_Istiklal);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var b = r.bounds; var mf = r.GetComponent<MeshFilter>();
            Out($"SOKAK {r.name} ({r.GetType().Name}) x={b.min.x:F2}..{b.max.x:F2} y={b.min.y:F2}..{b.max.y:F2} z={b.min.z:F2}..{b.max.z:F2} pivot={r.transform.position} mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + "/" + mf.sharedMesh.vertexCount + " v" : "-")}");
        }
        var cam = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/UnityTechnologies/Endless Runner/Scenes/Main.unity");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/UnityTechnologies/Endless Runner/Scenes/Main.unity");
        var c = Camera.main ?? Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        var ci = Object.FindFirstObjectByType<CharacterInputController>(FindObjectsInactive.Include);
        var tm = Object.FindFirstObjectByType<TrackManager>(FindObjectsInactive.Include);
        Out($"KAMERA pos={c.transform.position} rot={c.transform.eulerAngles} fov={c.fieldOfView} | karakter pos={ci.transform.position} | laneOffset={tm.laneOffset}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------------------------------------ görseller
    // Grafik açıkken (-nographics olmadan): her parça tepeden ve oyun kamerasından; engeller ve altınlar yerinde.
    public static void Gorseller()
    {
        s_Hata = 0;
        Directory.CreateDirectory(k_Gorsel);
        var day = AssetDatabase.LoadAssetAtPath<ThemeData>(k_DayTheme);
        var altin = AssetDatabase.LoadAssetAtPath<ThemeData>(k_IstanbulTema).collectiblePrefab;
        foreach (var k in k_Kombolar)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_KomboKlasoru}/{k.ad}.prefab");
            if (prefab == null) { Fail("gorsel: yok " + k.ad); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var seg = go.GetComponent<TrackSegment>(); var tas = go.GetComponent<TasarimliSatir>();
            Vector3 p0 = seg.pathParent.GetChild(0).position, p1 = seg.pathParent.GetChild(seg.pathParent.childCount - 1).position;
            float L = Vector3.Distance(p0, p1);
            Vector3 Nokta(float m) => Vector3.Lerp(p0, p1, m / L);
            foreach (var s in tas.satirlar)
                for (int lane = -1; lane <= 1; ++lane)
                    if ((s.seritler & (1 << (lane + 1))) != 0)
                        ((GameObject)PrefabUtility.InstantiatePrefab(s.engel.editorAsset)).transform.position = Nokta(s.konum) + Vector3.right * lane * k_SeritAraligi;
            foreach (var a in tas.altinlar)
                foreach (var (m, serit, y) in TasarimliSatir.Konumlar(a))
                    ((GameObject)PrefabUtility.InstantiatePrefab(altin)).transform.position = Nokta(m) + Vector3.right * serit * k_SeritAraligi + Vector3.up * y;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat("_CurveStrength", 0f);
            Cek($"{k.ad}-tepe", 520, 2000, Nokta(L * 0.5f) + new Vector3(0f, 40f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f, L * 0.5f + 1f, day.fogColor);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = day.fogColor;
            RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 70f;
            Shader.SetGlobalFloat("_CurveStrength", 0.005f);
            Cek($"{k.ad}-oyun", 780, 1688, Nokta(0f) + new Vector3(0f, 4f, -8f), Quaternion.Euler(14.94f, 0f, 0f), 58.7f, 0f, day.fogColor);
            Shader.SetGlobalFloat("_CurveStrength", 0f);
            RenderSettings.fog = false;
            Out("gorsel " + k.ad);
        }
        // tek tek engeller (yan görünüm)
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        float x = -6f;
        foreach (var ad in new[] { "ObstacleTramvay", "ObstacleRampaliTramvay", "ObstacleSeritZipla", "ObstacleSeritKay", "ObstacleSeritZiplaKay" })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_EngelKlasoru}/{ad}.prefab");
            if (p == null) { Fail("gorsel engel yok " + ad); continue; }
            ((GameObject)PrefabUtility.InstantiatePrefab(p)).transform.position = new Vector3(x, 0f, 0f); x += 3f;
        }
        Cek("engeller-yan", 1600, 900, new Vector3(18f, 6f, 1f), Quaternion.Euler(18f, -70f, 0f), 50f, 0f, day.fogColor);
        Cek("engeller-on", 1600, 900, new Vector3(0f, 3f, -14f), Quaternion.Euler(8f, 0f, 0f), 50f, 0f, day.fogColor);
        Out($"bitti hata={s_Hata}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Hata == 0 ? 0 : 1);
    }

    static void Cek(string ad, int w, int h, Vector3 pos, Quaternion rot, float fov, float ortho, Color arka)
    {
        var cam = new GameObject("Kamera").AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        if (ortho > 0f) { cam.orthographic = true; cam.orthographicSize = ortho; } else cam.fieldOfView = fov;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = arka;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 300f;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.aspect = (float)w / h; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes($"{k_Gorsel}/{ad}.png", tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(cam.gameObject);
    }
}
