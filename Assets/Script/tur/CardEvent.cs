using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Tur;

public class CardEvent : MonoBehaviour
{
    [System.Serializable]
    private class CardListWrapper
    {
        public List<Tur.CardData> cards = new List<Tur.CardData>();
    }

    [Header("Card JSON 設定")]
    public string cardJsonRelativePath = "json/card/card.json";

    private readonly Dictionary<string, Tur.CardData> cardLookup = new Dictionary<string, Tur.CardData>();
    private bool isLoaded;
    
    public bool TryGetCardById(string id, out Tur.CardData data)
    {
        data = null;
        if (string.IsNullOrEmpty(id)) return false;
        // 自我修復：若 isLoaded=true 但字典是空，代表載入狀態可能失真（常見於無 Domain Reload 或時序問題）
        if (!isLoaded || cardLookup.Count == 0)
            LoadCardDatabase();

        bool found = cardLookup.TryGetValue(id, out data);
        if (!found && cardLookup.Count == 0)
        {
            // 再次保底重載一次
            LoadCardDatabase();
            found = cardLookup.TryGetValue(id, out data);
        }

        if (!found)
        {
            string keyDump = cardLookup.Count == 0
                ? "(empty)"
                : string.Join(", ", System.Linq.Enumerable.Select(cardLookup.Keys, k => $"[{k}](len={k.Length})"));
            Debug.LogError($"[CardEvent] TryGetCardById MISS: query=[{id}](len={id.Length})  isLoaded={isLoaded}  dictCount={cardLookup.Count}  keys={keyDump}");
        }
        return found;
    }

    public Tur.CardData GetCardById(string id)
    {
        return TryGetCardById(id, out var data) ? data : null;
    }

    public void LoadCardDatabase()
    {
        cardLookup.Clear();
        isLoaded = false;

        var fullPath = Path.Combine(Application.dataPath, cardJsonRelativePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[CardEvent] LoadCardDatabase: 找不到卡片資料檔案 {fullPath}");
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CardEvent] LoadCardDatabase: 讀檔失敗 {e.Message}");
            return;
        }

        // 移除 BOM（UTF-8 BOM = \uFEFF）
        if (json.Length > 0 && json[0] == '\uFEFF')
            json = json.Substring(1);

        try
        {
            var token = Newtonsoft.Json.Linq.JToken.Parse(json);
            Newtonsoft.Json.Linq.JArray arr = null;

            // 支援四種格式：[] 陣列、{"id": {...}} ID 容器、{"cards":[...]} 包裝器、{...} 單物件
            if (token is Newtonsoft.Json.Linq.JArray directArr)
            {
                // 格式 1: 直接陣列 [...]
                arr = directArr;
            }
            else if (token is Newtonsoft.Json.Linq.JObject topObj)
            {
                // 嘗試格式 2: {"id": {"01": {...}, "02": {...}}}
                if (topObj["id"] is Newtonsoft.Json.Linq.JObject idContainer)
                {
                    arr = new Newtonsoft.Json.Linq.JArray();
                    foreach (var prop in idContainer.Properties())
                    {
                        if (prop.Value is Newtonsoft.Json.Linq.JObject cardObj)
                        {
                            // 將每個卡片物件加入陣列，並確保 id 被設定
                            var cardWithId = new Newtonsoft.Json.Linq.JObject(cardObj);
                            if (cardWithId["id"] == null)
                                cardWithId["id"] = prop.Name; // 用 property name 作為 id
                            arr.Add(cardWithId);
                        }
                    }
                }
                // 嘗試格式 3: {"cards": [...]}
                else if (topObj["cards"] is Newtonsoft.Json.Linq.JArray wrappedArr)
                {
                    arr = wrappedArr;
                }
                // 格式 4: 單個物件 {...}
                else
                {
                    arr = new Newtonsoft.Json.Linq.JArray(token);
                }
            }

            if (arr == null)
            {
                Debug.LogWarning("[CardEvent] LoadCardDatabase: JSON 格式無法識別。");
                return;
            }

            foreach (var item in arr)
            {
                if (item is not Newtonsoft.Json.Linq.JObject jobj) continue;

                // 直接用 key 取得 id，完全繞過 JsonProperty attribute 問題
                string id = jobj["id"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(id)) continue;

                // 先嘗試 attribute-based 反序列化，再手動補上可能遺漏的 Id
                Tur.CardData card;
                try { card = jobj.ToObject<Tur.CardData>(); }
                catch { card = null; }

                if (card == null)
                    card = new Tur.CardData();

                // 確保 Id 一定被設定（ToObject 若屬性映射失敗時的保底）
                if (string.IsNullOrEmpty(card.Id))
                    card.Id = id;

                cardLookup[id] = card;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CardEvent] LoadCardDatabase: JSON 解析失敗 {e.Message}");
            return;
        }

        isLoaded = cardLookup.Count > 0;
        string loadedKeys = string.Join(", ", System.Linq.Enumerable.Select(cardLookup.Keys, k => $"[{k}]"));
        Debug.Log($"[CardEvent] LoadCardDatabase: loaded={cardLookup.Count}  isLoaded={isLoaded}  keys={loadedKeys}");
    }

    void Start()
    {
        LoadCardDatabase();
    }

    void Awake()
    {
        // 盡早初始化，降低首次查詢發生在 Start 前的風險
        if (cardLookup.Count == 0 || !isLoaded)
            LoadCardDatabase();
    }

        // 這裡可以放置當卡片被使用時的事件邏輯
        //check card skill Text in json
    
    
}


