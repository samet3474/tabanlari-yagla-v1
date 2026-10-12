// Aşama 1 (ad ve marka) sahne/proje ayarı değişikliklerini tekrarlanabilir şekilde uygular. Yalnızca editörde çalışır.
// Komut satırı: Unity.exe -batchmode -projectPath <proje> -executeMethod MarkaUygula.Uygula
// Nesneler silinmez, yalnızca gizlenir; eski görseller projede kalır (geri dönüş için sahnedeki bağlantı eski sprite'a çevrilebilir).
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MarkaUygula
{
    const string k_StartScene = "Assets/UnityTechnologies/Endless Runner/Scenes/Start.unity";
    const string k_MainScene = "Assets/UnityTechnologies/Endless Runner/Scenes/Main.unity";
    const string k_OldLogo = "Assets/UnityTechnologies/Endless Runner/UI/Logo.png";
    const string k_NewLogo = "Assets/_TabanlariYagla/Art/Brand/logo_tabanlari_yagla_1024.png";
    const string k_NewLogoMaster = "Assets/_TabanlariYagla/Art/Brand/logo_tabanlari_yagla.png";
    const string k_AppIcon = "Assets/_TabanlariYagla/Art/Brand/uygulama_simgesi_1024.png";

    const string k_AboutTitle = "HAKKINDA";
    // Hakkında metni Luckiest Guy ile çiziliyor: bu yazı tipi küçük "i"yi noktasız gösterdiği için metin doğru Türkçe
    // büyük harfle yazılır (İ/I ayrımı korunur).
    const string k_AboutBody =
        "TABANLARI YAĞLA\nSÜRÜM 1.0\n\n" +
        "İSTANBUL SOKAKLARINDA GEÇEN SONSUZ KOŞU OYUNU.\n\n" +
        "UNITY İLE GELİŞTİRİLDİ. UNITY TECHNOLOGIES'İN ENDLESS RUNNER ÖRNEK PROJESİ TEMEL ALINMIŞTIR.\n\n" +
        "YAZI TİPİ: LUCKIEST GUY (APACHE LİSANSI 2.0).";
    const string k_AboutBodyPreviousPrefix = "Tabanları Yağla\nSürüm"; // ilk uygulamadaki metin

    static int s_Errors;

    static void Out(string s) { Debug.Log("MARKA| " + s); }
    static void Fail(string s) { s_Errors++; Debug.LogError("MARKA| HATA: " + s); }

    [MenuItem("Tabanları Yağla/Marka (Aşama 1) Uygula")]
    public static void Uygula()
    {
        s_Errors = 0;
        ImportBrandTextures();
        if (s_Errors == 0) ApplyStartScene();
        if (s_Errors == 0) ApplyMainScene();
        if (s_Errors == 0) ApplyPlayerSettings();
        Out(s_Errors == 0 ? "TAMAM" : "DURDU, hata sayısı=" + s_Errors);
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    static void ImportBrandTextures()
    {
        foreach (string path in new[] { k_NewLogo, k_NewLogoMaster })
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Fail("içe aktarılamadı: " + path); return; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = path == k_NewLogo ? 1024 : 2048;
            ti.SaveAndReimport();
        }

        var icon = AssetImporter.GetAtPath(k_AppIcon) as TextureImporter;
        if (icon == null) { Fail("içe aktarılamadı: " + k_AppIcon); return; }
        icon.textureType = TextureImporterType.Default;
        icon.mipmapEnabled = false;
        icon.maxTextureSize = 1024;
        icon.SaveAndReimport();
        Out("görseller içe aktarıldı");
    }

    static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (var root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        return list;
    }

    static GameObject FindSingleByName(Scene scene, string name)
    {
        var found = FindInScene<Transform>(scene).Where(t => t.name == name).ToList();
        if (found.Count != 1) { Fail($"'{name}' nesnesinden {found.Count} adet bulundu (1 bekleniyordu)"); return null; }
        return found[0].gameObject;
    }

    static Text FindSingleText(Scene scene, System.Func<string, bool> match, string what)
    {
        var found = FindInScene<Text>(scene).Where(t => t.text != null && match(t.text)).ToList();
        if (found.Count != 1) { Fail($"{what}: {found.Count} metin bulundu (1 bekleniyordu)"); return null; }
        return found[0];
    }

    static void ApplyStartScene()
    {
        var scene = EditorSceneManager.OpenScene(k_StartScene, OpenSceneMode.Single);
        Sprite oldLogo = AssetDatabase.LoadAssetAtPath<Sprite>(k_OldLogo);
        Sprite newLogo = AssetDatabase.LoadAssetAtPath<Sprite>(k_NewLogo);
        if (newLogo == null) { Fail("yeni logo sprite olarak yüklenemedi"); return; }

        var logos = FindInScene<Image>(scene).Where(i => i.sprite == oldLogo || i.sprite == newLogo).ToList();
        if (logos.Count != 1) { Fail($"logo görseli: {logos.Count} adet (1 bekleniyordu)"); return; }
        logos[0].sprite = newLogo;
        logos[0].preserveAspect = true;
        EditorUtility.SetDirty(logos[0]);
        Out($"Start: '{logos[0].name}' yeni logoya bağlandı (en-boy oranı korunuyor)");

        foreach (string name in new[] { "UnityURLButton", "UnityURLBackground" })
        {
            var go = FindSingleByName(scene, name);
            if (go == null) return;
            go.SetActive(false);
            EditorUtility.SetDirty(go);
            Out($"Start: '{name}' gizlendi (silinmedi)");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) Fail("Start sahnesi kaydedilemedi");
    }

    static void ApplyMainScene()
    {
        var scene = EditorSceneManager.OpenScene(k_MainScene, OpenSceneMode.Single);

        // Sahnede başlık "About\n" olarak duruyor; sondaki satır sonu yerleşimin parçası olduğu için korunur.
        var title = FindSingleText(scene, t => t.Trim() == "About" || t.Trim() == k_AboutTitle, "Hakkında başlığı");
        var body = FindSingleText(scene, t => t.StartsWith("Trash Dash is a sample game", System.StringComparison.Ordinal)
            || t.StartsWith(k_AboutBodyPreviousPrefix, System.StringComparison.Ordinal) || t == k_AboutBody, "Hakkında metni");
        var charity = FindSingleText(scene, t => t.StartsWith("All purchases in this game", System.StringComparison.Ordinal), "GameChanger metni");
        var visitUnity = FindSingleByName(scene, "VisitUnityButton");
        var visitCharity = FindSingleByName(scene, "VisitGameChangerButton");
        if (title == null || body == null || charity == null || visitUnity == null || visitCharity == null) return;

        title.text = k_AboutTitle + (title.text.EndsWith("\n", System.StringComparison.Ordinal) ? "\n" : ""); EditorUtility.SetDirty(title);
        body.text = k_AboutBody;
        // Kutu eski 3 satırlık metne göre boyutlanmıştı; altındaki alan gizlenen GameChanger metninden boşaldı, metin oraya taşabilir.
        body.verticalOverflow = VerticalWrapMode.Overflow;
        body.alignment = TextAnchor.UpperCenter; // ortalıyken taşan metin yukarıya, başlığın üstüne de uzuyordu

        EditorUtility.SetDirty(body);
        Out($"Main: Hakkında başlığı ve metni Türkçe (yazı tipi: {(body.font != null ? body.font.name : "?")})");

        foreach (var go in new[] { charity.gameObject, visitUnity, visitCharity })
        {
            go.SetActive(false);
            EditorUtility.SetDirty(go);
            Out($"Main: '{go.name}' gizlendi (silinmedi)");
        }

        // Türkçe aksesuar adları ("FÖTR VE KRAVAT") hazırlık ekranındaki dar etikete sığmıyordu: yalnızca bu etikete Best Fit.
        var loadouts = FindInScene<LoadoutState>(scene);
        if (loadouts.Count != 1 || loadouts[0].accesoryNameDisplay == null) { Fail($"LoadoutState aksesuar etiketi bulunamadı ({loadouts.Count})"); return; }
        Text accessoryLabel = loadouts[0].accesoryNameDisplay;
        // Luckiest Guy'ın satır yüksekliği yüzünden iki satırlık adların ("FÖTR VE KRAVAT", "PARTİ ŞAPKASI") ikinci satırı
        // dikey kırpma ile kayboluyordu ve Best Fit bunu düzeltmedi (28 ve 22 üst sınırıyla denendi). Sabit 20 punto,
        // 0,85 satır aralığı ve dikey taşmaya izin: iki satır ~51 birim, kutu 64,5 birim.
        accessoryLabel.resizeTextForBestFit = false;
        accessoryLabel.fontSize = 20;
        accessoryLabel.lineSpacing = 0.85f;
        accessoryLabel.verticalOverflow = VerticalWrapMode.Overflow;
        EditorUtility.SetDirty(accessoryLabel);
        Out($"Main: aksesuar etiketi '{accessoryLabel.name}' {accessoryLabel.fontSize} punto, satır aralığı {accessoryLabel.lineSpacing}, dikey taşma {accessoryLabel.verticalOverflow}");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) Fail("Main sahnesi kaydedilemedi");
    }

    static void ApplyPlayerSettings()
    {
        // Ürün adı, şirket adı ve paket kimlikleri BİLEREK değiştirilmiyor (kayıt klasörleri bunlara bağlı).
        Out($"productName='{PlayerSettings.productName}' companyName='{PlayerSettings.companyName}' (değiştirilmedi)");

        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.show = false;
        AssetDatabase.SaveAssets();
        Out($"açılış ekranı: show={PlayerSettings.SplashScreen.show} showUnityLogo={PlayerSettings.SplashScreen.showUnityLogo} (okunan değer)");

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(k_AppIcon);
        if (icon == null) { Fail("uygulama simgesi yüklenemedi"); return; }
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
        var set = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        Out($"varsayılan uygulama simgesi: {(set.Length > 0 && set[0] == icon ? "ayarlandı" : "AYARLANAMADI")}");
    }
}
