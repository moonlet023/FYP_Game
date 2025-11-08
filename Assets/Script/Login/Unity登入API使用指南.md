# 🎮 Unity 客戶端 - 登入 API 使用指南

## 📋 可用的 API 端點

您的伺服器提供以下登入相關的 API：

### 1. **獲取玩家資料**
- **方法**: `GET`
- **路徑**: `/loginDataBase/{username}`
- **功能**: 根據使用者名稱獲取玩家資料
- **範例**: `GET http://您的IP:6661/loginDataBase/testuser`

### 2. **檢查密碼**
- **方法**: `POST`
- **路徑**: `/loginDataBase/checkPassword`
- **功能**: 驗證使用者名稱和密碼
- **請求格式**: 
```json
{
  "username": "testuser",
  "password": "testpass"
}
```

### 3. **註冊新使用者**
- **方法**: `POST`
- **路徑**: `/loginDataBase/register`
- **功能**: 註冊新的使用者帳號
- **請求格式**: 
```json
{
  "username": "newuser",
  "password": "newpass"
}
```

## 🚀 在 Unity 中的使用步驟

### 步驟 1: 設置場景
1. 創建一個空的 GameObject，命名為 "LoginManager"
2. 將 `LoginApiClient.cs` 腳本附加到此物件
3. 在 Inspector 中設定您的伺服器 IP 地址

### 步驟 2: 配置 API 客戶端
```csharp
// 在 LoginApiClient 的 Inspector 中設定：
serverHost = "192.168.1.100";  // 您的伺服器 IP
httpPort = 6661;               // HTTP 端口
httpsPort = 6660;              // HTTPS 端口
useHttps = false;              // 開發時建議使用 HTTP
```

### 步驟 3: 使用 API

#### 🔐 登入驗證
```csharp
LoginApiClient apiClient = FindObjectOfType<LoginApiClient>();

apiClient.CheckPassword("username", "password", (isValid) => {
    if (isValid) {
        Debug.Log("登入成功！");
        // 執行登入成功的邏輯
    } else {
        Debug.Log("使用者名稱或密碼錯誤");
    }
});
```

#### 📝 註冊新使用者
```csharp
apiClient.RegisterUser("newusername", "newpassword", (success, message) => {
    if (success) {
        Debug.Log("註冊成功！");
    } else {
        Debug.Log($"註冊失敗: {message}");
    }
});
```

#### 🎮 獲取玩家資料
```csharp
apiClient.GetPlayerData("username", (playerData) => {
    if (playerData != null) {
        Debug.Log($"玩家資料: {playerData.username}");
        // 使用玩家資料
    } else {
        Debug.Log("找不到玩家資料");
    }
});
```

## 🎯 完整的登入流程範例

```csharp
public class GameLoginSystem : MonoBehaviour
{
    private LoginApiClient apiClient;
    
    void Start()
    {
        apiClient = FindObjectOfType<LoginApiClient>();
    }
    
    public void LoginPlayer(string username, string password)
    {
        // 步驟 1: 檢查密碼
        apiClient.CheckPassword(username, password, (isValid) => {
            if (isValid)
            {
                // 步驟 2: 獲取完整玩家資料
                apiClient.GetPlayerData(username, (playerData) => {
                    if (playerData != null)
                    {
                        // 步驟 3: 登入成功，初始化遊戲
                        InitializeGame(playerData);
                    }
                });
            }
            else
            {
                ShowLoginError("使用者名稱或密碼錯誤");
            }
        });
    }
    
    private void InitializeGame(LoginResponse playerData)
    {
        Debug.Log($"歡迎回來, {playerData.username}!");
        // 載入遊戲場景或初始化玩家狀態
    }
}
```

## 🛠️ 故障排除

### 404 Not Found 錯誤
- **確保 URL 正確**: 使用 `/loginDataBase/` 而不是 `/loginDatabase/`
- **檢查伺服器狀態**: 確保 .NET API 伺服器正在運行
- **驗證端點**: 確認您訪問的是正確的端點路徑

### 連接問題
1. **檢查 IP 地址**: 確保客戶端中的 IP 地址與伺服器相符
2. **檢查端口**: HTTP (6661) 和 HTTPS (6660)
3. **防火牆設定**: 確保防火牆允許這些端口的連接

### MongoDB 連接問題
- 確保 MongoDB 服務正在運行 (`mongodb://localhost:27017`)
- 檢查資料庫名稱 (`game_db`) 和集合名稱 (`players`)

## 📱 UI 整合範例

如果您想創建登入介面，可以使用提供的 `LoginUIManager.cs`：

1. 創建 Canvas 和 UI 元件 (InputField, Button, Text)
2. 將 `LoginUIManager.cs` 附加到 Canvas 或空物件
3. 在 Inspector 中連接 UI 元件引用
4. 設定 API 客戶端引用

## 🔒 安全性考慮

⚠️ **重要**: 目前的實作適用於開發環境，生產環境需要考慮：

1. **密碼加密**: 不應以明文儲存密碼
2. **HTTPS**: 生產環境應使用 HTTPS
3. **輸入驗證**: 添加使用者輸入的驗證
4. **錯誤處理**: 更完善的錯誤處理機制
5. **會話管理**: 實作 JWT 或其他認證機制

## 📞 API 測試

您可以使用以下工具測試 API：

### 使用 PowerShell 測試
```powershell
# 測試註冊
$body = @{username="testuser"; password="testpass"} | ConvertTo-Json
Invoke-RestMethod -Uri "http://您的IP:6661/loginDataBase/register" -Method Post -Body $body -ContentType "application/json"

# 測試登入
Invoke-RestMethod -Uri "http://您的IP:6661/loginDataBase/checkPassword" -Method Post -Body $body -ContentType "application/json"

# 測試獲取資料
Invoke-RestMethod -Uri "http://您的IP:6661/loginDataBase/testuser" -Method Get
```

## 🎮 遊戲整合建議

1. **玩家資料管理**: 創建一個單例的 PlayerManager 來保存登入後的玩家資訊
2. **場景切換**: 登入成功後自動載入遊戲主場景
3. **離線模式**: 考慮添加離線遊戲模式作為備案
4. **自動登入**: 保存上次成功的登入資訊（注意安全性）

現在您可以在 Unity 客戶端中完整使用您的登入系統了！🚀