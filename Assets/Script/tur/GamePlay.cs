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

        private bool playerCommonSummonUsedThisTurn = false; // 每回合 common summon 限制
    Button nextStageButton;

    private DeckData deckData;
    private DeckData aiDeck;
    private HandData handData;
    private HandData aiHand;

    [SerializeField] private Handcontroller handController;
    [SerializeField] private Handcontroller aiHandController;

    private List<string> playerCoreArea;
    private List<string> aiCoreArea;
    private List<string> energy;
    private List<string> AIenergy;

    // 供其他腳本讀取能量區資料
    public IReadOnlyList<string> Energy => energy;
    public IReadOnlyList<string> AIEnergy => AIenergy;

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

        InitializeGameState();
        setupCompleted = true;
    }

    // 牌庫為空時產生預設牌堆並儲存（測試用）
    private void EnsureDeckHasCards(DeckData deck, int minCards = 20)
    {
        var current = deck.LoadDeck();
        if (current.Count >= minCards) return;

        var defaultDeck = new List<string>();
        for (int i = 0; i < minCards; i++)
            defaultDeck.Add("01");
        deck.SaveDeck(defaultDeck);
        Debug.Log($"[GamePlay] Deck was empty/insufficient, generated {minCards} default cards.", this);
    }

    private async Task InitializeGameState()
    {
        playerCoreArea = new List<string>();
        aiCoreArea = new List<string>();
        energy = new List<string>();
        AIenergy = new List<string>();
        playerBenchPlacedThisTurn = false;

        // 初始化玩家牌堆並洗牌（若牌堆不足自動補牌）
        deckData = new DeckData();
        EnsureDeckHasCards(deckData);
        deckData.ShuffleDeck();
        handData = new HandData();

        if (isplaywithAI)
        {
            // 在玩家抽牌前先將牌堆快照複製給 AI，避免兩者共用同一個 JSON 路徑互相覆蓋
            aiDeck = new DeckData();
            aiDeck.SetPath("Assets/json/ai_deck.json");
            aiDeck.SaveDeck(new List<string>(deckData.LoadDeck())); // 複製同一套牌到 AI 路徑
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
        turnInProgress = false;
        gamestart = true;
    }
    

    // Update is called once per frame
    void Update()
    {
        // turnInProgress 防止每幀重複觸發；等待 EndPlayerTurn() 或 EndAITurn() 重置
        if (!gamestart || turnInProgress) return;
        if (!isplaywithAI) return;

        if (isplayerturn)
            StartPlayerTurn();
        else
            StartAITurn();
    }

    // 玩家回合開始：抽牌（資料 + 視覺）+ 頂牌進核心區，然後等待玩家輸入
    private async Task StartPlayerTurn()
    {
        turnInProgress = true;
        playerBenchPlacedThisTurn = false;
        playerCommonSummonUsedThisTurn = false; // 重置每回合 common summon 使用

        int generated = GenerateBenchEnergyAtTurnStart();

        Debug.Log("[GamePlay] Player turn start: bench & common summon placement reset.", this);
        Debug.Log($"[GamePlay] Player turn start energy: generated={generated}, current=[{string.Join(", ", energy ?? new List<string>())}]", this);
        if (YourTurnText != null)
        {
            YourTurnText.gameObject.SetActive(true);
            StartCoroutine(HideYourTurnTextAfterDelay(1f)); // 1秒後隱藏提示
        }

        await Task.Delay(1000); // 等待提示顯示一段時間（可調整）

        // 抽牌階段：從牌堆抽 1 張，同步更新資料層與視覺層
        var drawn = deckData.drawCard(handData, 1);
        foreach (var id in drawn)
            handController?.AddCardToHandById(id);

        // 頂牌進入核心區域
        string top = deckData.topCard();
        if (top != null)
            playerCoreArea.Add(top);
            Debug.Log($"core: {string.Join(", ", playerCoreArea)}", this);

        // 主要階段：等待玩家出牌，由 EndPlayerTurn() 結束回合
        endTurnButton.interactable = true; // 確保結束回合按鈕可用
        endTurnButton.onClick.AddListener(EndPlayerTurn);
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
                if (e == requiredColor) colorCount++;
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
                if (energy[i] == requiredColor)
                {
                    energy.RemoveAt(i);
                    consumed++;
                }
            }

            Debug.Log($"[GamePlay] Consumed {consumed}x '{requiredColor}' energy. Remaining: [{string.Join(", ", energy)}]", this);
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
                aiCoreArea.Add(top);
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

    private IEnumerator HideYourTurnTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (YourTurnText != null)
        {
            YourTurnText.gameObject.SetActive(false);
        }
    }
}
