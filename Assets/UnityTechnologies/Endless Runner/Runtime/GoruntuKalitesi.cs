using UnityEngine;

// Kenar yumuşatma (MSAA 2x) ve 4 kemikli deri: karakterler telefonda tırtıklı ve kırık görünmesin. Ayar dosyasına dokunmadan
// açılışta etkin işleme hattına uygulanır (bu derleme URP'ye doğrudan bağlı değil: özellik adıyla).
public static class GoruntuKalitesi
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Uygula()
    {
        // web/telefon varsayılan kalite seviyesi köşe noktası başına 1 kemik kullanıyor: Meshy karakterlerinde diz ve bilek
        // kırık görünüyordu. Deri her seviyede 4 kemikle çizilir.
#if !UNITY_EDITOR // düzenleyicide oynatınca kalite ve işleme hattı ayarları değişmesin
        QualitySettings.skinWeights = SkinWeights.FourBones;
        var hat = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var ozellik = hat != null ? hat.GetType().GetProperty("msaaSampleCount") : null;
        if (ozellik != null && ozellik.CanWrite && (int)ozellik.GetValue(hat) < 2)
            ozellik.SetValue(hat, 2);
#endif
    }
}
