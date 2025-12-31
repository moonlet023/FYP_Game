using UnityEngine;
using UnityEngine.UI;

// 掛在「展示攻擊/防禦」的區域上：維護一個 Image 顯示當前模式圖示，並可控制透明度（含淡入）
public class SimpleAreaModeDisplay : MonoBehaviour
{
    [Header("Display Target")] 
    public RawImage iconImage;                 // 要顯示圖示的 Image（可為此物件或子物件）

    [Header("Textures")] 
    public Texture attackTexture;             // 預設攻擊貼圖
    public Texture defenseTexture;            // 防禦貼圖

    [Header("Default")] 
    public bool defaultDefense = false;     // 預設顯示防禦；不勾選則預設攻擊
    public bool autoInitOnAwake = true;     // 啟動時自動套用預設

    [Header("Opacity Control")] 
    public bool controlOpacity = true;      // 是否控制透明度
    public Graphic opacityTarget;           // 要調整透明度的 Graphic（若為空則使用 iconImage）
    public bool startHidden = true;         // 一開始為透明（待放置時再顯示）
    public float shownAlpha = 1f;           // 顯示時透明度
    public float hiddenAlpha = 0f;          // 隱藏時透明度
    public bool fadeOpacity = true;         // 是否淡入/淡出
    public float fadeDuration = 0.15f;      // 淡入/淡出時間

    [Header("Sorting & Order")]
    public bool bringToFrontOnShow = true;   // 顯示時將圖示移到同層最後（最上層）
    public bool useOwnCanvasSorting = false; // 若卡片在其他 Canvas 蓋過，啟用此項並設定 sortingOrder
    public int sortingOrder = 1000;          // 專用 Canvas 的排序序號（越大越上層）

    private Coroutine fadeCo;

    void Awake()
    {
        if (opacityTarget == null) opacityTarget = iconImage;

        if (autoInitOnAwake)
        {
            if (startHidden)
            {
                // 初始透明（不顯示），等待放置後再顯示預設圖
                SetAlphaImmediate(hiddenAlpha);
                if (iconImage != null) iconImage.enabled = false;
            }
            else
            {
                ShowDefault();
            }
        }
    }

    // 顯示預設模式（依 defaultDefense 決定），並（可選）淡入
    public void ShowDefault()
    {
        SetMode(defense: defaultDefense);
        Show();
    }

    // 外部可呼叫：切換到攻擊或防禦（不處理透明度）
    public void SetMode(bool defense)
    {
        if (iconImage == null)
        {
            Debug.LogWarning($"SimpleAreaModeDisplay: iconImage is null on {name}");
            return;
        }
        iconImage.enabled = true;
        if (!defense)
        {
            if (attackTexture != null)
                iconImage.texture = attackTexture;
            else
                Debug.LogWarning("SimpleAreaModeDisplay: attackTexture not set");
        }
        else
        {
            if (defenseTexture != null)
                iconImage.texture = defenseTexture;
            else
                Debug.LogWarning("SimpleAreaModeDisplay: defenseTexture not set");
        }
    }

    // 外部可呼叫：顯示並調整透明度（淡入）
    public void Show()
    {
        if (controlOpacity)
        {
            if (fadeOpacity)
                StartFade(shownAlpha);
            else
                SetAlphaImmediate(shownAlpha);
        }
        if (iconImage != null) iconImage.enabled = true;
        ApplySortingOnShow();
    }

    // 外部可呼叫：隱藏（淡出）
    public void Hide()
    {
        if (controlOpacity)
        {
            if (fadeOpacity)
                StartFade(hiddenAlpha);
            else
                SetAlphaImmediate(hiddenAlpha);
        }
        // 可選：同時關閉 icon 顯示
        if (iconImage != null && hiddenAlpha <= 0f) iconImage.enabled = false;
    }

    private void StartFade(float targetAlpha)
    {
        if (opacityTarget == null) return;
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeTo(targetAlpha, fadeDuration));
    }

    private void SetAlphaImmediate(float a)
    {
        if (opacityTarget == null) return;
        var c = opacityTarget.color;
        c.a = a;
        opacityTarget.color = c;
    }

    private System.Collections.IEnumerator FadeTo(float target, float duration)
    {
        if (opacityTarget == null) yield break;
        float t = 0f;
        var c = opacityTarget.color;
        float start = c.a;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            c.a = Mathf.Lerp(start, target, k);
            opacityTarget.color = c;
            yield return null;
        }
        c.a = target;
        opacityTarget.color = c;
        fadeCo = null;
    }

    private void ApplySortingOnShow()
    {
        if (iconImage == null) return;

        // 1) 同層次內移到最後，確保在兄弟節點上方
        if (bringToFrontOnShow)
        {
            iconImage.transform.SetAsLastSibling();
        }

        // 2) 若需要跨 Canvas 壓過其他 UI，使用獨立 Canvas 排序
        if (useOwnCanvasSorting)
        {
            var c = iconImage.GetComponent<Canvas>();
            if (c == null) c = iconImage.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = sortingOrder;
        }
    }
}

