using UnityEngine;

/// <summary>
/// Oncoming tram: stands still until the runner is baslamaMesafesi metres in front of its nose, then drives towards
/// the runner at hiz m/s along the track. It stops whenever the run stops (hit, pause), like the rest of the track.
/// Its obstacle collider and roof come with the prefab; this only moves it. No Random call.
/// </summary>
public class HareketliTramvay : MonoBehaviour
{
    [Tooltip("Speed towards the runner, m/s.")]
    public float hiz = 6f;
    [Tooltip("Distance between the runner and the tram's nose when it starts moving, metres.")]
    public float baslamaMesafesi = 45f;

    bool m_Basladi;

    void Update()
    {
        TrackManager tm = TrackManager.instance;
        if (tm == null || !tm.isMoving || tm.characterController == null)
            return;

        if (!m_Basladi)
        {
            float onde = Vector3.Dot(transform.position - tm.characterController.transform.position, transform.forward);
            if (onde > baslamaMesafesi || onde < 0.0f)
                return;
            m_Basladi = true;
        }

        transform.position -= transform.forward * hiz * Time.deltaTime;
    }
}
