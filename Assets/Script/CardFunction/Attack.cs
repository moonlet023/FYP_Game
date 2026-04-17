using UnityEngine;

public class Attack : MonoBehaviour
{
}

// --- Combat types & logic (extracted from card script) ---
// 敵方戰鬥狀態：Attack 允許溢傷進入本體；Defense 不允許
public enum EnemyBattleState { Attack, Defense }

// 敵方狀態介面：由敵方物件腳本實作
public interface IEnemyStatus
{
    EnemyBattleState State { get; }
    int HP { get; set; }
    int FrontGuardHP { get; set; } // 用於觸發卡片被破壞事件（設為 0）
    int AttackStat { get; set; }   // 卡片的攻擊值（作為攻擊狀態比較閾值）
    int DefenseStat { get; set; }  // 卡片的防禦值（作為防守狀態比較閾值）
}

// 攻擊結果資料結構
public struct AttackResolution
{
    public int damageAppliedToGuard;   // 作用於前排的傷害
    public int overflowDamageToEnemy;  // 溢傷到敵方本體的傷害（Defense 下為 0）
    public bool guardDestroyed;        // 前排是否被擊倒
    public bool attackerDestroyed;     // 攻擊方卡片是否被擊倒
}

// 攻擊邏輯工具類：提供可重用的攻擊/溢傷處理
public static class AttackLogic
{
    // 新規則：
    // - 目標為 Attack：比較雙方攻擊力
    //   attacker > defenderAtk => defender destroyed + overflow to enemy HP
    //   attacker < defenderAtk => attacker destroyed
    //   attacker == defenderAtk => both destroyed
    // - 目標為 Defense：維持原先以 defenseStat 作門檻，不做反擊破壞
    public static AttackResolution ResolveBattle(IEnemyStatus defender, int attackerAttack, bool debugLogs = false)
    {
        if (defender == null)
        {
            if (debugLogs) Debug.LogWarning("AttackLogic: defender is null; resolve aborted");
            return new AttackResolution();
        }

        int attacker = Mathf.Max(0, attackerAttack);
        int prevHP = Mathf.Max(0, defender.HP);
        int prevGuardHP = Mathf.Max(0, defender.FrontGuardHP);
        var state = defender.State;

        bool defenderDestroyed = false;
        bool attackerDestroyed = false;
        int appliedToGuard = 0;
        int overflowToEnemy = 0;

        if (state == EnemyBattleState.Attack)
        {
            int defenderAttack = Mathf.Max(0, defender.AttackStat);
            appliedToGuard = Mathf.Min(attacker, defenderAttack);

            if (attacker > defenderAttack)
            {
                defenderDestroyed = true;
                overflowToEnemy = attacker - defenderAttack;
            }
            else if (attacker < defenderAttack)
            {
                attackerDestroyed = true;
            }
            else
            {
                // equal
                defenderDestroyed = true;
                attackerDestroyed = true;
            }

            if (debugLogs)
            {
                Debug.Log($"AttackLogic: attack-vs-attack -> attacker={attacker}, defenderAtk={defenderAttack}, defenderDestroyed={defenderDestroyed}, attackerDestroyed={attackerDestroyed}, overflow={overflowToEnemy}, hp={prevHP}, guardHP={prevGuardHP}");
            }
        }
        else
        {
            int defenderDefense = Mathf.Max(0, defender.DefenseStat);
            appliedToGuard = Mathf.Min(attacker, defenderDefense);
            defenderDestroyed = attacker >= defenderDefense;

            if (debugLogs)
            {
                Debug.Log($"AttackLogic: attack-vs-defense -> attacker={attacker}, defenderDef={defenderDefense}, defenderDestroyed={defenderDestroyed}, hp={prevHP}, guardHP={prevGuardHP}");
            }
        }

        if (defenderDestroyed)
        {
            defender.FrontGuardHP = 0;
            if (overflowToEnemy > 0)
            {
                defender.HP = Mathf.Max(0, defender.HP - overflowToEnemy);
            }
        }

        return new AttackResolution
        {
            damageAppliedToGuard = appliedToGuard,
            overflowDamageToEnemy = overflowToEnemy,
            guardDestroyed = defenderDestroyed,
            attackerDestroyed = attackerDestroyed
        };
    }

    public static AttackResolution ResolveAttackOnEnemy(IEnemyStatus status, int damage, bool debugLogs = false)
        => ResolveBattle(status, damage, debugLogs);

    public static AttackResolution PerformAttack(IEnemyStatus status, int damage, bool debugLogs = false)
        => ResolveAttackOnEnemy(status, damage, debugLogs);
}

// 目標查找工具：統一 self/children/parents 的 IEnemyStatus 搜尋策略
public static class EnemyStatusLocator
{
    public static MonoBehaviour FindStatusBehaviourFrom(GameObject go)
    {
        if (go == null) return null;

        // self
        var self = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < self.Length; i++)
        {
            if (self[i] is IEnemyStatus) return self[i];
        }

        // children
        var children = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is IEnemyStatus) return children[i];
        }

        // parents
        var parents = go.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i] is IEnemyStatus) return parents[i];
        }

        return null;
    }

    public static IEnemyStatus CoerceStatus(MonoBehaviour statusBehaviour)
        => statusBehaviour as IEnemyStatus;
}
