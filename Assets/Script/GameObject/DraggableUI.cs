using UnityEngine;
using UnityEngine.EventSystems;

// 簡易 UI 拖曳腳本：將此元件掛在卡片 Prefab 上
// 功能：在拖曳開始時通知 placecard.BeginTracking(this.gameObject)，拖曳結束通知 EndTracking
// 並以 anchoredPosition 移動卡片
[RequireComponent(typeof(RectTransform))]
public class DraggableUI : MonoBehaviour
{
    private RectTransform rt;
    private Canvas canvas;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 pos;
        var cam = canvas != null ? canvas.worldCamera : null; // Overlay 為 null
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt.parent as RectTransform, eventData.position, cam, out pos);
        rt.anchoredPosition = pos;
    }
}
