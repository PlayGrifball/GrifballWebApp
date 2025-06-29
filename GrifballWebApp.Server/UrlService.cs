
namespace GrifballWebApp.Server;

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
        return $"{_baseUrl}/season/{seasonId}/teams";
    }

    public string Season(int seasonId)
    {
        return $"{_baseUrl}/season/{seasonId}";
    }
}
