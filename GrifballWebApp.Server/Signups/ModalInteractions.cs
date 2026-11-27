using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;
using GrifballWebApp.Infrastructure.Discord;

namespace GrifballWebApp.Server.Signups;

/// <summary>
/// NetCord entry point for the signup modal: wraps the context and hands off to <see cref="SignupButtonHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class ModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly SignupButtonHandler _handler;

    public ModalInteractions(SignupButtonHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordModalsContants.Signup)]
    public async Task Signup(int seasonId)
    {
        await _handler.SaveSignupDetails(Context.ToDiscordContext(), seasonId);
    }
}
