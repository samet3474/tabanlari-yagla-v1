// İstanbul teması — simit tezgâhı test engeli (batchmode: -executeMethod IstanbulUygula.SimitTezgahi).
//
// Kaynak: Meshy'nin kendi ürettiği FBX (_araclar/istanbul/simit_3d_01a14b68/model.fbx). Arşivdeki dosya değiştirilmez;
// projeye bayt bayt kopyalanır ve özeti arşivle karşılaştırılır. Lisans/üst veri notu: _araclar/istanbul/URETIM_VE_LISANS.md.
//
// Yapılanlar (tekrar çalıştırılabilir):
//  1. FBX → Assets/_TabanlariYagla/Istanbul/Test/SimitTezgahi/ ; gömülü doku çıkarılır, oyunda en fazla 1024 piksel.
//  2. CurvedUnlit malzemesi (yalnız renk dokusu; ışık ve gölge yok — oyunun diğer modelleri gibi).
//  3. ObstacleSimitTezgahi.prefab: mevcut ObstacleRoadworksCone'un kopyası (SimpleBarricade, ses, katman 9, tetikleyici
//     kutu). Görsel değiştirilir; görsel 1,4 m genişliğe ölçeklenir; çarpışma kutusu 1,4 x 1,0 m.
//  4. Addressables: "Istanbul-Test" grubu (Day-Theme-Obstacles ayarlarıyla), adres = prefab adı (SimpleBarricade
//     engeli adıyla yükler).
// Tema, parça, mevcut engel ve oyun kodu değişmez.
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Rendering;

public static class IstanbulUygula
{
    const string k_ArchiveFbx = "../_araclar/istanbul/simit_3d_01a14b68/model.fbx";
    const string k_ArchiveSha = "../_araclar/istanbul/simit_3d_01a14b68/SHA256SUMS.txt";
    const string k_Folder = "Assets/_TabanlariYagla/Istanbul/Test/SimitTezgahi";
    const string k_Fbx = k_Folder + "/SimitTezgahi.fbx";
    const string k_Textures = k_Folder + "/Textures";
    const string k_Material = k_Folder + "/SimitTezgahi.mat";
    const string k_Prefab = k_Folder + "/ObstacleSimitTezgahi.prefab";
    const string k_ConePrefab = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/Obstacles/ObstacleRoadworksCone.prefab";
    const string k_Group = "Istanbul-Test";
    const string k_SourceGroup = "Day-Theme-Obstacles";

    const float k_Width = 1.4f;          // şerit aralığı 1,5 m
    const float k_ColliderHeight = 1.0f; // zıplama yüksekliği 1,2 m; trafik konisi 0,95 m
    const int k_MaxTexture = 1024;

    static int s_Errors;
    static void Out(string s) { Debug.Log("IST| " + s); }
    static void Fail(string s) { s_Errors++; Debug.LogError("IST| HATA: " + s); }

    public static void SimitTezgahi()
    {
        s_Errors = 0;
        CopyFbx();
        if (s_Errors == 0) ImportModel();
        if (s_Errors == 0) BuildPrefab();
        if (s_Errors == 0) AddToAddressables();
        AssetDatabase.SaveAssets();
        Out(s_Errors == 0 ? "BITTI" : $"BITTI hata={s_Errors}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    static string Sha(string path)
    {
        using (var sha = SHA256.Create())
        using (var f = File.OpenRead(path))
            return string.Concat(sha.ComputeHash(f).Select(b => b.ToString("x2")));
    }

    static void CopyFbx()
    {
        if (!File.Exists(k_ArchiveFbx)) { Fail("arşiv FBX yok: " + Path.GetFullPath(k_ArchiveFbx)); return; }
        string expected = File.ReadAllLines(k_ArchiveSha).Select(l => l.Split(' ')).FirstOrDefault(p => p.Length > 1 && p[p.Length - 1].TrimStart('*') == "model.fbx")?[0];
        string archive = Sha(k_ArchiveFbx);
        if (archive != expected) { Fail("arşiv FBX özeti değişmiş"); return; }
        Directory.CreateDirectory(k_Folder);
        if (!File.Exists(k_Fbx) || Sha(k_Fbx) != archive)
        {
            File.Copy(k_ArchiveFbx, k_Fbx, true);
            AssetDatabase.ImportAsset(k_Fbx, ImportAssetOptions.ForceUpdate);
        }
        Out($"FBX kopyası arşivle aynı: {Sha(k_Fbx) == archive} ({archive.Substring(0, 16)})");
    }

    static void ImportModel()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(k_Fbx);
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        mi.animationType = ModelImporterAnimationType.None;
        // Gömülü dokuyu çıkarmak için önce malzemeler okunur; sonra FBX'in malzemeleri kullanılmaz (kendi CurvedUnlit malzememiz).
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.SaveAndReimport();
        Directory.CreateDirectory(k_Textures);
        if (AssetDatabase.FindAssets("t:Texture2D", new[] { k_Textures }).Length == 0)
        {
            mi.ExtractTextures(k_Textures);
            AssetDatabase.Refresh();
        }
        mi = (ModelImporter)AssetImporter.GetAtPath(k_Fbx);
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.SaveAndReimport();

        string texPath = AssetDatabase.FindAssets("t:Texture2D", new[] { k_Textures }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        if (texPath == null) { Fail("FBX'ten doku çıkarılamadı"); return; }
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        ti.maxTextureSize = k_MaxTexture;
        ti.mipmapEnabled = true;
        ti.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        Out($"doku: {texPath} kaynak sınırı={ti.maxTextureSize} oyunda={tex.width}x{tex.height}");

        var shader = Shader.Find("Unlit/CurvedUnlit");
        if (shader == null) { Fail("Unlit/CurvedUnlit shader'ı yok"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(k_Material);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, k_Material); }
        mat.shader = shader;
        mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);
    }

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void BuildPrefab()
    {
        if (!File.Exists(k_Prefab) && !AssetDatabase.CopyAsset(k_ConePrefab, k_Prefab)) { Fail("koni prefab'ı kopyalanamadı"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(k_Prefab);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(k_Prefab);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // Koninin görseli ve çarpma animasyonu (yalnız koniye ait) çıkarılır; Obstacle.Impacted animasyon yoksa yalnız ses çalar.
            for (int i = root.transform.childCount - 1; i >= 0; --i)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_Fbx);
            var vis = (GameObject)Object.Instantiate(model, root.transform);
            vis.name = "SimitTezgahi";
            vis.transform.localPosition = Vector3.zero;
            foreach (var t in vis.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;

            // Eksen: genişlik (en uzun) x, yükseklik (orta) y, derinlik (en kısa) z — Meshy GLB'deki ölçülerle aynı sıra.
            Bounds b = RendererBounds(vis);
            Out($"ham ölçü {b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3}");
            if (b.size.z > b.size.y && b.size.z < b.size.x) vis.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * vis.transform.localRotation;
            b = RendererBounds(vis);
            if (b.size.z > b.size.x) vis.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * vis.transform.localRotation;
            b = RendererBounds(vis);

            vis.transform.localScale *= k_Width / b.size.x;
            b = RendererBounds(vis);
            vis.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = RendererBounds(vis);
            Out($"görsel ölçü {b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3} (taban y={b.min.y:F3})");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(k_Material);
            int tris = 0;
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            foreach (var mf in vis.GetComponentsInChildren<MeshFilter>(true)) tris += mf.sharedMesh.triangles.Length / 3;

            var box = root.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(k_Width, k_ColliderHeight, Mathf.Min(b.size.z, 0.9f));
            box.center = new Vector3(0f, k_ColliderHeight * 0.5f, 0f);
            Out($"çarpışma kutusu {box.size.x:F2} x {box.size.y:F2} x {box.size.z:F2}, katman={root.layer}, SimpleBarricade={root.GetComponent<SimpleBarricade>() != null}");
            Out($"üçgen={tris}");

            PrefabUtility.SaveAsPrefabAsset(root, k_Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Salt okunur denetim (-executeMethod IstanbulUygula.Dogrula): Addressables ayarlarını açar, grupların ve simit
    // kaydının çözüldüğünü yazar. Hiçbir şey kaydetmez.
    public static void Dogrula()
    {
        s_Errors = 0;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) Fail("Addressables ayarları açılamadı");
        else
        {
            int nulls = settings.groups.Count(g => g == null);
            Out($"gruplar={settings.groups.Count} cozulemeyen={nulls}: {string.Join(", ", settings.groups.Where(g => g != null).Select(g => g.Name))}");
            if (nulls > 0) Fail("çözülemeyen grup kaydı var");
            var group = settings.FindGroup(k_Group);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(k_Prefab));
            if (group == null) Fail(k_Group + " grubu bulunamadı");
            if (entry == null || entry.parentGroup != group) Fail("simit kaydı grupta değil");
            else Out($"kayit: adres={entry.address} grup={entry.parentGroup.Name} sema={string.Join(",", group.Schemas.Select(s => s.GetType().Name))}");
            Out($"etkin derleyici={settings.ActivePlayerDataBuilder?.Name} ({settings.ActivePlayerDataBuilderIndex}/{settings.DataBuilders.Count})");
        }
        Out(s_Errors == 0 ? "DOGRULAMA TAMAM" : $"DOGRULAMA hata={s_Errors}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ İstiklal test parçası
    // -executeMethod IstanbulUygula.IstiklalParca (tekrar çalıştırılabilir).
    // Day temasındaki UrbanBuilding01 kopyalanır: yol (Road01/02), yol noktaları (Entry/Exit, 18 m), sokak lambası, uzak
    // arka plan ve engel noktası aynen kalır. Sanayi binaları ve borular çıkarılır; yerine bu araçla üretilen tek bir mesh
    // gelir (mevcut VCOL malzemesi: CurvedUnlit, dokusuz, renk köşe renginden): İstiklal'i çağrıştıran özgün cepheler
    // (yazı, tabela, marka yok), ortada tramvay rayları ve taş döşeme. Olası engellere simit tezgâhı eklenir; parçada tek
    // engel noktası olduğundan simit en fazla 2 kez çıkar (SimpleBarricade 1-2 şerit). Addressables: Istanbul-Test grubu.
    const string k_IstiklalFolder = "Assets/_TabanlariYagla/Istanbul/Test/Istiklal";
    const string k_IstiklalPrefab = k_IstiklalFolder + "/IstiklalTest01.prefab";
    const string k_IstiklalMesh = k_IstiklalFolder + "/IstiklalSokak.asset";
    const string k_SourceSegment = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/Urban/UrbanBuilding01.prefab";
    const string k_VcolMaterial = "Assets/UnityTechnologies/Endless Runner/Art/Materials/VCOL.mat";
    static readonly string[] k_RemoveFromSegment = { "Decorations/Building02", "Decorations/Apartments04", "Decorations/Pipes" };

    public static void IstiklalParca()
    {
        s_Errors = 0;
        Directory.CreateDirectory(k_IstiklalFolder);
        AssetDatabase.Refresh();

        Mesh mesh = BuildIstiklalMesh();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(k_IstiklalMesh);
        if (old == null) AssetDatabase.CreateAsset(mesh, k_IstiklalMesh);
        else { EditorUtility.CopySerialized(mesh, old); mesh = old; EditorUtility.SetDirty(old); }
        Out($"sokak mesh: üçgen={mesh.triangles.Length / 3} köşe={mesh.vertexCount} sınır={mesh.bounds.min:F2}..{mesh.bounds.max:F2}");

        if (!File.Exists(k_IstiklalPrefab) && !AssetDatabase.CopyAsset(k_SourceSegment, k_IstiklalPrefab)) { Fail("parça kopyalanamadı"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(k_IstiklalPrefab);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(k_IstiklalPrefab);
            foreach (string p in k_RemoveFromSegment)
            {
                var t = root.transform.Find(p);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var deco = root.transform.Find("Decorations");
            Transform street = deco.Find("IstiklalSokak");
            if (street == null) street = new GameObject("IstiklalSokak").transform;
            street.SetParent(deco, false);
            street.localPosition = Vector3.zero; street.localRotation = Quaternion.identity; street.localScale = Vector3.one;
            street.gameObject.layer = deco.gameObject.layer;
            var mf = street.GetComponent<MeshFilter>(); if (mf == null) mf = street.gameObject.AddComponent<MeshFilter>();
            var mr = street.GetComponent<MeshRenderer>(); if (mr == null) mr = street.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_VcolMaterial);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var seg = root.GetComponent<TrackSegment>();
            string simitGuid = AssetDatabase.AssetPathToGUID(k_Prefab);
            if (!seg.possibleObstacles.Any(r => r.AssetGUID == simitGuid))
                seg.possibleObstacles = seg.possibleObstacles.Concat(new[] { new UnityEngine.AddressableAssets.AssetReference(simitGuid) }).ToArray();
            Out($"engel noktaları=[{string.Join(",", seg.obstaclePositions)}] olası engel: {string.Join(", ", seg.possibleObstacles.Select(r => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(r.AssetGUID))))}");

            int tris = 0, renderers = 0;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.GetComponent<MeshRenderer>() == null) continue;
                renderers++;
                for (int i = 0; i < f.sharedMesh.subMeshCount; ++i) tris += (int)f.sharedMesh.GetIndexCount(i) / 3;
            }
            Out($"parça (engelsiz): çizici={renderers} üçgen={tris}");
            foreach (Transform p in seg.pathParent) Out($"  yol {p.name} {p.localPosition}");
            PrefabUtility.SaveAsPrefabAsset(root, k_IstiklalPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup(k_Group);
        if (group == null) Fail(k_Group + " grubu yok");
        else
        {
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_IstiklalPrefab), group);
            entry.address = Path.GetFileNameWithoutExtension(k_IstiklalPrefab);
            Out($"Addressables: grup={group.Name} adres={entry.address} guid={entry.guid}");
        }
        AssetDatabase.SaveAssets();
        Out(s_Errors == 0 ? "BITTI" : $"BITTI hata={s_Errors}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ İstanbul teması (üretim iskeleti)
    // -executeMethod IstanbulUygula.IstanbulTema (tekrar çalıştırılabilir).
    // Ayrı ve seçilebilir tema: fiyat 0, mağazadan alınır, menüden seçilir; Day varsayılan kalır (oyun kodu değişmez).
    //  - Istanbul/Istiklal/Istiklal01.prefab: test parçasıyla aynı yapı (UrbanBuilding01 kopyası + İstiklal sokağı),
    //    ancak simit tezgâhı YOK (Meshy §2.4 belirsiz): yalnız mevcut alçak bariyer, koni, çöp kutusu.
    //  - Istanbul/IstanbulOnizleme.png: tema simgesi; bu parçanın editörde çekilmiş görüntüsü (yalnız proje geometrisi).
    //  - Istanbul/IstanbulTheme.asset: Day teması kopyası; ad "Istanbul", fiyat 0, tek bölge (İstiklal, 500 m).
    //    Altın, premium, bulut, gökyüzü ve menü zemini Day ile paylaşılır.
    //  - Addressables: tema Themes grubuna (adres ve etiket "themeData", Day/Night gibi); parça yeni Istanbul-Zones
    //    grubuna (Default-Zones ayarlarıyla). Simit ve Istanbul-Test prototipleri olduğu gibi kalır.
    const string k_TemaFolder = "Assets/_TabanlariYagla/Istanbul";
    const string k_UretimFolder = k_TemaFolder + "/Istiklal";
    const string k_UretimPrefab = k_UretimFolder + "/Istiklal01.prefab";
    const string k_UretimMesh = k_UretimFolder + "/Istiklal01Sokak.asset";
    const string k_TemaAsset = k_TemaFolder + "/IstanbulTheme.asset";
    const string k_TemaIcon = k_TemaFolder + "/IstanbulOnizleme.png";
    const string k_DayTheme = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/themeData.asset";
    const string k_ZonesGroup = "Istanbul-Zones";
    const string k_ZonesSourceGroup = "Default-Zones";
    const string k_ThemesGroup = "Themes";

    public static void IstanbulTema()
    {
        s_Errors = 0;
        Directory.CreateDirectory(k_UretimFolder);
        AssetDatabase.Refresh();

        // 1) Üretim parçası (simitsiz).
        Mesh mesh = BuildIstiklalMesh();
        var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(k_UretimMesh);
        if (oldMesh == null) AssetDatabase.CreateAsset(mesh, k_UretimMesh);
        else { EditorUtility.CopySerialized(mesh, oldMesh); mesh = oldMesh; EditorUtility.SetDirty(oldMesh); }
        if (!File.Exists(k_UretimPrefab) && !AssetDatabase.CopyAsset(k_SourceSegment, k_UretimPrefab)) { Fail("parça kopyalanamadı"); Bitir(); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(k_UretimPrefab);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(k_UretimPrefab);
            foreach (string p in k_RemoveFromSegment) { var t = root.transform.Find(p); if (t != null) Object.DestroyImmediate(t.gameObject); }
            var deco = root.transform.Find("Decorations");
            Transform street = deco.Find("IstiklalSokak");
            if (street == null) street = new GameObject("IstiklalSokak").transform;
            street.SetParent(deco, false);
            street.localPosition = Vector3.zero; street.localRotation = Quaternion.identity; street.localScale = Vector3.one;
            street.gameObject.layer = deco.gameObject.layer;
            var mf = street.GetComponent<MeshFilter>(); if (mf == null) mf = street.gameObject.AddComponent<MeshFilter>();
            var mr = street.GetComponent<MeshRenderer>(); if (mr == null) mr = street.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_VcolMaterial);
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var seg = root.GetComponent<TrackSegment>();
            string simitGuid = AssetDatabase.AssetPathToGUID(k_Prefab);
            seg.possibleObstacles = seg.possibleObstacles.Where(r => r.AssetGUID != simitGuid).ToArray();
            if (seg.possibleObstacles.Length == 0) Fail("parçada engel kalmadı");
            Out($"Istiklal01: engel noktaları=[{string.Join(",", seg.obstaclePositions)}] olası engel: {string.Join(", ", seg.possibleObstacles.Select(r => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(r.AssetGUID))))}");
            int tris = 0;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null)
                    for (int i = 0; i < f.sharedMesh.subMeshCount; ++i) tris += (int)f.sharedMesh.GetIndexCount(i) / 3;
            Out($"Istiklal01: engelsiz üçgen={tris}");
            PrefabUtility.SaveAsPrefabAsset(root, k_UretimPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (s_Errors > 0) { Bitir(); return; }

        // 2) Tema simgesi: parçanın görüntüsü (Day önizlemesiyle aynı oran, 363x149 → 2 kat çözünürlük).
        RenderIcon();
        var icon = AssetDatabase.LoadAllAssetsAtPath(k_TemaIcon).OfType<Sprite>().FirstOrDefault();
        if (icon == null) { Fail("simge sprite olarak yüklenemedi"); Bitir(); return; }

        // 3) Tema: Day kopyası, alanları değiştirilir.
        if (!File.Exists(k_TemaAsset) && !AssetDatabase.CopyAsset(k_DayTheme, k_TemaAsset)) { Fail("tema kopyalanamadı"); Bitir(); return; }
        var theme = AssetDatabase.LoadAssetAtPath<ThemeData>(k_TemaAsset);
        theme.themeName = "Istanbul";
        theme.cost = 0;
        theme.premiumCost = 0;
        theme.themeIcon = icon;
        theme.zones = new[]
        {
            new ThemeZone { length = 500, prefabList = new[] { new UnityEngine.AddressableAssets.AssetReference(AssetDatabase.AssetPathToGUID(k_UretimPrefab)) } },
        };
        EditorUtility.SetDirty(theme);
        Out($"tema: ad={theme.themeName} fiyat={theme.cost}/{theme.premiumCost} bölge={theme.zones.Length} parça={theme.zones[0].prefabList.Length} simge={icon.rect.width}x{icon.rect.height}");

        // 4) Addressables.
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var themes = settings.FindGroup(k_ThemesGroup);
        var zonesSrc = settings.FindGroup(k_ZonesSourceGroup);
        if (themes == null || zonesSrc == null) { Fail("Themes / Default-Zones grubu yok"); Bitir(); return; }
        var te = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_TemaAsset), themes);
        te.address = "themeData";
        te.SetLabel("themeData", true, true);
        var zones = settings.FindGroup(k_ZonesGroup) ?? settings.CreateGroup(k_ZonesGroup, false, false, true, zonesSrc.Schemas);
        var se = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_UretimPrefab), zones);
        se.address = Path.GetFileNameWithoutExtension(k_UretimPrefab);
        Out($"Addressables: {themes.Name} ← {k_TemaAsset} (etiket themeData); {zones.Name} ← {se.address}; grup guid={AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(zones))}");

        AssetDatabase.SaveAssets();
        Bitir();
    }

    static void Bitir()
    {
        Out(s_Errors == 0 ? "BITTI" : $"BITTI hata={s_Errors}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    static void RenderIcon()
    {
        const int w = 726, h = 298;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_UretimPrefab);
        for (int i = 0; i < 4; ++i)
        {
            var s = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            s.transform.position = new Vector3(0f, 0f, i * 17.99f);
        }
        var day = AssetDatabase.LoadAssetAtPath<ThemeData>(k_DayTheme);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = day.fogColor;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 70f;
        Shader.SetGlobalFloat("_CurveStrength", 0.005f);
        var cam = new GameObject("IconCamera").AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 2.6f, -7.5f);
        cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.6f, 30f) - cam.transform.position);
        cam.fieldOfView = 34f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = day.fogColor;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(k_TemaIcon, tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release();
        Shader.SetGlobalFloat("_CurveStrength", 0f);
        AssetDatabase.ImportAsset(k_TemaIcon, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(k_TemaIcon);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = false;
        ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
        Out($"simge: {k_TemaIcon} {w}x{h}");
    }

    // Salt okunur denetim (-executeMethod IstanbulUygula.TemaDogrula): tema ve Addressables kayıtları. Kaydetmez.
    public static void TemaDogrula()
    {
        s_Errors = 0;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        int nulls = settings.groups.Count(g => g == null);
        Out($"gruplar={settings.groups.Count} cozulemeyen={nulls}: {string.Join(", ", settings.groups.Where(g => g != null).Select(g => g.Name))}");
        if (nulls > 0) Fail("çözülemeyen grup kaydı var");
        var te = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(k_TemaAsset));
        if (te == null || te.parentGroup.Name != k_ThemesGroup || !te.labels.Contains("themeData")) Fail("tema kaydı Themes grubunda 'themeData' etiketiyle değil");
        else Out($"tema kaydı: grup={te.parentGroup.Name} adres={te.address} etiket={string.Join(",", te.labels)}");
        var se = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(k_UretimPrefab));
        if (se == null || se.parentGroup.Name != k_ZonesGroup) Fail("Istiklal01 Istanbul-Zones grubunda değil");
        else Out($"parça kaydı: grup={se.parentGroup.Name} adres={se.address} sema={string.Join(",", se.parentGroup.Schemas.Select(s => s.GetType().Name))}");
        var simit = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(k_Prefab));
        Out($"simit kaydı: grup={(simit != null ? simit.parentGroup.Name : "yok")} (üretim grubunda olmamalı)");
        if (simit != null && simit.parentGroup.Name != k_Group) Fail("simit üretim grubunda");
        var theme = AssetDatabase.LoadAssetAtPath<ThemeData>(k_TemaAsset);
        var deps = AssetDatabase.GetDependencies(k_TemaAsset, true).Where(d => !d.EndsWith(".cs")).ToArray();
        Out($"tema bağımlılıkları ({deps.Length}): {string.Join(", ", deps.Select(Path.GetFileName))}");
        if (deps.Any(d => d.Contains("SimitTezgahi"))) Fail("tema simite bağımlı");
        Out($"etkin derleyici={settings.ActivePlayerDataBuilder?.Name} ({settings.ActivePlayerDataBuilderIndex}/{settings.DataBuilders.Count})");
        Out(s_Errors == 0 ? "DOGRULAMA TAMAM" : $"DOGRULAMA hata={s_Errors}");
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    // Basit kutu/dörtgen geometri; renk köşe renginde. Işık yok (CurvedUnlit), derinlik hissi için yüz yönüne göre
    // gölgeleme köşe rengine işlenir. Sol taraf kurulur, sağ taraf x aynasıyla eklenir (farklı renk dizisiyle).
    class MeshBuilder
    {
        public readonly System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>();
        public readonly System.Collections.Generic.List<Color32> c = new System.Collections.Generic.List<Color32>();
        public readonly System.Collections.Generic.List<int> t = new System.Collections.Generic.List<int>();
        public bool mirror;

        static float Shade(Vector3 n)
        {
            if (n.y > 0.5f) return 1.0f;
            if (n.y < -0.5f) return 0.62f;
            if (Mathf.Abs(n.z) > 0.5f) return 0.8f;
            return 0.94f;
        }

        Vector3 M(Vector3 p) => mirror ? new Vector3(-p.x, p.y, p.z) : p;

        // a,b,c,d çevresel sırada; ön yüz n yönüne bakar (sarım kendiliğinden düzeltilir).
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 n, Color col)
        {
            a = M(a); b = M(b); cc = M(cc); d = M(d); if (mirror) n.x = -n.x;
            Color32 k = (Color)(col * Shade(n)); k.a = 255;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int j = 0; j < 4; ++j) c.Add(k);
            bool front = Vector3.Dot(Vector3.Cross(b - a, cc - a), n) > 0f;
            if (front) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        // Yalnız +x yönüne (sol taraftaki cephelerin sokağa bakan yüzü) bakan dörtgen.
        public void FaceX(float x, float y0, float y1, float z0, float z1, Color col)
            => Quad(new Vector3(x, y0, z0), new Vector3(x, y1, z0), new Vector3(x, y1, z1), new Vector3(x, y0, z1), Vector3.right, col);

        public void Box(float x0, float x1, float y0, float y1, float z0, float z1, Color col, bool bottom = true)
        {
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, col);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.left, col);
            Quad(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.up, col);
            if (bottom) Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down, col);
            Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.back, col);
            Quad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward, col);
        }
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    struct Building { public float z0, z1; public int floors; public Color wall; public Color awning; public bool cumba, balconies; }

    static Mesh BuildIstiklalMesh()
    {
        const float zStart = -4.5f, zEnd = 13.5f;   // parça: Entry z=-4,49 → Exit z=13,5
        const float face = -2.95f;                   // cephe çizgisi (mevcut binalarla aynı, x=±2,95)
        const float back = -9.0f;
        const float ground = 4.0f, floorH = 3.2f;
        Color cornice = Hex("EFE8DA"), frame = Hex("E9E2D2"), glass = Hex("2F3A46"), shop = Hex("3B4954"), rail = Hex("2B2B2E");

        var mb = new MeshBuilder();
        Building[][] sides =
        {
            new[] {
                new Building { z0 = zStart, z1 = 1.8f,  floors = 4, wall = Hex("E3CFA6"), awning = Hex("A83A32"), balconies = true },
                new Building { z0 = 1.8f,  z1 = 7.2f,  floors = 3, wall = Hex("C98468"), awning = Hex("2F6A4E"), cumba = true },
                new Building { z0 = 7.2f,  z1 = zEnd,  floors = 4, wall = Hex("BFB9AE"), awning = Hex("2B3F66"), balconies = true },
            },
            new[] {
                new Building { z0 = zStart, z1 = 1.2f,  floors = 3, wall = Hex("D8A65E"), awning = Hex("2F6A4E"), cumba = true },
                new Building { z0 = 1.2f,  z1 = 7.8f,  floors = 4, wall = Hex("E6BCAB"), awning = Hex("A83A32"), balconies = true },
                new Building { z0 = 7.8f,  z1 = zEnd,  floors = 4, wall = Hex("A9BB9E"), awning = Hex("6B2E3E"), balconies = true },
            },
        };

        for (int side = 0; side < 2; ++side)
        {
            mb.mirror = side == 1;
            foreach (var b in sides[side])
            {
                float h = ground + b.floors * floorH;
                float zc = (b.z0 + b.z1) * 0.5f, len = b.z1 - b.z0;
                // Gövde (arka ve alt yüzü görünmez; yine de kutu — az üçgen).
                mb.Box(back, face, 0f, h, b.z0, b.z1, b.wall, bottom: false);
                // Bina aralarında ince ayırıcı pilastr.
                mb.Box(face, face + 0.12f, 0f, h, b.z0, b.z0 + 0.25f, cornice, bottom: false);
                // Zemin kat: camlı dükkân (yazısız), üstünde tente.
                mb.FaceX(face + 0.02f, 0.25f, 3.25f, b.z0 + 0.55f, b.z1 - 0.4f, shop);
                mb.Box(face, face + 0.8f, 3.3f, 3.45f, b.z0 + 0.45f, b.z1 - 0.3f, b.awning);
                mb.Quad(new Vector3(face + 0.8f, 3.3f, b.z0 + 0.45f), new Vector3(face + 0.8f, 3.05f, b.z0 + 0.45f),
                        new Vector3(face + 0.8f, 3.05f, b.z1 - 0.3f), new Vector3(face + 0.8f, 3.3f, b.z1 - 0.3f), Vector3.right, b.awning * 0.85f);
                // Kat kornişleri ve çatı saçağı.
                for (int f = 0; f <= b.floors; ++f)
                {
                    float y = ground + f * floorH - 0.1f;
                    bool top = f == b.floors;
                    mb.Box(face, face + (top ? 0.4f : 0.16f), y - (top ? 0.25f : 0f), y + (top ? 0.45f : 0.2f), b.z0, b.z1, cornice);
                }
                // Pencereler: çerçeve + cam (yalnız sokağa bakan yüz).
                int windows = Mathf.Max(2, Mathf.FloorToInt(len / 1.75f));
                float step = len / windows;
                for (int f = 0; f < b.floors; ++f)
                {
                    float y = ground + f * floorH;
                    for (int w = 0; w < windows; ++w)
                    {
                        float wz = b.z0 + step * (w + 0.5f);
                        if (b.cumba && f >= 1 && Mathf.Abs(wz - zc) < 1.3f) continue; // cumbanın arkası
                        mb.FaceX(face + 0.01f, y + 0.75f, y + 2.6f, wz - 0.52f, wz + 0.52f, frame);
                        mb.FaceX(face + 0.025f, y + 0.85f, y + 2.5f, wz - 0.42f, wz + 0.42f, glass);
                    }
                }
                // Cumba: 2. kattan çatıya kadar çıkma, ön yüzünde pencereler.
                if (b.cumba && b.floors >= 2)
                {
                    float y0 = ground + floorH - 0.2f, y1 = h - 0.1f, d = face + 0.7f;
                    mb.Box(face, d, y0, y1, zc - 1.25f, zc + 1.25f, b.wall * 0.97f);
                    for (int f = 1; f < b.floors; ++f)
                    {
                        float y = ground + f * floorH;
                        mb.FaceX(d + 0.01f, y + 0.75f, y + 2.6f, zc - 0.95f, zc + 0.95f, frame);
                        mb.FaceX(d + 0.025f, y + 0.85f, y + 2.5f, zc - 0.85f, zc - 0.05f, glass);
                        mb.FaceX(d + 0.025f, y + 0.85f, y + 2.5f, zc + 0.05f, zc + 0.85f, glass);
                    }
                }
                // Balkonlar: 1. ve 3. katta ortadaki pencerelerin önünde; döşeme + koyu demir korkuluk.
                if (b.balconies)
                {
                    for (int f = 1; f < b.floors; f += 2)
                    {
                        float y = ground + f * floorH;
                        mb.Box(face, face + 0.6f, y + 0.6f, y + 0.75f, zc - 1.4f, zc + 1.4f, cornice);
                        mb.Box(face + 0.55f, face + 0.6f, y + 0.75f, y + 1.65f, zc - 1.4f, zc + 1.4f, rail);
                    }
                }
            }
        }

        // Sokak: açık renk taş döşeme ve ortada tek hatlı nostaljik tramvay rayları (yalnız görsel; çarpışma yok).
        // CurvedUnlit dünyayı köşe başına büker (y -= eğrilik x uzaklık²); 18 m'lik tek dörtgen ortasında eğriden ~40 cm
        // yukarıda düz kalır ve yol (köşeleri en fazla ~1 m arayla) uzakta döşemenin üstüne çıkar. Bu yüzden döşeme,
        // oluklar ve raylar ~1 m'lik parçalara bölünür (sapma ~1 mm; döşeme yolun 1,2 cm üstünde).
        mb.mirror = false;
        Color stone = Hex("A69E92"), joint = Hex("7D766C"), steel = Hex("9AA0A6"), groove = Hex("3A3A3C");
        const float py = 0.012f;
        int pieces = Mathf.CeilToInt((zEnd - zStart) / 1.0f);
        float pieceLen = (zEnd - zStart) / pieces;
        for (int i = 0; i < pieces; ++i)
        {
            float a = zStart + i * pieceLen, b = a + pieceLen;
            mb.Quad(new Vector3(-2.3f, py, a), new Vector3(2.3f, py, a), new Vector3(2.3f, py, b), new Vector3(-2.3f, py, b), Vector3.up, stone);
            foreach (float rx in new[] { -0.5f, 0.5f })
            {
                mb.Quad(new Vector3(rx - 0.09f, py + 0.004f, a), new Vector3(rx + 0.09f, py + 0.004f, a), new Vector3(rx + 0.09f, py + 0.004f, b), new Vector3(rx - 0.09f, py + 0.004f, b), Vector3.up, groove);
                // Ray: üst ve iki yan yüz (uçlar komşu parçayla birleşir, görünmez).
                float r0 = rx - 0.035f, r1 = rx + 0.035f, top = py + 0.03f;
                mb.Quad(new Vector3(r0, top, a), new Vector3(r1, top, a), new Vector3(r1, top, b), new Vector3(r0, top, b), Vector3.up, steel);
                mb.Quad(new Vector3(r1, py, a), new Vector3(r1, top, a), new Vector3(r1, top, b), new Vector3(r1, py, b), Vector3.right, steel);
                mb.Quad(new Vector3(r0, py, a), new Vector3(r0, top, a), new Vector3(r0, top, b), new Vector3(r0, py, b), Vector3.left, steel);
            }
        }
        for (float z = zStart + 0.75f; z < zEnd; z += 1.5f)
            mb.Quad(new Vector3(-2.3f, py + 0.002f, z - 0.03f), new Vector3(2.3f, py + 0.002f, z - 0.03f), new Vector3(2.3f, py + 0.002f, z + 0.03f), new Vector3(-2.3f, py + 0.002f, z + 0.03f), Vector3.up, joint);

        var mesh = new Mesh { name = "IstiklalSokak" };
        mesh.SetVertices(mb.v);
        mesh.SetColors(mb.c);
        mesh.SetTriangles(mb.t, 0);
        mesh.SetUVs(0, Enumerable.Repeat(Vector2.zero, mb.v.Count).ToList());
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Salt okunur döküm (-executeMethod IstanbulUygula.ParcaDok): Day temasındaki bir parçanın yapısı ve ölçüleri.
    public static void ParcaDok()
    {
        foreach (string name in new[] { "UrbanBuilding01", "UrbanPlasterWalls01" })
        {
            string path = AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p => p.Contains("/Default/"));
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var seg = go.GetComponent<TrackSegment>();
            Out($"== {path}");
            foreach (Transform p in seg.pathParent) Out($"  yol {p.name} {p.localPosition}");
            Out($"  engel noktaları=[{string.Join(",", seg.obstaclePositions)}] olası engel={seg.possibleObstacles.Length}: {string.Join(",", seg.possibleObstacles.Select(r => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(r.AssetGUID))))}");
            int total = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>(); if (r == null || mf.sharedMesh == null) continue;
                int t = 0; for (int i = 0; i < mf.sharedMesh.subMeshCount; ++i) t += (int)mf.sharedMesh.GetIndexCount(i) / 3;
                total += t;
                var b = r.bounds;
                Out($"  {AnimationUtility.CalculateTransformPath(mf.transform, go.transform)} mesh={mf.sharedMesh.name} ucgen={t} mat={string.Join("+", r.sharedMaterials.Select(m => m ? m.name + ":" + m.shader.name : "-"))} min={b.min:F2} max={b.max:F2} aktif={mf.gameObject.activeSelf}");
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Out($"  collider {c.name} {c.GetType().Name} katman={c.gameObject.layer} bounds={c.bounds.min:F2}..{c.bounds.max:F2}");
            Out($"  toplam ucgen={total}");
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void AddToAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var src = settings.FindGroup(k_SourceGroup);
        if (src == null) { Fail(k_SourceGroup + " grubu yok"); return; }
        var group = settings.FindGroup(k_Group) ?? settings.CreateGroup(k_Group, false, false, true, src.Schemas);
        string guid = AssetDatabase.AssetPathToGUID(k_Prefab);
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = Path.GetFileNameWithoutExtension(k_Prefab);
        EditorUtility.SetDirty(settings);
        Out($"Addressables: grup={group.Name} adres={entry.address} guid={guid}");
    }

    // ---------------------------------------------------------------------------------------------------------------
    // İstiklal tasarlanmış parçaları İ1–İ6 (batchmode: -executeMethod IstanbulUygula.IstiklalTasarim).
    // Her parça Istiklal01'in prefab varyantıdır (aynı 18 m geometri); köke TasarimliSatir eklenir: engeller ortada
    // (9. metre) sabit şeritlerde, altınlar düz/yay/çapraz desenle. Tekrar çalıştırılabilir (varyantlar yeniden yazılır).
    // Addressables grubu ve tema bu menüyle DEĞİŞMEZ (dosyalar 2.10 biçimine yeniden yazılmasın diye elle eklenir).
    // Görseller proje dışına yazılır: ../_araclar/istanbul/test/tasarim/.
    const string k_TasarimFolder = k_UretimFolder + "/Tasarim";
    const string k_TasarimGorsel = "../_araclar/istanbul/test/tasarim";
    const string k_Engeller = "Assets/UnityTechnologies/Endless Runner/Prefabs/Themes/Default/Obstacles/";
    const string k_Altin = "Assets/UnityTechnologies/Endless Runner/Prefabs/Pickup.prefab";
    const float k_SatirKonum = 9f;   // Istiklal01'in engel noktasıyla aynı (0,5 x 18 m)
    const int k_Sol = 1, k_Orta = 2, k_Sag = 4;

    class ParcaTasarim
    {
        public string ad, baslik;
        public (string engel, int seritler)[] satirlar;
        public TasarimliSatir.Altin[] altinlar;
    }

    static TasarimliSatir.Altin Duz(int serit, float bas, float bit) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Duz, serit = serit, bitisSeridi = serit, baslangic = bas, bitis = bit };
    static TasarimliSatir.Altin Yay(int serit, float bas, float bit, float tepe) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Yay, serit = serit, bitisSeridi = serit, baslangic = bas, bitis = bit, yukseklik = tepe };
    static TasarimliSatir.Altin Capraz(int bas, int bit, float m0, float m1) => new TasarimliSatir.Altin { tip = TasarimliSatir.AltinTipi.Capraz, serit = bas, bitisSeridi = bit, baslangic = m0, bitis = m1 };

    static readonly ParcaTasarim[] k_Parcalar =
    {
        new ParcaTasarim { ad = "IstiklalI1", baslik = "I1 Tek engel",
            satirlar = new[] { ("ObstacleBin", k_Orta) },
            altinlar = new[] { Duz(-1, 0f, 16.5f) } },
        new ParcaTasarim { ad = "IstiklalI2", baslik = "I2 Kaldirim kapaniyor",
            satirlar = new[] { ("ObstacleBin", k_Sol | k_Orta) },
            altinlar = new[] { Duz(1, 0f, 16.5f) } },
        new ParcaTasarim { ad = "IstiklalI3", baslik = "I3 Bariyer atla",
            satirlar = new[] { ("ObstacleLowBarrier", k_Orta) },
            altinlar = new[] { Duz(0, 0f, 3f), Yay(0, 4.5f, 13.5f, 1.6f), Duz(0, 15f, 16.5f) } },
        new ParcaTasarim { ad = "IstiklalI4", baslik = "I4 Kayarak gec",
            satirlar = new[] { ("ObstacleHighBarrier", k_Orta) },
            altinlar = new[] { Duz(0, 0f, 16.5f) } },
        new ParcaTasarim { ad = "IstiklalI5", baslik = "I5 Risk-odul",
            satirlar = new[] { ("ObstacleRoadworksCone", k_Sol), ("ObstacleWheelyBin", k_Sag) },
            altinlar = new[] { Yay(-1, 4.5f, 13.5f, 1.9f) } },
        new ParcaTasarim { ad = "IstiklalI6", baslik = "I6 Yol calismasi",
            satirlar = new[] { ("ObstacleRoadworksBarrier", k_Orta), ("ObstacleRoadworksCone", k_Sag) },
            altinlar = new[] { Capraz(1, -1, 0f, 6f), Duz(-1, 7.5f, 16.5f) } },
    };

    public static void IstiklalTasarim()
    {
        s_Errors = 0;
        if (!AssetDatabase.IsValidFolder(k_TasarimFolder)) AssetDatabase.CreateFolder(k_UretimFolder, "Tasarim");
        var temel = AssetDatabase.LoadAssetAtPath<GameObject>(k_UretimPrefab);
        if (temel == null) { Fail("Istiklal01 yok"); Bitir(); return; }
        foreach (var p in k_Parcalar)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(temel);
            inst.name = p.ad;
            var tas = inst.AddComponent<TasarimliSatir>();
            tas.satirlar = p.satirlar.Select(s =>
            {
                string guid = AssetDatabase.AssetPathToGUID(k_Engeller + s.engel + ".prefab");
                if (string.IsNullOrEmpty(guid)) Fail($"{p.ad}: engel yok {s.engel}");
                return new TasarimliSatir.Satir { konum = k_SatirKonum, engel = new UnityEngine.AddressableAssets.AssetReference(guid), seritler = s.seritler };
            }).ToArray();
            tas.altinlar = p.altinlar;
            string yol = $"{k_TasarimFolder}/{p.ad}.prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, yol, out bool ok);
            Object.DestroyImmediate(inst);
            if (!ok) { Fail($"kaydedilemedi {yol}"); continue; }
            Out($"parca: {p.ad} ({p.baslik}) guid={AssetDatabase.AssetPathToGUID(yol)} satir={string.Join(" + ", p.satirlar.Select(s => s.engel + "/" + s.seritler))} altin={string.Join(" + ", p.altinlar.Select(a => $"{a.tip}({a.serit}->{a.bitisSeridi} {a.baslangic}-{a.bitis}m h{a.yukseklik})"))}");
        }
        AssetDatabase.SaveAssets();
        IstiklalTasarimGorselUret();
        Bitir();
    }

    // Görseller (-executeMethod IstanbulUygula.IstiklalTasarimGorsel): kaydedilmiş varyantlardan, oyundaki gibi yerleşmiş
    // engel ve altınlarla: oyun kamerası (6 m arkada, 4 m yukarıda, 15 derece, 58,7 FOV, 390x844) ve tepeden görünüm.
    public static void IstiklalTasarimGorsel() { s_Errors = 0; IstiklalTasarimGorselUret(); Bitir(); }

    static void IstiklalTasarimGorselUret()
    {
        Directory.CreateDirectory(k_TasarimGorsel);
        var day = AssetDatabase.LoadAssetAtPath<ThemeData>(k_DayTheme);
        var temel = AssetDatabase.LoadAssetAtPath<GameObject>(k_UretimPrefab);
        var altin = AssetDatabase.LoadAssetAtPath<GameObject>(k_Altin);
        foreach (var p in k_Parcalar)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_TasarimFolder}/{p.ad}.prefab");
            if (prefab == null) { Fail($"görsel: {p.ad} yok"); continue; }
            // önceki ve sonraki parça düz Istiklal01 (engelsiz), ortadaki tasarlanmış parça
            for (int i = -1; i <= 1; ++i) ((GameObject)PrefabUtility.InstantiatePrefab(i == 0 ? prefab : temel)).transform.position = new Vector3(0f, 0f, i * 17.99f);
            var seg = prefab.GetComponent<TrackSegment>();
            var tas = prefab.GetComponent<TasarimliSatir>();
            var pts = Enumerable.Range(0, seg.pathParent.childCount).Select(i => seg.pathParent.GetChild(i).position).ToArray();
            float L = 0f; for (int i = 1; i < pts.Length; ++i) L += Vector3.Distance(pts[i - 1], pts[i]);
            Vector3 Nokta(float m) => Vector3.Lerp(pts[0], pts[pts.Length - 1], m / L); // düz yol: giriş-çıkış doğrusu
            foreach (var s in tas.satirlar)
                for (int lane = -1; lane <= 1; ++lane)
                    if ((s.seritler & (1 << (lane + 1))) != 0)
                        ((GameObject)PrefabUtility.InstantiatePrefab(s.engel.editorAsset)).transform.position = Nokta(s.konum) + Vector3.right * lane * 1.5f;
            int adet = 0;
            foreach (var a in tas.altinlar)
                for (float m = a.baslangic; m <= a.bitis + 0.001f; m += TasarimliSatir.k_AltinAraligi)
                {
                    float o = a.bitis > a.baslangic ? (m - a.baslangic) / (a.bitis - a.baslangic) : 0f;
                    float lane = a.tip == TasarimliSatir.AltinTipi.Capraz ? Mathf.Lerp(a.serit, a.bitisSeridi, o) : a.serit;
                    float y = a.tip == TasarimliSatir.AltinTipi.Yay ? Mathf.Sin(o * Mathf.PI) * a.yukseklik : a.yukseklik;
                    ((GameObject)PrefabUtility.InstantiatePrefab(altin)).transform.position = Nokta(m) + Vector3.right * lane * 1.5f + Vector3.up * y;
                    adet++;
                }
            Vector3 satir = Nokta(k_SatirKonum);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = day.fogColor;
            RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 70f;
            // oyun kamerası: karakter satırdan 12 m önce; kamera karakterin 6 m arkasında, 4 m yukarıda
            Shader.SetGlobalFloat("_CurveStrength", 0.005f);
            GorselCek($"{p.ad}-oyun", 390 * 2, 844 * 2, satir + new Vector3(0f, 4f, -18f), Quaternion.Euler(14.94f, 0f, 0f), 58.7f, 0f, day.fogColor);
            Shader.SetGlobalFloat("_CurveStrength", 0f);
            RenderSettings.fog = false;
            GorselCek($"{p.ad}-tepe", 600, 1200, satir + new Vector3(0f, 30f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f, 9.5f, day.fogColor);
            Out($"gorsel: {p.ad} engel={tas.satirlar.Sum(s => System.Convert.ToString(s.seritler, 2).Count(c => c == '1'))} altin={adet}");
        }
    }

    static void GorselCek(string ad, int w, int h, Vector3 pos, Quaternion rot, float fov, float ortho, Color arka)
    {
        var cam = new GameObject("TasarimKamera").AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        if (ortho > 0f) { cam.orthographic = true; cam.orthographicSize = ortho; } else cam.fieldOfView = fov;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = arka;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes($"{k_TasarimGorsel}/{ad}.png", tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(cam.gameObject);
    }
}
