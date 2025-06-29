using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Signups;

public class ButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly GrifballContext _context;
    private readonly SignupsService _signupsService;
    private readonly UrlService _urlService;
    public ButtonInteractions(GrifballContext context, SignupsService signupsService, UrlService urlService)
    {
        _context = context;
        _signupsService = signupsService;
        _urlService = urlService;
    }

    private async Task<Database.Models.User?> UserGuard()
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)Context.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await Context.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    [ComponentInteraction(DiscordButtonContants.Signup)]
    public async Task Signup(int seasonId)
    {
        var user = await UserGuard();
        if (user is null) return;

        var isSignedUp = await _context.SeasonSignups
            .Where(signup => signup.SeasonID == seasonId && signup.UserID == user.Id)
            .AnyAsync();
        var signupUrl = _urlService.SignupForm(seasonId);
        if (isSignedUp)
        {
            await Context.EphemeralResponse($"You are already signed up for this season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
            return;
        }

        var dto = new SignupRequestDto()
        {
            SeasonID = seasonId,
            UserID = user.Id,
        };
        await _signupsService.UpsertSignup(dto);

        await Context.EphemeralResponse($"You have signed up for the season. Remember you can do more things like set your availability {"here".LinkMarkdown(signupUrl)}");
    }
}
