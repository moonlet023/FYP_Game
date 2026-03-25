using Newtonsoft.Json;

namespace Tur
{
    public class CardData
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("Act Num")] public int ActNum { get; set; }
        [JsonProperty("cost")] public int Cost { get; set; }
        [JsonProperty("en_spawn")] public int EnSpawn { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("skill Text")] public string SkillText { get; set; }
        [JsonProperty("Atk")] public int Atk { get; set; }
        [JsonProperty("Def")] public int Def { get; set; }
        [JsonProperty("image")] public string ImagePath { get; set; }
    }
}
