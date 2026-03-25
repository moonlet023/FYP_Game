using System;
using ServerLib;

public static class MatchmakingSessionBridge
{
    public static MatchmakingStatus JoinStatus { get; private set; }
    public static MatchmakingStatus MatchedStatus { get; private set; }
    public static MatchDetail Detail { get; private set; }
    public static string LastError { get; private set; }
    public static DateTime UpdatedAtUtc { get; private set; }

    public static void Clear()
    {
        JoinStatus = null;
        MatchedStatus = null;
        Detail = null;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static void SetJoinStatus(MatchmakingStatus status)
    {
        JoinStatus = CloneStatus(status);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static void SetMatchedStatus(MatchmakingStatus status)
    {
        MatchedStatus = CloneStatus(status);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static void SetDetail(MatchDetail detail)
    {
        Detail = CloneDetail(detail);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static void SetError(string error)
    {
        LastError = error;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static MatchmakingStatus CloneStatus(MatchmakingStatus src)
    {
        if (src == null) return null;

        return new MatchmakingStatus
        {
            ticketId = src.ticketId,
            uid = src.uid,
            username = src.username,
            state = src.state,
            matchId = src.matchId,
            roomId = src.roomId,
            opponentUid = src.opponentUid,
            opponentUsername = src.opponentUsername
        };
    }

    private static MatchDetail CloneDetail(MatchDetail src)
    {
        if (src == null) return null;

        return new MatchDetail
        {
            matchId = src.matchId,
            roomId = src.roomId,
            playerA = src.playerA,
            playerB = src.playerB
        };
    }
}
