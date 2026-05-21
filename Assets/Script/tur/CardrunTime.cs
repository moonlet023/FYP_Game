using System.Collections.Generic;
using UnityEngine;

#pragma warning disable CS0246 // Name not found

/// <summary>
/// 卡片運行時系統 - 管理卡片效果的初始化、註冊和觸發
/// </summary>
public class CardrunTime : MonoBehaviour
{
    private CardEvent cardEvent;
    private GamePlay gamePlay;
    private CardEffectExecutor effectExecutor;
    private readonly HashSet<string> initializedCardIds = new HashSet<string>();

    void Awake()
    {
        cardEvent = FindObjectOfType<CardEvent>();
        gamePlay = FindObjectOfType<GamePlay>();

        if (cardEvent == null)
        {
            Debug.LogError("[CardrunTime] CardEvent not found in scene");
            return;
        }

        if (gamePlay == null)
        {
            Debug.LogError("[CardrunTime] GamePlay not found in scene");
            return;
        }

        effectExecutor = new CardEffectExecutor(gamePlay, cardEvent);

        Debug.Log("[CardrunTime] Initialized - CardEvent and GamePlay found");
        global::CardEffectTrace.Push("CardrunTime initialized.");
    }

    void Start()
    {
        // 初始化所有卡片的效果
        InitializeAllCardEffects();
    }

    /// <summary>
    /// 初始化所有卡片的 EZcode 效果
    /// </summary>
    private void InitializeAllCardEffects()
    {
        Debug.Log("[CardrunTime] Initializing all card effects from card database...");
        global::CardEffectTrace.Push("InitializeAllCardEffects start.");

        // 初始化主要卡片
        InitializeCardEffect("01");
        
        // 初始化特殊卡片：08 (Guard), 15 (Event), 16 (Event)
        InitializeCardEffect("08");
        InitializeCardEffect("15");
        InitializeCardEffect("16");
    }

    /// <summary>
    /// 初始化特定卡片的效果註冊
    /// </summary>
    public void InitializeCardEffect(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || cardEvent == null) return;

        if (initializedCardIds.Contains(cardId))
            return;

        if (!cardEvent.TryGetCardById(cardId, out var cardData))
        {
            Debug.LogWarning($"[CardrunTime] Card {cardId} not found");
            global::CardEffectTrace.Push($"Init failed: card {cardId} not found.");
            return;
        }

        if (string.IsNullOrEmpty(cardData.EZcode))
        {
            Debug.Log($"[CardrunTime] Card {cardId} has no EZcode");
            global::CardEffectTrace.Push($"Init skip: card {cardId} has no EZcode.");
            return;
        }

        // 解析 EZcode
        var instruction = CardEffectParser.ParseEZcode(cardData.EZcode);
        if (instruction == null)
        {
            Debug.LogError($"[CardrunTime] Failed to parse EZcode for card {cardId}: {cardData.EZcode}");
            global::CardEffectTrace.Push($"Init failed: card {cardId} EZcode parse failed.");
            return;
        }

        // 註冊效果回調
        CardEffectEvent.RegisterEffect(cardId, instruction.TriggerEvent, context =>
        {
            if (context == null || context.GamePlayRef == null)
            {
                Debug.LogError("[CardrunTime] Invalid context in effect callback");
                return;
            }

            Debug.Log($"[CardrunTime] Effect triggered for {cardId}: {instruction.TriggerEvent}");
            global::CardEffectTrace.Push($"Effect callback hit: {cardId} {instruction.TriggerEvent}");
            effectExecutor.ExecuteInstruction(instruction, cardId, context);
        });

        initializedCardIds.Add(cardId);

        Debug.Log($"[CardrunTime] Registered effect for card {cardId}: event={instruction.TriggerEvent}, actions={instruction.Actions.Count}");
        global::CardEffectTrace.Push($"Registered: {cardId} -> {instruction.TriggerEvent} ({instruction.Actions.Count} actions)");
    }

    /// <summary>
    /// 觸發卡片效果（由遊戲流程中的其他組件調用，例如放置卡片時）
    /// </summary>
    public void TriggerCardEffect(string cardId, CardEffectEvent.EventType eventType)
    {
        if (string.IsNullOrEmpty(cardId) || gamePlay == null)
        {
            Debug.LogWarning("[CardrunTime] Invalid cardId or GamePlay reference");
            return;
        }

        // 避免時序問題：第一次觸發前先確保該卡效果已註冊。
        if (!initializedCardIds.Contains(cardId))
        {
            InitializeCardEffect(cardId);
        }

        Debug.Log($"[CardrunTime] Triggering effect: cardId={cardId}, eventType={eventType}");
        global::CardEffectTrace.Push($"Trigger request: {cardId} {eventType}");
        CardEffectEvent.TriggerEffect(cardId, eventType, gamePlay);
    }

    /// <summary>
    /// 批量初始化多張卡片效果
    /// </summary>
    public void InitializeMultipleCardEffects(params string[] cardIds)
    {
        foreach (var cardId in cardIds)
        {
            InitializeCardEffect(cardId);
        }
    }

    /// <summary>
    /// 清空所有卡片效果（遊戲重置時使用）
    /// </summary>
    public void ClearAllEffects()
    {
        CardEffectEvent.ClearAllEffects();
        initializedCardIds.Clear();
        Debug.Log("[CardrunTime] All card effects cleared");
        global::CardEffectTrace.Push("All effects cleared.");
    }
}

#pragma warning restore CS0246