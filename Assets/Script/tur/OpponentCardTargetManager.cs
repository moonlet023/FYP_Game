using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 對手卡片目標選取管理器（Singleton）。
///
/// 使用方式：
///   var task = OpponentCardTargetManager.Instance.RequestTargetAsync();
///   yield return new WaitUntil(() => task.IsCompleted);
///   string chosenCardId = task.Result;
///
/// 啟動後，對手攻擊區所有卡片顯示橙色高亮覆蓋層，
/// 玩家點擊任意一張卡片後完成選取並回傳其 cardId。
///
/// Inspector 設定（可選）：
///   targetingPanel  → 提示面板根 GameObject（預設隱藏）
///   promptText      → 選擇提示文字
/// </summary>
public class OpponentCardTargetManager : MonoBehaviour
{
    public static OpponentCardTargetManager Instance { get; private set; }

    [Header("UI References (Optional)")]
    [SerializeField] private GameObject targetingPanel;
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("Highlight Style")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.55f, 0f, 0.45f);

    private TaskCompletionSource<string> _pendingTcs;
    private readonly List<(GameObject card, GameObject overlay)> _targetOverlays
        = new List<(GameObject, GameObject)>();
    private GameObject _lastChosenGO;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (targetingPanel != null) targetingPanel.SetActive(false);
    }

    // ---- Public API ----

    /// <summary>
    /// 顯示對手攻擊區目標選取 UI。
    /// 回傳選中的 cardId；若對手攻擊區無卡則立即回傳 null。
    /// </summary>
    public Task<string> RequestTargetAsync()
    {
        _pendingTcs = new TaskCompletionSource<string>();
        _lastChosenGO = null;

        var targets = FindOpponentAttackAreaCards();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[OppTargetMgr] RequestTargetAsync: NO cards found in opponent area → returning null immediately");
            _pendingTcs.SetResult(null);
            return _pendingTcs.Task;
        }

        Debug.Log($"[OppTargetMgr] RequestTargetAsync: {targets.Count} target(s) highlighted, waiting for player click...");
        ShowHighlights(targets);
        if (targetingPanel != null) targetingPanel.SetActive(true);
        if (promptText != null) promptText.text = "選擇一張對手攻擊區的卡片";
        return _pendingTcs.Task;
    }

    /// <summary>
    /// 取得最後一次選中的卡片 GameObject（用於銷毀場上物件）。
    /// </summary>
    public GameObject GetLastChosenGameObject() => _lastChosenGO;

    // ---- Private helpers ----

    private List<GameObject> FindOpponentAttackAreaCards()
    {
        var result = new List<GameObject>();
        var areas = Resources.FindObjectsOfTypeAll<SimpleDropArea>();
        Debug.Log($"[OppTargetMgr] Total SimpleDropAreas in scene: {areas.Length}");
        foreach (var area in areas)
        {
            if (area == null || !area.gameObject.scene.IsValid()) continue;

            string areaTag  = area.gameObject.tag;
            bool isOpponent = IsOpponentArea(area.transform);
            bool isBench    = area.IsBenchArea();
            var root        = area.contentRoot != null ? area.contentRoot : area.transform;
            int childCount  = root.childCount;

            Debug.Log($"[OppTargetMgr] Area='{area.name}' tag='{areaTag}' isOpp={isOpponent} isBench={isBench} children={childCount}");

            if (!isOpponent) continue;
            if (isBench && !area.IsAttackArea()) continue;

            for (int c = 0; c < childCount; c++)
            {
                var child = root.GetChild(c);
                if (child == null) continue;
                string childTag = child.gameObject.tag;
                Debug.Log($"[OppTargetMgr]   -> child='{child.name}' tag='{childTag}'");
                result.Add(child.gameObject);
            }
        }
        Debug.Log($"[OppTargetMgr] FindOpponentAttackAreaCards TOTAL: {result.Count}");
        return result;
    }

    private static bool IsOpponentArea(Transform t)
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

        // Fallback: 未標籤時用畫面位置判斷（上半部視為對手區）
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

    private void ShowHighlights(List<GameObject> targets)
    {
        foreach (var go in targets)
        {
            if (go == null) continue;

            var overlay = new GameObject("_TargetHighlight", typeof(RectTransform), typeof(Image), typeof(Button));
            overlay.transform.SetParent(go.transform, false);
            overlay.transform.SetAsLastSibling();

            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = overlay.GetComponent<Image>();
            img.color = highlightColor;
            img.raycastTarget = true;

            var captured = go;
            var btn = overlay.GetComponent<Button>();
            btn.onClick.AddListener(() => OnTargetChosen(captured));

            _targetOverlays.Add((go, overlay));
        }
    }

    private void OnTargetChosen(GameObject cardGO)
    {
        if (_pendingTcs == null || _pendingTcs.Task.IsCompleted) return;

        _lastChosenGO = cardGO;
        string cardId = ResolveCardId(cardGO);

        HideHighlights();
        _pendingTcs.SetResult(cardId);
        Debug.Log($"[OpponentCardTargetManager] Target chosen: go={cardGO.name}, cardId={cardId}");
    }

    private void HideHighlights()
    {
        if (targetingPanel != null) targetingPanel.SetActive(false);
        foreach (var (card, overlay) in _targetOverlays)
        {
            if (overlay != null) Destroy(overlay);
        }
        _targetOverlays.Clear();
    }

    private static string ResolveCardId(GameObject go)
    {
        if (go == null) return null;

        var simple = go.GetComponent<SimpleCardData>();
        if (simple != null && !string.IsNullOrWhiteSpace(simple.cardId))
            return simple.cardId.Trim();

        var cardData = go.GetComponent<CardData>();
        if (cardData != null && !string.IsNullOrWhiteSpace(cardData.id))
            return cardData.id.Trim();

        var identity = go.GetComponent<CardIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.Id))
            return identity.Id.Trim();

        return null;
    }
}
