using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 棄牌互動 UI 管理器（Singleton）。
///
/// 使用方式：
///   var discarded = await DiscardSelectUI.Instance.RequestDiscardAsync(count);
///
/// 面板顯示後，玩家點擊手牌卡片以切換選取（黃框）；
/// 選滿 requiredCount 張後確認按鈕變為可按，按下後：
///   1. 呼叫 GamePlay.DiscardSpecificCardFromPlayerHand 移除資料 + 視覺
///   2. 回傳被棄置的 cardId 清單
///
/// Inspector 設定：
///   discardPanel      → 整個棄牌選擇面板根 GameObject（預設隱藏）
///   discardHandArea   → 面板內新的 handarea Transform（可選；若指定則顯示選牌說明）
///   confirmButton     → 確認棄牌按鈕
///   promptText        → 提示文字（可選）
///   countText         → 已選/需選計數文字（可選）
/// </summary>
public class DiscardSelectUI : MonoBehaviour
{
    public static DiscardSelectUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject discardPanel;
    [SerializeField] private Transform  discardHandArea;   // 新的 handarea（供面板顯示用）
    [SerializeField] private Button     confirmButton;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Selection Style")]
    [SerializeField] private Color selectionBorderColor = Color.yellow;
    [SerializeField] private float borderThickness = 4f;

    [Header("References (auto-wired)")]
    [SerializeField] private Handcontroller handController;
    [SerializeField] private Handcontroller discardHandController;
    [SerializeField] private GamePlay gamePlay;

    [Header("Data Source")]
    [SerializeField] private string discardHandDataPath;

    [Header("Discard Card Size")]
    [SerializeField] private bool useFixedCardAspect6x4By8x9 = true;
    [SerializeField] private float discardCardWidth = 220f;
    [SerializeField] private float discardCardMinScale = 0.75f;
    [SerializeField] private float discardCardMinSpacing = 6f;

    [Header("Discard Overflow Drag")]
    [SerializeField] private bool enableHorizontalDragOverflow = true;
    [SerializeField] private int overflowDragStartCount = 8;
    [SerializeField] private float overflowCardSpacing = 24f;
    [SerializeField] private float overflowViewportWidthRatio = 0.9f;
    [SerializeField] private float overflowViewportMinHeight = 340f;

    // ---- private state ----
    private TaskCompletionSource<List<string>> _pendingTcs;
    private int _requiredCount;
    private readonly List<DiscardCardSelector> _selectors = new List<DiscardCardSelector>();
    private readonly List<DiscardCardSelector> _selectedSelectors = new List<DiscardCardSelector>();
    private readonly List<Behaviour> _lockedHandBehaviours = new List<Behaviour>();
    private readonly List<CanvasGroup> _lockedHandCanvasGroups = new List<CanvasGroup>();
    private ScrollRect _discardScrollRect;
    private RectTransform _discardScrollViewport;
    private RectTransform _discardScrollContent;

    // ---- Unity lifecycle ----

    void Awake()
    {
        // 初始化 discardHandDataPath 为 StreamingAssets 路径
        #if UNITY_EDITOR
            discardHandDataPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "json", "hand.json");
        #else
            discardHandDataPath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "json", "hand.json");
        #endif

        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (discardPanel   != null) discardPanel.SetActive(false);
        if (confirmButton  != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
            confirmButton.interactable = false;
        }
    }

    void Start()
    {
        if (handController == null) handController = FindObjectOfType<Handcontroller>(true);
        if (discardHandController == null) discardHandController = handController;
        if (gamePlay       == null) gamePlay       = FindObjectOfType<GamePlay>(true);
    }

    // ---- Public API ----

    /// <summary>
    /// 顯示棄牌 UI，等待玩家選擇 requiredCount 張並確認。
    /// 回傳實際棄置的 cardId 清單（資料已從手牌移除）。
    /// 若 UI 不可用或無手牌，立即 fallback 為自動棄置。
    /// </summary>
    public Task<List<string>> RequestDiscardAsync(int requiredCount)
    {
        if (_pendingTcs != null)
        {
            Debug.LogWarning("[DiscardSelectUI] 已有棄牌請求進行中，忽略重複呼叫。");
            return Task.FromResult(new List<string>());
        }

        _requiredCount = Mathf.Max(1, requiredCount);
        _selectedSelectors.Clear();
        _pendingTcs = new TaskCompletionSource<List<string>>();

        LockPrimaryHandInteractions();
        ShowPanel();
        return _pendingTcs.Task;
    }

    // ---- Panel management ----

    private void ShowPanel()
    {
        ClearSelectors();

        EnsureDiscardPanelInputReady();

        if (discardPanel != null)
        {
            discardPanel.SetActive(true);
            discardPanel.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        var handIds = LoadHandIdsFromData();
        if (handIds.Count == 0)
        {
            Debug.LogWarning("[DiscardSelectUI] 手牌資料為空，無法互動棄牌。");
            HidePanel();
            CompletePending(new List<string>());
            return;
        }

        Transform container = BuildDiscardHandFromData(handIds);
        if (container == null || container.childCount == 0)
        {
            Debug.LogWarning("[DiscardSelectUI] 無法建立棄牌手牌畫面，改用自動棄牌。", this);
            CompletePending(new List<string>());
            return;
        }

        // 為棄牌面板中的每張卡掛上 DiscardCardSelector
        for (int i = 0; i < container.childCount; i++)
        {
            var cardGO = container.GetChild(i).gameObject;
            if (cardGO == null) continue;

            string cardId = ResolveCardId(cardGO);
            var selector  = cardGO.AddComponent<DiscardCardSelector>();
            selector.Init(cardId, selectionBorderColor, borderThickness,
                          OnCardSelected, OnCardDeselected);
            AttachDiscardClickOverlay(cardGO, selector);
            _selectors.Add(selector);
        }

        if (discardPanel != null)
        {
            discardPanel.SetActive(true);
            discardPanel.transform.SetAsLastSibling();
        }
        UpdateUI();
    }

    private void HidePanel()
    {
        ClearSelectors();
        ClearDiscardHandVisuals();
        UnlockPrimaryHandInteractions();
        if (discardPanel != null) discardPanel.SetActive(false);
    }

    // ---- Selection callbacks ----

    private void OnCardSelected(DiscardCardSelector selector)
    {
        if (selector == null) return;
        if (!_selectedSelectors.Contains(selector))
            _selectedSelectors.Add(selector);
        UpdateUI();
    }

    private void OnCardDeselected(DiscardCardSelector selector)
    {
        if (selector == null) return;
        _selectedSelectors.Remove(selector);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (promptText    != null) promptText.text    = $"請選擇 {_requiredCount} 張手牌棄置";
        if (countText     != null) countText.text     = $"已選 {_selectedSelectors.Count} / {_requiredCount}";
        if (confirmButton != null) confirmButton.interactable = _selectedSelectors.Count >= _requiredCount;
    }

    // ---- Confirm ----

    private void OnConfirmClicked()
    {
        if (_pendingTcs == null) return;

        // 取前 _requiredCount 張（多選時僅取需要的數量）
        var toDiscard = new List<string>();
        int need = Mathf.Min(_requiredCount, _selectedSelectors.Count);
        for (int i = 0; i < need; i++)
        {
            var selector = _selectedSelectors[i];
            if (selector == null) continue;

            string cardId = selector.CardId;
            if (string.IsNullOrWhiteSpace(cardId)) continue;
            toDiscard.Add(cardId);
        }

        // 先棄牌（卡片還在 discardHandArea，GamePlay 會從資料層移除）
        if (gamePlay != null)
        {
            foreach (var id in toDiscard)
                gamePlay.DiscardSpecificCardFromPlayerHand(id, updateHandView: true);
        }

        HidePanel();  // 棄牌完成後才還原 + 隱藏面板

        CompletePending(toDiscard);
    }

    // ---- Helpers ----

    private void CompletePending(List<string> result)
    {
        _selectedSelectors.Clear();
        UnlockPrimaryHandInteractions();
        var tcs = _pendingTcs;
        _pendingTcs = null;
        tcs?.TrySetResult(result);
    }

    private void ClearSelectors()
    {
        foreach (var sel in _selectors)
        {
            if (sel != null) Destroy(sel);
        }
        _selectors.Clear();
        _selectedSelectors.Clear();
    }

    private List<string> LoadHandIdsFromData()
    {
        var data = new HandData();
        if (!string.IsNullOrWhiteSpace(discardHandDataPath))
            data.path = discardHandDataPath;

        data.LoadHand();
        return data.Hand != null ? new List<string>(data.Hand) : new List<string>();
    }

    private Transform BuildDiscardHandFromData(List<string> handIds)
    {
        Transform container = ResolveDiscardContainer();
        if (container == null) return null;

        ClearContainerChildren(container);

        if (discardHandController != null)
        {
            discardHandController.handContainer = container;
            discardHandController.handCardTransforms.Clear();
        }

        for (int i = 0; i < handIds.Count; i++)
        {
            string id = handIds[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            CreateDiscardCardVisual(container, id);
        }

        ReflowDiscardCards(container);

        if (discardHandController != null)
            discardHandController.RefreshUIHandRecord();

        return container;
    }

    private Transform ResolveDiscardContainer()
    {
        Transform preferred = null;
        if (discardHandArea != null) preferred = discardHandArea;
        else if (discardHandController != null && discardHandController.handContainer != null)
            preferred = discardHandController.handContainer;
        else if (handController != null && handController.handContainer != null)
            preferred = handController.handContainer;

        if (preferred == null) return null;

        var scrollContent = EnsureHorizontalScrollContainer(preferred);
        return scrollContent != null ? scrollContent : preferred;
    }

    private Transform EnsureHorizontalScrollContainer(Transform preferred)
    {
        if (!enableHorizontalDragOverflow) return null;
        if (discardHandArea == null) return null;
        if (preferred != discardHandArea) return null;

        var baseRect = ResolveDiscardViewportRect(preferred);
        var markerRect = preferred as RectTransform;
        var viewport = EnsureDedicatedDiscardViewport(baseRect, markerRect);
        if (viewport == null) return null;

        _discardScrollViewport = viewport;

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
        }
        viewportImage.raycastTarget = true;

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        _discardScrollRect = viewport.GetComponent<ScrollRect>();
        if (_discardScrollRect == null)
            _discardScrollRect = viewport.gameObject.AddComponent<ScrollRect>();

        var contentTf = viewport.Find("_DiscardScrollContent");
        RectTransform contentRect;
        if (contentTf == null)
        {
            var contentGo = new GameObject("_DiscardScrollContent", typeof(RectTransform));
            contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.SetParent(viewport, false);
        }
        else
        {
            contentRect = contentTf as RectTransform;
        }

        if (contentRect == null) return null;

        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;

        _discardScrollRect.viewport = viewport;
        _discardScrollRect.content = contentRect;
        _discardScrollRect.horizontal = true;
        _discardScrollRect.vertical = false;
        _discardScrollRect.inertia = true;
        _discardScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _discardScrollRect.scrollSensitivity = 0f;

        var noWheel = viewport.GetComponent<NoWheelScrollBlocker>();
        if (noWheel == null)
            viewport.gameObject.AddComponent<NoWheelScrollBlocker>();

        _discardScrollContent = contentRect;
        return _discardScrollContent;
    }

    private RectTransform EnsureDedicatedDiscardViewport(RectTransform baseRect, RectTransform markerRect)
    {
        if (baseRect == null) return null;

        var viewportTf = baseRect.Find("_DiscardScrollViewport");
        RectTransform viewport;
        if (viewportTf == null)
        {
            var viewportGo = new GameObject("_DiscardScrollViewport", typeof(RectTransform));
            viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(baseRect, false);
        }
        else
        {
            viewport = viewportTf as RectTransform;
        }

        if (viewport == null) return null;

        float expectedCardHeight = useFixedCardAspect6x4By8x9
            ? discardCardWidth * 8.9f / 6.4f
            : discardCardWidth;
        float width = Mathf.Max(discardCardWidth * 2.2f, baseRect.rect.width * Mathf.Clamp01(overflowViewportWidthRatio));
        float height = Mathf.Max(overflowViewportMinHeight, expectedCardHeight * 1.2f);

        Vector2 anchored = Vector2.zero;
        if (markerRect != null && markerRect.parent == baseRect)
            anchored = markerRect.anchoredPosition;

        viewport.anchorMin = new Vector2(0.5f, 0.5f);
        viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = anchored;
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        viewport.localScale = Vector3.one;

        return viewport;
    }

    private RectTransform ResolveDiscardViewportRect(Transform preferred)
    {
        var preferredRect = preferred as RectTransform;
        if (preferredRect == null) return null;

        float expectedCardHeight = useFixedCardAspect6x4By8x9
            ? discardCardWidth * 8.9f / 6.4f
            : discardCardWidth;

        bool tooSmall = preferredRect.rect.width < discardCardWidth * 2f
                     || preferredRect.rect.height < expectedCardHeight * 0.8f;
        if (!tooSmall) return preferredRect;

        if (discardPanel != null)
        {
            var panelRect = discardPanel.GetComponent<RectTransform>();
            if (panelRect != null) return panelRect;
        }

        var parentRect = preferredRect.parent as RectTransform;
        if (parentRect != null) return parentRect;

        return preferredRect;
    }

    private void ClearDiscardHandVisuals()
    {
        var container = ResolveDiscardContainer();
        if (container == null) return;

        ClearContainerChildren(container);

        if (discardHandController != null)
        {
            discardHandController.handCardTransforms.Clear();
            discardHandController.RefreshUIHandRecord();
        }
    }

    private void ClearContainerChildren(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private void CreateDiscardCardVisual(Transform container, string id)
    {
        if (container == null || string.IsNullOrWhiteSpace(id)) return;

        GameObject prefab = null;
        if (discardHandController != null)
            prefab = discardHandController.cardPrefab;
        if (prefab == null && handController != null)
            prefab = handController.cardPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("[DiscardSelectUI] 找不到 cardPrefab，無法建立棄牌卡片。", this);
            return;
        }

        var go = Instantiate(prefab, container, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        ApplyDiscardCardSize(go);
        DisableDiscardCloneInteractions(go);
        EnsureDiscardCardCanReceiveClick(go);

        var view = go.GetComponent<CardData>();
        if (view != null)
            view.SetCardId(id);

        var simple = go.GetComponent<SimpleCardData>();
        if (simple != null)
            simple.cardId = id;

        if (discardHandController != null)
            discardHandController.handCardTransforms.Add(go.transform);
    }

    private void ReflowDiscardCards(Transform container)
    {
        var containerRect = container as RectTransform;
        if (containerRect == null) return;

        int count = containerRect.childCount;
        if (count == 0) return;

        bool useScrollLayout = _discardScrollContent != null && containerRect == _discardScrollContent;

        float spacing = discardHandController != null ? Mathf.Max(0f, discardHandController.handSpacing) : 24f;
        if (useScrollLayout)
            spacing = Mathf.Max(spacing, overflowCardSpacing);

        float baseSpacing = spacing;
        float totalWidth = 0f;
        float cardsWidth = 0f;
        var widths = new float[count];

        for (int i = 0; i < count; i++)
        {
            var childRect = containerRect.GetChild(i) as RectTransform;
            float width = GetDiscardCardWidth(childRect);
            widths[i] = width;
            cardsWidth += width;
            totalWidth += width;
            if (i > 0) totalWidth += spacing;
        }

        float availableWidth = useScrollLayout && _discardScrollViewport != null
            ? _discardScrollViewport.rect.width
            : containerRect.rect.width;

        if (useScrollLayout)
        {
            bool enableDrag = count > Mathf.Max(1, overflowDragStartCount);
            totalWidth = cardsWidth + spacing * (count - 1);
            float contentWidth = enableDrag
                ? Mathf.Max(totalWidth, availableWidth + 1f)
                : Mathf.Max(availableWidth, totalWidth);

            float startX = enableDrag ? 0f : (contentWidth - totalWidth) * 0.5f;
            PositionDiscardCards(containerRect, widths, spacing, startX, 1f);

            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);

            if (_discardScrollRect != null)
            {
                _discardScrollRect.horizontal = enableDrag;
                _discardScrollRect.normalizedPosition = enableDrag
                    ? new Vector2(0f, 0.5f)
                    : new Vector2(0.5f, 0.5f);
            }
            return;
        }

        if (availableWidth > 0f && count > 1)
        {
            float fittedSpacing = (availableWidth - cardsWidth) / (count - 1);
            spacing = Mathf.Clamp(fittedSpacing, discardCardMinSpacing, baseSpacing);
            totalWidth = cardsWidth + spacing * (count - 1);
        }

        float scale = 1f;
        if (availableWidth > 0f && totalWidth > availableWidth)
            scale = Mathf.Clamp((availableWidth - Mathf.Max(0f, spacing * (count - 1))) / Mathf.Max(1f, cardsWidth), discardCardMinScale, 1f);

        float cursor = -totalWidth * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var childRect = containerRect.GetChild(i) as RectTransform;
            if (childRect == null) continue;

            childRect.anchorMin = new Vector2(0.5f, 0.5f);
            childRect.anchorMax = new Vector2(0.5f, 0.5f);
            childRect.pivot = new Vector2(0.5f, 0.5f);
            childRect.anchoredPosition = new Vector2((cursor + widths[i] * 0.5f) * scale, 0f);
            childRect.localRotation = Quaternion.identity;
            childRect.localScale = Vector3.one * scale;

            cursor += widths[i] + spacing;
        }
    }

    private void PositionDiscardCards(RectTransform containerRect, float[] widths, float spacing, float startX, float scale)
    {
        float cursor = startX;
        for (int i = 0; i < widths.Length; i++)
        {
            var childRect = containerRect.GetChild(i) as RectTransform;
            if (childRect == null) continue;

            childRect.anchorMin = new Vector2(0f, 0.5f);
            childRect.anchorMax = new Vector2(0f, 0.5f);
            childRect.pivot = new Vector2(0.5f, 0.5f);
            childRect.anchoredPosition = new Vector2((cursor + widths[i] * 0.5f) * scale, 0f);
            childRect.localRotation = Quaternion.identity;
            childRect.localScale = Vector3.one * scale;

            cursor += widths[i] + spacing;
        }
    }

    private float GetDiscardCardWidth(RectTransform cardRect)
    {
        if (cardRect == null) return discardCardWidth;
        if (cardRect.rect.width > 0f) return cardRect.rect.width;
        return discardCardWidth;
    }

    private void DisableDiscardCloneInteractions(GameObject go)
    {
        if (go == null) return;

        DisableComponent<SimpleDraggable>(go);
        DisableComponent<cardAnimation>(go);
        DisableComponent<leftRightClickCard>(go);

        var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null) continue;
            if (behaviour is DiscardCardSelector) continue;
        }

        var canvasGroups = go.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            var cg = canvasGroups[i];
            if (cg == null) continue;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        var graphics = go.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null) continue;
            if (graphic.gameObject == go) continue;
            graphic.raycastTarget = false;
        }
    }

    private void EnsureDiscardCardCanReceiveClick(GameObject go)
    {
        if (go == null) return;

        var graphic = go.GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
        }
        else
        {
            graphic.raycastTarget = true;
        }
    }

    private void AttachDiscardClickOverlay(GameObject go, DiscardCardSelector selector)
    {
        if (go == null || selector == null) return;

        var oldOverlay = go.transform.Find("_DiscardClickOverlay");
        if (oldOverlay != null)
            Destroy(oldOverlay.gameObject);

        var overlay = new GameObject("_DiscardClickOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(go.transform, false);
        overlay.transform.SetAsLastSibling();

        var overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var image = overlay.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var button = overlay.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.interactable = true;
        button.targetGraphic = image;
        button.onClick.AddListener(selector.ToggleSelection);
    }

    private void EnsureDiscardPanelInputReady()
    {
        if (discardPanel == null) return;

        EnsureDiscardPanelOutsideHandArea();

        var canvas = discardPanel.GetComponent<Canvas>();
        if (canvas == null)
            canvas = discardPanel.AddComponent<Canvas>();

        // 建立獨立 Canvas，避免被其他畫布排序與 raycast 狀態影響。
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        var raycaster = discardPanel.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = discardPanel.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        var group = discardPanel.GetComponent<CanvasGroup>();
        if (group == null)
            group = discardPanel.AddComponent<CanvasGroup>();

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = true;
    }

    private void EnsureDiscardPanelOutsideHandArea()
    {
        if (discardPanel == null) return;

        var panelTf = discardPanel.transform;
        Transform handTf = discardHandArea != null
            ? discardHandArea
            : (handController != null ? handController.handContainer : null);

        if (handTf == null) return;
        if (!panelTf.IsChildOf(handTf)) return;

        var parentCanvas = discardPanel.GetComponentInParent<Canvas>(true);
        var rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
        if (rootCanvas == null) return;

        panelTf.SetParent(rootCanvas.transform, false);

        var rt = panelTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }

    private void ApplyDiscardCardSize(GameObject go)
    {
        if (go == null) return;

        var rect = go.GetComponent<RectTransform>();
        if (rect == null) return;

        float width = Mathf.Max(1f, discardCardWidth);
        float height = useFixedCardAspect6x4By8x9 ? width * 8.9f / 6.4f : width;

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        var layout = go.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = width;
            layout.preferredHeight = height;
        }
    }

    private void DisableComponent<T>(GameObject go) where T : Behaviour
    {
        if (go == null) return;

        var components = go.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
                components[i].enabled = false;
        }
    }

    private void LockPrimaryHandInteractions()
    {
        _lockedHandBehaviours.Clear();
        _lockedHandCanvasGroups.Clear();

        LockAllHandComponent<SimpleDraggable>();
        LockAllHandComponent<cardAnimation>();
        LockAllHandComponent<leftRightClickCard>();

        var container = handController != null ? handController.handContainer : null;
        if (container == null) return;

        var discardContainer = ResolveDiscardContainer();
        bool shouldSkipCanvasGroupLock = SharesInteractionHierarchy(container, discardContainer);
        if (shouldSkipCanvasGroupLock)
            return;

        var canvasGroups = container.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            var cg = canvasGroups[i];
            if (cg == null) continue;
            cg.blocksRaycasts = false;
            _lockedHandCanvasGroups.Add(cg);
        }
    }

    private void UnlockPrimaryHandInteractions()
    {
        for (int i = 0; i < _lockedHandBehaviours.Count; i++)
        {
            var behaviour = _lockedHandBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = true;
        }
        _lockedHandBehaviours.Clear();

        for (int i = 0; i < _lockedHandCanvasGroups.Count; i++)
        {
            var cg = _lockedHandCanvasGroups[i];
            if (cg != null)
                cg.blocksRaycasts = true;
        }
        _lockedHandCanvasGroups.Clear();
    }

    private void LockAllHandComponent<T>() where T : Behaviour
    {
        var components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null || !component.enabled) continue;
            if (IsUnderDiscardPanel(component.transform)) continue;
            component.enabled = false;
            _lockedHandBehaviours.Add(component);
        }
    }

    private bool IsUnderDiscardPanel(Transform target)
    {
        if (target == null || discardPanel == null) return false;
        return target.IsChildOf(discardPanel.transform);
    }

    private static bool SharesInteractionHierarchy(Transform handContainer, Transform discardContainer)
    {
        if (handContainer == null || discardContainer == null) return false;
        if (handContainer == discardContainer) return true;
        if (discardContainer.IsChildOf(handContainer)) return true;
        if (handContainer.IsChildOf(discardContainer)) return true;
        return false;
    }

    private sealed class NoWheelScrollBlocker : MonoBehaviour, IScrollHandler
    {
        public void OnScroll(PointerEventData eventData)
        {
            if (eventData != null)
                eventData.Use();
        }
    }

    private static string ResolveCardId(GameObject go)
    {
        if (go == null) return string.Empty;

        var sd = go.GetComponent<SimpleCardData>();
        if (sd != null && !string.IsNullOrWhiteSpace(sd.cardId)) return sd.cardId.Trim();

        var cd = go.GetComponent<CardData>();
        if (cd != null && !string.IsNullOrWhiteSpace(cd.id)) return cd.id.Trim();

        var ci = go.GetComponent<CardIdentity>();
        if (ci != null && !string.IsNullOrWhiteSpace(ci.Id)) return ci.Id.Trim();

        return string.Empty;
    }
}
