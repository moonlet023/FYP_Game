using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using ServerLib.RCH_Connection;
using TMPro;
using UnityEngine.Networking;

public class pairing : MonoBehaviour
{

    public Button startButton;
    private String UserName;
    private String OpponentName;

    // Start is called before the first frame update
    void Start()
    {
        // 優先使用 UserInformation 中的已載入名稱；若未存在則嘗試從檔案讀取
        UserName = UserInformation.Instance != null ? UserInformation.Instance.PlayerName : TryLoadPlayerNameFromFile();
        startButton.onClick.AddListener(OnstratButtonClick);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnstratButtonClick()
    {
        // 若當前為空，嘗試再讀一次（例如場景剛載入尚未初始化）
        if (string.IsNullOrEmpty(UserName))
        {
            UserName = UserInformation.Instance != null ? UserInformation.Instance.PlayerName : TryLoadPlayerNameFromFile();
        }

        if (string.IsNullOrEmpty(UserName))
        {
            Debug.LogError("Pairing failed: username is empty. Please login or set via UserInformation.");
            return;
        }

        var conn = new RCH_Connection();
        StartCoroutine(conn.JoinQueue(UserName, resp =>
        {
            if (resp == null) { Debug.LogError("No response"); return; }
            if (resp.ok) Debug.Log("Join Queue OK");
            else Debug.LogError("Join Queue failed: " + resp.error);
        }));
    }

    private string TryLoadPlayerNameFromFile()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "player.json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var match = Regex.Match(json, @"""playerName""\s*:\s*""(?<name>.*?)""", RegexOptions.Singleline);
            return match.Success ? match.Groups["name"].Value : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("TryLoadPlayerNameFromFile error: " + e.Message);
            return null;
        }
    }


}
