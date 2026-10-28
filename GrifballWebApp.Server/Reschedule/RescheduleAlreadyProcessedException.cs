namespace GrifballWebApp.Server.Reschedule;

public class RescheduleAlreadyProcessedException : Exception
{
    public RescheduleAlreadyProcessedException() : base("Reschedule request has already been processed")
    {
    }
}
