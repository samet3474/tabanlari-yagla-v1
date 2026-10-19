// Kovalayanlar (Subway'deki müfettiş ve köpek yerine): Türk polisi (Meshy iskelet + koşu/bekleme) ve K9 köpeği.
//   -executeMethod IstanbulKovalayan.Kur
// Köpek Meshy'de iskeletlenemez (yalnız iki ayaklı): ağ burada gövde + 4 bacağa ayrılır, her bacağa bir kemik verilir
// (kalçada döner), bacak dışı köşe noktaları gövdeye bağlanır. Kovalayan.cs bu kemikleri dörtnala sallar.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public static class IstanbulKovalayan
{
    const string k_Kok = "Assets/_TabanlariYagla/Kovalayan";
    const string k_Kaynak = "../_araclar/parkur/meshy";
    static void Out(string s) => Debug.Log("KOVALAYAN| " + s);

    public static void Kur()
    {
        int hata = 0;
        try
        {
            if (!AssetDatabase.IsValidFolder(k_Kok)) AssetDatabase.CreateFolder("Assets/_TabanlariYagla", "Kovalayan");
            var net = Shader.Find("TabanlariYagla/KarakterNet");
            var kok = new GameObject("IstanbulKovalayan");
            var kov = kok.AddComponent<Kovalayan>();

            // ---- polis: iskeletli, Generic, Animator ölçeksiz Gorsel'de
            string pfbx = $"{k_Kok}/Polis.fbx";
            File.Copy(Path.Combine(k_Kaynak, "polis/Polis.fbx"), pfbx, true); AssetDatabase.ImportAsset(pfbx, ImportAssetOptions.ForceUpdate);
            var mi = (ModelImporter)AssetImporter.GetAtPath(pfbx);
            foreach (var eski in AssetDatabase.FindAssets("t:Texture2D", new[] { k_Kok }).Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p).StartsWith("Polis_") || p.Contains("baseColor")).ToList()) AssetDatabase.DeleteAsset(eski);
            mi.ExtractTextures(k_Kok); AssetDatabase.Refresh();
            mi = (ModelImporter)AssetImporter.GetAtPath(pfbx);
            mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.optimizeGameObjects = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.motionNodeName = "";
            var kl = mi.defaultClipAnimations;
            string[] adlar = { "Kosu", "Bekleme" };
            for (int i = 0; i < kl.Length; ++i) { if (i < adlar.Length) kl[i].name = adlar[i]; kl[i].loopTime = true; kl[i].loopPose = true; kl[i].keepOriginalPositionXZ = true; kl[i].keepOriginalPositionY = true; kl[i].keepOriginalOrientation = true; }
            mi.clipAnimations = kl; mi.SaveAndReimport();
            var pdoku = AssetDatabase.FindAssets("t:Texture2D", new[] { k_Kok }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => !p.Contains("Kopek"));
            var pmat = Malzeme($"{k_Kok}/Polis.mat", net, pdoku);
            var klip = AssetDatabase.LoadAllAssetsAtPath(pfbx).OfType<AnimationClip>().First(c => c.name == "Kosu");
            // ileri kayma: kalçanın net yer değiştirmesi en büyük olan yatay ekseni sabitlenir (karakterlerdekiyle aynı)
            var ac = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath($"{k_Kok}/Polis.controller");
            ac.layers[0].stateMachine.AddState("Kosu").motion = klip;
            var polis = new GameObject("Polis").transform; polis.SetParent(kok.transform, false); polis.localPosition = new Vector3(-0.4f, 0f, 0f); // koşucunun sol arkası
            var pgorsel = new GameObject("Gorsel").transform; pgorsel.SetParent(polis, false);
            var pm = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(pfbx));
            foreach (Transform c in pm.transform.Cast<Transform>().ToList()) c.SetParent(pgorsel, false);
            Object.DestroyImmediate(pm);
            float pboy = Boy(pgorsel.gameObject); polis.localScale = Vector3.one * (1.5f / Mathf.Max(0.01f, pboy)); // koşucudan (~1,3 m) biraz uzun
            foreach (var r in pgorsel.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = pmat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true; }
            var pan = pgorsel.gameObject.AddComponent<Animator>();
            pan.avatar = AssetDatabase.LoadAllAssetsAtPath(pfbx).OfType<Avatar>().FirstOrDefault(); pan.runtimeAnimatorController = ac; pan.applyRootMotion = false;
            kov.polisAnimator = pan;
            Out($"polis: boy {pboy:F2} -> 1,7 m, klip {kl.Length}");

            // ---- köpek: gövde + 4 bacak kemiği
            var kopek = KopekKur(k_Kok, net, out var bacaklar);
            kopek.SetParent(kok.transform, false);
            kopek.localPosition = new Vector3(0.85f, 0f, -0.2f);
            kov.kopek = kopek; kov.kopekBacaklari = bacaklar;

            string prefab = $"{k_Kok}/IstanbulKovalayan.prefab";
            var pre = PrefabUtility.SaveAsPrefabAsset(kok, prefab); Object.DestroyImmediate(kok);
            var tema = AssetDatabase.LoadAssetAtPath<ThemeData>("Assets/_TabanlariYagla/Istanbul/IstanbulTheme.asset");
            tema.kovalayan = pre; EditorUtility.SetDirty(tema); AssetDatabase.SaveAssets();
            Out("prefab " + prefab);
        }
        catch (System.Exception e) { Debug.LogException(e); hata++; }
        Out(hata == 0 ? "bitti" : "HATA");
        if (Application.isBatchMode) EditorApplication.Exit(hata == 0 ? 0 : 1);
    }

    static Material Malzeme(string yol, Shader sh, string doku)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, yol); }
        m.shader = sh;
        if (doku != null)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(doku);
            ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.SaveAndReimport();
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(doku);
        }
        EditorUtility.SetDirty(m); return m;
    }

    static Transform KopekKur(string klasor, Shader sh, out Transform[] bacaklar)
    {
        string fbx = $"{klasor}/Kopek.fbx", png = $"{klasor}/Kopek_renk.png";
        File.Copy(Path.Combine(k_Kaynak, "kopek/Kopek.fbx"), fbx, true); File.Copy(Path.Combine(k_Kaynak, "kopek/Kopek_base_color.png"), png, true);
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(png);
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.isReadable = true; mi.animationType = ModelImporterAnimationType.None; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
        var mat = Malzeme($"{klasor}/Kopek.mat", sh, png);

        // tüm ağları köpek kökü uzayında tek ağda birleştir
        var src = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); var n = new List<Vector3>();
        foreach (var mf in src.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh; int bas = v.Count;
            foreach (var p in m.vertices) v.Add(mf.transform.TransformPoint(p));
            foreach (var q in m.normals) n.Add(mf.transform.TransformDirection(q).normalized);
            uv.AddRange(m.uv.Length == m.vertexCount ? m.uv : new Vector2[m.vertexCount]);
            foreach (var i in m.triangles) t.Add(bas + i);
        }
        Object.DestroyImmediate(src);
        var b = new Bounds(v[0], Vector3.zero); foreach (var p in v) b.Encapsulate(p);
        // uzun yatay eksen ileri; baş tarafı (en yüksek noktanın olduğu uç) +z olsun
        bool xUzun = b.size.x > b.size.z;
        Vector3 Yonlu(Vector3 p) => xUzun ? new Vector3(-(p.z - b.center.z), p.y - b.min.y, p.x - b.center.x) : new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
        for (int i = 0; i < v.Count; ++i) { v[i] = Yonlu(v[i]); n[i] = xUzun ? new Vector3(-n[i].z, n[i].y, n[i].x) : n[i]; }
        var b2 = new Bounds(v[0], Vector3.zero); foreach (var p in v) b2.Encapsulate(p);
        float tepeZ = v.OrderByDescending(p => p.y).Take(20).Average(p => p.z);
        if (tepeZ < 0) for (int i = 0; i < v.Count; ++i) { v[i] = new Vector3(-v[i].x, v[i].y, -v[i].z); n[i] = new Vector3(-n[i].x, n[i].y, -n[i].z); }
        float olcek = 0.75f / b2.size.y; // omuz ~0,75 m (K9 boyu)
        for (int i = 0; i < v.Count; ++i) v[i] *= olcek;
        b2 = new Bounds(v[0], Vector3.zero); foreach (var p in v) b2.Encapsulate(p);

        // bacaklar: gövde altı (yüksekliğin %42'sinin altı) köşe noktaları; ön/arka (z işareti) x sol/sağ (x işareti)
        float kalcaY = b2.size.y * 0.42f, karisim = b2.size.y * 0.08f;
        var dort = new List<Vector3>[4]; for (int k = 0; k < 4; ++k) dort[k] = new List<Vector3>();
        int Ceyrek(Vector3 p) => (p.z > 0 ? 0 : 2) + (p.x < 0 ? 0 : 1);
        foreach (var p in v) if (p.y < kalcaY) dort[Ceyrek(p)].Add(p);
        var kokT = new GameObject("Kopek").transform;
        var govde = new GameObject("Govde").transform; govde.SetParent(kokT, false);
        bacaklar = new Transform[4]; var kemikler = new Transform[5]; kemikler[0] = govde;
        string[] ad = { "OnSol", "OnSag", "ArkaSol", "ArkaSag" };
        for (int k = 0; k < 4; ++k)
        {
            Vector3 m = dort[k].Count > 0 ? new Vector3(dort[k].Average(p => p.x), kalcaY, dort[k].Average(p => p.z)) : new Vector3((k % 2 == 0 ? -1 : 1) * 0.1f, kalcaY, k < 2 ? 0.2f : -0.2f);
            var bt = new GameObject("Bacak" + ad[k]).transform; bt.SetParent(govde, false); bt.localPosition = m;
            bacaklar[k] = bt; kemikler[k + 1] = bt;
        }
        var w = new BoneWeight[v.Count];
        for (int i = 0; i < v.Count; ++i)
        {
            var p = v[i];
            if (p.y >= kalcaY + karisim) { w[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f }; continue; }
            int k = Ceyrek(p);
            float a = Mathf.Clamp01((kalcaY + karisim - p.y) / (2f * karisim)); // 1: tam bacak
            w[i] = new BoneWeight { boneIndex0 = k + 1, weight0 = a, boneIndex1 = 0, weight1 = 1f - a };
        }
        var ag = new Mesh { name = "KopekAg", indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        ag.SetVertices(v); ag.SetNormals(n); ag.SetUVs(0, uv); ag.SetTriangles(t, 0); ag.boneWeights = w;
        ag.bindposes = kemikler.Select(k => k.worldToLocalMatrix * kokT.localToWorldMatrix).ToArray();
        ag.RecalculateBounds();
        string agYol = $"{klasor}/KopekAg.asset"; AssetDatabase.DeleteAsset(agYol); AssetDatabase.CreateAsset(ag, agYol);
        var cizici = new GameObject("Deri").AddComponent<SkinnedMeshRenderer>(); cizici.transform.SetParent(kokT, false);
        cizici.sharedMesh = ag; cizici.bones = kemikler; cizici.rootBone = govde; cizici.sharedMaterial = mat; cizici.updateWhenOffscreen = true;
        cizici.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Out($"kopek: {v.Count} nokta, boy {b2.size.y:F2} uzunluk {b2.size.z:F2}, bacak nokta {string.Join("/", dort.Select(d => d.Count))}");
        return kokT;
    }

    static float Boy(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) return 0f;
        var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); return bb.size.y;
    }
}

// Önizleme (-executeMethod IstanbulKovalayanOnizleme.Cek, grafik açık): önden, yandan, arkadan, çapraz; koşu ve dörtnala
public static class IstanbulKovalayanOnizleme
{
    public static void Cek()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        Shader.SetGlobalFloat("_CurveStrength", 0f); RenderSettings.fog = false;
        var klip = AssetDatabase.LoadAllAssetsAtPath("Assets/_TabanlariYagla/Kovalayan/Polis.fbx").OfType<AnimationClip>().First(c => c.name == "Kosu");
        var rt = new RenderTexture(400, 420, 24) { antiAliasing = 4 };
        var sonuc = new Texture2D(1600, 840, TextureFormat.RGB24, false);
        Vector3[] yon = { new Vector3(0.4f, 0.9f, 3.6f), new Vector3(3.6f, 0.9f, 0.3f), new Vector3(0.4f, 1.2f, -3.6f), new Vector3(2.6f, 1.6f, 2.6f) };
        for (int s = 0; s < 2; ++s)
            for (int g = 0; g < 4; ++g)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_TabanlariYagla/Kovalayan/IstanbulKovalayan.prefab"));
                var kv = go.GetComponent<Kovalayan>();
                klip.SampleAnimation(kv.polisAnimator.gameObject, klip.length * (s == 0 ? 0.1f : 0.6f));
                float f = s == 0 ? 0.6f : 2.3f;
                for (int i = 0; i < 4; ++i) kv.kopekBacaklari[i].localRotation = Quaternion.Euler(Mathf.Sin(f + (i >= 2 ? Mathf.PI : 0f) + (i % 2) * 0.35f) * 38f, 0f, 0f);
                var cam = new GameObject("k").AddComponent<Camera>(); cam.fieldOfView = 38;
                cam.transform.position = yon[g]; cam.transform.LookAt(new Vector3(0.35f, 0.7f, 0.15f));
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.6f, 0.68f);
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tx = new Texture2D(400, 420, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 400, 420), 0, 0); tx.Apply();
                sonuc.SetPixels(g * 400, (1 - s) * 420, 400, 420, tx.GetPixels());
                Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(go);
            }
        sonuc.Apply();
        File.WriteAllBytes("../_araclar/parkur/kovalayan-onizleme.png", sonuc.EncodeToPNG());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
