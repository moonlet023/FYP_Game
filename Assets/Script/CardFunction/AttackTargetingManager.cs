using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

// 目標選取管理器：集中管理攻擊者與目標點擊，不需在攻擊者卡牌上預先指定敵方參考。
public class AttackTargetingManager : MonoBehaviour
{
    public bool debugLogs = true;
    [Header("Attack Hint UI (Optional)")]
    [SerializeField] private TMPro.TextMeshProUGUI attackRuleHintText;
    [SerializeField] private float hintDisplaySeconds = 1.5f;
    [SerializeField] private string mustTargetEnemyCardHint = "場上有敵方卡片時，必須先攻擊敵方卡片";
    private Coroutine _hintCoroutine;

    [Header("Target Picking")]
    public string targetTag = "Opp";          // 對手卡牌的 tag（大小寫敏感，以 Unity 設定為準）
    public bool useTagFilter = true;           // 若為 true，僅拾取具備 targetTag 的物件
    public bool allowAnyTarget = false;        // 若為 true，忽略 tag 直接找 IEnemyStatus
    private static AttackTargetingManager _instance;
    public static AttackTargetingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AttackTargetingManager>(true);
                if (_instance == null)
                {
                    var go = new GameObject("AttackTargetingManager");
                    _instance = go.AddComponent<AttackTargetingManager>();
                    DontDestroyOnLoad(go);
                    Debug.Log("AttackTargetingManager: created singleton instance");
                }
            }
            return _instance;
        }
    }

    private leftRightClickCard _currentAttacker;
    private int _pendingDamage;
    private GamePlay _gamePlay;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        TryAutoBindHintText();
        if (attackRuleHintText != null)
            attackRuleHintText.gameObject.SetActive(false);
    }

    public bool IsAwaitingTarget => _currentAttacker != null;

    public void BeginAttack(leftRightClickCard attacker, int damage, bool verbose = false)
    {
        if (attacker == null)
        {
            if (debugLogs || verbose) Debug.LogWarning("AttackTargetingManager: BeginAttack ignored (attacker null)");
            return;
        }

        if (_gamePlay == null)
            _gamePlay = FindObjectOfType<GamePlay>();

        if (_gamePlay != null && !_gamePlay.CanPlayerAttackThisTurn(out var reason))
        {
            if (debugLogs || verbose) Debug.LogWarning($"AttackTargetingManager: attack denied -> {reason}");
            attacker.ExitAttackMode();
            return;
        }

        _currentAttacker = attacker;
        _pendingDamage = Mathf.Max(0, damage); // 允許0，負值視為0
        if (debugLogs || verbose) Debug.Log($"AttackTargetingManager: awaiting target -> attacker={attacker.name}, damage={_pendingDamage} (zero allowed)");
    }

    public AttackResolution TryApplyAttackToTarget(MonoBehaviour targetStatusBehaviour, bool verbose = false)
    {
        var empty = new AttackResolution();
        if (!IsAwaitingTarget)
        {
            if (debugLogs || verbose) Debug.Log("AttackTargetingManager: no attacker awaiting target");
            return empty;
        }
        if (targetStatusBehaviour == null)
        {
            if (debugLogs || verbose) Debug.LogWarning("AttackTargetingManager: targetStatusBehaviour null");
            return empty;
        }
        var status = EnemyStatusLocator.CoerceStatus(targetStatusBehaviour);
        if (status == null)
        {
            if (debugLogs || verbose) Debug.LogWarning($"AttackTargetingManager: target '{targetStatusBehaviour.name}' does not implement IEnemyStatus");
            return empty;
        }

        // 檢查 Guard 優先權：若對手有 Guard 卡，則目標卡片必須是 Guard
        int guardCount = CountOpponentGuardCardsOnBoard();
        if (guardCount > 0)
        {
            bool isTargetAGuard = false;
            var cardEvent = FindObjectOfType<CardEvent>();
            string targetCardId = ResolveCardIdFromGameObject(targetStatusBehaviour.gameObject);
            
            if (!string.IsNullOrWhiteSpace(targetCardId) && cardEvent != null 
                && cardEvent.TryGetCardById(targetCardId, out var targetData)
                && targetData != null && !string.IsNullOrEmpty(targetData.SkillText)
                && targetData.SkillText.ToLowerInvariant().Contains("[guard]"))
            {
                isTargetAGuard = true;
            }

            if (!isTargetAGuard)
            {
                ShowRuleHint("場上有 [Guard] 卡片，必須優先攻擊 Guard！");
                if (debugLogs || verbose) Debug.Log($"AttackTargetingManager: Guard priority enforcement - attack on non-Guard card blocked, {guardCount} Guard(s) present");
                return empty;
            }
        }

        if (debugLogs || verbose)
        {
            Debug.Log($"AttackTargetingManager: applying attack -> target={targetStatusBehaviour.name}, state={status.State}, hp={status.HP}, guardHP={status.FrontGuardHP}, damage={_pendingDamage}");
        }
        int attackValue = Mathf.Max(_pendingDamage, _currentAttacker != null ? _currentAttacker.selectedAttackDamage : 0);
        var res = AttackLogic.PerformAttack(status, attackValue, debugLogs || verbose);

        if (_currentAttacker != null)
        {
            _currentAttacker.MarkAttackUsedThisTurn();
        }

        if (res.attackerDestroyed)
        {
            DestroyAttackerCard(_currentAttacker);
        }

        _currentAttacker.ExitAttackMode();
        _currentAttacker = null;
        _pendingDamage = 0;
        if (debugLogs || verbose) Debug.Log($"AttackTargetingManager: applied; result guardApplied={res.damageAppliedToGuard}, overflow={res.overflowDamageToEnemy}, guardDestroyed={res.guardDestroyed}, attackerDestroyed={res.attackerDestroyed}. Cleared attacker.");
        return res;
    }

    private void DestroyAttackerCard(leftRightClickCard attacker)
    {
        if (attacker == null) return;

        var attackerStatusMB = EnemyStatusLocator.FindStatusBehaviourFrom(attacker.gameObject);
        var attackerStatus = EnemyStatusLocator.CoerceStatus(attackerStatusMB);
        if (attackerStatus != null)
        {
            attackerStatus.FrontGuardHP = 0;
            if (debugLogs) Debug.Log($"AttackTargetingManager: attacker '{attacker.name}' destroyed by battle (via FrontGuardHP=0)");
            return;
        }

        if (_gamePlay == null)
            _gamePlay = FindObjectOfType<GamePlay>();
        _gamePlay?.TrySendCardGameObjectToPlayerDiscard(attacker.gameObject);

        if (debugLogs) Debug.Log($"AttackTargetingManager: attacker '{attacker.name}' destroyed by battle (fallback Destroy)");
        Destroy(attacker.gameObject);
    }

    public void Cancel(bool verbose = false)
    {
        if (_currentAttacker != null)
        {
            _currentAttacker.ExitAttackMode();
        }
        _currentAttacker = null;
        _pendingDamage = 0;
        if (debugLogs || verbose) Debug.Log("AttackTargetingManager: selection cancelled");
    }

    // 當等待目標時，支援直接在場景中點擊（UI 或 2D/3D）來選取目標
    void Update()
    {
        if (!IsAwaitingTarget) return;
        if (Input.GetMouseButtonDown(0))
        {
            var picked = PickGameObjectUnderPointer();
            if (picked == null)
            {
                TryResolveDirectAttackOnNonCardClick("no GameObject picked under pointer");
                return;
            }

            var statusMB = FindStatusOnSelfOrParentsOnly(picked);
            if (statusMB == null)
            {
                TryResolveDirectAttackOnNonCardClick("picked object has no IEnemyStatus on self/parents");
                return;
            }

            if (debugLogs) Debug.Log($"AttackTargetingManager: picked '{picked.name}', tag='{picked.tag}', statusHost='{statusMB.name}', statusHostTag='{statusMB.tag}'");
            if (useTagFilter && !allowAnyTarget && !HasTargetTagInHierarchy(picked, statusMB.gameObject))
            {
                TryResolveDirectAttackOnNonCardClick($"picked object/status hierarchy does not match targetTag '{targetTag}'");
                return;
            }

            TryApplyAttackToTarget(statusMB, true);
        }
    }

    private void TryResolveDirectAttackOnNonCardClick(string reason)
    {
        int opponentBoardCount = CountOpponentBattleCardsOnBoard();
        if (opponentBoardCount > 0)
        {
            if (debugLogs)
                Debug.Log($"AttackTargetingManager: {reason}; direct attack blocked because opponent has {opponentBoardCount} card(s) on board");
            ShowRuleHint(mustTargetEnemyCardHint);
            return;
        }

        int attackValue = Mathf.Max(_pendingDamage, _currentAttacker != null ? _currentAttacker.selectedAttackDamage : 0);
        if (attackValue <= 0)
        {
            if (debugLogs)
                Debug.Log($"AttackTargetingManager: {reason}; direct attack skipped because attack value is {attackValue}");
            return;
        }

        if (_gamePlay == null)
            _gamePlay = FindObjectOfType<GamePlay>();

        if (_gamePlay != null)
        {
            _gamePlay.ReduceAIHP(attackValue);
            if (debugLogs)
                Debug.Log($"AttackTargetingManager: direct attack to AI HP for {attackValue} ({reason})");
        }
        else if (debugLogs)
        {
            Debug.LogWarning("AttackTargetingManager: direct attack failed because GamePlay was not found");
        }

        if (_currentAttacker != null)
        {
            _currentAttacker.MarkAttackUsedThisTurn();
            _currentAttacker.ExitAttackMode();
        }

        _currentAttacker = null;
        _pendingDamage = 0;
    }

    private void ShowRuleHint(string message)
    {
        TryAutoBindHintText();

        if (attackRuleHintText == null)
        {
            if (debugLogs)
                Debug.Log($"AttackTargetingManager: hint ui not assigned -> {message}");
            return;
        }

        attackRuleHintText.text = message;
        attackRuleHintText.gameObject.SetActive(true);

        if (_hintCoroutine != null)
            StopCoroutine(_hintCoroutine);
        _hintCoroutine = StartCoroutine(HideHintAfterDelay());
    }

    private IEnumerator HideHintAfterDelay()
    {
        float wait = Mathf.Max(0.1f, hintDisplaySeconds);
        yield return new WaitForSeconds(wait);

        if (attackRuleHintText != null)
            attackRuleHintText.gameObject.SetActive(false);
        _hintCoroutine = null;
    }

    private void TryAutoBindHintText()
    {
        if (attackRuleHintText != null)
            return;

        string[] preferredNames =
        {
            "AttackHintText",
            "AttackRuleHintText",
            "attackHint",
            "hintText"
        };

        for (int i = 0; i < preferredNames.Length && attackRuleHintText == null; i++)
        {
            var go = GameObject.Find(preferredNames[i]);
            if (go == null) continue;

            attackRuleHintText = go.GetComponent<TMPro.TextMeshProUGUI>();
            if (attackRuleHintText == null)
                attackRuleHintText = go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        }

        if (attackRuleHintText == null)
        {
            var allTexts = Resources.FindObjectsOfTypeAll<TMPro.TextMeshProUGUI>();
            for (int i = 0; i < allTexts.Length; i++)
            {
                var t = allTexts[i];
                if (t == null || !t.gameObject.scene.IsValid()) continue;

                string n = t.name != null ? t.name.ToLowerInvariant() : string.Empty;
                if (n.Contains("hint") && n.Contains("attack"))
                {
                    attackRuleHintText = t;
                    break;
                }
            }
        }
    }

    private int CountOpponentBattleCardsOnBoard()
    {
        int count = 0;
        var areas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!IsLikelyOpponentArea(area.transform))
                continue;

            if (area.IsBenchArea() && !area.IsAttackArea())
                continue;

            var root = area.contentRoot != null ? area.contentRoot : area.transform;
            for (int c = 0; c < root.childCount; c++)
            {
                var child = root.GetChild(c);
                if (child != null)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 統計對手場上有多少 Guard 卡片
    /// </summary>
    public int CountOpponentGuardCardsOnBoard()
    {
        int guardCount = 0;
        var cardEvent = FindObjectOfType<CardEvent>();
        if (cardEvent == null) return 0;

        var areas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null || !area.gameObject.scene.IsValid())
                continue;

            if (!IsLikelyOpponentArea(area.transform))
                continue;

            if (area.IsBenchArea() && !area.IsAttackArea())
                continue;

            var root = area.contentRoot != null ? area.contentRoot : area.transform;
            for (int c = 0; c < root.childCount; c++)
            {
                var child = root.GetChild(c);
                if (child == null) continue;

                // 嘗試解析卡片 ID
                string cardId = ResolveCardIdFromGameObject(child.gameObject);
                if (string.IsNullOrWhiteSpace(cardId)) continue;

                // 檢查卡片是否為 Guard
                if (cardEvent.TryGetCardById(cardId, out var cardData))
                {
                    if (cardData != null && !string.IsNullOrEmpty(cardData.SkillText) 
                        && cardData.SkillText.ToLowerInvariant().Contains("[guard]"))
                    {
                        guardCount++;
                    }
                }
            }
        }

        return guardCount;
    }

    private string ResolveCardIdFromGameObject(GameObject cardGO)
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

        return null;
    }

    private static MonoBehaviour FindStatusOnSelfOrParentsOnly(GameObject go)
    {
        if (go == null) return null;

        var self = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < self.Length; i++)
        {
            if (self[i] is IEnemyStatus) return self[i];
        }

        var parents = go.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i] is IEnemyStatus) return parents[i];
        }

        return null;
    }

    private static bool IsLikelyOpponentArea(Transform t)
    {
        Transform cursor = t;
        while (cursor != null)
        {
            if (cursor.CompareTag("Opp")) return true;
            if (cursor.CompareTag("Player")) return false;

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

    private bool HasTargetTagInHierarchy(GameObject picked, GameObject statusHost)
    {
        if (string.IsNullOrEmpty(targetTag)) return true;

        if (HasTagOnSelfOrParents(picked, targetTag)) return true;
        if (statusHost != null && HasTagOnSelfOrParents(statusHost, targetTag)) return true;
        return false;
    }

    private bool HasTagOnSelfOrParents(GameObject go, string tag)
    {
        var t = go != null ? go.transform : null;
        while (t != null)
        {
            if (t.CompareTag(tag)) return true;
            t = t.parent;
        }
        return false;
    }

    private GameObject PickGameObjectUnderPointer()
    {
        GameObject picked = null;
        // UI raycast
        if (EventSystem.current != null)
        {
            var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            if (results.Count > 0)
            {
                picked = results[0].gameObject;
                if (debugLogs) Debug.Log($"AttackTargetingManager: UI raycast picked '{picked.name}'");
            }
        }

        // 3D raycast
        if (picked == null && Camera.main != null)
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                picked = hit.collider.gameObject;
                if (debugLogs) Debug.Log($"AttackTargetingManager: 3D raycast picked '{picked.name}'");
            }
        }

        // 2D raycast
        if (picked == null && Camera.main != null)
        {
            var world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var hit2D = Physics2D.Raycast(world, Vector2.zero);
            if (hit2D.collider != null)
            {
                picked = hit2D.collider.gameObject;
                if (debugLogs) Debug.Log($"AttackTargetingManager: 2D raycast picked '{picked.name}'");
            }
        }

        return picked;
    }
}
