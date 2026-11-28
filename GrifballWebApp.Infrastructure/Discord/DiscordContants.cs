using GrifballWebApp.Domain.LateLeague;

namespace GrifballWebApp.Infrastructure.Discord;

/// <summary>
/// All customIds used by buttons. Centralized to avoid conflicts and make it easy to find where they are used.
/// </summary>
public class DiscordButtonContants
{
    public const string SetGamertag = "set_gamertag";
    public const string JoinQueue = "join_queue";
    public const string LeaveQueue = "leave_queue";
    

    public const string VoteForWinner = "voteforwinner";
    public static string VoteForWinnerWithParams(int matchId, WinnerVote winnerVote)
    {
        return $"{VoteForWinner}:{matchId}:{winnerVote}";
    }

    public const string Page = "page";
    public static string PageWithParams(string view, string key, int page)
    {
        return $"{Page}:{view}:{key}:{page}";
    }

    public const string RescheduleApprove = "rescheduleapprove";
    public static string RescheduleApproveWithParams(int request)
    {
        return $"{RescheduleApprove}:{request}";
    }

    public const string RescheduleReject = "reschedulereject";
    public static string RescheduleRejectWithParams(int request)
    {
        return $"{RescheduleReject}:{request}";
    }

    public const string ClaimApprove = "claimapprove";
    public static string ClaimApproveWithParams(int claim)
    {
        return $"{ClaimApprove}:{claim}";
    }

    public const string ClaimReject = "claimreject";
    public static string ClaimRejectWithParams(int claim)
    {
        return $"{ClaimReject}:{claim}";
    }

    public const string ChatConfirm = "chatconfirm";
    public static string ChatConfirmWithParams(string actionId, bool approved)
    {
        return $"{ChatConfirm}:{actionId}:{(approved ? "yes" : "no")}";
    }

    public const string Signup = "signup";
    public static string SignupWithParams(int seasonId)
    {
        return $"{Signup}:{seasonId}";
    }

    public const string SignupDetails = "signupdetails";
    public static string SignupDetailsWithParams(int seasonId)
    {
        return $"{SignupDetails}:{seasonId}";
    }
}

/// <summary>
/// All customIds used by string menus. Centralized to avoid conflicts and make it easy to find where they are used.
/// </summary>
public class DiscordStringMenuContants
{
    public const string VoteToKick = "votetokick";
    public static string VoteToKickWithParams(int matchId)
    {
        return $"{VoteToKick}:{matchId}";
    }

    public const string SeasonView = "seasonview";
    public static string SeasonViewWithParams(string view)
    {
        return $"{SeasonView}:{view}";
    }

    public const string SeasonStat = "seasonstat";
    public static string SeasonStatWithParams(int seasonId)
    {
        return $"{SeasonStat}:{seasonId}";
    }

    public const string PlayerView = "playerview";
    public static string PlayerViewWithParams(int userId)
    {
        return $"{PlayerView}:{userId}";
    }

    public const string DraftPick = "draftpick";
    public static string DraftPickWithParams(int seasonId, int captainId, int index)
    {
        return $"{DraftPick}:{seasonId}:{captainId}:{index}";
    }
}

/// <summary>
/// All customIds used by modals. Centralized to avoid conflicts and make it easy to find where they are used.
/// </summary>
public class DiscordModalsContants
{
    public const string SetGamertag = "set_gamertag";
    public const string SetRegion = "set_region";

    public const string Signup = "signup";
    public static string SignupWithParams(int seasonId)
    {
        return $"{Signup}:{seasonId}";
    }

    public const string BracketCreate = "bracket_create";
    public static string BracketCreateWithParams(int seasonId, int participants, int bestOf)
    {
        return $"{BracketCreate}:{seasonId}:{participants}:{bestOf}";
    }

    public const string JobPause = "job_pause";
    public static string JobPauseWithParams(string job)
    {
        return $"{JobPause}:{job}";
    }

    public const string MatchPull = "match_pull";
    public static string MatchPullWithParams(int matchId)
    {
        return $"{MatchPull}:{matchId}";
    }

    public const string RescheduleDecide = "reschedule_decide";
    public static string RescheduleDecideWithParams(int request)
    {
        return $"{RescheduleDecide}:{request}";
    }

    public const string RescheduleRequest = "reschedule_request";
    public static string RescheduleRequestWithParams(int seasonMatchId)
    {
        return $"{RescheduleRequest}:{seasonMatchId}";
    }

    public const string MatchReport = "match_report";
    public static string MatchReportWithParams(int seasonMatchId)
    {
        return $"{MatchReport}:{seasonMatchId}";
    }

    public const string MatchTime = "match_time";
    public static string MatchTimeWithParams(int seasonMatchId)
    {
        return $"{MatchTime}:{seasonMatchId}";
    }

    public const string ClaimReject = "claim_reject";
    public static string ClaimRejectWithParams(int claim)
    {
        return $"{ClaimReject}:{claim}";
    }
}
