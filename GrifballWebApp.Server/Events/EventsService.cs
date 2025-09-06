using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Events;

public class EventsService
{
    private readonly ILogger<EventsService> _logger;
    private readonly IOptions<DiscordOptions> _discordOptions;
    private readonly ulong _eventsChannel;
    private readonly IDiscordRestClient _discordClient;
    private readonly GrifballContext _context;
    private readonly UrlService _urlService;
    public EventsService(ILogger<EventsService> logger, IOptions<DiscordOptions> discordOptions, IDiscordRestClient discordClient, GrifballContext grifballContext, UrlService urlService)
    {
        _logger = logger;
        _discordOptions = discordOptions;
        _eventsChannel = discordOptions.Value.EventsChannel;
        if (_eventsChannel is 0)
            throw new Exception("Discord:EventsChannel is not set");
        _discordClient = discordClient;
        _context = grifballContext;
        _urlService = urlService;
    }
    public async Task Go(CancellationToken ct)
    {
        await UpdateMessage(ct);
    }

    private async Task UpdateMessage(CancellationToken ct)
    {
        var messages = (await _discordClient.GetMessagesAsync(_eventsChannel, new PaginationProperties<ulong>()
        {
            BatchSize = 20,
        }).Take(20).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToArray();

        var now = DateTime.UtcNow;
        var seasons = await _context.Seasons.Where(season => now >= season.PublicAt && now < season.SeasonEnd).ToArrayAsync();

        foreach (var season in seasons)
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

            var matchesSummaryEmbed = new EmbedProperties()
            {
                Color = new Color(87, 242, 135), // green
                Title = $"{season.SeasonName}",
                Description = $"{signupMessage}\n{draftMessage}\n{seasonMessage}",
            };

            var matchEmbeds = new List<EmbedProperties>();
            matchEmbeds.Add(matchesSummaryEmbed);

            
            //var leave = new ButtonProperties("leave_queue", "Leave Queue", new EmojiProperties("❌"), ButtonStyle.Danger);
            var gt = new ButtonProperties(DiscordButtonContants.SetGamertag, "Set Gamertag", ButtonStyle.Primary);

            ActionRowProperties actionRow = new([gt]);

            if (signupStatus is SignupStatus.Open)
            {
                var signup = new ButtonProperties(DiscordButtonContants.SignupWithParams(season.SeasonID), "Signup", new EmojiProperties("✅"), ButtonStyle.Success);
                actionRow.AddButtons(signup);
            }

            var mp = new MessageProperties()
            {
                Content = season.SeasonName,
                Embeds = matchEmbeds,
                Components = [actionRow],
            };
            var message = messages.FirstOrDefault(x => x.Content == season.SeasonName);
            await _discordClient.UpsertMessageAsync(_eventsChannel, message?.Id, mp, null, ct);
        }
    }
}

public enum SignupStatus
{
    Open,
    Closed,
    NotStarted,
}
