using UnityEngine;

// Meshy hareketlerinde yere basan ayak hafif yukarı/aşağı eğik kalıyor (menüde topuk ya da parmak havada görünüyordu).
// Ayak yere yakınken bilekten parmağa uzanan çizgi yatay olacak şekilde ayak kemiği döndürülür; havadaki ayağa dokunulmaz.
public class AyakDuzle : MonoBehaviour
{
    public Transform[] ayaklar;
    public Transform[] parmaklar;
    const float k_YereYakin = 0.14f;

    void LateUpdate()
    {
        if (ayaklar == null) return;
        float zemin = transform.parent != null ? transform.parent.position.y : transform.position.y;
        for (int i = 0; i < ayaklar.Length && i < parmaklar.Length; ++i)
        {
            Transform a = ayaklar[i], p = parmaklar[i];
            if (a == null || p == null) continue;
            float yukseklik = Mathf.Min(a.position.y, p.position.y) - zemin;
            if (yukseklik > k_YereYakin) continue;
            float agirlik = 1f - Mathf.Clamp01(yukseklik / k_YereYakin);
            Vector3 yon = p.position - a.position, duz = new Vector3(yon.x, 0f, yon.z);
            if (duz.sqrMagnitude < 1e-6f) continue;
            Quaternion q = Quaternion.FromToRotation(yon, duz.normalized * yon.magnitude);
            a.rotation = Quaternion.Slerp(Quaternion.identity, q, agirlik) * a.rotation;
        }
    }
}
