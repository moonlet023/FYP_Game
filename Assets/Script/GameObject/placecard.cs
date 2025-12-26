using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 將此腳本掛在「顯示區域管理」物件或任何常駐管理者上
// 功能：當卡片（UI 物件）與任一顯示區 RawImage 的矩形區域重疊時，將卡片貼齊到該顯示區並（可選）停用拖曳。
public class placecard : MonoBehaviour
{
    [Header("Card instance to monitor")]
    public GameObject card; // 場景中的卡片實例（非 prefab）。

    [Header("Display Areas (UI RawImage)")]
    public RawImage[] displayAreas; // 可指定一個或多個顯示區 UI。

    [Header("Placement Options")]
    public bool lockOnPlace = true;           // 放置後是否鎖定（停用拖曳）。
    public Behaviour dragScriptToDisable;     // 指定負責拖曳的腳本（例如自訂的 Draggable），放置後會停用。
    public bool centerInArea = true;          // 放置時置中顯示區。
    public Transform snapPoint;               // 可選：若指定，放置時貼齊到此點（需在同 Canvas/座標系統下）。

    [Header("Place Card Prefab (optional)")]
    public GameObject placeCardPrefab;        // 另一個用於放置時顯示的卡片 Prefab（UI）。
    public bool usePlacePrefab = true;        // 若為真且有 prefab，放置時會 Instantiate 該 prefab；否則直接重設父物件使用原卡。
    public bool hideSourceCardOnPlace = true; // 使用 Prefab 放置後是否隱藏原卡片（SetActive(false)）。
    public GameObject placedInstance;         // 放置生成的實例（唯讀觀察用途）。

    private bool isPlaced = false;
    [Header("Debug")]
    public bool debugLogs = true;             // 顯示除錯資訊

    void Update()
    {
        if (isPlaced) return;
        if (card == null) return;
        if (displayAreas == null || displayAreas.Length == 0) return;

        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect == null) return; // 僅支援 UI 卡片（需為 RectTransform）

        // 檢查卡片是否與任一顯示區矩形重疊
        foreach (var area in displayAreas)
        {
            if (area == null) continue;

            RectTransform areaRect = area.GetComponent<RectTransform>();
            if (areaRect == null) continue;

            if (RectOverlaps(cardRect, areaRect))
            {
                PlaceCardIntoArea(cardRect, areaRect);
                break;
            }
        }
    }

    // 將卡片放置到指定顯示區
    private void PlaceCardIntoArea(RectTransform cardRect, RectTransform areaRect)
    {
        if (usePlacePrefab && placeCardPrefab != null)
        {
            // 使用外部 Prefab 生成放置卡片
            placedInstance = Instantiate(placeCardPrefab, areaRect);
            var placedRect = placedInstance.GetComponent<RectTransform>();
            if (placedRect != null)
            {
                if (snapPoint != null)
                {
                    Vector2 localPoint;
                    var cam = areaRect.GetComponentInParent<Canvas>()?.worldCamera;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        areaRect,
                        RectTransformUtility.WorldToScreenPoint(cam, snapPoint.position),
                        cam,
                        out localPoint
                    );
                    placedRect.anchoredPosition = localPoint;
                }
                else if (centerInArea)
                {
                    placedRect.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                // 若 Prefab 不是 UI，退回 transform 置中
                placedInstance.transform.localPosition = Vector3.zero;
            }

            if (hideSourceCardOnPlace && card != null)
            {
                card.SetActive(false);
            }
        }
        else
        {
            // 直接使用原卡片：設定父物件以便 UI 對齊（不保留世界座標）
            cardRect.SetParent(areaRect, worldPositionStays: false);

            if (snapPoint != null)
            {
                Vector2 localPoint;
                var cam = areaRect.GetComponentInParent<Canvas>()?.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    areaRect,
                    RectTransformUtility.WorldToScreenPoint(cam, snapPoint.position),
                    cam,
                    out localPoint
                );
                cardRect.anchoredPosition = localPoint;
            }
            else if (centerInArea)
            {
                cardRect.anchoredPosition = Vector2.zero;
            }
        }

        // 停用拖曳腳本（若已指定）
        if (lockOnPlace && dragScriptToDisable != null)
        {
            dragScriptToDisable.enabled = false;
        }

        isPlaced = true;
    }

    // 判斷兩個 UI RectTransform 是否在世界座標下重疊
    private bool RectOverlaps(RectTransform a, RectTransform b)
    {
        // 使用螢幕座標判斷角點/中心是否落在對方矩形中，適用 Overlay 與 Camera Canvas
        var canvas = b.GetComponentInParent<Canvas>();
        Camera cam = canvas != null ? canvas.worldCamera : null; // Overlay 為 null

        var aCorners = new Vector3[4];
        var bCorners = new Vector3[4];
        a.GetWorldCorners(aCorners);
        b.GetWorldCorners(bCorners);

        // 將角點轉為螢幕座標
        var aScreen = new Vector2[4];
        var bScreen = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            aScreen[i] = RectTransformUtility.WorldToScreenPoint(cam, aCorners[i]);
            bScreen[i] = RectTransformUtility.WorldToScreenPoint(cam, bCorners[i]);
        }

        // A 的任一角點在 B 內
        for (int i = 0; i < 4; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(b, aScreen[i], cam))
            {
                if (debugLogs) Debug.Log("placecard: A corner inside B");
                return true;
            }
        }
        // B 的任一角點在 A 內（涵蓋 B 完全包含於 A 的情況）
        for (int i = 0; i < 4; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(a, bScreen[i], cam))
            {
                if (debugLogs) Debug.Log("placecard: B corner inside A");
                return true;
            }
        }
        // 補充：A 的中心在 B 內
        Vector3 aCenterWorld = (aCorners[0] + aCorners[2]) * 0.5f;
        Vector2 aCenterScreen = RectTransformUtility.WorldToScreenPoint(cam, aCenterWorld);
        if (RectTransformUtility.RectangleContainsScreenPoint(b, aCenterScreen, cam))
        {
            if (debugLogs) Debug.Log("placecard: A center inside B");
            return true;
        }

        return false;
    }
}
