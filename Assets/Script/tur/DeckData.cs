using System.Collections.Generic;
using System.Diagnostics;


public class DeckData
{
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
       Debug.WriteLine("Deck Path: " + path);
       Debug.WriteLine("Deck ID: " + id);
    }

    public void suffleDeck()
    {
        var deck = jsonLoader.LoadFromFile<List<int>>(path);
        var rnd = new System.Random();
        int n = deck.Count;
        while (n > 1)
        {
            int k = rnd.Next(n--);
            int temp = deck[n];
            deck[n] = deck[k];
            deck[k] = temp;
        }
        jsonLoader.SaveToFile(deck, path);
    }

    public void drawCard(HandData handData, int drawCount)
    {
        var deck = jsonLoader.LoadFromFile<List<string>>(path);
        for (int i = 0; i < drawCount; i++)
        {
            if (deck.Count == 0)
            {
                Debug.WriteLine("Deck is empty!");
                break;
            }
            string card = deck[0];
            deck.RemoveAt(0);
            handData.Hand.Add(card);
        }
        jsonLoader.SaveToFile(deck, path);
    }


}
