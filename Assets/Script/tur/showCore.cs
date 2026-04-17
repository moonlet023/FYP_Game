using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class showCore : MonoBehaviour
{
    [Header("組件設定（可選，不設則自動尋找）")]
    [SerializeField] private GamePlay gamePlayOverride;
    [SerializeField] private CardEvent cardEventOverride;

    [Header("卡片顯示設定")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform coreAreaRect; // RawImage 的 RectTransform（顯示區域根節點）

    [Header("佈局設定")]
    [SerializeField] private int cardsPerRow = 3;
    [SerializeField] private float cardSpacing = 8f;
    [SerializeField] private float cardPadding = 8f;
    [SerializeField] private float cardAspectRatio = 1.4f; // 卡片 高/寬 比例

    private CardEvent cardEvent;
    private GamePlay gamePlay;
    private ScrollRect scrollRect;
    private RectTransform contentRect;
    private GridLayoutGroup gridLayout;

    void Start()
    {
        StartCoroutine(InitializeAfterDelay());
    }

    void OnDestroy()
    {
        if (gamePlay != null)
            gamePlay.OnPlayerCoreAreaUpdated -= OnCoreAreaUpdated;
    }

    private IEnumerator InitializeAfterDelay()
    {
        yield return null;                  // 等待一幀確保其他 Start() 已執行
        yield return new WaitForEndOfFrame(); // 再等 Canvas layout 計算完成

        // 解析 GamePlay
        gamePlay = gamePlayOverride != null ? gamePlayOverride : FindObjectOfType<GamePlay>(true);
        if (gamePlay == null)
            Debug.LogError("[showCore] 找不到 GamePlay 組件，請確認場景中有 GamePlay 或在 Inspector 中手動指定", this);
        else
            gamePlay.OnPlayerCoreAreaUpdated += OnCoreAreaUpdated;

        // 解析 CardEvent
        cardEvent = cardEventOverride != null ? cardEventOverride : FindObjectOfType<CardEvent>(true);
        if (cardEvent == null)
        {
            Debug.LogError("[showCore] 找不到 CardEvent 組件，請確認場景中有 CardEvent 或在 Inspector 中手動指定", this);
        }

        // 解析 prefab
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
                Debug.LogWarning("[showCore] 找不到卡片 prefab（Resources 與 Handcontroller 都未取得），請在 Inspector 手動指定", this);
        }

        // 設置 ScrollRect + GridLayout 結構到 RawImage 區域上
        SetupScrollLayout();

        // 初始顯示
        RefreshAllCoreCards();
    }

    private void SetupScrollLayout()
    {
        // 若未指定，使用自身 RectTransform（此腳本掛在 RawImage 上）
        if (coreAreaRect == null)
            coreAreaRect = GetComponent<RectTransform>();
        if (coreAreaRect == null)
        {
            Debug.LogError("[showCore] 找不到 coreAreaRect，請將此腳本掛在 RawImage 上或手動指定", this);
            return;
        }

        // RectMask2D：讓超出區域的卡片被裁切
        if (coreAreaRect.GetComponent<RectMask2D>() == null)
            coreAreaRect.gameObject.AddComponent<RectMask2D>();

        // ScrollRect：處理滾輪滑動
        scrollRect = coreAreaRect.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = coreAreaRect.gameObject.AddComponent<ScrollRect>();

        scrollRect.horizontal     = false;
        scrollRect.vertical       = false; // 初始禁用，超過 2 行才開啟
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType   = ScrollRect.MovementType.Clamped;
        scrollRect.inertia        = false;

        // 建立或取得 Content 子物件（放置所有卡片）
        var existingContent = coreAreaRect.Find("CoreContent");
        if (existingContent != null)
        {
            contentRect = existingContent.GetComponent<RectTransform>();
        }
        else
        {
            var contentGO = new GameObject("CoreContent");
            contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.SetParent(coreAreaRect, false);
        }

        // Content 對齊頂部，寬度撐滿父物件
        contentRect.anchorMin        = new Vector2(0f, 1f);
        contentRect.anchorMax        = new Vector2(1f, 1f);
        contentRect.pivot            = new Vector2(0.5f, 1f);
        contentRect.sizeDelta        = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        // GridLayoutGroup：固定每行 3 張
        gridLayout = contentRect.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            gridLayout = contentRect.gameObject.AddComponent<GridLayoutGroup>();

        // ContentSizeFitter：讓 Content 高度跟著卡片數量自動延伸
        var fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // 綁定 ScrollRect
        scrollRect.content = contentRect;

        // 強制 Canvas 重算 layout，確保 rect.width 有正確的值
        Canvas.ForceUpdateCanvases();
        UpdateGridCellSize();
    }

    private void UpdateGridCellSize()
    {
        if (gridLayout == null || coreAreaRect == null) return;

        float areaWidth  = coreAreaRect.rect.width;
        float totalPad   = cardPadding * 2f;
        float totalSpac  = cardSpacing * (cardsPerRow - 1);
        float cellWidth  = (areaWidth - totalPad - totalSpac) / cardsPerRow;
        float cellHeight = cellWidth * cardAspectRatio;

        gridLayout.cellSize        = new Vector2(Mathf.Max(cellWidth, 1f), Mathf.Max(cellHeight, 1f));
        gridLayout.spacing         = new Vector2(cardSpacing, cardSpacing);
        gridLayout.padding         = new RectOffset(
            Mathf.RoundToInt(cardPadding), Mathf.RoundToInt(cardPadding),
            Mathf.RoundToInt(cardPadding), Mathf.RoundToInt(cardPadding));
        gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = cardsPerRow;
        gridLayout.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis       = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment  = TextAnchor.UpperLeft;
    }

    private void OnCoreAreaUpdated(List<string> coreCardIds)
    {
        Debug.Log($"[showCore] Core 區已更新，現有 {coreCardIds.Count} 張卡片", this);
        RefreshAllCoreCards();
    }

    /// <summary>重新顯示全部 core 卡</summary>
    public void RefreshAllCoreCards()
    {
        if (gamePlay == null || cardEvent == null || contentRect == null) return;

        // 每次刷新前重算尺寸，防止 rect 在第一次呼叫時尚未就緒
        Canvas.ForceUpdateCanvases();
        UpdateGridCellSize();

        // 清空 Content 所有子物件
        for (int i = contentRect.childCount - 1; i >= 0; i--)
            Destroy(contentRect.GetChild(i).gameObject);

        var coreIds = gamePlay.GetPlayerCoreArea();

        if (coreIds == null || coreIds.Count == 0)
        {
            Debug.Log("[showCore] Core 區為空", this);
            UpdateScrollState(0);
            return;
        }

        int spawned = 0;
        foreach (string id in coreIds)
        {
            if (string.IsNullOrEmpty(id)) continue;

            if (cardPrefab == null) continue;

            GameObject cardGO = Instantiate(cardPrefab, contentRect, false);

            // 重置 RectTransform，讓 GridLayoutGroup 從左上角正確排列
            var rt = cardGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot        = new Vector2(0f, 1f);
                rt.anchorMin    = new Vector2(0f, 1f);
                rt.anchorMax    = new Vector2(0f, 1f);
                rt.localScale   = Vector3.one;
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

        Debug.Log($"[showCore] 已顯示 {spawned} 張 core 卡", this);

        // 強制重算 GridLayout 位置，確保所有卡片在正確位置後再更新滾輪狀態
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        UpdateScrollState(spawned);
    }

    // 卡片數超過 2 行才開啟垂直滾輪
    private void UpdateScrollState(int cardCount)
    {
        if (scrollRect == null) return;
        scrollRect.vertical = cardCount > cardsPerRow * 2;
    }

    // 保留向後相容的舊介面
    public void DisplayCoreCard() => RefreshAllCoreCards();
    public void RefreshCoreCard() => RefreshAllCoreCards();
}
