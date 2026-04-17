using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 最簡版：把這個掛在可放置的 UI 區塊（例如一個空的 Image/RawImage）
// 作用：當有 SimpleDraggable 拖曳結束放到這裡，會把卡片收為子物件並置中
// 加強版：自動確保本物件具備可被射線命中的 Graphic，並提供除錯日誌
public class SimpleDropArea : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum AreaType
    {
        None = 0,
        Attack = 1,
        Bench = 2
    }

    // 限制每個區域只容納一張卡（簡單常見規則）
    public bool oneCardPerArea = true;
    public bool debugLogs = true;

    [Header("Area Type")]
    [SerializeField] private AreaType areaType = AreaType.None;

    [Header("Bench Energy")]
    public CardEvent cardEvent; // 可選；若空，Awake 時自動場景尋找
    public GamePlay gamePlay;  // 可選；若空，Awake 時自動場景尋找
    public CardrunTime cardrunTime; // 可選；若空，Awake 時自動場景尋找

    [Header("Card Effect Trigger")]
    public bool triggerCardEffectsOnDrop = true;

    // 放下時，用顯示用 Prefab 取代原拖曳卡片
    public bool replaceDroppedWithPrefab = true;
    public GameObject displayPrefab; // 指向你的顯示卡片 Prefab（建議為 UI RectTransform）
    // 若區域已有卡片是否允許覆蓋（刪除舊卡後放新卡）
    public bool allowReplaceExisting = false;

    [Header("Mode Display")]
    public SimpleAreaModeDisplay areaModeDisplay; // 可選：放置成功後顯示預設攻擊圖
    public bool autoHideModeDisplayWhenNoCard = true; // 區域沒有卡片時自動隱藏 icon
    public bool autoShowDefaultModeDisplayWhenCardExists = false; // 偵測到有卡片時可選擇自動顯示預設 icon

    [Header("Content Root (Optional)")]
    public Transform contentRoot; // 若指定，所有放置/檢查只針對此節點的子物件，不影響其他 UI（如圖示）

    // 取得放置內容的根節點（未指定則使用自身）
    private Transform Root => contentRoot != null ? contentRoot : transform;
    private bool _hasOccupantInitialized;
    private bool _lastHasOccupant;

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

    private T ResolveFromScene<T>() where T : Component
    {
        // 先找 active 物件
        var found = FindObjectOfType<T>();
        if (found != null) return found;

        // 再找 inactive（但仍在有效場景中）的物件
        var all = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c != null && c.gameObject.scene.IsValid())
                return c;
        }

        return null;
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

        if (cardEvent == null) cardEvent = ResolveFromScene<CardEvent>();
        if (gamePlay == null) gamePlay = ResolveFromScene<GamePlay>();
        if (cardrunTime == null) cardrunTime = ResolveFromScene<CardrunTime>();

        if (debugLogs)
        {
            Debug.Log($"[SimpleDropArea] {name} 初始化 areaType={areaType}  cardEvent={(cardEvent != null ? cardEvent.name : "null")}  gamePlay={(gamePlay != null ? gamePlay.name : "null")}  cardrunTime={(cardrunTime != null ? cardrunTime.name : "null")}");
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

        RefreshModeDisplayVisibility(force: true);
    }

    void Update()
    {
        RefreshModeDisplayVisibility(force: false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (debugLogs) Debug.Log($"SimpleDropArea: OnDrop over {name}");
        var dragged = eventData.pointerDrag;
        if (dragged == null) return;
        string droppedCardId = ResolveCardId(dragged);

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
                    if (gamePlay == null) gamePlay = ResolveFromScene<GamePlay>();
                    gamePlay?.TrySendCardGameObjectToPlayerDiscard(firstOccupant.gameObject);
                    Destroy(firstOccupant.gameObject);
                }
            }
        }

        // Bench 區域：檢查每回合放置次數，並在放置當下先產生一次能量
        if (debugLogs) Debug.Log($"[SimpleDropArea] OnDrop: areaType={areaType}  IsBenchArea={IsBenchArea()}");
        if (IsBenchArea())
        {
            if (gamePlay == null) gamePlay = ResolveFromScene<GamePlay>();
            if (gamePlay == null)
            {
                Debug.LogWarning("[SimpleDropArea] Bench: 找不到 GamePlay，無法檢查每回合放置次數，已取消放置。");
                return;
            }

            if (!gamePlay.TryConsumeBenchPlacementThisTurn())
            {
                if (debugLogs) Debug.Log("[SimpleDropArea] Bench: 本回合已放置過，取消此次放置。");
                return;
            }

            // 放置當下立即產能
            TryGrantBenchEnergy(dragged);
        }

            // Attack 區域：驗証 common summon、能量數量、能量顏色
            if (IsAttackArea())
            {
                if (gamePlay == null) gamePlay = ResolveFromScene<GamePlay>();
                if (cardEvent == null) cardEvent = ResolveFromScene<CardEvent>();
                if (gamePlay == null)
                {
                    Debug.LogWarning("[SimpleDropArea] Attack: 找不到 GamePlay，無法檢查能量，已取消放置。");
                    return;
                }
                if (cardEvent == null)
                {
                    Debug.LogWarning("[SimpleDropArea] Attack: 找不到 CardEvent，無法查詢卡片資料，已取消放置。");
                    return;
                }

                // 若為需要手牌棄置的 Ace 卡，使用互動式異步流程（避免 UI 事件順序衝突）
                string aceId = ResolveCardId(dragged);
                if (!string.IsNullOrEmpty(aceId)
                    && cardEvent.TryGetCardById(aceId, out var aceCheckData)
                    && aceCheckData.IsAce
                    && !string.IsNullOrWhiteSpace(aceCheckData.Seal)
                    && aceCheckData.Seal.ToLowerInvariant().Contains("hand card"))
                {
                    // 立即鎖定拖曳（避免 OnEndDrag 把卡片還原），之後由協程接管
                    draggable.wasDroppedThisDrag = true;
                    StartCoroutine(HandleAceHandDiscardCoroutine(dragged, draggable, aceId, aceCheckData, gamePlay, cardEvent));
                    return;
                }

                if (!ValidateAttackPlacement(dragged, gamePlay, cardEvent))
                {
                    if (debugLogs) Debug.Log("[SimpleDropArea] Attack: 放置驗証失敗，已取消放置。");
                    return;
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
            if (draggable.OriginalTurHandController != null)
            {
                var handTf = draggable.OriginalTurHandController.handContainer != null
                    ? draggable.OriginalTurHandController.handContainer
                    : draggable.OriginalTurHandController.transform;
                if (dragged.transform.parent == handTf)
                {
                    dragged.transform.SetParent(null, false);
                }
                draggable.OriginalTurHandController.OnCardRemoved(dragged);
            }
            Destroy(dragged);
            var go = Instantiate(displayPrefab, Root, false);
            CopyCardIdentity(dragged, go); // 保留卡片 id，供後續 Bench 產能與規則檢查使用
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
            draggable.OriginalTurHandController?.OnCardRemoved(dragged);

            // 顯示預設攻擊/防禦圖示（預設攻擊）
            areaModeDisplay?.ShowDefault();
        }

        // 通知這次拖曳已成功放置，避免 Draggable 還原
        draggable.wasDroppedThisDrag = true;

        if (triggerCardEffectsOnDrop)
        {
            TriggerDropEffects(droppedCardId);
        }

        RefreshModeDisplayVisibility(force: true);
    }

    // ────────────────────────────────────────────────────────────────
    // Ace + 手牌棄置的非同步協程流程
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 協程：對需要手牌棄置 Seal 的 Ace 卡進行互動式棄牌，完成後再執行放置。
    /// 若任何條件不符或玩家取消，則將卡片還原至手牌。
    /// </summary>
    private IEnumerator HandleAceHandDiscardCoroutine(
        GameObject dragged, SimpleDraggable draggable,
        string cardId, Tur.CardData data,
        GamePlay gp, CardEvent ce)
    {
        string sealStr = data.Seal?.ToLowerInvariant() ?? string.Empty;

        // 1. 解析封印中手牌棄置數量（"discard one hand card" → 1, "discard 2 hand cards" → 2）
        int handDiscardCnt = 1;
        int handCardIdx = sealStr.IndexOf("hand card", System.StringComparison.Ordinal);
        if (handCardIdx > 0)
        {
            string before = sealStr.Substring(0, handCardIdx);
            int lastDiscard = before.LastIndexOf("discard", System.StringComparison.Ordinal);
            if (lastDiscard >= 0)
            {
                string between = before.Substring(lastDiscard + "discard".Length).Trim();
                var parts = between.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out int n)) handDiscardCnt = n;
                // "one", "two" 等文字 → 維持預設 1
            }
        }

        // 2. 解析封印中綠色能量棄置數量
        int greenEnergyCost = 0;
        if (sealStr.Contains("green energy"))
        {
            var tokens = sealStr.Split(new char[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (int.TryParse(tokens[i], out int num))
                {
                    // 下一個 token 包含 "green" 即為綠能數量
                    if (tokens[i + 1].Contains("green"))
                    {
                        greenEnergyCost = num;
                        break;
                    }
                }
            }
        }

        int coreCost = data.Cost;

        // 3. 預先檢查（不消耗）
        bool alreadyHasAce = !string.IsNullOrWhiteSpace(gp.PlayerAceCardId);
        bool canPayCore    = gp.CanPayCoreCount(coreCost);
        bool canPayGreenEn = greenEnergyCost == 0 || gp.CanPayEnergyByColor("green", greenEnergyCost);

        if (alreadyHasAce || !canPayCore || !canPayGreenEn)
        {
            string preCheckReason = alreadyHasAce  ? "場上已有 Ace 卡" :
                                      !canPayCore    ? $"core 不足（需 {coreCost} 張，現有 {gp.GetPlayerCoreArea().Count} 張）" :
                                                       $"綠色能量不足（需 {greenEnergyCost}）";
            if (alreadyHasAce)  Debug.LogWarning("[SimpleDropArea] Ace: 場上已有 Ace 卡，放置取消。");
            if (!canPayCore)    Debug.LogWarning($"[SimpleDropArea] Ace: core 不足（需 {coreCost} 張，現有 {gp.GetPlayerCoreArea().Count} 張），放置取消。");
            if (!canPayGreenEn) Debug.LogWarning($"[SimpleDropArea] Ace: 綠色能量不足（需 {greenEnergyCost}），放置取消。");
            ReturnCardToHand(dragged, draggable, preCheckReason);
            yield break;
        }

        // 4. 展示互動棄牌 UI 並等待玩家選擇
        bool wasDraggedActive = dragged.activeSelf;
        dragged.SetActive(false);

        var discardTask = gp.RequestInteractiveDiscardAsync(handDiscardCnt);
        yield return new WaitUntil(() => discardTask.IsCompleted);

        if (discardTask.IsFaulted)
        {
            dragged.SetActive(wasDraggedActive);
            Debug.LogWarning("[SimpleDropArea] Ace: 互動棄牌執行失敗，放置取消。");
            ReturnCardToHand(dragged, draggable, "互動棄牌執行失敗");
            yield break;
        }

        if (discardTask.Result < handDiscardCnt)
        {
            dragged.SetActive(wasDraggedActive);
            Debug.LogWarning("[SimpleDropArea] Ace: 玩家取消互動棄牌，放置取消。");
            ReturnCardToHand(dragged, draggable, "玩家取消互動棄牌");
            yield break;
        }

        // 5. 消耗封印所需綠色能量
        if (greenEnergyCost > 0 && !gp.TryConsumeEnergyByColor("green", greenEnergyCost))
        {
            dragged.SetActive(wasDraggedActive);
            Debug.LogWarning("[SimpleDropArea] Ace: 確認棄牌後發現綠色能量不足，放置取消。");
            ReturnCardToHand(dragged, draggable, $"確認棄牌後綠色能量不足（需 {greenEnergyCost}）");
            yield break;
        }

        // 6. 消耗 Core（從 playerCoreArea 移除）
        if (!gp.TryConsumeCoreCount(coreCost))
        {
            dragged.SetActive(wasDraggedActive);
            Debug.LogWarning("[SimpleDropArea] Ace: 確認棄牌後發現 core 不足，放置取消。");
            ReturnCardToHand(dragged, draggable, $"確認棄牌後 core 不足（需 {coreCost} 張）");
            yield break;
        }

        // 7. 登記 Ace
        dragged.SetActive(wasDraggedActive);
        gp.RegisterPlayerAce(cardId);
        if (debugLogs) Debug.Log($"[SimpleDropArea] Ace 互動流程成功：id={cardId}，完成放置。");

        // 8. 完成實際放置
        CompletePlacementAfterValidation(dragged, draggable, cardId);
    }

    /// <summary>
    /// 將驗証通過後的卡片放入放置區（可替換 Prefab 或直接收容）。
    /// </summary>
    private void CompletePlacementAfterValidation(GameObject dragged, SimpleDraggable draggable, string cardId)
    {
        if (dragged == null) return;

        if (replaceDroppedWithPrefab && displayPrefab != null)
        {
            if (draggable.OriginalHandController != null)
            {
                var handTf = draggable.OriginalHandController.transform;
                if (dragged.transform.parent == handTf) dragged.transform.SetParent(null, false);
                draggable.OriginalHandController.OnCardRemoved(dragged);
            }
            if (draggable.OriginalTurHandController != null)
            {
                var handTf = draggable.OriginalTurHandController.handContainer != null
                    ? draggable.OriginalTurHandController.handContainer
                    : draggable.OriginalTurHandController.transform;
                if (dragged.transform.parent == handTf) dragged.transform.SetParent(null, false);
                draggable.OriginalTurHandController.OnCardRemoved(dragged);
            }

            Destroy(dragged);

            var go = Instantiate(displayPrefab, Root, false);
            // 手動複製 id（source GO 已被 Destroy）
            var sc = go.GetComponent<SimpleCardData>() ?? go.AddComponent<SimpleCardData>();
            sc.cardId = cardId;
            var cardView = go.GetComponent<CardData>();
            if (cardView != null) cardView.SetCardId(cardId);
            var ci = go.GetComponent<CardIdentity>();
            if (ci != null) ci.Id = cardId;

            var dispRT = go.GetComponent<RectTransform>();
            if (dispRT != null) dispRT.anchoredPosition = Vector2.zero;
            else go.transform.localPosition = Vector3.zero;

            areaModeDisplay?.ShowDefault();
        }
        else
        {
            dragged.transform.SetParent(Root, false);
            var drt = dragged.GetComponent<RectTransform>();
            if (drt != null) drt.anchoredPosition = Vector2.zero;

            draggable.OriginalHandController?.OnCardRemoved(dragged);
            draggable.OriginalTurHandController?.OnCardRemoved(dragged);
            areaModeDisplay?.ShowDefault();
        }

        draggable.wasDroppedThisDrag = true;
        if (triggerCardEffectsOnDrop) TriggerDropEffects(cardId);
        RefreshModeDisplayVisibility(force: true);
    }

    /// <summary>
    /// 協程取消時，將卡片還原至原手牌容器。
    /// </summary>
    private void ReturnCardToHand(GameObject dragged, SimpleDraggable draggable, string reason = "未知原因")
    {
        if (dragged == null) return;

        Transform returnTo = null;
        if (draggable.OriginalTurHandController != null)
            returnTo = draggable.OriginalTurHandController.handContainer
                       ?? draggable.OriginalTurHandController.transform;
        else if (draggable.OriginalHandController != null)
            returnTo = draggable.OriginalHandController.transform;
        else if (draggable.StartParent != null)
            returnTo = draggable.StartParent;

        if (returnTo != null)
        {
            dragged.transform.SetParent(returnTo, false);
            var rt = dragged.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = draggable.StartAnchoredPos;
        }

        var cg = dragged.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = true;

        draggable.OriginalTurHandController?.OnCardAdded(dragged);
        draggable.OriginalHandController?.OnCardAdded(dragged);

        if (debugLogs) Debug.Log($"[SimpleDropArea] 卡片已還原至手牌（Ace 放置取消）。原因：{reason}");
    }

    // ────────────────────────────────────────────────────────────────

    private void TriggerDropEffects(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            if (debugLogs) Debug.LogWarning("[SimpleDropArea] TriggerDropEffects: cardId is empty, skip.");
            return;
        }

        if (cardrunTime == null)
            cardrunTime = ResolveFromScene<CardrunTime>();

        if (cardrunTime == null)
        {
            Debug.LogWarning($"[SimpleDropArea] TriggerDropEffects: CardrunTime not found. cardId={cardId}");
            return;
        }

        // 所有成功放置都觸發 placed。
        cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.Placed);

        // Attack 區內的成功放置視為一次 common summon 事件。
        if (IsAttackArea())
        {
            cardrunTime.TriggerCardEffect(cardId, CardEffectEvent.EventType.CommonSummon);
        }

        RefreshPlayerAttackAreaAttackValues();
    }

    private void RefreshPlayerAttackAreaAttackValues()
    {
        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsAttackArea())
                continue;

            if (!IsPlayerOwnedArea(area.transform))
                continue;

            Transform root = area.Root;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                var click = child.GetComponentInChildren<leftRightClickCard>(true);
                if (click != null)
                    click.RefreshAttackDamageFromData();
            }
        }
    }

    private static bool IsPlayerOwnedArea(Transform areaTransform)
    {
        if (areaTransform == null)
            return false;

        Transform t = areaTransform;
        while (t != null)
        {
            string n = t.name != null ? t.name.ToLowerInvariant() : string.Empty;
            if (n.Contains("ai") || n.Contains("enemy"))
                return false;
            if (n == "player" || n.Contains("player"))
                return true;
            t = t.parent;
        }

        return true;
    }

    private int CountActiveOccupants()
    {
        int count = 0;
        var r = Root;
        for (int i = 0; i < r.childCount; i++)
        {
            var c = r.GetChild(i);
            if (IsIgnoredChild(c)) continue;
            count++;
        }
        return count;
    }

    private void RefreshModeDisplayVisibility(bool force)
    {
        if (areaModeDisplay == null) return;

        bool hasOccupant = CountActiveOccupants() > 0;
        if (!force && _hasOccupantInitialized && hasOccupant == _lastHasOccupant) return;

        _hasOccupantInitialized = true;
        _lastHasOccupant = hasOccupant;

        if (!hasOccupant)
        {
            if (autoHideModeDisplayWhenNoCard)
            {
                areaModeDisplay.Hide();
                if (debugLogs) Debug.Log($"SimpleDropArea: hide mode display on {name} (no card)");
            }
            return;
        }

        if (autoShowDefaultModeDisplayWhenCardExists)
        {
            areaModeDisplay.ShowDefault();
            if (debugLogs) Debug.Log($"SimpleDropArea: show default mode display on {name} (has card)");
        }
    }

    private void CopyCardIdentity(GameObject source, GameObject target)
    {
        if (source == null || target == null) return;

        string id = ResolveCardId(source);

        if (string.IsNullOrEmpty(id)) return;

        var targetSimple = target.GetComponent<SimpleCardData>();
        if (targetSimple == null)
            targetSimple = target.AddComponent<SimpleCardData>();
        targetSimple.cardId = id;

        var targetCard = target.GetComponent<CardData>();
        if (targetCard != null)
            targetCard.SetCardId(id);

        var targetIdentity = target.GetComponent<CardIdentity>();
        if (targetIdentity != null)
            targetIdentity.Id = id;
    }

    private string ResolveCardId(GameObject cardGO)
    {
        if (cardGO == null) return null;

        var simpleData = cardGO.GetComponent<SimpleCardData>();
        if (simpleData != null && !string.IsNullOrWhiteSpace(simpleData.cardId))
            return simpleData.cardId.Trim();

        var cardData = cardGO.GetComponent<CardData>();
        if (cardData != null && !string.IsNullOrWhiteSpace(cardData.id))
            return cardData.id.Trim();

        var identity = cardGO.GetComponent<CardIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.Id))
            return identity.Id.Trim();

        // 後備：物件名稱中常見 "01(Clone)" 或 "Card_01"，取其中數字片段
        var rawName = cardGO.name.Replace("(Clone)", "").Trim();
        var parts = rawName.Split(' ', '_', '-', '#');
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            bool allDigits = p.Length > 0;
            for (int j = 0; j < p.Length; j++)
            {
                if (!char.IsDigit(p[j]))
                {
                    allDigits = false;
                    break;
                }
            }

            if (allDigits)
            {
                // 轉成兩位數格式，與目前卡庫 id（如 01）對齊
                if (int.TryParse(p, out int n))
                    return n.ToString("D2");
            }
        }

        return null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (debugLogs) Debug.Log($"SimpleDropArea: pointer enter {name}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (debugLogs) Debug.Log($"SimpleDropArea: pointer exit {name}");
    }

    // Bench 放牌：從卡片取 id → 查詢 en_spawn/color → 寫入 GamePlay energy
    private void TryGrantBenchEnergy(GameObject cardGO)
    {
        if (debugLogs)
            Debug.Log($"[SimpleDropArea] Enter TryGrantBenchEnergy: cardGO={(cardGO != null ? cardGO.name : "null")}");

        try
        {
            // 執行當下再次嘗試綁定，避免 Awake 時序或場景切換造成引用遺失
            if (cardEvent == null) cardEvent = ResolveFromScene<CardEvent>();
            if (gamePlay == null) gamePlay = ResolveFromScene<GamePlay>();
            if (debugLogs)
                Debug.Log($"[SimpleDropArea] Bench refs: cardEvent={(cardEvent != null ? cardEvent.name : "null")}, gamePlay={(gamePlay != null ? gamePlay.name : "null")}");

            if (cardEvent == null)
            {
                Debug.LogWarning("[SimpleDropArea] TryGrantBenchEnergy: cardEvent is null。請在場景任一啟用中的 GameObject 掛上 CardEvent（檔案在 Assets/Script/tur/CardEvent.cs）。");
                return;
            }
            if (gamePlay == null)
            {
                Debug.LogWarning($"[SimpleDropArea] TryGrantBenchEnergy: gamePlay is null，請確認場景中有掛載 GamePlay 的物件。");
                return;
            }
            if (cardGO == null)
            {
                if (debugLogs) Debug.LogWarning("[SimpleDropArea] TryGrantBenchEnergy: cardGO is null。");
                return;
            }

            string id = ResolveCardId(cardGO);

            if (debugLogs)
                Debug.Log($"[SimpleDropArea] Bench card id resolve: card={cardGO.name} final={id}");

            if (string.IsNullOrEmpty(id))
            {
                if (debugLogs) Debug.LogWarning($"[SimpleDropArea] Bench: 無法取得卡片 id，能量未增加。cardGO={cardGO.name}");
                return;
            }
            bool cardFound = cardEvent.TryGetCardById(id, out var data);
            Debug.Log($"[SimpleDropArea] TryGetCardById({id}) → found={cardFound}  gamePlay#{gamePlay.GetInstanceID()}");
            if (!cardFound)
            {
                Debug.LogError($"[SimpleDropArea] Bench: 找不到 id={id} 的卡片資料，能量未增加。請確認 card.json 中存在此 id。");
                return;
            }

            var color = string.IsNullOrEmpty(data.Color) ? "colorless" : data.Color;
            var before = gamePlay.Energy != null
                ? new System.Collections.Generic.List<string>(gamePlay.Energy)
                : new System.Collections.Generic.List<string>();

            gamePlay.AddPlayerEnergy(color, data.EnSpawn);

            Debug.Log($"[SimpleDropArea] Bench 放牌 OK：id={id}  color={color}  en_spawn={data.EnSpawn}  after=[{string.Join(", ", gamePlay.Energy)}]");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SimpleDropArea] TryGrantBenchEnergy 例外：{ex}");
        }
    }

    // 由 GamePlay 在玩家回合開始時呼叫：依本 Bench 區內每張卡片的 en_spawn 產生能量
    public int GrantBenchEnergyFromPlacedCardsAtTurnStart()
    {
        if (!IsBenchArea()) return 0;

        if (cardEvent == null) cardEvent = ResolveFromScene<CardEvent>();
        if (gamePlay == null) gamePlay = ResolveFromScene<GamePlay>();
        if (cardEvent == null || gamePlay == null) return 0;

        int totalGenerated = 0;
        var r = Root;
        Debug.Log($"[SimpleDropArea] TurnStart scan: area={name}  Root.childCount={r.childCount}");
        for (int i = 0; i < r.childCount; i++)
        {
            var child = r.GetChild(i);
            bool ignored = IsIgnoredChild(child);
            string id = ResolveCardId(child.gameObject);
            Debug.Log($"[SimpleDropArea] TurnStart child[{i}]: name={child.name}  ignored={ignored}  id={id}");
            if (ignored) continue;

            if (string.IsNullOrEmpty(id)) continue;
            if (!cardEvent.TryGetCardById(id, out var data))
            {
                Debug.LogError($"[SimpleDropArea] TurnStart Bench: 查無卡片資料 id={id} area={name}，請確認 card.json。");
                continue;
            }

            var color = string.IsNullOrEmpty(data.Color) ? "colorless" : data.Color;
            if (data.EnSpawn <= 0)
            {
                if (debugLogs)
                    Debug.Log($"[SimpleDropArea] TurnStart Bench: id={id} en_spawn<=0，略過");
                continue;
            }

            gamePlay.AddPlayerEnergy(color, data.EnSpawn);
            totalGenerated += data.EnSpawn;

            if (debugLogs)
                Debug.Log($"[SimpleDropArea] TurnStart Bench 產能：area={name} id={id} color={color} en_spawn={data.EnSpawn}");
        }

        return totalGenerated;
    }

        // Attack 放牌驗証：檢查 ace / common summon 限制、能量/core 數量、能量/core 顏色，並消耗
        private bool ValidateAttackPlacement(GameObject cardGO, GamePlay gamePlay, CardEvent cardEvent)
        {
            if (cardGO == null || gamePlay == null || cardEvent == null) return false;

            try
            {
                // 1. 取得卡片 id
                string id = ResolveCardId(cardGO);

                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[SimpleDropArea] Attack: 無法取得卡片 id，放置取消。");
                    return false;
                }

                // 2. 查詢卡片資料
                if (!cardEvent.TryGetCardById(id, out var data))
                {
                    Debug.LogWarning($"[SimpleDropArea] Attack: 找不到 id={id} 的卡片資料，放置取消。");
                    return false;
                }

                // 3. 檢查 ace 卡限制
                if (data.IsAce)
                {
                    if (!string.IsNullOrWhiteSpace(gamePlay.PlayerAceCardId))
                    {
                        Debug.LogWarning($"[SimpleDropArea] Attack: Ace 卡本場合只能存在一張，放置取消。");
                        return false;
                    }

                    if (!ValidateAceSeal(id, data, gamePlay, cardEvent))
                    {
                        Debug.LogWarning($"[SimpleDropArea] Attack: Ace 卡 seal 條件不符，放置取消。");
                        return false;
                    }

                    int coreCost = data.Cost;

                    if (debugLogs)
                        Debug.Log($"[SimpleDropArea] Ace validate: id={id}  coreCost={coreCost}  coreAreaCount={gamePlay.GetPlayerCoreArea().Count}");

                    if (!gamePlay.TryConsumeCoreCount(coreCost))
                    {
                        Debug.LogWarning($"[SimpleDropArea] Attack: Ace 卡 core 不足，放置取消。需要 {coreCost} 張 core。");
                        return false;
                    }

                    gamePlay.RegisterPlayerAce(id);

                    if (debugLogs)
                        Debug.Log($"[SimpleDropArea] Ace 放牌成功：id={id}  消耗 {coreCost} 張 core");

                    return true;
                }

                // 4. 檢查普通卡：common summon 限制
                if (data.Type == "common")
                {
                    if (!gamePlay.TryConsumeCommonSummonThisTurn())
                    {
                        Debug.LogWarning($"[SimpleDropArea] Attack: common summon 本回合已用過，放置取消。");
                        return false;
                    }
                }

                // 5. 檢查能量條件
                int costAmount = data.Cost;
                var requiredColor = string.IsNullOrEmpty(data.Color) ? "colorless" : data.Color;

                if (debugLogs)
                    Debug.Log($"[SimpleDropArea] Attack validation: id={id}  type={data.Type}  cost={costAmount}  color={requiredColor}");

                // 6. 驗証能量並消耗
                if (!gamePlay.TryConsumeEnergyByColor(requiredColor, costAmount))
                {
                    Debug.LogWarning($"[SimpleDropArea] Attack: 能量不足或顏色不符，放置取消。需要 {costAmount}x '{requiredColor}'。");
                    return false;
                }

                if (debugLogs)
                    Debug.Log($"[SimpleDropArea] Attack 放牌成功：id={id}  消耗 {costAmount}x '{requiredColor}' 能量");

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SimpleDropArea] ValidateAttackPlacement 例外：{ex}");
                return false;
            }
        }

        private bool ValidateAceSeal(string aceCardId, Tur.CardData aceData, GamePlay gamePlay, CardEvent cardEvent)
        {
            if (string.IsNullOrWhiteSpace(aceData.Seal))
                return true;

            string sealStr = aceData.Seal.ToLowerInvariant();

            if (sealStr.Contains("discard") && sealStr.Contains("green energy"))
            {
                int cnt = 0;
                var parts = sealStr.Split(' ');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out int num))
                    {
                        cnt = num;
                        break;
                    }
                }

                if (cnt > 0)
                {
                    if (!gamePlay.TryConsumeEnergyByColor("green", cnt))
                    {
                        Debug.LogWarning($"[SimpleDropArea] Ace seal: need {cnt}x green energy, insufficient.");
                        return false;
                    }
                }
            }

            if (sealStr.Contains("discard") && sealStr.Contains("hand card"))
            {
                int discardCnt = 1;
                // Only extract the number from the "discard ... hand card" segment,
                // not from earlier conditions like "discard 3 green energy".
                int handCardIdx = sealStr.IndexOf("hand card", System.StringComparison.Ordinal);
                if (handCardIdx > 0)
                {
                    string beforeHandCard = sealStr.Substring(0, handCardIdx);
                    int lastDiscardIdx = beforeHandCard.LastIndexOf("discard", System.StringComparison.Ordinal);
                    if (lastDiscardIdx >= 0)
                    {
                        string between = beforeHandCard.Substring(lastDiscardIdx + "discard".Length).Trim();
                        var parts = between.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[0], out int num))
                            discardCnt = num;
                        // If not a number (e.g. "one"), keep default of 1
                    }
                }

                int discarded = gamePlay.DiscardCardsFromPlayerHand(discardCnt, true);
                if (discarded < discardCnt)
                {
                    Debug.LogWarning($"[SimpleDropArea] Ace seal: need discard {discardCnt} hand card(s), only discarded {discarded}.");
                    return false;
                }
            }

            return true;
        }
    // 供其他腳本讀取目前區域類型
    public AreaType GetAreaType()
    {
        return areaType;
    }

    // 指定此區域為 Attack
    public void SetAsAttackArea()
    {
        areaType = AreaType.Attack;
    }

    // 指定此區域為 Bench
    public void SetAsBenchArea()
    {
        areaType = AreaType.Bench;
    }

    // 方便其他腳本直接判斷
    private bool NameContainsInHierarchy(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        var key = keyword.ToLowerInvariant();

        Transform t = transform;
        while (t != null)
        {
            if (!string.IsNullOrEmpty(t.name) && t.name.ToLowerInvariant().Contains(key))
                return true;
            t = t.parent;
        }

        return false;
    }

    public bool IsAttackArea()
    {
        if (areaType == AreaType.Attack) return true;
        if (areaType != AreaType.None) return false;
        return NameContainsInHierarchy("attack");
    }

    public bool IsBenchArea()
    {
        if (areaType == AreaType.Bench) return true;
        if (areaType != AreaType.None) return false;
        return NameContainsInHierarchy("bench");
    }
}

