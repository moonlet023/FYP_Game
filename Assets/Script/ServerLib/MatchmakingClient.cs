using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// 簡易配對 API 客戶端（Unity）
// 依賴伺服器端端點：
// POST   /match/queue               -> 建立 ticket（加入佇列或立即配對）
// GET    /match/status/{ticketId}   -> 查詢 ticket 狀態（Waiting/Matched/Cancelled）
// GET    /match/detail/{matchId}    -> 取得配對雙方資訊
// DELETE /match/queue/{ticketId}    -> 取消佇列
// 若使用自簽 HTTPS 憑證，請搭配專案內的 Unity-SSLCertificateHandler.cs

namespace MyGame.Client
{
    [Serializable]
    public class JoinRequest
    {
        public string uid;       // 用於配對
        public string username;  // 可選，用於顯示
    }

    [Serializable]
    public class TicketStatusDto
    {
        public string ticketId;
        public string uid;
        public string username;      // optional
        public int state;       // 0=Waiting, 1=Matched, 2=Cancelled
        public string matchId;  // 可能為 null/空字串
        public string opponentUid;     // 可能為 null/空字串
        public string opponentUsername; // 可能為 null/空字串
    }

    [Serializable]
    public class MatchRecordDto
    {
        public string matchId;
        public string playerA;
        public string playerB;
        public string matchedAt; // ISO 字串
    }

    public class MatchmakingClient
    {
        private readonly string _baseUrl; // e.g. "https://localhost:6660"
        private readonly CertificateHandler _certHandler; // 可為 null

        public MatchmakingClient(string baseUrl, CertificateHandler certHandler = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _certHandler = certHandler;
        }

        public IEnumerator JoinQueue(string uid, string username, Action<TicketStatusDto> onSuccess, Action<string> onError)
        {
            var reqBody = JsonUtility.ToJson(new JoinRequest { uid = uid, username = username });
            var url = _baseUrl + "/match/queue";

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(reqBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Accept", "application/json");
                if (_certHandler != null)
                {
                    req.certificateHandler = _certHandler;
                    Debug.Log($"[TLS] Handler attached (client-provided) to {url}");
                }

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var dto = JsonUtility.FromJson<TicketStatusDto>(req.downloadHandler.text);
                        onSuccess?.Invoke(dto);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"JoinQueue parse error: {ex.Message}. Body=\n{req.downloadHandler?.text}");
                    }
                }
                else
                {
                    var body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                    onError?.Invoke($"JoinQueue error: {req.responseCode} {req.error}\nBody=\n{body}");
                }
            }
        }

        public IEnumerator GetStatus(string ticketId, Action<TicketStatusDto> onSuccess, Action<string> onError)
        {
            var url = _baseUrl + "/match/status/" + UnityWebRequest.EscapeURL(ticketId);
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (_certHandler != null)
                {
                    req.certificateHandler = _certHandler;
                    Debug.Log($"[TLS] Handler attached (client-provided) to {url}");
                }

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var dto = JsonUtility.FromJson<TicketStatusDto>(req.downloadHandler.text);
                        onSuccess?.Invoke(dto);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"GetStatus parse error: {ex.Message}. Body=\n{req.downloadHandler?.text}");
                    }
                }
                else
                {
                    var body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                    onError?.Invoke($"GetStatus error: {req.responseCode} {req.error}\nBody=\n{body}");
                }
            }
        }

        public IEnumerator GetMatchDetail(string matchId, Action<MatchRecordDto> onSuccess, Action<string> onError)
        {
            var url = _baseUrl + "/match/detail/" + UnityWebRequest.EscapeURL(matchId);
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (_certHandler != null)
                {
                    req.certificateHandler = _certHandler;
                    Debug.Log($"[TLS] Handler attached (client-provided) to {url}");
                }

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var dto = JsonUtility.FromJson<MatchRecordDto>(req.downloadHandler.text);
                        onSuccess?.Invoke(dto);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"GetMatchDetail parse error: {ex.Message}. Body=\n{req.downloadHandler?.text}");
                    }
                }
                else
                {
                    var body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                    onError?.Invoke($"GetMatchDetail error: {req.responseCode} {req.error}\nBody=\n{body}");
                }
            }
        }

        public IEnumerator Cancel(string ticketId, Action onSuccess, Action<string> onError)
        {
            var url = _baseUrl + "/match/queue/" + UnityWebRequest.EscapeURL(ticketId);
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbDELETE))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (_certHandler != null)
                {
                    req.certificateHandler = _certHandler;
                    Debug.Log($"[TLS] Handler attached (client-provided) to {url}");
                }

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success || req.responseCode == 204)
                {
                    onSuccess?.Invoke();
                }
                else
                {
                    onError?.Invoke($"Cancel error: {req.responseCode} {req.error}");
                }
            }
        }

        // 便利方法：持續輪詢直到配對完成或逾時
        public IEnumerator PollUntilMatched(string ticketId, float intervalSeconds, float timeoutSeconds,
            Action<TicketStatusDto> onMatched, Action onTimeout, Action<string> onError)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                bool done = false;
                yield return GetStatus(ticketId, status =>
                {
                    if (status.state == 1) // Matched
                    {
                        onMatched?.Invoke(status);
                        done = true;
                    }
                }, err =>
                {
                    onError?.Invoke(err);
                    done = true;
                });

                if (done) yield break;

                yield return new WaitForSeconds(intervalSeconds);
                elapsed += intervalSeconds;
            }

            onTimeout?.Invoke();
        }
    }
}
