using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


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

    public TextMeshProUGUI nameText;
    public TextMeshProUGUI skillsText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;


    void Start()
    {
        json = new ReadJson();
        // 設定欲讀取的 JSON 檔案路徑（請依你的專案路徑調整）
        var jsonPath = Application.dataPath + "/json/card/card.json";
        json.SetPath(jsonPath);

        // 讀檔並套用到資料與 UI
        LoadFromJsonFile();

        nameText.text = cardName;
        skillsText.text = skillText;
        atkText.text = Atk.ToString();
        defText.text = Def.ToString();
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
        if (json.TryGetElement<int>(text, "ActNum", out var actNum)) ActNum = actNum;
        if (json.TryGetElementString(text, "skillText", out var skStr)) skillText = skStr;
        if (json.TryGetElement<int>(text, "Atk", out var atk)) Atk = atk;
        if (json.TryGetElement<int>(text, "Def", out var def)) Def = def;

        // 更新 UI 顯示（若有指派）
        if (nameText) nameText.text = cardName;
        if (skillsText) skillsText.text = skillText;
        if (atkText) atkText.text = Atk.ToString();
        if (defText) defText.text = Def.ToString();
    }
}