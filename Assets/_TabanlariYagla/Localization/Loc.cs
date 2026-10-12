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

            // "\n" in the table is a line break (multi-line labels such as the tutorial prompts).
            s_Table[line.Substring(0, tab)] = line.Substring(tab + 1).Replace("\\n", "\n");
        }
    }

    public static string Get(string key, string fallback)
    {
        string text;
        return Table.TryGetValue(key, out text) ? text : fallback;
    }

    /// <summary>
    /// Table text with {0}-style placeholders. Numbers are formatted culture-invariantly so the output does not
    /// depend on the device locale. A broken format string in the table falls back instead of throwing.
    /// </summary>
    public static string Format(string key, string fallbackFormat, params object[] args)
    {
        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key, fallbackFormat), args);
        }
        catch (System.FormatException)
        {
            Debug.LogWarning("Loc: invalid format for key " + key);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, fallbackFormat, args);
        }
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

    // Saves made before the rename stored the sample's default player name; it is shown as the current default.
    // Display only: the save keeps whatever was written, and real player-typed names are returned unchanged.
    const string k_LegacyDefaultPlayerName = "Trash Cat";

    public static string PlayerName(string savedName)
    {
        return string.Equals(savedName, k_LegacyDefaultPlayerName, System.StringComparison.Ordinal)
            ? Get("player.defaultName", savedName)
            : savedName;
    }

    public static string ConsumableName(Consumable consumable)
    {
        // Enum names are culture-invariant, unlike the display strings returned by GetConsumableName().
        return Get("consumable." + consumable.GetConsumableType().ToString(), consumable.GetConsumableName());
    }
}
