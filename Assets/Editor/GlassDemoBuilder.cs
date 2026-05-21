using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class GlassDemoBuilder
{
    [MenuItem("Tools/Build Glass Demo")]
    public static void BuildDemo()
    {
        // create capture camera
        var camGO = new GameObject("GlassCaptureCam");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = ~ (1 << LayerMask.NameToLayer("UI"));
        cam.transform.position = new Vector3(0, 0, -10);

        // create Canvas
        var canvasGO = new GameObject("GlassDemoCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // create Border Image as parent (to provide rounded border look)
        var borderGO = new GameObject("GlassPanel_Border");
        borderGO.transform.SetParent(canvasGO.transform, false);
        var borderImg = borderGO.AddComponent<Image>();
        var borderRt = borderGO.GetComponent<RectTransform>();
        borderRt.sizeDelta = new Vector2(620, 720);
        borderRt.anchoredPosition = Vector2.zero;
        // use built-in sprite for UI background
        var bgSprite = (Sprite)Resources.GetBuiltinResource(typeof(Sprite), "UI/Skin/Background.psd");
        if (bgSprite != null) borderImg.sprite = bgSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(1f,1f,1f,0.08f);

        // create RawImage (glass panel) as child
        var rawGO = new GameObject("GlassPanel");
        rawGO.transform.SetParent(borderGO.transform, false);
        var raw = rawGO.AddComponent<RawImage>();
        var rt = rawGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600, 700);
        rt.anchoredPosition = Vector2.zero;

        // add GlassUI to raw (logic lives on RawImage)
        var glass = rawGO.AddComponent<GlassUI>();
        glass.captureCamera = cam;
        glass.downsample = 3;
        glass.iterations = 2;
        glass.tint = new Color(0.93f, 0.96f, 0.98f, 0.18f);

        // assign border image to glass
        glass.borderImage = borderImg;
        glass.borderColor = new Color(1f,1f,1f,0.10f);
        glass.borderThickness = 10f;

        // create shine image
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(rawGO.transform, false);
        var shine = shineGO.AddComponent<Image>();
        // assign builtin UISprite so it renders
        var sprite = (Sprite)Resources.GetBuiltinResource(typeof(Sprite), "UI/Skin/UISprite.psd");
        if (sprite != null) shine.sprite = sprite;
        shine.color = new Color(1f,1f,1f,0.25f);
        var srt = shineGO.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(100, 700);
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);

        glass.shineImage = shine;
        glass.shineSpeed = 0.6f;
        glass.shineRange = 1.6f;

        Selection.activeGameObject = rawGO;

        Debug.Log("Glass demo created. Assign a material with shader 'Hidden/SeparableBlur' to the GlassUI.blurMaterial if you want a custom material. Otherwise GlassUI will create one at runtime.");
    }
}
