using System.Collections.Generic;
using Newtonsoft.Json;

public class HandData
{
    public int id;
    public int count = 0;
    public string path;

    public List<string> Hand = new List<string>();
    public HandData()
    {
        path = "Assets/json/hand.json";
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
        var list = JsonLoader.LoadFromFile<List<string>>(path);
        Hand = list ?? new List<string>();
        count = Hand.Count;
    }

    // 將目前手牌清單寫回 hand.json
    public void SaveHand()
    {
        JsonLoader.SaveToFile(Hand ?? new List<string>(), path, indented: true);
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
}
