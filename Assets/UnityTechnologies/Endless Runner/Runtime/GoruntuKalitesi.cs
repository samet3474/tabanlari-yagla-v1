using UnityEngine;

// Kenar yumuşatma (MSAA 2x): karakter ve tramvay kenarları telefonda tırtıklı görünmesin. Ayar dosyasına dokunmadan
// açılışta etkin işleme hattına uygulanır (bu derleme URP'ye doğrudan bağlı değil: özellik adıyla).
public static class GoruntuKalitesi
{
#if !UNITY_EDITOR // düzenleyicide oynatınca ayar dosyası değişmesin
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Uygula()
    {
        var hat = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var ozellik = hat != null ? hat.GetType().GetProperty("msaaSampleCount") : null;
        if (ozellik != null && ozellik.CanWrite && (int)ozellik.GetValue(hat) < 2)
            ozellik.SetValue(hat, 2);
    }
#endif
}
