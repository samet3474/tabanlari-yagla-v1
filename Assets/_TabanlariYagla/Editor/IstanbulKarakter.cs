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
        // eski çıkarılmış doku aynı adla kalırsa yenisi yazılmaz (yeni model eski dokuyla görünür): önce silinir
        foreach (var eski in AssetDatabase.FindAssets("t:Texture2D", new[] { klasor }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.EndsWith("Ikon.png")).ToList())
            AssetDatabase.DeleteAsset(eski);
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.ExtractTextures(klasor);
        AssetDatabase.Refresh();
        mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        // Humanoid: Meshy hareketleri farklı bir dinlenme pozuna göre yazılmış; Generic'te beden ~0,6 m havada kalıyordu.
        // Humanoid yeniden hedefleme kendi iskeletine uyarlar, ayaklara göre yere oturtur.
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.optimizeGameObjects = false;
        mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        // kök hareketi düğümü yok: kalça ayırmak bedeni ~0,6 m yukarı itiyordu; ileri kayma IstanbulKarakterKlip'te silinir
        mi.motionNodeName = "";
        string kalca = "(yok)";
        var klipler = mi.defaultClipAnimations;
        Out($"{ad}: {klipler.Length} klip [{string.Join(", ", klipler.Select(k => k.name))}] kalca={kalca}");
        if (klipler.Length < k_Hareket.Length) { Out($"{ad}: eksik klip"); return false; }
        for (int i = 0; i < klipler.Length; ++i)
        {
            var k = klipler[i];
            if (i < k_Hareket.Length) k.name = k_Hareket[i];
            bool dongu = k.name == "Kosu" || k.name == "Bekleme" || k.name == "Dans";
            k.loopTime = dongu; k.loopPose = dongu;
            // kök: dönüş ve yükseklik pozda (yükseklik ayaklara göre), ileri kayma ayrılır ve uygulanmaz → yerinde, yerde
            k.lockRootRotation = true; k.lockRootHeightY = true; k.lockRootPositionXZ = false;
            k.keepOriginalOrientation = true; k.keepOriginalPositionY = false; k.heightFromFeet = true; k.keepOriginalPositionXZ = false;
        }
        mi.clipAnimations = klipler;
        mi.SaveAndReimport();

        // malzeme: kedininkiyle aynı (kavisli dünya) gölgelendirici, Meshy dokusu
        var kediMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnityTechnologies/Endless Runner/Art/Materials/CatOrange.mat");
        var doku = AssetDatabase.FindAssets("t:Texture2D", new[] { klasor }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t != null && !t.name.EndsWith("Ikon"));
        if (doku != null)
        {
            Genislet(AssetDatabase.GetAssetPath(doku));
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(doku));
            ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.anisoLevel = 4; ti.mipMapBias = 0f; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.filterMode = FilterMode.Trilinear; ti.SaveAndReimport(); // Kaiser: küçülürken keskin; negatif sapma yamalar arası taşmayı (lekeleri) artırıyordu
        }
        string matYol = $"{klasor}/{ad}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matYol);
        // Subway tarzı net görünüm: ışık, kenar parlaması, dış çizgi (kedinin düz gölgelendiricisi yerine)
        var netShader = Shader.Find("TabanlariYagla/KarakterNet") ?? kediMat.shader;
        if (mat == null) { mat = new Material(netShader); AssetDatabase.CreateAsset(mat, matYol); }
        mat.shader = netShader; mat.mainTexture = doku;
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
        // oyun zıplama/kaymayı kedinin klip sürelerine göre oynatır (JumpSpeed): Meshy klipleri işe yarayan bölümüne kırpılıp
        // aynı süreye getirilir; yoksa zıplama bittiğinde klip daha üçte birindedir
        foreach (var (adi, bas, son, sure) in k_Zamanlama)
            if (klipVarlik.TryGetValue(adi, out var ham)) klipVarlik[adi] = Zamanla(ham, bas, son, sure, $"{klasor}/{ad}_{adi}.anim");
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
        // ayak IK: Humanoid yeniden hedeflemede ayaklar yere düz otursun (kedi Generic, etkilenmez)
        var temelAc = temel as UnityEditor.Animations.AnimatorController;
        if (temelAc != null)
        {
            foreach (var katman in temelAc.layers) AyakIK(katman.stateMachine);
            EditorUtility.SetDirty(temelAc);
        }
        string ustYol = $"{klasor}/{ad}Animasyon.overrideController";
        AssetDatabase.DeleteAsset(ustYol); AssetDatabase.CreateAsset(ust, ustYol);

        // prefab: kedi şablonu; görünüm (çocuklar) yerine yeni model
        var kok = PrefabUtility.LoadPrefabContents(k_Kedi);
        foreach (Transform c in kok.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
        var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        float boy = Boy(model), olcek = boy > 0.01f ? kediBoy * 1.15f / boy : 1f;
        // görünüm ve Animator "Gorsel" alt nesnesinde: Humanoid Animator ölçekli kökteyken beden ~0,6 m havada kalıyordu;
        // ölçeksiz alt nesnede ayaklar yere basar
        var gorsel = new GameObject("Gorsel").transform; gorsel.SetParent(kok.transform, false);
        foreach (Transform c in model.transform.Cast<Transform>().ToList()) c.SetParent(gorsel, false);
        kok.transform.localScale = Vector3.one * olcek;
        gorsel.localPosition = new Vector3(0f, 0.05f, 0f); // Meshy pozlarında taban ~5 cm yere gömülüyor
        Object.DestroyImmediate(model);
        foreach (var r in kok.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
        var eskiAn = kok.GetComponent<Animator>(); var kip = eskiAn.cullingMode; Object.DestroyImmediate(eskiAn);
        var an = gorsel.gameObject.AddComponent<Animator>(); an.cullingMode = kip;
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

    // Meshy dokusu küçük yamalardan oluşur, aralarında saf siyah boşluk kalır; küçülünce (mip) siyah yamaya taşar ve
    // karakterde koyu lekeler olur. Boşluklar komşu yamanın rengiyle 24 piksel genişletilir (dilation).
    static void Genislet(string yol)
    {
        var tx = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tx.LoadImage(File.ReadAllBytes(yol))) return;
        int w = tx.width, h = tx.height; var p = tx.GetPixels32();
        var bos = new bool[p.Length]; int sayi = 0;
        for (int i = 0; i < p.Length; ++i) if (p[i].r < 3 && p[i].g < 3 && p[i].b < 3) { bos[i] = true; sayi++; }
        for (int tur = 0; tur < 24 && sayi > 0; ++tur)
        {
            var yeni = (bool[])bos.Clone();
            for (int y = 0; y < h; ++y) for (int x = 0; x < w; ++x)
            {
                int i = y * w + x; if (!bos[i]) continue;
                int r = 0, g = 0, b = 0, n = 0;
                if (x > 0 && !bos[i - 1]) { r += p[i - 1].r; g += p[i - 1].g; b += p[i - 1].b; n++; }
                if (x < w - 1 && !bos[i + 1]) { r += p[i + 1].r; g += p[i + 1].g; b += p[i + 1].b; n++; }
                if (y > 0 && !bos[i - w]) { r += p[i - w].r; g += p[i - w].g; b += p[i - w].b; n++; }
                if (y < h - 1 && !bos[i + w]) { r += p[i + w].r; g += p[i + w].g; b += p[i + w].b; n++; }
                if (n == 0) continue;
                p[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255); yeni[i] = false; sayi--;
            }
            bos = yeni;
        }
        tx.SetPixels32(p); tx.Apply();
        File.WriteAllBytes(yol, yol.EndsWith(".png") ? tx.EncodeToPNG() : tx.EncodeToJPG(95));
        Object.DestroyImmediate(tx); AssetDatabase.ImportAsset(yol);
        Out($"{Path.GetFileName(yol)}: yama boşlukları dolduruldu (kalan {sayi})");
    }

    // (klip, kırpma başı, kırpma sonu, hedef süre) — kedinin Cat_Jump 0,60 / Cat_Slide 1,03 / Cat_HitWall1 0,73 sn; koşu döngüsü
    // Subway temposunda biraz hızlandırılır
    static readonly (string, float, float, float)[] k_Zamanlama =
    {
        ("Ziplama", 0.40f, 1.20f, 0.60f), ("Kayma", 0.15f, 1.30f, 1.03f), ("Carpma", 0f, 1.10f, 0.73f), ("Kosu", 0f, 0.533f, 0.42f),
    };

    static AnimationClip Zamanla(AnimationClip ham, float bas, float son, float sure, string yol)
    {
        son = Mathf.Min(son, ham.length); float olcek = (son - bas) / sure;
        var yeni = new AnimationClip { name = ham.name, frameRate = 30f };
        int n = Mathf.CeilToInt(sure * 30f);
        foreach (var b in AnimationUtility.GetCurveBindings(ham))
        {
            var e = AnimationUtility.GetEditorCurve(ham, b); var anahtar = new Keyframe[n + 1];
            for (int i = 0; i <= n; ++i) { float t = sure * i / n; anahtar[i] = new Keyframe(t, e.Evaluate(bas + t * olcek)); }
            var c = new AnimationCurve(anahtar);
            for (int i = 0; i < c.length; ++i) c.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(yeni, b, c);
        }
        var ayar = AnimationUtility.GetAnimationClipSettings(ham);
        ayar.startTime = 0f; ayar.stopTime = sure;
        AnimationUtility.SetAnimationClipSettings(yeni, ayar);
        AssetDatabase.DeleteAsset(yol); AssetDatabase.CreateAsset(yeni, yol);
        Out($"{ham.name}: {bas:F2}-{son:F2} sn -> {sure:F2} sn ({yol})");
        return yeni;
    }

    static void AyakIK(UnityEditor.Animations.AnimatorStateMachine sm)
    {
        foreach (var d in sm.states) d.state.iKOnFeet = true;
        foreach (var alt in sm.stateMachines) AyakIK(alt.stateMachine);
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
        if (k.Contains("start")) return "Bekleme"; // geri sayımda kedi gibi hazır bekler, süre dolunca koşar (runStart)
        if (k.Contains("run")) return "Kosu";
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

// Tanı (-executeMethod IstanbulKarakterTani.Olc): her karakterin koşu durumunda en alçak noktası (ayak tabanı) zamanla.
public static class IstanbulKarakterTani
{
    public static void Olc()
    {
        foreach (var yol in new[] { "Assets/UnityTechnologies/Endless Runner/Prefabs/Characters/Cat/character.prefab", "Assets/_TabanlariYagla/Karakterler/Samet/Samet.prefab", "Assets/_TabanlariYagla/Karakterler/Berivan/Berivan.prefab" })
        {
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(yol));
            var an = go.GetComponentInChildren<Animator>();
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in rs) r.updateWhenOffscreen = true;
            string Alt() { float al = float.MaxValue; foreach (var r in rs) { var m = new Mesh(); r.BakeMesh(m, true); foreach (var v in m.vertices) al = Mathf.Min(al, r.transform.TransformPoint(v).y); Object.DestroyImmediate(m); } return al.ToString("F2"); }
            string poz = Alt();
            an.Rebind(); an.Play("Idle", 0, 0.3f); an.Update(0.01f); string bekle = Alt();
            Debug.Log($"TANI| {System.IO.Path.GetFileName(yol)} duruspozu_alt={poz} bekleme_alt={bekle}");
            an.Rebind(); an.SetBool("Moving", true); an.Play("runLoop", 0, 0f); an.Update(0f);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 12; ++i)
            {
                an.Update(0.06f);
                float alt = float.MaxValue, ust = float.MinValue;
                foreach (var r in rs) { var m = new Mesh(); r.BakeMesh(m, true); foreach (var v in m.vertices) { var w = r.transform.TransformPoint(v); alt = Mathf.Min(alt, w.y); ust = Mathf.Max(ust, w.y); } Object.DestroyImmediate(m); }
                var kalca = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hips");
                sb.Append($" [{alt:F2}..{ust:F2} kalca {(kalca ? kalca.position.y : -1):F2}]");
            }
            Debug.Log($"TANI| {System.IO.Path.GetFileName(yol)} durum={an.GetCurrentAnimatorStateInfo(0).shortNameHash}{sb}");
            Object.DestroyImmediate(go);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}


// İçe aktarımda karakter kliplerinin kalça konumundan yalnız ileri kayma ekseni sabitlenir (net yer değiştirmesi en
// büyük eksen): karakter yerinde koşar, yukarı-aşağı sekme ve yana salınım kalır. Oyun ilerlemeyi kendisi verir.
public class IstanbulKarakterKlip : AssetPostprocessor
{
    void OnPostprocessAnimation(GameObject kok, AnimationClip klip)
    {
        if (!assetPath.StartsWith("Assets/_TabanlariYagla/Karakterler/")) return;
        // iskeletin üst düğümü (Armature) için yazılan konum/ölçek/dönüş eğrileri içe aktarma artığı: ölçeği ezip bedeni
        // ~0,6 m kaldırıyordu; silinir (düğüm prefabdaki hâlinde kalır)
        foreach (var b in AnimationUtility.GetCurveBindings(klip).Where(b => !b.path.Contains("/") && !b.path.EndsWith("Hips") && b.type == typeof(Transform)).ToList())
        { AnimationUtility.SetEditorCurve(klip, b, null); Debug.Log($"KARAKTER| {klip.name}: ust dugum egrisi silindi {b.path}.{b.propertyName}"); }
        var baglar = AnimationUtility.GetCurveBindings(klip).Where(b => b.path.EndsWith("Hips") && b.propertyName.StartsWith("m_LocalPosition.")).ToList();
        // yukarı ekseni: duruş pozunda kalçanın en büyük konum bileşeni (kalça yüksekliği); o eksene dokunulmaz
        var kalca = kok.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hips");
        string yukari = null;
        if (kalca != null) { var p = kalca.localPosition; yukari = Mathf.Abs(p.x) >= Mathf.Abs(p.y) && Mathf.Abs(p.x) >= Mathf.Abs(p.z) ? "x" : Mathf.Abs(p.y) >= Mathf.Abs(p.z) ? "y" : "z"; }
        EditorCurveBinding? ileri = null; float enBuyuk = 0f;
        foreach (var b in baglar)
        {
            if (yukari != null && b.propertyName.EndsWith("." + yukari)) continue;
            var e = AnimationUtility.GetEditorCurve(klip, b);
            if (e == null || e.length < 2) continue;
            float d = Mathf.Abs(e.keys[e.length - 1].value - e.keys[0].value);
            if (d > enBuyuk) { enBuyuk = d; ileri = b; }
        }
        if (ileri == null) return;
        var c = AnimationUtility.GetEditorCurve(klip, ileri.Value);
        float ilk = c.keys[0].value;
        AnimationUtility.SetEditorCurve(klip, ileri.Value, AnimationCurve.Constant(0f, klip.length, ilk));
        Debug.Log($"KARAKTER| {klip.name}: kalca {ileri.Value.propertyName} sabitlendi (kayma {enBuyuk:F2}, yukari={yukari})");
    }
}

public static class IstanbulKarakterTani2
{
    public static void Olc()
    {
        var fbx = "Assets/_TabanlariYagla/Karakterler/Samet/Samet.fbx";
        var kosu = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => c.name == "Kosu");
        foreach (var b in AnimationUtility.GetCurveBindings(kosu).Where(b => b.path.Split('/').Length <= 2).Take(12))
        {
            var c = AnimationUtility.GetEditorCurve(kosu, b);
            Debug.Log($"TANI2| egri {b.path} {b.propertyName} ilk={c.keys[0].value:F3} son={c.keys[c.length - 1].value:F3}");
        }
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_TabanlariYagla/Karakterler/Samet/Samet.prefab"));
        foreach (var t in go.GetComponentsInChildren<Transform>(true).Where(t => t.parent == go.transform || t.name == "Hips"))
            Debug.Log($"TANI2| dugum {AnimationUtility.CalculateTransformPath(t, go.transform)} lp={t.localPosition} lr={t.localEulerAngles} ls={t.localScale}");
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        Debug.Log($"TANI2| smr kok kemik={(smr.rootBone ? AnimationUtility.CalculateTransformPath(smr.rootBone, go.transform) : "yok")} kemik={smr.bones.Length} ilk={(smr.bones.Length > 0 && smr.bones[0] ? AnimationUtility.CalculateTransformPath(smr.bones[0], go.transform) : "-")}");
        kosu.SampleAnimation(go, 0.1f);
        var h = go.GetComponentsInChildren<Transform>().First(t => t.name == "Hips");
        Debug.Log($"TANI2| ornek sonrasi hips lp={h.localPosition} dunya={h.position}");
        Object.DestroyImmediate(go);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}

public static class IstanbulKarakterTani3
{
    public static void Olc()
    {
        var fbx = "Assets/_TabanlariYagla/Karakterler/Samet/Samet.fbx";
        foreach (var klip in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
        {
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_TabanlariYagla/Karakterler/Samet/Samet.prefab"));
            var an = go.GetComponentInChildren<Animator>();
            var ac = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath("Assets/_gecici_tani.controller");
            ac.layers[0].stateMachine.AddState("s").motion = klip;
            an.runtimeAnimatorController = ac; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.Rebind(); an.Update(0f);
            var rs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            float enAlt = 9, enUst = -9; var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 10; ++i)
            {
                an.Update(klip.length / 10f);
                float al = 9, us = -9;
                foreach (var r in rs) { var m = new Mesh(); r.BakeMesh(m, true); foreach (var v in m.vertices) { float y = r.transform.TransformPoint(v).y; al = Mathf.Min(al, y); us = Mathf.Max(us, y); } Object.DestroyImmediate(m); }
                sb.Append($" {al:F2}"); enAlt = Mathf.Min(enAlt, al); enUst = Mathf.Max(enUst, us);
            }
            Debug.Log($"TANI3| {klip.name} sure={klip.length:F2} humanoid={klip.humanMotion} alt:{sb} | ust max {enUst:F2}");
            Object.DestroyImmediate(go); AssetDatabase.DeleteAsset("Assets/_gecici_tani.controller");
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}

public static class IstanbulKarakterTani4
{
    public static void Olc()
    {
        foreach (var yol in new[] { "Assets/UnityTechnologies/Endless Runner/Art/Animations/Cat_Jump.fbx", "Assets/UnityTechnologies/Endless Runner/Art/Animations/Cat_Slide.fbx", "Assets/UnityTechnologies/Endless Runner/Art/Animations/Cat_RunShort.fbx", "Assets/UnityTechnologies/Endless Runner/Art/Animations/Cat_HitWall1.fbx", "Assets/UnityTechnologies/Endless Runner/Art/Animations/Cat_Death.fbx" })
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(yol).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
                Debug.Log($"TANI4| {c.name} {c.length:F2} sn loop={c.isLooping}");
        foreach (var c in AssetDatabase.LoadAllAssetsAtPath("Assets/_TabanlariYagla/Karakterler/Samet/Samet.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
            Debug.Log($"TANI4| Samet {c.name} {c.length:F2} sn loop={c.isLooping}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}

// Menü önizlemesi (-executeMethod IstanbulKarakterOnizleme.Cek, grafik açık): bekleme pozunda, menü kamerası yakınlığında
public static class IstanbulKarakterOnizleme
{
    public static void Cek()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        Shader.SetGlobalFloat("_CurveStrength", 0f); RenderSettings.fog = false;
        int i = 0;
        foreach (var ad in new[] { "Samet", "Berivan" })
        {
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_TabanlariYagla/Karakterler/{ad}/{ad}.prefab"));
            go.transform.position = new Vector3(i++ * 1.6f - 0.8f, 0, 0); go.transform.rotation = Quaternion.Euler(0, 160, 0);
            var an = go.GetComponentInChildren<Animator>(); an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.Rebind(); an.Play("Idle", 0, 0.3f); an.Update(0.05f);
        }
        var cam = new GameObject("k").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0.9f, 3.4f); cam.transform.rotation = Quaternion.Euler(5, 180, 0); cam.fieldOfView = 40;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.35f, 0.37f, 0.45f);
        var rt = new RenderTexture(1200, 1000, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(1200, 1000, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); tx.Apply();
        System.IO.File.WriteAllBytes("../_araclar/parkur/karakter-menu-onizleme.png", tx.EncodeToPNG());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
