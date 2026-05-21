using UnityEngine;
using UnityEngine.UI;

public class GlassUI : MonoBehaviour
{
    public Camera captureCamera;            // 指向一個用來截屏的 Camera（通常設定只渲染遊戲場景）
    public Material blurMaterial;           // 指向 SeparableBlur Shader 的 Material
    [Range(1,4)] public int downsample = 2; // 降採樣倍數（越大越快但越糊）
    [Range(0,4)] public int iterations = 2; // 模糊迴圈次數
    [Range(0.1f, 8f)] public float blurSize = 1f; // 模糊半徑倍率
    [Range(0f,1f)] public float blurBlend = 1f; // 0 = 原畫面, 1 = 完全模糊
    public Color tint = new Color(1f,1f,1f,0.25f); // 玻璃色與透明度

    [Header("Shine (optional)")]
    public Image shineImage;    // 一個子 Image 當高光（使用 Additive）
    public float shineSpeed = 1f;
    public float shineRange = 1.2f;
    
    [Header("Border (optional)")]
    public Image borderImage;   // 可設定為父 Image 或同層 Image，GlassUI 會根據 thickness 調整尺寸
    public Color borderColor = new Color(1f,1f,1f,0.12f);
    public float borderThickness = 8f;
    Material borderMaterial;

    RawImage raw;
    Image panelImage;
    Material panelMaterial;
    RenderTexture rtA, rtB;
    int lastW, lastH;

    void Awake()
    {
        raw = GetComponent<RawImage>();
        panelImage = GetComponent<Image>();

        if (raw != null)
        {
            raw.raycastTarget = true; // 可接事件
        }
        else if (panelImage != null)
        {
            panelImage.raycastTarget = true;
            panelImage.type = Image.Type.Simple;
            panelImage.sprite = null;
        }
    }

    void OnEnable()
    {
        EnsureRT();
    }

    void OnDisable()
    {
        ReleaseRT();
    }

    void EnsureRT()
    {
        int w = Mathf.Max(1, Screen.width / downsample);
        int h = Mathf.Max(1, Screen.height / downsample);
        if (rtA == null || lastW != w || lastH != h)
        {
            ReleaseRT();
            rtA = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
            rtA.Create();
            rtB = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
            rtB.Create();
            lastW = w; lastH = h;
        }
    }

    void ReleaseRT()
    {
        if (rtA != null) { rtA.Release(); Destroy(rtA); rtA = null; }
        if (rtB != null) { rtB.Release(); Destroy(rtB); rtB = null; }
    }

    void EnsurePanelMaterial()
    {
        if (panelMaterial == null)
        {
            if (panelImage != null)
            {
                panelMaterial = new Material(Shader.Find("UI/Default"));
                panelMaterial.mainTexture = rtB;
            }
        }
    }

    void LateUpdate()
    {
        if (captureCamera == null || (raw == null && panelImage == null)) return;

        // lazy-create material if missing
        if (blurMaterial == null)
        {
            Shader sh = Shader.Find("Hidden/SeparableBlur");
            if (sh != null) blurMaterial = new Material(sh);
        }
        if (blurMaterial == null) return;

        EnsureRT();

        // 抓畫面
        var prev = captureCamera.targetTexture;
        captureCamera.targetTexture = rtA;
        captureCamera.Render();
        captureCamera.targetTexture = prev;

        // 初始複製到 rtB
        Graphics.Blit(rtA, rtB);

        // 分離式橫/直向多次迭代
        for (int i = 0; i < iterations; i++)
        {
            float sz = blurSize * (1f + i * 0.5f); // 每次迭代略增半徑
            blurMaterial.SetFloat("_BlurSize", sz);
            blurMaterial.SetFloat("_Blend", blurBlend);

            // horizontal
            blurMaterial.SetVector("_Direction", new Vector4(1f, 0f, 0f, 0f));
            Graphics.Blit(rtB, rtA, blurMaterial);

            // vertical
            blurMaterial.SetVector("_Direction", new Vector4(0f, 1f, 0f, 0f));
            Graphics.Blit(rtA, rtB, blurMaterial);
        }

        if (raw != null)
        {
            raw.texture = rtB;
            raw.color = tint;
        }
        else if (panelImage != null)
        {
            EnsurePanelMaterial();
            if (panelMaterial != null)
            {
                panelMaterial.mainTexture = rtB;
            }
            panelImage.material = panelMaterial;
            panelImage.color = tint;
        }

        // 調整邊框顏色與材質參數（border 由 shader 繪製）
        if (borderImage != null)
        {
            // ensure material
            if (borderMaterial == null)
            {
                var sh = Shader.Find("UI/RectBorder");
                if (sh != null)
                {
                    borderMaterial = new Material(sh);
                    borderImage.material = borderMaterial;
                }
            }

            var panelRect = raw != null ? raw.rectTransform : panelImage.rectTransform;
            borderImage.rectTransform.sizeDelta = panelRect.sizeDelta;
            borderImage.rectTransform.anchoredPosition = panelRect.anchoredPosition;

            if (borderMaterial != null)
            {
                borderMaterial.SetColor("_Color", borderColor);
                borderMaterial.SetFloat("_Thickness", borderThickness);
                borderMaterial.SetVector("_RectSize", new Vector4(panelRect.sizeDelta.x, panelRect.sizeDelta.y,0,0));
            }
            else
            {
                // fallback: tint the image
                borderImage.color = borderColor;
            }
        }

        // 高光動態（如果有）
        if (shineImage != null)
        {
            var t = (Time.time * shineSpeed) % 1f;
            var rect = (raw.rectTransform.rect);
            float x = Mathf.Lerp(-rect.width * 0.5f * shineRange, rect.width * 0.5f * shineRange, Mathf.SmoothStep(0,1,t));
            shineImage.rectTransform.anchoredPosition = new Vector2(x, 0f);
            var c = shineImage.color; c.a = Mathf.Lerp(0f, 0.6f, Mathf.Abs(Mathf.Sin(Time.time * shineSpeed))); shineImage.color = c;
        }
    }
}
