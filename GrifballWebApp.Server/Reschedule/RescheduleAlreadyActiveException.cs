namespace GrifballWebApp.Server.Reschedule;

public class RescheduleAlreadyActiveException : Exception
{
    public RescheduleAlreadyActiveException() : base("There is already an active reschedule request for this match.")
    {
    }
}
