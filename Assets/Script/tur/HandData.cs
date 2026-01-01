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
}
