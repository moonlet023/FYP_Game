using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 棄牌選擇模式下，臨時附加到手牌 GameObject 的元件。
/// 點擊切換選取狀態，並顯示/隱藏黃色邊框。
/// 不影響原卡片的任何其他元件或外觀，Destroy 後自動移除邊框。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DiscardCardSelector : MonoBehaviour, IPointerClickHandler
{
    private string _cardId;
    private bool _isSelected;
    private Action<DiscardCardSelector> _onSelected;
    private Action<DiscardCardSelector> _onDeselected;
    private GameObject _borderOverlay;
    private Color _borderColor;
    private float _borderThickness;

    public bool IsSelected => _isSelected;
    public string CardId => _cardId;

    /// <summary>
    /// 初始化選擇器（由 DiscardSelectUI 呼叫）。
    /// </summary>
    public void Init(string cardId, Color borderColor, float borderThickness,
                     Action<DiscardCardSelector> onSelected, Action<DiscardCardSelector> onDeselected)
    {
        _cardId      = cardId;
        _borderColor = borderColor;
        _borderThickness = borderThickness;
        _onSelected  = onSelected;
        _onDeselected = onDeselected;
        _isSelected  = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleSelection();
    }

    public void ToggleSelection()
    {
        _isSelected = !_isSelected;
        if (_isSelected)
        {
            CreateBorder();
            _onSelected?.Invoke(this);
        }
        else
        {
            DestroyBorder();
            _onDeselected?.Invoke(this);
        }
    }

    // ---- 邊框繪製 ----

    private void CreateBorder()
    {
        if (_borderOverlay != null) return;

        _borderOverlay = new GameObject("_DiscardBorder", typeof(RectTransform));
        _borderOverlay.transform.SetParent(transform, false);
        _borderOverlay.transform.SetAsLastSibling();

        var rt = _borderOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 四條邊
        CreateSide(_borderOverlay.transform, "Top",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -_borderThickness), new Vector2(0f, 0f));
        CreateSide(_borderOverlay.transform, "Bottom",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, _borderThickness));
        CreateSide(_borderOverlay.transform, "Left",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(_borderThickness, 0f));
        CreateSide(_borderOverlay.transform, "Right",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-_borderThickness, 0f), new Vector2(0f, 0f));
    }

    private void CreateSide(Transform parent, string sideName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(sideName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;

        var img = go.GetComponent<Image>();
        img.color = _borderColor;
        img.raycastTarget = false;
    }

    private void DestroyBorder()
    {
        if (_borderOverlay == null) return;
        Destroy(_borderOverlay);
        _borderOverlay = null;
    }

    void OnDestroy()
    {
        DestroyBorder();
    }
}
