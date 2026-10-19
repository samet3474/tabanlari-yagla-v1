using UnityEngine;

/// <summary>
/// Spins a collectible's visual around the vertical axis (İstanbul gold coin). Pure visual: the collider and the
/// Coin component stay on the parent, so collection and the coin pool are unchanged.
/// </summary>
public class AltinDon : MonoBehaviour
{
    public float hiz = 180f; // derece/sn

    void Update()
    {
        transform.Rotate(0f, hiz * Time.deltaTime, 0f, Space.Self);
    }
}
