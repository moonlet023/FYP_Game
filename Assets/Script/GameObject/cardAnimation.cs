using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class cardAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public float hoverScale = 1.2f;
    public float animateDuration = 0.15f;
    public bool useRectTransform = true; // UI 卡片使用 RectTransform
    public bool bringToFrontOnHover = true; // 懸停時提到最上層
    public bool enableDrag = true; // 啟用點擊拖動
    public bool blockRaycastsWhileDragging = true; // 拖動時阻擋其他事件

    private Vector3 _originalScale;
    private Coroutine _scaleRoutine;
    private RectTransform _rt;
    private int _originalSiblingIndex = -1;
    private Transform _originalParent;
    private Vector3 _originalLocalPos;
    private CanvasGroup _canvasGroup;
    
    // Start is called before the first frame update
    void Start()
    {
        _originalScale = transform.localScale;
        if (useRectTransform)
        {
            _rt = GetComponent<RectTransform>();
        }
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null && useRectTransform)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (bringToFrontOnHover)
        {
            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
        }
        StartScaleTo(_originalScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartScaleTo(_originalScale);
        if (bringToFrontOnHover && _originalSiblingIndex >= 0)
        {
            transform.SetSiblingIndex(_originalSiblingIndex);
            _originalSiblingIndex = -1;
        }
    }

    private void StartScaleTo(Vector3 targetScale)
    {
        if (_scaleRoutine != null)
        {
            StopCoroutine(_scaleRoutine);
        }
        _scaleRoutine = StartCoroutine(AnimateScale(targetScale, animateDuration));
    }

    private IEnumerator AnimateScale(Vector3 target, float duration)
    {
        float t = 0f;
        Vector3 start = transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(start, target, k);
            yield return null;
        }
        transform.localScale = target;
        _scaleRoutine = null;
    }

    // ------------- 拖動支援 -------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enableDrag) return;

        _originalParent = transform.parent;
        _originalLocalPos = transform.localPosition;
        _originalSiblingIndex = transform.GetSiblingIndex();

        // 提到最上層，避免被遮擋
        transform.SetAsLastSibling();

        // 拖動時可選擇阻擋其他 Graphic Raycasts
        if (_canvasGroup != null && blockRaycastsWhileDragging)
        {
            _canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!enableDrag) return;

        if (useRectTransform && _rt != null)
        {
            // 將螢幕座標轉為父容器的本地座標
            RectTransform parentRT = _rt.parent as RectTransform;
            if (parentRT != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, eventData.position, eventData.pressEventCamera, out localPoint);
                _rt.anchoredPosition = localPoint;
            }
            else
            {
                // 後備：直接置於滑鼠位置（可能不準）
                _rt.position = eventData.position;
            }
        }
        else
        {
            // 世界物件拖動：沿著滑鼠在世界中的位置移動（需要對應相機）
            var cam = eventData.pressEventCamera ?? Camera.main;
            if (cam != null)
            {
                Vector3 world;
                Vector3 screen = new Vector3(eventData.position.x, eventData.position.y, cam.WorldToScreenPoint(transform.position).z);
                world = cam.ScreenToWorldPoint(screen);
                transform.position = world;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!enableDrag) return;

        // 還原 Raycast 阻擋
        if (_canvasGroup != null && blockRaycastsWhileDragging)
        {
            _canvasGroup.blocksRaycasts = true;
        }

        // 可在這裡判斷是否放到合法區域，否則回到原位
        bool droppedToValidArea = false; // TODO: 視需求加上檢測
        if (!droppedToValidArea)
        {
            transform.SetParent(_originalParent, worldPositionStays: false);
            transform.SetSiblingIndex(_originalSiblingIndex >= 0 ? _originalSiblingIndex : 0);
            transform.localPosition = _originalLocalPos;
        }
    }
}
