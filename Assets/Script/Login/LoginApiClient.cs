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
    void Start()
    {
        // 設定基礎 URL
        string protocol = useHttps ? "https" : "http";
        int port = useHttps ? httpsPort : httpPort;
        baseUrl = $"{protocol}://{serverHost}:{port}";
        
        Debug.Log($"API 客戶端已初始化: {baseUrl}");
    }

    /// <summary>
    /// 獲取玩家資料
    /// </summary>
    /// <param name="username">使用者名稱</param>
    /// <param name="callback">回調函數</param>
    public void GetPlayerData(string username, System.Action<LoginResponse> callback)
    {
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
                Debug.LogError($"❌ 獲取玩家資料失敗: {request.error}");
                Debug.LogError($"HTTP 狀態碼: {request.responseCode}");
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
                bool isValid = bool.Parse(request.downloadHandler.text);
                callback?.Invoke(isValid);
            }
            else
            {
                Debug.LogError($"❌ 密碼檢查失敗: {request.error}");
                Debug.LogError($"HTTP 狀態碼: {request.responseCode}");
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
                callback?.Invoke(true, request.downloadHandler.text);
            }
            else if (request.responseCode == 409) // Conflict - 使用者名稱已存在
            {
                Debug.LogWarning($"⚠️ 使用者名稱已存在: {request.downloadHandler.text}");
                callback?.Invoke(false, "使用者名稱已存在");
            }
            else
            {
                Debug.LogError($"❌ 註冊失敗: {request.error}");
                Debug.LogError($"HTTP 狀態碼: {request.responseCode}");
                callback?.Invoke(false, $"註冊失敗: {request.error}");
            }
        }
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