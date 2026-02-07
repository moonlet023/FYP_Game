using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Security.Cryptography.X509Certificates;
using ServerLib;

namespace ServerLib.RCH_Connection
{
    public class RCH_Connection
    {
        [System.Serializable] public class Req { public string username; public string password; public string ticketid; }
        [System.Serializable] public class Resp { public bool ok; public string error; public string token; public string accessToken; public string jwt; }

        string baseUrl = "https://pal.moonlet023.com:6660";
        // 指紋釘選：伺服器憑證 SHA256 指紋（可改為從設定載入）
        static readonly string[] s_allowedFingerprints = new[]
        {
            "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85"
        };

        // 靜態認證資料，跨所有 RCH_Connection 實例維持
        static string s_authCookie;
        static string s_bearerToken;

        public IEnumerator StartRegister(string u, string p, Action<Resp> callback = null) => Post("/api/auth/register", new Req { username = u, password = p }, callback);
        public IEnumerator StartLogin(string u, string p, Action<Resp> callback = null) => Post("/api/auth/login", new Req { username = u, password = p }, callback);

        public IEnumerator testServer(Req bodyObj, Action<Resp> callback) => Post("/weather/test", bodyObj, callback);

        public IEnumerator JoinQueue(string u, Action<Resp> callback = null)
        {
            var finalUser = ResolveUsername(u);
            if (string.IsNullOrEmpty(finalUser))
            {
                Debug.LogError("JoinQueue: username is empty. Please login or set UserInformation.Instance.SetPlayerName().");
                callback?.Invoke(new Resp { ok = false, error = "username is required" });
                yield break;
            }
            yield return Post("/match/queue", new Req { username = finalUser }, callback);
        }
        public IEnumerator Cancel(string u, Action<Resp> callback = null) => Delete("/match/queue", new Req { ticketid = u }, callback);
        

        public IEnumerator StartGet(string path, Action<Resp> callback = null) => Get(path, callback);

        IEnumerator Post(string path, Req bodyObj, Action<Resp> callback)
        {
            string json = JsonUtility.ToJson(bodyObj);
            using var uw = new UnityWebRequest(baseUrl + path, "POST");
            uw.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            uw.downloadHandler = new DownloadHandlerBuffer();
            uw.SetRequestHeader("Content-Type", "application/json");

            // 若已有認證資訊則自動附加
            if (!string.IsNullOrEmpty(s_bearerToken))
                uw.SetRequestHeader("Authorization", "Bearer " + s_bearerToken);
            if (!string.IsNullOrEmpty(s_authCookie))
                uw.SetRequestHeader("Cookie", s_authCookie);

            // 掛載共用憑證處理器（嚴格指紋釘選）
            TlsCertConfig.Attach(uw, baseUrl + path);

            UnityWebRequestAsyncOperation op = null;
            try
            {
                op = uw.SendWebRequest();
            }
            catch (InvalidOperationException invEx)
            {
                Debug.LogError("SendWebRequest threw: " + invEx.Message);
                yield break;
            }

            yield return op;

            Debug.Log($"ResponseCode: {uw.responseCode}, Error: {uw.error}");
            Debug.Log($"Raw response: {uw.downloadHandler?.text}");

    #if UNITY_2020_1_OR_NEWER
            if (uw.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Request error: " + uw.error + "  Response: " + uw.downloadHandler.text);
                yield break;
            }
    #else
            if (uw.isNetworkError || uw.isHttpError)
            {
                Debug.LogError("Request error: " + uw.error + "  Response: " + uw.downloadHandler.text);
                yield break;
            }
    #endif
            Resp resp = null;
            try { resp = JsonUtility.FromJson<Resp>(uw.downloadHandler.text); }
            catch (Exception e) { Debug.LogError("Failed parse resp: " + e.Message); }

            // 擷取認證（Set-Cookie / token）
            CaptureAuthFromResponse(uw, resp);

            callback?.Invoke(resp);
            Debug.Log("resp: " + uw.downloadHandler.text);
        }

        IEnumerator Delete(string path, Req bodyObj, Action<Resp> callback)
        {
            string json = bodyObj != null ? JsonUtility.ToJson(bodyObj) : null;

            using var uw = new UnityWebRequest(baseUrl + path, "DELETE");
            if (!string.IsNullOrEmpty(json))
            {
                uw.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                uw.SetRequestHeader("Content-Type", "application/json");
            }
            uw.downloadHandler = new DownloadHandlerBuffer();

            TlsCertConfig.Attach(uw, baseUrl + path);

            if (!string.IsNullOrEmpty(s_bearerToken))
                uw.SetRequestHeader("Authorization", "Bearer " + s_bearerToken);
            if (!string.IsNullOrEmpty(s_authCookie))
                uw.SetRequestHeader("Cookie", s_authCookie);

            UnityWebRequestAsyncOperation op = null;
            try
            {
                op = uw.SendWebRequest();
            }
            catch (InvalidOperationException invEx)
            {
                Debug.LogError("SendWebRequest threw: " + invEx.Message);
                yield break;
            }

            yield return op;

            Debug.Log($"DELETE ResponseCode: {uw.responseCode}, Error: {uw.error}");
            Debug.Log($"DELETE Raw response: {uw.downloadHandler?.text}");

#if UNITY_2020_1_OR_NEWER
            if (uw.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Request error: " + uw.error + "  Response: " + uw.downloadHandler.text);
                yield break;
            }
#else
            if (uw.isNetworkError || uw.isHttpError)
            {
                Debug.LogError("Request error: " + uw.error + "  Response: " + uw.downloadHandler.text);
                yield break;
            }
#endif
            Resp resp = null;
            try { resp = JsonUtility.FromJson<Resp>(uw.downloadHandler.text); }
            catch (Exception e) { Debug.LogError("Failed parse resp: " + e.Message); }

            CaptureAuthFromResponse(uw, resp);

            callback?.Invoke(resp);
        }

        IEnumerator Get(string path, Action<Resp> callback)
        {
            using var uw = UnityWebRequest.Get(baseUrl + path);
            uw.downloadHandler = new DownloadHandlerBuffer();
            TlsCertConfig.Attach(uw, baseUrl + path);

            if (!string.IsNullOrEmpty(s_bearerToken))
                uw.SetRequestHeader("Authorization", "Bearer " + s_bearerToken);
            if (!string.IsNullOrEmpty(s_authCookie))
                uw.SetRequestHeader("Cookie", s_authCookie);

            UnityWebRequestAsyncOperation op = null;
            try
            {
                op = uw.SendWebRequest();
            }
            catch (InvalidOperationException invEx)
            {
                Debug.LogError("SendWebRequest threw: " + invEx.Message);
                yield break;
            }

            yield return op;

            Debug.Log($"GET ResponseCode: {uw.responseCode}, Error: {uw.error}");
            Debug.Log($"GET Raw response: {uw.downloadHandler?.text}");

#if UNITY_2020_1_OR_NEWER
            if (uw.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Request error: " + uw.error + "  Response: " + uw.downloadHandler.text);
                yield break;
            }
#else
            if (uw.isNetworkError || uw.isHttpError)
            {
                Debug.LogError("Request error: " + uw.error + "  Response: " + uw.downloadHandler.text);
                yield break;
            }
#endif
            Resp resp = null;
            try { resp = JsonUtility.FromJson<Resp>(uw.downloadHandler.text); }
            catch (Exception e) { Debug.LogError("Failed parse resp: " + e.Message); }

            CaptureAuthFromResponse(uw, resp);

            callback?.Invoke(resp);
        }

        

        // 新增：測試 GET 請求
        public IEnumerator TestServerGet(Action<string> callback = null) => GetTest("/", callback);
        
        IEnumerator GetTest(string path, Action<string> callback)
        {
            using var uw = UnityWebRequest.Get(baseUrl + path);
            TlsCertConfig.Attach(uw, baseUrl + path);
            
            yield return uw.SendWebRequest();
            
            Debug.Log($"GET ResponseCode: {uw.responseCode}, Error: {uw.error}");
            Debug.Log($"GET Raw response: {uw.downloadHandler?.text}");
            
            if (uw.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(uw.downloadHandler.text);
            }
        }

        // 接受指定 thumbprint 的憑證（較安全）
        class AcceptCertByThumbprint : CertificateHandler
        {
            readonly string expectedThumb; // normalized (no ':' and uppercase)

            public AcceptCertByThumbprint(string thumbprint)
            {
                expectedThumb = NormalizeThumb(thumbprint);
            }

            protected override bool ValidateCertificate(byte[] certificateData)
            {
                try
                {
                    var cert = new X509Certificate2(certificateData);
                    var tp = NormalizeThumb(cert.Thumbprint);
                    Debug.Log($"Server cert thumbprint: {tp}");
                    return tp == expectedThumb;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ValidateCertificate error: " + e.Message);
                    return false;
                }
            }

            static string NormalizeThumb(string t) => (t ?? "").Replace(":", "").Replace(" ", "").ToUpperInvariant();
        }

        // 臨時測試用：接受所有憑證
        class AcceptAllCerts : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] cert) => true;
        }

        // 從回應擷取 Cookie 或 Token 並保存到靜態欄位（跨實例沿用）
        static void CaptureAuthFromResponse(UnityWebRequest uw, Resp resp)
        {
            try
            {
                var headers = uw.GetResponseHeaders();
                if (headers != null && headers.TryGetValue("Set-Cookie", out var setCookie))
                {
                    // 取第一段 cookie（name=value），忽略屬性
                    var idx = setCookie.IndexOf(';');
                    s_authCookie = idx >= 0 ? setCookie.Substring(0, idx) : setCookie;
                    Debug.Log("Captured auth cookie: " + s_authCookie);
                }

                var tokenCandidate = resp?.token ?? resp?.accessToken ?? resp?.jwt;
                if (!string.IsNullOrEmpty(tokenCandidate))
                {
                    s_bearerToken = tokenCandidate;
                    Debug.Log("Captured bearer token.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("CaptureAuthFromResponse error: " + e.Message);
            }
        }

        // 嘗試從傳入值或本地保存資料取得使用者名稱
        static string ResolveUsername(string u)
        {
            if (!string.IsNullOrEmpty(u)) return u;
            try
            {
                return UserInformation.Instance != null ? UserInformation.Instance.PlayerName : null;
            }
            catch
            {
                return null;
            }
        }
    }
}