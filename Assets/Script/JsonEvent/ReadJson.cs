using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

// 通用 JSON 載入器：
// - 提供讀/寫文字、泛型物件序列化/反序列化
// - 仍保留簡易鍵值擷取（扁平 JSON）以相容既有使用情境
public class JsonLoader
{
  public string path;

  public JsonLoader() {}
  public JsonLoader(string path) { this.path = path; }

  // 設定欲讀寫的 JSON 檔案路徑
  public void SetPath(string path) => this.path = path;

  // 從既定路徑讀取 JSON 純文字
  public string ReadJsonText()
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    return File.ReadAllText(path, Encoding.UTF8);
  }

  // 將 JSON 純文字寫入既定路徑
  public void WriteJsonText(string json)
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    EnsureDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, json ?? string.Empty, Encoding.UTF8);
  }

  // 以 Newtonsoft.Json 反序列化（使用既定路徑）
  public T Load<T>()
  {
    var json = ReadJsonText();
    return JsonConvert.DeserializeObject<T>(json);
  }

  // 以 Newtonsoft.Json 反序列化（指定路徑）
  public T LoadFromFile<T>(string filePath)
  {
    var json = File.ReadAllText(filePath, Encoding.UTF8);
    return JsonConvert.DeserializeObject<T>(json);
  }

  // 以 Newtonsoft.Json 序列化（使用既定路徑）
  public void Save<T>(T data, bool indented = true)
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    var json = JsonConvert.SerializeObject(data, indented ? Formatting.Indented : Formatting.None);
    EnsureDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, json, Encoding.UTF8);
  }

  // 以 Newtonsoft.Json 序列化（指定路徑）
  public void SaveToFile<T>(T data, string filePath, bool indented = true)
  {
    var json = JsonConvert.SerializeObject(data, indented ? Formatting.Indented : Formatting.None);
    EnsureDirectory(Path.GetDirectoryName(filePath));
    File.WriteAllText(filePath, json, Encoding.UTF8);
  }

  // 嘗試由 JSON 字串中取得某個鍵的字串值（僅支援扁平結構）
  public bool TryGetElementString(string json, string key, out string value)
  {
    value = null;
    if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;

    // 匹配格式："key" : "value" 或 數字/bool/null
    var pattern = $"\\\"{Regex.Escape(key)}\\\"\\s*:\\s*(\\\"(.*?)\\\"|[-+]?[0-9]*\\.?[0-9]+|true|false|null)";
    var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    if (!m.Success) return false;

    var raw = m.Groups[1].Value;
    var asStringGroup = m.Groups[2];
    if (asStringGroup != null && asStringGroup.Success)
    {
      value = asStringGroup.Value; // 字串值（不含外層引號）
    }
    else
    {
      value = raw; // 數字/bool/null 以字串返回
    }
    return true;
  }

  // 泛型版本：將元素值轉型為 T（僅適用基本型態）
  public bool TryGetElement<T>(string json, string key, out T result)
  {
    result = default;
    if (!TryGetElementString(json, key, out var s)) return false;

    try
    {
      if (typeof(T) == typeof(string))
      {
        result = (T)(object)s;
        return true;
      }
      if (typeof(T) == typeof(bool))
      {
        if (bool.TryParse(s, out var b)) { result = (T)(object)b; return true; }
        return false;
      }
      if (typeof(T) == typeof(int))
      {
        if (int.TryParse(s, out var i)) { result = (T)(object)i; return true; }
        return false;
      }
      if (typeof(T) == typeof(float))
      {
        if (float.TryParse(s, out var f)) { result = (T)(object)f; return true; }
        return false;
      }
      if (typeof(T) == typeof(double))
      {
        if (double.TryParse(s, out var d)) { result = (T)(object)d; return true; }
        return false;
      }
      // 其他型態可依需要擴充
    }
    catch { return false; }

    return false;
  }

  // 範例：從既定路徑讀取並回傳指定鍵的字串值
  public string GetElementFromFile(string key)
  {
    var json = ReadJsonText();
    return TryGetElementString(json, key, out var v) ? v : null;
  }

  private static void EnsureDirectory(string dir)
  {
    if (string.IsNullOrEmpty(dir)) return;
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
  }
}

// 兼容舊用法：ReadJson 保留為 JsonLoader 的別名/子類
public class ReadJson : JsonLoader
{
  public ReadJson() : base() {}
  public ReadJson(string path) : base(path) {}
}
