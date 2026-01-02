using UnityEngine;

// 將此腳本掛在敵方物件上（或任一管理器），
// 指定 EnemyStatusBehaviour 與各種 VFX/SFX，以便在狀態變更時呈現攻擊效果。
public class AttackEffects : MonoBehaviour
{
    [Header("Target")]
    public EnemyStatusBehaviour target;             // 目標敵方狀態
    public Transform vfxAnchor;                      // VFX 生成位置（預設用 target.transform）

    [Header("VFX Prefabs (Optional)")]
    public GameObject vfxGuardHit;                  // 作用於前排的打擊效果
    public GameObject vfxGuardBreak;                // 前排擊倒效果
    public GameObject vfxEnemyHit;                  // 本體受擊效果
    public GameObject vfxEnemyDeath;                // 本體死亡效果

    [Header("Audio (Optional)")]
    public AudioClip sfxGuardHit;
    public AudioClip sfxGuardBreak;
    public AudioClip sfxEnemyHit;
    public AudioClip sfxEnemyDeath;
    public AudioSource audioSource;                 // 若未指定，會嘗試使用自身 AudioSource 或自動建立

    public bool debugLogs = true;

    void Awake()
    {
        if (target == null)
            target = GetComponent<EnemyStatusBehaviour>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (vfxAnchor == null && target != null)
            vfxAnchor = target.transform;
    }

    void OnEnable()
    {
        if (target != null)
        {
            target.OnFrontGuardHPChanged += HandleFrontGuardHPChanged;
            target.OnGuardDestroyed += HandleGuardDestroyed;
            target.OnHPChanged += HandleHPChanged;
            target.OnEnemyDefeated += HandleEnemyDefeated;
        }
    }

    void OnDisable()
    {
        if (target != null)
        {
            target.OnFrontGuardHPChanged -= HandleFrontGuardHPChanged;
            target.OnGuardDestroyed -= HandleGuardDestroyed;
            target.OnHPChanged -= HandleHPChanged;
            target.OnEnemyDefeated -= HandleEnemyDefeated;
        }
    }

    private void HandleFrontGuardHPChanged(int newGuardHP)
    {
        // 當前排受擊（HP 下降）時播放 guard hit 效果
        if (debugLogs) Debug.Log($"AttackEffects: FrontGuardHP -> {newGuardHP}");
        SpawnVFX(vfxGuardHit);
        PlaySfx(sfxGuardHit);
    }

    private void HandleGuardDestroyed()
    {
        if (debugLogs) Debug.Log("AttackEffects: guard destroyed");
        SpawnVFX(vfxGuardBreak);
        PlaySfx(sfxGuardBreak);
    }

    private void HandleHPChanged(int newHP)
    {
        // 當本體受擊（HP 下降）時播放 enemy hit 效果
        if (debugLogs) Debug.Log($"AttackEffects: HP -> {newHP}");
        SpawnVFX(vfxEnemyHit);
        PlaySfx(sfxEnemyHit);
    }

    private void HandleEnemyDefeated()
    {
        if (debugLogs) Debug.Log("AttackEffects: enemy defeated");
        SpawnVFX(vfxEnemyDeath);
        PlaySfx(sfxEnemyDeath);
    }

    private void SpawnVFX(GameObject prefab)
    {
        if (prefab == null) return;
        Transform anchor = vfxAnchor != null ? vfxAnchor : transform;
        var go = Instantiate(prefab, anchor.position, anchor.rotation);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
