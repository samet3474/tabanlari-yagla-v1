// Parkur adaleti ve tepki süresi ölçümü (yalnızca editör; Editor klasöründe olduğu için oyuna/yayın derlemesine girmez).
// Oyun mantığını değiştirmez: Main sahnesinden gerçek hız/hareket değerlerini, temalardan gerçek segment ve engel
// prefablarını okur; TrackManager + SimpleBarricade/AllLaneObstacle/PatrollingObstacle/Missile üretim kurallarını taklit
// ederek binlerce segmentlik parkur üretir ve her ardışık engel çifti için kaçış yolu ile zaman payını hesaplar.
// Hiçbir sahne/prefab kaydedilmez. Çalıştırma:
//   Unity -batchmode -projectPath . -executeMethod ParkurAdaletOlcum.Calistir -parkurCikti <klasör> [-parkurSegment 20000] [-parkurTohum 1]
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

    enum Eylem { Serbest, Zipla, Kay, Kapali }

    class EngelTipi
    {
        public string ad, tur; public Bounds b; // engel kökü (lane 0) uzayında çarpışma kutusu (yalnız katman 9 tetikleyiciler)
        public string yokSayilan; public int tetikSayisi;
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
    }

    static float s_LaneOffset, s_MinSpeed, s_MaxSpeed, s_LaneSpeed, s_JumpLen, s_JumpH, s_SlideLen;
    static float s_CW, s_CH, s_CD, s_CB;
    const int k_EngelKatmani = 9;  // CharacterCollider.k_ObstacleLayerIndex ile aynı
    static bool s_EskiSecim;       // -parkurEskiSecim: m_PreviousSegment düzeltmesi öncesi segment seçimi (karşılaştırma için)
    static float[] s_Incele = new float[0]; // -parkurIncele "d1,d2": bu mesafelerden başlayan çiftleri ayrıntılı yaz
    static int s_Nokta, s_Yerlesen;  // engel noktası sayısı / engel konan nokta sayısı
    static StringBuilder s_Out = new StringBuilder();

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
        foreach (var tema in temalar)
        {
            if (tema.zones.All(z => z.length == 0)) { Yaz($"TEMA {tema.themeName} atlandi: bolge uzunluklari 0 (yalniz egitim, engeller elle yerlesir)"); continue; }
            var bolgeler = new List<(int uzunluk, SegmentTipi[] seg)>();
            foreach (var z in tema.zones)
            {
                var list = new List<SegmentTipi>();
                foreach (var r in z.prefabList)
                {
                    var go = r.editorAsset as GameObject; if (go == null) { Yaz($"UYARI {tema.themeName}: cozulemeyen segment"); continue; }
                    list.Add(SegmentOku(go, engelOnbellek));
                }
                bolgeler.Add((z.length, list.ToArray()));
            }
            Yaz("");
            Yaz($"=== TEMA {tema.themeName} === bolge: " + string.Join(" | ", bolgeler.Select(b => $"{b.uzunluk} m [{string.Join(",", b.seg.Select(s => $"{s.ad} {s.uzunluk:F1}m konum={string.Join("/", s.konumMesafe.Select(k => k.ToString("F1")))}"))}]")));
            foreach (var e in engelOnbellek.Values.Where(e => bolgeler.Any(b => b.seg.Any(s => s.engeller.Contains(e)))).OrderBy(e => e.ad))
                Yaz($"  ENGEL {e.ad} ({e.tur}) x={e.b.min.x:F2}..{e.b.max.x:F2} y={e.b.min.y:F2}..{e.b.max.y:F2} z={e.b.min.z:F2}..{e.b.max.z:F2} tetikleyici={e.tetikSayisi} yok_sayilan=[{e.yokSayilan}]");
            var siralar = Uret(bolgeler, segSayisi, tohum);
            foreach (float v in new[] { s_MinSpeed, (s_MinSpeed + s_MaxSpeed) * 0.5f, s_MaxSpeed })
                Degerlendir(tema.themeName, siralar, v);
        }
    }

    static SegmentTipi SegmentOku(GameObject prefab, Dictionary<GameObject, EngelTipi> onbellek)
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
        var tas = inst.GetComponent<TasarimliSatir>();
        if (tas != null)
        {
            // TrackManager.SpawnTasarimli: Random yok; satır t = konum / uzunluk, şerit başına bir örnek.
            seg.tasarim = tas.satirlar.GroupBy(r => r.konum).OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Select(r => (EngelOku(r.engel.editorAsset as GameObject, onbellek), r.seritler)).ToList())).ToList();
            TasarimAltinDenetle(seg, tas);
        }
        Object.DestroyImmediate(inst);
        return seg;
    }

    // Tasarlanmış altın desenlerinin engel kutularının (katman 9) içine düşüp düşmediği: altın çarpıştırıcısı ile.
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
        Bounds k = s_AltinKutu.Value; int adet = 0, cakisma = 0; var ornek = new List<string>();
        foreach (var a in tas.altinlar)
            for (float m = a.baslangic; m <= a.bitis + 0.001f; m += TasarimliSatir.k_AltinAraligi)
            {
                float o = a.bitis > a.baslangic ? (m - a.baslangic) / (a.bitis - a.baslangic) : 0f;
                float lane = a.tip == TasarimliSatir.AltinTipi.Capraz ? Mathf.Lerp(a.serit, a.bitisSeridi, o) : a.serit;
                float y = a.tip == TasarimliSatir.AltinTipi.Yay ? Mathf.Sin(o * Mathf.PI) * a.yukseklik : a.yukseklik;
                var ab = new Bounds(k.center + new Vector3(lane * s_LaneOffset, y, m), k.size); adet++;
                foreach (var (sm, engel) in seg.tasarim) foreach (var (e, maske) in engel)
                    for (int l = -1; l <= 1; l++)
                    {
                        if ((maske & (1 << (l + 1))) == 0) continue;
                        float xo = e.tur == nameof(AllLaneObstacle) ? 0f : l * s_LaneOffset;
                        var eb = new Bounds(e.b.center + new Vector3(xo, 0, sm), e.b.size);
                        if (eb.Intersects(ab)) { cakisma++; if (ornek.Count < 3) ornek.Add($"{e.ad}@{sm}m serit{l} <- altin {m:F1}m serit{lane:F1} y{y:F2}"); }
                    }
            }
        Yaz($"TASARIM {seg.ad}: satir={string.Join(" ; ", seg.tasarim.Select(t => $"{t.m}m " + string.Join("+", t.engel.Select(x => x.e.ad.Replace("Obstacle", "") + "/" + x.maske))))} | altin={adet} engel_ici_altin={cakisma} {string.Join(" | ", ornek)}");
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
        string yokSayilan = string.Join(",", tum.Except(cols).Select(c => $"{c.gameObject.name}(L{c.gameObject.layer},{(c.isTrigger ? "tetik" : "kati")})"));
        if (cols.Length == 0) Yaz($"UYARI {prefab.name}: katman {k_EngelKatmani} tetikleyici yok");
        Bounds b = cols.Length > 0 ? cols[0].bounds : new Bounds(Vector3.zero, Vector3.zero);
        foreach (var c in cols) b.Encapsulate(c.bounds);
        var ob = inst.GetComponent<Obstacle>();
        e = new EngelTipi { ad = prefab.name, tur = ob != null ? ob.GetType().Name : "?", b = b, yokSayilan = yokSayilan, tetikSayisi = cols.Length };
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

    // Aynı metredeki tasarlanmış engeller tek satır: kutular birleştirilir (en yüksek/alçak sınır), şeritler maskeden.
    // Not: farklı yükseklikteki engeller birleşince satır bütünüyle en zor engele göre değerlendirilir (temkinli).
    static Sira TasarimSira(SegmentTipi s, float d, List<(EngelTipi e, int maske)> engel)
    {
        Bounds b = engel[0].e.b; var kapali = new bool[3]; float zMin = float.MaxValue, zMax = float.MinValue;
        foreach (var (e, maske) in engel)
        {
            for (int l = -1; l <= 1; l++)
            {
                if ((maske & (1 << (l + 1))) == 0) continue;
                float xo = e.tur == nameof(AllLaneObstacle) ? 0f : l * s_LaneOffset;
                if (e.tur == nameof(PatrollingObstacle)) kapali[0] = kapali[1] = kapali[2] = true; else SeritIsaretle(kapali, e.b, xo);
            }
            b.Encapsulate(e.b); zMin = Mathf.Min(zMin, d + e.b.min.z); zMax = Mathf.Max(zMax, d + e.b.max.z);
        }
        var sr = new Sira { d = d, segment = s.ad, engel = string.Join("+", engel.Select(x => x.e.ad)), tur = engel.Any(x => x.e.tur == nameof(AllLaneObstacle)) ? nameof(AllLaneObstacle) : engel[0].e.tur, b = b, zMin = zMin, zMax = zMax };
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
        for (int l = 0; l < 3; l++) sr.serit[l] = kapali[l] ? Eylem.Kapali : Eylem.Serbest;
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

    // Bir engel için hızdaki zıplama/kayma pencereleri (m). -1: mümkün değil.
    static void Pencere(Sira s, float v, out float zipla, out float kay, out float s1Zipla)
    {
        float r = (v - s_MinSpeed) / (s_MaxSpeed - s_MinSpeed);
        float J = s_JumpLen * (1 + r), S = s_SlideLen * (1 + r);
        float Lo = (s.zMax - s.zMin) + s_CD;
        zipla = -1; kay = -1; s1Zipla = 0;
        float k = (s.b.max.y - s_CB) / s_JumpH; // karakterin altı engelin üstünü geçmeli
        if (k <= 0) { zipla = J; s1Zipla = 0; }
        else if (k < 1) { float s1 = J / Mathf.PI * Mathf.Asin(k); float w = (J - 2 * s1) - Lo; if (w > 0) { zipla = w; s1Zipla = s1; } }
        if (s.b.min.y > s_CB + s_CH * 0.5f) kay = S - Lo; // kayarken çarpışma kutusunun üstü engelin altında kalmalı
        if (kay <= 0) kay = -1;
    }

    class Sonuc { public int toplam, imkansiz, gercekdisi, zor, rahat, kacisYok, darZipla, darKay; public Dictionary<string, List<string>> ornek = new Dictionary<string, List<string>>(); public List<float> paylar = new List<float>();
        public Dictionary<string, int> katImk = new Dictionary<string, int>(), katGd = new Dictionary<string, int>(), katZor = new Dictionary<string, int>(); }

    static void Degerlendir(string tema, List<Sira> siralar, float v)
    {
        var res = new Sonuc();
        float r = (v - s_MinSpeed) / (s_MaxSpeed - s_MinSpeed);
        float J = s_JumpLen * (1 + r);
        float tSerit = s_LaneOffset / s_LaneSpeed;
        // her satır için şerit eylemleri (bu hızda)
        var eylem = new Eylem[siralar.Count][]; var zp = new float[siralar.Count]; var kp = new float[siralar.Count]; var s1 = new float[siralar.Count];
        for (int i = 0; i < siralar.Count; i++)
        {
            Pencere(siralar[i], v, out zp[i], out kp[i], out s1[i]);
            eylem[i] = new Eylem[3];
            for (int l = 0; l < 3; l++)
                eylem[i][l] = siralar[i].serit[l] == Eylem.Serbest ? Eylem.Serbest : zp[i] > 0 ? Eylem.Zipla : kp[i] > 0 ? Eylem.Kay : Eylem.Kapali;
            bool kacis = eylem[i].Any(x => x != Eylem.Kapali);
            if (!kacis) { res.kacisYok++; Ornek(res, $"KACIS YOK: {siralar[i].engel} @ {siralar[i].d:F0} m ({siralar[i].segment})"); }
            bool sadeceZipla = !eylem[i].Contains(Eylem.Serbest) && zp[i] > 0, sadeceKay = !eylem[i].Contains(Eylem.Serbest) && kp[i] > 0 && zp[i] <= 0;
            if (sadeceZipla && zp[i] / v < k_DarPencere) res.darZipla++;
            if (sadeceKay && kp[i] / v < k_DarPencere) res.darKay++;
        }
        // ardışık çiftler: en iyi yolun zaman payı
        for (int i = 1; i < siralar.Count; i++)
        {
            var a = siralar[i - 1]; var b = siralar[i];
            float x1Son = a.zMax + s_CD * 0.5f, x2Bas = b.zMin - s_CD * 0.5f;
            float enIyi = float.NegativeInfinity; string yol = "";
            for (int l1 = 0; l1 < 3; l1++)
            {
                var e1 = eylem[i - 1][l1]; if (e1 == Eylem.Kapali) continue;
                // zıplamadan sonra yeni zıplama ancak inişten sonra: en erken başlayan zıplamanın inişi
                float inis = e1 == Eylem.Zipla ? (a.zMin - s_CD * 0.5f) - (J - s1[i - 1] - (a.zMax - a.zMin + s_CD)) + J : float.NegativeInfinity;
                for (int l2 = 0; l2 < 3; l2++)
                {
                    var e2 = eylem[i][l2]; if (e2 == Eylem.Kapali) continue;
                    float serit = Mathf.Abs(l2 - l1) * tSerit;
                    float pay = (x2Bas - x1Son) / v - serit; // şerit değişimi iki engel arasında bitmeli
                    if (e2 == Eylem.Zipla)
                    {
                        float sonZipla = x2Bas - s1[i]; // en geç zıplama başlangıcı
                        float serbest = Mathf.Max(x1Son, e1 == Eylem.Zipla ? inis : x1Son);
                        pay = Mathf.Min(pay, (sonZipla - serbest) / v);
                    }
                    // Kay: en geç başlangıç x2Bas; zıplama/kayma birbirini iptal edebildiği için ek bekleme yok
                    string not = "";
                    if (l1 == l2 && e1 == e2 && (e1 == Eylem.Zipla || e1 == Eylem.Kay))
                    {
                        // Tek hareketle ikisini birden geçmek (ikinci dokunuş yok): birleşik engelin zamanlama penceresi
                        var birlesik = new Sira { zMin = a.zMin, zMax = b.zMax, b = new Bounds() };
                        birlesik.b.SetMinMax(new Vector3(0, Mathf.Max(a.b.min.y, b.b.min.y), 0), new Vector3(0, Mathf.Max(a.b.max.y, b.b.max.y), 0));
                        if (e1 == Eylem.Kay) birlesik.b.SetMinMax(new Vector3(0, Mathf.Min(a.b.min.y, b.b.min.y), 0), new Vector3(0, Mathf.Max(a.b.max.y, b.b.max.y), 0));
                        Pencere(birlesik, v, out float bz, out float bk, out _);
                        float w = (e1 == Eylem.Zipla ? bz : bk) / v;
                        if (w > pay) { pay = w; not = " (tek hareket)"; }
                    }
                    // İkinci engel oyuncunun şeridini kapatmıyorsa (aynı şeritte kal, yeni dokunuş yok) zaman sınırı yoktur.
                    if (l1 == l2 && e2 == Eylem.Serbest) { pay = float.PositiveInfinity; not = " (dokunus gerekmez)"; }
                    if (pay > enIyi) { enIyi = pay; yol = $"{"SOS"[l1]}:{e1}->{"SOS"[l2]}:{e2}{not}"; }
                }
            }
            if (float.IsNegativeInfinity(enIyi)) continue; // kaçışsız satır zaten sayıldı
            res.toplam++; res.paylar.Add(enIyi);
            string ac = $"{a.engel}({string.Join("", a.serit.Select(x => x == Eylem.Kapali ? "X" : "."))}) @ {a.d:F0} m -> {b.engel}({string.Join("", b.serit.Select(x => x == Eylem.Kapali ? "X" : "."))}) @ {b.d:F0} m, ara {(b.zMin - a.zMax):F1} m, pay {enIyi:F2} sn, yol {yol}";
            if (s_Incele.Any(x => Mathf.Abs(a.d - x) < 1.5f)) Yaz($"   INCELE {(enIyi < 0 ? "IMKANSIZ" : enIyi < k_Gercekdisi ? "GERCEKDISI" : enIyi < k_Zor ? "ZOR" : "RAHAT")} ({v:F0} m/s): " + ac);
            string kat = Kategori(a, b);
            if (enIyi < 0) { res.imkansiz++; Ornek(res, "IMKANSIZ: " + ac); Say(res.katImk, kat); }
            else if (enIyi < k_Gercekdisi) { res.gercekdisi++; Ornek(res, "GERCEKDISI: " + ac); Say(res.katGd, kat); }
            else if (enIyi < k_Zor) { res.zor++; Say(res.katZor, kat); }
            else res.rahat++;
        }
        res.paylar.Sort();
        float P(float q) => res.paylar.Count == 0 ? 0 : res.paylar[Mathf.Clamp((int)(q * res.paylar.Count), 0, res.paylar.Count - 1)];
        Yaz($"-- {tema} hiz {v:F0} m/s (oran {r:F1}): satir={siralar.Count} cift={res.toplam} | kacisYok={res.kacisYok} imkansiz={res.imkansiz} gercekdisi(<{k_Gercekdisi}s)={res.gercekdisi} zor(<{k_Zor}s)={res.zor} rahat={res.rahat} | pay min={P(0):F2} %5={P(0.05f):F2} medyan={P(0.5f):F2} sn | darPencere zipla={res.darZipla} kay={res.darKay}");
        Yaz($"   pencere (tek engel, bu hizda): " + string.Join(" ; ", siralar.GroupBy(s => s.engel).Select(g => { var s = g.First(); Pencere(s, v, out var z, out var k, out _); return $"{g.Key}: zipla {(z > 0 ? (z / v).ToString("F2") + "s" : "yok")}, kay {(k > 0 ? (k / v).ToString("F2") + "s" : "yok")}"; })));
        string Dok(Dictionary<string, int> d) => d.Count == 0 ? "-" : string.Join(", ", d.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
        Yaz($"   kategori imkansiz: {Dok(res.katImk)} | gercekdisi: {Dok(res.katGd)} | zor: {Dok(res.katZor)}");
        foreach (var kv in res.ornek) foreach (var o in kv.Value) Yaz("   ornek " + o);
    }

    // Engel çifti kategorisi: tüm şerit bariyer (AllLaneObstacle), fare (PatrollingObstacle), şerit engeli (SimpleBarricade/Missile).
    static string Kategori(Sira a, Sira b)
    {
        string T(Sira s) => s.tur == nameof(AllLaneObstacle) ? "bariyer" : s.tur == nameof(PatrollingObstacle) ? "fare" : "serit";
        string ta = T(a), tb = T(b);
        if (ta == "fare" || tb == "fare") return "fare+X";
        if (ta == "bariyer" && tb == "bariyer") return "bariyer-bariyer";
        if (ta == "serit" && tb == "serit") return "serit-serit";
        return "serit<->bariyer";
    }

    static void Say(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }

    static void Ornek(Sonuc r, string s) { string k = s.Substring(0, s.IndexOf(':')); if (!r.ornek.TryGetValue(k, out var l)) r.ornek[k] = l = new List<string>(); if (l.Count < 3) l.Add(s); }
}
