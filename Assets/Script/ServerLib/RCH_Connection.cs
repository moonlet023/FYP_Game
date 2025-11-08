using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Security.Cryptography.X509Certificates;

namespace ServerLib.RCH_Connection
{
    public class RCH_Connection
    {
        [System.Serializable] public class Req { public string username; public string password; }
        [System.Serializable] public class Resp { public bool ok; public string error; }

        string baseUrl = "https://pal.moonlet023.com:6660";

        public IEnumerator StartRegister(string u, string p, Action<Resp> callback = null) => Post("/api/auth/register", new Req { username = u, password = p }, callback);
        public IEnumerator StartLogin(string u, string p, Action<Resp> callback = null) => Post("/api/auth/login", new Req { username = u, password = p }, callback);

        public IEnumerator testServer(Req bodyObj, Action<Resp> callback) => Post("/weather/test", bodyObj, callback);
        IEnumerator Post(string path, Req bodyObj, Action<Resp> callback)
        {
            string json = JsonUtility.ToJson(bodyObj);
            using var uw = new UnityWebRequest(baseUrl + path, "POST");
            uw.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            uw.downloadHandler = new DownloadHandlerBuffer();
            uw.SetRequestHeader("Content-Type", "application/json");

            // const string KNOWN_THUMBPRINT = "2C43330EBE0BBD5CF541599388BC2A8D9D10C829";
            // uw.certificateHandler = new AcceptCertByThumbprint(KNOWN_THUMBPRINT);
            uw.certificateHandler = new AcceptAllCerts();

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

            callback?.Invoke(resp);
            Debug.Log("resp: " + uw.downloadHandler.text);
        }

        // 新增：測試 GET 請求
        public IEnumerator TestServerGet(Action<string> callback = null) => GetTest("/", callback);
        
        IEnumerator GetTest(string path, Action<string> callback)
        {
            using var uw = UnityWebRequest.Get(baseUrl + path);
            uw.certificateHandler = new AcceptAllCerts();
            
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
    }
}