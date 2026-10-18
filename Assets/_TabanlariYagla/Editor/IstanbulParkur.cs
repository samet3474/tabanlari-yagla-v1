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
    const float k_TramvayEn = 1.36f;          // şerit aralığı 1,5 m
    public const float k_RampaBoy = 7.5f;
    const float k_SeritEn = 1.4f;
    const int k_EngelKatmani = 9, k_ZeminKatmani = 11;
    const int k_Sol = 1, k_Orta = 2, k_Sag = 4;

    static int s_Hata;
    static void Out(string s) { Debug.Log("PARKURKUR| " + s); }
    static void Fail(string s) { s_Hata++; Debug.LogError("PARKURKUR| HATA: " + s); }

    // ------------------------------------------------------------------------------------------------ tasarımlar
    class Kombo
    {
        public string ad, baslik, karar; public int kat = 3;
        public (float m, string engel, int maske)[] satir;
        public TasarimliSatir.Altin[] altin;
    }
    static TasarimliSatir.Altin Duz(int s, float a, float b, float h = 0f) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Duz, serit = s, bitisSeridi = s, baslangic = a, bitis = b, yukseklik = h };
    static TasarimliSatir.Altin Yay(int s, float a, float b, float tepe) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Yay, serit = s, bitisSeridi = s, baslangic = a, bitis = b, yukseklik = tepe };
    static TasarimliSatir.Altin Capraz(int s0, int s1, float a, float b, float h = 0f) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Capraz, serit = s0, bitisSeridi = s1, baslangic = a, bitis = b, yukseklik = h };
    // Rampa üzerinde: rampa başlangıcı r0 (m), yüzey yüksekliği eğimle.
    static TasarimliSatir.Altin RampaAltin(int s, float r0, float a, float b) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Egim, serit = s, bitisSeridi = s, baslangic = a, bitis = b, yukseklik = (a - r0) * k_Tavan / k_RampaBoy, bitisYukseklik = (b - r0) * k_Tavan / k_RampaBoy };

    // Engel adları: kendi prefablarımız ya da mevcut Default engelleri (Obstacle<ad>).
    static readonly Kombo[] k_Kombolar =
    {
        new Kombo { ad = "IstiklalK01", baslik = "K01 Tramvay hatti", karar = "ortada duran tramvay: yana gec; sonra soldaki cop kutusu",
            satir = new[] { (12f, "Tramvay", k_Orta), (36f, "Bin", k_Sol) },
            altin = new[] { Duz(1, 6f, 27f), Capraz(1, 0, 28.5f, 33f), Duz(0, 34.5f, 51f) } },
        new Kombo { ad = "IstiklalK02", baslik = "K02 Zikzak", karar = "sag -> sol (iki serit) -> sag/orta: uc ardisik serit degisimi",
            satir = new[] { (10f, "Bin", k_Sol | k_Orta), (27f, "WheelyBin", k_Orta | k_Sag), (44f, "RoadworksBarrier", k_Sol), (44f, "RoadworksCone", k_Orta) },
            altin = new[] { Duz(1, 0f, 7.5f), Capraz(1, -1, 13.5f, 22.5f), Duz(-1, 24f, 28.5f), Capraz(-1, 1, 30f, 39f), Duz(1, 40.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK03", baslik = "K03 Atla ve sap", karar = "tum serit alcak bariyer: zipla; inince sola gec; sonra soldaki tramvaydan kac",
            satir = new[] { (12f, "LowBarrier", k_Orta), (28f, "Bin", k_Orta), (28f, "WheelyBin", k_Sag), (44f, "Tramvay", k_Sol) },
            altin = new[] { Duz(0, 3f, 6f), Yay(0, 7.5f, 16.5f, 1.9f), Capraz(0, -1, 19.5f, 25.5f), Duz(-1, 27f, 37.5f), Capraz(-1, 0, 39f, 42f), Duz(0, 43.5f, 52.5f) } },
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
            altin = new[] { RampaAltin(0, 14f - k_RampaBoy, 8f, 12.5f), Duz(0, 15f, 31.5f, k_Tavan), Yay(-1, 16f, 24f, 1.6f), Duz(0, 36f, 52.5f) } },
        new Kombo { ad = "IstiklalK10", baslik = "K10 Yol calismasi", karar = "bariyer+koni, kutu+bariyer, koni+kutu: her sirada farkli acik serit ya da zipla",
            satir = new[] { (10f, "RoadworksBarrier", k_Sol), (10f, "RoadworksCone", k_Orta), (24f, "WheelyBin", k_Sag), (24f, "RoadworksBarrier", k_Orta), (38f, "RoadworksCone", k_Sol), (38f, "Bin", k_Orta) },
            altin = new[] { Yay(0, 7f, 13f, 1.6f), Capraz(0, -1, 15f, 21f), Duz(-1, 22.5f, 34f), Yay(-1, 35f, 41f, 1.6f), Duz(-1, 43.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK11", baslik = "K11 Dar gecit", karar = "iki yanda tramvay: ortada kay; ikinci gecitte zipla ya da kay; sonra tum serit kay",
            satir = new[] { (8f, "Tramvay", k_Sol | k_Sag), (13f, "SeritKay", k_Orta), (26f, "Tramvay", k_Sol | k_Sag), (31f, "SeritZiplaKay", k_Orta), (46f, "HighBarrier", k_Orta) },
            altin = new[] { Duz(0, 3f, 24f), Yay(0, 27f, 35f, 1.9f), Duz(0, 37.5f, 52.5f) } },
        new Kombo { ad = "IstiklalK12", baslik = "K12 Uzun kombinasyon", kat = 4, karar = "sag'a gec; sagda rampadan tavana cik ya da ortada kay; tum serit bariyeri atla; sonra sola ya da koniyi atla",
            satir = new[] { (10f, "Bin", k_Sol | k_Orta), (22f, "Tramvay", k_Sol), (24f, "RampaliTramvay", k_Sag), (28f, "SeritKay", k_Orta), (52f, "LowBarrier", k_Orta), (64f, "WheelyBin", k_Orta), (64f, "RoadworksCone", k_Sag) },
            altin = new[] { Duz(1, 0f, 13.5f), RampaAltin(1, 24f - k_RampaBoy, 18f, 22.5f), Duz(1, 25.5f, 31.5f, k_Tavan), Duz(0, 27f, 30f), Yay(1, 48f, 56f, 1.9f), Yay(1, 61f, 67f, 1.6f), Duz(1, 68f, 70.5f) } },
        new Kombo { ad = "IstiklalK13", baslik = "K13 Karsidan tramvay", karar = "sagda duran tramvay, ortada kutu; soldan karsidan gelen tramvay: saga gec",
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
            var engeller = new Dictionary<string, string>();
            engeller["Tramvay"] = TramvayPrefab("ObstacleTramvay", tramvayMat, false, false);
            engeller["TramvayHareketli"] = TramvayPrefab("ObstacleTramvayHareketli", tramvayMat, true, false);
            engeller["RampaliTramvay"] = TramvayPrefab("ObstacleRampaliTramvay", tramvayMat, false, true, rampaMat);
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
            Tetik(root, new Vector3(0f, govde * 0.5f, k_TramvayBoy * 0.5f), new Vector3(1.3f, govde, k_TramvayBoy), root.GetComponent<BoxCollider>());
            Zemin(root.transform, "TavanZemin", new Vector3(0f, k_Tavan, k_TramvayBoy * 0.5f), Quaternion.identity, new Vector3(1.4f, 0.2f, k_TramvayBoy));
            if (rampali)
            {
                Yerlestir($"{k_Modeller}/Rampa.fbx", rampaMat, root.transform, "Rampa", k_TramvayEn, k_RampaBoy, -k_RampaBoy, k_Tavan, true);
                float aci = Mathf.Atan2(k_Tavan, k_RampaBoy) * Mathf.Rad2Deg, hip = Mathf.Sqrt(k_Tavan * k_Tavan + k_RampaBoy * k_RampaBoy);
                Zemin(root.transform, "RampaZemin", new Vector3(0f, k_Tavan * 0.5f, -k_RampaBoy * 0.5f), Quaternion.Euler(-aci, 0f, 0f), new Vector3(1.4f, 0.1f, hip));
                // Rampaya yandan girilemez: yüzeyin tırmanma adımını (0,6 m) aştığı kısımda basamaklı tetikleyiciler,
                // her biri başladığı yerdeki yüzeyin 0,3 m altında biter (rampada koşan çarpmaz).
                float egim = k_Tavan / k_RampaBoy, bas = -k_RampaBoy + 0.6f / egim;
                const int adim = 4; float uz = -bas / adim;
                for (int i = 0; i < adim; ++i)
                {
                    float z0 = bas + i * uz, ust = (z0 + k_RampaBoy) * egim - 0.3f;
                    Tetik(root, new Vector3(0f, ust * 0.5f, z0 + uz * 0.5f), new Vector3(1.3f, ust, uz));
                }
            }
            if (hareketli)
            {
                var h = root.AddComponent<HareketliTramvay>(); h.hiz = 6f; h.baslamaMesafesi = 45f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, yol);
            Out($"engel {ad}: tetikleyici={root.GetComponents<BoxCollider>().Length} hareketli={hareketli} rampali={rampali}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return yol;
    }

    // Tek şerit bariyeri: mevcut tüm-şerit bariyerin görseli şerit genişliğine (1,4 m) daraltılır; tetikleyici y0..y1.
    static string SeritBariyeri(string ad, string kaynak, float yOlcek, float y0, float y1)
    {
        var root = KoniKopyasi(ad, out string yol); if (root == null) return null;
        try
        {
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
            Tetik(root, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(1.3f, y1 - y0, 0.3f), root.GetComponent<BoxCollider>());
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

    // ------------------------------------------------------------------------------------------------ görseller
    // Grafik açıkken (-nographics olmadan): her parça tepeden ve oyun kamerasından; engeller ve altınlar yerinde.
    public static void Gorseller()
    {
        s_Hata = 0;
        Directory.CreateDirectory(k_Gorsel);
        var day = AssetDatabase.LoadAssetAtPath<ThemeData>(k_DayTheme);
        var altin = AssetDatabase.LoadAssetAtPath<GameObject>(k_Altin);
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
                        ((GameObject)PrefabUtility.InstantiatePrefab(s.engel.editorAsset)).transform.position = Nokta(s.konum) + Vector3.right * lane * 1.5f;
            foreach (var a in tas.altinlar)
                for (float m = a.baslangic; m <= a.bitis + 0.001f; m += TasarimliSatir.k_AltinAraligi)
                {
                    float o = a.bitis > a.baslangic ? (m - a.baslangic) / (a.bitis - a.baslangic) : 0f;
                    ((GameObject)PrefabUtility.InstantiatePrefab(altin)).transform.position = Nokta(m) + Vector3.right * TasarimliSatir.AltinSeridi(a, o) * 1.5f + Vector3.up * TasarimliSatir.AltinYuksekligi(a, o);
                }
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
