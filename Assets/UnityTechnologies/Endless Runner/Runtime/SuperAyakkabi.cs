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

// Giyilen ayakkabı: ayak kemiğinin konumunu izler, dönmez (iri ayakkabı koşu yönüne bakar).
public class AyakTakip : MonoBehaviour
{
    public Transform ayak;
    void LateUpdate()
    {
        if (ayak == null) return;
        transform.position = ayak.position + Vector3.down * 0.04f;
        transform.rotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
    }
}
