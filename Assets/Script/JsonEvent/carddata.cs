using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Tur;
using UnityEngine.UI;


public class CardData : MonoBehaviour
{
   
    public String id;
    public String cardName;
    public String type;
    public int ActNum;
    public String skillText;
    public int Atk;
    public int Def;
    private ReadJson json;

    // 若為程式提供資料，避免在 Start() 讀檔覆蓋
    public bool overrideFromCode = false;

    public TextMeshProUGUI nameText;
    public TextMeshProUGUI skillsText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
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
    }

    // 從目前設定的檔案讀取並填入欄位 + 更新 UI
    public void LoadFromJsonFile()
    {
        if (json == null) return;
        string text;
        try { text = json.ReadJsonText(); }
        catch (Exception e) { Debug.LogError($"ReadJsonText failed: {e.Message}"); return; }

        // 依鍵名取值（此方法適用於『扁平 JSON 物件』）
        if (json.TryGetElementString(text, "id", out var idStr)) id = idStr;
        if (json.TryGetElementString(text, "name", out var nameStr)) cardName = nameStr;
        if (json.TryGetElementString(text, "type", out var typeStr)) type = typeStr;
        if (json.TryGetElement<int>(text, "Act Num", out var actNum)) ActNum = actNum;
        if (json.TryGetElementString(text, "skill Text", out var skStr)) skillText = skStr;
        if (json.TryGetElement<int>(text, "Atk", out var atk)) Atk = atk;
        if (json.TryGetElement<int>(text, "Def", out var def)) Def = def;

        // 更新 UI 顯示（若有指派）
        if (nameText) nameText.text = cardName;
        if (skillsText) skillsText.text = skillText;
        if (atkText) atkText.text = Atk.ToString();
        if (defText) defText.text = Def.ToString();
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

        UpdateUITexts();
    }

    // 僅指定卡片 ID（例如來自 deck.json），不覆蓋其他欄位
    public void SetCardId(string newId)
    {
        overrideFromCode = true;
        id = newId;
        // 若 UI 需要展示 id，可在此更新；目前僅記錄
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
            Def = Def
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
        if (updateUI) target.UpdateUITexts();
    }

    // 更新 UI 文字顯示，使用目前欄位值
    public void UpdateUITexts()
    {
       
        if (nameText) nameText.text = cardName;
        if (skillsText) skillsText.text = skillText;
        if (atkText) atkText.text = Atk.ToString();
        if (defText) defText.text = Def.ToString();
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