namespace GrifballWebApp.Server.Signups;

public class SignupResponseDto
{
    public int SeasonID { get; set; }
    public int UserID { get; set; }
    public string? PersonName { get; set; }
    public DateTime Timestamp { get; set; }
    public string? TeamName { get; set; }
    public bool WillCaptain { get; set; }
    public bool RequiresAssistanceDrafting { get; set; }
    public TimeslotDto[] Timeslots { get; set; } = [];
}

public class TimeslotDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public bool IsChecked { get; set; }
}
