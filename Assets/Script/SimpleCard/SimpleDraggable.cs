using UnityEngine;
using UnityEngine.EventSystems;

// 最簡版：把這個掛在卡片 UI（含 RectTransform）上
// 需求：場景內要有 EventSystem + Canvas
// 拖曳時讓卡片跟著滑鼠移動，放開時若有丟到 SimpleDropArea，會被該區接住
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class SimpleDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rt;
    private Canvas canvas;
    private CanvasGroup cg;
    private Transform startParent;
    private Vector2 startAnchoredPos;
    public Transform OriginalParent { get; private set; }
    public SimpleHandController OriginalHandController { get; private set; }
    public Handcontroller OriginalTurHandController { get; private set; }

    // 由 SimpleDropArea 設定，表示這次拖曳已被接住
    internal bool wasDroppedThisDrag = false;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("SimpleDraggable: 未找到 Canvas，請確保卡片在 Canvas 下");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        wasDroppedThisDrag = false;
        startParent = transform.parent;
        startAnchoredPos = rt.anchoredPosition;
        OriginalParent = startParent;
        OriginalHandController = OriginalParent != null ? OriginalParent.GetComponent<SimpleHandController>() : null;
        OriginalTurHandController = OriginalParent != null ? OriginalParent.GetComponent<Handcontroller>() : null;

        // 允許射線穿透自己，讓 Drop 區能接到事件
        cg.blocksRaycasts = false;

        // 將卡片移到 Canvas 頂層，避免被其他 UI 遮住（保留世界座標）
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, true);
            transform.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        // 使用 delta/scaleFactor 讓不同解析度下移動一致
        rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 恢復阻擋射線
        cg.blocksRaycasts = true;

        if (!wasDroppedThisDrag)
        {
            // 若沒有被任何 SimpleDropArea 接住，就回到原位
            transform.SetParent(startParent, false);
            rt.anchoredPosition = startAnchoredPos;
            // 回到手牌後也觸發一次重排與記錄
            OriginalHandController?.OnCardAdded(gameObject);
            OriginalTurHandController?.OnCardAdded(gameObject);
        }

        // 重置旗標，供下一次拖曳使用
        wasDroppedThisDrag = false;
    }
}
