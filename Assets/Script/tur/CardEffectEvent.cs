using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 卡片效果事件系統 - 定義卡片能夠觸發的各種事件類型
/// </summary>
public static class CardEffectEvent
{
    // 事件類型列舉
    public enum EventType
    {
        None,
        CommonSummon,
        SpecialSummon,
        Attack,
        Defend,
        Placed,
        Destroyed,
        TurnStart,
        TurnEnd,
        Drawn,
        Discarded,
        Act,
        EventUse
    }

    // 回調委託類型
    public delegate void CardEffectCallback(CardEffectContext context);

    // 效果上下文
    [System.Serializable]
    public class CardEffectContext
    {
        public string CardId;
        public string CardName;
        public EventType EventType;
        public CardData SourceCard;
        public GamePlay GamePlayRef;
        public object ExtraData;

        public CardEffectContext(string cardId, EventType eventType, GamePlay gamePlayRef)
        {
            CardId = cardId;
            EventType = eventType;
            GamePlayRef = gamePlayRef;
        }
    }

    private static System.Collections.Generic.Dictionary<string, CardEffectCallback> effectRegistry
        = new System.Collections.Generic.Dictionary<string, CardEffectCallback>();

    public static void RegisterEffect(string cardId, EventType eventType, CardEffectCallback callback)
    {
        if (string.IsNullOrEmpty(cardId) || callback == null) return;
        string key = $"{cardId}_{eventType}";
        effectRegistry[key] = callback;
        Debug.Log($"[CardEffectEvent] Registered effect for {key}");
        CardEffectTrace.Push($"Register key: {key}");
    }

    public static void TriggerEffect(string cardId, EventType eventType, GamePlay gamePlayRef)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        string key = $"{cardId}_{eventType}";
        if (effectRegistry.TryGetValue(key, out var callback))
        {
            var context = new CardEffectContext(cardId, eventType, gamePlayRef);
            try
            {
                callback.Invoke(context);
                Debug.Log($"[CardEffectEvent] Triggered {key}");
                CardEffectTrace.Push($"Triggered key: {key}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardEffectEvent] Error: {e.Message}");
                CardEffectTrace.Push($"Trigger error: {key} ({e.Message})");
            }
        }
        else
        {
            Debug.LogWarning($"[CardEffectEvent] No effect registered for {key}");
            CardEffectTrace.Push($"No registered key: {key}");
        }
    }

    public static void ClearAllEffects()
    {
        effectRegistry.Clear();
        Debug.Log("[CardEffectEvent] All effects cleared");
        CardEffectTrace.Push("Registry cleared.");
    }
}

/// <summary>
/// 卡片效果追蹤總線：集中收集卡片效果流程訊息，供 UI 面板顯示。
/// </summary>
public static class CardEffectTrace
{
    private static readonly List<string> lines = new List<string>();

    public static event Action OnTraceUpdated;

    public static int MaxStoredLines { get; set; } = 60;

    public static IReadOnlyList<string> Lines => lines;

    public static void Push(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string time = DateTime.Now.ToString("HH:mm:ss");
        lines.Add($"[{time}] {message}");

        if (lines.Count > MaxStoredLines)
        {
            int removeCount = lines.Count - MaxStoredLines;
            lines.RemoveRange(0, removeCount);
        }

        OnTraceUpdated?.Invoke();
    }

    public static void Clear()
    {
        lines.Clear();
        OnTraceUpdated?.Invoke();
    }
}

/// <summary>
/// 將 CardEffectTrace 內容顯示到 UI 的簡易面板。
/// 掛在場景物件上，並在 Inspector 指定 outputText。
/// </summary>
public class CardEffectTracePanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI outputText;
    [SerializeField] private bool visibleOnStart = true;
    [SerializeField] private bool clearTraceOnEnable = false;
    [SerializeField] private int maxVisibleLines = 14;

    private bool isVisible;

    void OnEnable()
    {
        CardEffectTrace.OnTraceUpdated += Refresh;
        isVisible = visibleOnStart;

        if (clearTraceOnEnable)
            CardEffectTrace.Clear();

        Refresh();
    }

    void OnDisable()
    {
        CardEffectTrace.OnTraceUpdated -= Refresh;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        Refresh();
    }

    public void ToggleVisible()
    {
        isVisible = !isVisible;
        Refresh();
    }

    public void ClearTrace()
    {
        CardEffectTrace.Clear();
    }

    public void Refresh()
    {
        if (outputText == null)
            return;

        if (!isVisible)
        {
            outputText.text = string.Empty;
            return;
        }

        var traceLines = CardEffectTrace.Lines;
        int start = Mathf.Max(0, traceLines.Count - Mathf.Max(1, maxVisibleLines));

        var sb = new StringBuilder(512);
        sb.AppendLine("[Card Effect Trace]");
        for (int i = start; i < traceLines.Count; i++)
            sb.AppendLine(traceLines[i]);

        outputText.text = sb.ToString();
    }
}
