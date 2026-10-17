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
}
