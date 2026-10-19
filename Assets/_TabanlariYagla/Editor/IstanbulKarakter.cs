// Meshy ile üretilen İstanbul karakterleri (Samet, Berivan) oyuna eklenir:
//   -executeMethod IstanbulKarakter.Kur
// Kaynak: ../_araclar/parkur/meshy/<ad>/<Ad>.fbx (iskeletli model + 7 hareket: koşu, zıplama, kayma, düşme, bekleme,
// çarpma, dans). Kedinin prefabı şablon alınır (ses, Character, AudioSource aynı kalır); görünüm ve iskelet yeni modelden,
// animasyonlar CharacterAnimation denetleyicisinin üstüne AnimatorOverrideController ile. Mağazada 1 altın (test fiyatı).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public static class IstanbulKarakter
{
    const string k_Kok = "Assets/_TabanlariYagla/Karakterler";
    const string k_Kaynak = "../_araclar/parkur/meshy";
    const string k_Kedi = "Assets/UnityTechnologies/Endless Runner/Prefabs/Characters/Cat/character.prefab";
    const string k_Denetleyici = "Assets/UnityTechnologies/Endless Runner/Art/Animations/Animators/CharacterAnimation.controller";
    const int k_Fiyat = 1;
    // Meshy hareket sırası (meshy_animate action_ids): koşu, zıplama, kayma, düşme, bekleme, çarpma, dans
    static readonly string[] k_Hareket = { "Kosu", "Ziplama", "Kayma", "Dusme", "Bekleme", "Carpma", "Dans" };

    static void Out(string s) => Debug.Log("KARAKTER| " + s);

    public static void Kur()
    {
        int hata = 0;
        try
        {
            if (!AssetDatabase.IsValidFolder(k_Kok)) AssetDatabase.CreateFolder("Assets/_TabanlariYagla", "Karakterler");
            float kediBoy = Boy(AssetDatabase.LoadAssetAtPath<GameObject>(k_Kedi));
            Out($"kedi boyu {kediBoy:F2} m");
            foreach (var ad in new[] { "Samet", "Berivan" })
                if (!Ekle(ad, kediBoy)) hata++;
        }
        catch (System.Exception e) { Debug.LogException(e); hata++; }
        AssetDatabase.SaveAssets();
        Out(hata == 0 ? "bitti" : $"HATA {hata}");
        if (Application.isBatchMode) EditorApplication.Exit(hata == 0 ? 0 : 1);
    }

    static bool Ekle(string ad, float kediBoy)
    {
        string klasor = $"{k_Kok}/{ad}";
        if (!AssetDatabase.IsValidFolder(klasor)) AssetDatabase.CreateFolder(k_Kok, ad);
        string kaynak = Path.Combine(k_Kaynak, ad.ToLowerInvariant(), ad + ".fbx");
        if (!File.Exists(kaynak)) { Out("kaynak yok " + kaynak); return false; }
        string fbx = $"{klasor}/{ad}.fbx";
        File.Copy(kaynak, fbx, true); AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);

        // içe aktarma: Generic iskelet, kemikler açık (süper ayakkabı ayağı izler), gömülü doku dışarı
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.ExtractTextures(klasor);
        AssetDatabase.Refresh();
        mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.optimizeGameObjects = false;
        mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        string kalca = mi.transformPaths.FirstOrDefault(p => p.EndsWith("/Hips") || p == "Hips");
        if (kalca != null) mi.motionNodeName = kalca;
        var klipler = mi.defaultClipAnimations;
        Out($"{ad}: {klipler.Length} klip [{string.Join(", ", klipler.Select(k => k.name))}] kalca={kalca}");
        if (klipler.Length < k_Hareket.Length) { Out($"{ad}: eksik klip"); return false; }
        for (int i = 0; i < klipler.Length; ++i)
        {
            var k = klipler[i];
            if (i < k_Hareket.Length) k.name = k_Hareket[i];
            bool dongu = k.name == "Kosu" || k.name == "Bekleme" || k.name == "Dans";
            k.loopTime = dongu; k.loopPose = dongu;
            // kalçanın ileri/yan kayması kök hareketi olarak ayrılır (Animator uygulamaz): yerinde koşar;
            // dönüş ve yükseklik pozda kalır
            k.lockRootRotation = true; k.lockRootHeightY = true; k.lockRootPositionXZ = false;
            k.keepOriginalOrientation = true; k.keepOriginalPositionY = true;
        }
        mi.clipAnimations = klipler;
        mi.SaveAndReimport();

        // malzeme: kedininkiyle aynı (kavisli dünya) gölgelendirici, Meshy dokusu
        var kediMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnityTechnologies/Endless Runner/Art/Materials/CatOrange.mat");
        var doku = AssetDatabase.FindAssets("t:Texture2D", new[] { klasor }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t != null && !t.name.EndsWith("Ikon"));
        if (doku != null)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(doku));
            ti.maxTextureSize = 2048; ti.SaveAndReimport(); // yakın kamerada net görünsün
        }
        string matYol = $"{klasor}/{ad}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        if (mat == null) { mat = new Material(kediMat.shader); AssetDatabase.CreateAsset(mat, matYol); }
        mat.shader = kediMat.shader; mat.CopyPropertiesFromMaterial(kediMat); mat.mainTexture = doku;
        EditorUtility.SetDirty(mat);

        // simge (mağaza): Meshy görseli
        string ikonYol = $"{klasor}/{ad}Ikon.png";
        File.Copy($"../TabanlariYagla-V1/karakter-{ad.ToLowerInvariant()}.png", ikonYol, true);
        AssetDatabase.ImportAsset(ikonYol);
        var iti = (TextureImporter)AssetImporter.GetAtPath(ikonYol);
        iti.textureType = TextureImporterType.Sprite; iti.maxTextureSize = 256; iti.SaveAndReimport();
        var ikon = AssetDatabase.LoadAssetAtPath<Sprite>(ikonYol);

        // animasyonlar: CharacterAnimation'daki her klibe karşılık gelen hareket
        var klipVarlik = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        var temel = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_Denetleyici);
        var ust = new AnimatorOverrideController(temel) { name = ad + "Animasyon" };
        var liste = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ust.GetOverrides(liste);
        for (int i = 0; i < liste.Count; ++i)
        {
            string n = liste[i].Key.name, hedef = Esle(n);
            if (hedef != null && klipVarlik.TryGetValue(hedef, out var yeni)) liste[i] = new KeyValuePair<AnimationClip, AnimationClip>(liste[i].Key, yeni);
            Out($"{ad}: {n} -> {hedef}");
        }
        ust.ApplyOverrides(liste);
        string ustYol = $"{klasor}/{ad}Animasyon.overrideController";
        AssetDatabase.DeleteAsset(ustYol); AssetDatabase.CreateAsset(ust, ustYol);

        // prefab: kedi şablonu; görünüm (çocuklar) yerine yeni model
        var kok = PrefabUtility.LoadPrefabContents(k_Kedi);
        foreach (Transform c in kok.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
        var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        float boy = Boy(model), olcek = boy > 0.01f ? kediBoy * 1.15f / boy : 1f;
        foreach (Transform c in model.transform.Cast<Transform>().ToList())
        {
            c.SetParent(kok.transform, false);
            c.localScale *= olcek; c.localPosition *= olcek;
        }
        Object.DestroyImmediate(model);
        foreach (var r in kok.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
        var an = kok.GetComponent<Animator>();
        an.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
        an.runtimeAnimatorController = ust;
        an.applyRootMotion = false;
        var ch = kok.GetComponent<Character>();
        ch.characterName = ad; ch.cost = k_Fiyat; ch.premiumCost = 0; ch.icon = ikon; ch.animator = an;
        ch.accessories = new CharacterAccessories[0];
        string prefab = $"{klasor}/{ad}.prefab";
        bool avatarGecerli = an.avatar != null && an.avatar.isValid;
        PrefabUtility.SaveAsPrefabAsset(kok, prefab);
        PrefabUtility.UnloadPrefabContents(kok);

        // Addressables: Characters grubu, adres = karakter adı, etiket "characters"
        var ayar = AddressableAssetSettingsDefaultObject.Settings;
        var grup = ayar.FindGroup("Characters");
        var giris = ayar.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefab), grup);
        giris.address = ad; giris.SetLabel("characters", true, true);
        EditorUtility.SetDirty(ayar);
        Out($"{ad}: prefab {prefab}, olcek {olcek:F3}, fiyat {k_Fiyat}, avatar {avatarGecerli}");
        return true;
    }

    // CharacterAnimation klip adı → Meshy hareketi
    static string Esle(string n)
    {
        string k = n.ToLowerInvariant();
        if (k.Contains("dance")) return "Dans";
        if (k.Contains("slide")) return "Kayma";
        if (k.Contains("jump") || k.Contains("falling")) return "Ziplama";
        if (k.Contains("death")) return "Dusme";
        if (k.Contains("hit")) return "Carpma";
        if (k.Contains("idle")) return "Bekleme";
        if (k.Contains("run") || k.Contains("start")) return "Kosu";
        return null;
    }

    static float Boy(GameObject kaynak)
    {
        bool varlik = !kaynak.scene.IsValid();
        var go = varlik ? (GameObject)Object.Instantiate(kaynak) : kaynak;
        try {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return 0f;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
        } finally { if (varlik) Object.DestroyImmediate(go); }
    }
}
