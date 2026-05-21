using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Tur
{
    internal class StringOrArrayToStringListConverter : JsonConverter<List<string>>
    {
        public override List<string> ReadJson(JsonReader reader, System.Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var list = existingValue ?? new List<string>();
            list.Clear();

            if (reader.TokenType == JsonToken.String)
            {
                string s = reader.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s.Trim());
                return list;
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var arr = JArray.Load(reader);
                for (int i = 0; i < arr.Count; i++)
                {
                    string s = arr[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s.Trim());
                }
                return list;
            }

            if (reader.TokenType == JsonToken.Null)
                return list;

            string fallback = JToken.Load(reader).ToString();
            if (!string.IsNullOrWhiteSpace(fallback))
                list.Add(fallback.Trim());
            return list;
        }

        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNull();
                return;
            }

            if (value.Count == 1)
            {
                writer.WriteValue(value[0]);
                return;
            }

            writer.WriteStartArray();
            for (int i = 0; i < value.Count; i++)
                writer.WriteValue(value[i]);
            writer.WriteEndArray();
        }
    }

    public class CardData
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")]
        [JsonConverter(typeof(StringOrArrayToStringListConverter))]
        public List<string> Types { get; set; } = new List<string>();
        [JsonIgnore]
        public string Type
        {
            get => Types != null && Types.Count > 0 ? Types[0] : string.Empty;
            set
            {
                if (Types == null)
                    Types = new List<string>();

                string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
                if (Types.Count == 0)
                    Types.Add(normalized);
                else
                    Types[0] = normalized;
            }
        }
        [JsonProperty("Act Num")] public int ActNum { get; set; }
        [JsonProperty("cost")] public int Cost { get; set; }
        [JsonProperty("en_spawn")] public int EnSpawn { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("skill Text")] public string SkillText { get; set; }
        [JsonProperty("Atk")] public int Atk { get; set; }
        [JsonProperty("Def")] public int Def { get; set; }
        [JsonProperty("image")] public string ImagePath { get; set; }
        [JsonProperty("EZcode")] public string EZcode { get; set; }
        [JsonProperty("is Ace")] public bool IsAce { get; set; }
        [JsonProperty("seal")] public string Seal { get; set; }
        [JsonProperty("is Event")] public bool IsEvent { get; set; }
    }
}
