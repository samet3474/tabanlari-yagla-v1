using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the player's saved coin total (PlayerData.coins) in the menu. The text is rewritten only when the value
/// changes (after a run, a purchase in the store, a mission reward, deleting the save).
/// </summary>
[RequireComponent(typeof(Text))]
public class MenuCoinCounter : MonoBehaviour
{
    Text m_Text;
    int m_Shown = int.MinValue;

    void Awake()
    {
        m_Text = GetComponent<Text>();
    }

    void Update()
    {
        if (PlayerData.instance == null)
            return;
        int coins = PlayerData.instance.coins;
        if (coins == m_Shown)
            return;
        m_Shown = coins;
        m_Text.text = coins.ToString();
    }
}
