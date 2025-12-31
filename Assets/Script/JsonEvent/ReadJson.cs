using System;
using System.IO;
using System.Text.RegularExpressions;

// 簡易 JSON 讀取工具：
// - 支援直接從字串或檔案取出「扁平」JSON 物件中的單一元素值
// - 可處理字串/數字/bool/null 基本型態；複雜巢狀結構請改用類別 + JsonUtility
public class ReadJson
{
  public string path;

  // 設定欲讀取的 JSON 檔案路徑
  public void SetPath(string path)
  {
    this.path = path;
  }

  // 從檔案讀取 JSON 文字
  public string ReadJsonText()
  {
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("JSON path not set");
    return File.ReadAllText(path);
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

  // 範例：從檔案讀取並回傳指定鍵的字串值
  public string GetElementFromFile(string key)
  {
    var json = ReadJsonText();
    return TryGetElementString(json, key, out var v) ? v : null;
  }
}
