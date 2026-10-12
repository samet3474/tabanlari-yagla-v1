using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces a static UI label with its entry in the text table (see <see cref="Loc"/>). The text typed in the
/// scene/prefab stays as the fallback, so a missing key or table never leaves the label empty.
///
/// Applied once, when the object is first activated: some labels (e.g. the shop's buy button) are later rewritten
/// by game code ("ALINDI"), and re-applying on every enable would overwrite that.
/// Labels whose text is written by code (scores, names, mission descriptions) do not get this component.
/// </summary>
[RequireComponent(typeof(Text))]
public class LocText : MonoBehaviour
{
    public string key;

    void Awake()
    {
        if (string.IsNullOrEmpty(key))
            return;

        Text label = GetComponent<Text>();
        string original = label.text ?? "";
        string text = Loc.Get(key, original);

        // Some labels end with a line break or spaces that position them inside their box; keep that layout.
        int end = original.Length;
        while (end > 0 && char.IsWhiteSpace(original[end - 1]))
            end--;
        string trailing = original.Substring(end);
        if (trailing.Length > 0 && !text.EndsWith(trailing, System.StringComparison.Ordinal))
            text = text.TrimEnd() + trailing;

        label.text = text;
    }
}
