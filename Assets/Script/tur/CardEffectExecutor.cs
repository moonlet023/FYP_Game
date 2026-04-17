using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡片效果執行器 - 根據解析的效果指令執行具體邏輯
/// </summary>
public class CardEffectExecutor
{
    private readonly GamePlay gamePlayRef;
    private readonly CardEvent cardEventRef;

    public CardEffectExecutor(GamePlay gamePlayRef, CardEvent cardEventRef = null)
    {
        this.gamePlayRef = gamePlayRef;
        this.cardEventRef = cardEventRef;
    }

    /// <summary>
    /// 執行單個效果指令
    /// </summary>
    public void ExecuteInstruction(CardEffectParser.EffectInstruction instruction, string cardId, CardEffectEvent.CardEffectContext context = null)
    {
        if (instruction == null || string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("[CardEffectExecutor] Invalid instruction or cardId");
            CardEffectTrace.Push("ExecuteInstruction skipped: invalid instruction/cardId.");
            return;
        }

        if (!EvaluateConditions(instruction, context))
        {
            CardEffectTrace.Push($"ExecuteInstruction skipped by condition: card={cardId}");
            return;
        }

        int dynamicValue = 0;
        if (instruction.DynamicVariables != null && instruction.DynamicVariables.TryGetValue("X", out var xDef))
        {
            dynamicValue = EvaluateDynamicVariable(xDef, cardId, context);
            Debug.Log($"[CardEffectExecutor] Evaluated X={xDef} -> {dynamicValue}");
        }

        Debug.Log($"[CardEffectExecutor] Executing {instruction.Actions.Count} actions for card {cardId}");
        CardEffectTrace.Push($"ExecuteInstruction: card={cardId}, actions={instruction.Actions.Count}");

        foreach (var action in instruction.Actions)
            ExecuteAction(action, cardId, dynamicValue);
    }

    private bool EvaluateConditions(CardEffectParser.EffectInstruction instruction, CardEffectEvent.CardEffectContext context)
    {
        if (instruction?.Conditions == null || instruction.Conditions.Count == 0)
            return true;

        if (instruction.Conditions.TryGetValue("type_count_gt", out var gtText)
            && instruction.Conditions.TryGetValue("type_name", out var typeName)
            && int.TryParse(gtText, out int threshold))
        {
            int inGameCount = CountCardsByTypeInPlay(typeName);
            bool ok = inGameCount > threshold;
            Debug.Log($"[CardEffectExecutor] Condition type_count_gt: type={typeName}, count={inGameCount}, threshold={threshold}, pass={ok}");
            CardEffectTrace.Push($"Condition check: {typeName} in play={inGameCount}, need>{threshold}, pass={ok}");
            return ok;
        }

        return true;
    }

    private int EvaluateDynamicVariable(string varDef, string cardId, CardEffectEvent.CardEffectContext context)
    {
        if (string.IsNullOrWhiteSpace(varDef))
            return 0;

        string normalized = varDef.ToLowerInvariant().Trim();

        if (normalized.Contains("energy"))
        {
            int count = (gamePlayRef?.Energy?.Count ?? 0);
            Debug.Log($"[CardEffectExecutor] Dynamic X (energy): {count}");
            return count;
        }

        if (normalized.Contains("number of card") && (normalized.Contains("attack area") || normalized.Contains("bench")))
        {
            int count = CountCardsInAttackAreaAndBench();
            Debug.Log($"[CardEffectExecutor] Dynamic X (attack+bench cards): {count}");
            return count;
        }

        if (normalized.Contains("hp") || normalized.Contains("life"))
        {
            Debug.Log($"[CardEffectExecutor] Dynamic X (HP): not yet implemented");
            return 0;
        }

        Debug.LogWarning($"[CardEffectExecutor] Unknown dynamic variable definition: {varDef}");
        return 0;
    }

    private int CountCardsInAttackAreaAndBench()
    {
        int total = 0;
        var areas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsAttackArea() && !area.IsBenchArea())
                continue;

            var root = area.transform;
            for (int c = 0; c < root.childCount; c++)
            {
                var child = root.GetChild(c);
                if (child != null)
                    total++;
            }
        }

        return total;
    }

    private int CountCardsByTypeInPlay(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName) || cardEventRef == null)
            return 0;

        int count = 0;
        var allDropAreas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
        for (int i = 0; i < allDropAreas.Length; i++)
        {
            var area = allDropAreas[i];
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            var root = area.transform;
            for (int c = 0; c < root.childCount; c++)
            {
                var child = root.GetChild(c);
                if (child == null) continue;

                string cardId = ResolveCardId(child.gameObject);
                if (string.IsNullOrWhiteSpace(cardId))
                    continue;

                if (!cardEventRef.TryGetCardById(cardId, out var data) || data == null)
                    continue;

                if (string.Equals((data.Type ?? string.Empty).Trim(), typeName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    count++;
            }
        }

        return count;
    }

    private static string ResolveCardId(GameObject cardGO)
    {
        if (cardGO == null) return null;

        var simpleData = cardGO.GetComponent<SimpleCardData>();
        if (simpleData != null && !string.IsNullOrWhiteSpace(simpleData.cardId))
            return simpleData.cardId.Trim();

        var viewData = cardGO.GetComponent<CardData>();
        if (viewData != null && !string.IsNullOrWhiteSpace(viewData.id))
            return viewData.id.Trim();

        var identity = cardGO.GetComponent<CardIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.Id))
            return identity.Id.Trim();

        return null;
    }

    /// <summary>
    /// 執行單個行動
    /// </summary>
    private void ExecuteAction(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        if (action == null) return;

        Debug.Log($"[CardEffectExecutor] Executing action: {action.ActionType}");
        CardEffectTrace.Push($"Action: {action.ActionType}");

        switch (action.ActionType.ToLower())
        {
            case "draw":
                ExecuteDraw(action, cardId, dynamicValue);
                break;

            case "damage":
                ExecuteDamage(action, cardId, dynamicValue);
                break;

            case "heal":
                ExecuteHeal(action, cardId, dynamicValue);
                break;

            case "gain_energy":
                ExecuteGainEnergy(action, cardId, dynamicValue);
                break;

            case "discard":
                ExecuteDiscard(action, cardId, dynamicValue);
                break;

            case "discard_energy":
                ExecuteDiscardEnergy(action, cardId, dynamicValue);
                break;

            case "buff_atk":
                ExecuteBuffAttack(action, cardId, dynamicValue);
                break;

            case "summon":
                ExecuteSummon(action, cardId, dynamicValue);
                break;

            default:
                Debug.LogWarning($"[CardEffectExecutor] Unknown action type: {action.ActionType}");
                CardEffectTrace.Push($"Unknown action: {action.ActionType}");
                break;
        }
    }

    private void ExecuteDraw(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        if (gamePlayRef == null)
        {
            Debug.LogError("[CardEffectExecutor] GamePlay reference is null");
            return;
        }

        int drawCount = GetIntParam(action, "param0", 1, dynamicValue);
        var drawn = gamePlayRef.DrawCardsForPlayer(drawCount, true);
        Debug.Log($"[CardEffectExecutor] Draw executed: cardId={cardId}, requested={drawCount}, drawn={drawn.Count}");
        CardEffectTrace.Push($"Draw result: requested={drawCount}, drawn={drawn.Count}");
    }

    private void ExecuteDamage(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        int damageAmount = GetIntParam(action, "param0", 1, dynamicValue);
        Debug.Log($"[CardEffectExecutor] Damage: {damageAmount}");
        // TODO: 實現傷害邏輯
    }

    private void ExecuteHeal(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        int healAmount = GetIntParam(action, "param0", 1, dynamicValue);
        Debug.Log($"[CardEffectExecutor] Heal: {healAmount}");
        
        if (gamePlayRef != null)
            gamePlayRef.AddPlayerHP(healAmount);
    }

    private void ExecuteGainEnergy(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        int energyAmount = GetIntParam(action, "param0", 1, dynamicValue);
        string energyColor = action.Parameters.GetValueOrDefault("color", "colorless").ToLowerInvariant();

        if (energyColor == "colorless" && cardEventRef != null && cardEventRef.TryGetCardById(cardId, out var cardData))
            energyColor = string.IsNullOrWhiteSpace(cardData?.Color) ? "green" : cardData.Color.ToLowerInvariant();

        Debug.Log($"[CardEffectExecutor] Gain energy: {energyAmount}x {energyColor}");

        if (gamePlayRef != null)
            gamePlayRef.AddPlayerEnergy(energyColor, energyAmount);
    }

    private void ExecuteDiscard(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        int discardCount = GetIntParam(action, "param0", 1, dynamicValue);

        if (gamePlayRef == null)
        {
            Debug.LogError("[CardEffectExecutor] GamePlay reference is null, discard skipped");
            return;
        }

        int discarded = gamePlayRef.DiscardCardsFromPlayerHand(discardCount, true);
        Debug.Log($"[CardEffectExecutor] Discard executed: requested={discardCount}, discarded={discarded}");
        CardEffectTrace.Push($"Discard result: requested={discardCount}, discarded={discarded}");
    }

    private void ExecuteDiscardEnergy(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        if (gamePlayRef == null)
        {
            Debug.LogError("[CardEffectExecutor] GamePlay reference is null, discard_energy skipped");
            return;
        }

        int amount = GetIntParam(action, "param0", 1, dynamicValue);
        string color = action.Parameters.GetValueOrDefault("color", string.Empty).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(color) && cardEventRef != null && cardEventRef.TryGetCardById(cardId, out var data))
            color = (data?.Color ?? "green").ToLowerInvariant();

        bool ok = gamePlayRef.TryConsumeEnergyByColor(color, amount);
        Debug.Log($"[CardEffectExecutor] discard_energy: {amount}x {color}, success={ok}");
        CardEffectTrace.Push($"Discard energy: {amount}x {color}, success={ok}");
    }

    private void ExecuteBuffAttack(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        if (gamePlayRef == null)
        {
            Debug.LogError("[CardEffectExecutor] GamePlay reference is null, buff_atk skipped");
            return;
        }

        int amount = GetIntParam(action, "param0", 1, dynamicValue);
        if (action.IsApplyToAll)
        {
            // 對所有攻擊區域中的卡牌套用 buff
            var allDropAreas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
            int buffCount = 0;

            foreach (var area in allDropAreas)
            {
                if (area == null || !area.gameObject.scene.IsValid() || !area.IsAttackArea())
                    continue;

                var root = area.transform;
                for (int c = 0; c < root.childCount; c++)
                {
                    var child = root.GetChild(c);
                    if (child == null)
                        continue;

                    string targetCardId = ResolveCardId(child.gameObject);
                    if (string.IsNullOrWhiteSpace(targetCardId))
                        continue;

                    gamePlayRef.AddPlayerAttackBuff(targetCardId, amount);
                    buffCount++;
                }
            }

            Debug.Log($"[CardEffectExecutor] buff_atk (all attack area): +{amount} to {buffCount} cards");
            CardEffectTrace.Push($"ATK buff (all): +{amount} to {buffCount} cards");
            return;
        }

        gamePlayRef.AddPlayerAttackBuff(cardId, amount);
        Debug.Log($"[CardEffectExecutor] buff_atk: card={cardId}, +{amount}");
        CardEffectTrace.Push($"ATK buff: {cardId} +{amount}");
    }

    private void ExecuteSummon(CardEffectParser.EffectAction action, string cardId, int dynamicValue = 0)
    {
        string targetCardId = action.Parameters.GetValueOrDefault("param0", null);
        Debug.Log($"[CardEffectExecutor] Summon: {targetCardId}");
        // TODO: 實現召喚邏輯
    }

    private int GetIntParam(CardEffectParser.EffectAction action, string key, int defaultValue, int dynamicValue = 0)
    {
        if (action.Parameters.TryGetValue(key, out var value))
        {
            if (value.Contains("X") && dynamicValue > 0)
                return dynamicValue;

            if (int.TryParse(value, out int result))
                return result;
        }

        return defaultValue;
    }
}
