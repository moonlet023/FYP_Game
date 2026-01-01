using Newtonsoft.Json;

public class CardData
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("Act Num")] public int ActNum { get; set; }
    [JsonProperty("skill Text")] public string SkillText { get; set; }
    [JsonProperty("Atk")] public int Atk { get; set; }
    [JsonProperty("Def")] public int Def { get; set; }
}
