using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Server.Matchmaking;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The app's publisher, recording what is published: <see cref="Refreshed"/> completes once the queue display's refresh
/// (<see cref="UpdateDisplayNotification"/>) is published, which the matchmaking handlers do in the background.
/// </summary>
internal sealed class RecordingPublisher : IPublisher
{
    private readonly TaskCompletionSource _refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Refreshed => _refreshed.Task;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        if (notification is UpdateDisplayNotification)
            _refreshed.TrySetResult();
        return Task.CompletedTask;
    }

    /// <summary>Waits for the queue display's refresh; fails the test (a timeout) when it isn't published.</summary>
    public Task WaitForRefresh() => Refreshed.WaitAsync(TimeSpan.FromSeconds(10));
}
