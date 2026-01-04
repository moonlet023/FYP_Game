using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

// Usage:
// 1) 將此腳本掛在遊戲最初場景的一個 GameObject 上（例如 "GameManager"）。
// 2) 讀取名稱：var name = UserInformation.Instance.PlayerName;
// 3) 設定名稱並立即保存：UserInformation.Instance.SetPlayerName("YourName");
// 4) 此資料會保存到 Application.persistentDataPath 下的 player.json，並跨場景保留。

// 不再使用 [System.Serializable] 類別，改以輕量 JSON 手工序列化避免編譯衝突。

public class UserInformation : MonoBehaviour
{
    public static UserInformation Instance { get; private set; }

    [SerializeField]
    private string playerName = string.Empty;

    private string SavePath => Path.Combine(Application.persistentDataPath, "player.json");

    public string PlayerName => playerName;

    private void Awake()
    {
        // Singleton + 跨場景保留
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 啟動時嘗試載入已保存的資料
        Load();
    }

    // 設定玩家名稱並立刻保存到 JSON
    public void SetPlayerName(string name)
    {
        playerName = name ?? string.Empty;
        Save();
    }

    // 供其他腳本直接呼叫取得名稱（等同於屬性）
    public string GetPlayerName()
    {
        return playerName;
    }

    // 將目前名稱保存為 JSON
    public void Save()
    {
        // 手工組裝極簡 JSON：{"playerName":"..."}
        var safeName = JsonEscape(playerName);
        var json = @$"{{""playerName"":""{safeName}""}}";
        try
        {
            var dir = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save player data: {e.Message}");
        }
    }

    // 從 JSON 載入玩家名稱
    public void Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                // 簡易解析 "playerName":"..." 的值
                var match = Regex.Match(json, @"""playerName""\s*:\s*""(?<name>.*?)""", RegexOptions.Singleline);
                playerName = match.Success ? match.Groups["name"].Value : string.Empty;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load player data: {e.Message}");
        }
    }

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        // 只處理基本的跳脫需求（雙引號與反斜線）
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
