using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class textsize : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public TextMeshProUGUI textComponent;
    public RawImage area;
    public bool autoFindMissingReferences = true;
    public bool onlyDragWhenPointerOverArea = true;
    public float dragSensitivity = 1f;

    private RectTransform _viewport;
    private RectTransform _textRect;
    private Vector2 _baseAnchoredPos;
    private string _lastText;
    private float _lastPreferredHeight = -1f;
    private float _currentOffset;
    private Canvas _canvas;

    void Awake()
    {
        ResolveReferences();
        SetupViewportMask();
        CacheTextTransform();
        RebuildAndClamp();
    }

    void Update()
    {
        if (_viewport == null || _textRect == null || textComponent == null) return;

        if (TextLayoutChanged())
            RebuildAndClamp();
    }

    private void ResolveReferences()
    {
        if (textComponent == null && autoFindMissingReferences)
            textComponent = GetComponentInChildren<TextMeshProUGUI>(true);

        if (area == null && autoFindMissingReferences)
            area = GetComponentInChildren<RawImage>(true);

        _viewport = area != null ? area.rectTransform : null;
        _canvas = GetComponentInParent<Canvas>();
    }

    private void SetupViewportMask()
    {
        if (_viewport == null) return;

        var mask = _viewport.GetComponent<RectMask2D>();
        if (mask == null)
            mask = _viewport.gameObject.AddComponent<RectMask2D>();
    }

    private void CacheTextTransform()
    {
        if (textComponent == null) return;

        _textRect = textComponent.rectTransform;
        _baseAnchoredPos = _textRect.anchoredPosition;

        // Keep full text content and let RectMask2D do clipping.
        textComponent.overflowMode = TextOverflowModes.Overflow;
    }

    private bool TextLayoutChanged()
    {
        if (textComponent == null) return false;

        float preferred = textComponent.preferredHeight;
        bool changed = !string.Equals(_lastText, textComponent.text, System.StringComparison.Ordinal)
            || !Mathf.Approximately(preferred, _lastPreferredHeight);

        if (changed)
        {
            _lastText = textComponent.text;
            _lastPreferredHeight = preferred;
        }

        return changed;
    }

    private void RebuildAndClamp()
    {
        if (_viewport == null || _textRect == null || textComponent == null) return;

        float preferredHeight = textComponent.preferredHeight;
        var size = _textRect.sizeDelta;
        _textRect.sizeDelta = new Vector2(size.x, preferredHeight);

        float maxScroll = GetMaxScrollOffset();
        _currentOffset = Mathf.Clamp(_currentOffset, 0f, maxScroll);
        _textRect.anchoredPosition = _baseAnchoredPos + new Vector2(0f, _currentOffset);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Intentionally empty: implementing this ensures EventSystem forwards drag events.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_viewport == null || _textRect == null) return;
        if (onlyDragWhenPointerOverArea && !IsPointerOverViewport(eventData.position)) return;

        float maxScroll = GetMaxScrollOffset();
        if (maxScroll <= 0f) return;

        float deltaY = eventData.delta.y * dragSensitivity;
        _currentOffset = Mathf.Clamp(_currentOffset + deltaY, 0f, maxScroll);
        _textRect.anchoredPosition = _baseAnchoredPos + new Vector2(0f, _currentOffset);
    }

    private float GetMaxScrollOffset()
    {
        if (_viewport == null || _textRect == null) return 0f;

        float viewportHeight = _viewport.rect.height;
        float contentHeight = Mathf.Max(_textRect.rect.height, textComponent != null ? textComponent.preferredHeight : 0f);
        return Mathf.Max(0f, contentHeight - viewportHeight);
    }

    private bool IsPointerOverViewport(Vector2 screenPosition)
    {
        if (_viewport == null) return false;

        Camera cam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(_viewport, screenPosition, cam);
    }
}
