// Parkur adaleti ve tepki süresi ölçümü (yalnızca editör; Editor klasöründe olduğu için oyuna/yayın derlemesine girmez).
// Oyun mantığını değiştirmez: Main sahnesinden gerçek hız/hareket değerlerini, temalardan gerçek segment ve engel
// prefablarını okur; TrackManager + SimpleBarricade/AllLaneObstacle/PatrollingObstacle/Missile üretim kurallarını taklit
// ederek binlerce segmentlik parkur üretir ve her ardışık engel çifti için kaçış yolu ile zaman payını hesaplar.
// Hiçbir sahne/prefab kaydedilmez. Çalıştırma:
//   Unity -batchmode -projectPath . -executeMethod ParkurAdaletOlcum.Calistir -parkurCikti <klasör> [-parkurSegment 20000] [-parkurTohum 1]
//
// Tasarlanmış parçalar (TasarimliSatir) için ek kurallar:
//  - Engel şerit başınadır: aynı sıradaki farklı engeller (ör. solda zıplama, ortada kayma, sağda tramvay) her şeritte
//    kendi kutusuyla değerlendirilir.
//  - Uzun engeller (derinlik > 2 m, tramvay): kapladıkları mesafe boyunca, o aralığa düşen diğer sıralarda da şeritlerini
//    kapatır; çiftlerde şerit değişimi uzun engelin bitişinden sonra / başlangıcından önce olmalıdır.
//  - Rampalı tramvay: rampanın şeridinden rampa başlamadan girilirse tavan rotası serbesttir (Rampa); tavandan aynı
//    şeritte sonraki engele, düşüş süresi (√(2·2,4/30) ≈ 0,4 s) eklenerek bakılır. Rampaya yandan girilemez.
//  - Karşıdan gelen tramvay (HareketliTramvay): her hızda, oyuncuyla karşılaştığı yere kaydırılır ve kısalır (u·D/(v+u)).
//  - Altınlar: engel tetikleyicisinin içinde mi, yürünen yüzeyin altında mı, zıplayarak ulaşılamayacak kadar yüksekte mi
//    (gerçek çarpıştırıcılarla, sahnede yerleştirilerek).
// Day/Night parçalarında (tek tür engel, kısa) sonuçlar önceki sürümle aynıdır.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkurAdaletOlcum
{
    // Sınıflandırma eşikleri (saniye) — varsayımlar, rapora yazılır.
    const float k_Gercekdisi = 0.20f; // bu payın altında insan tepki + kaydırma yetişmez
    const float k_Zor = 0.40f;        // 0.20–0.40 zor, üstü rahat
    const float k_DarPencere = 0.10f; // zıplama/kayma zamanlama penceresi bunun altındaysa "çok dar"
    const float k_UzunEngel = 2f;     // bu derinlikten uzun engel başka sıraların şeridini de kapatır
    const float k_Yercekimi = 30f;    // CharacterInputController.k_Yercekimi
    const float k_TirmanmaAdimi = 0.6f;

    // Rampa: rampa şeridinden tavana; SpanRampa: başka bir sıranın tramvay tavanı (yalnız aynı şeritte devam edilir).
    enum Eylem { Serbest, Zipla, Kay, Kapali, Rampa, SpanRampa }

    class EngelTipi
    {
        public string ad, tur; public Bounds b; // engel kökü (lane 0) uzayında çarpışma kutusu (yalnız katman 9 tetikleyiciler)
        public string yokSayilan; public int tetikSayisi;
        public bool uzun; public float rampaBas = float.NaN; public float hiz, baslama; // tasarlanmış engeller
        public float tavan = -1;
    }
    class SegmentTipi
    {
        public string ad; public float uzunluk; public float[] konumMesafe; public EngelTipi[] engeller;
        public List<(float m, List<(EngelTipi e, int maske)> engel)> tasarim; // TasarimliSatir: sabit şeritli satırlar (aynı metredekiler tek satır)
    }
    class Sira // bir engel noktası (satır)
    {
        public float d; public string segment, engel, tur; public Eylem[] serit = new Eylem[3];
        public float zMin, zMax; public float ziplaPencere = -1, kayPencere = -1; // m (hıza göre değişir; hesap anında)
        public Bounds b; public int[] seritler;
        // şerit başına (kapalı şeritler): engel kutusu, mesafe aralığı, rampa, hareket
        public Bounds[] sb = new Bounds[3]; public float[] kz0 = new float[3], kz1 = new float[3];
        public bool[] uzun = new bool[3], rampa = new bool[3], tavan = new bool[3]; public float[] rampaBas = new float[3], hiz = new float[3], baslama = new float[3];
        public float zMinKisa, zMaxKisa; // kısa engellerin aralığı (yoksa d) — serbest şeritler için
    }

    static float s_LaneOffset, s_MinSpeed, s_MaxSpeed, s_LaneSpeed, s_JumpLen, s_JumpH, s_SlideLen;
    static float s_CW, s_CH, s_CD, s_CB;
    const int k_EngelKatmani = 9;  // CharacterCollider.k_ObstacleLayerIndex ile aynı
    const int k_ZeminKatmani = 11; // CharacterInputController.k_ZeminKatmani ile aynı
    static bool s_EskiSecim;       // -parkurEskiSecim: m_PreviousSegment düzeltmesi öncesi segment seçimi (karşılaştırma için)
    static float[] s_Incele = new float[0]; // -parkurIncele "d1,d2": bu mesafelerden başlayan çiftleri ayrıntılı yaz
    static int s_Nokta, s_Yerlesen;  // engel noktası sayısı / engel konan nokta sayısı
    static StringBuilder s_Out = new StringBuilder();
    static int s_AltinSorun;       // tüm tasarlanmış parçalarda engel içi + yüzey altı + ulaşılamaz altın

    static void Yaz(string s) { s_Out.AppendLine(s); Debug.Log("PARKUR| " + s); }

    public static void Calistir()
    {
        var args = System.Environment.GetCommandLineArgs();
        string Arg(string k, string def) { int i = System.Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
        string cikti = Arg("-parkurCikti", "Temp/parkur");
        int segSayisi = int.Parse(Arg("-parkurSegment", "20000"));
        int tohum = int.Parse(Arg("-parkurTohum", "1"));
        s_EskiSecim = System.Array.IndexOf(args, "-parkurEskiSecim") >= 0;
        s_Incele = Arg("-parkurIncele", "").Split(',').Where(x => x.Length > 0).Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Yaz($"AYAR tohum={tohum} segment={segSayisi} secim={(s_EskiSecim ? "eski (m_PreviousSegment calismiyor)" : "duzeltilmis m_PreviousSegment")} engel=yalniz katman {k_EngelKatmani} tetikleyiciler");
        Directory.CreateDirectory(cikti);
        int kod = 0;
        try { Olc(segSayisi, tohum); }
        catch (System.Exception e) { Yaz("HATA: " + e); kod = 1; }
        File.WriteAllText(Path.Combine(cikti, "parkur-adalet.txt"), s_Out.ToString(), new UTF8Encoding(false));
        EditorApplication.Exit(kod);
    }

    static void Olc(int segSayisi, int tohum)
    {
        // 1) Gerçek değerler: Main sahnesi (kaydedilmez).
        EditorSceneManager.OpenScene("Assets/UnityTechnologies/Endless Runner/Scenes/Main.unity", OpenSceneMode.Single);
        var tm = Object.FindFirstObjectByType<TrackManager>(FindObjectsInactive.Include);
        var ci = Object.FindFirstObjectByType<CharacterInputController>(FindObjectsInactive.Include);
        var cc = Object.FindFirstObjectByType<CharacterCollider>(FindObjectsInactive.Include);
        s_LaneOffset = tm.laneOffset; s_MinSpeed = tm.minSpeed; s_MaxSpeed = tm.maxSpeed;
        s_LaneSpeed = ci.laneChangeSpeed; s_JumpLen = ci.jumpLength; s_JumpH = ci.jumpHeight; s_SlideLen = ci.slideLength;
        var box = cc.GetComponent<BoxCollider>();
        Vector3 sc = box.transform.lossyScale;
        s_CW = box.size.x * Mathf.Abs(sc.x); s_CH = box.size.y * Mathf.Abs(sc.y); s_CD = box.size.z * Mathf.Abs(sc.z);
        s_CB = (box.center.y - box.size.y * 0.5f) * sc.y + box.transform.localPosition.y;
        Yaz($"DEGERLER hiz={s_MinSpeed}-{s_MaxSpeed} m/s ivme=0.2 m/s2 serit={s_LaneOffset} m seritHizi={s_LaneSpeed} m/s ziplama={s_JumpLen} m x(1+oran) yukseklik={s_JumpH} m kayma={s_SlideLen} m x(1+oran)");
        Yaz($"KARAKTER carpisma kutusu genislik={s_CW:F2} yukseklik={s_CH:F2} derinlik={s_CD:F2} alt={s_CB:F2} (kayarken yukseklik x0.5)");

        // 2) Temalar → bölgeler → segmentler → olası engeller (boş sahnede örneklenir, kaydedilmez).
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var engelOnbellek = new Dictionary<GameObject, EngelTipi>();
        var temalar = AssetDatabase.FindAssets("t:ThemeData").Select(g => AssetDatabase.LoadAssetAtPath<ThemeData>(AssetDatabase.GUIDToAssetPath(g))).Where(t => t != null).ToList();
        Yaz($"HIZ ILERLEMESI: {s_MinSpeed} m/s baslangic, {(s_MinSpeed + s_MaxSpeed) * 0.5f} m/s ~{((s_MaxSpeed - s_MinSpeed) * 0.5f / 0.2f):F0}. sn, {s_MaxSpeed} m/s {((s_MaxSpeed - s_MinSpeed) / 0.2f):F0}. sn (TrackManager k_Acceleration=0.2)");
        var okunan = new HashSet<GameObject>();
        float varsayilanSerit = s_LaneOffset;
        foreach (var tema in temalar)
        {
            // temaya özel şerit aralığı (ThemeData.seritAraligi; 0 = sahnedeki TrackManager.laneOffset)
            s_LaneOffset = tema.seritAraligi > 0 ? tema.seritAraligi : varsayilanSerit;
            if (tema.zones.All(z => z.length == 0)) { Yaz($"TEMA {tema.themeName} atlandi: bolge uzunluklari 0 (yalniz egitim, engeller elle yerlesir)"); continue; }
            var bolgeler = new List<(int uzunluk, SegmentTipi[] seg)>();
            var segOnbellek = new Dictionary<GameObject, SegmentTipi>();
            foreach (var z in tema.zones)
            {
                var list = new List<SegmentTipi>();
                foreach (var r in z.prefabList)
                {
                    var go = r.editorAsset as GameObject; if (go == null) { Yaz($"UYARI {tema.themeName}: cozulemeyen segment"); continue; }
                    if (!segOnbellek.TryGetValue(go, out var st)) segOnbellek[go] = st = SegmentOku(go, engelOnbellek, okunan.Add(go));
                    list.Add(st);
                }
                bolgeler.Add((z.length, list.ToArray()));
            }
            Yaz("");
            Yaz($"=== TEMA {tema.themeName} === serit={s_LaneOffset} m (degisim {s_LaneOffset / s_LaneSpeed:F3} s) bolge: " + string.Join(" | ", bolgeler.Select(b => $"{b.uzunluk} m [{string.Join(",", b.seg.Select(s => $"{s.ad} {s.uzunluk:F1}m konum={string.Join("/", s.konumMesafe.Select(k => k.ToString("F1")))}"))}]")));
            foreach (var e in engelOnbellek.Values.Where(e => bolgeler.Any(b => b.seg.Any(s => s.engeller.Contains(e) || (s.tasarim != null && s.tasarim.Any(t => t.engel.Any(x => x.e == e)))))).OrderBy(e => e.ad))
                Yaz($"  ENGEL {e.ad} ({e.tur}) x={e.b.min.x:F2}..{e.b.max.x:F2} y={e.b.min.y:F2}..{e.b.max.y:F2} z={e.b.min.z:F2}..{e.b.max.z:F2} tetikleyici={e.tetikSayisi} yok_sayilan=[{e.yokSayilan}]{(e.uzun ? " UZUN" : "")}{(float.IsNaN(e.rampaBas) ? "" : $" rampa_bas={e.rampaBas:F2}")}{(e.tavan > 0 ? $" tavan={e.tavan:F2}" : "")}{(e.hiz > 0 ? $" hareket={e.hiz} m/s {e.baslama} m kala" : "")}");
            var siralar = Uret(bolgeler, segSayisi, tohum);
            foreach (float v in new[] { s_MinSpeed, (s_MinSpeed + s_MaxSpeed) * 0.5f, s_MaxSpeed })
                Degerlendir(tema.themeName, siralar, v);
        }
        Yaz("");
        Yaz($"ALTIN SORUNU TOPLAM (tasarlanmis parcalar): {s_AltinSorun}");
    }

    static SegmentTipi SegmentOku(GameObject prefab, Dictionary<GameObject, EngelTipi> onbellek, bool yaz)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var ts = inst.GetComponent<TrackSegment>();
        var pts = Enumerable.Range(0, ts.pathParent.childCount).Select(i => ts.pathParent.GetChild(i).position).ToArray();
        var cum = new float[pts.Length];
        for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        float Mesafe(float t) // TrackSegment.GetPointAt ile aynı indeksleme, yay uzunluğuna çevrilmiş
        {
            float st = (pts.Length - 1) * Mathf.Clamp01(t); int idx = Mathf.FloorToInt(st);
            if (idx >= pts.Length - 1) return cum[pts.Length - 1];
            return cum[idx] + (st - idx) * (cum[idx + 1] - cum[idx]);
        }
        var seg = new SegmentTipi
        {
            ad = prefab.name, uzunluk = cum[pts.Length - 1],
            konumMesafe = ts.obstaclePositions.Select(Mesafe).ToArray(),
            engeller = ts.possibleObstacles.Select(r => EngelOku(r.editorAsset as GameObject, onbellek)).Where(e => e != null).ToArray()
        };
        Object.DestroyImmediate(inst);
        var tas = prefab.GetComponent<TasarimliSatir>(); // prefab varlığındaki veri (örnek silindi)
        if (tas != null)
        {
            // TrackManager.SpawnTasarimli: Random yok; satır t = konum / uzunluk, şerit başına bir örnek.
            seg.tasarim = tas.satirlar.GroupBy(r => r.konum).OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Select(r => (EngelOku(r.engel.editorAsset as GameObject, onbellek), r.seritler)).ToList())).ToList();
            if (yaz) TasarimAltinDenetle(seg, tas);
        }
        return seg;
    }

    // Tasarlanmış altınlar, engeller sahnede yerleştirilerek (gerçek çarpıştırıcılar) denetlenir:
    //  engel_ici: altın kutusu bir katman 9 tetikleyicisine değiyor; yuzey_alti: yürünen yüzeyin (tavan/rampa) altında;
    //  ulasilamaz: altındaki yüzeyden (yoksa yoldan) zıplama tepesinde bile karakter kutusuna değmeyecek kadar yüksekte.
    static Bounds? s_AltinKutu;
    static void TasarimAltinDenetle(SegmentTipi seg, TasarimliSatir tas)
    {
        if (s_AltinKutu == null)
        {
            var ap = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityTechnologies/Endless Runner/Prefabs/Pickup.prefab");
            var ai = (GameObject)PrefabUtility.InstantiatePrefab(ap); ai.transform.position = Vector3.zero; Physics.SyncTransforms();
            var cs = ai.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToArray();
            Bounds ab = cs.Length > 0 ? cs[0].bounds : new Bounds(Vector3.zero, Vector3.one * 0.5f); foreach (var c in cs) ab.Encapsulate(c.bounds);
            s_AltinKutu = ab; Object.DestroyImmediate(ai);
            Yaz($"ALTIN carpisma kutusu: x={ab.min.x:F2}..{ab.max.x:F2} y={ab.min.y:F2}..{ab.max.y:F2} z={ab.min.z:F2}..{ab.max.z:F2}");
        }
        // engeller: segment girişi z=0, şerit x = lane * laneOffset (düz yol)
        var yerlesen = new List<GameObject>();
        foreach (var r in tas.satirlar)
            for (int l = -1; l <= 1; l++)
                if ((r.seritler & (1 << (l + 1))) != 0)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(r.engel.editorAsset as GameObject);
                    go.transform.position = new Vector3(l * s_LaneOffset, 0f, r.konum); yerlesen.Add(go);
                }
        Physics.SyncTransforms();
        Bounds k = s_AltinKutu.Value; int adet = 0, cakisma = 0, alt = 0, yuksek = 0; var ornek = new List<string>();
        float erisim = s_JumpH + s_CB + s_CH; // zıplama tepesinde karakter kutusunun üstü (yüzeye göre)
        foreach (var a in tas.altinlar)
            for (float m = a.baslangic; m <= a.bitis + 0.001f; m += TasarimliSatir.k_AltinAraligi)
            {
                float o = a.bitis > a.baslangic ? (m - a.baslangic) / (a.bitis - a.baslangic) : 0f;
                float lane = TasarimliSatir.AltinSeridi(a, o), y = TasarimliSatir.AltinYuksekligi(a, o);
                Vector3 c = k.center + new Vector3(lane * s_LaneOffset, y, m); adet++;
                if (Physics.CheckBox(c, k.extents, Quaternion.identity, 1 << k_EngelKatmani, QueryTriggerInteraction.Collide))
                { cakisma++; if (ornek.Count < 4) ornek.Add($"engel_ici {m:F1}m serit{lane:F1} y{y:F2}"); }
                // yüzey: kesirli şeritte (tavandan tavana geçiş) iki komşu şeridin yüksek olanı
                float yuzey = 0f;
                foreach (float ls in new[] { Mathf.Floor(lane), Mathf.Ceil(lane) })
                    if (Physics.Raycast(new Vector3(ls * s_LaneOffset, 8f, m), Vector3.down, out RaycastHit hit, 9f, 1 << k_ZeminKatmani, QueryTriggerInteraction.Collide)) yuzey = Mathf.Max(yuzey, hit.point.y);
                if (y < yuzey - 0.05f) { alt++; if (ornek.Count < 4) ornek.Add($"yuzey_alti {m:F1}m serit{lane:F1} y{y:F2} yuzey{yuzey:F2}"); }
                else if (y + k.min.y > yuzey + erisim) { yuksek++; if (ornek.Count < 4) ornek.Add($"ulasilamaz {m:F1}m serit{lane:F1} y{y:F2} yuzey{yuzey:F2}"); }
            }
        foreach (var go in yerlesen) Object.DestroyImmediate(go);
        s_AltinSorun += cakisma + alt + yuksek;
        Yaz($"TASARIM {seg.ad} ({seg.uzunluk:F1} m): satir={string.Join(" ; ", seg.tasarim.Select(t => $"{t.m}m " + string.Join("+", t.engel.Select(x => x.e.ad.Replace("Obstacle", "") + "/" + x.maske))))} | altin={adet} engel_ici_altin={cakisma} yuzey_alti={alt} ulasilamaz={yuksek} {string.Join(" | ", ornek)}");
    }

    static EngelTipi EngelOku(GameObject prefab, Dictionary<GameObject, EngelTipi> onbellek)
    {
        if (prefab == null) return null;
        if (onbellek.TryGetValue(prefab, out var e)) return e;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        // Oyun yalnızca katman 9'daki tetikleyicileri engel çarpışması sayar (CharacterCollider.OnTriggerEnter,
        // k_ObstacleLayerIndex = 9). Gölge vb. diğer çarpıştırıcılar karakteri durdurmaz, hesaba katılmaz.
        var tum = inst.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToArray();
        var cols = tum.Where(c => c.isTrigger && c.gameObject.layer == k_EngelKatmani).ToArray();
        var zemin = tum.Where(c => c.gameObject.layer == k_ZeminKatmani).ToArray();
        string yokSayilan = string.Join(",", tum.Except(cols).Except(zemin).Select(c => $"{c.gameObject.name}(L{c.gameObject.layer},{(c.isTrigger ? "tetik" : "kati")})"));
        if (cols.Length == 0) Yaz($"UYARI {prefab.name}: katman {k_EngelKatmani} tetikleyici yok");
        Bounds b = cols.Length > 0 ? cols[0].bounds : new Bounds(Vector3.zero, Vector3.zero);
        foreach (var c in cols) b.Encapsulate(c.bounds);
        var ob = inst.GetComponent<Obstacle>();
        e = new EngelTipi { ad = prefab.name, tur = ob != null ? ob.GetType().Name : "?", b = b, yokSayilan = yokSayilan, tetikSayisi = cols.Length };
        e.uzun = b.size.z > k_UzunEngel;
        var rz = zemin.FirstOrDefault(c => c.gameObject.name == "RampaZemin");
        if (rz != null) e.rampaBas = rz.bounds.min.z;
        var tz = zemin.FirstOrDefault(c => c.gameObject.name == "TavanZemin");
        if (tz != null) e.tavan = tz.bounds.max.y;
        var ht = inst.GetComponent<HareketliTramvay>();
        if (ht != null) { e.hiz = ht.hiz; e.baslama = ht.baslamaMesafesi; }
        Object.DestroyImmediate(inst);
        onbellek[prefab] = e;
        return e;
    }

    // TrackManager.SpawnNewSegment + SpawnObstacle kurallarının taklidi (Random.Range dağılımlarıyla aynı).
    static List<Sira> Uret(List<(int uzunluk, SegmentTipi[] seg)> bolgeler, int segSayisi, int tohum)
    {
        var rnd = new System.Random(tohum);
        var siralar = new List<Sira>();
        float d = 0, bolgeMesafe = 0; int bolge = 0, guvenli = 2;
        int onceki = -1, tekrar = 0, tekrarCokluBolge = 0, cokluBolgeSeg = 0, ham = 0; // TrackManager.m_PreviousSegment taklidi
        for (int n = 0; n < segSayisi; n++)
        {
            if (bolgeler[bolge].uzunluk < bolgeMesafe) { bolge = (bolge + 1) % bolgeler.Count; bolgeMesafe = 0; onceki = -1; }
            var havuz = bolgeler[bolge].seg;
            int sec = rnd.Next(havuz.Length); // tek Random çağrısı (oyundaki gibi)
            if (sec == onceki) { ham++; if (!s_EskiSecim) sec = (sec + 1) % havuz.Length; }
            if (sec == onceki) { tekrar++; if (havuz.Length > 1) tekrarCokluBolge++; }
            if (havuz.Length > 1) cokluBolgeSeg++;
            onceki = sec;
            var s = havuz[sec];
            if (guvenli > 0) guvenli--;
            else if (s.tasarim != null)
            {
                // Tasarlanmış parça: sabit şeritler, Random çağrısı yok (TrackManager.SpawnTasarimli ile aynı).
                // Ayna (segmentin rastgele çevrilmesi) şeritleri simetrik değiştirir; değerlendirme simetrik olduğu için ayrıca üretilmez.
                foreach (var (sm, engel) in s.tasarim) { siralar.Add(TasarimSira(s, d + sm, engel)); s_Nokta++; s_Yerlesen++; }
            }
            else
            {
                s_Nokta += s.konumMesafe.Length;
                if (s.engeller.Length > 0)
                    for (int i = 0; i < s.konumMesafe.Length; i++)
                    {
                        var e = s.engeller[rnd.Next(s.engeller.Length)];
                        siralar.Add(SiraKur(e, s, d + s.konumMesafe[i], rnd));
                        s_Yerlesen++;
                    }
            }
            d += s.uzunluk; bolgeMesafe += s.uzunluk;
        }
        siralar.Sort((a, b) => a.d.CompareTo(b.d));
        // Üretim özeti: konum (cm), engel ve kapalı şeritlerden FNV-1a karması — üretim değişmeden kalırsa birebir aynı olmalı.
        uint h = 2166136261;
        foreach (var sr in siralar) foreach (char ch in $"{Mathf.RoundToInt(sr.d * 100)}|{sr.engel}|{string.Join("", sr.serit.Select(x => x == Eylem.Kapali ? "X" : "."))};") { h ^= ch; h *= 16777619; }
        Yaz($"URETIM OZETI: satir={siralar.Count} karma={h:X8} engeller=[{string.Join(", ", siralar.GroupBy(x => x.engel).OrderBy(g => g.Key).Select(g => $"{g.Key.Replace("Obstacle", "")}={g.Count()}"))}]");
        Yaz($"ENGEL NOKTASI: toplam={s_Nokta} engel_konan={s_Yerlesen} engelsiz={(s_Nokta - s_Yerlesen)} (%{(s_Nokta == 0 ? 0 : 100f * (s_Nokta - s_Yerlesen) / s_Nokta):F2}) [ilk 2 guvenli segment haric]");
        s_Nokta = s_Yerlesen = 0;
        Yaz($"SEGMENT TEKRAR: art arda ayni segment={tekrar} (2+ segmentli bolgelerde={tekrarCokluBolge}, bu bolgelerdeki segment={cokluBolgeSeg}) | kuralin degistirdigi secim={ham}");
        return siralar;
    }

    // Aynı metredeki tasarlanmış engeller tek satır; her şerit kendi engeliyle (kutu, aralık, rampa, hareket).
    static Sira TasarimSira(SegmentTipi s, float d, List<(EngelTipi e, int maske)> engel)
    {
        Bounds b = engel[0].e.b; var kapali = new bool[3]; float zMin = float.MaxValue, zMax = float.MinValue;
        var sr = new Sira { d = d, segment = s.ad };
        bool kisaVar = false; float kMin = float.MaxValue, kMax = float.MinValue;
        var ilk = new bool[3];
        foreach (var (e, maske) in engel)
        {
            for (int l = -1; l <= 1; l++)
            {
                if ((maske & (1 << (l + 1))) == 0) continue;
                float xo = e.tur == nameof(AllLaneObstacle) ? 0f : l * s_LaneOffset;
                var yeni = new bool[3];
                if (e.tur == nameof(PatrollingObstacle)) yeni[0] = yeni[1] = yeni[2] = true; else SeritIsaretle(yeni, e.b, xo);
                for (int k = 0; k < 3; k++)
                {
                    if (!yeni[k]) continue;
                    kapali[k] = true;
                    var kb = new Bounds(new Vector3(0, e.b.center.y, 0), new Vector3(0, e.b.size.y, 0));
                    float z0 = d + e.b.min.z, z1 = d + e.b.max.z;
                    if (!ilk[k]) { ilk[k] = true; sr.sb[k] = kb; sr.kz0[k] = z0; sr.kz1[k] = z1; }
                    else { sr.sb[k].Encapsulate(kb); sr.kz0[k] = Mathf.Min(sr.kz0[k], z0); sr.kz1[k] = Mathf.Max(sr.kz1[k], z1); }
                    sr.uzun[k] |= e.uzun;
                    if (!float.IsNaN(e.rampaBas)) { sr.rampa[k] = true; sr.rampaBas[k] = d + e.rampaBas; }
                    if (e.hiz > 0) { sr.hiz[k] = e.hiz; sr.baslama[k] = e.baslama; }
                    if (e.tavan > 0 && e.hiz <= 0) sr.tavan[k] = true; // yürünen tavan (hareketli tramvay hariç)
                }
            }
            b.Encapsulate(e.b); zMin = Mathf.Min(zMin, d + e.b.min.z); zMax = Mathf.Max(zMax, d + e.b.max.z);
            if (!e.uzun) { kisaVar = true; kMin = Mathf.Min(kMin, d + e.b.min.z); kMax = Mathf.Max(kMax, d + e.b.max.z); }
        }
        sr.engel = string.Join("+", engel.Select(x => x.e.ad));
        sr.tur = engel.Any(x => x.e.tur == nameof(AllLaneObstacle)) ? nameof(AllLaneObstacle) : engel[0].e.tur;
        sr.b = b; sr.zMin = zMin; sr.zMax = zMax;
        // yalnız uzun engelli satır (ör. yalnız tramvay): boş şeritlerinde kısıt yoktur; çiftlerde önceki kısa engelli satıra bakılır
        sr.zMinKisa = kisaVar ? kMin : float.PositiveInfinity; sr.zMaxKisa = kisaVar ? kMax : float.NegativeInfinity;
        for (int l = 0; l < 3; l++) sr.serit[l] = kapali[l] ? Eylem.Kapali : Eylem.Serbest;
        return sr;
    }

    static Sira SiraKur(EngelTipi e, SegmentTipi s, float d, System.Random rnd)
    {
        var sr = new Sira { d = d, segment = s.ad, engel = e.ad, tur = e.tur, b = e.b, zMin = d + e.b.min.z, zMax = d + e.b.max.z };
        var kapali = new bool[3];
        if (e.tur == nameof(SimpleBarricade))
        {
            int count = rnd.Next(1, 3), start = rnd.Next(-1, 2);
            for (int i = 0; i < count; i++) { int lane = start + i; if (lane > 1) lane = -1; SeritIsaretle(kapali, e.b, lane * s_LaneOffset); }
        }
        else if (e.tur == nameof(Missile))
        {
            int lane = rnd.Next(-1, 2); SeritIsaretle(kapali, e.b, lane * s_LaneOffset);
        }
        else if (e.tur == nameof(PatrollingObstacle)) { kapali[0] = kapali[1] = kapali[2] = true; } // şeritler arasında gidip gelir: her şerit tehlikeli sayılır
        else SeritIsaretle(kapali, e.b, 0f); // AllLaneObstacle: kendi genişliği
        for (int l = 0; l < 3; l++)
        {
            sr.serit[l] = kapali[l] ? Eylem.Kapali : Eylem.Serbest;
            sr.sb[l] = e.b; sr.kz0[l] = sr.zMin; sr.kz1[l] = sr.zMax; // rastgele (Day/Night) satırlarda uzun engel kuralı yok
        }
        sr.zMinKisa = sr.zMin; sr.zMaxKisa = sr.zMax;
        return sr;
    }

    static void SeritIsaretle(bool[] kapali, Bounds b, float xOff)
    {
        for (int l = 0; l < 3; l++)
        {
            float cx = (l - 1) * s_LaneOffset;
            if (cx + s_CW * 0.5f > b.min.x + xOff && cx - s_CW * 0.5f < b.max.x + xOff) kapali[l] = true;
        }
    }

    // Bir engel kutusu (y aralığı) ve mesafe aralığı için hızdaki zıplama/kayma pencereleri (m). -1: mümkün değil.
    static void Pencere(Bounds bb, float z0, float z1, float v, out float zipla, out float kay, out float s1Zipla)
    {
        float r = (v - s_MinSpeed) / (s_MaxSpeed - s_MinSpeed);
        float J = s_JumpLen * (1 + r), S = s_SlideLen * (1 + r);
        float Lo = (z1 - z0) + s_CD;
        zipla = -1; kay = -1; s1Zipla = 0;
        float k = (bb.max.y - s_CB) / s_JumpH; // karakterin altı engelin üstünü geçmeli
        if (k <= 0) { zipla = J; s1Zipla = 0; }
        else if (k < 1) { float s1 = J / Mathf.PI * Mathf.Asin(k); float w = (J - 2 * s1) - Lo; if (w > 0) { zipla = w; s1Zipla = s1; } }
        if (bb.min.y > s_CB + s_CH * 0.5f) kay = S - Lo; // kayarken çarpışma kutusunun üstü engelin altında kalmalı
        if (kay <= 0) kay = -1;
    }

    class Sonuc { public int toplam, imkansiz, gercekdisi, zor, rahat, kacisYok, darZipla, darKay; public Dictionary<string, List<string>> ornek = new Dictionary<string, List<string>>(); public List<float> paylar = new List<float>();
        public Dictionary<string, int> katImk = new Dictionary<string, int>(), katGd = new Dictionary<string, int>(), katZor = new Dictionary<string, int>(); }

    // Bir hızdaki satır görünümü: hareketli engeller kaydırılmış, şerit eylemleri ve uzun engel kapamaları hesaplanmış.
    class Gorunum
    {
        public Sira s; public float d; public float[] z0 = new float[3], z1 = new float[3];
        public Eylem[] e = new Eylem[3]; public float[] zp = new float[3], kp = new float[3], s1 = new float[3];
        public float[] ekSon = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity }; // başka sıranın uzun engeli
        public float[] ekBas = { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
    }

    static void Degerlendir(string tema, List<Sira> siralar, float v)
    {
        var res = new Sonuc();
        float r = (v - s_MinSpeed) / (s_MaxSpeed - s_MinSpeed);
        float J = s_JumpLen * (1 + r);
        float tSerit = s_LaneOffset / s_LaneSpeed;
        float dusus = v * Mathf.Sqrt(2f * IstanbulParkur.k_Tavan / k_Yercekimi); // tavandan inerken geçen mesafe
        var G = new List<Gorunum>(siralar.Count);
        foreach (var s in siralar)
        {
            var g = new Gorunum { s = s, d = s.d };
            for (int l = 0; l < 3; l++)
            {
                g.z0[l] = s.kz0[l]; g.z1[l] = s.kz1[l];
                if (s.serit[l] == Eylem.Kapali && s.hiz[l] > 0)
                {
                    // karşıdan gelen: oyuncu burnun D m gerisindeyken kalkar, (v+u) ile yaklaşır
                    float u = s.hiz[l], kay = u * s.baslama[l] / (v + u), boy = (s.kz1[l] - s.kz0[l]) * v / (v + u);
                    g.z0[l] = s.kz0[l] - kay; g.z1[l] = g.z0[l] + boy; g.d = Mathf.Min(g.d, g.z0[l]);
                }
            }
            G.Add(g);
        }
        G = G.OrderBy(x => x.d).ToList(); // kararlı: eşit mesafedeki satırlar özgün sırada kalır
        // şerit eylemleri (bu hızda)
        for (int i = 0; i < G.Count; i++)
        {
            var g = G[i];
            for (int l = 0; l < 3; l++)
            {
                if (g.s.serit[l] == Eylem.Serbest) { g.e[l] = Eylem.Serbest; continue; }
                if (g.s.rampa[l]) { g.e[l] = Eylem.Rampa; continue; }
                Pencere(g.s.sb[l], g.z0[l], g.z1[l], v, out g.zp[l], out g.kp[l], out g.s1[l]);
                g.e[l] = g.zp[l] > 0 ? Eylem.Zipla : g.kp[l] > 0 ? Eylem.Kay : Eylem.Kapali;
            }
        }
        // Tavan zincirleri: rampalı tramvayın tavanına çıkılır; aynı şeritte hemen ardından gelen vagonun (≤1 m ara)
        // ve yandaki şeritte en az 2 m örtüşen tavanın üstüne geçilir. Ulaşılabilen tavanlar o şeritte SpanRampa olur;
        // yerden girişi yalnız zincirin rampasından (rampa başlamadan) mümkündür, yandan geçilen tavana yerden girilmez.
        var tavanlar = new List<(int j, int l, float z0, float z1, bool rampa, float giris)>();
        for (int j = 0; j < G.Count; j++)
            for (int l = 0; l < 3; l++)
                if (G[j].s.tavan[l] && G[j].s.serit[l] == Eylem.Kapali)
                    tavanlar.Add((j, l, G[j].z0[l], G[j].z1[l], G[j].s.rampa[l], G[j].s.rampa[l] ? G[j].s.rampaBas[l] : float.NaN));
        bool degisti = true;
        while (degisti)
        {
            degisti = false;
            for (int k = 0; k < tavanlar.Count; k++)
            {
                if (!float.IsNaN(tavanlar[k].giris)) continue;
                var t = tavanlar[k]; float g = float.NaN;
                foreach (var p in tavanlar)
                {
                    if (float.IsNaN(p.giris)) continue;
                    if (p.l == t.l && t.z0 >= p.z1 - 0.01f && t.z0 - p.z1 <= 1f) { g = float.IsNaN(g) ? p.giris : Mathf.Min(g, p.giris); }
                    else if (Mathf.Abs(p.l - t.l) == 1 && Mathf.Min(p.z1, t.z1) - Mathf.Max(p.z0, t.z0) >= 2f && float.IsNaN(g)) g = float.NegativeInfinity;
                }
                if (!float.IsNaN(g)) { tavanlar[k] = (t.j, t.l, t.z0, t.z1, t.rampa, g); degisti = true; }
            }
        }
        var ulasilir = new Dictionary<(int, int), float>();
        foreach (var t in tavanlar) if (!float.IsNaN(t.giris)) { ulasilir[(t.j, t.l)] = t.giris; if (!t.rampa) { G[t.j].e[t.l] = Eylem.SpanRampa; G[t.j].ekBas[t.l] = Mathf.Min(G[t.j].ekBas[t.l], t.giris); } }
        // uzun engeller, aralıklarına düşen diğer sıraların şeritlerini de kapatır (ulaşılabilen tavan: SpanRampa)
        for (int j = 0; j < G.Count; j++)
            for (int l = 0; l < 3; l++)
            {
                var gj = G[j]; if (!gj.s.uzun[l] || gj.s.serit[l] != Eylem.Kapali) continue;
                bool tavanli = ulasilir.TryGetValue((j, l), out float giris);
                float bas = gj.s.rampa[l] ? gj.s.rampaBas[l] : gj.z0[l];
                for (int i = 0; i < G.Count; i++)
                {
                    if (i == j) continue; var gi = G[i];
                    if (gi.d > gj.z1[l] + 1f) { if (i > j) break; else continue; }
                    if (gi.d < bas - 1f) continue;
                    if (gi.d >= bas && gi.d <= gj.z1[l])
                    {
                        if (gi.e[l] != Eylem.Kapali) gi.e[l] = tavanli ? Eylem.SpanRampa : Eylem.Kapali;
                        gi.ekSon[l] = Mathf.Max(gi.ekSon[l], gj.z1[l]); gi.ekBas[l] = Mathf.Min(gi.ekBas[l], tavanli ? Mathf.Min(bas, giris) : bas);
                    }
                }
            }
        var oncekiKisa = new int[G.Count]; int sonKisa = -1;
        for (int i = 0; i < G.Count; i++) { oncekiKisa[i] = sonKisa; if (!float.IsInfinity(G[i].s.zMaxKisa)) sonKisa = i; }
        float Son(Gorunum g, int l) => Mathf.Max(g.s.serit[l] == Eylem.Kapali ? g.z1[l] : g.s.zMaxKisa, g.ekSon[l]);
        float Bas(Gorunum g, int l) => Mathf.Min(g.s.serit[l] == Eylem.Kapali ? (g.s.rampa[l] ? g.s.rampaBas[l] : g.z0[l]) : g.s.zMinKisa, g.ekBas[l]);
        bool Tavan(Eylem e) => e == Eylem.Rampa || e == Eylem.SpanRampa;

        for (int i = 0; i < G.Count; i++)
        {
            var g = G[i];
            bool kacis = g.e.Any(x => x != Eylem.Kapali && x != Eylem.SpanRampa) || g.e.Any(x => x == Eylem.SpanRampa);
            if (!kacis) { res.kacisYok++; Ornek(res, $"KACIS YOK: {g.s.engel} @ {g.d:F0} m ({g.s.segment})"); }
            if (!g.e.Contains(Eylem.Serbest) && !g.e.Contains(Eylem.Rampa))
            {
                int lz = System.Array.IndexOf(g.e, Eylem.Zipla), lk = System.Array.IndexOf(g.e, Eylem.Kay);
                if (lz >= 0 && g.zp[lz] / v < k_DarPencere) res.darZipla++;
                if (lk >= 0 && g.kp[lk] / v < k_DarPencere) res.darKay++;
            }
        }
        // ardışık çiftler: en iyi yolun zaman payı
        for (int i = 1; i < G.Count; i++)
        {
            var a0 = G[i - 1]; var b = G[i];
            bool aUzunTek = float.IsInfinity(a0.s.zMaxKisa);
            float enIyi = float.NegativeInfinity; string yol = "";
            for (int l1 = 0; l1 < 3; l1++)
            {
                if (a0.e[l1] == Eylem.Kapali) continue;
                // yalnız uzun engelli satırın boş şeridi: oyuncunun son eylemi bir önceki kısa engelli satırdaydı
                var a = a0;
                if (aUzunTek && a0.e[l1] == Eylem.Serbest && oncekiKisa[i - 1] >= 0 && G[oncekiKisa[i - 1]].e[l1] != Eylem.Kapali) a = G[oncekiKisa[i - 1]];
                var e1 = a.e[l1];
                // zıplamadan sonra yeni zıplama ancak inişten sonra: en erken başlayan zıplamanın inişi
                float inis = e1 == Eylem.Zipla ? (a.z0[l1] - s_CD * 0.5f) - (J - a.s1[l1] - (a.z1[l1] - a.z0[l1] + s_CD)) + J : float.NegativeInfinity;
                for (int l2 = 0; l2 < 3; l2++)
                {
                    var e2 = b.e[l2]; if (e2 == Eylem.Kapali) continue;
                    if (l1 != l2 && Mathf.Abs(l1 - l2) == 1 && Tavan(e1) && e2 == Eylem.SpanRampa)
                    {
                        // tavandan komşu tavana (aynı yükseklik, örtüşen katarlar): zaman sınırı yok
                        if (float.PositiveInfinity > enIyi) { enIyi = float.PositiveInfinity; yol = $"{"SOS"[l1]}:{e1}->{"SOS"[l2]}:{e2} (tavandan tavana)"; }
                        continue;
                    }
                    int lo = Mathf.Min(l1, l2), hi = Mathf.Max(l1, l2);
                    float x1Son = float.NegativeInfinity, x2Bas = float.PositiveInfinity;
                    for (int l = lo; l <= hi; l++) { x1Son = Mathf.Max(x1Son, Mathf.Max(Son(a, l), a0.s.serit[l] == Eylem.Kapali || a0.ekSon[l] > float.NegativeInfinity ? Son(a0, l) : float.NegativeInfinity)); x2Bas = Mathf.Min(x2Bas, Bas(b, l)); }
                    if (l1 == l2 && Tavan(e1) && (e2 == Eylem.Zipla || e2 == Eylem.Kay)) x1Son += dusus;
                    x1Son += s_CD * 0.5f; x2Bas -= s_CD * 0.5f;
                    float serit = (hi - lo) * tSerit;
                    float pay = (x2Bas - x1Son) / v - serit; // şerit değişimi iki engel arasında bitmeli
                    if (e2 == Eylem.Zipla)
                    {
                        float sonZipla = x2Bas - b.s1[l2]; // en geç zıplama başlangıcı
                        float serbest = Mathf.Max(x1Son, e1 == Eylem.Zipla ? inis : x1Son);
                        pay = Mathf.Min(pay, (sonZipla - serbest) / v);
                    }
                    // Kay: en geç başlangıç x2Bas; zıplama/kayma birbirini iptal edebildiği için ek bekleme yok
                    string not = "";
                    if (l1 == l2 && e1 == e2 && (e1 == Eylem.Zipla || e1 == Eylem.Kay))
                    {
                        // Tek hareketle ikisini birden geçmek (ikinci dokunuş yok): birleşik engelin zamanlama penceresi
                        var bb = new Bounds();
                        if (e1 == Eylem.Zipla) bb.SetMinMax(new Vector3(0, Mathf.Max(a.s.sb[l1].min.y, b.s.sb[l2].min.y), 0), new Vector3(0, Mathf.Max(a.s.sb[l1].max.y, b.s.sb[l2].max.y), 0));
                        else bb.SetMinMax(new Vector3(0, Mathf.Min(a.s.sb[l1].min.y, b.s.sb[l2].min.y), 0), new Vector3(0, Mathf.Max(a.s.sb[l1].max.y, b.s.sb[l2].max.y), 0));
                        Pencere(bb, a.z0[l1], b.z1[l2], v, out float bz, out float bk, out _);
                        float w = (e1 == Eylem.Zipla ? bz : bk) / v;
                        if (w > pay) { pay = w; not = " (tek hareket)"; }
                    }
                    // İkinci engel oyuncunun şeridini kapatmıyorsa (aynı şeritte kal, yeni dokunuş yok) zaman sınırı yoktur;
                    // rampaya kendi şeridinden girmek ve tavanda kalmak da dokunuş istemez.
                    if (l1 == l2 && (e2 == Eylem.Serbest || e2 == Eylem.Rampa || e2 == Eylem.SpanRampa)) { pay = float.PositiveInfinity; not = " (dokunus gerekmez)"; }
                    if (pay > enIyi) { enIyi = pay; yol = $"{"SOS"[l1]}:{e1}->{"SOS"[l2]}:{e2}{not}"; }
                }
            }
            if (float.IsNegativeInfinity(enIyi)) continue; // kaçışsız satır zaten sayıldı
            res.toplam++; res.paylar.Add(enIyi);
            string Ser(Gorunum g) => string.Join("", g.e.Select(x => x == Eylem.Kapali ? "X" : x == Eylem.Zipla ? "z" : x == Eylem.Kay ? "k" : x == Eylem.Rampa ? "R" : x == Eylem.SpanRampa ? "t" : "."));
            var a_ = a0; string ac = $"{a_.s.engel}({Ser(a_)}) @ {a_.d:F0} m -> {b.s.engel}({Ser(b)}) @ {b.d:F0} m [{b.s.segment}], pay {enIyi:F2} sn, yol {yol}";
            if (s_Incele.Any(x => Mathf.Abs(a0.d - x) < 1.5f)) Yaz($"   INCELE {(enIyi < 0 ? "IMKANSIZ" : enIyi < k_Gercekdisi ? "GERCEKDISI" : enIyi < k_Zor ? "ZOR" : "RAHAT")} ({v:F0} m/s): " + ac);
            string kat = Kategori(a0.s, b.s);
            if (enIyi < 0) { res.imkansiz++; Ornek(res, "IMKANSIZ: " + ac); Say(res.katImk, kat); }
            else if (enIyi < k_Gercekdisi) { res.gercekdisi++; Ornek(res, "GERCEKDISI: " + ac); Say(res.katGd, kat); }
            else if (enIyi < k_Zor) { res.zor++; Say(res.katZor, kat); if (tema == "Istanbul") Ornek(res, "ZOR: " + ac); }
            else res.rahat++;
        }
        res.paylar.Sort();
        float P(float q) => res.paylar.Count == 0 ? 0 : res.paylar[Mathf.Clamp((int)(q * res.paylar.Count), 0, res.paylar.Count - 1)];
        Yaz($"-- {tema} hiz {v:F0} m/s (oran {r:F1}): satir={siralar.Count} cift={res.toplam} | kacisYok={res.kacisYok} imkansiz={res.imkansiz} gercekdisi(<{k_Gercekdisi}s)={res.gercekdisi} zor(<{k_Zor}s)={res.zor} rahat={res.rahat} | pay min={P(0):F2} %5={P(0.05f):F2} medyan={P(0.5f):F2} sn | darPencere zipla={res.darZipla} kay={res.darKay}");
        Yaz($"   pencere (tek engel, bu hizda): " + string.Join(" ; ", siralar.GroupBy(s => s.engel).Select(g =>
        {
            var s = g.First(); int l = System.Array.FindIndex(s.serit, x => x == Eylem.Kapali); if (l < 0) l = 1;
            Pencere(s.sb[l], s.kz0[l], s.kz1[l], v, out var z, out var k, out _);
            return $"{g.Key}: zipla {(z > 0 ? (z / v).ToString("F2") + "s" : "yok")}, kay {(k > 0 ? (k / v).ToString("F2") + "s" : "yok")}";
        })));
        string Dok(Dictionary<string, int> d) => d.Count == 0 ? "-" : string.Join(", ", d.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
        Yaz($"   kategori imkansiz: {Dok(res.katImk)} | gercekdisi: {Dok(res.katGd)} | zor: {Dok(res.katZor)}");
        foreach (var kv in res.ornek) foreach (var o in kv.Value) Yaz("   ornek " + o);
    }

    // Engel çifti kategorisi: tramvay (uzun), tüm şerit bariyer (AllLaneObstacle), fare (PatrollingObstacle), şerit engeli.
    static string Kategori(Sira a, Sira b)
    {
        string T(Sira s) => s.uzun.Any(x => x) ? "tramvay" : s.tur == nameof(AllLaneObstacle) ? "bariyer" : s.tur == nameof(PatrollingObstacle) ? "fare" : "serit";
        string ta = T(a), tb = T(b);
        if (ta == "fare" || tb == "fare") return "fare+X";
        if (ta == "tramvay" || tb == "tramvay") return "tramvay+X";
        if (ta == "bariyer" && tb == "bariyer") return "bariyer-bariyer";
        if (ta == "serit" && tb == "serit") return "serit-serit";
        return "serit<->bariyer";
    }

    static void Say(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }

    static void Ornek(Sonuc r, string s) { string k = s.Substring(0, s.IndexOf(':')); if (!r.ornek.TryGetValue(k, out var l)) r.ornek[k] = l = new List<string>(); if (l.Count < (k == "ZOR" ? 6 : 3)) l.Add(s); }
}
