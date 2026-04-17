using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;


public class DeckData
{
    [System.Serializable]
    private class DeckFile
    {
        public List<string> deck = new List<string>();
    }

    public int id;
    public int count = 50;
    public string path;
    public JsonLoader jsonLoader = new JsonLoader();

    public DeckData()
    {
        path = "Assets/json/deck.json";
    }

     public void PrintDeckLog()
     {
         jsonLoader.SetPath(path);
         Debug.Log("Deck Path: " + path);
         Debug.Log("Deck ID: " + id);
     }

    // 舊名保留：suffleDeck，呼叫正確拼字的 ShuffleDeck
    public void suffleDeck() => ShuffleDeck();

    // 正式洗牌（使用一致型別 List<string>）
    public void ShuffleDeck()
    {
        var deck = LoadDeck();
        var rnd = new System.Random();
        int n = deck.Count;
        while (n > 1)
        {
            int k = rnd.Next(n--);
            string temp = deck[n];
            deck[n] = deck[k];
            deck[k] = temp;
        }
        SaveDeck(deck);
    }

    // 抽指定張數，從牌堆移除頂牌並回傳抽到的 id，同時寫入 hand.json
    public List<string> drawCard(HandData handData, int drawCount)
    {
        var deck = LoadDeck();
        var drawn = new List<string>();
        for (int i = 0; i < drawCount; i++)
        {
            if (deck.Count == 0)
            {
                Debug.Log("Deck is empty!");
                break;
            }
            string cardId = deck[0];
            deck.RemoveAt(0);
            drawn.Add(cardId);
            if (handData != null)
            {
                handData.AddCardId(cardId);
            }
        }
        SaveDeck(deck);
        return drawn;
    }

    // 移除最上方一張牌，回傳該牌 id
    public string topCard()
    {
        var deck = LoadDeck();
        if (deck.Count == 0)
        {
            Debug.Log("Deck is empty!");
            return null;
        }
        string cardId = deck[0];
        deck.RemoveAt(0);
        SaveDeck(deck);
        return cardId;
    }

    // 讀取整副牌（JSON）
    public List<string> LoadDeck()
    {
        jsonLoader.SetPath(path);
        try
        {
            // 首選目前格式：{ "deck": [...] }
            var wrapped = JsonLoader.LoadFromFile<DeckFile>(path);
            if (wrapped != null && wrapped.deck != null)
            {
                return wrapped.deck;
            }
        }
        catch (JsonException)
        {
            // 往下嘗試舊格式
        }

        try
        {
            // 相容舊格式：["01", "02", ...]
            return JsonLoader.LoadFromFile<List<string>>(path) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    // 儲存整副牌（JSON）
    public void SaveDeck(List<string> deck)
    {
        jsonLoader.SetPath(path);
        var data = new DeckFile
        {
            deck = deck ?? new List<string>()
        };
        JsonLoader.SaveToFile(data, path, indented: true);
    }

    // 可選：更新路徑
    public void SetPath(string newPath)
    {
        if (!string.IsNullOrEmpty(newPath))
        {
            path = newPath;
            jsonLoader.SetPath(path);
        }
    }

    public void backToDeck(List<string> cardIds)
    {
        var deck = LoadDeck();
        deck.AddRange(cardIds);
        SaveDeck(deck);
    }

}
