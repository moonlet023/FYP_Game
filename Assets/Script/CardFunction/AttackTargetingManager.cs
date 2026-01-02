using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 目標選取管理器：集中管理攻擊者與目標點擊，不需在攻擊者卡牌上預先指定敵方參考。
public class AttackTargetingManager : MonoBehaviour
{
    public bool debugLogs = true;
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
                var go = new GameObject("AttackTargetingManager");
                _instance = go.AddComponent<AttackTargetingManager>();
                DontDestroyOnLoad(go);
                Debug.Log("AttackTargetingManager: created singleton instance");
            }
            return _instance;
        }
    }

    private leftRightClickCard _currentAttacker;
    private int _pendingDamage;

    public bool IsAwaitingTarget => _currentAttacker != null && _pendingDamage > 0;

    public void BeginAttack(leftRightClickCard attacker, int damage, bool verbose = false)
    {
        if (attacker == null)
        {
            if (debugLogs || verbose) Debug.LogWarning("AttackTargetingManager: BeginAttack ignored (attacker null)");
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
        var status = targetStatusBehaviour as IEnemyStatus;
        if (status == null)
        {
            if (debugLogs || verbose) Debug.LogWarning($"AttackTargetingManager: target '{targetStatusBehaviour.name}' does not implement IEnemyStatus");
            return empty;
        }
        if (debugLogs || verbose)
        {
            Debug.Log($"AttackTargetingManager: applying attack -> target={targetStatusBehaviour.name}, state={status.State}, hp={status.HP}, guardHP={status.FrontGuardHP}, damage={_pendingDamage}");
        }
        var res = AttackLogic.PerformAttack(status, _pendingDamage, debugLogs || verbose);
        _currentAttacker.ExitAttackMode();
        _currentAttacker = null;
        _pendingDamage = 0;
        if (debugLogs || verbose) Debug.Log($"AttackTargetingManager: applied; result guardApplied={res.damageAppliedToGuard}, overflow={res.overflowDamageToEnemy}, guardDestroyed={res.guardDestroyed}. Cleared attacker.");
        return res;
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
                if (debugLogs) Debug.Log("AttackTargetingManager: no GameObject picked under pointer");
                return;
            }

            if (debugLogs) Debug.Log($"AttackTargetingManager: picked '{picked.name}', tag='{picked.tag}'");
            if (useTagFilter && !allowAnyTarget)
            {
                if (!picked.CompareTag(targetTag))
                {
                    if (debugLogs) Debug.Log($"AttackTargetingManager: picked object does not match targetTag '{targetTag}'");
                    return;
                }
            }

            var statusMB = FindStatusBehaviourFromGO(picked);
            if (statusMB == null)
            {
                if (debugLogs) Debug.Log("AttackTargetingManager: picked object has no IEnemyStatus in self/children/parents");
                return;
            }

            TryApplyAttackToTarget(statusMB, true);
        }
    }

    private MonoBehaviour FindStatusBehaviourFromGO(GameObject go)
    {
        // self
        var mbs = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < mbs.Length; i++) if (mbs[i] is IEnemyStatus) return mbs[i];
        // children
        var childMbs = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < childMbs.Length; i++) if (childMbs[i] is IEnemyStatus) return childMbs[i];
        // parents
        var parentMbs = go.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parentMbs.Length; i++) if (parentMbs[i] is IEnemyStatus) return parentMbs[i];
        return null;
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
