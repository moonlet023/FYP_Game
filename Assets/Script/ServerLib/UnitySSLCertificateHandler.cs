using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine.Networking;

// 開發用：自訂憑證驗證處理器
// 建議：正式環境請使用正確 CN/SAN 的憑證；此處理器僅用於自簽或測試環境。
public class UnitySSLCertificateHandler : CertificateHandler
{
    private readonly HashSet<string> _allowedFingerprints; // 以 SHA256 指紋白名單方式驗證

    /// <summary>
    /// 建構時可提供允許的憑證 SHA256 指紋（不含分隔符、大小寫不敏感）。
    /// 若未提供（或集合為空），將接受所有憑證（開發測試用，安全性低）。
    /// </summary>
    public UnitySSLCertificateHandler(IEnumerable<string> allowedFingerprints = null)
    {
        _allowedFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (allowedFingerprints != null)
        {
            foreach (var fp in allowedFingerprints)
            {
                var norm = NormalizeFingerprint(fp);
                if (!string.IsNullOrEmpty(norm)) _allowedFingerprints.Add(norm);
            }
        }
    }

    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // 無白名單時，為了開發便利直接接受（不安全，僅供測試）
        if (_allowedFingerprints == null || _allowedFingerprints.Count == 0)
            return true;

        var fp = ComputeSha256Fingerprint(certificateData);
        return _allowedFingerprints.Contains(fp);
    }

    private static string ComputeSha256Fingerprint(byte[] data)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToUpperInvariant();
        }
    }

    private static string NormalizeFingerprint(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        return s.Replace(":", string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();
    }
}
