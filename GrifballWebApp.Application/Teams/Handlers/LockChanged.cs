using GrifballWebApp.Application.Notifications;

namespace GrifballWebApp.Application.Teams.Handlers;

public record LockChanged(bool Value, int seasonID, string? connectionID) : INotification;
