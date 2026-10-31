using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GrifballWebApp.Server.Events;

/// <summary>
/// Keeps each public, unfinished season's announcement in the events channel up to date: works out how it should read
/// on each tick, and queues it on the outbox when that differs from what was last queued (signups opening, closing,
/// the season changed), or that was given up on.
/// </summary>
public class EventsService
{
    private readonly ILogger<EventsService> _logger;
    private readonly IOptions<DiscordOptions> _discordOptions;
    private readonly ulong _eventsChannel;
    private readonly IOutbox _outbox;
    private readonly GrifballContext _context;
    private readonly UrlService _urlService;
    public EventsService(ILogger<EventsService> logger, IOptions<DiscordOptions> discordOptions, IOutbox outbox, GrifballContext grifballContext, UrlService urlService)
    {
        _logger = logger;
        _discordOptions = discordOptions;
        _eventsChannel = discordOptions.Value.EventsChannel;
        if (_eventsChannel is 0)
            throw new Exception("Discord:EventsChannel is not set");
        _outbox = outbox;
        _context = grifballContext;
        _urlService = urlService;
    }
    public async Task Go(CancellationToken ct)
    {
        await UpdateMessage(ct);
    }

    private async Task UpdateMessage(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // A season with no PublicAt is not public yet: it is not announced.
        var seasons = await _context.Seasons.Where(season => season.PublicAt != null && season.PublicAt <= now && now < season.SeasonEnd).ToArrayAsync(ct);

        foreach (var season in seasons)
        {
            var announcement = Announcement(season, now);
            var group = EventsOutbox.Group(season.SeasonID);
            var last = await _context.Outbox
                .Where(x => x.GroupKey == group)
                .OrderByDescending(x => x.Sequence)
                .Select(x => new { x.Payload, x.Status })
                .FirstOrDefaultAsync(ct);
            var payload = JsonSerializer.Serialize(announcement, OutboxJson.Options);
            if (last is not null && last.Payload == payload && last.Status != OutboxStatus.DeadLettered)
                continue;

            await _outbox.EnqueueAsync(EventsOutbox.SeasonUpsert, announcement, group, ct);
        }

        await _context.SaveChangesAsync(ct);
    }

    private SeasonAnnouncement Announcement(Season season, DateTime now)
    {
        var signupStatus = season switch
        {
            _ when season.SignupsOpen <= now && now <= season.SignupsClose => SignupStatus.Open,
            _ when now < season.SignupsOpen => SignupStatus.NotStarted,
            _ => SignupStatus.Closed,
        };

        var signupUrl = _urlService.SignupForm(season.SeasonID);
        var viewSignupsUrl = _urlService.ViewSignups(season.SeasonID);
        var signupMessage = signupStatus switch
        {
            SignupStatus.Open => $"Signups are open until {season.SignupsClose.DiscordTimeEmbed()}. {"Signup here".LinkMarkdown(signupUrl)}. {"View Signups".LinkMarkdown(viewSignupsUrl)}.",
            SignupStatus.NotStarted => $"Signups will start at {season.SignupsOpen.DiscordTimeEmbed()} and close at {season.SignupsClose.DiscordTimeEmbed()}",
            SignupStatus.Closed => "Signups are closed.",
            _ => throw new ArgumentOutOfRangeException(nameof(signupStatus), signupStatus, null),
        };

        var draftUrl = _urlService.Draft(season.SeasonID);
        var draftMessage = $"The {"draft".LinkMarkdown(draftUrl)} will start at {season.DraftStart.DiscordTimeEmbed()}";

        var seasonUrl = _urlService.Season(season.SeasonID);
        var seasonMessage = $"The {"season".LinkMarkdown(seasonUrl)} will run from {season.SeasonStart.DiscordTimeEmbed()} to {season.SeasonEnd.DiscordTimeEmbed()}";

        return new SeasonAnnouncement(season.SeasonID, season.SeasonName, $"{signupMessage}\n{draftMessage}\n{seasonMessage}", signupStatus is SignupStatus.Open);
    }
}

public enum SignupStatus
{
    Open,
    Closed,
    NotStarted,
}
