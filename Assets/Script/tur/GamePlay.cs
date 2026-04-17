using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using static DeckData;
using static HandData;
using random = UnityEngine.Random;
using UnityEngine.UI;
using System.Threading.Tasks;


public class GamePlay : MonoBehaviour
{
    // Core 區更新事件系統
    public delegate void CoreAreaUpdatedDelegate(List<string> coreCardIds);
    public event CoreAreaUpdatedDelegate OnPlayerCoreAreaUpdated;
    public event CoreAreaUpdatedDelegate OnAICoreAreaUpdated;

    // 棄牌區更新事件系統
    public delegate void DiscardPileUpdatedDelegate(List<string> discardCardIds);
    public event DiscardPileUpdatedDelegate OnPlayerDiscardPileUpdated;
    public event DiscardPileUpdatedDelegate OnAIDiscardPileUpdated;

    // Energy 更新事件系統
    public delegate void EnergyUpdatedDelegate(IReadOnlyList<string> energyList);
    public event EnergyUpdatedDelegate OnPlayerEnergyUpdated;

    // HP 更新事件系統
    public delegate void HPUpdatedDelegate(int playerHP, int aiHP);
    public event HPUpdatedDelegate OnHPUpdated;

    [SerializeField] private Button playWithAIButton;
    [SerializeField] private GameObject nowscene;
    [SerializeField] private GameObject playscene;
    [SerializeField] private bool autoSetupOnEnterGame = true;
    [SerializeField] private bool autoPlayWithAI = true;
    [SerializeField] private TMPro.TextMeshProUGUI YourTurnText;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private RawImage reshuffleUI; // 下一階段按鈕（可選，若有則綁定結束回合功能）
    [SerializeField] private Button reshuffleButton; // 重新洗牌按鈕
    [SerializeField] private Button noreshuffleButton; // 不重新洗牌按鈕

    private bool isplaywithAI = false;
    private bool isplayerturn = true;
    private bool gamestart = false;
    private bool setupCompleted = false;
    private bool turnInProgress = false;
    private bool playerBenchPlacedThisTurn = false;
    private bool playerWentFirst = false;
    private int playerTurnCount = 0;

        private bool playerCommonSummonUsedThisTurn = false; // 每回合 common summon 限制
    Button nextStageButton;

    private DeckData deckData;
    private DeckData aiDeck;
    private HandData handData;
    private HandData aiHand;

    [SerializeField] private Handcontroller handController;
    [SerializeField] private Handcontroller aiHandController;
    [SerializeField] private DiscardSelectUI discardSelectUI;

    private List<string> playerCoreArea;
    private List<string> aiCoreArea;
    private List<string> playerDiscardPile;
    private List<string> aiDiscardPile;
    private List<string> energy;
    private List<string> AIenergy;
    private List<string> playerCoreResources;
    private string playerAceCardId;
    private readonly Dictionary<string, int> playerAttackBuffByCardId = new Dictionary<string, int>();
    private CardrunTime cardrunTime;

    private int playerHP = 20;
    private int aiHP = 20;

    // 供其他腳本讀取能量區資料
    public IReadOnlyList<string> Energy => energy;
    public IReadOnlyList<string> AIEnergy => AIenergy;
    public IReadOnlyList<string> CoreResources => playerCoreResources;
    public string PlayerAceCardId => playerAceCardId;
    public int PlayerHP => playerHP;
    public int AIHP => aiHP;

    void Awake()
    {
        AutoWireReferences();
    }

    void Start()
    {
        if (autoSetupOnEnterGame && playscene != null && playscene.activeInHierarchy)
        {
            StartGame(autoPlayWithAI);
        }
    }

    void OnEnable()
    {
        if (playWithAIButton == null)
        {
            Debug.LogError("[GamePlay] playWithAIButton is not assigned. Click callback cannot be registered.", this);
            return;
        }

        playWithAIButton.onClick.RemoveListener(OnPlayWithAIButtonClicked);
        playWithAIButton.onClick.AddListener(OnPlayWithAIButtonClicked);
        Debug.Log("[GamePlay] OnClick listener registered.", this);
    }

    void OnDisable()
    {
        if (playWithAIButton != null)
        {
            playWithAIButton.onClick.RemoveListener(OnPlayWithAIButtonClicked);
        }
    }

    private void OnPlayWithAIButtonClicked()
    {
        Debug.Log("[GamePlay] playWithAIButton clicked.", this);

        // 再嘗試一次補全空引用（ButtonClick 時物件可能才剛被創建）
        if (nowscene == null) nowscene = FindObjectByName("MainMenu");
        if (playscene == null) playscene = FindObjectByName("MatchRoomUI") ?? FindObjectByName("PlayScene");

        if (nowscene == null || playscene == null)
        {
            Debug.LogError("[GamePlay] nowscene or playscene is not assigned.", this);
            return;
        }

        nowscene.SetActive(false);
        playscene.SetActive(true);
        StartGame(true);
    }

    // Public hook for UnityEvent (Inspector OnClick) wiring.
    public void OnPlayWithAIButtonClickedFromUI()
    {
        OnPlayWithAIButtonClicked();
    }

    private void AutoWireReferences()
    {
        if (playWithAIButton == null)
        {
            playWithAIButton = FindButtonByName("PlayAIButton") ?? FindButtonByName("Playbutton");
            if (playWithAIButton == null)
            {
                playWithAIButton = GetComponentInChildren<Button>(true);
            }
        }

        if (nowscene == null)
        {
            nowscene = FindObjectByName("MainMenu");
        }

        if (playscene == null)
        {
            playscene = FindObjectByName("MatchRoomUI") ?? FindObjectByName("PlayScene");
        }

        // 自動尋找玩家的 Handcontroller（若 Inspector 未手動指定）
        if (handController == null)
        {
            handController = FindObjectOfType<Handcontroller>(true);
        }

        if (cardrunTime == null)
        {
            cardrunTime = FindObjectOfType<CardrunTime>(true);
        }

        if (discardSelectUI == null)
            discardSelectUI = FindObjectOfType<DiscardSelectUI>(true);

        // 自動綁定結束回合按鈕
        if (nextStageButton == null)
        {
            nextStageButton = FindButtonByName("NextStageButton") ?? FindButtonByName("EndTurnButton");
            if (nextStageButton != null)
            {
                nextStageButton.onClick.RemoveListener(EndPlayerTurn);
                nextStageButton.onClick.AddListener(EndPlayerTurn);
            }
        }
    }

    private Button FindButtonByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        var obj = GameObject.Find(objectName);
        if (obj == null) return null;
        return obj.GetComponent<Button>();
    }

    private GameObject FindObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        // GameObject.Find 只能找到 active 物件，所以先嘗試
        var found = GameObject.Find(objectName);
        if (found != null) return found;
        // 若找不到（例如物件目前 inactive），改用全場景搜尋
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene.IsValid() && go.name == objectName)
                return go;
        }
        return null;
    }

    private void StartGame(bool withAI)
    {
        isplaywithAI = withAI;
        if (setupCompleted)
        {
            Debug.Log("[GamePlay] Setup already completed, skip re-initialize.", this);
            return;
        }

#pragma warning disable CS4014 // Because this call is not awaited, execution will continue before the call completes
        InitializeGameState();
#pragma warning restore CS4014

        setupCompleted = true;
    }

    // 建立本局使用的 runtime 牌堆：若張數不足則以 01 補滿（測試用）
    // 注意：不會回寫 deck.json，避免污染原始牌組配置。
    private List<string> BuildRuntimeDeckWithPadding(DeckData sourceDeck, int minCards = 20)
    {
        var current = sourceDeck.LoadDeck() ?? new List<string>();
        var runtimeDeck = new List<string>(current);

        if (runtimeDeck.Count >= minCards)
            return runtimeDeck;

        int need = minCards - runtimeDeck.Count;
        for (int i = 0; i < need; i++)
            runtimeDeck.Add("01");

        Debug.Log($"[GamePlay] Runtime deck padded with {need}x '01' for testing (source count={current.Count}, target={minCards}).", this);
        return runtimeDeck;
    }

    private async Task InitializeGameState()
    {
        playerCoreArea = new List<string>();
        aiCoreArea = new List<string>();
        playerDiscardPile = new List<string>();
        aiDiscardPile = new List<string>();
        playerCoreResources = new List<string>();
        playerAceCardId = null;
        energy = new List<string>();
        AIenergy = new List<string>();
        playerBenchPlacedThisTurn = false;
        playerHP = 20;
        aiHP = 20;
        OnHPUpdated?.Invoke(playerHP, aiHP);

        // 以 deck.json 作為「牌組清單」，每局複製到 runtime_deck.json 當作戰鬥中的可變牌堆
        // 若牌組不足 minCards，僅在 runtime 牌堆補 01（不回寫 deck.json）
        var sourceDeckData = new DeckData(); // 預設 path: Assets/json/deck.json
        var sourceCards = sourceDeckData.LoadDeck();
        var runtimeSourceCards = BuildRuntimeDeckWithPadding(sourceDeckData, 20);

        deckData = new DeckData();
        deckData.SetPath("Assets/json/runtime_deck.json");
        deckData.SaveDeck(new List<string>(runtimeSourceCards));
        deckData.ShuffleDeck();
        handData = new HandData();

        if (isplaywithAI)
        {
            // AI 也使用獨立的 runtime 牌堆
            aiDeck = new DeckData();
            aiDeck.SetPath("Assets/json/ai_deck.json");
            aiDeck.SaveDeck(new List<string>(runtimeSourceCards));
            aiDeck.ShuffleDeck();                                   // AI 牌堆獨立洗牌
            aiHand = new HandData();
            aiHand.path = "Assets/json/ai_hand.json";              // AI 手牌用獨立路徑
            aiDeck.drawCard(aiHand, 7);                             // AI 初始抽 7 張（資料層）
        }

        // 玩家初始抽 7 張，資料層 + 視覺層同步
        var initialDraw = deckData.drawCard(handData, 7);
        if (handController != null)
        {
            foreach (var id in initialDraw)
                handController.AddCardToHandById(id);
        }

        await Task.Delay(500); 

        // 利用 TaskCompletionSource 確保玩家完成重洗選擇後再決定先後手
        var reshuffleCompleted = new System.Threading.Tasks.TaskCompletionSource<bool>();

        //玩家可以選擇一次重新洗牌再抽7張
        reshuffleUI.gameObject.SetActive(true);

        reshuffleButton.onClick.AddListener(async () =>
        {
            // 將目前所有手牌放回牌堆，洗牌後再抽 7 張
            handData.LoadHand();
            var cardsInHand = new List<string>(handData.Hand);

            var returnedDeck = deckData.LoadDeck();
            returnedDeck.AddRange(cardsInHand);
            deckData.SaveDeck(returnedDeck);

            // 清空玩家手牌（資料層 + 視覺層）
            handData.ClearHand();
            if (handController != null)
            {
                if (handController.handContainer != null)
                {
                    for (int i = handController.handContainer.childCount - 1; i >= 0; i--)
                    {
                        Destroy(handController.handContainer.GetChild(i).gameObject);
                    }
                }
                handController.handCardTransforms.Clear();
                handController.RefreshUIHandRecord();
            }

            deckData.ShuffleDeck();
            var newDraw = deckData.drawCard(handData, 7);
            if (handController != null)
            {
                foreach (var id in newDraw)
                    handController.AddCardToHandById(id);
            }
            reshuffleUI.gameObject.SetActive(false);
            reshuffleCompleted.SetResult(true);

            await Task.Delay(500); // 等待洗牌和抽牌動畫完成（如果有的話）
        });

        noreshuffleButton.onClick.AddListener(() =>
        {
            reshuffleUI.gameObject.SetActive(false);
            reshuffleCompleted.SetResult(false);
        });

        // 等待玩家完成重洗或不重洗的選擇
        await reshuffleCompleted.Task;

        // 利用擲骰子決定先後手
        int diceRoll = Random.Range(1, 7); // 1-6
        isplayerturn = (diceRoll % 2 == 1); // 單數玩家先手，雙數 AI 先手
        playerWentFirst = isplayerturn;
        playerTurnCount = 0;
        turnInProgress = false;
        gamestart = true;

        Debug.Log($"[GamePlay] Turn order decided: playerWentFirst={playerWentFirst}", this);
    }
    

    // Update is called once per frame
    void Update()
    {
        // turnInProgress 防止每幀重複觸發；等待 EndPlayerTurn() 或 EndAITurn() 重置
        if (!gamestart || turnInProgress) return;
        if (!isplaywithAI) return;

        if (isplayerturn)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution will continue before the call completes
            StartPlayerTurn();
#pragma warning restore CS4014
        }
        else
            StartAITurn();
    }

    // 玩家回合開始：抽牌（資料 + 視覺）+ 頂牌進核心區，然後等待玩家輸入
    private async Task StartPlayerTurn()
    {
        playerTurnCount++;
        turnInProgress = true;
        playerBenchPlacedThisTurn = false;
        playerCommonSummonUsedThisTurn = false; // 重置每回合 common summon 使用
        ClearPlayerAttackBuffs();

        int generated = GenerateBenchEnergyAtTurnStart();
        int triggerCount = TriggerTurnStartEffectsForPlayerBoard();

        Debug.Log("[GamePlay] Player turn start: bench & common summon placement reset.", this);
        Debug.Log($"[GamePlay] TurnStart effects triggered on board cards: {triggerCount}", this);
        Debug.Log($"[GamePlay] Player turn index={playerTurnCount}, playerWentFirst={playerWentFirst}", this);
        Debug.Log($"[GamePlay] Player turn start energy: generated={generated}, current=[{string.Join(", ", energy ?? new List<string>())}]", this);
        if (YourTurnText != null)
        {
            YourTurnText.gameObject.SetActive(true);
            StartCoroutine(HideYourTurnTextAfterDelay(1f)); // 1秒後隱藏提示
        }

        await Task.Delay(1000); // 等待提示顯示一段時間（可調整）

        // 抽牌階段：從牌堆抽 1 張，同步更新資料層與視覺層
        DrawCardsForPlayer(1, true);

        // 頂牌進入核心區域
        string top = deckData.topCard();
        if (top != null)
        {
            playerCoreArea.Add(top);
            OnPlayerCoreAreaUpdated?.Invoke(playerCoreArea);
        }
        Debug.Log($"core: {string.Join(", ", playerCoreArea)}", this);

        // 主要階段：等待玩家出牌，由 EndPlayerTurn() 結束回合
        endTurnButton.interactable = true; // 確保結束回合按鈕可用
        endTurnButton.onClick.AddListener(EndPlayerTurn);
    }

    // 供卡片效果系統或遊戲流程共用的玩家抽牌 API。
    public List<string> DrawCardsForPlayer(int count, bool updateHandView = true)
    {
        var result = new List<string>();

        if (count <= 0)
            return result;

        if (deckData == null || handData == null)
        {
            Debug.LogWarning("[GamePlay] DrawCardsForPlayer failed: deckData or handData is null", this);
            return result;
        }

        var drawn = deckData.drawCard(handData, count);
        if (drawn == null || drawn.Count == 0)
            return result;

        result.AddRange(drawn);

        if (updateHandView && handController != null)
        {
            foreach (var id in drawn)
                handController.AddCardToHandById(id);
        }

        Debug.Log($"[GamePlay] DrawCardsForPlayer: requested={count}, drawn={result.Count}", this);
        return result;
    }

    private int GenerateBenchEnergyAtTurnStart()
    {
        int totalGenerated = 0;
        int activeBenchAreas = 0;
        int totalDropAreas = 0;

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid()) continue;
            totalDropAreas++;

            bool isBench = area.IsBenchArea();
            Debug.Log($"[GamePlay] TurnStart area check: name='{area.name}', type='{area.GetAreaType()}', isBench={isBench}", this);

            if (!isBench) continue;

            activeBenchAreas++;
            int generatedFromArea = area.GrantBenchEnergyFromPlacedCardsAtTurnStart();
            totalGenerated += generatedFromArea;
            Debug.Log($"[GamePlay] TurnStart Bench area '{area.name}' generated={generatedFromArea}", this);
        }

        Debug.Log($"[GamePlay] TurnStart Bench scan: dropAreas={totalDropAreas}, benchAreas={activeBenchAreas}, generated={totalGenerated}", this);
        return totalGenerated;
    }

    // 供其他腳本加入玩家能量（例如放置 Bench 卡後）
    public void AddPlayerEnergy(string color, int count)
    {
        if (string.IsNullOrEmpty(color) || count <= 0) return;
        if (energy == null)
        {
            energy = new List<string>();
            Debug.LogWarning("[GamePlay] energy 尚未初始化，已自動建立新清單。", this);
        }
        for (int i = 0; i < count; i++)
            energy.Add(color);
        Debug.Log($"[GamePlay] PlayerEnergy +{count} {color}: [{string.Join(", ", energy)}]", this);
        OnPlayerEnergyUpdated?.Invoke(energy);
    }

    public void AddPlayerAttackBuff(string cardId, int amount)
    {
        if (string.IsNullOrWhiteSpace(cardId) || amount == 0)
            return;

        string key = cardId.Trim();
        if (!playerAttackBuffByCardId.ContainsKey(key))
            playerAttackBuffByCardId[key] = 0;

        playerAttackBuffByCardId[key] += amount;
        Debug.Log($"[GamePlay] Attack buff updated: id={key}, delta={amount}, total={playerAttackBuffByCardId[key]}", this);
    }

    public int GetPlayerAttackWithBuff(string cardId, int baseAttack)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return Mathf.Max(0, baseAttack);

        int bonus = 0;
        playerAttackBuffByCardId.TryGetValue(cardId.Trim(), out bonus);
        bonus += CalculatePersistentWhenAttackAreaAuraBonus(cardId.Trim());
        return Mathf.Max(0, baseAttack + bonus);
    }

    private int CalculatePersistentWhenAttackAreaAuraBonus(string targetCardId)
    {
        if (string.IsNullOrWhiteSpace(targetCardId))
            return 0;

        var playerAttackCards = GetPlayerAttackAreaCardIds();
        if (playerAttackCards.Count == 0)
            return 0;

        bool targetInAttackArea = false;
        for (int i = 0; i < playerAttackCards.Count; i++)
        {
            if (string.Equals(playerAttackCards[i], targetCardId, System.StringComparison.OrdinalIgnoreCase))
            {
                targetInAttackArea = true;
                break;
            }
        }

        if (!targetInAttackArea)
            return 0;

        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null)
            return 0;

        int totalAuraBonus = 0;
        int xValue = playerAttackCards.Count; // 依需求：X = 我方攻擊區卡牌數量

        for (int i = 0; i < playerAttackCards.Count; i++)
        {
            string sourceId = playerAttackCards[i];
            if (!cardEvent.TryGetCardById(sourceId, out var sourceData) || sourceData == null)
                continue;

            string ez = sourceData.EZcode;
            if (!IsPersistentWhenAttackAuraEZcode(ez))
                continue;

            totalAuraBonus += xValue;
        }

        return totalAuraBonus;
    }

    private List<string> GetPlayerAttackAreaCardIds()
    {
        var result = new List<string>();

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsAttackArea())
                continue;

            if (!IsPlayerOwnedArea(area.transform))
                continue;

            Transform root = area.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                string id = ResolveCardIdForEffectTrigger(child.gameObject);
                if (!string.IsNullOrWhiteSpace(id))
                    result.Add(id.Trim());
            }
        }

        return result;
    }

    private static bool IsPlayerOwnedArea(Transform areaTransform)
    {
        if (areaTransform == null)
            return false;

        Transform t = areaTransform;
        while (t != null)
        {
            string name = t.name != null ? t.name.ToLowerInvariant() : string.Empty;
            if (name.Contains("ai") || name.Contains("enemy"))
                return false;
            if (name == "player" || name.Contains("player"))
                return true;
            t = t.parent;
        }

        return true;
    }

    private static bool IsPersistentWhenAttackAuraEZcode(string ezcode)
    {
        if (string.IsNullOrWhiteSpace(ezcode))
            return false;

        string s = ezcode.ToLowerInvariant();
        bool hasWhen = s.Contains("when");
        bool hasAttackArea = s.Contains("attack area");
        bool hasAllYourCards = s.Contains("all of your card") || s.Contains("all your card") || s.Contains("all your cards");
        bool hasAtkPlusX = s.Contains("atk + x") || s.Contains("atk+x") || s.Contains("atk +x");

        return hasWhen && hasAttackArea && hasAllYourCards && hasAtkPlusX;
    }

    public void ClearPlayerAttackBuffs()
    {
        playerAttackBuffByCardId.Clear();
    }

    public void AddPlayerCore(string coreColor, int count)
    {
        if (string.IsNullOrEmpty(coreColor) || count <= 0) return;
        if (playerCoreResources == null)
            playerCoreResources = new List<string>();

        for (int i = 0; i < count; i++)
            playerCoreResources.Add(coreColor);

        Debug.Log($"[GamePlay] PlayerCore +{count} {coreColor}: [{string.Join(", ", playerCoreResources)}]", this);
    }

    public bool TryConsumeCoreByColor(string requiredColor, int costAmount)
    {
        if (playerCoreResources == null || playerCoreResources.Count < costAmount)
        {
            Debug.LogWarning($"[GamePlay] Core insufficient: required {costAmount}, available {(playerCoreResources != null ? playerCoreResources.Count : 0)}", this);
            return false;
        }

        int colorCount = 0;
        foreach (var c in playerCoreResources)
        {
            if (c == requiredColor) colorCount++;
        }

        if (colorCount < costAmount)
        {
            Debug.LogWarning($"[GamePlay] Core color mismatch: need {costAmount}x '{requiredColor}', have {colorCount}", this);
            return false;
        }

        int consumed = 0;
        for (int i = playerCoreResources.Count - 1; i >= 0 && consumed < costAmount; i--)
        {
            if (playerCoreResources[i] == requiredColor)
            {
                playerCoreResources.RemoveAt(i);
                consumed++;
            }
        }

        Debug.Log($"[GamePlay] Consumed {consumed}x '{requiredColor}' core. Remaining: [{string.Join(", ", playerCoreResources)}]", this);
        return true;
    }

    public void RegisterPlayerAce(string aceCardId)
    {
        playerAceCardId = aceCardId;
        Debug.Log($"[GamePlay] Registered player ace: {aceCardId}", this);
    }

    public void ClearPlayerAce()
    {
        playerAceCardId = null;
        Debug.Log("[GamePlay] Player ace cleared.", this);
    }

    public void AddPlayerHP(int amount)
    {
        if (amount <= 0) return;
        playerHP += amount;
        Debug.Log($"[GamePlay] Player HP +{amount}: {playerHP}", this);
        OnHPUpdated?.Invoke(playerHP, aiHP);
    }

    public void ReducePlayerHP(int amount)
    {
        if (amount <= 0) return;
        playerHP = Mathf.Max(0, playerHP - amount);
        Debug.Log($"[GamePlay] Player HP -{amount}: {playerHP}", this);
        OnHPUpdated?.Invoke(playerHP, aiHP);
    }

    public void ReduceAIHP(int amount)
    {
        if (amount <= 0) return;
        aiHP = Mathf.Max(0, aiHP - amount);
        Debug.Log($"[GamePlay] AI HP -{amount}: {aiHP}", this);
        OnHPUpdated?.Invoke(playerHP, aiHP);
    }

    public void SetPlayerHP(int newHP)
    {
        playerHP = Mathf.Max(0, newHP);
        OnHPUpdated?.Invoke(playerHP, aiHP);
    }

    public void SetAIHP(int newHP)
    {
        aiHP = Mathf.Max(0, newHP);
        OnHPUpdated?.Invoke(playerHP, aiHP);
    }

    private int TriggerTurnStartEffectsForPlayerBoard()
    {
        if (cardrunTime == null)
            cardrunTime = FindObjectOfType<CardrunTime>(true);

        if (cardrunTime == null)
            return 0;

        int triggeredCount = 0;

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsAttackArea() && !area.IsBenchArea())
                continue;

            Transform root = area.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                string id = ResolveCardIdForEffectTrigger(child.gameObject);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                cardrunTime.TriggerCardEffect(id, CardEffectEvent.EventType.TurnStart);
                triggeredCount++;
            }
        }

        return triggeredCount;
    }

    private string ResolveCardIdForEffectTrigger(GameObject cardGO)
    {
        if (cardGO == null) return null;

        var simpleData = cardGO.GetComponent<SimpleCardData>();
        if (simpleData != null && !string.IsNullOrWhiteSpace(simpleData.cardId))
            return simpleData.cardId.Trim();

        var viewData = cardGO.GetComponent<CardData>();
        if (viewData != null && !string.IsNullOrWhiteSpace(viewData.id))
            return viewData.id.Trim();

        var identity = cardGO.GetComponent<CardIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.Id))
            return identity.Id.Trim();

        return null;
    }

    // 每回合玩家 Bench 只能放一次；成功消耗時回傳 true
    public bool TryConsumeBenchPlacementThisTurn()
    {
        if (!isplayerturn || !turnInProgress)
        {
            Debug.LogWarning("[GamePlay] Bench placement rejected: not in active player turn.", this);
            return false;
        }

        if (playerBenchPlacedThisTurn)
        {
            Debug.Log("[GamePlay] Bench placement rejected: already used this turn.", this);
            return false;
        }

        playerBenchPlacedThisTurn = true;
        Debug.Log("[GamePlay] Bench placement consumed for this turn.", this);
        return true;
    }

    // 先攻玩家在自己的第一個回合不能攻擊。
    public bool CanPlayerAttackThisTurn(out string reason)
    {
        if (!isplayerturn || !turnInProgress)
        {
            reason = "Not in active player turn.";
            return false;
        }

        if (playerWentFirst && playerTurnCount <= 1)
        {
            reason = "Go-first player cannot attack in first round.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

        // 每回合玩家 common summon 只能放一次；成功消耗時回傳 true
        public bool TryConsumeCommonSummonThisTurn()
        {
            if (!isplayerturn || !turnInProgress)
            {
                Debug.LogWarning("[GamePlay] Common summon rejected: not in active player turn.", this);
                return false;
            }

            if (playerCommonSummonUsedThisTurn)
            {
                Debug.Log("[GamePlay] Common summon rejected: already used this turn.", this);
                return false;
            }

            playerCommonSummonUsedThisTurn = true;
            Debug.Log("[GamePlay] Common summon consumed for this turn.", this);
            return true;
        }

        // 嘗試消耗指定數量和顏色的能量；若不足或無該顏色則回傳 false，否則消耗並回傳 true
        public bool TryConsumeEnergyByColor(string requiredColor, int costAmount)
        {
            if (energy == null || energy.Count < costAmount)
            {
                Debug.LogWarning($"[GamePlay] Energy insufficient: required {costAmount}, available {(energy != null ? energy.Count : 0)}", this);
                return false;
            }

            // 計算是否有足夠的該顏色能量
            int colorCount = 0;
            foreach (var e in energy)
            {
                if (string.Equals(e, requiredColor, System.StringComparison.OrdinalIgnoreCase)) colorCount++;
            }

            if (colorCount < costAmount)
            {
                Debug.LogWarning($"[GamePlay] Energy color mismatch: need {costAmount}x '{requiredColor}', have {colorCount}", this);
                return false;
            }

            // 消耗該顏色的能量
            int consumed = 0;
            for (int i = energy.Count - 1; i >= 0 && consumed < costAmount; i--)
            {
                if (string.Equals(energy[i], requiredColor, System.StringComparison.OrdinalIgnoreCase))
                {
                    energy.RemoveAt(i);
                    consumed++;
                }
            }

            Debug.Log($"[GamePlay] Consumed {consumed}x '{requiredColor}' energy. Remaining: [{string.Join(", ", energy)}]", this);
            OnPlayerEnergyUpdated?.Invoke(energy);
            return true;
        }

    // 供 UI Button（EndTurn/NextStage）呼叫以結束玩家回合
    public void EndPlayerTurn()
    {
        if (!turnInProgress || !isplayerturn)
        {
            Debug.LogWarning("[GamePlay] EndPlayerTurn called at wrong time.", this);
            return;
        }
        isplayerturn = false;
        turnInProgress = false;
    }

    // AI 回合：抽牌（資料 + 視覺）+ 頂牌進核心區 + 自動結束
    private void StartAITurn()
    {
        turnInProgress = true;
        Debug.Log($"[GamePlay] AI turn start energy: current=[{string.Join(", ", AIenergy ?? new List<string>())}]", this);

        if (aiDeck != null && aiHand != null)
        {
            var drawn = aiDeck.drawCard(aiHand, 1);
            foreach (var id in drawn)
                aiHandController?.AddCardToHandById(id);

            string top = aiDeck.topCard();
            if (top != null)
            {
                aiCoreArea.Add(top);
                OnAICoreAreaUpdated?.Invoke(aiCoreArea);
            }
        }

        // AI 策略邏輯（未來擴充位置）
        Debug.Log("[GamePlay] AI turn done.", this);
        EndAITurn();
    }

    private void EndAITurn()
    {
        isplayerturn = true;
        turnInProgress = false;
    }

    // 供其他腳本（如 showCore）獲取玩家最後一張 core 卡的 ID
    public string GetPlayerLastCoreCardId()
    {
        if (playerCoreArea == null || playerCoreArea.Count == 0)
        {
            Debug.LogWarning("[GamePlay] No core cards available in playerCoreArea");
            return null;
        }
        return playerCoreArea[playerCoreArea.Count - 1];
    }

    // 供其他腳本獲取玩家所有 core 卡的 ID 列表
    public IReadOnlyList<string> GetPlayerCoreArea()
    {
        return playerCoreArea ?? new List<string>();
    }

    // 供其他腳本獲取玩家棄牌區卡片 ID 列表
    public IReadOnlyList<string> GetPlayerDiscardPile()
    {
        return playerDiscardPile ?? new List<string>();
    }

    // 供其他腳本獲取 AI 棄牌區卡片 ID 列表
    public IReadOnlyList<string> GetAIDiscardPile()
    {
        return aiDiscardPile ?? new List<string>();
    }

    // 加入一張牌到玩家棄牌區
    public void AddCardToPlayerDiscard(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return;

        if (playerDiscardPile == null)
            playerDiscardPile = new List<string>();

        playerDiscardPile.Add(cardId);
        OnPlayerDiscardPileUpdated?.Invoke(playerDiscardPile);
        Debug.Log($"[GamePlay] Card moved to player discard: {cardId} (count={playerDiscardPile.Count})", this);
    }

    // 加入一張牌到 AI 棄牌區
    public void AddCardToAIDiscard(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return;

        if (aiDiscardPile == null)
            aiDiscardPile = new List<string>();

        aiDiscardPile.Add(cardId);
        OnAIDiscardPileUpdated?.Invoke(aiDiscardPile);
        Debug.Log($"[GamePlay] Card moved to AI discard: {cardId} (count={aiDiscardPile.Count})", this);
    }

    // 效果用：從玩家手牌棄置指定張數（目前採用由後往前的順序）
    public int DiscardCardsFromPlayerHand(int count, bool updateHandView = true)
    {
        if (count <= 0) return 0;
        if (handData == null)
        {
            Debug.LogWarning("[GamePlay] DiscardCardsFromPlayerHand failed: handData is null", this);
            return 0;
        }

        handData.LoadHand();
        if (handData.Hand == null || handData.Hand.Count == 0)
        {
            Debug.Log("[GamePlay] DiscardCardsFromPlayerHand: hand is empty", this);
            return 0;
        }

        int discarded = 0;
        for (int i = handData.Hand.Count - 1; i >= 0 && discarded < count; i--)
        {
            string id = handData.Hand[i];
            handData.Hand.RemoveAt(i);
            AddCardToPlayerDiscard(id);
            discarded++;

            if (updateHandView)
                RemoveFirstHandCardVisualById(id);
        }

        handData.SaveHand();
        if (handController != null)
            handController.RefreshUIHandRecord();

        Debug.Log($"[GamePlay] DiscardCardsFromPlayerHand: requested={count}, discarded={discarded}", this);
        return discarded;
    }

    /// <summary>
    /// 棄置手牌中指定 cardId 的一張牌（資料層 + 視覺層）。
    /// </summary>
    public bool DiscardSpecificCardFromPlayerHand(string cardId, bool updateHandView = true)
    {
        if (string.IsNullOrWhiteSpace(cardId) || handData == null) return false;

        handData.LoadHand();
        int idx = handData.Hand != null ? handData.Hand.IndexOf(cardId) : -1;
        if (idx < 0)
        {
            Debug.LogWarning($"[GamePlay] DiscardSpecificCardFromPlayerHand: id={cardId} not found in hand.", this);
            return false;
        }

        handData.Hand.RemoveAt(idx);
        AddCardToPlayerDiscard(cardId);

        if (updateHandView)
            RemoveFirstHandCardVisualById(cardId);

        handData.SaveHand();
        if (handController != null)
            handController.RefreshUIHandRecord();

        Debug.Log($"[GamePlay] DiscardSpecificCardFromPlayerHand: discarded {cardId}", this);
        return true;
    }

    /// <summary>
    /// 若 DiscardSelectUI 存在則展示互動棄牌 UI 並等待玩家選擇；否則自動從手牌末尾棄置。
    /// 回傳實際棄牌數量。
    /// </summary>
    public async Task<int> RequestInteractiveDiscardAsync(int count)
    {
        var ui = discardSelectUI != null ? discardSelectUI : DiscardSelectUI.Instance;
        if (ui != null)
        {
            var result = await ui.RequestDiscardAsync(count);
            return result.Count;
        }
        // Fallback：自動棄置
        return DiscardCardsFromPlayerHand(count, updateHandView: true);
    }

    /// <summary>
    /// 在不消耗的前提下，檢查 playerCoreArea（Core 卡片數量）是否足夠支付 count 張。
    /// </summary>
    public bool CanPayCoreCount(int count)
    {
        if (count <= 0) return true;
        return playerCoreArea != null && playerCoreArea.Count >= count;
    }

    /// <summary>
    /// 從 playerCoreArea 移除 count 張 Core 卡（從末尾開始），並觸發更新事件。
    /// </summary>
    public bool TryConsumeCoreCount(int count)
    {
        if (count <= 0) return true;
        if (playerCoreArea == null || playerCoreArea.Count < count)
        {
            Debug.LogWarning($"[GamePlay] Core 不足：需要 {count}，現有 {playerCoreArea?.Count ?? 0}");
            return false;
        }
        for (int i = 0; i < count; i++)
            playerCoreArea.RemoveAt(playerCoreArea.Count - 1);
        OnPlayerCoreAreaUpdated?.Invoke(playerCoreArea);
        Debug.Log($"[GamePlay] 消耗 {count} Core。剩餘：{playerCoreArea.Count}");
        return true;
    }

    /// <summary>
    /// 在不消耗的前提下，檢查玩家是否擁有足夠數量的指定顏色 Core（舊版 playerCoreResources）。
    /// </summary>
    public bool CanPayCoreByColor(string color, int count)
    {
        if (count <= 0) return true;
        if (playerCoreResources == null) return false;
        int has = 0;
        foreach (var c in playerCoreResources)
            if (string.Equals(c, color, System.StringComparison.OrdinalIgnoreCase)) has++;
        return has >= count;
    }

    /// <summary>
    /// 在不消耗的前提下，檢查玩家是否擁有足夠數量的指定顏色 Energy。
    /// </summary>
    public bool CanPayEnergyByColor(string color, int count)
    {
        if (count <= 0) return true;
        if (energy == null) return false;
        int has = 0;
        foreach (var e in energy)
            if (string.Equals(e, color, System.StringComparison.OrdinalIgnoreCase)) has++;
        return has >= count;
    }

    // 場上卡片被破壞時呼叫：從物件解析 cardId 並送入玩家棄牌區
    public bool TrySendCardGameObjectToPlayerDiscard(GameObject cardObject)
    {
        if (cardObject == null) return false;

        if (IsOppTagged(cardObject))
        {
            Debug.Log($"[GamePlay] Skip discard for opp card: {cardObject.name}", this);
            return false;
        }

        string cardId = ResolveCardIdFromGameObject(cardObject);
        if (string.IsNullOrWhiteSpace(cardId))
        {
            Debug.LogWarning($"[GamePlay] TrySendCardGameObjectToPlayerDiscard failed: cannot resolve card id from '{cardObject.name}'", this);
            return false;
        }

        if (handData != null)
            handData.RemoveCardId(cardId);

        AddCardToPlayerDiscard(cardId);
        return true;
    }

    private bool IsOppTagged(GameObject go)
    {
        if (go == null) return false;

        var t = go.transform;
        while (t != null)
        {
            if (t.CompareTag("opp") || t.CompareTag("Opp"))
                return true;
            t = t.parent;
        }

        return false;
    }

    private bool RemoveFirstHandCardVisualById(string cardId)
    {
        if (handController == null || handController.handContainer == null || string.IsNullOrWhiteSpace(cardId))
            return false;

        for (int i = handController.handContainer.childCount - 1; i >= 0; i--)
        {
            var child = handController.handContainer.GetChild(i).gameObject;
            string childId = ResolveCardIdFromGameObject(child);
            if (childId != cardId) continue;

            child.transform.SetParent(null, false);
            handController.OnCardRemoved(child);
            Destroy(child);
            return true;
        }

        return false;
    }

    private string ResolveCardIdFromGameObject(GameObject cardObject)
    {
        if (cardObject == null) return null;

        var simpleData = cardObject.GetComponent<SimpleCardData>();
        if (simpleData != null && !string.IsNullOrWhiteSpace(simpleData.cardId))
            return simpleData.cardId.Trim();

        var viewData = cardObject.GetComponent<global::CardData>();
        if (viewData != null && !string.IsNullOrWhiteSpace(viewData.id))
            return viewData.id.Trim();

        var identity = cardObject.GetComponent<CardIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.Id))
            return identity.Id.Trim();

        var rawName = cardObject.name.Replace("(Clone)", "").Trim();
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

            if (allDigits && int.TryParse(p, out int n))
                return n.ToString("D2");
        }

        return null;
    }

    private IEnumerator HideYourTurnTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (YourTurnText != null)
        {
            YourTurnText.gameObject.SetActive(false);
        }
    }
}
