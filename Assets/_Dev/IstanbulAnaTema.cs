#if UNITY_EDITOR || DEVELOPMENT_BUILD || TY_DEVTOOLS
// Web test sürümünde İstanbul ana tema: yalnız geliştirme / TY_DEVTOOLS derlemesinde derlenir, yalnız WebGL'de çalışır
// (Windows geliştirme testleri Day ile başlamaya devam eder). Yayın derlemesine ve PlayerData.NewSave'e dokunmaz.
// Bu test sürümü bir tarayıcıda ilk açıldığında İstanbul sahiplenilir, seçilir ve kaydedilir; sonra menüden başka
// tema seçilirse o seçim korunur (PlayerPrefs bayrağı). Adreste parcatest varsa ParcaTestAkisi zaten İstanbul'u seçer.
using System.Collections;
using UnityEngine;

public class IstanbulAnaTema : MonoBehaviour
{
    const string k_Tema = "Istanbul";
    const string k_Bayrak = "ty_test_istanbul_ana_v1";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer) return;
        if (Application.absoluteURL.Contains("parcatest=")) return;
        // ?kayitsil=1 kaydı sıfırladığında (DevTimeline) yeni kullanıcı gibi yeniden seçilir
        if (PlayerPrefs.GetInt(k_Bayrak, 0) == 1 && !Application.absoluteURL.Contains("kayitsil=1")) return;
        if (Object.FindAnyObjectByType<IstanbulAnaTema>() != null) return;
        var go = new GameObject("IstanbulAnaTema");
        DontDestroyOnLoad(go);
        go.AddComponent<IstanbulAnaTema>();
    }

    IEnumerator Start()
    {
        float bekle = 0f;
        while (!(PlayerData.instance != null && ThemeDatabase.loaded && GameManager.instance != null && GameManager.instance.topState != null) && bekle < 60f)
        { bekle += Time.unscaledDeltaTime; yield return null; }
        if (bekle >= 60f || ThemeDatabase.GetThemeData(k_Tema) == null) { Debug.Log("ANATEMA| Istanbul secilemedi"); Destroy(gameObject); yield break; }

        var pd = PlayerData.instance;
        if (!pd.themes.Contains(k_Tema)) pd.themes.Add(k_Tema);
        pd.usedTheme = pd.themes.IndexOf(k_Tema);
        pd.Save();
        PlayerPrefs.SetInt(k_Bayrak, 1);
        PlayerPrefs.Save();
        if (GameManager.instance.topState is LoadoutState loadout) loadout.Refresh();
        Debug.Log("ANATEMA| Istanbul ana tema secildi (temalar: " + string.Join(",", pd.themes) + ")");
        Destroy(gameObject);
    }
}
#endif
