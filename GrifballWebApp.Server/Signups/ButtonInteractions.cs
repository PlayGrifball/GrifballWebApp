using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Signups;

/// <summary>
/// NetCord entry point for the season signup button: wraps the context and hands off to <see cref="SignupButtonHandler"/>.
/// Keep logic out of here, it is excluded from coverage (coverlet.runsettings).
/// </summary>
public class ButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly SignupButtonHandler _handler;

    public ButtonInteractions(SignupButtonHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordButtonContants.Signup)]
    public async Task Signup(int seasonId)
    {
        await _handler.Signup(Context.ToDiscordContext(), seasonId);
    }
}
