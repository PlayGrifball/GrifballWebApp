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

    public const string Signup = "signup";
    public static string SignupWithParams(int seasonId)
    {
        return $"{Signup}:{seasonId}";
    }
}
