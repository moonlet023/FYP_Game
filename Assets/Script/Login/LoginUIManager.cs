using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUIManager : MonoBehaviour
{
    [Header("UI 元件")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public Button registerButton;
    public Text statusText;
    public RawImage serverStatusIndicator;
    public GameObject mainmenuUI;
    public GameObject loginUI;
    
    [Header("API 客戶端")]
    public LoginApiClient apiClient;
    
    void Start()
    {
        serverStatusIndicator.gameObject.SetActive(false);

        // 先確保有 API 客戶端，避免 baseUrl 尚未初始化或引用為空
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<LoginApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("找不到 LoginApiClient！請確保場景中有此元件。");
            }
        }

        // 設定按鈕事件
        loginButton.onClick.AddListener(OnLoginButtonClicked);
        registerButton.onClick.AddListener(OnRegisterButtonClicked);

        // 再測試連線，避免使用未初始化的 baseUrl
        TestConnection();

        SetStatusText("請輸入使用者名稱和密碼");
    }
    
    /// <summary>
    /// 登入按鈕點擊事件
    /// </summary>
    public void OnLoginButtonClicked()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetStatusText("❌ 請輸入使用者名稱和密碼");
            return;
        }
        
        SetStatusText("🔍 檢查登入資訊...");
        SetButtonsEnabled(false);
        
        // 檢查密碼
        apiClient.CheckPassword(username, password, OnPasswordCheckResult);
    }
    
    /// <summary>
    /// 密碼檢查結果回調
    /// </summary>
    private void OnPasswordCheckResult(bool isValid)
    {
        SetButtonsEnabled(true);
        
        if (isValid)
        {
            SetStatusText("✅ 登入成功！");
            Debug.Log("登入成功！");
            
            // 這裡可以添加登入成功後的邏輯
            OnLoginSuccess();
        }
        else
        {
            SetStatusText("❌ 使用者名稱或密碼錯誤");
        }
    }
    
    /// <summary>
    /// 註冊按鈕點擊事件
    /// </summary>
    public void OnRegisterButtonClicked()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetStatusText("❌ 請輸入使用者名稱和密碼");
            return;
        }
        
        if (password.Length < 4)
        {
            SetStatusText("❌ 密碼至少需要 4 個字元");
            return;
        }
        
        SetStatusText("📝 註冊中...");
        SetButtonsEnabled(false);
        
        // 註冊新使用者
        apiClient.RegisterUser(username, password, OnRegisterResult);
    }
    
    /// <summary>
    /// 註冊結果回調
    /// </summary>
    private void OnRegisterResult(bool success, string message)
    {
        SetButtonsEnabled(true);
        
        if (success)
        {
            SetStatusText("✅ 註冊成功！現在可以登入了");
            Debug.Log("註冊成功！");
        }
        else
        {
            SetStatusText($"❌ {message}");
        }
    }
    
    /// <summary>
    /// 登入成功後的處理
    /// </summary>
    private void OnLoginSuccess()
    {
        // 這裡添加登入成功後的邏輯
        // 例如：切換到遊戲場景、載入玩家資料等
        
        // 獲取完整的玩家資料
        string username = usernameInput.text.Trim();
        apiClient.GetPlayerData(username, OnPlayerDataReceived);
        mainmenuUI.SetActive(true);
        loginUI.SetActive(false);
    }
    
    /// <summary>
    /// 玩家資料接收回調
    /// </summary>
    private void OnPlayerDataReceived(LoginResponse playerData)
    {
        if (playerData != null)
        {
            Debug.Log($"載入玩家資料: {playerData.username}");
            // 這裡可以將玩家資料保存到遊戲管理器中
        }
    }
    
    /// <summary>
    /// 設定狀態文字
    /// </summary>
    private void SetStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
        Debug.Log($"狀態: {text}");
    }
    
    /// <summary>
    /// 設定按鈕啟用狀態
    /// </summary>
    private void SetButtonsEnabled(bool enabled)
    {
        if (loginButton != null) loginButton.interactable = enabled;
        if (registerButton != null) registerButton.interactable = enabled;
    }
    
    /// <summary>
    /// 測試連接按鈕 (可選)
    /// </summary>
    public void TestConnection()
    {
        SetStatusText("🔗 測試連接...");
        
        apiClient.TestConnection((ok, msg) => {
            try
            {
                if (!ok)
                {
                    SetStatusText("❌ 伺服器連接異常");
                    OnServerConnectionFailed();
                }
                else
                {
                    SetStatusText("✅ 伺服器連接正常");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"測試連接時發生例外: {ex.Message}");
                SetStatusText("❌ 伺服器連接異常");
                OnServerConnectionFailed();
            }
        });
    }

    //若鏈接伺服器失敗 則游戲畫面轉為維護中
    public void OnServerConnectionFailed()
    {
        // 更新狀態文字
        SetStatusText("❌ 伺服器連接失敗，系統維護中");
        serverStatusIndicator.gameObject.SetActive(true);
    }
}