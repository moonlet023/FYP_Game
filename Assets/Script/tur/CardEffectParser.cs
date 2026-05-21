using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// EZcode 解析器 - 將卡片的 EZcode 字串解析為可執行的效果指令
/// </summary>
public static class CardEffectParser
{
    [Serializable]
    public class EffectInstruction
    {
        public CardEffectEvent.EventType TriggerEvent;
        public List<EffectAction> Actions = new List<EffectAction>();
        public Dictionary<string, string> Conditions = new Dictionary<string, string>();
        public Dictionary<string, string> DynamicVariables = new Dictionary<string, string>();
    }

    [Serializable]
    public class EffectAction
    {
        public string ActionType;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();

        public bool IsApplyToAll = false;  // 標記是否應用於所有卡牌

        public override string ToString()
        {
            return $"{ActionType}({string.Join(", ", Parameters)})";
        }
    }

    /// <summary>
    /// 解析 EZcode 字串。支援較自然語句，例如：
    /// - "elementalist, more than 3 Eif: start stage, earn:1 green energy"
    /// - "Act -> discard:1 green energy, choose:1 card in attack area, ATK +1"
    /// </summary>
    public static EffectInstruction ParseEZcode(string ezcode)
    {
        if (string.IsNullOrWhiteSpace(ezcode))
        {
            Debug.LogWarning("[CardEffectParser] EZcode is null or empty");
            return null;
        }

        var instruction = new EffectInstruction
        {
            TriggerEvent = CardEffectEvent.EventType.None
        };

        string normalized = ezcode.Replace("->", ",");
        string[] rawParts = normalized.Split(',');
        if (rawParts.Length == 0)
        {
            Debug.LogWarning($"[CardEffectParser] Invalid EZcode format: {ezcode}");
            return null;
        }

        for (int i = 0; i < rawParts.Length; i++)
        {
            string part = rawParts[i]?.Trim();
            if (string.IsNullOrWhiteSpace(part))
                continue;

            // 檢查是否包含 [Guard]，並將其存儲在 Conditions 中
            if (part.Contains("[Guard]", System.StringComparison.OrdinalIgnoreCase) || 
                part.Contains("[guard]", System.StringComparison.OrdinalIgnoreCase))
            {
                instruction.Conditions["is_guard"] = "true";
                part = StripEventTokensForGuard(part);
                if (string.IsNullOrWhiteSpace(part))
                    continue;
            }

            // 檢查是否包含 [Limit]，並將其存儲在 Conditions 中
            if (part.Contains("[Limit]", System.StringComparison.OrdinalIgnoreCase) || 
                part.Contains("[limit]", System.StringComparison.OrdinalIgnoreCase))
            {
                instruction.Conditions["is_limit"] = "true";
                part = Regex.Replace(part, @"\[limit\]", string.Empty, RegexOptions.IgnoreCase);
                part = part.Trim();
                if (string.IsNullOrWhiteSpace(part))
                    continue;
            }

            if (instruction.TriggerEvent == CardEffectEvent.EventType.None && TryParseEventTypeFromText(part, out var evt))
            {
                instruction.TriggerEvent = evt;

                string left = StripEventTokens(part);
                if (!string.IsNullOrWhiteSpace(left))
                    ParseTokenAsConditionOrAction(left, instruction);

                continue;
            }

            if (TryParseDynamicVariable(part, instruction.DynamicVariables))
                continue;

            ParseTokenAsConditionOrAction(part, instruction);
        }

        if (instruction.TriggerEvent == CardEffectEvent.EventType.None)
        {
            Debug.LogWarning($"[CardEffectParser] Unknown event type in EZcode: {ezcode}");
            return null;
        }

        Debug.Log($"[CardEffectParser] Parsed EZcode: event={instruction.TriggerEvent}, actions={instruction.Actions.Count}, conditions={instruction.Conditions.Count}");
        return instruction;
    }

    private static void ParseTokenAsConditionOrAction(string token, EffectInstruction instruction)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (TryParseCondition(token, instruction.Conditions))
            return;

        if (LooksLikeNoiseToken(token))
            return;

        var action = ParseAction(token);
        if (action != null)
            instruction.Actions.Add(action);
    }

    private static bool LooksLikeNoiseToken(string token)
    {
        string t = NormalizeToken(token);
        return t == "elementalist" || t == "if";
    }

    private static bool TryParseCondition(string token, Dictionary<string, string> conditions)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var m = Regex.Match(
            token,
            @"more\s+than\s+(\d+)\s*\[?\s*([A-Za-z_]+)\s*\]?",
            RegexOptions.IgnoreCase);

        if (!m.Success)
            return false;

        conditions["type_count_gt"] = m.Groups[1].Value.Trim();
        conditions["type_name"] = m.Groups[2].Value.Trim();
        return true;
    }

    private static bool TryParseDynamicVariable(string token, Dictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var m = Regex.Match(
            token,
            @"X\s*=\s*(.+)",
            RegexOptions.IgnoreCase);

        if (!m.Success)
            return false;

        string varDef = m.Groups[1].Value.Trim();
        variables["X"] = varDef;
        Debug.Log($"[CardEffectParser] Parsed dynamic variable: X = {varDef}");
        return true;
    }

    private static bool TryParseEventTypeFromText(string text, out CardEffectEvent.EventType eventType)
    {
        eventType = CardEffectEvent.EventType.None;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string normalized = NormalizeToken(text);

        if (normalized.Contains("common_summon") || normalized.Contains("commonsummon"))
        {
            eventType = CardEffectEvent.EventType.CommonSummon;
            return true;
        }

        // 兼容舊版 EZcode：例如 "summon -> heal:X HP"
        if (normalized == "summon" || normalized.Contains("onsummon") || normalized.Contains("summon"))
        {
            eventType = CardEffectEvent.EventType.CommonSummon;
            return true;
        }

        if (normalized.Contains("special_summon") || normalized.Contains("specialsummon"))
        {
            eventType = CardEffectEvent.EventType.SpecialSummon;
            return true;
        }

        if (normalized.Contains("turn_start") || normalized.Contains("turnstart") || normalized.Contains("start_stage") || normalized.Contains("startstage"))
        {
            eventType = CardEffectEvent.EventType.TurnStart;
            return true;
        }

        if (normalized.Contains("turn_end") || normalized.Contains("turnend"))
        {
            eventType = CardEffectEvent.EventType.TurnEnd;
            return true;
        }

        if (normalized.StartsWith("act") || normalized.Contains("active_skill"))
        {
            eventType = CardEffectEvent.EventType.Act;
            return true;
        }

        if (normalized.Contains("placed") || normalized == "place")
        {
            eventType = CardEffectEvent.EventType.Placed;
            return true;
        }

        if (normalized.Contains("attack"))
        {
            eventType = CardEffectEvent.EventType.Attack;
            return true;
        }

        if (normalized.Contains("defend"))
        {
            eventType = CardEffectEvent.EventType.Defend;
            return true;
        }

        if (normalized.Contains("destroyed"))
        {
            eventType = CardEffectEvent.EventType.Destroyed;
            return true;
        }

        if (normalized.Contains("drawn"))
        {
            eventType = CardEffectEvent.EventType.Drawn;
            return true;
        }

        if (normalized.Contains("discarded"))
        {
            eventType = CardEffectEvent.EventType.Discarded;
            return true;
        }

        if (normalized.Contains("event_use") || normalized.Contains("eventuse") || normalized.Contains("event use"))
        {
            eventType = CardEffectEvent.EventType.EventUse;
            return true;
        }

        return false;
    }

    private static string StripEventTokens(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        string result = token;
        string[] eventTokens =
        {
            "common summon", "special summon", "turn start", "turn end",
            "start stage", "act", "attack", "defend", "placed", "place",
            "destroyed", "drawn", "discarded", "event use", "event_use"
        };

        for (int i = 0; i < eventTokens.Length; i++)
            result = Regex.Replace(result, Regex.Escape(eventTokens[i]), string.Empty, RegexOptions.IgnoreCase);

        // 移除括號內的標記（如 [Limit], [Guard] 等）
        result = Regex.Replace(result, @"\[.*?\]", string.Empty, RegexOptions.IgnoreCase);
        result = result.Replace("_", " ").Trim(' ', ':', '-', '>');
        return result;
    }

    private static string StripEventTokensForGuard(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        string result = token;
        string[] guardTokens = { "[guard]", "[Guard]", "[Limit]", "[limit]" };

        for (int i = 0; i < guardTokens.Length; i++)
            result = Regex.Replace(result, Regex.Escape(guardTokens[i]), string.Empty, RegexOptions.IgnoreCase);

        result = result.Trim();
        return result;
    }

    /// <summary>
    /// 解析效果行動
    /// </summary>
    private static EffectAction ParseAction(string actionStr)
    {
        if (string.IsNullOrWhiteSpace(actionStr))
            return null;

        // 檢查是否為 destroy_opponent_card 直接文本
        if (actionStr.ToLowerInvariant().Contains("destroy") && actionStr.ToLowerInvariant().Contains("opponent"))
        {
            return new EffectAction
            {
                ActionType = "destroy_opponent_card",
                Parameters = new Dictionary<string, string>()
            };
        }

        var atkPlus = Regex.Match(actionStr, @"atk\s*\+\s*(\d+)", RegexOptions.IgnoreCase);
        if (atkPlus.Success)
        {
            bool isApplyToAll = actionStr.Contains("all", System.StringComparison.OrdinalIgnoreCase) 
                                && (actionStr.Contains("your card", System.StringComparison.OrdinalIgnoreCase) 
                                    || actionStr.Contains("cards", System.StringComparison.OrdinalIgnoreCase));

            return new EffectAction
            {
                ActionType = "buff_atk",
                Parameters = new Dictionary<string, string>
                {
                    ["param0"] = atkPlus.Groups[1].Value
                },
                IsApplyToAll = isApplyToAll
            };
        }

        string[] segments = actionStr.Split(':');
        if (segments.Length == 0)
            return null;

        var action = new EffectAction
        {
            ActionType = NormalizeActionType(segments[0])
        };

        for (int i = 1; i < segments.Length; i++)
        {
            string segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            int eqIndex = segment.IndexOf('=');
            if (eqIndex >= 0)
            {
                string key = segment.Substring(0, eqIndex).Trim();
                string value = segment.Substring(eqIndex + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key))
                    action.Parameters[key] = value;
            }
            else
            {
                action.Parameters[$"param{i - 1}"] = segment;
            }
        }

        string energyPhrase = action.Parameters.GetValueOrDefault("param0", string.Empty);
        if (action.Parameters.TryGetValue("param1", out var p1) && !string.IsNullOrWhiteSpace(p1))
            energyPhrase = string.IsNullOrWhiteSpace(energyPhrase) ? p1 : $"{energyPhrase} {p1}";

        if (action.ActionType == "discard" && energyPhrase.ToLowerInvariant().Contains("energy"))
            action.ActionType = "discard_energy";

        if (action.ActionType == "gain_energy" || action.ActionType == "discard_energy")
            ParseEnergyPhraseToParams(energyPhrase, action.Parameters);

        if (action.ActionType == "choose")
        {
            return null;
        }

        Debug.Log($"[CardEffectParser] Parsed action: {action.ActionType}");
        return action;
    }

    private static string NormalizeActionType(string actionRaw)
    {
        string action = NormalizeToken(actionRaw);

        if (action == "draw") return "draw";
        if (action == "damage") return "damage";
        if (action == "heal") return "heal";
        if (action == "discard") return "discard";
        if (action == "summon") return "summon";
        if (action == "choose") return "choose";

        if (action == "gain_energy" || action == "gain" || action == "earn" || action == "gainarow")
            return "gain_energy";

        if (action == "atk" || action == "attack_up" || action == "buff_atk")
            return "buff_atk";

        if (action == "find_and_summon" || action == "find_summon")
            return "find_and_summon";

        if (action == "choose_opponent_bounce")
            return "choose_opponent_bounce";

        if (action == "destroy_opponent_card" || action == "destroyone_opponent_card" || action == "destroy_opponent")
            return "destroy_opponent_card";

        if (action == "shuffle_hand_draw" || action == "shuffle_hand" || action == "reshuffle_draw")
            return "shuffle_hand_draw";

        return action;
    }

    private static void ParseEnergyPhraseToParams(string phrase, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return;

        var numMatch = Regex.Match(phrase, @"\d+");
        if (numMatch.Success)
            parameters["param0"] = numMatch.Value;

        var colorMatch = Regex.Match(phrase, @"\b(red|green|blue|yellow|white|black|colorless)\b", RegexOptions.IgnoreCase);
        if (colorMatch.Success)
            parameters["color"] = colorMatch.Groups[1].Value.ToLowerInvariant();
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        string s = token.Trim().ToLowerInvariant();
        s = s.Replace("-", " ");
        s = Regex.Replace(s, @"\s+", " ");
        s = s.Replace(" ", "_");
        return s;
    }

    /// <summary>
    /// 取得簡化後的 EZcode 說明（用於 UI 展示）
    /// </summary>
    public static string GetReadableEffectDescription(string ezcode)
    {
        if (string.IsNullOrEmpty(ezcode)) return "未定義";

        var instruction = ParseEZcode(ezcode);
        if (instruction == null) return "解析失敗";

        var descriptions = new List<string> { $"當{instruction.TriggerEvent}" };

        foreach (var action in instruction.Actions)
        {
            descriptions.Add(action.ActionType switch
            {
                "draw" => $"抽{action.Parameters.GetValueOrDefault("param0", "1")}張牌",
                "damage" => $"造成{action.Parameters.GetValueOrDefault("param0", "1")}點傷害",
                "heal" => $"回復{action.Parameters.GetValueOrDefault("param0", "1")}點生命",
                "gain_energy" => $"獲得{action.Parameters.GetValueOrDefault("param0", "1")}點{action.Parameters.GetValueOrDefault("color", "無色")}能量",
                "discard_energy" => $"消耗{action.Parameters.GetValueOrDefault("param0", "1")}點{action.Parameters.GetValueOrDefault("color", "無色")}能量",
                "buff_atk" => $"攻擊力 +{action.Parameters.GetValueOrDefault("param0", "1")}",
                _ => action.ActionType
            });
        }

        return string.Join("，", descriptions);
    }
}
