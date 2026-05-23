using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using static DeckData;
using static HandData;
using random = UnityEngine.Random;
using UnityEngine.UI;
using UnityEngine.Video;
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
    [SerializeField] private TMPro.TextMeshProUGUI playerHPText;
    [SerializeField] private TMPro.TextMeshProUGUI opponentHPText;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private RawImage reshuffleUI; // 下一階段按鈕（可選，若有則綁定結束回合功能）
    [SerializeField] private Button reshuffleButton; // 重新洗牌按鈕
    [SerializeField] private Button noreshuffleButton; // 不重新洗牌按鈕
    [SerializeField] private GameObject endGameVideoObject;
    [SerializeField] private VideoPlayer endGameVideoPlayer;

    private bool isplaywithAI = false;
    private bool isplayerturn = true;
    private bool gamestart = false;
    private bool isEndSequenceActive = false;
    private bool setupCompleted = false;
    private bool turnInProgress = false;
    private bool playerBenchPlacedThisTurn = false;
    private bool playerWentFirst = false;
    private bool hasShownTurnOrderPrompt = false;
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
    [SerializeField] private UIExampleController uiExampleController;

    [Header("Board Prefabs")]
    [SerializeField] private GameObject playerBoardCardPrefab;
    [SerializeField] private GameObject aiBoardCardPrefab;
    [SerializeField] private float aiActionDelaySeconds = 2f;

    private GameObject GetBoardCardPrefab(bool isPlayer)
    {
        return isPlayer ? playerBoardCardPrefab : aiBoardCardPrefab;
    }

    [Header("AI Board Area Hints")]
    [SerializeField] private RawImage[] aiAttackAreaHints;
    [SerializeField] private RawImage[] aiBenchAreaHints;

    private List<string> playerCoreArea;
    private List<string> aiCoreArea;
    private List<string> playerDiscardPile;
    private List<string> aiDiscardPile;
    private List<string> energy;
    private List<string> AIenergy;
    private List<string> playerCoreResources;
    private string playerAceCardId;
    private string aiAceCardId;
    private readonly Dictionary<string, int> playerAttackBuffByCardId = new Dictionary<string, int>();
    
    // 場上卡牌管理
    private List<string> playerAttackArea = new List<string>();
    private List<string> aiAttackArea = new List<string>();
    private List<string> playerBenchArea = new List<string>();
    private List<string> aiBenchArea = new List<string>();

    private readonly List<GameObject> effectSummonTargetHighlights = new List<GameObject>();
    private TaskCompletionSource<SimpleDropArea> effectSummonSelectionTcs;

    private CardrunTime cardrunTime;

    private int playerHP = 20;
    private int aiHP = 20;

    // 供其他腳本讀取能量區資料
    public IReadOnlyList<string> Energy => energy;
    public IReadOnlyList<string> AIEnergy => AIenergy;
    public IReadOnlyList<string> CoreResources => playerCoreResources;
    public string PlayerAceCardId => playerAceCardId;
    public string AIAceCardId => aiAceCardId;
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
        }
        else
        {
            playWithAIButton.onClick.RemoveListener(OnPlayWithAIButtonClicked);
            playWithAIButton.onClick.AddListener(OnPlayWithAIButtonClicked);
            Debug.Log("[GamePlay] OnClick listener registered.", this);
        }

        if (uiExampleController == null)
        {
            uiExampleController = FindObjectOfType<UIExampleController>(true);
        }

        if (uiExampleController != null)
        {
            OnHPUpdated += uiExampleController.UpdateHealthBars;
            Debug.Log("[GamePlay] UIExampleController found and subscribed to HP updates.", this);
        }
        else
        {
            Debug.LogWarning("[GamePlay] UIExampleController not found. HP bar UI will not update.", this);
        }
    }

    void OnDisable()
    {
        if (playWithAIButton != null)
        {
            playWithAIButton.onClick.RemoveListener(OnPlayWithAIButtonClicked);
        }

        if (uiExampleController != null)
        {
            OnHPUpdated -= uiExampleController.UpdateHealthBars;
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

        if (playerHPText == null)
        {
            playerHPText = FindTextByName("myHP") ?? FindTextByName("playerHP");
        }

        if (opponentHPText == null)
        {
            opponentHPText = FindTextByName("opp_HP") ?? FindTextByName("enemyHP") ?? FindTextByName("aiHP");
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

        if (uiExampleController == null)
        {
            uiExampleController = FindObjectOfType<UIExampleController>(true);
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

    private TMPro.TextMeshProUGUI FindTextByName(string objectName)
    {
        var go = FindObjectByName(objectName);
        if (go == null) return null;

        var text = go.GetComponent<TMPro.TextMeshProUGUI>();
        if (text != null) return text;

        return go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
    }

    private void NotifyHPUpdated()
    {
        OnHPUpdated?.Invoke(playerHP, aiHP);
        RefreshHPTextUI();
        CheckEndGameConditions();
    }

    private void CheckEndGameConditions()
    {
        if (isEndSequenceActive) return;
        if (aiHP <= 0)
        {
            StartEndGameSequence();
        }
    }

    private void StartEndGameSequence()
    {
        isEndSequenceActive = true;
        gamestart = false;
        turnInProgress = false;

        if (endGameVideoObject != null)
        {
            endGameVideoObject.SetActive(true);
            if (endGameVideoPlayer == null)
                endGameVideoPlayer = endGameVideoObject.GetComponent<VideoPlayer>();

            if (endGameVideoPlayer != null)
            {
                endGameVideoPlayer.Stop();
                endGameVideoPlayer.Play();
            }
        }
    }

    private void ReturnToMainMenuAndResetBattleScene()
    {
        isEndSequenceActive = false;
        if (endGameVideoPlayer != null)
        {
            endGameVideoPlayer.Stop();
        }

        if (endGameVideoObject != null)
        {
            endGameVideoObject.SetActive(false);
        }

        if (nowscene != null)
        {
            nowscene.SetActive(true);
        }

        if (playscene != null)
        {
            playscene.SetActive(false);
        }

        ResetBattleState();
    }

    private void ResetBattleState()
    {
        gamestart = false;
        turnInProgress = false;
        setupCompleted = false;
        isplayerturn = true;
        playerBenchPlacedThisTurn = false;
        playerCommonSummonUsedThisTurn = false;
        playerWentFirst = false;
        hasShownTurnOrderPrompt = false;
        playerTurnCount = 0;

        if (handController != null && handController.handContainer != null)
        {
            for (int i = handController.handContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(handController.handContainer.GetChild(i).gameObject);
            }
            handController.handCardTransforms.Clear();
            handController.RefreshUIHandRecord();
        }

        if (aiHandController != null && aiHandController.handContainer != null)
        {
            for (int i = aiHandController.handContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(aiHandController.handContainer.GetChild(i).gameObject);
            }
            aiHandController.handCardTransforms.Clear();
            aiHandController.RefreshUIHandRecord();
        }

        if (handData != null)
            handData.ClearHand();
        if (aiHand != null)
            aiHand.ClearHand();

        var cardViews = FindObjectsOfType<CardData>(true);
        foreach (var cardView in cardViews)
        {
            if (cardView != null && cardView.gameObject != null)
                Destroy(cardView.gameObject);
        }

        foreach (var highlight in effectSummonTargetHighlights)
        {
            if (highlight != null)
                Destroy(highlight);
        }
        effectSummonTargetHighlights.Clear();

        playerCoreArea?.Clear();
        aiCoreArea?.Clear();
        playerDiscardPile?.Clear();
        aiDiscardPile?.Clear();
        playerAttackArea?.Clear();
        aiAttackArea?.Clear();
        playerBenchArea?.Clear();
        aiBenchArea?.Clear();
        playerCoreResources?.Clear();
        energy?.Clear();
        AIenergy?.Clear();

        playerAceCardId = null;
        aiAceCardId = null;

        if (endTurnButton != null)
        {
            endTurnButton.interactable = false;
            endTurnButton.onClick.RemoveListener(EndPlayerTurn);
        }

        if (reshuffleButton != null)
            reshuffleButton.onClick.RemoveAllListeners();
        if (noreshuffleButton != null)
            noreshuffleButton.onClick.RemoveAllListeners();

        if (YourTurnText != null)
            YourTurnText.gameObject.SetActive(false);
        if (reshuffleUI != null)
            reshuffleUI.gameObject.SetActive(false);

        NotifyHPUpdated();
    }

    private void RefreshHPTextUI()
    {
        if (playerHPText != null)
            playerHPText.text = playerHP.ToString();

        if (opponentHPText != null)
            opponentHPText.text = aiHP.ToString();
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
        playerAttackArea = new List<string>();
        aiAttackArea = new List<string>();
        playerBenchArea = new List<string>();
        aiBenchArea = new List<string>();
        playerAceCardId = null;
        aiAceCardId = null;
        energy = new List<string>();
        AIenergy = new List<string>();
        playerBenchPlacedThisTurn = false;
        playerHP = 20;
        aiHP = 20;
        NotifyHPUpdated();

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
        hasShownTurnOrderPrompt = true;
        playerTurnCount = 0;
        turnInProgress = false;
        gamestart = true;

        if (YourTurnText != null)
        {
            YourTurnText.text = isplayerturn ? "You go first" : "You go second";
            YourTurnText.gameObject.SetActive(true);
            StartCoroutine(HideYourTurnTextAfterDelay(1f));
        }

        Debug.Log($"[GamePlay] Turn order decided: playerWentFirst={playerWentFirst}", this);
    }
    

    // Update is called once per frame
    void Update()
    {
        // turnInProgress 防止每幀重複觸發；等待 EndPlayerTurn() 或 EndAITurn() 重置
        if (isEndSequenceActive)
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                ReturnToMainMenuAndResetBattleScene();
            }
            return;
        }

        if (!gamestart || turnInProgress) return;
        if (!isplaywithAI) return;

        if (isplayerturn)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution will continue before the call completes
            StartPlayerTurn();
#pragma warning restore CS4014
        }
        else
            StartCoroutine(StartAITurn());
    }

    // 玩家回合開始：抽牌（資料 + 視覺）+ 頂牌進核心區，然後等待玩家輸入
    private async Task StartPlayerTurn()
    {
        playerTurnCount++;
        turnInProgress = true;
        playerBenchPlacedThisTurn = false;
        playerCommonSummonUsedThisTurn = false; // 重置每回合 common summon 使用
        ResetPlayerCardBattleStateToggleUsage();
        ResetPlayerCardAttackUsage();
        ClearPlayerAce(); // 回合開始時清除玩家 Ace
        ClearPlayerAttackBuffs();

        int generated = GenerateBenchEnergyAtTurnStart();
        if (generated > 0)
        {
            AddPlayerEnergy("generic", generated);
        }
        int triggerCount = TriggerTurnStartEffectsForPlayerBoard();

        Debug.Log("[GamePlay] Player turn start: bench & common summon placement reset.", this);
        Debug.Log($"[GamePlay] TurnStart effects triggered on board cards: {triggerCount}", this);
        Debug.Log($"[GamePlay] Player turn index={playerTurnCount}, playerWentFirst={playerWentFirst}", this);
        Debug.Log($"[GamePlay] Player turn start energy: generated={generated}, current=[{string.Join(", ", energy ?? new List<string>())}]", this);
        if (YourTurnText != null)
        {
            if (!(hasShownTurnOrderPrompt && playerTurnCount == 1 && playerWentFirst))
            {
                YourTurnText.text = "Your Turn";
                YourTurnText.gameObject.SetActive(true);
                StartCoroutine(HideYourTurnTextAfterDelay(1f)); // 1秒後隱藏提示
            }
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

            if (!IsPlayerOwnedArea(area.transform))
                continue;

            activeBenchAreas++;
            int generatedFromArea = area.GrantBenchEnergyFromPlacedCardsAtTurnStart();
            totalGenerated += generatedFromArea;
            Debug.Log($"[GamePlay] TurnStart Bench area '{area.name}' generated={generatedFromArea}", this);
        }

        Debug.Log($"[GamePlay] TurnStart Bench scan: dropAreas={totalDropAreas}, benchAreas={activeBenchAreas}, generated={totalGenerated}", this);
        return totalGenerated;
    }

    private int GenerateAIBenchEnergyAtTurnStart()
    {
        int totalGenerated = 0;
        int activeBenchAreas = 0;

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid()) continue;
            if (!area.IsBenchArea()) continue;
            if (IsPlayerOwnedArea(area.transform)) continue;

            activeBenchAreas++;
            int generatedFromArea = area.GrantBenchEnergyFromPlacedCardsAtTurnStart(false);
            totalGenerated += generatedFromArea;
            Debug.Log($"[GamePlay] AI TurnStart bench generated={generatedFromArea} from area={area.name}");
        }

        Debug.Log($"[GamePlay] AI TurnStart bench total generated={totalGenerated} from {activeBenchAreas} bench areas");
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

    public void AddAIEnergy(string color, int count)
    {
        if (string.IsNullOrEmpty(color) || count <= 0) return;
        if (AIenergy == null)
        {
            AIenergy = new List<string>();
            Debug.LogWarning("[GamePlay] AIenergy 尚未初始化，已自動建立新清單。", this);
        }
        for (int i = 0; i < count; i++)
            AIenergy.Add(color);
        Debug.Log($"[GamePlay] AIenergy +{count} {color}: [{string.Join(", ", AIenergy)}]", this);
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

    private List<string> GetAIAttackAreaCardIds()
    {
        var result = new List<string>();

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsAttackArea())
                continue;

            if (IsPlayerOwnedArea(area.transform)) // AI區域是非玩家區域
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

    private List<string> GetPlayerBenchAreaCardIds()
    {
        var result = new List<string>();

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsBenchArea())
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

    private List<string> GetAIBenchAreaCardIds()
    {
        var result = new List<string>();

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!area.IsBenchArea())
                continue;

            if (IsPlayerOwnedArea(area.transform)) // AI區域是非玩家區域
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

        // 若找不到 player/ai 關鍵字，預設當成非玩家區域，避免 AI 區域被錯誤判定為玩家區域。
        return false;
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

    public void RegisterAIAce(string aceCardId)
    {
        aiAceCardId = aceCardId;
        Debug.Log($"[GamePlay] Registered AI ace: {aceCardId}", this);
    }

    public void ClearAIAce()
    {
        aiAceCardId = null;
        Debug.Log("[GamePlay] AI ace cleared.", this);
    }

    public void AddPlayerHP(int amount)
    {
        if (amount <= 0) return;
        playerHP += amount;
        Debug.Log($"[GamePlay] Player HP +{amount}: {playerHP}", this);
        NotifyHPUpdated();
    }

    public void ReducePlayerHP(int amount)
    {
        if (amount <= 0) return;
        playerHP = Mathf.Max(0, playerHP - amount);
        Debug.Log($"[GamePlay] Player HP -{amount}: {playerHP}", this);
        NotifyHPUpdated();
    }

    public void ReduceAIHP(int amount)
    {
        if (amount <= 0) return;
        aiHP = Mathf.Max(0, aiHP - amount);
        Debug.Log($"[GamePlay] AI HP -{amount}: {aiHP}", this);
        NotifyHPUpdated();
    }

    public void SetPlayerHP(int newHP)
    {
        playerHP = Mathf.Max(0, newHP);
        NotifyHPUpdated();
    }

    public void SetAIHP(int newHP)
    {
        aiHP = Mathf.Max(0, newHP);
        NotifyHPUpdated();
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

    private void ResetPlayerCardBattleStateToggleUsage()
    {
        foreach (var card in Resources.FindObjectsOfTypeAll<leftRightClickCard>())
        {
            if (card == null) continue;
            if (!card.gameObject.scene.IsValid()) continue;
            card.ResetBattleStateToggleThisTurn();
        }
    }

    private void ResetPlayerCardAttackUsage()
    {
        foreach (var card in Resources.FindObjectsOfTypeAll<leftRightClickCard>())
        {
            if (card == null) continue;
            if (!card.gameObject.scene.IsValid()) continue;
            card.ResetAttackUsageThisTurn();
        }
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

    // AI 回合：抽牌（資料 + 視覺）+ 頂牌進核心區 + AI 策略邏輯
    private IEnumerator StartAITurn()
    {
        turnInProgress = true;
        ClearAIAce(); // 回合開始時清除 AI Ace
        Debug.Log($"[GamePlay] AI turn start energy: current=[{string.Join(", ", AIenergy ?? new List<string>())}]", this);

        if (aiDeck != null && aiHand != null)
        {
            int generated = GenerateAIBenchEnergyAtTurnStart();
            if (generated > 0)
            {
                AddAIEnergy("generic", generated);
            }
            Debug.Log($"[GamePlay] AI turn start: generated bench energy={generated}, current AIenergy=[{string.Join(", ", AIenergy ?? new List<string>())}]");

            var drawn = aiDeck.drawCard(aiHand, 1);
            foreach (var id in drawn)
                aiHandController?.AddCardToHandById(id);

            string top = aiDeck.topCard();
            if (top != null)
            {
                aiCoreArea.Add(top);
                OnAICoreAreaUpdated?.Invoke(aiCoreArea);
            }

            Debug.Log($"[AI] StartAITurn: drawn {drawn.Count} card(s), current hand=[{string.Join(", ", aiHand.Hand ?? new List<string>())}], core top={top}");
            yield return new WaitForSeconds(aiActionDelaySeconds);
        }

        // AI 策略邏輯
        yield return ExecuteAIStrategy();

        Debug.Log("[GamePlay] AI turn done.", this);
        EndAITurn();
    }

    /// <summary>
    /// 將卡牌放置到指定區域（用於玩家與AI）
    /// </summary>
    public bool PlaceCardToArea(string cardId, bool isPlayer, bool toAttackArea)
    {
        if (string.IsNullOrEmpty(cardId))
            return false;

        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null || !cardEvent.TryGetCardById(cardId, out var cardData))
            return false;

        Debug.Log($"[GamePlay] PlaceCardToArea: card={cardId}, owner={(isPlayer ? "player" : "AI")}, targetArea={(toAttackArea ? "attack" : "bench")}, energyBefore=[{string.Join(", ", (isPlayer ? energy : AIenergy) ?? new List<string>())}]");

        // === Ace 卡特殊邏輯 ===
        if (toAttackArea && cardData.IsAce)
        {
            return HandleAceCardPlacement(cardId, cardData, isPlayer, cardEvent);
        }

        if (toAttackArea)
        {
            // 檢查能量是否足夠
            if (!CanAffordCard(cardData, isPlayer ? energy : AIenergy))
            {
                Debug.LogWarning($"[GamePlay] PlaceCardToArea failed: insufficient energy for {cardId}");
                return false;
            }

            // 消耗能量
            ConsumeEnergyForCard(cardEvent, cardId, isPlayer);
        }
        else
        {
            Debug.Log($"[GamePlay] Bench placement does not require energy for card {cardId}");
        }

        // 添加到對應區域
        if (isPlayer)
        {
            if (toAttackArea)
                playerAttackArea.Add(cardId);
            else
                playerBenchArea.Add(cardId);
        }
        else
        {
            if (toAttackArea)
                aiAttackArea.Add(cardId);
            else
                aiBenchArea.Add(cardId);
        }

        // 從手牌移除
        if (isPlayer)
        {
            handData.LoadHand();
            handData.Hand.Remove(cardId);
            handData.SaveHand();
        }
        else
        {
            aiHand.LoadHand();
            aiHand.Hand.Remove(cardId);
            aiHand.SaveHand();
            RemoveCardObjectFromHand(aiHandController, cardId);
            var spawned = TrySpawnAIBoardCard(cardId, toAttackArea);
            if (!spawned)
            {
                Debug.LogWarning($"[GamePlay] AI board card spawn failed for {cardId}. Restoring AI hand.");
                aiHand.LoadHand();
                aiHand.Hand.Add(cardId);
                aiHand.SaveHand();
                return false;
            }

            if (!toAttackArea && cardData.EnSpawn > 0)
            {
                string color = string.IsNullOrEmpty(cardData.Color) ? "colorless" : cardData.Color;
                AddAIEnergy(color, cardData.EnSpawn);
                Debug.Log($"[GamePlay] AI bench card placed and generated energy: id={cardId}, color={color}, en_spawn={cardData.EnSpawn}");
            }
        }

        Debug.Log($"[GamePlay] Placed card {cardId} to {(isPlayer ? "player" : "AI")} {(toAttackArea ? "attack" : "bench")} area");
        return true;
    }

    /// <summary>
    /// 處理 Ace 卡放置邏輯（玩家與 AI）
    /// </summary>
    private bool HandleAceCardPlacement(string cardId, Tur.CardData cardData, bool isPlayer, CardEvent cardEvent)
    {
        string sealStr = cardData.Seal?.ToLowerInvariant() ?? string.Empty;

        // 1. 檢查是否已有 Ace 卡
        string currentAceId = isPlayer ? playerAceCardId : aiAceCardId;
        if (!string.IsNullOrWhiteSpace(currentAceId))
        {
            Debug.LogWarning($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} 場上已有 Ace 卡，放置取消。");
            return false;
        }

        // 2. 解析封印中手牌棄置數量
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
            }
        }

        // 3. 解析封印中綠色能量棄置數量
        int greenEnergyCost = 0;
        if (sealStr.Contains("green energy"))
        {
            var tokens = sealStr.Split(new char[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (int.TryParse(tokens[i], out int num))
                {
                    if (tokens[i + 1].Contains("green"))
                    {
                        greenEnergyCost = num;
                        break;
                    }
                }
            }
        }

        int coreCost = cardData.Cost;

        // 4. 檢查資源是否足夠（不消耗）
        bool canPayCore = isPlayer ? CanPayCoreCount(coreCost) : CanPayAICoreCount(coreCost);
        bool canPayGreenEn = isPlayer ? CanPayEnergyByColor("green", greenEnergyCost) : CanPayAIEnergyByColor("green", greenEnergyCost);
        bool canDiscardHand = isPlayer ? (handData.Hand != null && handData.Hand.Count >= handDiscardCnt) : (aiHand.Hand != null && aiHand.Hand.Count >= handDiscardCnt);

        if (!canPayCore)
        {
            Debug.LogWarning($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} Core 不足（需 {coreCost} 張）。");
            return false;
        }
        if (!canPayGreenEn)
        {
            Debug.LogWarning($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} 綠色能量不足（需 {greenEnergyCost}）。");
            return false;
        }
        if (!canDiscardHand)
        {
            Debug.LogWarning($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} 手牌不足以棄置（需 {handDiscardCnt} 張）。");
            return false;
        }

        // 5. AI 自動丟棄（無需互動）；玩家需要互動式丟棄
        if (isPlayer)
        {
            // 玩家 Ace：由 SimpleDropArea 的 HandleAceHandDiscardCoroutine 負責互動與丟棄
            // 此函式只需消耗能量與 Core，丟棄由協程處理
            Debug.Log($"[GamePlay] Player Ace: deferring to SimpleDropArea interactive discard flow for {cardId}");
            return false; // 交給 SimpleDropArea 處理
        }
        else
        {
            // AI Ace：自動執行丟棄
            int discarded = DiscardCardsFromAIHand(handDiscardCnt, false);
            if (discarded < handDiscardCnt)
            {
                Debug.LogWarning($"[GamePlay] Ace: AI 手牌棄置失敗（請求 {handDiscardCnt} 張，成功 {discarded} 張），放置取消。");
                return false;
            }
            Debug.Log($"[GamePlay] Ace: AI 自動棄置 {discarded} 張手牌");
        }

        // 6. 消耗綠色能量
        if (greenEnergyCost > 0)
        {
            bool energyConsumed = isPlayer ? TryConsumeEnergyByColor("green", greenEnergyCost) : TryConsumeAIEnergyByColor("green", greenEnergyCost);
            if (!energyConsumed)
            {
                Debug.LogWarning($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} 綠色能量消耗失敗，放置取消。");
                return false;
            }
            Debug.Log($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} 消耗 {greenEnergyCost} 綠色能量");
        }

        // 7. 消耗 Core
        if (!((isPlayer ? TryConsumeCoreCount(coreCost) : TryConsumeAICoreCount(coreCost))))
        {
            Debug.LogWarning($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} Core 消耗失敗，放置取消。");
            return false;
        }
        Debug.Log($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} 消耗 {coreCost} Core");

        // 8. 登記 Ace 卡
        if (isPlayer)
            RegisterPlayerAce(cardId);
        else
            RegisterAIAce(cardId);

        Debug.Log($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} Ace 已登記: id={cardId}");

        // 9. 現在執行標準放置流程（添加到場地）
        if (isPlayer)
        {
            playerAttackArea.Add(cardId);
            handData.LoadHand();
            handData.Hand.Remove(cardId);
            handData.SaveHand();
        }
        else
        {
            aiAttackArea.Add(cardId);
            aiHand.LoadHand();
            aiHand.Hand.Remove(cardId);
            aiHand.SaveHand();
            RemoveCardObjectFromHand(aiHandController, cardId);
            var spawned = TrySpawnAIBoardCard(cardId, true);
            if (!spawned)
            {
                Debug.LogWarning($"[GamePlay] Ace: AI 版面卡生成失敗，放置取消。");
                aiAttackArea.Remove(cardId);
                aiHand.LoadHand();
                aiHand.Hand.Add(cardId);
                aiHand.SaveHand();
                ClearAIAce();
                return false;
            }
        }

        Debug.Log($"[GamePlay] Ace: {(isPlayer ? "玩家" : "AI")} Ace 卡 {cardId} 放置成功");
        return true;
    }

    /// <summary>
    /// 檢查是否能負擔卡牌的召喚成本
    /// </summary>
    private bool CanAffordCard(Tur.CardData cardData, List<string> energyList = null)
    {
        if (cardData == null)
            return false;

        var targetEnergy = energyList ?? energy;
        return targetEnergy != null && targetEnergy.Count >= cardData.Cost;
    }

    /// <summary>
    /// 為卡牌消耗能量
    /// </summary>
    private void ConsumeEnergyForCard(CardEvent cardEvent, string cardId, bool isPlayer = true)
    {
        if (!cardEvent.TryGetCardById(cardId, out var cardData) || cardData == null)
            return;

        var targetEnergy = isPlayer ? energy : AIenergy;
        if (targetEnergy == null)
            return;

        int cost = cardData.Cost;
        for (int i = 0; i < cost && targetEnergy.Count > 0; i++)
        {
            targetEnergy.RemoveAt(0); // 移除第一個能量
        }

        Debug.Log($"[GamePlay] Consumed {cost} energy for card {cardId} ({(isPlayer ? "player" : "AI")}). Remaining energy: {string.Join(", ", targetEnergy)}");
    }

    private void RemoveCardObjectFromHand(Handcontroller handControllerRef, string cardId)
    {
        if (handControllerRef == null || handControllerRef.handContainer == null || string.IsNullOrWhiteSpace(cardId))
            return;

        for (int i = handControllerRef.handContainer.childCount - 1; i >= 0; i--)
        {
            var child = handControllerRef.handContainer.GetChild(i)?.gameObject;
            if (child == null) continue;

            var resolvedId = ResolveCardIdForEffectTrigger(child);
            if (string.Equals(resolvedId, cardId, System.StringComparison.OrdinalIgnoreCase))
            {
                handControllerRef.OnCardRemoved(child);
                Destroy(child);
                return;
            }
        }
    }

    private bool TrySpawnAIBoardCard(string cardId, bool toAttackArea)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return false;

        var area = FindAIBoardArea(toAttackArea);
        if (area == null)
        {
            Debug.LogWarning($"[AI] No AI {(toAttackArea ? "attack" : "bench")} area available to place card {cardId}");
            return false;
        }

        var cardEvent = FindObjectOfType<CardEvent>(true);
        var targetArea = area.contentRoot != null ? area.contentRoot : area.transform;
        var prefab = GetBoardCardPrefab(isPlayer: false);
        if (prefab == null)
        {
            Debug.LogWarning($"[AI] Cannot spawn board card: card prefab not found for {cardId}");
            return false;
        }

        var go = Instantiate(prefab, targetArea, false);
        if (go == null)
            return false;

        go.SetActive(true);

        if (cardEvent.TryGetCardById(cardId, out var cardData) && cardData != null)
        {
            var cardView = go.GetComponent<global::CardData>();
            if (cardView != null)
            {
                cardView.overrideFromCode = true;
                cardView.InitializeFromData(cardData);
            }
            else
            {
                var fallbackView = go.GetComponent<CardData>();
                if (fallbackView != null)
                {
                    fallbackView.SetCardId(cardId);
                }
            }
        }
        else
        {
            var fallbackView = go.GetComponent<CardData>();
            if (fallbackView != null)
            {
                fallbackView.SetCardId(cardId);
            }
        }

        var simpleData = go.GetComponent<SimpleCardData>();
        if (simpleData != null)
        {
            simpleData.cardId = cardId;
        }

        var identity = go.GetComponent<CardIdentity>();
        if (identity != null)
        {
            identity.Id = cardId;
        }

        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            var prefabRect = prefab.GetComponent<RectTransform>();
            rect.localScale = prefabRect != null ? prefabRect.localScale : Vector3.one;
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
        }

        return true;
    }

    private SimpleDropArea FindAIBoardArea(bool toAttackArea)
    {
        var hint = toAttackArea ? GetDropAreaFromHints(aiAttackAreaHints) : GetDropAreaFromHints(aiBenchAreaHints);
        if (hint != null && hint.gameObject.scene.IsValid())
        {
            if ((toAttackArea && hint.IsAttackArea()) || (!toAttackArea && hint.IsBenchArea()))
            {
                if (!IsPlayerOwnedArea(hint.transform) && !IsDropAreaOccupied(hint))
                    return hint;
            }
        }

        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (toAttackArea && !area.IsAttackArea())
                continue;
            if (!toAttackArea && !area.IsBenchArea())
                continue;
            if (IsPlayerOwnedArea(area.transform))
                continue;

            if (!IsDropAreaOccupied(area))
                return area;
        }

        return null;
    }

    private SimpleDropArea GetDropAreaFromHints(RawImage[] hints)
    {
        if (hints == null || hints.Length == 0)
            return null;

        foreach (var hint in hints)
        {
            if (hint == null)
                continue;

            var area = hint.GetComponent<SimpleDropArea>();
            if (area != null)
                return area;

            area = hint.GetComponentInParent<SimpleDropArea>();
            if (area != null)
                return area;
        }

        return null;
    }

    private bool IsDropAreaOccupied(SimpleDropArea area)
    {
        if (area == null)
            return false;

        var root = area.contentRoot != null ? area.contentRoot : area.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null)
                continue;

            string id = ResolveCardIdForEffectTrigger(child.gameObject);
            if (!string.IsNullOrWhiteSpace(id))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 觸發卡牌召喚效果
    /// </summary>
    private void TriggerCardSummonEffect(string cardId, bool isAttackArea)
    {
        var eventType = CardEffectEvent.EventType.CommonSummon;
        Debug.Log($"[GamePlay] TriggerCardSummonEffect: card={cardId}, eventType={eventType}, area={(isAttackArea ? "attack" : "bench")}");
        if (cardrunTime != null)
        {
            cardrunTime.TriggerCardEffect(cardId, eventType);
        }
    }

    /// <summary>
    /// AI 策略邏輯實現
    /// </summary>
    private IEnumerator ExecuteAIStrategy()
    {
        // 獲取 CardEvent 引用
        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null)
        {
            Debug.LogWarning("[AI] CardEvent not found, skipping AI strategy");
            yield break;
        }

        // 載入手牌
        aiHand.LoadHand();
        var handCards = aiHand.Hand ?? new List<string>();
        
        Debug.Log($"[AI] Hand cards: {string.Join(", ", handCards)}");
        Debug.Log($"[AI] Energy: {string.Join(", ", AIenergy ?? new List<string>())}");

        // AI策略：根據當前回合進程與卡片屬性選擇召喚目標。
        bool earlyGame = playerTurnCount <= 3;
        bool lateGame = playerTurnCount >= 5;

        string bestBenchCard = null;
        int bestBenchScore = int.MinValue;
        string bestAttackCard = null;
        int bestAttackScore = int.MinValue;

        foreach (var cardId in handCards)
        {
            if (!cardEvent.TryGetCardById(cardId, out var cardData) || cardData == null)
                continue;

            bool canSummon = CanAffordCard(cardData, AIenergy);
            int benchScore = CalculateAISummonScore(cardData, false, earlyGame, lateGame);
            int attackScore = canSummon ? CalculateAISummonScore(cardData, true, earlyGame, lateGame) : int.MinValue;

            Debug.Log($"[AI] Evaluate hand card {cardId}: Atk={cardData.Atk}, Cost={cardData.Cost}, EnSpawn={cardData.EnSpawn}, canSummon={canSummon}, benchScore={benchScore}, attackScore={attackScore}");

            if (benchScore > bestBenchScore)
            {
                bestBenchScore = benchScore;
                bestBenchCard = cardId;
            }

            if (attackScore > bestAttackScore)
            {
                bestAttackScore = attackScore;
                bestAttackCard = cardId;
            }
        }

        string chosenSummonCardId = null;
        bool toAttackArea = true;
        bool attackAreaAvailable = FindAIBoardArea(true) != null;
        bool benchAreaAvailable = FindAIBoardArea(false) != null;

        if (benchAreaAvailable && bestBenchCard != null && bestBenchScore >= bestAttackScore)
        {
            chosenSummonCardId = bestBenchCard;
            toAttackArea = false;
        }
        else if (attackAreaAvailable && bestAttackCard != null)
        {
            chosenSummonCardId = bestAttackCard;
            toAttackArea = true;
        }
        else if (!attackAreaAvailable && benchAreaAvailable && bestBenchCard != null)
        {
            chosenSummonCardId = bestBenchCard;
            toAttackArea = false;
        }
        else
        {
            Debug.Log("[AI] No suitable card to summon");
        }

        if (!string.IsNullOrWhiteSpace(chosenSummonCardId))
        {
            Debug.Log($"[AI] Chosen summon target: {chosenSummonCardId} (attackScore={bestAttackScore}, benchScore={bestBenchScore})");
            Debug.Log($"[AI] Will place card {chosenSummonCardId} to {(toAttackArea ? "attack" : "bench")} area (attackAvailable={attackAreaAvailable}, benchAvailable={benchAreaAvailable})");

            if (PlaceCardToArea(chosenSummonCardId, false, toAttackArea))
            {
                Debug.Log($"[AI] Successfully summoned card: {chosenSummonCardId} to {(toAttackArea ? "attack" : "bench")} area");
                yield return new WaitForSeconds(aiActionDelaySeconds);

                if (toAttackArea)
                {
                    ExecuteAIAttack(chosenSummonCardId);
                    yield return new WaitForSeconds(aiActionDelaySeconds);
                }
            }
            else
            {
                Debug.LogWarning($"[AI] Failed to place card {chosenSummonCardId} to {(toAttackArea ? "attack" : "bench")} area");
            }
        }

        if (string.IsNullOrWhiteSpace(chosenSummonCardId) || aiAttackArea.Count == 0 || !aiAttackArea.Contains(chosenSummonCardId))
        {
            var existingAttackCards = GetAIAttackAreaCardIds();
            if (existingAttackCards.Count > 0)
            {
                var playerAttackCards = GetPlayerAttackAreaCardIds();
                string attackCardId = SelectBestAIAttacker(existingAttackCards, playerAttackCards, cardEvent);
                if (!string.IsNullOrWhiteSpace(attackCardId))
                {
                    Debug.Log($"[AI] Attacking with existing board card: {attackCardId}");
                    ExecuteAIAttack(attackCardId);
                    yield return new WaitForSeconds(aiActionDelaySeconds);
                }
                else
                {
                    Debug.Log("[AI] No valid existing AI attack card found to attack.");
                }
            }
            else
            {
                Debug.Log("[AI] No existing AI attack cards available.");
            }
        }
    }

    private int CalculateAISummonScore(Tur.CardData cardData, bool toAttackArea, bool earlyGame, bool lateGame)
    {
        if (cardData == null)
            return int.MinValue;

        bool specialSummon = IsSpecialSummonCard(cardData);
        int score = 0;

        if (specialSummon)
            score += 1400;

        if (toAttackArea)
        {
            score += cardData.Atk * 15;
            score -= cardData.Cost * 20;
            score += cardData.EnSpawn * 10;

            if (earlyGame)
                score += Mathf.Max(0, 5 - cardData.Cost) * 40;

            if (lateGame)
                score += cardData.Cost * 25;
        }
        else
        {
            score += cardData.EnSpawn * 60;
            score += cardData.Atk * 6;

            if (specialSummon)
                score += 300;

            if (earlyGame)
                score += cardData.EnSpawn * 25;

            if (lateGame)
                score += cardData.Atk * 15;
        }

        return score;
    }

    private bool IsSpecialSummonCard(Tur.CardData cardData)
    {
        if (cardData == null)
            return false;

        if (cardData.Types != null)
        {
            foreach (var type in cardData.Types)
            {
                if (!string.IsNullOrWhiteSpace(type) && type.Trim().ToLowerInvariant().Contains("special"))
                    return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(cardData.SkillText))
        {
            var text = cardData.SkillText.ToLowerInvariant();
            if (text.Contains("special") || text.Contains("special summon") || text.Contains("特殊"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 執行AI攻擊邏輯
    /// </summary>
    private void ExecuteAIAttack(string attackerCardId)
    {
        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null || !cardEvent.TryGetCardById(attackerCardId, out var attackerData))
            return;

        // 獲取玩家場上的卡牌
        var playerAttackCards = GetPlayerAttackAreaCardIds();
        
        if (playerAttackCards.Count == 0)
        {
            // 沒有防禦卡，直接攻擊玩家HP
            int damage = attackerData.Atk;
            ReducePlayerHP(damage);
            Debug.Log($"[AI] Direct attack: {attackerCardId} deals {damage} damage to player HP");
            return;
        }

        Debug.Log($"[AI] Player attack area cards: {string.Join(", ", playerAttackCards)}");
        Debug.Log($"[AI] Attacker {attackerCardId} Atk={attackerData.Atk}");

        // 有防禦卡，選擇攻擊最弱的
        string targetCardId = SelectBestAIAttackTarget(attackerData, playerAttackCards, cardEvent);

        if (targetCardId != null)
        {
            Debug.Log($"[AI] Selected attack target {targetCardId} with negotiated choice");
            var targetGO = FindCardGameObject(targetCardId, true);
            var attackerGO = FindCardGameObject(attackerCardId, false);
            Debug.Log($"[AI] Attack scene objects: attackerGO={(attackerGO != null ? attackerGO.name : "null")}, targetGO={(targetGO != null ? targetGO.name : "null")}");
            // 執行攻擊
            PerformAttack(attackerCardId, targetCardId, false); // AI攻擊玩家
        }
        else
        {
            Debug.LogWarning($"[AI] Could not select a valid player target for attacker {attackerCardId}");
        }
    }

    private bool HasPlayerGuardTargets(List<string> playerAttackCards, CardEvent cardEvent, out List<string> guardTargets)
    {
        guardTargets = new List<string>();
        if (playerAttackCards == null || playerAttackCards.Count == 0 || cardEvent == null)
            return false;

        foreach (var cardId in playerAttackCards)
        {
            if (!cardEvent.TryGetCardById(cardId, out var cardData) || cardData == null)
                continue;

            if (!string.IsNullOrWhiteSpace(cardData.SkillText) && cardData.SkillText.ToLowerInvariant().Contains("[guard]"))
                guardTargets.Add(cardId);
        }

        return guardTargets.Count > 0;
    }

    private string SelectBestAIAttackTarget(Tur.CardData attackerData, List<string> playerAttackCards, CardEvent cardEvent)
    {
        if (attackerData == null || playerAttackCards == null || playerAttackCards.Count == 0 || cardEvent == null)
            return null;

        if (HasPlayerGuardTargets(playerAttackCards, cardEvent, out var guardTargets) && guardTargets.Count > 0)
            playerAttackCards = guardTargets;

        string bestTargetId = null;
        int bestScore = int.MinValue;

        foreach (var defenderId in playerAttackCards)
        {
            if (!cardEvent.TryGetCardById(defenderId, out var defenderData) || defenderData == null)
                continue;

            int displayedAtk = GetPlayerAttackWithBuff(defenderId, defenderData.Atk);
            int score = 0;
            bool canKill = attackerData.Atk >= defenderData.Def;
            int overkill = attackerData.Atk - defenderData.Def;

            if (canKill)
            {
                score += 1600;
                score += System.Math.Max(0, overkill) * 20;
                score += displayedAtk * 10;
            }
            else
            {
                score -= (defenderData.Def - attackerData.Atk) * 12;
                score += displayedAtk * 5;
            }

            if (!string.IsNullOrWhiteSpace(defenderData.SkillText) && defenderData.SkillText.ToLowerInvariant().Contains("[guard]"))
                score += 350;

            if (defenderData.IsAce)
                score += 300;

            if (defenderData.IsEvent)
                score -= 150;

            score += defenderData.Cost * 8;
            score += defenderData.Atk * 4;
            score -= defenderData.Def * 2;

            if (score > bestScore)
            {
                bestScore = score;
                bestTargetId = defenderId;
            }
        }

        return bestTargetId;
    }

    private string SelectBestAIAttacker(List<string> existingAttackCards, List<string> playerAttackCards, CardEvent cardEvent)
    {
        if (existingAttackCards == null || existingAttackCards.Count == 0 || cardEvent == null)
            return null;

        bool hasGuardTargets = HasPlayerGuardTargets(playerAttackCards, cardEvent, out var guardTargets);
        string bestId = null;
        int bestScore = int.MinValue;

        foreach (var cardId in existingAttackCards)
        {
            if (!cardEvent.TryGetCardById(cardId, out var cardData) || cardData == null)
                continue;

            int score = cardData.Atk * 20;
            if (playerAttackCards != null && playerAttackCards.Count > 0)
            {
                bool hasKillableTarget = false;
                int bestPenalty = int.MaxValue;

                foreach (var defenderId in hasGuardTargets ? guardTargets : playerAttackCards)
                {
                    if (!cardEvent.TryGetCardById(defenderId, out var defenderData) || defenderData == null)
                        continue;

                    int diff = defenderData.Def - cardData.Atk;
                    if (diff <= 0)
                    {
                        hasKillableTarget = true;
                        bestPenalty = System.Math.Min(bestPenalty, System.Math.Abs(diff));
                    }
                }

                if (hasKillableTarget)
                {
                    score += 1400;
                    score += System.Math.Max(0, 30 - bestPenalty) * 10;
                }
                else
                {
                    score -= 20;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestId = cardId;
            }
        }

        return bestId;
    }

    /// <summary>
    /// 執行攻擊
    /// </summary>
    private void PerformAttack(string attackerCardId, string defenderCardId, bool isPlayerAttacking)
    {
        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null)
            return;

        if (!cardEvent.TryGetCardById(attackerCardId, out var attackerData) ||
            !cardEvent.TryGetCardById(defenderCardId, out var defenderData))
        {
            Debug.LogWarning($"[GamePlay] PerformAttack failed: card data missing attacker={attackerCardId} defender={defenderCardId}");
            return;
        }

        var attackerGO = FindCardGameObject(attackerCardId, isPlayerAttacking);
        var defenderGO = FindCardGameObject(defenderCardId, !isPlayerAttacking);
        var attackerStatus = attackerGO != null ? EnemyStatusLocator.CoerceStatus(EnemyStatusLocator.FindStatusBehaviourFrom(attackerGO)) : null;
        var defenderStatus = defenderGO != null ? EnemyStatusLocator.CoerceStatus(EnemyStatusLocator.FindStatusBehaviourFrom(defenderGO)) : null;

        Debug.Log($"[GamePlay] PerformAttack lookup: attackerGO={(attackerGO != null ? attackerGO.name : "null")}, attackerStatus={(attackerStatus != null ? "found" : "null")}, defenderGO={(defenderGO != null ? defenderGO.name : "null")}, defenderStatus={(defenderStatus != null ? "found" : "null")}");

        if (defenderStatus != null)
        {
            Debug.Log($"[GamePlay] AI direct attack resolution: attacker={attackerCardId}, defender={defenderCardId}, damage={attackerData.Atk}");
            Debug.Log($"[GamePlay] Defender runtime status: state={defenderStatus.State}, attackStat={defenderStatus.AttackStat}, defenseStat={defenderStatus.DefenseStat}, hp={defenderStatus.HP}, guardHP={defenderStatus.FrontGuardHP}");
            var result = AttackLogic.PerformAttack(defenderStatus, attackerData.Atk, true);

            if (result.attackerDestroyed)
            {
                Debug.Log($"[GamePlay] AI attacker destroyed by combat: {attackerCardId}");
                if (attackerGO != null)
                {
                    if (attackerStatus != null)
                    {
                        attackerStatus.FrontGuardHP = 0;
                    }
                    else
                    {
                        TrySendCardGameObjectToAIDiscard(attackerGO);
                        Destroy(attackerGO);
                    }
                }
                aiAttackArea.Remove(attackerCardId);
            }

            if (result.guardDestroyed)
            {
                Debug.Log($"[GamePlay] AI defender destroyed by combat: {defenderCardId}");
                if (defenderGO != null && defenderStatus == null)
                {
                    TrySendCardGameObjectToPlayerDiscard(defenderGO);
                    Destroy(defenderGO);
                }
                playerAttackArea.Remove(defenderCardId);
            }
            return;
        }

        if (defenderGO != null)
        {
            Debug.Log($"[GamePlay] AI fallback attack: defender exists but no status component for {defenderCardId}, destroying object anyway");
            TrySendCardGameObjectToPlayerDiscard(defenderGO);
            if (playerAttackArea.Contains(defenderCardId))
                playerAttackArea.Remove(defenderCardId);
            Destroy(defenderGO);
            return;
        }

        Debug.Log($"[GamePlay] AI fallback attack: no defender GameObject found for {defenderCardId}, removing from player board");
        if (playerAttackArea.Contains(defenderCardId))
            playerAttackArea.Remove(defenderCardId);
        AddCardToPlayerDiscard(defenderCardId);
        return;

        Debug.LogWarning($"[GamePlay] PerformAttack could not resolve defender status for {defenderCardId}");
    }

    /// <summary>
    /// 根據卡牌ID找到場上的GameObject（簡化實現）
    /// </summary>
    private GameObject FindCardGameObject(string cardId, bool isPlayerCard)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        GameObject found = FindCardGameObjectByComponent<CardIdentity>(identity => string.Equals(identity.Id, cardId, System.StringComparison.OrdinalIgnoreCase), isPlayerCard);
        if (found != null)
            return found;

        found = FindCardGameObjectByComponent<global::CardData>(cardData => string.Equals(cardData.id, cardId, System.StringComparison.OrdinalIgnoreCase), isPlayerCard);
        if (found != null)
            return found;

        found = FindCardGameObjectByComponent<SimpleCardData>(simpleCard => string.Equals(simpleCard.cardId, cardId, System.StringComparison.OrdinalIgnoreCase), isPlayerCard);
        return found;
    }

    private GameObject FindCardGameObjectByComponent<T>(System.Func<T, bool> predicate, bool isPlayerCard) where T : Component
    {
        foreach (var component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || component.gameObject == null)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            if (!predicate(component))
                continue;

            bool ownedByPlayer = IsCardOwnedByPlayer(component.gameObject);
            if (ownedByPlayer != isPlayerCard)
                continue;

            return component.gameObject;
        }

        return null;
    }

    private bool IsCardOwnedByPlayer(GameObject cardGO)
    {
        if (cardGO == null)
            return false;

        if (IsPlayerOwnedArea(cardGO.transform))
            return true;

        return false;
    }

    /// <summary>
    /// 檢查AI是否能負擔卡牌的召喚成本
    /// </summary>
    private bool CanAffordCard(Tur.CardData cardData)
    {
        if (cardData == null || AIenergy == null)
            return false;

        // 簡化檢查：只要能量總數 >= 成本
        return AIenergy.Count >= cardData.Cost;
    }

    /// <summary>
    /// 為卡牌消耗能量
    /// </summary>
    private void ConsumeEnergyForCard(CardEvent cardEvent, string cardId)
    {
        if (!cardEvent.TryGetCardById(cardId, out var cardData) || cardData == null)
            return;

        int cost = cardData.Cost;
        for (int i = 0; i < cost && AIenergy.Count > 0; i++)
        {
            AIenergy.RemoveAt(0); // 移除第一個能量
        }
        
        // 通知能量更新
        OnPlayerEnergyUpdated?.Invoke(AIenergy); // 注意：這裡用的是Player的delegate，但AI沒有專門的
        Debug.Log($"[AI] Consumed {cost} energy for card {cardId}");
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

    // 效果用：從AI手牌棄置指定張數（目前採用由後往前的順序）
    public int DiscardCardsFromAIHand(int count, bool updateHandView = true)
    {
        if (count <= 0) return 0;
        if (aiHand == null)
        {
            Debug.LogWarning("[GamePlay] DiscardCardsFromAIHand failed: aiHand is null", this);
            return 0;
        }

        aiHand.LoadHand();
        if (aiHand.Hand == null || aiHand.Hand.Count == 0)
        {
            Debug.Log("[GamePlay] DiscardCardsFromAIHand: hand is empty", this);
            return 0;
        }

        int discarded = 0;
        for (int i = aiHand.Hand.Count - 1; i >= 0 && discarded < count; i--)
        {
            string id = aiHand.Hand[i];
            aiHand.Hand.RemoveAt(i);
            AddCardToAIDiscard(id);
            discarded++;

            if (updateHandView)
                RemoveCardObjectFromHand(aiHandController, id);
        }

        aiHand.SaveHand();
        if (aiHandController != null)
            aiHandController.RefreshUIHandRecord();

        Debug.Log($"[GamePlay] DiscardCardsFromAIHand: requested={count}, discarded={discarded}", this);
        return discarded;
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
    /// 檢查玩家是否擁有足夠的指定顏色 Energy。
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

    /// <summary>
    /// 檢查 AI 是否擁有足夠的指定顏色 Energy。
    /// </summary>
    public bool CanPayAIEnergyByColor(string color, int count)
    {
        if (count <= 0) return true;
        if (AIenergy == null) return false;
        int has = 0;
        foreach (var e in AIenergy)
            if (string.Equals(e, color, System.StringComparison.OrdinalIgnoreCase)) has++;
        return has >= count;
    }

    /// <summary>
    /// 嘗試消耗 AI 指定數量和顏色的能量；若不足或無該顏色則回傳 false，否則消耗並回傳 true
    /// </summary>
    public bool TryConsumeAIEnergyByColor(string requiredColor, int costAmount)
    {
        if (AIenergy == null || AIenergy.Count < costAmount)
        {
            Debug.LogWarning($"[GamePlay] AI energy insufficient: required {costAmount}, available {(AIenergy != null ? AIenergy.Count : 0)}", this);
            return false;
        }

        // 計算是否有足夠的該顏色能量
        int colorCount = 0;
        foreach (var e in AIenergy)
        {
            if (string.Equals(e, requiredColor, System.StringComparison.OrdinalIgnoreCase)) colorCount++;
        }

        if (colorCount < costAmount)
        {
            Debug.LogWarning($"[GamePlay] AI energy color mismatch: need {costAmount}x '{requiredColor}', have {colorCount}", this);
            return false;
        }

        // 消耗該顏色的能量
        int consumed = 0;
        for (int i = AIenergy.Count - 1; i >= 0 && consumed < costAmount; i--)
        {
            if (string.Equals(AIenergy[i], requiredColor, System.StringComparison.OrdinalIgnoreCase))
            {
                AIenergy.RemoveAt(i);
                consumed++;
            }
        }

        Debug.Log($"[GamePlay] AI consumed {consumed}x '{requiredColor}' energy. Remaining: [{string.Join(", ", AIenergy)}]", this);
        return true;
    }

    /// <summary>
    /// 檢查 AI 是否擁有足夠的 Core 卡數量。
    /// </summary>
    public bool CanPayAICoreCount(int count)
    {
        if (count <= 0) return true;
        return aiCoreArea != null && aiCoreArea.Count >= count;
    }

    /// <summary>
    /// 從 AI aiCoreArea 移除 count 張 Core 卡（從末尾開始），並觸發更新事件。
    /// </summary>
    public bool TryConsumeAICoreCount(int count)
    {
        if (count <= 0) return true;
        if (aiCoreArea == null || aiCoreArea.Count < count)
        {
            Debug.LogWarning($"[GamePlay] AI Core 不足：需要 {count}，現有 {aiCoreArea?.Count ?? 0}");
            return false;
        }
        for (int i = 0; i < count; i++)
            aiCoreArea.RemoveAt(aiCoreArea.Count - 1);
        OnAICoreAreaUpdated?.Invoke(aiCoreArea);
        Debug.Log($"[GamePlay] AI 消耗 {count} Core。剩餘：{aiCoreArea.Count}");
        return true;
    }

    /// <summary>
    /// 獲取 AI Core 區卡片 ID 列表
    /// </summary>
    public IReadOnlyList<string> GetAICoreArea()
    {
        return aiCoreArea ?? new List<string>();
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

    public bool TrySendCardGameObjectToAIDiscard(GameObject cardObject)
    {
        if (cardObject == null) return false;

        if (!IsOppTagged(cardObject))
        {
            Debug.Log($"[GamePlay] Skip discard for player card: {cardObject.name}", this);
            return false;
        }

        string cardId = ResolveCardIdFromGameObject(cardObject);
        if (string.IsNullOrWhiteSpace(cardId))
        {
            Debug.LogWarning($"[GamePlay] TrySendCardGameObjectToAIDiscard failed: cannot resolve card id from '{cardObject.name}'", this);
            return false;
        }

        AddCardToAIDiscard(cardId);
        return true;
    }

    private bool IsOppTagged(GameObject go)
    {
        if (go == null) return false;

        var t = go.transform;
        while (t != null)
        {
            var tag = t.tag;
            if (string.Equals(tag, "opp", System.StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// 在玩家牌堆中按名稱搜尋卡片，找到則移入手牌並返回 cardId。
    /// </summary>
    /// <summary>
    /// 記錄由效果直接從牌堆召喚進手牌的卡片 id。
    /// SimpleDropArea 放置時若 id 在此集合中，則消耗一次記錄並跳過 CommonSummon 觸發。
    /// </summary>
    public static readonly System.Collections.Generic.Dictionary<string, int> EffectSummonedCardIds
        = new System.Collections.Generic.Dictionary<string, int>();

    public string FindAndDrawCardFromDeckByName(string cardName, CardEvent cardEventRef)
    {
        if (deckData == null || handData == null || cardEventRef == null || string.IsNullOrWhiteSpace(cardName))
            return null;

        string targetKey = NormalizeCardNameForLookup(cardName);

        var deck = deckData.LoadDeck();
        for (int i = 0; i < deck.Count; i++)
        {
            string id = deck[i];
            if (!cardEventRef.TryGetCardById(id, out var data) || data == null) continue;
            string candidateName = data.Name ?? string.Empty;
            bool matched = string.Equals(candidateName.Trim(), cardName.Trim(), System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeCardNameForLookup(candidateName), targetKey, System.StringComparison.OrdinalIgnoreCase);
            if (matched)
            {
                deck.RemoveAt(i);
                deckData.SaveDeck(deck);
                handData.AddCardId(id);
                if (handController != null)
                    handController.AddCardToHandById(id);

                // 標記此 id 下一次進場時不視為 CommonSummon
                EffectSummonedCardIds.TryGetValue(id, out int cnt);
                EffectSummonedCardIds[id] = cnt + 1;
                Debug.Log($"[GamePlay] FindAndDrawCardFromDeckByName: found '{cardName}' (id={id}, candidate='{candidateName}'), moved to hand (effectSummoned)", this);
                return id;
            }
        }
        Debug.Log($"[GamePlay] FindAndDrawCardFromDeckByName: '{cardName}' not found in deck", this);
        return null;
    }

    public string FindAndSummonCardFromDeckByName(string cardName, CardEvent cardEventRef)
    {
        string foundId = FindAndDrawCardFromDeckByName(cardName, cardEventRef);
        if (!string.IsNullOrWhiteSpace(foundId))
        {
            Debug.Log($"[GamePlay] FindAndSummonCardFromDeckByName: scheduling effect summon placement for {foundId}", this);
            StartChoosePlayerAttackAreaForEffectSummon(foundId);
        }
        return foundId;
    }

    public void StartChoosePlayerAttackAreaForEffectSummon(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return;

        StartCoroutine(ChoosePlayerAttackAreaForEffectSummonCoroutine(cardId));
    }

    private IEnumerator ChoosePlayerAttackAreaForEffectSummonCoroutine(string cardId)
    {
        var availableAreas = FindPlayerEmptyAttackAreas();
        if (availableAreas == null || availableAreas.Count == 0)
        {
            Debug.LogWarning($"[GamePlay] No empty player attack area available for effect summon {cardId}", this);
            yield break;
        }

        ClearEffectSummonHighlights();
        CreateEffectSummonHighlights(availableAreas);
        effectSummonSelectionTcs = new TaskCompletionSource<SimpleDropArea>();

        yield return new WaitUntil(() => effectSummonSelectionTcs.Task.IsCompleted);

        var chosenArea = effectSummonSelectionTcs.Task.Result;
        ClearEffectSummonHighlights();
        effectSummonSelectionTcs = null;

        if (chosenArea == null)
        {
            Debug.LogWarning($"[GamePlay] Effect summon placement cancelled for {cardId}", this);
            yield break;
        }

        if (!PlaceCardToAreaFromEffect(cardId, true, true))
        {
            Debug.LogWarning($"[GamePlay] Effect summon failed to place card {cardId} to player attack area", this);
            yield break;
        }

        if (!TrySpawnPlayerBoardCard(cardId, chosenArea))
        {
            Debug.LogWarning($"[GamePlay] Effect summon card spawn failed for {cardId}", this);
            yield break;
        }

        cardrunTime = cardrunTime ?? FindObjectOfType<CardrunTime>(true);
        cardrunTime?.TriggerCardEffect(cardId, CardEffectEvent.EventType.Placed);
        RefreshPlayerAttackAreaAttackValues();

        Debug.Log($"[GamePlay] Effect-summoned card {cardId} to attack area {chosenArea.name}", this);
    }

    private List<SimpleDropArea> FindPlayerEmptyAttackAreas()
    {
        var result = new List<SimpleDropArea>();
        foreach (var area in Resources.FindObjectsOfTypeAll<SimpleDropArea>())
        {
            if (area == null || !area.gameObject.scene.IsValid())
                continue;
            if (!area.IsAttackArea())
                continue;
            if (!IsPlayerOwnedArea(area.transform))
                continue;
            if (IsDropAreaOccupied(area))
                continue;
            result.Add(area);
        }
        return result;
    }

    private void CreateEffectSummonHighlights(List<SimpleDropArea> areas)
    {
        if (areas == null || areas.Count == 0)
            return;

        foreach (var area in areas)
        {
            if (area == null) continue;
            var root = area.contentRoot != null ? area.contentRoot : area.transform;
            var layer = new GameObject("EffectSummonHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            layer.transform.SetParent(root, false);
            layer.transform.SetAsLastSibling();

            var rt = layer.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var highlightImage = layer.GetComponent<Image>();
            highlightImage.color = new Color(0f, 1f, 0f, 0.25f);
            highlightImage.raycastTarget = true;

            var button = layer.GetComponent<Button>();
            var capturedArea = area;
            button.onClick.AddListener(() => OnEffectSummonAreaSelected(capturedArea));

            effectSummonTargetHighlights.Add(layer);
        }
    }

    private void ClearEffectSummonHighlights()
    {
        for (int i = effectSummonTargetHighlights.Count - 1; i >= 0; i--)
        {
            var go = effectSummonTargetHighlights[i];
            if (go != null)
                Destroy(go);
        }
        effectSummonTargetHighlights.Clear();
    }

    private void OnEffectSummonAreaSelected(SimpleDropArea area)
    {
        if (effectSummonSelectionTcs == null || effectSummonSelectionTcs.Task.IsCompleted)
            return;
        effectSummonSelectionTcs.TrySetResult(area);
    }

    public bool PlaceCardToAreaFromEffect(string cardId, bool isPlayer, bool toAttackArea)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return false;

        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null || !cardEvent.TryGetCardById(cardId, out var cardData))
            return false;

        if (isPlayer)
        {
            if (toAttackArea)
                playerAttackArea.Add(cardId);
            else
                playerBenchArea.Add(cardId);
        }
        else
        {
            if (toAttackArea)
                aiAttackArea.Add(cardId);
            else
                aiBenchArea.Add(cardId);
        }

        if (isPlayer)
        {
            handData.LoadHand();
            handData.Hand.Remove(cardId);
            handData.SaveHand();
            RemoveCardObjectFromHand(handController, cardId);
        }
        else
        {
            aiHand.LoadHand();
            aiHand.Hand.Remove(cardId);
            aiHand.SaveHand();
            RemoveCardObjectFromHand(aiHandController, cardId);
        }

        return true;
    }

    private bool TrySpawnPlayerBoardCard(string cardId, SimpleDropArea targetArea)
    {
        if (string.IsNullOrWhiteSpace(cardId) || targetArea == null)
            return false;

        var cardEvent = FindObjectOfType<CardEvent>(true);
        if (cardEvent == null)
            return false;

        var root = targetArea.contentRoot != null ? targetArea.contentRoot : targetArea.transform;
        var prefab = GetBoardCardPrefab(isPlayer: true);
        if (prefab == null)
        {
            Debug.LogWarning($"[GamePlay] TrySpawnPlayerBoardCard failed: no card prefab available for {cardId}", this);
            return false;
        }

        var go = Instantiate(prefab, root, false);
        if (go == null)
            return false;

        if (cardEvent.TryGetCardById(cardId, out var cardData) && cardData != null)
        {
            var cardView = go.GetComponent<global::CardData>();
            if (cardView != null)
            {
                cardView.overrideFromCode = true;
                cardView.InitializeFromData(cardData);
            }
            else
            {
                var fallbackView = go.GetComponent<CardData>();
                if (fallbackView != null)
                    fallbackView.SetCardId(cardId);
            }
        }
        else
        {
            var fallbackView = go.GetComponent<CardData>();
            if (fallbackView != null)
                fallbackView.SetCardId(cardId);
        }

        var simpleData = go.GetComponent<SimpleCardData>();
        if (simpleData != null)
            simpleData.cardId = cardId;

        var identity = go.GetComponent<CardIdentity>();
        if (identity != null)
            identity.Id = cardId;

        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
        }

        return true;
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

            Transform root = area.contentRoot != null ? area.contentRoot : area.transform;
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

    private static string NormalizeCardNameForLookup(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string s = raw.Trim().ToLowerInvariant();
        s = s.Replace("\"", string.Empty).Replace("'", string.Empty);
        s = s.Replace("-", " ").Replace("_", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    /// <summary>
    /// 將卡片 ID 添加到 AI 牌堆底部。
    /// </summary>
    public void AddCardToAIDeckBottom(string cardId)
    {
        if (aiDeck == null || string.IsNullOrEmpty(cardId)) return;
        var deck = aiDeck.LoadDeck();
        deck.Add(cardId);
        aiDeck.SaveDeck(deck);
        Debug.Log($"[GamePlay] AddCardToAIDeckBottom: {cardId}", this);
    }

    /// <summary>
    /// 消耗任意顏色的能量 amount 個（從能量池末端移除）。
    /// </summary>
    public bool TryConsumeAnyEnergy(int amount)
    {
        if (energy == null || amount <= 0) return true;
        int toRemove = System.Math.Min(amount, energy.Count);
        if (toRemove <= 0) return false;
        energy.RemoveRange(energy.Count - toRemove, toRemove);
        OnPlayerEnergyUpdated?.Invoke(energy);
        Debug.Log($"[GamePlay] TryConsumeAnyEnergy: removed {toRemove}", this);
        return toRemove >= amount;
    }

    /// <summary>
    /// 啟動「選擇對手攻擊區卡片→反彈到牌堆底＋本回合 ATK 加成」效果協程。
    /// </summary>
    public void StartChooseOpponentBounceEffect(string attackerCardId, CardEvent cardEventRef)
    {
        StartCoroutine(ChooseOpponentBounceCoroutine(attackerCardId, cardEventRef));
    }

    private IEnumerator ChooseOpponentBounceCoroutine(string attackerCardId, CardEvent cardEventRef)
    {
        var mgr = OpponentCardTargetManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[GamePlay] ChooseOpponentBounce: OpponentCardTargetManager not found in scene, using fallback auto target", this);

            if (!TryFindFallbackOpponentAttackTarget(out string fallbackCardId, out GameObject fallbackCardGO))
            {
                CardEffectTrace.Push("ChooseOpponentBounce: no opponent target (fallback)");
                yield break;
            }

            ApplyOpponentBounceResult(attackerCardId, fallbackCardId, fallbackCardGO, cardEventRef);
            yield break;
        }

        var task = mgr.RequestTargetAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        string chosenCardId = task.Result;
        if (string.IsNullOrEmpty(chosenCardId))
        {
            Debug.Log("[GamePlay] ChooseOpponentBounce: no card chosen, effect cancelled", this);
            CardEffectTrace.Push("ChooseOpponentBounce: cancelled (no target)");
            yield break;
        }

        var cardGO = mgr.GetLastChosenGameObject();
        ApplyOpponentBounceResult(attackerCardId, chosenCardId, cardGO, cardEventRef);
    }

    private void ApplyOpponentBounceResult(string attackerCardId, string chosenCardId, GameObject chosenCardGO, CardEvent cardEventRef)
    {
        int chosenCost = 0;
        if (cardEventRef != null && cardEventRef.TryGetCardById(chosenCardId, out var chosenData))
            chosenCost = chosenData?.Cost ?? 0;

        Debug.Log($"[GamePlay] ChooseOpponentBounce: chosen={chosenCardId}, cost={chosenCost}", this);

        // 消耗等同費用的能量
        if (chosenCost > 0)
            TryConsumeAnyEnergy(chosenCost);

        // 移動被選卡片到 AI 牌堆底部
        AddCardToAIDeckBottom(chosenCardId);

        // 銷毀場上的對手卡片 GameObject
        if (chosenCardGO != null)
            Destroy(chosenCardGO);

        // 本回合 ATK 加成
        if (chosenCost > 0)
            AddPlayerAttackBuff(attackerCardId, chosenCost);

        CardEffectTrace.Push($"ChooseOpponentBounce: bounced {chosenCardId} (cost={chosenCost}), ATK +{chosenCost}");
    }

    private bool TryFindFallbackOpponentAttackTarget(out string targetCardId, out GameObject targetCardGO)
    {
        targetCardId = null;
        targetCardGO = null;

        var areas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!IsLikelyOpponentArea(area.transform))
                continue;

            // 有 Opp tag 的格子即為對手攻擊格，排除明確標為 bench 的區域
            if (area.IsBenchArea() && !area.IsAttackArea())
                continue;

            var root = area.contentRoot != null ? area.contentRoot : area.transform;
            for (int c = 0; c < root.childCount; c++)
            {
                var child = root.GetChild(c);
                if (child == null)
                    continue;

                string id = ResolveCardIdFromGameObject(child.gameObject);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                targetCardId = id;
                targetCardGO = child.gameObject;
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelyOpponentArea(Transform t)
    {
        Transform cursor = t;
        while (cursor != null)
        {
            var tag = cursor.tag;
            if (string.Equals(tag, "Opp", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(tag, "Player", System.StringComparison.OrdinalIgnoreCase)) return false;

            string n = cursor.name != null ? cursor.name.ToLowerInvariant() : string.Empty;
            if (n.Contains("opp") || n.Contains("enemy") || n.Contains("ai")) return true;
            if (n.Contains("player")) return false;

            cursor = cursor.parent;
        }

        return IsUpperHalfOnScreen(t);
    }

    private static bool IsUpperHalfOnScreen(Transform t)
    {
        if (t == null)
            return false;

        Vector3 worldPos = t.position;
        var rt = t as RectTransform;
        if (rt != null)
            worldPos = rt.TransformPoint(rt.rect.center);

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
        if (screenPos == Vector3.zero)
            return worldPos.y > 0f;

        return screenPos.y >= (Screen.height * 0.5f);
    }

    /// <summary>
    /// 開始選擇對手卡片銷毀效果流程 (用於事件卡)
    /// </summary>
    public void StartChooseOpponentCardDestroyEffect()
    {
        var mgr = OpponentCardTargetManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[GamePlay] OpponentCardTargetManager not found in scene, StartChooseOpponentCardDestroyEffect skipped");
            return;
        }

        StartCoroutine(ChooseOpponentCardDestroyCoroutine());
    }

    private IEnumerator ChooseOpponentCardDestroyCoroutine()
    {
        var mgr = OpponentCardTargetManager.Instance;
        if (mgr == null) yield break;

        // 啟動對手卡片選擇 UI
        var selectionTask = mgr.RequestTargetAsync();
        
        // 等待玩家選擇對手卡片
        yield return new WaitUntil(() => selectionTask.IsCompleted);

        var selectedCard = selectionTask.Result;
        var selectedGO = mgr.GetLastChosenGameObject();

        if (!string.IsNullOrEmpty(selectedCard) && selectedGO != null)
        {
            Debug.Log($"[GamePlay] Destroying opponent card: {selectedCard}");
            Destroy(selectedGO);
            CardEffectTrace.Push($"Destroyed opponent card: {selectedCard}");
        }
        else
        {
            Debug.Log("[GamePlay] No opponent card selected for destroy effect");
            CardEffectTrace.Push("Destroy effect: no card selected");
        }
    }

    /// <summary>
    /// 開始洗牌抽牌效果 (用於事件卡 15)
    /// </summary>
    public void StartShuffleHandDrawEffect(int drawCount = 4)
    {
        StartCoroutine(ShuffleHandDrawCoroutine(drawCount));
    }

    private IEnumerator ShuffleHandDrawCoroutine(int drawCount)
    {
        // 玩家操作：將手牌洗入牌堆並抽取新卡
        if (handData != null)
        {
            // 讀取目前手牌
            handData.LoadHand();
            var handCards = new List<string>(handData.Hand ?? new List<string>());

            // 清除手牌
            if (handController != null && handController.handContainer != null)
            {
                for (int i = handController.handContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(handController.handContainer.GetChild(i).gameObject);
                }
                handController.handCardTransforms.Clear();
            }

            handData.ClearHand();

            // 把手牌加回牌堆
            deckData.backToDeck(handCards);
            deckData.ShuffleDeck();

            yield return new WaitForSeconds(0.5f);

            // 抽取新卡
            var drawn = deckData.drawCard(handData, drawCount);
            if (handController != null && drawn != null)
            {
                foreach (var id in drawn)
                {
                    handController.AddCardToHandById(id);
                }
            }

            Debug.Log($"[GamePlay] Shuffle hand draw: returned {handCards.Count} cards, drew {drawn.Count} new cards");
            CardEffectTrace.Push($"Shuffle hand draw: returned={handCards.Count}, drew={drawn.Count}");
        }

        // 對手操作：同樣洗牌抽牌
        if (aiHand != null && aiDeck != null)
        {
            aiHand.LoadHand();
            var aiHandCards = new List<string>(aiHand.Hand ?? new List<string>());

            aiHand.ClearHand();
            aiDeck.backToDeck(aiHandCards);
            aiDeck.ShuffleDeck();

            var aiDrawn = aiDeck.drawCard(aiHand, drawCount);
            if (aiHandController != null && aiDrawn != null)
            {
                foreach (var id in aiDrawn)
                {
                    aiHandController.AddCardToHandById(id);
                }
            }

            Debug.Log($"[GamePlay] AI Shuffle hand draw: returned {aiHandCards.Count} cards, drew {aiDrawn.Count} new cards");
        }
    }
}
