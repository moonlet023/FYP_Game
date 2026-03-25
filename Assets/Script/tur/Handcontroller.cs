using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Tur;

public class Handcontroller : MonoBehaviour
{
    [System.Serializable]
    public class CardDataEvent : UnityEvent<Tur.CardData> {}

    public GameObject cardPrefab;
    public HandData handData;
    public DeckData deckData;
    [Header("動畫與版面設定")]
    public Transform deckSpawnPoint;        // 抽牌起始位置（例如牌堆頂）
    public Transform handContainer;          // 承載手牌的父物件（其座標系為版面基準）
    public float handSpacing = 1.2f;         // 手牌水平間距（localX）
    public float drawDuration = 0.4f;        // 抽牌飛入時間
    public AnimationCurve drawCurve;         // 動畫曲線（0→1）

    // 目前在手上的卡牌對應的 Transform（若 handData 內含物件，可在外部同步填入）
    public List<Transform> handCardTransforms = new List<Transform>();

    [Header("事件控制（可由 UI/Button/EventTrigger 觸發）")]
    public UnityEvent addCardDefaultEvent;   // 觸發預設加入卡片
    public CardDataEvent addCardDataEvent;   // 觸發以資料加入卡片

    [Header("卡片大小設定")]
    public bool normalizeCardSize = true;     // 是否正規化卡片尺寸（UI）
    public float targetCardWidth = 160f;      // 目標寬度（UI）
    public float targetCardHeight = 220f;     // 目標高度（UI）
    public bool useUniformScale = false;      // 是否使用統一縮放（非 UI 或輔助 UI 效果）
    public float uniformScale = 1f;           // 統一縮放比例

    [Header("UI 版面（SimpleHandController 同款行為）")]
    public bool useUIRectReflow = true;   // 啟用 UI RectTransform 重排
    public bool uiAnimateReflow = true;   // 是否啟用重排動畫
    public float uiReflowDuration = 0.2f; // 動畫時間（秒）
    public bool uiCenterHand = true;      // 是否置中整排卡片

    [Header("手牌橫向檢視")]
    public int handScrollThreshold = 13;   // 手牌超過此數量才啟用橫向檢視
    public bool enableWheelHandScroll = true; // 以滑鼠滾輪左右檢視手牌
    public float handWheelScrollSpeed = 60f;  // 每次滾輪移動的像素量

    private readonly List<string> uiHandRecord = new List<string>();
    private float currentHandContentWidth = 0f;
    private float currentHandVisibleWidth = 0f;

    void Start()
    {
        handData = new HandData();
        deckData = new DeckData();
        if (drawCurve == null)
        {
            // 預設使用緩入緩出
            drawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        // 初始化事件並綁定行為
        if (addCardDefaultEvent == null) addCardDefaultEvent = new UnityEvent();
        if (addCardDataEvent == null) addCardDataEvent = new CardDataEvent();
        addCardDefaultEvent.RemoveAllListeners();
        addCardDataEvent.RemoveAllListeners();
        addCardDefaultEvent.AddListener(AddCardToHandDefault);
        addCardDataEvent.AddListener(AddCardToHand);
    }

    void Update()
    {
        HandleHandWheelScroll();
    }

    public void init()
    {
        deckData.suffleDeck();
        var ids = deckData.drawCard(handData, 5);
        if (ids != null)
        {
            foreach (var id in ids)
            {
                AddCardToHandById(id);
            }
        }
        handData.PrintHandLog();
        deckData.PrintDeckLog();

        // 若已有對應的卡牌 GameObject，可於外部填入 handCardTransforms 後置中
        CenterHand();
    }

    // 提供給 UI Button/EventTrigger 呼叫
    public void InvokeAddCardDefaultEvent()
    {
        addCardDefaultEvent?.Invoke();
    }

    // 注意：Tur.CardData 無法在 Inspector 直接配置；通常由程式端呼叫
    public void InvokeAddCardDataEvent(Tur.CardData data)
    {
        addCardDataEvent?.Invoke(data);
    }

    // 將目前手牌在 handContainer 下以等距方式水平置中排布
    public void CenterHand()
    {
        if (handContainer == null) return;
        var count = handCardTransforms.Count;
        if (count == 0) return;

        // 以 handContainer 的 local 空間為基準，沿 X 排列並置中
        float startX = -(count - 1) * 0.5f * handSpacing;
        for (int i = 0; i < count; i++)
        {
            var t = handCardTransforms[i];
            if (t == null) continue;
            t.SetParent(handContainer, worldPositionStays: false);
            var targetLocal = new Vector3(startX + i * handSpacing, 0f, 0f);
            t.localPosition = targetLocal;
        }
    }

    // 將某張牌動畫地移動到置中位置（相對於 handContainer 的 local 原點）
    public void CenterSelectedCard(Transform card)
    {
        if (card == null || handContainer == null) return;
        StartCoroutine(AnimateToLocal(card, Vector3.zero, drawDuration));
    }

    // 抽牌動畫：將卡牌從 deckSpawnPoint 世界座標飛到 handContainer 內對應索引的置中排布位置
    public void PlayDrawCardAnimation(Transform cardTransform, int targetIndex)
    {
        if (cardTransform == null || handContainer == null || deckSpawnPoint == null) return;

        // 計算目標 local 位置（把該牌視為已加入 hand，重算置中座標）
        int count = Mathf.Max(handCardTransforms.Count, 0) + 1;
        float startX = -(count - 1) * 0.5f * handSpacing;
        targetIndex = Mathf.Clamp(targetIndex, 0, count - 1);
        Vector3 targetLocal = new Vector3(startX + targetIndex * handSpacing, 0f, 0f);

        // 設置父子關係到 handContainer，並將起始位置放在 deckSpawnPoint（世界座標）
        cardTransform.SetParent(handContainer, worldPositionStays: true);
        cardTransform.position = deckSpawnPoint.position;

        // 啟動動畫，飛入目標 local 位置
        StartCoroutine(AnimateToLocal(cardTransform, targetLocal, drawDuration, onComplete: () =>
        {
            // 動畫完成後，真正加入手牌清單並重新置中一次
            handCardTransforms.Insert(Mathf.Clamp(targetIndex, 0, handCardTransforms.Count), cardTransform);
            // UI 模式：使用 RectTransform 重排；否則用等距 CenterHand
            var isUI = handContainer is RectTransform;
            if (useUIRectReflow && isUI)
            {
                ReflowHandUI();
                RefreshUIHandRecord();
            }
            else
            {
                CenterHand();
            }
        }));
    }

    // 協程：將目標 Transform 由目前 localPosition 動畫到目標 localPosition
    private IEnumerator AnimateToLocal(Transform t, Vector3 targetLocal, float duration, System.Action onComplete = null)
    {
        if (t == null) yield break;
        if (duration <= 0f)
        {
            t.localPosition = targetLocal;
            onComplete?.Invoke();
            yield break;
        }

        Vector3 startLocal = t.parent != null ? t.localPosition : t.position;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / duration);
            float k = drawCurve != null ? drawCurve.Evaluate(p) : p;
            Vector3 cur = Vector3.Lerp(startLocal, targetLocal, k);
            if (t.parent != null)
                t.localPosition = cur;
            else
                t.position = cur; // 若無父物件就以世界座標移動
            yield return null;
        }
        if (t.parent != null)
            t.localPosition = targetLocal;
        else
            t.position = targetLocal;
        onComplete?.Invoke();
    }

    // 當有卡片加入到手牌容器（UI 模式）
    public void OnCardAdded(GameObject added)
    {
        if (added == null) return;

        if (!handCardTransforms.Contains(added.transform))
            handCardTransforms.Add(added.transform);

        if (!useUIRectReflow)
        {
            CenterHand();
            return;
        }

        ReflowHandUI();
        RefreshUIHandRecord();
    }

    // 當有卡片從手牌容器移除（UI 模式）
    public void OnCardRemoved(GameObject removed)
    {
        if (removed == null) return;

        RemoveFromHandCardTransforms(removed.transform);

        if (!useUIRectReflow)
        {
            CenterHand();
            return;
        }

        ReflowHandUI();
        RefreshUIHandRecord();
    }

    // 以 RectTransform 版面重排（等效 SimpleHandController.ReflowHand）
    public void ReflowHandUI()
    {
        var handRT = handContainer as RectTransform;
        if (!useUIRectReflow || handRT == null) return;

        SyncHandCardTransformsFromContainer();
        float spacing = handSpacing;

        int n = handRT.childCount;
        if (n == 0) return;

        float totalWidth = 0f;
        var widths = new float[n];
        for (int i = 0; i < n; i++)
        {
            var child = handRT.GetChild(i) as RectTransform;
            if (child == null) { widths[i] = 0f; continue; }
            widths[i] = GetCardWidth(child);
            totalWidth += widths[i];
            if (i > 0) totalWidth += spacing;
        }

        currentHandContentWidth = totalWidth;
        currentHandVisibleWidth = GetVisibleWidthForScroll(widths, spacing, n);
        bool scrollActive = IsHandScrollActive(handRT, totalWidth);

        float startX = (uiCenterHand && !scrollActive)
            ? -totalWidth * 0.5f
            : -currentHandVisibleWidth * 0.5f;
        float cursor = startX;

        for (int i = 0; i < n; i++)
        {
            var child = handRT.GetChild(i) as RectTransform;
            if (child == null) continue;
            float targetX = cursor + widths[i] * 0.5f;
            Vector2 target = new Vector2(targetX, 0f);

            if (uiAnimateReflow)
                StartCoroutine(AnimateToAnchored(child, target, uiReflowDuration));
            else
                child.anchoredPosition = target;

            cursor += widths[i] + spacing;
        }

        ClampHandContainerX(handRT);
    }

    private bool IsHandScrollActive(RectTransform handRT, float totalWidth)
    {
        if (!useUIRectReflow || handRT == null) return false;
        if (handRT.childCount <= handScrollThreshold) return false;
        return totalWidth > currentHandVisibleWidth + 1f;
    }

    private void HandleHandWheelScroll()
    {
        if (!enableWheelHandScroll || !useUIRectReflow) return;

        var handRT = handContainer as RectTransform;
        if (handRT == null) return;
        if (!IsHandScrollActive(handRT, currentHandContentWidth)) return;

        var viewport = handRT.parent as RectTransform;
        if (viewport == null) return;

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) < 0.0001f) return;

        var pos = handRT.anchoredPosition;
        pos.x += wheel * handWheelScrollSpeed;
        handRT.anchoredPosition = pos;
        ClampHandContainerX(handRT);
    }

    private void ClampHandContainerX(RectTransform handRT)
    {
        if (handRT == null) return;

        var viewport = handRT.parent as RectTransform;
        if (viewport == null)
        {
            var p = handRT.anchoredPosition;
            p.x = 0f;
            handRT.anchoredPosition = p;
            return;
        }

        float visibleWidth = currentHandVisibleWidth > 0f ? currentHandVisibleWidth : viewport.rect.width;
        float overflow = Mathf.Max(0f, currentHandContentWidth - visibleWidth);
        var pos = handRT.anchoredPosition;

        if (overflow <= 0f || handRT.childCount <= handScrollThreshold)
        {
            pos.x = 0f;
        }
        else
        {
            pos.x = Mathf.Clamp(pos.x, -overflow, 0f);
        }

        handRT.anchoredPosition = pos;
    }

    private float GetVisibleWidthForScroll(float[] widths, float spacing, int totalCards, float viewportWidth)
    {
        if (widths == null || totalCards <= 0)
            return 0f;

        if (handScrollThreshold <= 0 || totalCards <= handScrollThreshold)
        {
            float full = 0f;
            for (int i = 0; i < totalCards; i++)
            {
                full += widths[i];
                if (i > 0) full += spacing;
            }
            return Mathf.Max(0f, full);
        }

        int visibleCards = Mathf.Min(handScrollThreshold, totalCards);
        float desired = 0f;
        for (int i = 0; i < visibleCards; i++)
        {
            desired += widths[i];
            if (i > 0) desired += spacing;
        }

        return Mathf.Max(0f, desired);
    }

    private float GetVisibleWidthForScroll(float[] widths, float spacing, int totalCards)
    {
        return GetVisibleWidthForScroll(widths, spacing, totalCards, 0f);
    }

    private float GetCardWidth(RectTransform child)
    {
        if (child == null) return 0f;

        var le = child.GetComponent<LayoutElement>();
        if (le != null && le.preferredWidth > 0f)
            return le.preferredWidth;

        if (child.rect.width > 0f)
            return child.rect.width;

        return Mathf.Max(1f, targetCardWidth);
    }

    private void RemoveFromHandCardTransforms(Transform removed)
    {
        for (int i = handCardTransforms.Count - 1; i >= 0; i--)
        {
            var t = handCardTransforms[i];
            if (t == null || t == removed)
                handCardTransforms.RemoveAt(i);
        }
    }

    private void SyncHandCardTransformsFromContainer()
    {
        if (handContainer == null) return;

        handCardTransforms.Clear();
        for (int i = 0; i < handContainer.childCount; i++)
        {
            handCardTransforms.Add(handContainer.GetChild(i));
        }
    }

    // 取得手牌容器所在 Canvas 的相機；Overlay 模式回傳 null
    private Camera GetCanvasCamera(Transform t)
    {
        var canvas = t != null ? t.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.worldCamera : null;
    }

    // 重建 UI 模式的手牌記錄
    public void RefreshUIHandRecord()
    {
        uiHandRecord.Clear();
        var handRT = handContainer as RectTransform;
        if (handRT == null) return;
        int n = handRT.childCount;
        for (int i = 0; i < n; i++)
        {
            var child = handRT.GetChild(i).gameObject;
            var simpleData = child.GetComponent<SimpleCardData>();
            var viewData = child.GetComponent<global::CardData>();
            string id = null;
            if (simpleData != null && !string.IsNullOrEmpty(simpleData.cardId)) id = simpleData.cardId;
            else if (viewData != null && !string.IsNullOrEmpty(viewData.cardName)) id = viewData.cardName;
            else id = child.name;
            uiHandRecord.Add(id);
        }
        Debug.Log("Handcontroller(UI): hand = [" + string.Join(", ", uiHandRecord) + "]");
    }

    private IEnumerator AnimateToAnchored(RectTransform rt, Vector2 target, float duration)
    {
        if (rt == null) yield break;
        float t = 0f;
        Vector2 start;
        try { start = rt.anchoredPosition; }
        catch { yield break; }

        while (t < duration)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            try { rt.anchoredPosition = Vector2.Lerp(start, target, k); }
            catch { yield break; }
            yield return null;
        }
        if (rt != null)
        {
            try { rt.anchoredPosition = target; } catch { }
        }
    }

    // 套用卡片大小：UI 使用 RectTransform/LayoutElement，否則使用 localScale
    private void ApplyNormalizedCardSize(GameObject go)
    {
        if (go == null) return;
        if (!normalizeCardSize && !useUniformScale) return;

        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            if (normalizeCardSize)
            {
                if (targetCardWidth > 0)
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetCardWidth);
                if (targetCardHeight > 0)
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetCardHeight);

                var le = go.GetComponent<LayoutElement>();
                if (le != null)
                {
                    if (targetCardWidth > 0) le.preferredWidth = targetCardWidth;
                    if (targetCardHeight > 0) le.preferredHeight = targetCardHeight;
                }
            }

            if (useUniformScale)
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, uniformScale);
        }
        else
        {
            if (useUniformScale)
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, uniformScale);
        }
    }

    // 視覺：以預設資料建立一張卡並加入手牌（使用 prefab 自帶的 CardData.Start 載入）
    public void AddCardToHandDefault()
    {
        if (cardPrefab == null || handContainer == null || deckSpawnPoint == null)
        {
            Debug.LogWarning("AddCardToHandDefault: Missing prefab or container/spawn setup.");
            return;
        }

        var go = Instantiate(cardPrefab);
        ApplyNormalizedCardSize(go);
        var t = go.transform;
        // 動畫飛入到下一個位置
        PlayDrawCardAnimation(t, handCardTransforms.Count);
        // 動畫完成後於 PlayDrawCardAnimation 觸發重排
    }

    // 視覺 + 資料：以傳入的資料建立卡片並加入手牌
    public void AddCardToHand(Tur.CardData data)
    {
        if (cardPrefab == null || handContainer == null || deckSpawnPoint == null)
        {
            Debug.LogWarning("AddCardToHand: Missing prefab or container/spawn setup.");
            return;
        }

        var go = Instantiate(cardPrefab);
        ApplyNormalizedCardSize(go);
        var view = go.GetComponent<global::CardData>();
        if (view != null && data != null)
        {
            // 讓卡片以程式資料初始化，避免 Start 讀檔覆蓋
            view.overrideFromCode = true;
            view.InitializeFromData(data);
        }

        var t = go.transform;
        PlayDrawCardAnimation(t, handCardTransforms.Count);
    }

    // 範例：從牌堆抽 1 張，僅視覺顯示。
    public void DrawOneVisual()
    {
        var ids = deckData.drawCard(handData, 1);
        if (ids != null && ids.Count > 0)
        {
            AddCardToHandById(ids[0]);
        }
    }

    // 以 id 生成卡片並標記到 prefab（只設定 ID，不覆蓋其他欄位）
    public void AddCardToHandById(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (cardPrefab == null || handContainer == null || deckSpawnPoint == null)
        {
            Debug.LogWarning("AddCardToHandById: Missing prefab or container/spawn setup.");
            return;
        }
        var go = Instantiate(cardPrefab);
        ApplyNormalizedCardSize(go);
        var view = go.GetComponent<global::CardData>();
        if (view != null)
        {
            view.SetCardId(id);
        }
        var simple = go.GetComponent<SimpleCardData>();
        if (simple != null)
        {
            simple.cardId = id;
        }
        var t = go.transform;
        PlayDrawCardAnimation(t, handCardTransforms.Count);
    }

    // 將指定 id 的牌放回牌堆（從 handData 移除，加入 deckData）
    public void backToDeck(List<string> cardIds)
    {
        if (cardIds == null || cardIds.Count == 0) return;

        var deck = deckData.LoadDeck();
        foreach (var id in cardIds)
        {
            if (handData.Hand.Contains(id))
            {
                handData.Hand.Remove(id);
                deck.Add(id);
            }
            else
            {
                Debug.LogWarning("backToDeck: Hand does not contain card ID " + id);
            }
        }
        deckData.SaveDeck(deck);
        handData.SaveHand();
    }
}