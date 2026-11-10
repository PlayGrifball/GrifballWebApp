using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Extensions;

public static class StringExtensions
{
    public static string RemoveXUIDWrapper(this string s)
    {
        return s.Replace("xuid(", "").Replace(")", "");
    }

    public static string AddXUIDWrapper(this string s)
    {
        return $"xuid({s})";
    }

    public static string LinkMarkdown(this string display, string url)
    {
        if (url is null)
            return display;
        else
            return $"[{display}]({url})";
    }

    public static string? ToDisplayName(this MatchedPlayer player)
    {
        return player.User.ToDisplayName();
    }

    public static string? ToDisplayName(this QueuedPlayer player)
    {
        return player.User.ToDisplayName();
    }

    public static string? ToDisplayName(this User user)
    {
        return user.ShownName();
    }
}
