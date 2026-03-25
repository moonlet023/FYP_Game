using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using ServerLib;

public class Game_System : MonoBehaviour
{

    [Header("Server Base URL")] public string baseUrl = "https://pal.moonlet023.com:6660"; // 或你的外網域名
    [Header("UI References")] private string uid; // 以 UID 進行配對
    [Header("TLS/Cert")] public bool trustSelfSignedCertificate = true; // 開發用：信任自簽憑證
    [Tooltip("可選：允許的伺服器憑證 SHA256 指紋（不含冒號與破折號）。若未提供指紋則維持系統預設驗證（較安全）。")]
    public string[] allowedFingerprints = new[] { "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85" };
    
    public string roomId;
    public string matchId;
    public string ticketId;
    public string ticketState;
    public string selfUid;
    public string selfUsername;
    public string opponentUid;
    public string opponentUsername;
    public string matchDetailText;
    public string lastMatchError;
    private MatchmakingClient _client;
    

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
    }


    // Start is called before the first frame update
    void Start()
    {
        LoadMatchmakingStatusFromBridge();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LoadMatchmakingStatusFromBridge()
    {
        var join = MatchmakingSessionBridge.JoinStatus;
        var matched = MatchmakingSessionBridge.MatchedStatus;
        var detail = MatchmakingSessionBridge.Detail;

        if (join != null)
        {
            ticketId = join.ticketId;
            ticketState = join.state;
            if (string.IsNullOrEmpty(selfUid)) selfUid = join.uid;
            if (string.IsNullOrEmpty(selfUsername)) selfUsername = join.username;
        }

        if (matched != null)
        {
            ticketState = matched.state;
            matchId = matched.matchId;
            roomId = matched.roomId;
            selfUid = matched.uid;
            selfUsername = matched.username;
            opponentUid = matched.opponentUid;
            opponentUsername = matched.opponentUsername;
        }

        if (detail != null)
        {
            matchDetailText = $"{detail.playerA} vs {detail.playerB}";
            if (string.IsNullOrEmpty(matchId)) matchId = detail.matchId;
            if (string.IsNullOrEmpty(roomId)) roomId = detail.roomId;
        }

        lastMatchError = MatchmakingSessionBridge.LastError;

        if (string.IsNullOrEmpty(roomId))
        {
            roomId = PlayerPrefs.GetString("match_room_id", string.Empty);
        }

        if (string.IsNullOrEmpty(matchId))
        {
            matchId = PlayerPrefs.GetString("match_id", string.Empty);
        }

        Debug.Log($"[Game_System] Loaded match status: ticket={ticketId}, state={ticketState}, self={selfUid}, opponent={opponentUid}, room={roomId}, match={matchId}");

        if (!string.IsNullOrEmpty(lastMatchError))
        {
            Debug.LogWarning($"[Game_System] Last matchmaking error: {lastMatchError}");
        }
    }
}
