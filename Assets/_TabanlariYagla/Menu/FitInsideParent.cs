using UnityEngine;

/// <summary>
/// Shrinks a fixed-size panel (designed for the 720-unit-wide reference screen) evenly so it fits the width of its
/// parent on taller, narrower phones (the canvas keeps 1280 units of height, so its width drops to ~540 at 21:9).
/// Never enlarges it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FitInsideParent : MonoBehaviour
{
    public float margin = 12f; // canvas units kept free on each side

    RectTransform m_Rect, m_Parent;
    float m_LastParentWidth = -1f;

    void OnEnable()
    {
        m_Rect = (RectTransform)transform;
        m_Parent = transform.parent as RectTransform;
        m_LastParentWidth = -1f;
    }

    void LateUpdate()
    {
        if (m_Parent == null)
            return;
        float parentWidth = m_Parent.rect.width;
        if (Mathf.Approximately(parentWidth, m_LastParentWidth))
            return;
        m_LastParentWidth = parentWidth;
        float width = m_Rect.rect.width;
        float scale = width > 0f ? Mathf.Min(1f, (parentWidth - 2f * margin) / width) : 1f;
        m_Rect.localScale = new Vector3(scale, scale, 1f);
    }
}
