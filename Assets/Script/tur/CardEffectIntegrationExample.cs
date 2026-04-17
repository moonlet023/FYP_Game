using System.Collections.Generic;
using UnityEngine;

/**
 * ========================================
 * 卡片效果系統集成示例
 * ========================================
 * 
 * 此文件展示如何在遊戲中實際應用卡片效果系統。
 * 
 * 使用場景:
 * 1. 當玩家普通召喚卡片時觸發效果
 * 2. 當卡片置於板面時觸發效果
 * 3. 當卡片攻擊時觸發效果
 * 
 * ========================================
 */

public class CardEffectIntegrationExample : MonoBehaviour
{
    private CardrunTime cardrunTime;
    private GamePlay gamePlay;

    void Awake()
    {
        cardrunTime = FindObjectOfType<CardrunTime>();
        gamePlay = FindObjectOfType<GamePlay>();

        if (cardrunTime == null)
            Debug.LogWarning("[CardEffectIntegrationExample] CardrunTime not found");
        if (gamePlay == null)
            Debug.LogWarning("[CardEffectIntegrationExample] GamePlay not found");
    }

    /// <summary>
    /// 範例 1: 當玩家普通召喚卡片時觸發效果
    /// 在 UI 或卡片放置邏輯中呼叫此方法
    /// </summary>
    public void OnCardCommonSummoned(string cardId)
    {
        Debug.Log($"[Example] Card {cardId} common summoned!");

        if (cardrunTime != null)
        {
            // 方法 A: 直接觸發該卡片已註冊的效果
            cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.CommonSummon);

            // 方法 B: 或者，如果卡片還未初始化，先初始化再觸發
            // cardrunTime.InitializeCardEffect(cardId);
            // cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.CommonSummon);
        }
    }

    /// <summary>
    /// 範例 2: 當卡片被放置在板面上時觸發效果
    /// </summary>
    public void OnCardPlaced(string cardId)
    {
        Debug.Log($"[Example] Card {cardId} placed on board!");

        if (cardrunTime != null)
        {
            cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.Placed);
        }
    }

    /// <summary>
    /// 範例 3: 當卡片攻擊時觸發效果
    /// </summary>
    public void OnCardAttack(string cardId)
    {
        Debug.Log($"[Example] Card {cardId} is attacking!");

        if (cardrunTime != null)
        {
            cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.Attack);
        }
    }

    /// <summary>
    /// 範例 4: 當卡片被抽取時觸發效果
    /// </summary>
    public void OnCardDrawn(string cardId)
    {
        Debug.Log($"[Example] Card {cardId} was drawn!");

        if (cardrunTime != null)
        {
            cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.Drawn);
        }
    }

    /// <summary>
    /// 範例 5: 初始化多張卡片的效果（遊戲開始時使用）
    /// </summary>
    public void InitializeGameCards()
    {
        if (cardrunTime != null)
        {
            // 初始化牌庫中所有卡片的效果
            cardrunTime.InitializeMultipleCardEffects("01", "02", "03", "04", "05");
        }
    }

    /// <summary>
    /// 範例 6: 在 GamePlay 中集成 - 修改 StartPlayerTurn 方法時使用
    /// 假設這是在卡片普通召喚流程中
    /// </summary>
    public void ExampleGameIntegration()
    {
        // 模擬玩家普通召喚卡片 "01"
        string commonSummonedCardId = "01";

        Debug.Log($"[Example] Player common summoned card {commonSummonedCardId}");

        // 1. 進行普通召喚的遊戲邏輯（放置卡片等）
        // ... 其他遊戲邏輯 ...

        // 2. 觸發該卡片的普通召喚效果
        if (cardrunTime != null)
        {
            cardrunTime.TriggerCardEffect(commonSummonedCardId, CardEffectEvent.EventType.CommonSummon);
        }

        // 3. 繼續其他遊戲邏輯
    }
}

/**
 * ========================================
 * 在現有代碼中的集成點
 * ========================================
 * 
 * 1. 在 Handcontroller 中 (當卡片從手中移出時):
 *    ---
 *    public void OnCardPlayedFromHand(string cardId)
 *    {
 *        cardEffectIntegration.OnCardCommonSummoned(cardId);
 *    }
 * 
 * 2. 在 SimpleDropArea 中 (當卡片被放置時):
 *    ---
 *    void OnCardDropped(string cardId)
 *    {
 *        cardEffectIntegration.OnCardPlaced(cardId);
 *        
 *        // 然後根據卡片類型觸發相應的時序事件
 *        if (IsCommonSummon(cardId))
 *            cardEffectIntegration.OnCardCommonSummoned(cardId);
 *    }
 * 
 * 3. 在 GamePlay 中 (當抽牌時):
 *    ---
 *    var drawn = deckData.drawCard(handData, 1);
 *    foreach (var id in drawn)
 *    {
 *        handController?.AddCardToHandById(id);
 *        cardEffectIntegration.OnCardDrawn(id);
 *    }
 * 
 * 4. 在 GamePlay 中 (攻擊階段):
 *    ---
 *    public void AttackWithCard(string attackerCardId, string targetCardId)
 *    {
 *        cardEffectIntegration.OnCardAttack(attackerCardId);
 *        // ... 攻擊邏輯 ...
 *    }
 * 
 * ========================================
 */
