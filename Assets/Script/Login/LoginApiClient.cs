using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class LoginApiClient : MonoBehaviour
{
    [Header("伺服器設定")]
    public string serverHost = "pal.moonlet023.com"; // 替換為您的伺服器 IP
    public int httpPort = 6661;
    public int httpsPort = 6660;
    public bool useHttps = false;

    private string baseUrl;

    void Awake()
    {
        BuildBaseUrl();
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
            if (useHttps)
            {
                request.certificateHandler = new CustomCertificateHandler();
            }
            
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
            
            if (useHttps)
            {
                request.certificateHandler = new CustomCertificateHandler();
            }
            
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
            
            if (useHttps)
            {
                request.certificateHandler = new CustomCertificateHandler();
            }
            
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
            if (useHttps)
            {
                request.certificateHandler = new CustomCertificateHandler();
            }
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