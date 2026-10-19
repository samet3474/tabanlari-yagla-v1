using UnityEngine;
using UnityEngine.UI;

// Süper ayakkabı (Subway Surfers'taki Super Sneakers gibi): yolda dönen, parlayan tek ayakkabı. Alınınca karakter bir süre
// ayaklarında iri ayakkabılarla çok yükseğe ve uzağa zıplar; rampasız tramvay tavanına çıkabilir. Mağaza ve kayıttaki
// bonus listesine (Consumable) girmez: yalnız koşu içinde, İstanbul temasının tasarlanmış parçalarında çıkar.
public class SuperAyakkabi : MonoBehaviour
{
    [Tooltip("Ayaklara takılan model (her ayağa bir kopya)")]
    public GameObject giyilenModel;
    [Tooltip("Sol alttaki süre göstergesinin simgesi")]
    public Sprite ikon;
    public AudioClip alinmaSesi;
    public Transform gorsel;
    public Transform hale;

    const float k_DonmeHizi = 120f;

    void Update()
    {
        if (gorsel != null)
        {
            gorsel.localRotation = Quaternion.Euler(0f, Time.time * k_DonmeHizi, 0f);
            gorsel.localPosition = new Vector3(0f, 0.15f * Mathf.Sin(Time.time * 3f), 0f);
        }
        if (hale != null && Camera.main != null)
        {
            hale.rotation = Quaternion.LookRotation(hale.position - Camera.main.transform.position);
            float s = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            hale.localScale = new Vector3(s, s, s) * 2.2f;
        }
    }

    // Giyilen ayakkabıyı ayak kemiğine bağlar (konum/açı bindpose'dan); karakter: koşucunun kökü (yukarı/ileri yönü)
    public static bool KemigeBagla(Transform karakter, Transform ayakkabi, Transform ayak)
    {
        foreach (var smr in karakter.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            int i = System.Array.IndexOf(smr.bones, ayak);
            if (i < 0 || smr.sharedMesh == null || i >= smr.sharedMesh.bindposes.Length) continue;
            Matrix4x4 bp = smr.sharedMesh.bindposes[i];          // ağ uzayı -> kemik uzayı (bağlanma pozunda)
            Matrix4x4 kemikAg = bp.inverse;                       // kemik -> ağ uzayı
            // ağ uzayında yukarı ve ileri: karakterin yönleri, ağ çizicisinin uzayına çevrilir (çizici canlandırılmaz)
            Vector3 yukariAg = smr.transform.InverseTransformDirection(karakter.up).normalized;
            Vector3 ileriAg = smr.transform.InverseTransformDirection(karakter.forward).normalized;
            Vector3 ayakAg = kemikAg.GetColumn(3);
            // taban: ağın en alçak noktası (yukarı yönünde), ayak bileğinin hizasında; ayakkabı biraz öne kaydırılır
            var sinir = smr.sharedMesh.bounds; float altY = float.MaxValue;
            foreach (var k in new[] { sinir.min, sinir.max, new Vector3(sinir.min.x, sinir.min.y, sinir.max.z), new Vector3(sinir.min.x, sinir.max.y, sinir.min.z), new Vector3(sinir.max.x, sinir.min.y, sinir.min.z), new Vector3(sinir.min.x, sinir.max.y, sinir.max.z), new Vector3(sinir.max.x, sinir.min.y, sinir.max.z), new Vector3(sinir.max.x, sinir.max.y, sinir.min.z) })
                altY = Mathf.Min(altY, Vector3.Dot(k, yukariAg));
            float bilekY = Vector3.Dot(ayakAg, yukariAg);
            Vector3 tabanAg = ayakAg - yukariAg * (bilekY - altY - 0.01f / Mathf.Max(1e-4f, smr.transform.lossyScale.y));
            // ileri: bilek ile parmak kemiğinin ortası (ayakkabı ayağı sarsın, bilekte değil)
            int p = -1; for (int j = 0; j < smr.bones.Length; ++j) if (smr.bones[j] != null && smr.bones[j].parent == ayak && smr.bones[j].name.Contains("Toe")) { p = j; break; }
            if (p >= 0 && p < smr.sharedMesh.bindposes.Length)
            {
                Vector3 parmakAg = smr.sharedMesh.bindposes[p].inverse.GetColumn(3);
                tabanAg += ileriAg * (Vector3.Dot(parmakAg - ayakAg, ileriAg) * 0.5f);
            }
            // ağ uzayındaki ofset kemik uzayına
            Vector3 yerelKonum = bp.MultiplyPoint3x4(tabanAg);
            Quaternion yerelDonus = Quaternion.LookRotation(bp.MultiplyVector(ileriAg).normalized, bp.MultiplyVector(yukariAg).normalized);
            Vector3 dunyaOlcek = ayakkabi.lossyScale;
            ayakkabi.SetParent(ayak, false);
            ayakkabi.localPosition = yerelKonum; ayakkabi.localRotation = yerelDonus;
            Vector3 ks = ayak.lossyScale;
            ayakkabi.localScale = new Vector3(dunyaOlcek.x / ks.x, dunyaOlcek.y / ks.y, dunyaOlcek.z / ks.z);
            return true;
        }
        return false;
    }


    // ---- sol alttaki süre göstergesi (simge + azalan çubuk), koşu ekranının üstünde
    static GameObject s_Gosterge;
    static Image s_Dolgu;

    public static void Goster(Sprite ikon, float oran)
    {
        if (s_Gosterge == null)
        {
            s_Gosterge = new GameObject("SuperAyakkabiGosterge", typeof(Canvas), typeof(CanvasScaler));
            var canvas = s_Gosterge.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var olcek = s_Gosterge.GetComponent<CanvasScaler>();
            olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1080f, 1920f);
            olcek.matchWidthOrHeight = 0.5f;

            var kutu = Yeni("Kutu", s_Gosterge.transform, new Color(0f, 0f, 0f, 0.35f));
            Yerlestir(kutu, new Vector2(40f, 150f), new Vector2(330f, 110f));
            var simge = Yeni("Simge", kutu, Color.white);
            simge.GetComponent<Image>().sprite = ikon;
            simge.GetComponent<Image>().preserveAspect = true;
            Yerlestir(simge, new Vector2(8f, 5f), new Vector2(100f, 100f));
            var cubuk = Yeni("Cubuk", kutu, new Color(1f, 1f, 1f, 0.3f));
            Yerlestir(cubuk, new Vector2(118f, 38f), new Vector2(196f, 34f));
            var dolgu = Yeni("Dolgu", cubuk, new Color(1f, 0.82f, 0.1f, 1f));
            var dr = dolgu.GetComponent<RectTransform>();
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
            dr.pivot = new Vector2(0f, 0.5f);
            s_Dolgu = dolgu.GetComponent<Image>();
            Object.DontDestroyOnLoad(s_Gosterge);
        }
        s_Gosterge.SetActive(true);
        s_Dolgu.rectTransform.localScale = new Vector3(Mathf.Clamp01(oran), 1f, 1f);
    }

    public static void Gizle()
    {
        if (s_Gosterge != null) s_Gosterge.SetActive(false);
    }

    static RectTransform Yeni(string ad, Transform ebeveyn, Color renk)
    {
        var go = new GameObject(ad, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(ebeveyn, false);
        var img = go.GetComponent<Image>(); img.color = renk; img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    static void Yerlestir(RectTransform r, Vector2 solAlt, Vector2 boyut)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
        r.anchoredPosition = solAlt; r.sizeDelta = boyut;
    }
}

// Giyilen ayakkabı: ayağı (ayak bileği + parmak kemiği) sarar. Tabanı ayağın en alçak kemiğinin biraz altında, burnu ayağın
// baktığı yönde (bilekten parmağa), yalnız yatay döner; böylece bacak boyunca dikilmez.
public class AyakTakip : MonoBehaviour
{
    public Transform ayak;
    public Transform parmak;
    void LateUpdate()
    {
        if (ayak == null) return;
        Vector3 ileri = transform.parent != null ? transform.parent.forward : Vector3.forward; ileri.y = 0f;
        Vector3 bilek = ayak.position, uc = parmak != null ? parmak.position : bilek + ileri.normalized * 0.1f;
        float taban = Mathf.Min(bilek.y, uc.y) - 0.03f;
        Vector3 orta = (bilek + uc) * 0.5f;
        transform.SetPositionAndRotation(new Vector3(orta.x, taban, orta.z), Quaternion.LookRotation(ileri.normalized, Vector3.up));
    }
}
