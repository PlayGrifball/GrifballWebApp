using System.ComponentModel;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Tools;

/// <summary>Buttons the Discord bot's chat shows under its reply: the click opens the bot's own modal or flow for the person who clicks.</summary>
public class ButtonTools
{
    public const string NoSuchSeason = "There's no such season.";
    public const string SignupsNotOpen = "That season's signups aren't open.";

    private readonly ToolCaller _caller;
    private readonly ISlashCommandCatalog _catalog;
    private readonly SeasonVisibility _visibility;
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;

    public ButtonTools(ToolCaller caller, ISlashCommandCatalog catalog, SeasonVisibility visibility, IApplicationDbContext context, TimeProvider time)
    {
        _caller = caller;
        _catalog = catalog;
        _visibility = visibility;
        _context = context;
        _time = time;
    }

    [Description("Show the person a button under your reply that does something for them in Discord: set their gamertag (opens a form), sign up for a season, "
        + "set their signup details and times (opens a form), or join the matchmaking queue. Use it when they ask to do one of these, or you would otherwise tell them "
        + "to type a command. The button is added for you: write nothing for it in your reply.")]
    public async Task<string> OfferButton([Description("What the button does.")] ChatButtonKind kind,
        [Description("The season's ID, for signup and signup_details buttons (list_seasons gives it).")] int? seasonID = null, CancellationToken ct = default)
    {
        if (_catalog.Commands().Count == 0)
            throw new ToolException("Buttons are only shown by the Discord bot.");
        if (ChatButton.NeedsSeason(kind) && seasonID is null)
            throw new ToolException($"A {kind} button needs the season's ID.");
        if (!ChatButton.NeedsSeason(kind) && seasonID is not null)
            throw new ToolException($"A {kind} button is not about a season: leave the season ID out.");
        if (seasonID is { } season)
        {
            // What the reply may name: an unannounced season stays out of a public one (the caller's viewer says so)
            if (!await _visibility.CanSeeSeason(season, _caller.Viewer, ct))
                throw new ToolException(NoSuchSeason);
            if (!await _context.Seasons.Where(s => s.SeasonID == season).AnyAsync(Season.SignupsOpenAt(_time.GetUtcNow().UtcDateTime), ct))
                throw new ToolException(SignupsNotOpen);
        }
        return $"Shown under your reply: {ChatButtons.Token(new ChatButton(kind, seasonID))}";
    }
}
