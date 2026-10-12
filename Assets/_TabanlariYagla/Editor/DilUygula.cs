// Türkçe arayüz aşaması: sahne ve prefab'lardaki sabit etiketlere LocText bileşeni ekler (anahtar → Resources/Lang/tr.txt).
// Yalnızca editörde çalışır, tekrar çalıştırılabilir. Etiketlerin sahnedeki metni değiştirilmez; tablo eksikse o metin görünür.
// Komut satırı: Unity.exe -batchmode -projectPath <proje> -executeMethod DilUygula.Uygula
// Koddan yazılan etiketler (puan, ad, görev açıklaması, "ALINDI", OYNA düğmesi) burada YOK; onlar Loc ile kodda çözülüyor.
// Eğitim akışı ve GameState.cs bu aşamanın dışında (b-tutorial-test dalı); burada yalnızca eğitim yönergelerinin metinleri var.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DilUygula
{
    const string k_Root = "Assets/UnityTechnologies/Endless Runner/";

    // Yol: kök nesnenin ADI HARİÇ, kökten etikete kadar tam hiyerarşi yolu (birebir eşleşir, kısmi eşleşme yok).
    static readonly (string path, string key)[] k_Start =
    {
        ("StartButton/StartText", "ui.play"),
    };

    static readonly (string path, string key)[] k_Main =
    {
        // Hazırlık ekranı
        ("Loadout/StoreButton/Text", "ui.store"),
        ("Loadout/MissionButton/Text", "ui.missions"),
        ("Loadout/OpenLeaderboard/Text", "ui.leaderboard"),
        ("Loadout/SettingButton/Text", "ui.settings"),
        ("Loadout/TutorialOverlay/TutorialText/Text", "loadout.tutorialPrompt"),
        // Ayarlar, silme onayı, Hakkında
        ("Loadout/SettingPopup/Background/Title", "ui.settings"),
        ("Loadout/SettingPopup/Background/MasterSlider/Text", "settings.master"),
        ("Loadout/SettingPopup/Background/MusicSlider/Text", "settings.music"),
        ("Loadout/SettingPopup/Background/MasterSFXSlider/Text", "settings.sfx"),
        ("Loadout/SettingPopup/Background/DeleteData/Text", "settings.deleteData"),
        ("Loadout/SettingPopup/Background/About/Text", "settings.about"),
        ("Loadout/SettingPopup/ConfirmPopup/Image/TitleText", "settings.confirmTitle"),
        ("Loadout/SettingPopup/ConfirmPopup/Image/QueryText", "settings.deleteQuery"),
        ("Loadout/SettingPopup/ConfirmPopup/Image/YESButton/Text", "ui.yes"),
        ("Loadout/SettingPopup/ConfirmPopup/Image/NOButton/Text", "ui.no"),
        ("Loadout/SettingPopup/AboutPopup/Image/TitleText", "about.title"),
        ("Loadout/SettingPopup/AboutPopup/Background/AboutUnityText", "about.body"),
        ("Loadout/SettingPopup/AboutPopup/Image/BackButton/Text", "ui.back"),
        // Koşu ekranı
        ("Game/WholeUI/ScoreZone/ScoreLabel", "hud.score"),
        ("Game/PauseMenu/Text", "pause.title"),
        ("Game/PauseMenu/Resume/Resume", "ui.resume"),
        ("Game/PauseMenu/Exit/Text", "ui.mainMenu"),
        ("Game/DeathPopup/Text", "death.title"),
        ("Game/DeathPopup/GameOver/Text", "death.endRun"),
        ("Game/DeathPopup/ButtonLayout/Ad Button/Text", "death.watchVideo"),
        ("Game/DeathPopup/PremiumDisplay/CurrencyText", "death.balance"),
        // Eğitim yönergeleri (yalnızca metin)
        ("Game/Tutorials/UpTuto/Text", "tutorial.jump"),
        ("Game/Tutorials/SlideTuto/Text", "tutorial.lane"),
        ("Game/Tutorials/DownTuto/Text", "tutorial.slide"),
        ("Game/Tutorials/EndTutorial/Text", "tutorial.done"),
        ("Game/Tutorials/EndTutorial/Button/Text", "tutorial.toMenu"),
        // Oyun sonu
        ("GameOver/Text", "gameOver.title"),
        ("GameOver/RunButton/RunText", "ui.playAgain"),
        ("GameOver/Loadout/Text", "ui.mainMenu"),
        ("GameOver/StoreButton/Text", "ui.store"),
        ("GameOver/MissionsButton/Text", "ui.missions"),
        ("GameOver/OpenLeaderboard/Text", "ui.leaderboard"),
        ("GameOver/Highscore/PlayerEntry/InputField/Placeholder", "ui.enterName"),
        // Sıralama
        ("Leaderboard/Background/Text", "ui.leaderboard"),
        ("Leaderboard/Background/Buttons/Next/Text", "leaderboard.next"),
        ("Leaderboard/Background/Buttons/Prev/Text", "leaderboard.prev"),
        ("Leaderboard/Background/Buttons/InputField/Placeholder", "ui.enterName"),
    };

    static readonly (string path, string key)[] k_Shop =
    {
        ("Background/StoreTitle", "ui.store"),
        ("Background/TabsSwitch/Item/Text", "shop.tab.items"),
        ("Background/TabsSwitch/Character/Text", "shop.tab.characters"),
        ("Background/TabsSwitch/Accesories/Text", "shop.tab.accessories"),
        ("Background/TabsSwitch/Themes/Text", "shop.tab.themes"),
        // Premium mağaza penceresi (WebGL'de kapalı; açılırsa İngilizce görünmesin)
        ("Background/IAPPopup/Image/PremiumText", "shop.premiumTitle"),
        ("Background/IAPPopup/Image/Content/10_entry/Title", "ui.loading"),
        ("Background/IAPPopup/Image/Content/10_entry/Description", "ui.loading"),
        ("Background/IAPPopup/Image/Content/10_entry/Button/Text", "ui.loading"),
        ("Background/IAPPopup/Image/Content/50_entry/Title", "ui.loading"),
        ("Background/IAPPopup/Image/Content/50_entry/Description", "ui.loading"),
        ("Background/IAPPopup/Image/Content/50_entry/Button/Text", "ui.loading"),
        ("Background/IAPPopup/Image/Content/100_entry/Title", "ui.loading"),
        ("Background/IAPPopup/Image/Content/100_entry/Description", "ui.loading"),
        ("Background/IAPPopup/Image/Content/100_entry/Button/Text", "ui.loading"),
    };

    static readonly (string prefab, string path, string key)[] k_Prefabs =
    {
        ("Prefabs/UI/ItemEntry.prefab", "NamePriceButtonZone/PriceButtonZone/BuyButton/Text", "shop.buy"),
        ("Prefabs/UI/MissionEntry.prefab", "Image/ClaimButton/Text", "mission.claim"),
        ("Prefabs/UI/MissionPopup.prefab", "MissionBackground/Text", "ui.missions"),
        ("Prefabs/UI/AddMissionButton.prefab", "Text", "mission.new"),
        ("Prefabs/UI/AddMissionButton.prefab", "Button/Text", "mission.watchAd"),
    };

    static int s_Errors;
    static int s_Added;

    static void Out(string s) { Debug.Log("DIL| " + s); }
    static void Fail(string s) { s_Errors++; Debug.LogError("DIL| HATA: " + s); }

    [MenuItem("Tabanları Yağla/Türkçe Arayüz Uygula")]
    public static void Uygula()
    {
        s_Errors = 0;
        s_Added = 0;

        CheckKeys();
        if (s_Errors == 0) ImportNoItemSprite();
        if (s_Errors == 0) ApplyPrefabs();
        if (s_Errors == 0) ApplyScene("Scenes/Start.unity", k_Start, null);
        if (s_Errors == 0) ApplyScene("Scenes/Shop.unity", k_Shop, ShopLayout);
        if (s_Errors == 0) ApplyScene("Scenes/Main.unity", k_Main, MainLayout);

        Out(s_Errors == 0 ? $"TAMAM, yeni eklenen bileşen={s_Added}" : "DURDU, hata sayısı=" + s_Errors);
        if (Application.isBatchMode) EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
    }

    // Her anahtar tabloda olmalı: yoksa etiket sessizce İngilizce kalırdı.
    static void CheckKeys()
    {
        var keys = k_Start.Select(e => e.key).Concat(k_Main.Select(e => e.key)).Concat(k_Shop.Select(e => e.key))
            .Concat(k_Prefabs.Select(e => e.key)).Distinct();
        foreach (string key in keys)
            if (Loc.Get(key, null) == null) Fail("tabloda olmayan anahtar: " + key);
        if (s_Errors == 0) Out("tüm anahtarlar tabloda var");
    }

    static string RelativePath(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (Transform c = t; c != null && c != root; c = c.parent) parts.Insert(0, c.name);
        return string.Join("/", parts);
    }

    static Text FindText(IEnumerable<Transform> roots, string path, string where)
    {
        var found = new List<Text>();
        foreach (Transform root in roots)
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (RelativePath(t.transform, root) == path) found.Add(t);
        if (found.Count != 1) { Fail($"{where}: '{path}' için {found.Count} etiket bulundu (1 bekleniyordu)"); return null; }
        return found[0];
    }

    static bool Attach(Text label, string key, string where)
    {
        var loc = label.GetComponent<LocText>();
        if (loc == null) { loc = label.gameObject.AddComponent<LocText>(); s_Added++; }
        else if (loc.key == key) return false;
        loc.key = key;
        EditorUtility.SetDirty(loc);
        Out($"{where}: {RelativePath(label.transform, label.transform.root)} → {key} (yedek metin: \"{label.text.Replace("\n", "\\n")}\")");
        return true;
    }

    static void ApplyScene(string scenePath, (string path, string key)[] entries, System.Func<List<Transform>, bool> layout)
    {
        var scene = EditorSceneManager.OpenScene(k_Root + scenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects().Select(g => g.transform).ToList();
        string where = System.IO.Path.GetFileNameWithoutExtension(scenePath);

        // Önce hepsini bul; biri eksikse sahneye hiç dokunmadan dur.
        var labels = entries.Select(e => FindText(roots, e.path, where)).ToList();
        if (labels.Any(l => l == null)) return;

        bool changed = false;
        for (int i = 0; i < entries.Length; ++i)
            changed |= Attach(labels[i], entries[i].key, where);

        if (layout != null)
        {
            changed |= layout(roots);
            if (s_Errors > 0) return; // yerleşim hedeflerinden biri bulunamadı: sahneyi kaydetmeden dur
        }

        if (!changed) { Out($"{where}: değişiklik yok"); return; }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) Fail(where + " kaydedilemedi");
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Mobil yerleşim düzeltmeleri. Hepsi 390x844 (iPhone 13) ve 360x800 ekranlarda ÖLÇÜLEN taşmalara karşılık; yazı
    // genişlikleri Luckiest Guy ile ölçüldü. Ana sahne tuvali 720x1280 referanslı ve yüksekliğe göre ölçekleniyor, bu
    // yüzden uzun telefonlarda genişlik yalnızca ~576-591 birim (720 değil). Her değişiklik eski→yeni değeriyle yazılır.
    // ---------------------------------------------------------------------------------------------------------------

    const string k_NoItemSprite = "Assets/_TabanlariYagla/Art/UI/guclendirme_yok.png";

    static RectTransform FindRect(IEnumerable<Transform> roots, string path, string where)
    {
        var found = new List<Transform>();
        foreach (Transform root in roots)
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && RelativePath(t, root) == path) found.Add(t);
        if (found.Count != 1 || !(found[0] is RectTransform)) { Fail($"{where}: '{path}' için {found.Count} nesne bulundu (1 RectTransform bekleniyordu)"); return null; }
        return (RectTransform)found[0];
    }

    static bool FontSize(IEnumerable<Transform> roots, string path, int size)
    {
        Text t = FindText(roots, path, "yerleşim");
        if (t == null || t.fontSize == size) return false;
        Out($"yerleşim: {path} yazı boyutu {t.fontSize} → {size}");
        t.fontSize = size;
        EditorUtility.SetDirty(t);
        return true;
    }

    // Yatayda ekranın belirli bir oranına sabitler (dikey çapa ve konum aynı kalır).
    static bool AnchorX(IEnumerable<Transform> roots, string path, float fraction)
    {
        RectTransform r = FindRect(roots, path, "yerleşim");
        if (r == null || (Mathf.Approximately(r.anchorMin.x, fraction) && Mathf.Approximately(r.anchorMax.x, fraction) && Mathf.Approximately(r.anchoredPosition.x, 0f))) return false;
        Out($"yerleşim: {path} yatay çapa {r.anchorMin.x}-{r.anchorMax.x} x={r.anchoredPosition.x} → {fraction} x=0");
        r.anchorMin = new Vector2(fraction, r.anchorMin.y);
        r.anchorMax = new Vector2(fraction, r.anchorMax.y);
        r.anchoredPosition = new Vector2(0f, r.anchoredPosition.y);
        EditorUtility.SetDirty(r);
        return true;
    }

    static bool MainLayout(List<Transform> roots)
    {
        bool changed = false;

        // Hazırlık ekranı alt çubuğu: dış düğmeler ortadan ±250/260 birimdeydi, 591 birimlik ekranda SIRALAMA ve AYARLAR
        // kenardan taşıyordu. Dört düğme ekranın 1/8, 3/8, 5/8, 7/8 noktalarına; etiketler 34 → 28 punto
        // (SIRALAMA 155 → 128 birim). 576 birimde en yakın etiketler arası ≥ 23 birim kalıyor.
        string[] bar = { "Loadout/OpenLeaderboard", "Loadout/StoreButton", "Loadout/MissionButton", "Loadout/SettingButton" };
        for (int i = 0; i < bar.Length; ++i)
        {
            changed |= AnchorX(roots, bar[i], 0.125f + 0.25f * i);
            changed |= FontSize(roots, bar[i] + "/Text", 28);
        }

        // Ayarlar: ses etiketi kutuları 118 birim; "GENEL SES" (30 punto 134 birim) iki satıra bölünüp kırpılıyordu,
        // "EFEKTLER" sığmıyordu. 24 puntoda en uzunu 108 birim.
        foreach (string s in new[] { "MasterSlider", "MusicSlider", "MasterSFXSlider" })
            changed |= FontSize(roots, "Loadout/SettingPopup/Background/" + s + "/Text", 24);

        // Hazırlık ekranındaki eğitim daveti: kutu 799 birimdi (ekrandan geniş; eski İngilizce metin de kesiliyordu).
        // 560 birim genişlik ve 52 punto: "EĞİTİME BAŞLAMAK / İÇİN DOKUN" iki satır.
        RectTransform prompt = FindRect(roots, "Loadout/TutorialOverlay/TutorialText/Text", "yerleşim");
        if (prompt != null && !Mathf.Approximately(prompt.sizeDelta.x, 460f))
        {
            Out($"yerleşim: eğitim daveti genişlik farkı {prompt.sizeDelta.x} → 460 (toplam 560 birim)");
            prompt.sizeDelta = new Vector2(460f, prompt.sizeDelta.y);
            EditorUtility.SetDirty(prompt);
            changed = true;
        }
        changed |= FontSize(roots, "Loadout/TutorialOverlay/TutorialText/Text", 52);

        // Eğitim yönergeleri tam genişlikte; en uzun satır "ŞERİT DEĞİŞTİRMEK İÇİN" 64 puntoda 673 birim. 50 puntoda 526.
        foreach (string s in new[] { "UpTuto", "SlideTuto", "DownTuto" })
            changed |= FontSize(roots, "Game/Tutorials/" + s + "/Text", 50);

        // Oyun sonu: "OYUN BİTTİ" 128 puntoda 645 birim (iki yandan kesiliyordu) → 100 punto, 504 birim.
        changed |= FontSize(roots, "GameOver/Text", 100);
        // "TEKRAR OYNA" düğmesi ekran genişliği - 48 birim; 102 puntoda 636 birim (ikinci kelime kayboluyordu) → 80 punto, 499.
        changed |= FontSize(roots, "GameOver/RunButton/RunText", 80);

        // Oyun sonu skor tablosu 720 birim sabit genişlikteydi; sıra numaraları ve skorlar kenarlarda kesiliyordu.
        // Ekran genişliğine esnetilir (her yandan 60 birim pay); satır genişliğini zaten yerleşim grubu yönetiyor.
        RectTransform hs = FindRect(roots, "GameOver/Highscore", "yerleşim");
        if (hs != null && !(Mathf.Approximately(hs.anchorMin.x, 0f) && Mathf.Approximately(hs.anchorMax.x, 1f) && Mathf.Approximately(hs.sizeDelta.x, -120f)))
        {
            Out($"yerleşim: GameOver/Highscore yatay çapa {hs.anchorMin.x}-{hs.anchorMax.x} genişlik {hs.sizeDelta.x} → 0-1, -120");
            hs.anchorMin = new Vector2(0f, hs.anchorMin.y);
            hs.anchorMax = new Vector2(1f, hs.anchorMax.y);
            hs.sizeDelta = new Vector2(-120f, hs.sizeDelta.y);
            hs.anchoredPosition = new Vector2(0f, hs.anchoredPosition.y);
            EditorUtility.SetDirty(hs);
            changed = true;
        }

        // Tam sıralama: satırlar sabit genişlikte panelden taşıyordu ve sıra numarası panelin solunda kalıyordu.
        // Yerleşim grubu satır genişliğini yönetsin; soldan 20 birim pay sıra numarasına yer açar.
        RectTransform display = FindRect(roots, "Leaderboard/Background/Display", "yerleşim");
        var group = display != null ? display.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
        if (display != null && group == null) Fail("Leaderboard/Background/Display: yerleşim grubu yok");
        if (group != null && !(group.childControlWidth && group.childForceExpandWidth && group.padding.left == 20))
        {
            Out($"yerleşim: sıralama listesi genişlik yönetimi {group.childControlWidth}/{group.childForceExpandWidth} sol pay {group.padding.left} → True/True 20");
            group.childControlWidth = true;
            group.childForceExpandWidth = true;
            group.padding.left = 20;
            EditorUtility.SetDirty(group);
            changed = true;
        }

        changed |= ScoresNeverWrap(roots, "Main");

        // Güçlendirme seçilmediğinde gösterilen görselin içinde "NO ITEM" yazıyordu; aynı stilde Türkçe görsel.
        var loadouts = roots.SelectMany(r => r.GetComponentsInChildren<LoadoutState>(true)).ToList();
        Sprite noItem = AssetDatabase.LoadAssetAtPath<Sprite>(k_NoItemSprite);
        if (loadouts.Count != 1 || noItem == null) Fail($"LoadoutState ({loadouts.Count}) veya {k_NoItemSprite} bulunamadı");
        else if (loadouts[0].noItemIcon != noItem)
        {
            Out($"yerleşim: LoadoutState.noItemIcon '{(loadouts[0].noItemIcon != null ? loadouts[0].noItemIcon.name : "yok")}' → '{noItem.name}'");
            loadouts[0].noItemIcon = noItem;
            EditorUtility.SetDirty(loadouts[0]);
            changed = true;
        }

        return changed;
    }

    // Sıralama satırları ekrana sığacak kadar daralınca skor hücresi çok daralıyordu: kaydırma açıkken "4321" karakterden
    // bölünüp "432" görünüyordu; yalnızca taşmaya izin verilince de ortalanmış sayı ekranın sağ kenarına taşıyordu ("04").
    // Asıl neden oyun sonu satırlarında ad hücresinin 460 birimlik EN KÜÇÜK genişliğiydi (aşağıda 0 yapılıyor). Skor sayısı
    // bölünmez ve sağa hizalı: yalnızca sola büyür, satırın sağ kenarını geçmez.
    // k_ScoreMinWidth: önceki bir denemede verilen ve geri alınan değer (yalnızca tanıyıp geri almak için).
    const float k_ScoreMinWidth = 150f;

    static bool ScoresNeverWrap(IEnumerable<Transform> roots, string where)
    {
        bool changed = false;
        int count = 0;
        foreach (Transform root in roots)
            foreach (HighscoreUI hs in root.GetComponentsInChildren<HighscoreUI>(true))
            {
                if (hs.score == null) { Fail($"{where}: {RelativePath(hs.transform, root)} skor alanı boş"); continue; }
                var cell = hs.score.GetComponent<LayoutElement>();
                if (cell == null) { Fail($"{where}: {RelativePath(hs.score.transform, root)} LayoutElement yok"); continue; }
                count++;

                // Oyun sonu satırlarında ad hücresinin EN KÜÇÜK genişliği 460 birimdi (720'lik ekrana göre): dar ekranda
                // ad hücresi tek başına satırdan genişti ve skora hiç yer kalmıyordu. En küçük genişlik 0; tercih edilen
                // genişlik (300) aynı kalır, yerleşim grubu satır daraldıkça adı küçültür.
                foreach (GameObject nameCell in new[] { hs.inputName != null ? hs.inputName.gameObject : null, hs.playerName != null ? hs.playerName.gameObject : null })
                {
                    var nameLayout = nameCell != null ? nameCell.GetComponent<LayoutElement>() : null;
                    if (nameLayout == null || nameLayout.minWidth <= 0f) continue;
                    Out($"yerleşim: {where} {RelativePath(nameCell.transform, root)} ad hücresi en küçük genişlik {nameLayout.minWidth} → 0");
                    nameLayout.minWidth = 0f;
                    EditorUtility.SetDirty(nameLayout);
                    changed = true;
                }

                // Önceki bir denemede ad yazılarına Best Fit (28-boyut) açılmıştı: tek kelimelik ad küçülmek yerine
                // karakterden bölünüp kırpıldı ("OYU"). Sahnedeki tüm ad yazılarında Best Fit başlangıçta kapalıydı; geri alınır.
                // O denemenin izleri de silinir: prefab örneklerinde override'lar geri alınır, diğerlerinde sahnedeki
                // ilk değerler (Best Fit kapalı, 1-56) yazılır.
                foreach (Text nameText in new[] { hs.playerName, hs.inputName != null ? hs.inputName.textComponent : null })
                {
                    if (nameText == null) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(nameText))
                        changed |= RevertOverrides(nameText, "m_FontData.m_BestFit", "m_FontData.m_MinSize", "m_FontData.m_MaxSize");
                    else if (nameText.resizeTextForBestFit || nameText.resizeTextMinSize == 28)
                    {
                        Out($"yerleşim: {where} {RelativePath(nameText.transform, root)} ad yazısı Best Fit ilk hâline (kapalı, 1-56)");
                        nameText.resizeTextForBestFit = false;
                        nameText.resizeTextMinSize = 1;
                        nameText.resizeTextMaxSize = 56;
                        EditorUtility.SetDirty(nameText);
                        changed = true;
                    }
                }

                // Skor hücresine verilen en küçük 150 birim de geri alınır (tam sıralamada ad hücresini daraltıyordu);
                // hücre başlangıçtaki gibi yazının kendi genişliğini ve esnek payı kullanır.
                if (PrefabUtility.IsPartOfPrefabInstance(cell))
                    changed |= RevertOverrides(cell, "m_MinWidth");
                else if (Mathf.Approximately(cell.minWidth, k_ScoreMinWidth))
                {
                    cell.minWidth = -1f;
                    EditorUtility.SetDirty(cell);
                    changed = true;
                }

                if (hs.score.horizontalOverflow == HorizontalWrapMode.Overflow && hs.score.alignment == TextAnchor.MiddleRight) continue;
                hs.score.horizontalOverflow = HorizontalWrapMode.Overflow;
                hs.score.alignment = TextAnchor.MiddleRight;
                EditorUtility.SetDirty(hs.score);
                changed = true;
            }
        Out($"yerleşim: {where} sıralama satırı skoru: taşma, sağa hizalı ({count} satır){(changed ? "" : ", değişiklik yok")}");
        return changed;
    }

    // Salt okunur ölçüm: Main sahnesini açar, tuvali verilen genişliğe (yükseklik 1280) ayarlayıp yerleşimi hesaplatır ve
    // sıralama satırlarındaki hücre genişliklerini yazar. Sahne KAYDEDİLMEZ.
    // Komut satırı: -executeMethod DilUygula.SiralamaOlc
    public static void SiralamaOlc()
    {
        var scene = EditorSceneManager.OpenScene(k_Root + "Scenes/Main.unity", OpenSceneMode.Single);
        // Paneller sahnede kapalı; kapalı nesnelerde yerleşim hesaplanmaz. Bellekte açılır, gerçekçi metin yazılır.
        foreach (HighscoreUI hs in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HighscoreUI>(true)))
        {
            for (Transform t = hs.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            if (hs.playerName != null) hs.playerName.text = "Oyuncu";
            if (hs.inputName != null) hs.inputName.text = "Oyuncu";
            hs.score.text = "4321";
        }
        foreach (float width in new[] { 576f, 591f, 720f })
        {
            foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                var cr = (RectTransform)canvas.transform;
                cr.sizeDelta = new Vector2(width, 1280f);
                Canvas.ForceUpdateCanvases();
                for (int pass = 0; pass < 3; ++pass)
                    foreach (RectTransform r in canvas.GetComponentsInChildren<RectTransform>(false).Reverse())
                        LayoutRebuilder.ForceRebuildLayoutImmediate(r);
            }
            foreach (HighscoreUI hs in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HighscoreUI>(true)).Take(17))
            {
                string path = RelativePath(hs.transform, hs.transform.root);
                if (!path.EndsWith("/Score") && !path.EndsWith("PlayerEntry")) continue;
                var parts = new List<string>();
                foreach (RectTransform c in hs.transform)
                {
                    var t = c.GetComponent<Text>();
                    parts.Add($"{c.name}={c.rect.width:F0}" + (t != null ? $"(fs{t.fontSize} tercih={t.preferredWidth:F0})" : ""));
                }
                Out($"OLCUM genislik={width}: {path} satir={((RectTransform)hs.transform).rect.width:F0} | {string.Join(" ", parts)}");
            }
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // Prefab örneğindeki verilen alanların override'larını geri alır (prefab'daki değere döner).
    static bool RevertOverrides(Object target, params string[] properties)
    {
        bool changed = false;
        var so = new SerializedObject(target);
        foreach (string name in properties)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || !p.prefabOverride) continue;
            PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
            changed = true;
        }
        if (changed) Out($"yerleşim: {target.name} override geri alındı ({string.Join(", ", properties)})");
        return changed;
    }

    static bool ShopLayout(List<Transform> roots)
    {
        // Mağaza tuvali 720 birim genişlikte sabit. "MAĞAZA" 72 puntoda 272 birim ve ortada olduğu için sağdaki altın
        // kutusunun (463. birimden başlıyor) üstüne biniyordu. 64 punto (242 birim) ve 40 birim sola: 199-441.
        bool changed = FontSize(roots, "Background/StoreTitle", 64);
        RectTransform title = FindRect(roots, "Background/StoreTitle", "yerleşim");
        if (title != null && !Mathf.Approximately(title.anchoredPosition.x, -40f))
        {
            Out($"yerleşim: mağaza başlığı x {title.anchoredPosition.x} → -40");
            title.anchoredPosition = new Vector2(-40f, title.anchoredPosition.y);
            EditorUtility.SetDirty(title);
            changed = true;
        }
        return changed;
    }

    static void ImportNoItemSprite()
    {
        var ti = AssetImporter.GetAtPath(k_NoItemSprite) as TextureImporter;
        if (ti == null) { Fail("içe aktarılamadı: " + k_NoItemSprite + " (Tools/dil/guc_yok_uret.ps1 ile üretin)"); return; }
        if (ti.textureType == TextureImporterType.Sprite && ti.maxTextureSize == 256) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 256;
        ti.SaveAndReimport();
        Out("güçlendirme yok görseli sprite olarak içe aktarıldı");
    }

    static void ApplyPrefabs()
    {
        foreach (var group in k_Prefabs.GroupBy(e => e.prefab))
        {
            string assetPath = k_Root + group.Key;
            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var labels = group.Select(e => FindText(new[] { contents.transform }, e.path, group.Key)).ToList();
                if (labels.Any(l => l == null)) continue;

                bool changed = false;
                int i = 0;
                foreach (var e in group) changed |= Attach(labels[i++], e.key, group.Key);                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
