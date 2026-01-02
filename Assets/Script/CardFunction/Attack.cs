using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
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
}

// 攻擊邏輯工具類：提供可重用的攻擊/溢傷處理
public static class AttackLogic
{
    public static AttackResolution ResolveAttackOnEnemy(IEnemyStatus status, int damage, bool debugLogs = false)
    {
        if (status == null)
        {
            if (debugLogs) Debug.LogWarning("AttackLogic: status is null; resolve aborted");
            return new AttackResolution();
        }

        int dmg = Mathf.Max(0, damage);
        int prevHP = Mathf.Max(0, status.HP);
        int prevGuardHP = Mathf.Max(0, status.FrontGuardHP);
        var prevState = status.State;

        // 依狀態選擇比較閾值（攻擊 vs 防禦）
        int threshold = prevState == EnemyBattleState.Attack
            ? Mathf.Max(0, status.AttackStat)
            : Mathf.Max(0, status.DefenseStat);

        bool cardDestroyed = dmg >= threshold;
        int overflow = Mathf.Max(0, dmg - threshold);
        int spillToEnemy = 0;

        if (debugLogs)
        {
            Debug.Log($"AttackLogic: begin -> dmg={dmg}, state={prevState}, threshold={threshold}, hp={prevHP}, guardHP={prevGuardHP}, cardDestroyed={cardDestroyed}, overflow={overflow}");
        }

        if (cardDestroyed)
        {
            // 將 FrontGuardHP 設為 0 以觸發 OnGuardDestroyed 與自動銷毀（若啟用）
            status.FrontGuardHP = 0;

            // 僅在攻擊狀態下，將溢傷轉到本體 HP
            if (overflow > 0 && prevState == EnemyBattleState.Attack)
            {
                spillToEnemy = overflow;
                status.HP = Mathf.Max(0, status.HP - spillToEnemy);
            }
            else if (overflow > 0 && prevState == EnemyBattleState.Defense)
            {
                if (debugLogs) Debug.Log("AttackLogic: overflow blocked (Defense)");
            }
        }

        var result = new AttackResolution
        {
            damageAppliedToGuard = Mathf.Min(dmg, threshold),
            overflowDamageToEnemy = spillToEnemy,
            guardDestroyed = cardDestroyed
        };

        if (debugLogs)
        {
            Debug.Log($"AttackLogic: end -> appliedToCard={result.damageAppliedToGuard}, overflowToEnemy={spillToEnemy}, destroyed={cardDestroyed}, newHP={status.HP}, newGuardHP={status.FrontGuardHP}");
        }
        return result;
    }

    public static AttackResolution PerformAttack(IEnemyStatus status, int damage, bool debugLogs = false)
        => ResolveAttackOnEnemy(status, damage, debugLogs);
}
