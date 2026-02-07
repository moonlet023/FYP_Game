using UnityEngine;
using UnityEngine.Networking;

namespace ServerLib
{
    // 共用 TLS 憑證設定與工廠方法：集中指紋釘選
    public static class TlsCertConfig
    {
        // 允許的伺服器憑證 SHA256 指紋（可改由設定檔載入）
        public static readonly string[] AllowedFingerprints = new[]
        {
            "2C:97:2E:87:E3:3B:7A:D3:5C:08:8A:48:F8:28:6F:EC:5C:5B:F6:0F:44:2A:63:4A:2D:47:49:77:AD:50:68:85"
        };

        public static bool TrustSelfSigned = true;

        // 依 URL 判斷是否掛載自訂憑證處理器；若非 https 或未啟用則回傳 null
        public static CertificateHandler CreateHandlerOrNull(string url)
        {
            if (!string.IsNullOrEmpty(url)
                && url.StartsWith("https", System.StringComparison.OrdinalIgnoreCase)
                && TrustSelfSigned
                && AllowedFingerprints != null && AllowedFingerprints.Length > 0)
            {
                Debug.Log($"[TLS] Create handler for {url}. Fingerprints count={AllowedFingerprints.Length}");
                return new UnitySSLCertificateHandler(AllowedFingerprints);
            }
            return null;
        }

        public static void Attach(UnityWebRequest req, string url)
        {
            var handler = CreateHandlerOrNull(url);
            if (handler != null)
            {
                req.certificateHandler = handler;
                Debug.Log($"[TLS] Handler attached to {url}");
            }
            else
            {
                Debug.LogWarning($"[TLS] No handler attached (non-HTTPS or disabled). URL={url}");
            }
        }
    }
}