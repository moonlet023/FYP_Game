using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

// JSON 讀/寫工具（支援純文字與泛型序列化/反序列化）
public class ReadJson
{
  public string path;

  public ReadJson() {}
  public ReadJson(string path) { this.path = path; }

  public void SetPath(string path) => this.path = path;

  // 讀取既定路徑的 JSON 純文字
  public string ReadJsonText()
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    return File.ReadAllText(path, Encoding.UTF8);
  }

  // 寫入既定路徑的 JSON 純文字
  public void WriteJsonText(string json)
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    EnsureDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, json ?? string.Empty, Encoding.UTF8);
  }

  // 反序列化（既定路徑）
  public T Load<T>()
  {
    var json = ReadJsonText();
    return JsonConvert.DeserializeObject<T>(json);
  }

  // 反序列化（指定路徑）
  public static T LoadFromFile<T>(string filePath)
  {
    var json = File.ReadAllText(filePath, Encoding.UTF8);
    return JsonConvert.DeserializeObject<T>(json);
  }

  // 反序列化（字串）
  public static T LoadFromText<T>(string json)
  {
    return JsonConvert.DeserializeObject<T>(json);
  }

  // 序列化（既定路徑）
  public void Save<T>(T data, bool indented = true)
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    var json = JsonConvert.SerializeObject(data, indented ? Formatting.Indented : Formatting.None);
    EnsureDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, json, Encoding.UTF8);
  }

  // 序列化（指定路徑）
  public static void SaveToFile<T>(T data, string filePath, bool indented = true)
  {
    var json = JsonConvert.SerializeObject(data, indented ? Formatting.Indented : Formatting.None);
    EnsureDirectory(Path.GetDirectoryName(filePath));
    File.WriteAllText(filePath, json, Encoding.UTF8);
  }

  // 取得序列化字串
  public static string ToText<T>(T data, bool indented = true)
  {
    return JsonConvert.SerializeObject(data, indented ? Formatting.Indented : Formatting.None);
  }

  // 扁平鍵值擷取（保留相容）
  public bool TryGetElementString(string json, string key, out string value)
  {
    value = null;
    if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;
    var pattern = $"\\\"{Regex.Escape(key)}\\\"\\s*:\\s*(\\\"(.*?)\\\"|[-+]?[0-9]*\\.?[0-9]+|true|false|null)";
    var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    if (!m.Success) return false;
    var raw = m.Groups[1].Value;
    var asStringGroup = m.Groups[2];
    if (asStringGroup != null && asStringGroup.Success)
      value = asStringGroup.Value;
    else
      value = raw;
    return true;
  }

  public bool TryGetElement<T>(string json, string key, out T result)
  {
    result = default;
    if (!TryGetElementString(json, key, out var s)) return false;
    try
    {
      if (typeof(T) == typeof(string)) { result = (T)(object)s; return true; }
      if (typeof(T) == typeof(bool)) { if (bool.TryParse(s, out var b)) { result = (T)(object)b; return true; } return false; }
      if (typeof(T) == typeof(int)) { if (int.TryParse(s, out var i)) { result = (T)(object)i; return true; } return false; }
      if (typeof(T) == typeof(float)) { if (float.TryParse(s, out var f)) { result = (T)(object)f; return true; } return false; }
      if (typeof(T) == typeof(double)) { if (double.TryParse(s, out var d)) { result = (T)(object)d; return true; } return false; }
    }
    catch { return false; }
    return false;
  }

  private static void EnsureDirectory(string dir)
  {
    if (string.IsNullOrEmpty(dir)) return;
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
  }
}

// 相容別名：如需可使用 JsonLoader
public class JsonLoader : ReadJson
{
  public JsonLoader() : base() {}
  public JsonLoader(string path) : base(path) {}
}
