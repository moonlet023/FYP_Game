using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 最簡版：把這個掛在可放置的 UI 區塊（例如一個空的 Image/RawImage）
// 作用：當有 SimpleDraggable 拖曳結束放到這裡，會把卡片收為子物件並置中
// 加強版：自動確保本物件具備可被射線命中的 Graphic，並提供除錯日誌
public class SimpleDropArea : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    // 限制每個區域只容納一張卡（簡單常見規則）
    public bool oneCardPerArea = true;
    public bool debugLogs = true;

    // 放下時，用顯示用 Prefab 取代原拖曳卡片
    public bool replaceDroppedWithPrefab = true;
    public GameObject displayPrefab; // 指向你的顯示卡片 Prefab（建議為 UI RectTransform）
    // 若區域已有卡片是否允許覆蓋（刪除舊卡後放新卡）
    public bool allowReplaceExisting = false;

    [Header("Mode Display")]
    public SimpleAreaModeDisplay areaModeDisplay; // 可選：放置成功後顯示預設攻擊圖

    [Header("Content Root (Optional)")]
    public Transform contentRoot; // 若指定，所有放置/檢查只針對此節點的子物件，不影響其他 UI（如圖示）

    // 取得放置內容的根節點（未指定則使用自身）
    private Transform Root => contentRoot != null ? contentRoot : transform;

    // 判斷是否為應忽略的 UI 子物件（例如攻/防圖示）
    private bool IsIgnoredChild(Transform child)
    {
        if (areaModeDisplay != null)
        {
            if (child == areaModeDisplay.transform) return true;
            if (areaModeDisplay.iconImage != null && child == areaModeDisplay.iconImage.transform) return true;
        }
        return false;
    }

    void Awake()
    {
        // 確保有 Graphic（例如 Image/RawImage），且 RaycastTarget 為 true
        var graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 透明，不影響外觀
            img.raycastTarget = true;
            graphic = img;
            if (debugLogs) Debug.Log($"SimpleDropArea: added transparent Image for raycast on {name}");
        }
        else
        {
            graphic.raycastTarget = true;
            if (debugLogs) Debug.Log($"SimpleDropArea: found Graphic and enabled raycast on {name}");
        }

        // 確保 Canvas 上有 GraphicRaycaster
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                if (debugLogs) Debug.Log($"SimpleDropArea: added GraphicRaycaster on canvas {canvas.name}");
            }
        }
        else if (debugLogs)
        {
            Debug.LogWarning("SimpleDropArea: not under a Canvas – UI drop will not work");
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (debugLogs) Debug.Log($"SimpleDropArea: OnDrop over {name}");
        var dragged = eventData.pointerDrag;
        if (dragged == null) return;

        var draggable = dragged.GetComponent<SimpleDraggable>();
        var rt = dragged.GetComponent<RectTransform>();
        if (draggable == null || rt == null) return;

        // 若已滿（忽略非卡片 UI，例如攻/防圖示或與內容無關的子物件）
        int occupantCount = 0;
        Transform firstOccupant = null;
        var r = Root;
        for (int i = 0; i < r.childCount; i++)
        {
            var c = r.GetChild(i);
            if (IsIgnoredChild(c)) continue;
            if (firstOccupant == null) firstOccupant = c;
            occupantCount++;
        }

        if (oneCardPerArea && occupantCount > 0)
        {
            if (!allowReplaceExisting)
            {
                if (debugLogs) Debug.Log($"SimpleDropArea: area {name} already occupied, skip");
                return;
            }
            else
            {
                // 覆蓋：刪除舊卡（僅刪除非忽略的第一個佔位）
                if (firstOccupant != null)
                {
                    if (debugLogs) Debug.Log($"SimpleDropArea: replacing existing '{firstOccupant.name}'");
                    Destroy(firstOccupant.gameObject);
                }
            }
        }

        if (replaceDroppedWithPrefab && displayPrefab != null)
        {
            // 刪除原拖曳卡並生成顯示卡片 Prefab
            if (debugLogs) Debug.Log($"SimpleDropArea: destroy dragged '{dragged.name}' and instantiate displayPrefab");
            // 先從手牌容器脫離，避免 Reflow 動畫碰到即將被刪除的物件
            if (draggable.OriginalHandController != null)
            {
                var handTf = draggable.OriginalHandController.transform;
                if (dragged.transform.parent == handTf)
                {
                    dragged.transform.SetParent(null, false);
                }
                // 通知手牌控制器：有卡片被移除（此時該卡已不在手牌之下）
                draggable.OriginalHandController.OnCardRemoved(dragged);
            }
            Destroy(dragged);
            var go = Instantiate(displayPrefab, Root, false);
            var dispRT = go.GetComponent<RectTransform>();
            if (dispRT != null)
            {
                dispRT.anchoredPosition = Vector2.zero;
            }
            else
            {
                go.transform.localPosition = Vector3.zero;
            }

            // 顯示預設攻擊/防禦圖示（預設攻擊）
            areaModeDisplay?.ShowDefault();
        }
        else
        {
            // 接住原卡片：設為子物件並置中
            dragged.transform.SetParent(Root, false);
            rt.anchoredPosition = Vector2.zero;
            if (debugLogs) Debug.Log($"SimpleDropArea: accepted '{dragged.name}' and centered");
            // 通知原手牌控制器移除並重排
            draggable.OriginalHandController?.OnCardRemoved(dragged);

            // 顯示預設攻擊/防禦圖示（預設攻擊）
            areaModeDisplay?.ShowDefault();
        }

        // 通知這次拖曳已成功放置，避免 Draggable 還原
        draggable.wasDroppedThisDrag = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (debugLogs) Debug.Log($"SimpleDropArea: pointer enter {name}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (debugLogs) Debug.Log($"SimpleDropArea: pointer exit {name}");
    }
}
