// Ana menü prototipi: Main sahnesindeki Loadout ekranını yeni düzene getirir. Tekrar çalıştırılabilir (her seferinde aynı
// sonucu üretir); yalnızca editörde çalışır.
//   Uygula : Unity.exe -batchmode -projectPath <proje> -executeMethod MenuUygula.Uygula
//   Önizle : Unity.exe -batchmode -projectPath <proje> -executeMethod MenuUygula.Onizle -menuOnizleme <klasör> [-menuGorunumler <dosya>]
//            Menü kamerasından PNG (arayüz çizilmez; arayüz bölgeleri yarı saydam bantlarla işaretlenir).
//
// Yapılanlar:
//  1. Start sahnesinin animasyonlu sokak ortamı (MenuEnvironment, MenUAnimation) ve kamerası kopyalanıp
//     MenuBackdrop.prefab yapılır (bu adımda Start sahnesi kaydedilmez).
//  1b. Start sahnesi düğmesiz yükleme ekranı olur (ApplyStartScene): OYNA gizlenir, ana menü kendiliğinden açılır.
//  2. Main sahnesine MenuBackdrop yerleştirilir (pistten uzakta), karakter noktası ve menü kamerası LoadoutState'e
//     bağlanır. Oyun kamerası ve pist dokunulmadan kalır.
//  3. Loadout arayüzü yeniden yerleştirilir: güvenli alan kökü, üstte logo + altın + sıralama, ortada karakter adı ve
//     oklar, altta büyük OYNA ve Karakterler/Görevler/Mağaza/Ayarlar şeridi. Mevcut düğmeler ve bağlantıları korunur;
//     yalnızca "Karakterler" yeni (mağazanın karakter sekmesi). Nesne silinmez.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuUygula
{
    const string k_StartScene = "Assets/UnityTechnologies/Endless Runner/Scenes/Start.unity";
    const string k_MainScene = "Assets/UnityTechnologies/Endless Runner/Scenes/Main.unity";
    const string k_Folder = "Assets/_TabanlariYagla/Menu";
    const string k_BackdropPrefab = k_Folder + "/MenuBackdrop.prefab";
    const string k_Logo = "Assets/_TabanlariYagla/Art/Brand/logo_tabanlari_yagla_1024.png";
    const string k_SpriteSheet = "Assets/UnityTechnologies/Endless Runner/UI/UISpritesheet.png";
    const string k_PreviewCharacter = "Assets/UnityTechnologies/Endless Runner/Prefabs/Characters/Cat/character.prefab";
    const string k_PreviewIdle = "Assets/UnityTechnologies/Endless Runner/Art/Animations/Cat_Idle1.fbx";

    // Arka plan Main sahnesinde pistin ve eski menü noktasının çok uzağında durur (yalnızca menüdeyken açık).
    static readonly Vector3 k_BackdropPosition = new Vector3(0f, 0f, -1000f);

    // Menü kamerası ve karakter, Start ortamının kendi koordinatlarında. -menuKamera / -menuKarakter ile denenebilir.
    static Vector3 s_CameraPos = new Vector3(-0.93f, 1.3f, 6.34f);  // Start kamerasının yeri, biraz yukarıda
    static Vector3 s_CameraEuler = new Vector3(8f, -164f, 0f);       // aynı yön, hafif aşağı
    static float s_CameraFov = 45f;
    static Vector3 s_CharacterPos = new Vector3(-2.03f, 0f, 2.5f);  // kameranın 4 m önü: ekranda yüksekliğin ~%33-71i
    static float s_CharacterYaw = -18f; // kameraya bakış yönünden sapma (derece)

    // Arayüz (Canvas birimi; Loadout tuvali 720x1280'i yüksekliğe göre ölçekler: dikeyde hep 1280 birim, yatayda
    // telefona göre ~540 (21:9) ile 720 (16:9) arası). Konumlar güvenli alan köküne göre.
    const float k_Margin = 16f;
    const float k_LogoWidth = 250f, k_LogoHeight = 141f;        // logo 16:9
    const float k_CoinWidth = 184f, k_CoinHeight = 62f;
    const float k_RoundButton = 88f;
    const float k_NameTop = 204f, k_NameWidth = 440f, k_NameHeight = 72f;
    const float k_BarHeight = 136f;
    // Alt şerit yazıları: 22 boyutunda "KARAKTERLER" ~150, "GÖREVLER" ~108 birim; en dar ekranda (21:9, ~549 birim)
    // ağırlık 1.4 ile Karakterler ~156, diğerleri ~112 birim alır.
    const int k_BarLabelSize = 22;
    const float k_CharactersWeight = 1.4f;
    const float k_PlayBottom = 172f, k_PlayWidth = 440f, k_PlayHeight = 150f;
    const float k_SideBottom = k_PlayBottom + k_PlayHeight + 20f;  // yan seçicilerin alt kenarı (OYNA'nın üstü)

    static readonly Color k_PanelColor = new Color(0.07f, 0.09f, 0.19f, 0.78f);
    static readonly Color k_Gold = new Color(1.0f, 0.84f, 0.30f, 1.0f);

    static int s_Errors;
    static Color s_StartFogColor; static float s_StartFogStart, s_StartFogEnd;

    static void Out(string s) { Debug.Log("MENU| " + s); }
    static void Fail(string s) { s_Errors++; Debug.LogError("MENU| HATA: " + s); }

    [MenuItem("Tabanları Yağla/Ana Menü Prototipi Uygula")]
    public static void Uygula()
    {
        s_Errors = 0;
        ReadArgs();
        BuildBackdropPrefab();
        if (s_Errors == 0) ApplyMainScene();
        if (s_Errors == 0) ApplyStartScene();
        Out(s_Errors == 0 ? "TAMAM" : "DURDU, hata sayısı=" + s_Errors);
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ arka plan

    static void BuildBackdropPrefab()
    {
        Scene start = EditorSceneManager.OpenScene(k_StartScene, OpenSceneMode.Single);
        s_StartFogColor = RenderSettings.fogColor;
        s_StartFogStart = RenderSettings.fogStartDistance;
        s_StartFogEnd = RenderSettings.fogEndDistance;

        GameObject env = FindRoot(start, "MenuEnvironment"), anim = FindRoot(start, "MenUAnimation"), cam = FindRoot(start, "Main Camera");
        if (env == null || anim == null || cam == null) { Fail("Start sahnesinde ortam/kamera bulunamadı"); return; }

        var root = new GameObject("MenuBackdrop");
        Object.Instantiate(env, root.transform).name = "MenuEnvironment";
        GameObject animCopy = Object.Instantiate(anim, root.transform);
        animCopy.name = "MenUAnimation";
        // Start'ın geçen kedi/rakun oyuncuları menü karakterinin yanında ikinci bir karakter gibi görünür: çıkarılır.
        // Sokağın hareketli süsleri (MenuEnvironmentProps) kalır.
        foreach (Transform t in animCopy.GetComponentsInChildren<Transform>(true))
            if (t != null && t.parent == animCopy.transform && t.name.Contains("MenuActors"))
                Object.DestroyImmediate(t.gameObject);

        GameObject camCopy = Object.Instantiate(cam, root.transform);
        camCopy.name = "MenuCamera";
        camCopy.tag = "Untagged"; // Camera.main oyun kamerası kalmalı (TrackManager onu taşır)
        foreach (var l in camCopy.GetComponents<AudioListener>()) Object.DestroyImmediate(l);
        Camera c = camCopy.GetComponent<Camera>();
        c.enabled = false; // LoadoutState açar
        c.cullingMask = ~0;  // karakter UI katmanında
        c.fieldOfView = s_CameraFov;
        c.nearClipPlane = 0.1f;
        camCopy.transform.localPosition = s_CameraPos;
        camCopy.transform.localRotation = Quaternion.Euler(s_CameraEuler);
        var sky = GetOrAdd<Skybox>(camCopy);
        sky.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat"); // Start sahnesinin gökyüzü

        var spot = new GameObject("MenuCharacterSpot");
        spot.transform.SetParent(root.transform, false);
        spot.transform.localPosition = s_CharacterPos;
        Vector3 toCamera = s_CameraPos - s_CharacterPos; toCamera.y = 0f;
        spot.transform.localRotation = Quaternion.LookRotation(toCamera.normalized) * Quaternion.Euler(0f, s_CharacterYaw, 0f);

        if (!AssetDatabase.IsValidFolder(k_Folder)) Fail("klasör yok: " + k_Folder);
        PrefabUtility.SaveAsPrefabAsset(root, k_BackdropPrefab, out bool ok);
        Object.DestroyImmediate(root);
        if (!ok) Fail("prefab kaydedilemedi");
        else Out("MenuBackdrop.prefab kaydedildi (Start sahnesi kaydedilmedi)");
    }

    // ------------------------------------------------------------------ Start sahnesi: düğmesiz yükleme ekranı

    // Logo ve hareketli sokak aynen kalır; OYNA düğmesi görünmez ve tıklanamaz olur, yazısı "YÜKLENİYOR..." olur.
    // StartButton.autoStart: kayıt ve veritabanları hazır olunca ana menü kendiliğinden açılır. Nesne silinmez.
    static void ApplyStartScene()
    {
        Scene start = EditorSceneManager.OpenScene(k_StartScene, OpenSceneMode.Single);
        StartButton sb = Object.FindObjectsByType<StartButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (sb == null) { Fail("Start sahnesinde StartButton yok"); return; }

        sb.autoStart = true;
        EditorUtility.SetDirty(sb);
        var button = sb.GetComponent<Button>();
        if (button != null) { button.interactable = false; EditorUtility.SetDirty(button); }
        var image = sb.GetComponent<Image>();
        if (image != null) { image.enabled = false; EditorUtility.SetDirty(image); }

        Text label = sb.GetComponentInChildren<Text>(true);
        if (label == null) { Fail("Start düğmesinde yazı yok"); return; }
        Font font = label.font;
        label.gameObject.SetActive(false); // eski düğme yazısı; yerine çubuk, yüzde ve ipucu

        // Yükleme ekranı: üstte mevcut logo, arkada hareketli sokak; altta ipucu, dolan çubuk ve yüzde (güvenli alanda).
        Transform canvas = sb.GetComponentInParent<Canvas>().transform;
        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(k_SpriteSheet).OfType<Sprite>().ToDictionary(s => s.name);
        RectTransform safe = Child(canvas, "LoadingSafeArea");
        Stretch(safe);
        GetOrAdd<SafeAreaFitter>(safe.gameObject);

        RectTransform tip = Child(safe, "Tip");
        tip.anchorMin = new Vector2(0, 0); tip.anchorMax = new Vector2(1, 0); tip.pivot = new Vector2(0.5f, 0);
        tip.offsetMin = new Vector2(48f, 236f); tip.offsetMax = new Vector2(-48f, 236f + 120f);
        Text tipText = Label(Get<Text>(tip), font, "", 30, 30, TextAnchor.LowerCenter, Color.white);
        tipText.resizeTextForBestFit = false; // yazı tipi dinamik değil
        tipText.supportRichText = true;
        tipText.verticalOverflow = VerticalWrapMode.Overflow;
        Shadowed(tip.gameObject);

        RectTransform bar = Child(safe, "ProgressBar");
        bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
        bar.offsetMin = new Vector2(64f, 150f); bar.offsetMax = new Vector2(-64f, 150f + 66f);
        Panel(Get<Image>(bar), sprites["UIBorderFilled01"]);
        RectTransform fill = Child(bar, "Fill");
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.pivot = new Vector2(0.5f, 0.5f);
        fill.offsetMin = new Vector2(7f, 7f); fill.offsetMax = new Vector2(-7f, -7f);
        Image fillImage = Get<Image>(fill);
        fillImage.sprite = sprites["UISliderFill"];
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        fillImage.color = new Color(1.0f, 0.62f, 0.15f, 1f); // OYNA turuncusu
        fillImage.raycastTarget = false;
        RectTransform pct = Child(bar, "Percent");
        Stretch(pct);
        Text pctText = Label(Get<Text>(pct), font, "0%", 36, 36, TextAnchor.MiddleCenter, Color.white);
        pctText.resizeTextForBestFit = false;
        Shadowed(pct.gameObject);

        sb.progressFill = fillImage;
        sb.progressText = pctText;
        sb.tipText = tipText;
        sb.tipKeys = new[] { "tip.1", "tip.2", "tip.3", "tip.4", "tip.5" };

        // Sayfanın yükleme ekranıyla aynı (index.html #ty-loader): düz lacivert zemin, ortada logo; StartButton logoyu
        // yukarı taşır ve ipucu + çubuk grubunu görünür yapar.
        RectTransform bg = Child(canvas, "LoadingBackground");
        Stretch(bg);
        bg.SetAsFirstSibling();
        Image bgImage = Get<Image>(bg);
        bgImage.sprite = null;
        bgImage.color = new Color32(0x1B, 0x25, 0x50, 0xFF);
        bgImage.raycastTarget = false;
        EditorUtility.SetDirty(bgImage);

        RectTransform logo = canvas.Find("LogoImage") as RectTransform;
        if (logo == null) { Fail("Start sahnesinde LogoImage yok"); return; }
        logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(0.5f, 0.5f);
        logo.anchoredPosition = Vector2.zero;
        logo.sizeDelta = new Vector2(560f, 315f);
        logo.localScale = Vector3.one;
        var logoImage = logo.GetComponent<Image>();
        if (logoImage != null) { logoImage.preserveAspect = true; EditorUtility.SetDirty(logoImage); }
        EditorUtility.SetDirty(logo);

        sb.logo = logo;
        sb.loadingGroup = GetOrAdd<CanvasGroup>(safe.gameObject);
        sb.loadingGroup.alpha = 0f;
        sb.loadingGroup.interactable = false;
        sb.loadingGroup.blocksRaycasts = false;
        EditorUtility.SetDirty(sb.loadingGroup);
        EditorUtility.SetDirty(sb);

        EditorSceneManager.MarkSceneDirty(start);
        EditorSceneManager.SaveScene(start);
        Out("Start sahnesi: otomatik geçişli yükleme ekranı (düğme gizli; çubuk, yüzde, ipucu)");
    }

    // ------------------------------------------------------------------ Main sahnesi

    static void ApplyMainScene()
    {
        Scene main = EditorSceneManager.OpenScene(k_MainScene, OpenSceneMode.Single);
        LoadoutState loadout = Object.FindObjectsByType<LoadoutState>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (loadout == null) { Fail("LoadoutState yok"); return; }

        // Arka plan (her çalıştırmada yenilenir)
        foreach (var old in main.GetRootGameObjects().Where(g => g.name == "MenuBackdrop").ToList()) Object.DestroyImmediate(old);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_BackdropPrefab);
        var backdrop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, main);
        backdrop.transform.position = k_BackdropPosition;
        backdrop.SetActive(false);

        Camera gameCam = main.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(cam => cam != null && cam.CompareTag("MainCamera"));
        if (gameCam == null) { Fail("oyun kamerası yok"); return; }

        loadout.menuBackdrop = backdrop;
        loadout.menuCamera = backdrop.transform.Find("MenuCamera").GetComponent<Camera>();
        loadout.gameCamera = gameCam;
        loadout.gameCameraCurver = gameCam.GetComponent<WorldCurver>();
        loadout.charPosition = backdrop.transform.Find("MenuCharacterSpot");
        loadout.menuFogColor = s_StartFogColor;
        loadout.menuFogStart = s_StartFogStart;
        loadout.menuFogEnd = s_StartFogEnd;
        EditorUtility.SetDirty(loadout);

        LayoutLoadoutUI(loadout);

        if (s_Errors == 0)
        {
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
            Out("Main sahnesi kaydedildi");
        }
    }

    static void LayoutLoadoutUI(LoadoutState loadout)
    {
        Canvas canvas = loadout.inventoryCanvas;
        Transform cv = canvas.transform;
        Font font = loadout.runButton.GetComponentInChildren<Text>().font;
        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(k_SpriteSheet).OfType<Sprite>().ToDictionary(s => s.name);
        Sprite S(string n) { if (sprites.TryGetValue(n, out var s)) return s; Fail("sprite yok: " + n); return null; }

        var scaler = canvas.GetComponent<CanvasScaler>();
        Out($"Loadout tuvali: ölçek modu={scaler.uiScaleMode} referans={scaler.referenceResolution} eşleme={scaler.matchWidthOrHeight}");

        // Güvenli alan kökü: menü öğeleri bunun içinde; açılır pencereler tam ekran kalır (tuvalin kendisinde).
        RectTransform safe = Child(cv, "MenuSafeArea");
        Stretch(safe);
        if (safe.GetComponent<SafeAreaFitter>() == null) safe.gameObject.AddComponent<SafeAreaFitter>();
        safe.SetSiblingIndex(0);

        // ---- Üst: logo, altın, sıralama
        RectTransform logo = Child(safe, "MenuLogo");
        Place(logo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(k_Margin, -10f), new Vector2(k_LogoWidth, k_LogoHeight));
        Image logoImage = Get<Image>(logo);
        logoImage.sprite = AssetDatabase.LoadAllAssetsAtPath(k_Logo).OfType<Sprite>().FirstOrDefault();
        if (logoImage.sprite == null) Fail("logo sprite değil: " + k_Logo);
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;

        RectTransform coin = Child(safe, "MenuCoins");
        Place(coin, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-k_Margin, -26f), new Vector2(k_CoinWidth, k_CoinHeight));
        Panel(Get<Image>(coin), S("UIBorderFilled01"));
        RectTransform coinIcon = Child(coin, "Icon");
        Place(coinIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8f, 0f), new Vector2(50f, 50f));
        Icon(Get<Image>(coinIcon), S("FishboneIcone"));
        RectTransform coinText = Child(coin, "Count");
        coinText.anchorMin = Vector2.zero; coinText.anchorMax = Vector2.one; coinText.pivot = new Vector2(0.5f, 0.5f);
        coinText.offsetMin = new Vector2(62f, 4f); coinText.offsetMax = new Vector2(-14f, -4f);
        Text ct = Label(Get<Text>(coinText), font, "0", 40, 18, TextAnchor.MiddleRight, Color.white);
        if (coinText.GetComponent<MenuCoinCounter>() == null) coinText.gameObject.AddComponent<MenuCoinCounter>();
        Shadowed(ct.gameObject);

        RectTransform leaderboard = Move(cv, "OpenLeaderboard", safe);
        Place(leaderboard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-k_Margin, -102f), new Vector2(k_RoundButton, k_RoundButton));
        IconButton(leaderboard, S("UIBorderFilled01"), S("LeaderboardIcon"), font, false);

        // ---- Orta: karakter adı ve karakter okları (karakter 3D, menü kamerasıyla)
        RectTransform charZone = Move(cv, "CharZone", safe);
        Place(charZone, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, -k_NameTop), new Vector2(k_NameWidth, k_NameHeight));
        RectTransform charName = (RectTransform)loadout.charNameDisplay.transform;
        Stretch(charName);
        loadout.charNameDisplay.fontSize = 48;
        loadout.charNameDisplay.resizeTextForBestFit = true;
        loadout.charNameDisplay.resizeTextMinSize = 24;
        loadout.charNameDisplay.resizeTextMaxSize = 48;
        loadout.charNameDisplay.alignment = TextAnchor.MiddleCenter;

        RectTransform charSelect = loadout.charSelect;
        charSelect.SetParent(safe, false);
        charSelect.anchorMin = new Vector2(0, 0.5f); charSelect.anchorMax = new Vector2(1, 0.5f); charSelect.pivot = new Vector2(0.5f, 0.5f);
        charSelect.anchoredPosition = new Vector2(0f, 70f); charSelect.sizeDelta = new Vector2(-2f * k_Margin, 120f);
        foreach (Transform b in charSelect)
        {
            var rt = (RectTransform)b;
            bool left = b.name.Contains("Left");
            Vector2 size = rt.sizeDelta;
            Place(rt, new Vector2(left ? 0 : 1, 0.5f), new Vector2(left ? 0 : 1, 0.5f), Vector2.zero, size);
        }

        // ---- Yan seçiciler (yalnızca sahip olunanlar varsa görünür): sol altta tema ve güçlendirme, sağda aksesuar
        RectTransform theme = Move(cv, "ThemeZone", safe);
        Place(theme, new Vector2(0, 0), new Vector2(0, 0), new Vector2(k_Margin, k_SideBottom), new Vector2(720f, 172f));
        theme.localScale = Vector3.one * 0.42f;
        RectTransform powerup = Move(cv, "PowerupZone", safe);
        Place(powerup, new Vector2(0, 0), new Vector2(0, 0), new Vector2(k_Margin, k_SideBottom + 172f * 0.42f + 16f), powerup.sizeDelta);
        RectTransform accessories = (RectTransform)loadout.accessoriesSelector.transform;
        accessories.SetParent(safe, false);
        Place(accessories, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-k_Margin, k_SideBottom), accessories.sizeDelta);
        accessories.localScale = Vector3.one * 0.55f;

        // ---- Alt-orta: OYNA
        RectTransform play = (RectTransform)loadout.runButton.transform;
        play.SetParent(safe, false);
        Place(play, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, k_PlayBottom), new Vector2(k_PlayWidth, k_PlayHeight));

        // ---- En alt: Karakterler, Görevler, Mağaza, Ayarlar
        RectTransform bar = Child(safe, "MenuBottomBar");
        bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
        bar.offsetMin = new Vector2(k_Margin * 0.75f, 12f); bar.offsetMax = new Vector2(-k_Margin * 0.75f, 12f + k_BarHeight);
        Panel(Get<Image>(bar), S("UIBorderFilled01"));
        var layout = Get<HorizontalLayoutGroup>(bar);
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        RectTransform store = Move(cv, "StoreButton", bar);
        RectTransform characters = bar.Find("CharactersButton") as RectTransform;
        if (characters == null)
        {
            characters = (RectTransform)Object.Instantiate(store.gameObject, bar).transform;
            characters.name = "CharactersButton";
        }
        RewireCharactersButton(characters.GetComponent<Button>(), loadout);
        var charLabel = characters.GetComponentInChildren<Text>();
        charLabel.text = "KARAKTERLER";
        var charLoc = GetOrAdd<LocText>(charLabel.gameObject);
        charLoc.key = "ui.characters";

        RectTransform missions = Move(cv, "MissionButton", bar);
        RectTransform settings = Move(cv, "SettingButton", bar);
        characters.SetSiblingIndex(0); missions.SetSiblingIndex(1); store.SetSiblingIndex(2); settings.SetSiblingIndex(3);
        IconButton(characters, null, S("CatIcone"), font, true);
        foreach (RectTransform b in new[] { characters, missions, store, settings })
            GetOrAdd<LayoutElement>(b.gameObject).flexibleWidth = b == characters ? k_CharactersWeight : 1f;
        IconButton(missions, null, S("MissionsIcon"), font, true);
        IconButton(store, null, S("StoreIcon"), font, true);
        IconButton(settings, null, S("SettingIcone"), font, true);

        // ---- Eğitim perdesi: OYNA dışını karartır (güvenli alan dışına taşarak tüm ekranı kaplar)
        RectTransform blocker = (RectTransform)loadout.tutorialBlocker.transform;
        blocker.SetParent(safe, false);
        Stretch(blocker);
        blocker.SetAsLastSibling();
        foreach (Transform t in blocker)
        {
            var rt = (RectTransform)t;
            if (t.name == "BlockerBottom")
            {
                rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
                rt.offsetMin = new Vector2(-400f, -600f); rt.offsetMax = new Vector2(400f, k_PlayBottom);
            }
            else if (t.name == "BlockerTop")
            {
                rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(-400f, k_PlayBottom + k_PlayHeight); rt.offsetMax = new Vector2(400f, 600f);
            }
            else if (t.name == "TutorialText")
            {
                // Çocuklar eski yerleşime göre büyük negatif konumlarda ve ok animasyonu konumunu mutlak değerlerle
                // sürüyor (TutorialArrow.anim): çocuklara dokunulmaz, yalnızca bu kutu taşınır. Yazı (alt kenara göre
                // -519.5, genişlik = kutu + 460) OYNA'nın altında ~85'e, ok (merkeze göre -449) OYNA'nın alt kenarına gelir.
                const float textOffset = 519.5f, arrowOffset = 449f, textCenter = 85f, arrowCenter = k_PlayBottom + 13f;
                float boxBottom = textCenter + textOffset;
                float boxHeight = 2f * (arrowCenter + arrowOffset - boxBottom);
                Place(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, boxBottom), new Vector2(100f, boxHeight));
            }
        }

        // Açılır pencereler tuvalin üstünde kalır
        loadout.missionPopup.transform.SetAsLastSibling();
        var settingPopup = cv.Find("SettingPopup");
        if (settingPopup != null)
        {
            settingPopup.SetAsLastSibling();
            // 720 genişliğe göre çizilmiş pencereler dar ekranda kendini küçültür.
            foreach (string path in new[] { "Background", "ConfirmPopup/Image", "AboutPopup/Image", "AboutPopup/Background" })
            {
                Transform panel = settingPopup.Find(path);
                if (panel == null) { Fail("ayarlar paneli yok: " + path); continue; }
                GetOrAdd<FitInsideParent>(panel.gameObject);
            }
        }

        Out("Loadout arayüzü yerleştirildi");
    }

    static void RewireCharactersButton(Button b, LoadoutState loadout)
    {
        // Kopyalanan mağaza düğmesinin kalıcı bağlantıları temizlenir: karakter sekmesi + aynı tık sesi.
        AudioSource click = b.GetComponent<AudioSource>();
        while (b.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(b.onClick, 0);
        UnityEventTools.AddVoidPersistentListener(b.onClick, loadout.GoToCharacterStore);
        if (click != null) UnityEventTools.AddVoidPersistentListener(b.onClick, click.Play);
        foreach (var ll in b.GetComponents<LevelLoader>()) Object.DestroyImmediate(ll);
    }

    // Simge üstte, yazı altta; düğmenin kendi görseli yalnızca dokunma alanı (görünmez) ya da yuvarlak panel.
    static void IconButton(RectTransform button, Sprite background, Sprite icon, Font font, bool withLabel)
    {
        var bg = button.GetComponent<Image>();
        if (background != null) Panel(bg, background);
        else { bg.sprite = null; bg.color = new Color(1, 1, 1, 0); bg.raycastTarget = true; }

        RectTransform iconRt = Child(button, "Icon");
        if (withLabel) Place(iconRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, -4f), new Vector2(72f, 72f));
        else Place(iconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
        var iconImage = Get<Image>(iconRt);
        Icon(iconImage, icon);
        iconRt.SetAsFirstSibling();
        var btn = button.GetComponent<Button>();
        btn.targetGraphic = iconImage;

        foreach (var label in button.GetComponentsInChildren<Text>(true))
        {
            label.gameObject.SetActive(withLabel);
            if (!withLabel) continue;
            var rt = (RectTransform)label.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(2f, 2f); rt.offsetMax = new Vector2(-2f, 38f);
            Label(label, font, label.text, k_BarLabelSize, k_BarLabelSize, TextAnchor.MiddleCenter, Color.white);
            // Sabit boyut: Unity'nin en uygun boyutu tek uzun kelimeyi ("KARAKTERLER") harf harf bölüp ilk satırını
            // gösteriyordu. Taşma yatayda serbest; genişlikler (k_CharactersWeight) en dar ekranda da sığacak biçimde.
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Shadowed(label.gameObject);
        }
        // Eski görselin gölge/çerçeve çocukları gizlenir (silinmez).
        foreach (Transform t in button)
            if (t != iconRt && t.GetComponent<Text>() == null && t.GetComponentInChildren<Text>(true) == null)
                t.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ önizleme

    public static void Onizle()
    {
        s_Errors = 0;
        ReadArgs();
        string dir = Arg("-menuOnizleme") ?? "MenuOnizleme";
        Directory.CreateDirectory(dir);

        // Görünümler dosyası: her satır "ad px py pz ex ey ez fov cx cy cz yaw"; yoksa sahnedeki ayar.
        var views = new List<(string, Vector3, Vector3, float, Vector3, float)>();
        string viewsFile = Arg("-menuGorunumler");
        if (viewsFile != null)
            foreach (var line in File.ReadAllLines(viewsFile))
            {
                var p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 12 || p[0].StartsWith("#")) continue;
                float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
                views.Add((p[0], new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), F(7), new Vector3(F(8), F(9), F(10)), F(11)));
            }
        else
            views.Add(("sahne", s_CameraPos, s_CameraEuler, s_CameraFov, s_CharacterPos, s_CharacterYaw));

        // Start sahnesinin kendi ortamı ve ışığıyla değil, Main sahnesine kopyalanmış hâliyle (oyunda görüneceği gibi).
        Scene main = EditorSceneManager.OpenScene(k_MainScene, OpenSceneMode.Single);
        var backdrop = main.GetRootGameObjects().FirstOrDefault(g => g.name == "MenuBackdrop");
        if (backdrop == null) { Fail("önce Uygula çalıştırılmalı"); Finish(); return; }
        backdrop.SetActive(true);
        LoadoutState loadout = Object.FindObjectsByType<LoadoutState>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        RenderSettings.fogColor = loadout.menuFogColor;
        RenderSettings.fogStartDistance = loadout.menuFogStart;
        RenderSettings.fogEndDistance = loadout.menuFogEnd;
        Shader.SetGlobalFloat("_CurveStrength", 0f);
        Camera cam = backdrop.transform.Find("MenuCamera").GetComponent<Camera>();
        cam.enabled = true;
        loadout.gameCamera.enabled = false;
        Transform spot = backdrop.transform.Find("MenuCharacterSpot");

        GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_PreviewCharacter));
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(k_PreviewIdle).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

        // Telefon profilleri (genişlik x yükseklik, piksel): dar 16:9, iPhone 13 19.5:9, uzun 20:9, çok uzun 21:9
        var profiles = new[] { ("16x9", 540, 960), ("iphone13", 585, 1266), ("20x9", 540, 1200), ("21x9", 540, 1260) };
        foreach (var v in views)
        {
            cam.transform.localPosition = v.Item2;
            cam.transform.localRotation = Quaternion.Euler(v.Item3);
            cam.fieldOfView = v.Item4;
            spot.localPosition = v.Item5;
            Vector3 toCamera = v.Item2 - v.Item5; toCamera.y = 0f;
            spot.localRotation = Quaternion.LookRotation(toCamera.normalized) * Quaternion.Euler(0f, v.Item6, 0f);
            character.transform.SetParent(spot, false);
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            if (idle != null) idle.SampleAnimation(character.GetComponentInChildren<Animator>() != null ? character.GetComponentInChildren<Animator>().gameObject : character, 0.4f);

            foreach (var (name, w, h) in profiles)
            {
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.aspect = (float)w / h;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                MarkUiBands(tex);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"{v.Item1}_{name}.png"), tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                rt.Release();
                Out($"önizleme {v.Item1}_{name}");
            }
        }
        Finish();
    }

    // Arayüzün kaplayacağı bölgeler (güvenli alan payı olmadan): üst şerit, OYNA ve alt şerit yarı saydam kırmızı.
    static void MarkUiBands(Texture2D tex)
    {
        float unit = tex.height / 1280f;
        void Band(float fromBottom, float toBottom)
        {
            int y0 = Mathf.Clamp((int)(fromBottom * unit), 0, tex.height), y1 = Mathf.Clamp((int)(toBottom * unit), 0, tex.height);
            for (int y = y0; y < y1; ++y)
                for (int x = 0; x < tex.width; ++x)
                    tex.SetPixel(x, y, Color.Lerp(tex.GetPixel(x, y), Color.red, 0.25f));
        }
        Band(1280f - k_NameTop - k_NameHeight, 1280f);           // logo, altın, karakter adı
        Band(k_PlayBottom, k_PlayBottom + k_PlayHeight);           // OYNA
        Band(0f, 12f + k_BarHeight);                               // alt şerit
    }

    static void Finish()
    {
        Out(s_Errors == 0 ? "TAMAM" : "DURDU, hata sayısı=" + s_Errors);
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ yardımcılar

    static void ReadArgs()
    {
        float[] Nums(string a) => Arg(a)?.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var cam = Nums("-menuKamera");      // px,py,pz,ex,ey,ez,fov
        if (cam != null && cam.Length == 7) { s_CameraPos = new Vector3(cam[0], cam[1], cam[2]); s_CameraEuler = new Vector3(cam[3], cam[4], cam[5]); s_CameraFov = cam[6]; }
        var chr = Nums("-menuKarakter");    // x,y,z,yaw
        if (chr != null && chr.Length == 4) { s_CharacterPos = new Vector3(chr[0], chr[1], chr[2]); s_CharacterYaw = chr[3]; }
    }

    static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static GameObject FindRoot(Scene s, string name) => s.GetRootGameObjects().FirstOrDefault(g => g.name == name);

    static RectTransform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        return (RectTransform)t;
    }

    // Tuvalde ya da daha önce taşındığı yerde arar, hedefe taşır (aynı nesne: bağlantılar korunur).
    static RectTransform Move(Transform canvas, string name, Transform target)
    {
        Transform t = canvas.Find(name);
        if (t == null) t = target.Find(name);
        if (t == null) t = canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name && x.parent != null && x.parent.name.StartsWith("Menu"));
        if (t == null) { Fail("bulunamadı: " + name); return Child(target, name + "_YOK"); }
        t.SetParent(target, false);
        return (RectTransform)t;
    }

    // Unity nesneleri "??" ile denetlenemez (yok edilmiş/eksik bileşen gerçek null değildir): açık karşılaştırma.
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static T Get<T>(Component c) where T : Component => GetOrAdd<T>(c.gameObject);

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size; rt.localScale = Vector3.one;
    }

    static void Panel(Image img, Sprite sprite)
    {
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = k_PanelColor;
        img.raycastTarget = img.GetComponent<Button>() != null;
    }

    static void Icon(Image img, Sprite sprite)
    {
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    static Text Label(Text t, Font font, string text, int size, int min, TextAnchor align, Color color)
    {
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = min;
        t.resizeTextMaxSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    static void Shadowed(GameObject go)
    {
        var s = GetOrAdd<Shadow>(go);
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        s.effectDistance = new Vector2(2f, -3f);
    }
}
