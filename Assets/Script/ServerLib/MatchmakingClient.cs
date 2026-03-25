using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class MatchApiClient : MonoBehaviour
{
    [Header("Server")]
    public string baseUrl = "https://pal.moonlet023.com:6661"; // 開發建議先用 http
    public float pollInterval = 1.0f;

    private string _ticketId;
    private Coroutine _pollCoroutine;

    [Serializable]
    public class JoinRequest
    {
        public string uid;
        public string username;
    }

    [Serializable]
    public class PlayerReadyRequest
    {
        public string uid;
        public bool isReady;
    }

    [Serializable]
    public class TicketStatus
    {
        public string ticketId;
        public string uid;
        public string username;
        public string state; // Waiting / Matched / Cancelled
        public string matchId;
        public string roomId;
        public string opponentUid;
        public string opponentUsername;
    }

    public void StartPairing(string uid, string username, Action<TicketStatus> onMatched, Action<string> onError)
    {
        if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
        StartCoroutine(JoinQueue(uid, username, onMatched, onError));
    }

    public void CancelPairing(Action onDone = null)
    {
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
            _pollCoroutine = null;
        }

        if (!string.IsNullOrEmpty(_ticketId))
            StartCoroutine(Delete($"/match/queue/{_ticketId}", _ => onDone?.Invoke(), _ => onDone?.Invoke()));
        else
            onDone?.Invoke();
    }

    public void SetReady(string roomId, string uid, bool isReady, Action<string> onOk, Action<string> onError)
    {
        var body = new PlayerReadyRequest { uid = uid, isReady = isReady };
        StartCoroutine(PostJson($"/match/room/{roomId}/ready", JsonUtility.ToJson(body), onOk, onError));
    }

    public void StartGame(string roomId, Action<string> onOk, Action<string> onError)
    {
        StartCoroutine(PostJson($"/match/room/{roomId}/start", "{}", onOk, onError));
    }

    private IEnumerator JoinQueue(string uid, string username, Action<TicketStatus> onMatched, Action<string> onError)
    {
        var req = new JoinRequest { uid = uid, username = username };
        bool done = false;
        string error = null;
        TicketStatus joinResult = null;

        yield return PostJson(
            "/match/queue",
            JsonUtility.ToJson(req),
            json =>
            {
                joinResult = JsonUtility.FromJson<TicketStatus>(json);
                done = true;
            },
            err =>
            {
                error = err;
                done = true;
            });

        if (!done || error != null || joinResult == null)
        {
            onError?.Invoke(error ?? "join queue failed");
            yield break;
        }

        _ticketId = joinResult.ticketId;

        if (joinResult.state == "Matched")
        {
            onMatched?.Invoke(joinResult);
            yield break;
        }

        _pollCoroutine = StartCoroutine(PollStatus(onMatched, onError));
    }

    private IEnumerator PollStatus(Action<TicketStatus> onMatched, Action<string> onError)
    {
        while (true)
        {
            bool done = false;
            string error = null;
            TicketStatus status = null;

            yield return Get($"/match/status/{_ticketId}",
                json =>
                {
                    status = JsonUtility.FromJson<TicketStatus>(json);
                    done = true;
                },
                err =>
                {
                    error = err;
                    done = true;
                });

            if (!done)
            {
                onError?.Invoke("poll timeout");
                yield break;
            }

            if (error != null)
            {
                onError?.Invoke(error);
                yield break;
            }

            if (status != null && status.state == "Matched")
            {
                onMatched?.Invoke(status);
                yield break;
            }

            if (status != null && status.state == "Cancelled")
            {
                onError?.Invoke("pairing cancelled");
                yield break;
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator Get(string path, Action<string> onOk, Action<string> onErr)
    {
        using var req = UnityWebRequest.Get(baseUrl + path);
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
        else onErr?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler.text}");
    }

    private IEnumerator Delete(string path, Action<string> onOk, Action<string> onErr)
    {
        using var req = UnityWebRequest.Delete(baseUrl + path);
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
        else onErr?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler.text}");
    }

    private IEnumerator PostJson(string path, string json, Action<string> onOk, Action<string> onErr)
    {
        using var req = new UnityWebRequest(baseUrl + path, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
        else onErr?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler.text}");
    }
}

namespace ServerLib
{
    [Serializable]
    public class MatchmakingStatus
    {
        public string ticketId;
        public string uid;
        public string username;
        public string state;
        public string matchId;
        public string roomId;
        public string opponentUid;
        public string opponentUsername;
    }

    [Serializable]
    public class MatchDetail
    {
        public string matchId;
        public string roomId;
        public string playerA;
        public string playerB;
    }

    [Serializable]
    internal class JoinQueueRequest
    {
        public string uid;
        public string username;
    }

    public class MatchmakingClient
    {
        private readonly string _baseUrl;
        private readonly CertificateHandler _certHandler;

        public MatchmakingClient(string baseUrl, CertificateHandler certHandler = null)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _certHandler = certHandler;
        }

        public IEnumerator JoinQueue(string uid, string username, Action<MatchmakingStatus> onSuccess, Action<string> onError)
        {
            var body = new JoinQueueRequest { uid = uid, username = username };
            yield return PostJson("/match/queue", JsonUtility.ToJson(body), json =>
            {
                var status = JsonUtility.FromJson<MatchmakingStatus>(json);
                if (status == null)
                {
                    onError?.Invoke("join queue response parse failed");
                    return;
                }
                onSuccess?.Invoke(status);
            }, onError);
        }

        public IEnumerator PollUntilMatched(
            string ticketId,
            float pollIntervalSeconds,
            float timeoutSeconds,
            Action<MatchmakingStatus> onMatched,
            Action onTimeout,
            Action<string> onError)
        {
            if (string.IsNullOrEmpty(ticketId))
            {
                onError?.Invoke("ticketId is required");
                yield break;
            }

            var elapsed = 0f;
            var wait = new WaitForSeconds(Mathf.Max(0.1f, pollIntervalSeconds));

            while (elapsed < timeoutSeconds)
            {
                bool done = false;
                bool hasError = false;

                yield return Get($"/match/status/{ticketId}", json =>
                {
                    var status = JsonUtility.FromJson<MatchmakingStatus>(json);
                    if (status == null)
                    {
                        hasError = true;
                        onError?.Invoke("poll status parse failed");
                        done = true;
                        return;
                    }

                    if (string.Equals(status.state, "Matched", StringComparison.OrdinalIgnoreCase))
                    {
                        onMatched?.Invoke(status);
                        done = true;
                        return;
                    }

                    if (string.Equals(status.state, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        hasError = true;
                        onError?.Invoke("pairing cancelled");
                        done = true;
                        return;
                    }

                    done = true;
                }, err =>
                {
                    hasError = true;
                    done = true;
                    onError?.Invoke(err);
                });

                if (hasError)
                {
                    yield break;
                }

                if (!done)
                {
                    onError?.Invoke("poll request failed");
                    yield break;
                }

                elapsed += pollIntervalSeconds;
                yield return wait;
            }

            onTimeout?.Invoke();
        }

        public IEnumerator GetMatchDetail(string matchId, Action<MatchDetail> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                onError?.Invoke("matchId is required");
                yield break;
            }

            yield return Get($"/match/{matchId}", json =>
            {
                var detail = JsonUtility.FromJson<MatchDetail>(json);
                if (detail == null)
                {
                    onError?.Invoke("match detail parse failed");
                    return;
                }
                onSuccess?.Invoke(detail);
            }, onError);
        }

        public IEnumerator Cancel(string ticketId, Action onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(ticketId))
            {
                onError?.Invoke("ticketId is required");
                yield break;
            }

            yield return Delete($"/match/queue/{ticketId}", _ => onSuccess?.Invoke(), onError);
        }

        private IEnumerator Get(string path, Action<string> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get(_baseUrl + path);
            req.downloadHandler = new DownloadHandlerBuffer();
            AttachCertificate(req, _baseUrl + path);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                onError?.Invoke(FormatError(req));
            }
        }

        private IEnumerator Delete(string path, Action<string> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Delete(_baseUrl + path);
            req.downloadHandler = new DownloadHandlerBuffer();
            AttachCertificate(req, _baseUrl + path);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                onError?.Invoke(FormatError(req));
            }
        }

        private IEnumerator PostJson(string path, string json, Action<string> onSuccess, Action<string> onError)
        {
            using var req = new UnityWebRequest(_baseUrl + path, "POST");
            var body = Encoding.UTF8.GetBytes(json ?? "{}");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            AttachCertificate(req, _baseUrl + path);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                onError?.Invoke(FormatError(req));
            }
        }

        private void AttachCertificate(UnityWebRequest req, string requestUrl)
        {
            if (_certHandler != null)
            {
                req.certificateHandler = _certHandler;
                return;
            }

            TlsCertConfig.Attach(req, requestUrl);
        }

        private static string FormatError(UnityWebRequest req)
        {
            var body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            return $"{req.responseCode} {req.error} {body}";
        }
    }
}