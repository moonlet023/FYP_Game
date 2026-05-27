using System.Collections.Generic;
using Newtonsoft.Json;

public class HandData
{
    [System.Serializable]
    private class HandFile
    {
        public List<string> hand = new List<string>();
    }

    public int id;
    public int count = 0;
    public string path;

    public List<string> Hand = new List<string>();
    public HandData()
    {
        // 使用 StreamingAssets 路徑
        #if UNITY_EDITOR
            path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "json", "hand.json");
        #else
            path = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "json", "hand.json");
        #endif
    }

    public void PrintHandLog()
    {
       JsonLoader jsonLoader = new JsonLoader();
       jsonLoader.SetPath(path);
       System.Diagnostics.Debug.WriteLine("Hand Path: " + path);
       System.Diagnostics.Debug.WriteLine("Hand ID: " + id);
    }

    // 讀取手牌清單（hand.json）到記憶體
    public void LoadHand()
    {
        List<string> list = null;

        try
        {
            // 首選目前格式：{ "hand": [...] }
            var wrapped = JsonLoader.LoadFromFile<HandFile>(path);
            if (wrapped != null)
            {
                list = wrapped.hand;
            }
        }
        catch (JsonException)
        {
            // 往下嘗試舊格式
        }

        if (list == null)
        {
            try
            {
                // 相容舊格式：["01", "02", ...]
                list = JsonLoader.LoadFromFile<List<string>>(path);
            }
            catch (JsonException)
            {
                list = new List<string>();
            }
        }

        Hand = list ?? new List<string>();
        count = Hand.Count;
    }

    // 將目前手牌清單寫回 hand.json
    public void SaveHand()
    {
        var data = new HandFile
        {
            hand = Hand ?? new List<string>()
        };
        JsonLoader.SaveToFile(data, path, indented: true);
        count = Hand?.Count ?? 0;
    }

    // 加入一張卡的 id 並立即保存到 hand.json
    public void AddCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        Hand.Add(cardId);
        count = Hand.Count;
        SaveHand();
    }

    // 移除一張卡的 id 並立即保存到 hand.json
    public void RemoveCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        Hand.Remove(cardId);
        count = Hand.Count;
        SaveHand();
    }

    // 清空手牌並保存到 hand.json
    public void ClearHand()
    {
        Hand.Clear();
        count = 0;
        SaveHand();
    }
}
