using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 將此腳本掛在管理物件上：當卡片位置與任一顯示區位置相同（含閾值）時，刪除原卡並在該區生成顯示卡片 Prefab。
public class placecard : MonoBehaviour
{
    [Header("Areas & Card")]
    public RawImage[] displayArea;      // 可放置的顯示區（UI）
    public GameObject card;             // 當前檢測的單一卡片（UI 或世界物件）

    [Header("Dynamic Discovery (optional)")]
    public bool autoFindCardsInContainer = true; // 自動在容器內檢測多張卡片
    public Transform cardContainer;              // 卡片生成的父容器（例如手牌區域）
    public bool autoFindCardsByTag = false;      // 若無容器，可用 Tag 搜尋
    public string cardTag = "Card";              // 卡片的 Tag 名稱

    [Header("Display Card Prefab")]
    public GameObject displayCardPrefab; // 要放置的顯示卡片 Prefab（建議為 UI）

    [Header("Match Settings")]
    public float positionMatchThresholdPixels = 6f; // 位置相同的螢幕像素閾值（越小越嚴格）
    public bool debugLogs = true;

    void Start()
    {
        if (debugLogs)
        {
            int count = displayArea != null ? displayArea.Length : 0;
            Debug.Log($"placecard: displayArea count={count}");
            if (displayArea != null)
            {
                for (int i = 0; i < displayArea.Length; i++)
                {
                    var name = displayArea[i] != null ? displayArea[i].name : "<null>";
                    Debug.Log($"placecard: area[{i}]={name}");
                }
            }
        }
    }

    void Update()
    {
        if (debugLogs)
        {
            int count = displayArea != null ? displayArea.Length : 0;
            Debug.Log($"placecard: Update tick card={(card != null ? card.name : "<null>")} areas={count}");
        }

        // 收集候選卡片：容器、Tag 或單一卡片
        List<GameObject> candidates = new List<GameObject>();
        if (autoFindCardsInContainer && cardContainer != null)
        {
            int childCount = cardContainer.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = cardContainer.GetChild(i).gameObject;
                candidates.Add(child);
            }
            if (debugLogs) Debug.Log($"placecard: found {candidates.Count} cards in container");
        }
        else if (autoFindCardsByTag)
        {
            var tagged = GameObject.FindGameObjectsWithTag(cardTag);
            candidates.AddRange(tagged);
            if (debugLogs) Debug.Log($"placecard: found {tagged.Length} cards by tag '{cardTag}'");
        }
        else if (card != null)
        {
            candidates.Add(card);
        }
        else
        {
            if (debugLogs) Debug.Log("placecard: skip, no candidates (card null & no auto-find)");
            return;
        }
        if (displayArea == null || displayArea.Length == 0)
        {
            if (debugLogs) Debug.Log("placecard: skip, displayArea is null or empty");
            return;
        }

        bool placedThisFrame = false;
        foreach (var candidate in candidates)
        {
            if (candidate == null) continue;
            var cardRT = candidate.GetComponent<RectTransform>();
            Vector2 cardCenterScreen = GetCenterScreenPoint(cardRT, candidate.transform);
            if (debugLogs) Debug.Log($"placecard: card '{candidate.name}' center screen={cardCenterScreen}");

            for (int i = 0; i < displayArea.Length; i++)
            {
                var area = displayArea[i];
                if (area == null) continue;
                RectTransform areaRT = area.GetComponent<RectTransform>();
                Vector2 areaCenterScreen = GetCenterScreenPoint(areaRT, area.transform);
                if (debugLogs) Debug.Log($"placecard: area[{i}] center screen={areaCenterScreen}");

                float dist = Vector2.Distance(cardCenterScreen, areaCenterScreen);
                // 另補：卡片中心是否在顯示區矩形中
                var canvas = area.GetComponentInParent<Canvas>();
                var cam = canvas != null ? canvas.worldCamera : null; // Overlay 為 null
                bool inside = false;
                if (areaRT != null)
                {
                    inside = RectTransformUtility.RectangleContainsScreenPoint(areaRT, cardCenterScreen, cam);
                }

                if (debugLogs)
                {
                    Debug.Log($"placecard: check card='{candidate.name}' area[{i}] dist={dist:F2} inside={inside}");
                }

                if (dist <= positionMatchThresholdPixels || inside)
                {
                    if (debugLogs) Debug.Log($"placecard: MATCH card='{candidate.name}' -> area[{i}] dist={dist:F2} px");
                    ReplaceWithDisplayCard(areaRT, cardRT, candidate);
                    placedThisFrame = true;
                    break;
                }
            }
            if (placedThisFrame) break; // 一幀只處理一張卡
        }
    }

    private Vector2 GetCenterScreenPoint(RectTransform rt, Transform tr)
    {
        // 若為 UI（RectTransform），以四角平均做中心；否則以 transform.position
        var canvas = tr.GetComponentInParent<Canvas>();
        var cam = canvas != null ? canvas.worldCamera : null; // Overlay 為 null
        if (rt != null)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, centerWorld);
            if (debugLogs) Debug.Log($"placecard: GetCenterScreenPoint UI centerWorld={centerWorld} screen={screen}");
            return screen;
        }
        else
        {
            var screen = RectTransformUtility.WorldToScreenPoint(cam, tr.position);
            if (debugLogs) Debug.Log($"placecard: GetCenterScreenPoint worldPos={tr.position} screen={screen}");
            return screen;
        }
    }

    private void ReplaceWithDisplayCard(RectTransform areaRT, RectTransform cardRT, GameObject cardGO)
    {
        if (displayCardPrefab == null)
        {
            Debug.LogWarning("placecard: displayCardPrefab 未設定，無法替換");
            return;
        }

        // 生成顯示卡片到該顯示區
        var go = Instantiate(displayCardPrefab, areaRT);
        if (debugLogs) Debug.Log($"placecard: instantiate displayCardPrefab into area={areaRT.gameObject.name}");
        var dispRT = go.GetComponent<RectTransform>();
        if (dispRT != null)
        {
            dispRT.anchoredPosition = Vector2.zero; // 置中顯示區
            if (debugLogs) Debug.Log("placecard: display card anchoredPosition set to zero (center)");
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            if (debugLogs) Debug.Log("placecard: display card is non-UI, localPosition set to zero");
        }

        // 刪除原卡片
        if (debugLogs) Debug.Log($"placecard: destroy original card {cardGO.name}");
        Destroy(cardGO);
    }
}