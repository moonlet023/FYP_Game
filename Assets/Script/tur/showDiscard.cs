using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class showDiscard : MonoBehaviour
{
    [Header("組件設定（可選，不設則自動尋找）")]
    [SerializeField] private GamePlay gamePlayOverride;
    [SerializeField] private CardEvent cardEventOverride;

    [Header("卡片顯示設定")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform discardAreaRect;

    [Header("佈局設定")]
    [SerializeField] private int cardsPerRow = 3;
    [SerializeField] private float cardSpacing = 8f;
    [SerializeField] private float cardPadding = 8f;
    [SerializeField] private float cardAspectRatio = 1.4f;

    private CardEvent cardEvent;
    private GamePlay gamePlay;
    private ScrollRect scrollRect;
    private RectTransform contentRect;
    private GridLayoutGroup gridLayout;

    void Start()
    {
        StartCoroutine(InitializeAfterDelay());
    }

    void OnEnable()
    {
        if (contentRect != null)
            RefreshAllDiscardCards();
    }

    void OnDestroy()
    {
        if (gamePlay != null)
            gamePlay.OnPlayerDiscardPileUpdated -= OnDiscardPileUpdated;
    }

    private IEnumerator InitializeAfterDelay()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        gamePlay = gamePlayOverride != null ? gamePlayOverride : FindObjectOfType<GamePlay>(true);
        if (gamePlay == null)
            Debug.LogError("[showDiscard] 找不到 GamePlay 組件，請確認場景中有 GamePlay 或在 Inspector 中手動指定", this);
        else
            gamePlay.OnPlayerDiscardPileUpdated += OnDiscardPileUpdated;

        cardEvent = cardEventOverride != null ? cardEventOverride : FindObjectOfType<CardEvent>(true);
        if (cardEvent == null)
            Debug.LogError("[showDiscard] 找不到 CardEvent 組件，請確認場景中有 CardEvent 或在 Inspector 中手動指定", this);

        if (cardPrefab == null)
        {
            cardPrefab = Resources.Load<GameObject>("prefab/card")
                      ?? Resources.Load<GameObject>("prefab/placeCard");
            if (cardPrefab == null)
            {
                var hand = FindObjectOfType<Handcontroller>(true);
                if (hand != null)
                    cardPrefab = hand.cardPrefab;
            }
            if (cardPrefab == null)
                Debug.LogWarning("[showDiscard] 找不到卡片 prefab（Resources 與 Handcontroller 都未取得），請在 Inspector 手動指定", this);
        }

        SetupScrollLayout();
        RefreshAllDiscardCards();
    }

    private void SetupScrollLayout()
    {
        var selfRect = GetComponent<RectTransform>();
        if (discardAreaRect == null)
            discardAreaRect = selfRect;

        // 場景可能誤綁到其他 UI（例如細線 RawImage）；優先使用本面板底下名為 area 的區塊
        var area = transform.Find("area");
        if (area != null)
        {
            var areaRect = area.GetComponent<RectTransform>();
            if (areaRect != null)
                discardAreaRect = areaRect;
        }

        // 若目前指定的顯示區不是此面板子物件，回退到自身
        if (discardAreaRect != null && !discardAreaRect.IsChildOf(transform) && discardAreaRect != selfRect)
            discardAreaRect = selfRect;

        if (discardAreaRect == null)
        {
            Debug.LogError("[showDiscard] 找不到 discardAreaRect，請將此腳本掛在 RawImage 上或手動指定", this);
            return;
        }

        if (discardAreaRect.GetComponent<RectMask2D>() == null)
            discardAreaRect.gameObject.AddComponent<RectMask2D>();

        scrollRect = discardAreaRect.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = discardAreaRect.gameObject.AddComponent<ScrollRect>();

        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        var existingContent = discardAreaRect.Find("DiscardContent");
        if (existingContent != null)
        {
            contentRect = existingContent.GetComponent<RectTransform>();
        }
        else
        {
            var contentGO = new GameObject("DiscardContent");
            contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.SetParent(discardAreaRect, false);
        }

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        gridLayout = contentRect.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            gridLayout = contentRect.gameObject.AddComponent<GridLayoutGroup>();

        var fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        Canvas.ForceUpdateCanvases();
        UpdateGridCellSize();
    }

    private void UpdateGridCellSize()
    {
        if (gridLayout == null || discardAreaRect == null) return;

        float areaWidth = discardAreaRect.rect.width;
        float totalPad = cardPadding * 2f;
        float totalSpac = cardSpacing * (cardsPerRow - 1);
        float cellWidth = (areaWidth - totalPad - totalSpac) / cardsPerRow;
        float cellHeight = cellWidth * cardAspectRatio;

        gridLayout.cellSize = new Vector2(Mathf.Max(cellWidth, 1f), Mathf.Max(cellHeight, 1f));
        gridLayout.spacing = new Vector2(cardSpacing, cardSpacing);
        gridLayout.padding = new RectOffset(
            Mathf.RoundToInt(cardPadding), Mathf.RoundToInt(cardPadding),
            Mathf.RoundToInt(cardPadding), Mathf.RoundToInt(cardPadding));
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = cardsPerRow;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
    }

    private void OnDiscardPileUpdated(List<string> discardCardIds)
    {
        Debug.Log($"[showDiscard] 棄牌區已更新，現有 {discardCardIds.Count} 張卡片", this);
        RefreshAllDiscardCards();
    }

    public void RefreshAllDiscardCards()
    {
        if (gamePlay == null || cardEvent == null || contentRect == null) return;

        Canvas.ForceUpdateCanvases();
        UpdateGridCellSize();

        for (int i = contentRect.childCount - 1; i >= 0; i--)
            Destroy(contentRect.GetChild(i).gameObject);

        var discardIds = gamePlay.GetPlayerDiscardPile();

        if (discardIds == null || discardIds.Count == 0)
        {
            Debug.Log("[showDiscard] 棄牌區為空", this);
            UpdateScrollState(0);
            return;
        }

        int spawned = 0;
        foreach (string id in discardIds)
        {
            if (string.IsNullOrEmpty(id)) continue;

            if (cardPrefab == null) continue;

            GameObject cardGO = Instantiate(cardPrefab, contentRect, false);

            var rt = cardGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(0f, 1f);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }

            var cardComp = cardGO.GetComponent<CardData>();
            if (cardComp != null)
            {
                Tur.CardData data = cardEvent != null ? cardEvent.GetCardById(id) : null;
                if (data != null)
                    cardComp.InitializeFromData(data);
                else
                    cardComp.SetCardId(id);
            }
            spawned++;
        }

        Debug.Log($"[showDiscard] 已顯示 {spawned} 張棄牌卡", this);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        UpdateScrollState(spawned);
    }

    private void UpdateScrollState(int cardCount)
    {
        if (scrollRect == null) return;
        scrollRect.vertical = cardCount > cardsPerRow * 2;
    }

    public void DisplayDiscardCard() => RefreshAllDiscardCards();
    public void RefreshDiscardCard() => RefreshAllDiscardCards();
}
