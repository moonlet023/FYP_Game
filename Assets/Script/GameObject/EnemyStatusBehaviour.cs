using System;
using UnityEngine;

// 基礎敵方狀態行為，實作 IEnemyStatus，並提供事件與方法，
// 以便未來與線上對戰（伺服器權威/狀態同步）整合。
public class EnemyStatusBehaviour : MonoBehaviour, IEnemyStatus
{
    [Header("Enemy Status")] 
    [SerializeField] private EnemyBattleState state = EnemyBattleState.Defense; // 初始為防禦
    [SerializeField] private int hp = 30;              // 敵方本體血量
    [SerializeField] private int frontGuardHP = 10;    // 前排卡/護盾血量
    [SerializeField] private int attackStat = 5;       // 卡片攻擊值（攻擊狀態下比較閾值）
    [SerializeField] private int defenseStat = 5;      // 卡片防禦值（防守狀態下比較閾值）
    public bool debugLogs = true;

    [Header("Auto Destroy When Knocked Down")]
    public bool destroyOnGuardDestroyed = true;   // 前排被擊倒時銷毀此卡片
    public bool destroyOnEnemyDefeated = true;    // 本體死亡時銷毀此卡片
    public float destroyDelaySeconds = 0f;        // 銷毀延遲（可用於播放擊倒/死亡特效）

    // IEnemyStatus 介面實作
    public EnemyBattleState State => state;

    public int HP
    {
        get => hp;
        set => SetHP(value);
    }

    public int FrontGuardHP
    {
        get => frontGuardHP;
        set => SetFrontGuardHP(value);
    }

    public int AttackStat
    {
        get => attackStat;
        set => attackStat = Mathf.Max(0, value);
    }

    public int DefenseStat
    {
        get => defenseStat;
        set => defenseStat = Mathf.Max(0, value);
    }

    // 事件：供 UI 或戰鬥系統監聽（非序列化）
    public event Action<int> OnHPChanged;              // 參數為新 HP
    public event Action<int> OnFrontGuardHPChanged;    // 參數為新 FrontGuardHP
    public event Action OnGuardDestroyed;              // 前排擊倒
    public event Action OnEnemyDefeated;               // 敵方本體死亡
    public event Action<EnemyBattleState> OnStateChanged; // 狀態切換事件

    // 設定狀態（供外部呼叫）
    public void SetState(EnemyBattleState newState)
    {
        if (state == newState) return;
        state = newState;
        OnStateChanged?.Invoke(state);
        if (debugLogs) Debug.Log($"EnemyStatusBehaviour: state -> {state}");
    }

    // 內部安全設定 HP
    private void SetHP(int newHP)
    {
        int clamped = Mathf.Max(0, newHP);
        if (hp == clamped) return;
        hp = clamped;
        OnHPChanged?.Invoke(hp);
        if (debugLogs) Debug.Log($"EnemyStatusBehaviour: HP -> {hp}");
        if (hp <= 0)
        {
            OnEnemyDefeated?.Invoke();
            if (debugLogs) Debug.Log("EnemyStatusBehaviour: enemy defeated");
            if (destroyOnEnemyDefeated)
            {
                if (debugLogs) Debug.Log("EnemyStatusBehaviour: destroying card (enemy defeated)");
                Destroy(gameObject, destroyDelaySeconds);
            }
        }
    }

    // 內部安全設定 FrontGuardHP
    private void SetFrontGuardHP(int newGuardHP)
    {
        int prev = frontGuardHP;
        int clamped = Mathf.Max(0, newGuardHP);
        if (frontGuardHP == clamped) return;
        frontGuardHP = clamped;
        OnFrontGuardHPChanged?.Invoke(frontGuardHP);
        if (debugLogs) Debug.Log($"EnemyStatusBehaviour: FrontGuardHP {prev} -> {frontGuardHP}");
        if (prev > 0 && frontGuardHP <= 0)
        {
            OnGuardDestroyed?.Invoke();
            if (debugLogs) Debug.Log("EnemyStatusBehaviour: guard destroyed");
            if (destroyOnGuardDestroyed)
            {
                if (debugLogs) Debug.Log("EnemyStatusBehaviour: destroying card (guard destroyed)");
                Destroy(gameObject, destroyDelaySeconds);
            }
        }
    }

    // 便捷方法：執行一次攻擊並返回結果（使用 AttackLogic）
    public AttackResolution ApplyIncomingAttack(int damage)
    {
        var result = AttackLogic.ResolveAttackOnEnemy(this, damage, debugLogs);
        return result;
    }

    // --- 未來線上對戰的同步掛勾 ---
    // 在採用 Mirror/Netcode 時，可由伺服器呼叫此方法同步狀態到客戶端。
    public void SyncFromServer(int syncedHP, int syncedGuardHP, EnemyBattleState syncedState)
    {
        // 勿直接改欄位，使用 setter 觸發事件，保持單一入口與一致性
        SetHP(syncedHP);
        SetFrontGuardHP(syncedGuardHP);
        SetState(syncedState);
    }

    // 範例：若採伺服器權威，可集中由伺服器端呼叫此入口進行攻擊，
    // 並透過 RPC/NetworkVariable 廣播結果；此處僅示意不直接依賴任何網路框架。
    public AttackResolution ServerAuthoritativeAttack(int damage)
    {
        // 伺服器端計算
        var res = AttackLogic.ResolveAttackOnEnemy(this, damage, debugLogs);
        // TODO: 廣播 res 給所有客戶端（依使用之網路框架實作）
        return res;
    }
}
