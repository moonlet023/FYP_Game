using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using ServerLib;

// Usage:
// 1) 將此腳本掛在場景中的一個 GameObject（如 "NetworkManager"）。
// 2) 設定登入 URL：在 Inspector 的 `loginUrl` 欄位填入伺服器登入端點。
// 3) 於按鈕事件呼叫 `LoginWithCredentials(username, password)`。
// 4) 登入成功後，會：
//    - 觸發 `OnLoginSucceeded` 事件並提供 username
//    - 呼叫 `UserInformation.Instance.SetPlayerName(username)` 以保存到 JSON（persistentDataPath/player.json）
// 5) 若登入回應格式為 {"username":"..."} 或 {"data":{"username":"..."}} 皆可解析。

[Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public class LoginResponseSimple
{
    public string username;
}

[Serializable]
public class LoginData
{
    public string username;
}

[Serializable]
public class LoginEnvelope
{
    public bool success;
    public LoginData data;
    public string message;
}

public class LoginService : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string loginUrl = string.Empty;

    [Header("TLS/憑證設定")]
    [Tooltip("開發/測試用：信任自簽或不被系統信任的憑證。正式環境請使用可信 CA 憑證並關閉此選項。")]
    [SerializeField] private bool trustSelfSignedCertificate = true;
    [Tooltip("允許的伺服器憑證 SHA256 指紋（不含冒號與破折號）。留空則不掛自訂處理器。")]
    [SerializeField] private string[] allowedFingerprints = new[] { "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85" };

    [Header("Events")]
    public UnityEvent<string> OnLoginSucceeded;
    public UnityEvent<string> OnLoginFailed;

    public void SetLoginUrl(string url)
    {
        loginUrl = url;
    }

    // 以帳密登入
    public void LoginWithCredentials(string user, string pass)
    {
        if (string.IsNullOrEmpty(loginUrl))
        {
            Debug.LogError("Login URL not set.");
            OnLoginFailed?.Invoke("Login URL not set");
            return;
        }
        StartCoroutine(LoginCoroutine(loginUrl, user, pass));
    }

    private IEnumerator LoginCoroutine(string url, string user, string pass)
    {
        var bodyObj = new LoginRequest { username = user, password = pass };
        var bodyJson = JsonUtility.ToJson(bodyObj);

        var request = new UnityWebRequest(url, "POST");
        var bytes = Encoding.UTF8.GetBytes(bodyJson);
        request.uploadHandler = new UploadHandlerRaw(bytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");

        // 使用共用憑證設定產生處理器（指紋釘選）
        request.certificateHandler = TlsCertConfig.CreateHandlerOrNull(url);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Login request failed: {request.error}");
            OnLoginFailed?.Invoke(request.error);
            yield break;
        }

        var responseText = request.downloadHandler.text;
        var username = ExtractUsername(responseText);

        if (!string.IsNullOrEmpty(username))
        {
            if (UserInformation.Instance != null)
            {
                UserInformation.Instance.SetPlayerName(username);
            }
            OnLoginSucceeded?.Invoke(username);
        }
        else
        {
            Debug.LogWarning("Username not present in server response.");
            OnLoginFailed?.Invoke("Username not present in server response");
        }
    }

    // 嘗試解析常見兩種結構：{"username":"..."} 或 {"data":{"username":"..."}}
    private string ExtractUsername(string json)
    {
        try
        {
            var simple = JsonUtility.FromJson<LoginResponseSimple>(json);
            if (simple != null && !string.IsNullOrEmpty(simple.username))
            {
                return simple.username;
            }

            var envelope = JsonUtility.FromJson<LoginEnvelope>(json);
            if (envelope != null && envelope.data != null && !string.IsNullOrEmpty(envelope.data.username))
            {
                return envelope.data.username;
            }

            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse login response: {e.Message}");
            return null;
        }
    }
}
