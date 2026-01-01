using System;
using System.IO;
using UnityEngine;

// 掛到任意場景物件即可示範讀/寫
public class JsonReaderExample : MonoBehaviour
{
  [Serializable]
  public class GameConfig
  {
    public string playerName;
    public int maxLives;
    public bool isHardMode;
  }

  [Header("StreamingAssets 下的檔名")]
  public string fileName = "config.json";

  private ReadJson reader;

  void Start()
  {
    // 在 Editor/Windows 上可直接用 File.ReadAllText 讀 StreamingAssets
    var path = Path.Combine(Application.streamingAssetsPath, fileName);
    reader = new ReadJson(path);

    // 1) 以型別反序列化
    try
    {
      var cfg = reader.Load<GameConfig>();
      Debug.Log($"[Load<T>] player={cfg.playerName}, lives={cfg.maxLives}, hard={cfg.isHardMode}");

      // 修改並存回（縮排輸出）
      cfg.maxLives += 1;
      reader.Save(cfg, indented: true);
      Debug.Log($"[Save] 已將 maxLives+1 並寫回 {path}");
    }
    catch (Exception e)
    {
      Debug.LogError($"反序列化失敗: {e.Message}");
    }

    // 2) 以純文字 + 鍵值擷取（適合扁平 JSON）
    try
    {
      var jsonText = reader.ReadJsonText();
      if (reader.TryGetElement<string>(jsonText, "playerName", out var name))
        Debug.Log($"[TryGetElement<string>] playerName={name}");
      if (reader.TryGetElement<int>(jsonText, "maxLives", out var lives))
        Debug.Log($"[TryGetElement<int>] maxLives={lives}");
      if (reader.TryGetElement<bool>(jsonText, "isHardMode", out var hard))
        Debug.Log($"[TryGetElement<bool>] isHardMode={hard}");
    }
    catch (Exception e)
    {
      Debug.LogError($"純文字讀取失敗: {e.Message}");
    }
  }
}
