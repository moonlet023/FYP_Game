using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using ServerLib;

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
    public GameObject MatchRoomUI;
    [Header("Match Room")]
    [Tooltip("配對成功後要切換的場景名稱。留空則不切場景，改用 MatchRoomUI（若有指定）。")]
    public string matchRoomSceneName;
    [Header("TLS/Cert")] public bool trustSelfSignedCertificate = true; // 開發用：信任自簽憑證
    [Tooltip("可選：允許的伺服器憑證 SHA256 指紋（不含冒號與破折號）。若未提供指紋則維持系統預設驗證（較安全）。")]
    public string[] allowedFingerprints = new[] { "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85" };

  

    private MatchmakingClient _client;
    private string _ticketId;

    void Awake()
    {
        // 保底：若 Inspector 為空，填入預設指紋以啟用釘選
        if (trustSelfSignedCertificate && (allowedFingerprints == null || allowedFingerprints.Length == 0))
        {
            allowedFingerprints = new[] { "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85" };
        }
        // 使用共用憑證設定產生處理器（指紋釘選）
        CertificateHandler certHandler = TlsCertConfig.CreateHandlerOrNull(baseUrl);
        _client = new MatchmakingClient(baseUrl, certHandler);

        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnJoinClicked()
    {
        MatchmakingSessionBridge.Clear();

        if (waiting != null) waiting.gameObject.SetActive(true);
        if (MainMenuUI != null) MainMenuUI.gameObject.SetActive(false);
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
            if (statusText != null) statusText.text = "UID not found";
            if (MainMenuUI != null) MainMenuUI.gameObject.SetActive(true);
            if (waiting != null) waiting.gameObject.SetActive(false);
            return;
        }
        this.uid = uid;
        if (statusText != null) statusText.text = "Queueing by UID...";
        StartCoroutine(_client.JoinQueue(uid, username, status =>
        {
            MatchmakingSessionBridge.SetJoinStatus(status);

            _ticketId = status.ticketId;
            if (statusText != null) statusText.text = $"Joined queue. Ticket: {_ticketId}. State={status.state}. UID={status.uid}";
            // 開始輪詢直到配對
            StartCoroutine(_client.PollUntilMatched(_ticketId, 2f, 60f,
                onMatched: matchedStatus =>
                {
                    MatchmakingSessionBridge.SetMatchedStatus(matchedStatus);

                    if (waiting != null) waiting.gameObject.SetActive(false);
                    if (statusText != null) statusText.text = BuildMatchedSummary(matchedStatus);

                    if (string.IsNullOrEmpty(matchedStatus.matchId))
                    {
                        return;
                    }

                    // 取詳細資料
                    StartCoroutine(_client.GetMatchDetail(matchedStatus.matchId, detail =>
                    {
                        MatchmakingSessionBridge.SetDetail(detail);

                        if (statusText != null)
                        {
                            statusText.text = BuildMatchedSummary(matchedStatus) +
                                              $"\nDetail: {detail.playerA} vs {detail.playerB}";
                        }

                        EnterMatchRoom(matchedStatus);
                    }, err =>
                    {
                        MatchmakingSessionBridge.SetError(err);

                        if (statusText != null)
                        {
                            statusText.text = BuildMatchedSummary(matchedStatus) +
                                              "\nGet detail error: " + err;
                        }

                        // 即使取詳細資訊失敗，也仍然進房
                        EnterMatchRoom(matchedStatus);
                    }));
                },
                onTimeout: () =>
                {
                    MatchmakingSessionBridge.SetError("Timeout while waiting for match");

                    if (statusText != null) statusText.text = "Timeout while waiting for match";
                    if (MainMenuUI != null) MainMenuUI.gameObject.SetActive(true);
                    if (waiting != null) waiting.gameObject.SetActive(false);
                },
                onError: err =>
                {
                    MatchmakingSessionBridge.SetError(err);

                    if (statusText != null) statusText.text = err;
                    if (MainMenuUI != null) MainMenuUI.gameObject.SetActive(true);
                    if (waiting != null) waiting.gameObject.SetActive(false);
                }));
        }, err =>
        {
            MatchmakingSessionBridge.SetError(err);

            if (statusText != null) statusText.text = err;
            if (MainMenuUI != null) MainMenuUI.gameObject.SetActive(true);
            if (waiting != null) waiting.gameObject.SetActive(false);
        }));
        
    }
    private void OnCancelClicked()
    {
        if (string.IsNullOrEmpty(_ticketId))
        {
            if (statusText != null) statusText.text = "No active ticket";
            return;
        }

        StartCoroutine(_client.Cancel(_ticketId, () =>
        {
            if (statusText != null) statusText.text = "Cancelled queue";
            _ticketId = null;
            MatchmakingSessionBridge.Clear();
            if (MainMenuUI != null) MainMenuUI.gameObject.SetActive(true);
            if (waiting != null) waiting.gameObject.SetActive(false);
        }, err =>
        {
            MatchmakingSessionBridge.SetError(err);
            if (statusText != null) statusText.text = err;
        }));
    }

    private string BuildMatchedSummary(MatchmakingStatus matchedStatus)
    {
        if (matchedStatus == null) return "Matched!";

        var selfUid = string.IsNullOrEmpty(matchedStatus.uid) ? uid : matchedStatus.uid;
        return $"Matched!\nSelfUid={selfUid}\nOpponent={matchedStatus.opponentUsername} ({matchedStatus.opponentUid})\nRoomId={matchedStatus.roomId}\nMatchId={matchedStatus.matchId}";
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

    private void EnterMatchRoom(MatchmakingStatus matchedStatus)
    {
        if (matchedStatus != null)
        {
            if (!string.IsNullOrEmpty(matchedStatus.roomId))
            {
                PlayerPrefs.SetString("match_room_id", matchedStatus.roomId);
            }

            if (!string.IsNullOrEmpty(matchedStatus.matchId))
            {
                PlayerPrefs.SetString("match_id", matchedStatus.matchId);
            }

            PlayerPrefs.Save();
        }

        if (!string.IsNullOrWhiteSpace(matchRoomSceneName))
        {
            SceneManager.LoadScene(matchRoomSceneName.Trim());
            return;
        }

        if (MatchRoomUI != null)
        {
            MatchRoomUI.SetActive(true);
            if (MainMenuUI != null) MainMenuUI.SetActive(false);
            if (waiting != null) waiting.gameObject.SetActive(false);
            return;
        }

        if (statusText != null)
        {
            statusText.text += "\nMatched, but no match room target set. Please assign MatchRoomUI or matchRoomSceneName.";
        }
    }
}
