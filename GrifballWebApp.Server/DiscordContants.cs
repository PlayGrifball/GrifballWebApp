using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server;

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

    public const string Signup = "signup";
    public static string SignupWithParams(int seasonId)
    {
        return $"{Signup}:{seasonId}";
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
}
