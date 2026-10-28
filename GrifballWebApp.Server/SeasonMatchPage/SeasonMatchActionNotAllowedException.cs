namespace GrifballWebApp.Server.SeasonMatchPage;

/// <summary>
/// A commissioner action the rules do not allow for this match. The controller answers it with a 400.
/// </summary>
public class SeasonMatchActionNotAllowedException : Exception
{
    public SeasonMatchActionNotAllowedException(string message) : base(message)
    {
    }
}
