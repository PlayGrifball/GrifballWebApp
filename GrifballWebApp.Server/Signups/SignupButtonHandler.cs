using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Signups;

/// <summary>
/// The season signup button. <see cref="ButtonInteractions"/> is the NetCord module; it only wraps its context and calls this,
/// so it takes our <see cref="IDiscordButtonInteractionContext"/> and tests can pass any implementation.
/// </summary>
public class SignupButtonHandler
{
    private readonly GrifballContext _context;
    private readonly SignupsService _signupsService;
    private readonly UrlService _urlService;
    public SignupButtonHandler(GrifballContext context, SignupsService signupsService, UrlService urlService)
    {
        _context = context;
        _signupsService = signupsService;
        _urlService = urlService;
    }

    private async Task<Database.Models.User?> UserGuard(IDiscordButtonInteractionContext discordContext)
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)discordContext.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await discordContext.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    public async Task Signup(IDiscordButtonInteractionContext discordContext, int seasonId)
    {
        var user = await UserGuard(discordContext);
        if (user is null) return;

        var isSignedUp = await _context.SeasonSignups
            .Where(signup => signup.SeasonID == seasonId && signup.UserID == user.Id)
            .AnyAsync();
        var signupUrl = _urlService.SignupForm(seasonId);
        if (isSignedUp)
        {
            await discordContext.EphemeralResponse($"You are already signed up for this season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
            return;
        }

        var dto = new SignupRequestDto()
        {
            SeasonID = seasonId,
            UserID = user.Id,
        };
        try
        {
            await _signupsService.UpsertSignup(dto);
        }
        catch (SignupsClosedException)
        {
            await discordContext.EphemeralResponse("Signups for this season are closed.");
            return;
        }

        await discordContext.EphemeralResponse($"You have signed up for the season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
    }
}
