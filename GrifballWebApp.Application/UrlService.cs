namespace GrifballWebApp.Application;

public class UrlService
{
    private readonly string _baseUrl;
    public UrlService(IConfiguration configuration)
    {
        _baseUrl = configuration.GetValue<string>("BaseUrl") ?? throw new Exception("Missing BaseUrl from configuration. Should be the home page for the website. Ex: https://localhost:4200");
    }

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
        return $"{_baseUrl}/seasonmatch/{seasonMatchId}";
    }

    public string Match(Guid matchId)
    {
        return $"{_baseUrl}/match/{matchId}";
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
