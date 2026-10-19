using UnityEngine;

// 4 kemikli deri: web/telefon kalite seviyesi köşe noktası başına 1 kemik kullanıyordu; Meshy karakterlerinde diz ve bilek
// kırık görünüyordu. Ayar dosyasına dokunmadan açılışta uygulanır.
// (MSAA burada açılmaz: iOS Safari çok örnekli çizim yüzeyinde "NotSupportedError" verip oyunu durduruyordu.)
public static class GoruntuKalitesi
{
#if !UNITY_EDITOR // düzenleyicide oynatınca kalite ayarı değişmesin
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Uygula()
    {
        QualitySettings.skinWeights = SkinWeights.FourBones;
    }
#endif
}
