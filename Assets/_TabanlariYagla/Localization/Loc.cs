using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-facing text table. Lines in Resources/Lang/tr.txt are "key<TAB>text"; empty lines and lines starting with '#'
/// are ignored. Keys are compared ordinally (never culture-aware case conversion: the build machine runs a Turkish
/// locale, where ToLower/ToUpper change i/ı).
///
/// Character, theme and accessory ids double as save-file keys and Addressables addresses, so they are never
/// translated: only the text shown on screen is looked up here, falling back to the id itself.
/// </summary>
public static class Loc
{
    const string k_TablePath = "Lang/tr";

    static Dictionary<string, string> s_Table;

    static Dictionary<string, string> Table
    {
        get
        {
            if (s_Table == null)
                Load();
            return s_Table;
        }
    }

    static void Load()
    {
        s_Table = new Dictionary<string, string>(System.StringComparer.Ordinal);
        TextAsset asset = Resources.Load<TextAsset>(k_TablePath);
        if (asset == null)
        {
            Debug.LogWarning("Loc: text table not found at Resources/" + k_TablePath);
            return;
        }

        string[] lines = asset.text.Split('\n');
        for (int i = 0; i < lines.Length; ++i)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
                continue;

            int tab = line.IndexOf('\t');
            if (tab <= 0)
                continue;

            s_Table[line.Substring(0, tab)] = line.Substring(tab + 1);
        }
    }

    public static string Get(string key, string fallback)
    {
        string text;
        return Table.TryGetValue(key, out text) ? text : fallback;
    }

    public static string CharacterName(string characterId)
    {
        return Get("char." + characterId, characterId);
    }

    public static string ThemeName(string themeId)
    {
        return Get("theme." + themeId, themeId);
    }

    public static string AccessoryName(string accessoryId)
    {
        return Get("accessory." + accessoryId, accessoryId);
    }

    public static string ConsumableName(Consumable consumable)
    {
        // Enum names are culture-invariant, unlike the display strings returned by GetConsumableName().
        return Get("consumable." + consumable.GetConsumableType().ToString(), consumable.GetConsumableName());
    }
}
