using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using ServerLib;

public class LoginApiClient : MonoBehaviour
{
    [Header("伺服器設定")]
    public string serverHost = "pal.moonlet023.com"; // 替換為您的伺服器 IP
    public int httpPort = 6661;
    public int httpsPort = 6660;
    public bool useHttps = true;
    [Header("TLS/憑證設定")]
    [Tooltip("開發/測試用：信任自簽或不被系統信任的憑證。正式環境請使用可信 CA 憑證並關閉此選項。")]
    public bool trustSelfSignedCertificate = true;
    [Tooltip("可選：允許的伺服器憑證 SHA256 指紋（不含冒號與破折號）。若留空且 trustSelfSignedCertificate=true，將接受所有憑證（僅供開發測試）。")]
    public string[] allowedFingerprints = new[] { "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85" };

    private string baseUrl;

    void Awake()
    {
        BuildBaseUrl();
        // 若序列化導致 Inspector 為空，保底填入指紋以啟用釘選
        if (useHttps && trustSelfSignedCertificate && (allowedFingerprints == null || allowedFingerprints.Length == 0))
        {
            allowedFingerprints = new[] { "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85" };
        }
    }

    void Start()
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            BuildBaseUrl();
        }
        Debug.Log($"API 客戶端已初始化: {baseUrl}");
    }

    private void BuildBaseUrl()
    {
        string protocol = useHttps ? "https" : "http";
        int port = useHttps ? httpsPort : httpPort;
        baseUrl = $"{protocol}://{serverHost}:{port}";
    }

    /// <summary>
    /// 獲取玩家資料
    /// </summary>
    /// <param name="username">使用者名稱</param>
    /// <param name="callback">回調函數</param>
    public void GetPlayerData(string username, System.Action<LoginResponse> callback)
    {
        EnsureBaseUrl();
        StartCoroutine(GetPlayerDataCoroutine(username, callback));
    }
    
    private IEnumerator GetPlayerDataCoroutine(string username, System.Action<LoginResponse> callback)
    {
        string url = $"{baseUrl}/loginDataBase/{username}";
        Debug.Log($"🔍 獲取玩家資料: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // 共用釘選策略
            TlsCertConfig.Attach(request, url);
            
            request.timeout = 10;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 成功獲取玩家資料: {request.downloadHandler.text}");
                LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
                callback?.Invoke(response);
            }
            else
            {
                LogRequestFailure(request, "GetPlayerData");
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// 檢查密碼
    /// </summary>
    /// <param name="username">使用者名稱</param>
    /// <param name="password">密碼</param>
    /// <param name="callback">回調函數 (bool: 密碼是否正確)</param>
    public void CheckPassword(string username, string password, System.Action<bool> callback)
    {
        EnsureBaseUrl();
        StartCoroutine(CheckPasswordCoroutine(username, password, callback));
    }
    
    private IEnumerator CheckPasswordCoroutine(string username, string password, System.Action<bool> callback)
    {
        string url = $"{baseUrl}/loginDataBase/checkPassword";
        Debug.Log($"🔐 檢查密碼: {url}");
        
        PlayerData loginData = new PlayerData(username, password);
        string jsonData = JsonUtility.ToJson(loginData);
        Debug.Log($"發送資料: {jsonData}");
        
        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            TlsCertConfig.Attach(request, url);
            
            request.timeout = 10;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 密碼檢查成功: {request.downloadHandler.text}");
                bool isValid = false;
                try
                {
                    // 嘗試解析 { ok: bool, error: string }
                    var resp = JsonUtility.FromJson<AuthResp>(request.downloadHandler.text);
                    isValid = resp != null && resp.ok;
                }
                catch { }
                // 若不是 JSON，嘗試直接解析為 bool
                if (!isValid)
                {
                    bool parsed;
                    if (bool.TryParse(request.downloadHandler.text, out parsed))
                        isValid = parsed;
                }
                callback?.Invoke(isValid);
            }
            else
            {
                LogRequestFailure(request, "CheckPassword");
                callback?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// 註冊新使用者
    /// </summary>
    /// <param name="username">使用者名稱</param>
    /// <param name="password">密碼</param>
    /// <param name="callback">回調函數 (bool: 註冊是否成功, string: 訊息)</param>
    public void RegisterUser(string username, string password, System.Action<bool, string> callback)
    {
        EnsureBaseUrl();
        StartCoroutine(RegisterUserCoroutine(username, password, callback));
    }
    
    private IEnumerator RegisterUserCoroutine(string username, string password, System.Action<bool, string> callback)
    {
        string url = $"{baseUrl}/loginDataBase/register";
        Debug.Log($"📝 註冊使用者: {url}");
        
        PlayerData newPlayer = new PlayerData(username, password);
        string jsonData = JsonUtility.ToJson(newPlayer);
        Debug.Log($"發送資料: {jsonData}");
        
        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            TlsCertConfig.Attach(request, url);
            
            request.timeout = 10;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 註冊成功: {request.downloadHandler.text}");
                string msg = request.downloadHandler.text;
                try
                {
                    var resp = JsonUtility.FromJson<AuthResp>(msg);
                    if (resp != null)
                    {
                        callback?.Invoke(resp.ok, string.IsNullOrEmpty(resp.error) ? msg : resp.error);
                        yield break;
                    }
                }
                catch { }
                callback?.Invoke(true, msg);
            }
            else if (request.responseCode == 409) // Conflict - 使用者名稱已存在
            {
                Debug.LogWarning($"⚠️ 使用者名稱已存在: {request.downloadHandler.text}");
                callback?.Invoke(false, "使用者名稱已存在");
            }
            else
            {
                LogRequestFailure(request, "RegisterUser");
                callback?.Invoke(false, $"註冊失敗: {request.error}");
            }
        }
    }

    // 新增：伺服器連線測試（使用既有 /weather/test 路由）
    public void TestConnection(System.Action<bool, string> callback)
    {
        EnsureBaseUrl();
        StartCoroutine(TestConnectionCoroutine(callback));
    }

    private IEnumerator TestConnectionCoroutine(System.Action<bool, string> callback)
    {
        string url = $"{baseUrl}/weather/test";
        Debug.Log($"🔗 測試連接: {url}");
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            TlsCertConfig.Attach(request, url);
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 伺服器連接正常: {request.downloadHandler.text}");
                callback?.Invoke(true, request.downloadHandler.text);
            }
            else
            {
                LogRequestFailure(request, "TestConnection");
                callback?.Invoke(false, request.error);
            }
        }
    }

    // 對齊 RCH_Connection 的回應格式
    [Serializable]
    private class AuthResp { public bool ok; public string error; }

    private void EnsureBaseUrl()
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            Debug.LogWarning("baseUrl 尚未初始化，嘗試重新建立。");
            BuildBaseUrl();
        }
    }

    private void LogRequestFailure(UnityWebRequest request, string tag)
    {
        var headers = request.GetResponseHeaders();
        var headerDump = headers == null ? "<no headers>" : string.Join("; ", System.Linq.Enumerable.Select(headers, kv => kv.Key + ": " + kv.Value));
        var bodyPreview = request.downloadHandler != null ? request.downloadHandler.text : "<no body>";
        if (!string.IsNullOrEmpty(bodyPreview) && bodyPreview.Length > 500) bodyPreview = bodyPreview.Substring(0, 500) + "...";

        Debug.LogError($"[{tag}] 請求失敗\nURL: {request.url}\nResult: {request.result}\nError: {request.error}\nHTTP 狀態碼: {request.responseCode}\nHeaders: {headerDump}\nBody: {bodyPreview}");
    }
}

// 自定義憑證處理器 - 用於 HTTPS 連接
public class CustomCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("接受開發環境 SSL 憑證");
        return true;
        #else
        return false;
        #endif
    }
}