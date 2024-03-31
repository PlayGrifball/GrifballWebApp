namespace GrifballWebApp.Server.Signups;

public class SignupRequestDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
    public string? TeamName { get; set; }
    public bool WillCaptain { get; set; }
    public bool RequiresAssistanceDrafting { get; set; }
}
