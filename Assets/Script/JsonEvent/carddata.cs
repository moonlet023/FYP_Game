using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using Tur;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;


public class CardData : MonoBehaviour
{
   
    public String id;
    public String cardName;
    public String type;
    public int ActNum;
    public String skillText;
    public int Atk;
    public int Def;
    public string imagePath;
    private ReadJson json;

    private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    // 若為程式提供資料，避免在 Start() 讀檔覆蓋
    public bool overrideFromCode = false;

    public TextMeshProUGUI nameText;
    public TextMeshProUGUI skillsText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public RawImage cardRawImage;
    public Image cardSpriteImage;
    public String cardImage;
    public Button Act1;
    public Button Act2;


    void Start()
    {
        if (!overrideFromCode)
        {
            json = new ReadJson();
            // 設定欲讀取的 JSON 檔案路徑（請依你的專案路徑調整）
            var jsonPath = Application.dataPath + "/json/card/card.json";
            json.SetPath(jsonPath);

            // 讀檔並套用到資料與 UI
            LoadFromJsonFile();
        }

        // 不論資料來源，更新 UI 顯示
        if (nameText) nameText.text = cardName;
        if (skillsText) skillsText.text = skillText;
        if (atkText) atkText.text = Atk.ToString();
        if (defText) defText.text = Def.ToString();

        if (Act1 != null)
        {
            Act1.onClick.RemoveListener(OnAct1Clicked);
            Act1.onClick.AddListener(OnAct1Clicked);
        }

        EnsureImageTargets();
        if (!string.IsNullOrWhiteSpace(imagePath))
            ApplyImageByPath(imagePath);
    }

    // 從目前設定的檔案讀取並填入欄位 + 更新 UI
    public void LoadFromJsonFile()
    {
        if (json == null) return;
        string text;
        try { text = json.ReadJsonText(); }
        catch (Exception e) { Debug.LogError($"ReadJsonText failed: {e.Message}"); return; }

        try
        {
            // 移除 BOM（若存在）
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);

            var token = Newtonsoft.Json.Linq.JToken.Parse(text);
            Newtonsoft.Json.Linq.JObject cardObj = null;

            // 支援嵌套結構 {"id": {"01": {...}}}
            if (token is Newtonsoft.Json.Linq.JObject topObj && topObj["id"] is Newtonsoft.Json.Linq.JObject idContainer)
            {
                // 取第一張卡片，或按 id 取特定卡片
                // 此處簡化為取第一張；若需按 id 取特定卡片，可修改邏輯
                foreach (var prop in idContainer.Properties())
                {
                    if (prop.Value is Newtonsoft.Json.Linq.JObject obj)
                    {
                        cardObj = obj;
                        if (cardObj["id"] == null)
                            cardObj["id"] = prop.Name; // 設定 id
                        break;
                    }
                }
            }
            // 支援直接物件 {...}
            else if (token is Newtonsoft.Json.Linq.JObject obj)
            {
                cardObj = obj;
            }

            if (cardObj != null)
            {
                // 使用 JSON.NET 反序列化為 CardData
                var data = cardObj.ToObject<Tur.CardData>();
                if (data != null)
                {
                    id = data.Id ?? "";
                    cardName = data.Name ?? "";
                    type = data.Type ?? "";
                    ActNum = data.ActNum;
                    skillText = data.SkillText ?? "";
                    Atk = data.Atk;
                    Def = data.Def;
                    imagePath = data.ImagePath ?? "";
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LoadFromJsonFile JSON parse failed: {e.Message}");
        }

        // 更新 UI 顯示（若有指派）
        if (nameText) nameText.text = cardName;
        if (skillsText) skillsText.text = skillText;
        if (atkText) atkText.text = Atk.ToString();
        if (defText) defText.text = Def.ToString();
        if (!string.IsNullOrWhiteSpace(imagePath))
            ApplyImageByPath(imagePath);
    }

    private void OnAct1Clicked()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[CardData] Act1 clicked but card id is empty.");
            return;
        }

        var runtime = FindObjectOfType<CardrunTime>();
        if (runtime == null)
        {
            Debug.LogWarning($"[CardData] Act1 clicked but CardrunTime not found. id={id}");
            return;
        }

        runtime.TriggerCardEffect(id.Trim(), CardEffectEvent.EventType.Act);
    }

    // 由程式提供資料模型（Tur.CardData）初始化卡片並更新 UI
    public void InitializeFromData(Tur.CardData data)
    {
        if (data == null) return;
        id = data.Id;
        cardName = data.Name;
        type = data.Type;
        ActNum = data.ActNum;
        skillText = data.SkillText;
        Atk = data.Atk;
        Def = data.Def;
        imagePath = data.ImagePath;

        UpdateUITexts();
        if (!string.IsNullOrWhiteSpace(imagePath))
            ApplyImageByPath(imagePath);
    }

    // 僅指定卡片 ID（例如來自 deck.json），不覆蓋其他欄位
    public void SetCardId(string newId)
    {
        overrideFromCode = true;
        id = string.IsNullOrWhiteSpace(newId) ? string.Empty : newId.Trim();

        if (string.IsNullOrWhiteSpace(id))
            return;

        if (TryLoadCardById(id, out var data))
        {
            InitializeFromData(data);
            return;
        }

        Debug.LogWarning($"[CardData] SetCardId: cannot find card data for id={id}");
    }

    // 從目前欄位產出資料模型（Tur.CardData）
    public Tur.CardData ToDataModel()
    {
        return new Tur.CardData
        {
            Id = id,
            Name = cardName,
            Type = type,
            ActNum = ActNum,
            SkillText = skillText,
            Atk = Atk,
            Def = Def,
            ImagePath = imagePath
        };
    }

    // 將本卡資料複製到另一個 CardData（並可選擇更新 UI）
    public void CopyTo(CardData target, bool updateUI = true)
    {
        if (target == null) return;
        target.overrideFromCode = true;
        target.id = id;
        target.cardName = cardName;
        target.type = type;
        target.ActNum = ActNum;
        target.skillText = skillText;
        target.Atk = Atk;
        target.Def = Def;
        target.imagePath = imagePath;
        if (updateUI) target.UpdateUITexts();
        if (updateUI && !string.IsNullOrWhiteSpace(imagePath))
            target.ApplyImageByPath(imagePath);
    }

    // 更新 UI 文字顯示，使用目前欄位值
    public void UpdateUITexts()
    {
       
        if (nameText) nameText.text = cardName;
        if (skillsText) skillsText.text = skillText;
        if (atkText) atkText.text = Atk.ToString();
        if (defText) defText.text = Def.ToString();
    }

    private void EnsureImageTargets()
    {
        if (cardRawImage == null)
            cardRawImage = GetComponentInChildren<RawImage>(true);

        if (cardSpriteImage == null)
            cardSpriteImage = FindFirstNonButtonImage();
    }

    private Image FindFirstNonButtonImage()
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img == null) continue;
            // Skip any image used by Button visuals to avoid writing card art onto buttons.
            if (img.GetComponentInParent<Button>(true) != null) continue;
            return img;
        }

        return null;
    }

    private bool TryLoadCardById(string queryId, out Tur.CardData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(queryId))
            return false;

        var cardEvent = FindObjectOfType<CardEvent>();
        if (cardEvent != null && cardEvent.TryGetCardById(queryId, out data) && data != null)
            return true;

        return TryLoadCardByIdFromJson(queryId, out data);
    }

    private bool TryLoadCardByIdFromJson(string queryId, out Tur.CardData data)
    {
        data = null;
        string jsonPath = Path.Combine(Application.dataPath, "json/card/card.json");
        if (!File.Exists(jsonPath))
            return false;

        try
        {
            string text = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);

            var token = JToken.Parse(text);
            if (token is JObject topObj && topObj["id"] is JObject idContainer)
            {
                var cardToken = idContainer[queryId] as JObject;
                if (cardToken != null)
                {
                    var withId = new JObject(cardToken);
                    if (withId["id"] == null)
                        withId["id"] = queryId;

                    data = withId.ToObject<Tur.CardData>();
                    if (data != null && string.IsNullOrWhiteSpace(data.Id))
                        data.Id = queryId;
                    return data != null;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CardData] TryLoadCardByIdFromJson failed: {e.Message}");
        }

        return false;
    }

    private void ApplyImageByPath(string projectRelativePath)
    {
        EnsureImageTargets();

        if (string.IsNullOrWhiteSpace(projectRelativePath))
            return;

        string normalized = projectRelativePath.Replace('\\', '/').Trim();
        string fullPath = normalized.StartsWith("Assets/")
            ? Path.Combine(Application.dataPath, normalized.Substring("Assets/".Length))
            : Path.Combine(Application.dataPath, normalized);

        fullPath = fullPath.Replace('\\', '/');
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[CardData] Image not found: {fullPath}");
            return;
        }

        if (!textureCache.TryGetValue(fullPath, out var tex) || tex == null)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    Destroy(tex);
                    Debug.LogWarning($"[CardData] Failed to decode image: {fullPath}");
                    return;
                }
                textureCache[fullPath] = tex;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardData] ApplyImageByPath read failed: {e.Message}");
                return;
            }
        }

        if (cardRawImage != null)
            cardRawImage.texture = tex;

        if (cardSpriteImage != null)
        {
            if (!spriteCache.TryGetValue(fullPath, out var sprite) || sprite == null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                spriteCache[fullPath] = sprite;
            }
            cardSpriteImage.sprite = sprite;
        }
    }

    // 由當前卡片生成一個放置用的 placecard prefab，並套用相同資料與 id
    public GameObject InstantiatePlaceCard(GameObject placePrefab, Transform parent = null)
    {
        if (placePrefab == null) { Debug.LogWarning("InstantiatePlaceCard: placePrefab is null"); return null; }
        var go = Instantiate(placePrefab);
        if (parent != null)
        {
            var rt = parent as RectTransform;
            var childRT = go.GetComponent<RectTransform>();
            go.transform.SetParent(parent, worldPositionStays: false);
            if (rt != null && childRT != null)
                childRT.anchoredPosition = Vector2.zero;
            else
                go.transform.localPosition = Vector3.zero;
        }
        var target = go.GetComponent<CardData>();
        if (target != null) CopyTo(target, updateUI: true);
        var simple = go.GetComponent<SimpleCardData>();
        if (simple != null) simple.cardId = id;
        return go;
    }
}