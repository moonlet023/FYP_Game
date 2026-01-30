using UnityEngine;

/// <summary>
/// 簡單的 API 使用範例 - 展示如何在程式碼中直接使用 API
/// </summary>
public class LoginApiExample : MonoBehaviour
{
    [Header("測試設定")]
    public string testUsername = "testuser";
    public string testPassword = "testpass";
    
    private LoginApiClient apiClient;
    
    void Start()
    {
        // 獲取 API 客戶端
        apiClient = FindObjectOfType<LoginApiClient>();
        
        if (apiClient == null)
        {
            Debug.LogError("找不到 LoginApiClient！");
            return;
        }
        
        Debug.Log("=== 登入 API 測試開始 ===");
    }
    
    void Update()
    {
        // 按鍵測試 (僅在編輯器中)
        if (Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                TestRegister();
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                TestLogin();
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                TestGetPlayerData();
            }
        }
    }
    
    /// <summary>
    /// 測試註冊功能
    /// </summary>
    [ContextMenu("測試註冊")]
    public void TestRegister()
    {
        Debug.Log($"🧪 測試註冊: {testUsername}");
        
        apiClient.RegisterUser(testUsername, testPassword, (success, message) => {
            if (success)
            {
                Debug.Log($"✅ 註冊成功: {message}");
            }
            else
            {
                Debug.Log($"❌ 註冊失敗: {message}");
            }
        });
    }
    
    /// <summary>
    /// 測試登入功能
    /// </summary>
    [ContextMenu("測試登入")]
    public void TestLogin()
    {
        Debug.Log($"🧪 測試登入: {testUsername}");
        
        apiClient.CheckPassword(testUsername, testPassword, (isValid) => {
            if (isValid)
            {
                Debug.Log("✅ 登入成功！");
            }
            else
            {
                Debug.Log("❌ 登入失敗！");
            }
        });
    }
    
    /// <summary>
    /// 測試獲取玩家資料
    /// </summary>
    [ContextMenu("測試獲取玩家資料")]
    public void TestGetPlayerData()
    {
        Debug.Log($"🧪 測試獲取玩家資料: {testUsername}");
        
        apiClient.GetPlayerData(testUsername, (playerData) => {
            if (playerData != null)
            {
                Debug.Log($"✅ 獲取玩家資料成功:");
                Debug.Log($"   ID: {playerData.uid}");
                Debug.Log($"   使用者名稱: {playerData.username}");
                Debug.Log($"   密碼: [已隱藏]");
            }
            else
            {
                Debug.Log("❌ 找不到玩家資料！");
            }
        });
    }
    
    /// <summary>
    /// 完整的登入流程測試
    /// </summary>
    [ContextMenu("完整登入流程測試")]
    public void TestFullLoginFlow()
    {
        Debug.Log("🧪 開始完整登入流程測試");
        
        // 步驟 1: 嘗試註冊
        apiClient.RegisterUser(testUsername, testPassword, (regSuccess, regMessage) => {
            Debug.Log($"註冊結果: {(regSuccess ? "成功" : "失敗")} - {regMessage}");
            
            // 步驟 2: 嘗試登入
            apiClient.CheckPassword(testUsername, testPassword, (loginSuccess) => {
                Debug.Log($"登入結果: {(loginSuccess ? "成功" : "失敗")}");
                
                if (loginSuccess)
                {
                    // 步驟 3: 獲取玩家資料
                    apiClient.GetPlayerData(testUsername, (playerData) => {
                        if (playerData != null)
                        {
                            Debug.Log($"✅ 完整流程測試成功！玩家: {playerData.username}");
                        }
                        else
                        {
                            Debug.Log("❌ 無法獲取玩家資料");
                        }
                    });
                }
            });
        });
    }
    
    void OnGUI()
    {
        if (Application.isEditor)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== API 測試控制台 ===");
            
            if (GUILayout.Button("R - 測試註冊"))
                TestRegister();
            
            if (GUILayout.Button("L - 測試登入"))
                TestLogin();
            
            if (GUILayout.Button("G - 獲取玩家資料"))
                TestGetPlayerData();
            
            if (GUILayout.Button("完整流程測試"))
                TestFullLoginFlow();
                
            GUILayout.EndArea();
        }
    }
}