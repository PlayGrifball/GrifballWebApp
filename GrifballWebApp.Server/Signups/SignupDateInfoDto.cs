namespace GrifballWebApp.Server.Signups;

public class SignupDateInfoDto
{
    public int SeasonID { get; set; }
    public bool IsSignedUp { get; set; }
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
}
