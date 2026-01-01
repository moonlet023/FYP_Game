using System.Collections.Generic;
using UnityEngine;

// 掛在「手牌容器」UI（含 RectTransform）上：
// - 當卡片被移除或新增時，執行水平重排並可選擇動畫（消除空隙）
// - 維護當前手牌記錄（cardId 或名稱）
[RequireComponent(typeof(RectTransform))]
public class SimpleHandController : MonoBehaviour
{
    [Header("Layout")]
    public float spacing = 10f;          // 卡片間距，設 0 可完全貼齊
    public bool animateReflow = true;    // 是否啟用重排動畫
    public float reflowDuration = 0.2f;  // 動畫時間（秒）
    public bool centerHand = true;       // 是否置中整排卡片

    [Header("Record")]
    public bool logHandRecordOnChange = true; // 每次更新在 Console 輸出手牌記錄

    private RectTransform handRT;
    private readonly List<string> handRecord = new List<string>();

    void Awake()
    {
        handRT = GetComponent<RectTransform>();
    }

    // 對外：有卡片被移除（例如放置到 DropArea 或被銷毀）
    public void OnCardRemoved(GameObject removed)
    {
        if (removed == null) return;
        ReflowHand();
        RefreshHandRecord();
    }

    // 對外：有卡片加入到此容器（例如抽牌生成後設為本物件子物件）
    public void OnCardAdded(GameObject added)
    {
        if (added == null) return;
        ReflowHand();
        RefreshHandRecord();
    }

    // 重新計算並排列所有子卡片位置（消除空隙）
    public void ReflowHand()
    {
        int n = handRT.childCount;
        if (n == 0) return;

        // 先計算總寬（每張卡寬度 + 間距）
        float totalWidth = 0f;
        var widths = new float[n];
        for (int i = 0; i < n; i++)
        {
            var child = handRT.GetChild(i) as RectTransform;
            if (child == null) { widths[i] = 0f; continue; }
            widths[i] = child.rect.width;
            totalWidth += widths[i];
            if (i > 0) totalWidth += spacing;
        }

        // 左起始位置（若置中，讓整排中心對齊 0）
        float startX = centerHand ? -totalWidth * 0.5f : 0f;
        float cursor = startX;

        for (int i = 0; i < n; i++)
        {
            var child = handRT.GetChild(i) as RectTransform;
            if (child == null) continue;
            float targetX = cursor + widths[i] * 0.5f; // 以 pivot 0.5 居中
            Vector2 target = new Vector2(targetX, 0f);

            if (animateReflow)
            {
                // 啟動協程逐張動畫移動
                StartCoroutine(AnimateTo(child, target, reflowDuration));
            }
            else
            {
                child.anchoredPosition = target;
            }

            cursor += widths[i] + spacing;
        }
    }

    // 重新生成手牌記錄（handRecord）
    public void RefreshHandRecord()
    {
        handRecord.Clear();
        int n = handRT.childCount;
        for (int i = 0; i < n; i++)
        {
            var child = handRT.GetChild(i).gameObject;
            var data = child.GetComponent<SimpleCardData>();
            string id = data != null && !string.IsNullOrEmpty(data.cardId) ? data.cardId : child.name;
            handRecord.Add(id);
        }
        if (logHandRecordOnChange)
        {
            Debug.Log($"SimpleHandController: hand = [" + string.Join(", ", handRecord) + "]");
        }
    }

    public List<string> GetHandSnapshot()
    {
        return new List<string>(handRecord);
    }

    private System.Collections.IEnumerator AnimateTo(RectTransform rt, Vector2 target, float duration)
    {
        if (rt == null) yield break; // 已被銷毀或不存在
        float t = 0f;
        Vector2 start;
        try { start = rt.anchoredPosition; }
        catch { yield break; }

        while (t < duration)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            try { rt.anchoredPosition = Vector2.Lerp(start, target, k); }
            catch { yield break; }
            yield return null;
        }
        if (rt != null)
        {
            try { rt.anchoredPosition = target; } catch { /* ignore */ }
        }
    }
}
