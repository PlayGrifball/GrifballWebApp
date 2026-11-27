namespace GrifballWebApp.Application;

public class UrlService
{
    private readonly string _baseUrl;
    public UrlService(IConfiguration configuration)
        : this(configuration.GetValue<string>("BaseUrl") ?? throw new Exception("Missing BaseUrl from configuration. Should be the home page for the website. Ex: https://localhost:4200"))
    {
    }

    /// <summary>Links on the site at <paramref name="baseUrl"/> (its home page, no trailing slash).</summary>
    public UrlService(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>Links as paths on the site itself ("/season/3"), for a page that is already on it and turns them into links.</summary>
    public static UrlService Relative { get; } = new("");

    public string SignupForm(int seasonId)
    {
        return $"{_baseUrl}/login?followUp=/season/{seasonId}/signupForm";
    }

    public string ViewSignups(int seasonId)
    {
        return $"{_baseUrl}/season/{seasonId}/signups";
    }

    public string Draft(int seasonId)
    {
        return $"{_baseUrl}/season/{seasonId}/draft";
    }

    public string Season(int seasonId)
    {
        return $"{_baseUrl}/season/{seasonId}";
    }

    public string Grades(int seasonId)
    {
        return $"{_baseUrl}/season/{seasonId}/playergrades";
    }

    public string Team(int seasonId, int teamId)
    {
        return $"{_baseUrl}/season/{seasonId}/team/{teamId}";
    }

    public string TeamHistory(int teamId)
    {
        return $"{_baseUrl}/history/team/{teamId}";
    }

    public string Profile(int userId)
    {
        return $"{_baseUrl}/profile/{userId}";
    }

    public string SeasonMatch(int seasonMatchId)
    {
        return SeasonMatchOn(_baseUrl, seasonMatchId);
    }

    /// <summary>A season match's page on the site at <paramref name="siteUrl"/> (its home page, no trailing slash): for a
    /// caller that knows the site from its request rather than from BaseUrl (the calendar feeds).</summary>
    public static string SeasonMatchOn(string siteUrl, int seasonMatchId)
    {
        return $"{siteUrl}/seasonmatch/{seasonMatchId}";
    }

    public string Match(Guid matchId)
    {
        return $"{_baseUrl}/match/{matchId}";
    }

    /// <summary>An Xbox account's page: its saved games, account or not.</summary>
    public string XboxAccount(long xboxUserId)
    {
        return $"{_baseUrl}/xbox/{xboxUserId}";
    }

    /// <summary>The picture the site keeps for a user (<see cref="Chat.ChatImages.Slug"/>): the API's own address, so a chat can show it.</summary>
    public string PlayerImage(int userId, string slug)
    {
        return $"{_baseUrl}/api/users/{userId}/{slug}";
    }

    public string RescheduleRequest(int seasonMatchId)
    {
        return $"{_baseUrl}/reschedule-request/{seasonMatchId}";
    }

    /// <summary>A page of the site by its path, for the pages with no parameters (seasons, leaderboards, late-league, ...).</summary>
    public string Page(string path)
    {
        return $"{_baseUrl}/{path.TrimStart('/')}";
    }
}
