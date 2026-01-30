using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using MyGame.Client;
using System.IO;
using System;

// 範例行為：示範如何在 Unity 中使用 MatchmakingClient
// 搭配自簽憑證時，請將 Unity-SSLCertificateHandler.cs 放入專案並傳入實例。

public class MatchmakingExample : MonoBehaviour
{
    [Header("Server Base URL")] public string baseUrl = "https://pal.moonlet023.com:6660"; // 或你的外網域名
    [Header("UI References")] private string uid; // 以 UID 進行配對
    public TMPro.TextMeshProUGUI statusText;
    public Button joinButton;
    public Button cancelButton;
    public RawImage waiting;
    public GameObject MainMenuUI;
    [Header("TLS/Cert")] public bool trustSelfSignedCertificate = true; // 開發用：信任自簽憑證

  

    private MatchmakingClient _client;
    private string _ticketId;

    void Awake()
    {
        
        // 若需忽略自簽憑證，請使用專案內的自訂 CertificateHandler
        CertificateHandler certHandler = null;
        if (trustSelfSignedCertificate)
        {
            // 未提供白名單即接受所有憑證（僅供開發測試）。
            certHandler = new UnitySSLCertificateHandler();
        }
        _client = new MatchmakingClient(baseUrl, certHandler);

        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnJoinClicked()
    {
        waiting.gameObject.SetActive(true);
        MainMenuUI.gameObject.SetActive(false);
        // 優先從本地檔案（persistent/LocalAppData 下的 player/userinfo.json）讀取 uid
        string uid = null;
        string username = ""; // 可選：若你有名稱可傳入，否則留空
        try
        {
            var filePath = GetUserInfoPath();
            if (File.Exists(filePath))
            {
                string fileJson = File.ReadAllText(filePath);
                var stored = JsonUtility.FromJson<StoredUserInfo>(fileJson);
                if (stored != null)
                {
                    uid = stored.uid;
                    username = stored.username ?? "";
                }
            }

            // 若檔案不存在或未取得 uid，嘗試從 PlayerPrefs 讀取
            if (string.IsNullOrEmpty(uid))
            {
                string prefJson = PlayerPrefs.GetString("player_info", "{}");
                var stored2 = JsonUtility.FromJson<StoredUserInfo>(prefJson);
                if (stored2 != null)
                {
                    uid = stored2.uid;
                    if (string.IsNullOrEmpty(username)) username = stored2.username ?? "";
                }
            }
        }
        catch (Exception e)
        {
            statusText.text = "Read UID error: " + e.Message;
            return;
        }

        if (string.IsNullOrEmpty(uid))
        {
            statusText.text = "UID not found";
            MainMenuUI.gameObject.SetActive(true);
            waiting.gameObject.SetActive(false);
            return;
        }
        statusText.text = "Queueing by UID...";
        StartCoroutine(_client.JoinQueue(uid, username, status =>
        {
            _ticketId = status.ticketId;
            statusText.text = $"Joined queue. Ticket: {_ticketId}. State={status.state}. UID={status.uid}";
            // 開始輪詢直到配對
            StartCoroutine(_client.PollUntilMatched(_ticketId, 2f, 60f,
                onMatched: matchedStatus =>
                {
                    statusText.text = $"Matched! OpponentUid={matchedStatus.opponentUid}. MatchId={matchedStatus.matchId}";
                    // 取詳細資料
                    StartCoroutine(_client.GetMatchDetail(matchedStatus.matchId, detail =>
                    {
                        statusText.text = $"Match UID: {detail.playerA} vs {detail.playerB}\nMatchId={detail.matchId}";
                    }, err =>
                    {
                        statusText.text = "Get detail error: " + err;
                    }));
                },
                onTimeout: () =>
                {
                    statusText.text = "Timeout while waiting for match";
                    MainMenuUI.gameObject.SetActive(true);
                    waiting.gameObject.SetActive(false);
                },
                onError: err => { statusText.text = err; }));
        }, err =>
        {
            statusText.text = err;
        }));
        
    }
    private void OnCancelClicked()
    {
        if (string.IsNullOrEmpty(_ticketId))
        {
            statusText.text = "No active ticket";
            return;
        }

        StartCoroutine(_client.Cancel(_ticketId, () =>
        {
            statusText.text = "Cancelled queue";
            _ticketId = null;
        }, err =>
        {
            statusText.text = err;
        }));
    }

    private string GetUserInfoPath()
    {
        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        string dir = System.IO.Path.Combine(appData, Application.companyName, Application.productName, "player");
        return System.IO.Path.Combine(dir, "userinfo.json");
        #else
        return System.IO.Path.Combine(Application.persistentDataPath, "player", "userinfo.json");
        #endif
    }
}
