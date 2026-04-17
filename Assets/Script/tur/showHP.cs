using UnityEngine;
using TMPro;

/// <summary>
/// 显示玩家和 AI 血量的 UI 脚本（使用 TMPro）
/// </summary>
public class showHP : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerHPText;
    [SerializeField] private TextMeshProUGUI aiHPText;
    [SerializeField] private GamePlay gamePlay;

    void OnEnable()
    {
        if (gamePlay == null)
            gamePlay = FindObjectOfType<GamePlay>(true);

        if (gamePlay == null)
        {
            Debug.LogError("[showHP] 找不到 GamePlay 組件，請確認場景中有 GamePlay 或在 Inspector 中手動指定", this);
            return;
        }

        gamePlay.OnHPUpdated += UpdateHPDisplay;
        UpdateHPDisplay(gamePlay.PlayerHP, gamePlay.AIHP);
    }

    void OnDisable()
    {
        if (gamePlay != null)
            gamePlay.OnHPUpdated -= UpdateHPDisplay;
    }

    private void UpdateHPDisplay(int playerHP, int aiHP)
    {
        if (playerHPText != null)
            playerHPText.text = $"{playerHP}";

        if (aiHPText != null)
            aiHPText.text = $"{aiHP}";

        Debug.Log($"[showHP] Updated display - Player HP: {playerHP}, AI HP: {aiHP}");
    }
}
