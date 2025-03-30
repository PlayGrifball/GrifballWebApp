using MediatR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class Notification<T> : INotification where T : class
{
    public Notification(string? connectionId, T value)
    {
        ConnectionId = connectionId;
        Value = value;
    }
    public string? ConnectionId { get; init; }
    public T Value { get; init; }
}

public static class Notification
{
    public static Notification<T> Create<T>(string? connectionId, T value) where T : class
    {
        return new Notification<T>(connectionId, value);
    }
}
