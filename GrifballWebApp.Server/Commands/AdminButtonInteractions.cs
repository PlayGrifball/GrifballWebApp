using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for the Approve and Reject buttons under /reschedule pending and /admin claims list: wraps the context and hands off
/// to <see cref="AdminCommandsHandler"/>, which checks the caller's role as the commands do. Keep logic out of here, it is only tested end to
/// end (DiscordTransportTests).
/// </summary>
public class AdminButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly AdminCommandsHandler _handler;

    public AdminButtonInteractions(AdminCommandsHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordButtonContants.RescheduleApprove)]
    public Task RescheduleApprove(int request) => _handler.ApproveReschedule(Context.ToDiscordContext(), request);

    [ComponentInteraction(DiscordButtonContants.RescheduleReject)]
    public Task RescheduleReject(int request) => _handler.RejectReschedule(Context.ToDiscordContext(), request);

    [ComponentInteraction(DiscordButtonContants.ClaimApprove)]
    public Task ClaimApprove(int claim) => _handler.ApproveClaim(Context.ToDiscordContext(), claim);

    [ComponentInteraction(DiscordButtonContants.ClaimReject)]
    public Task ClaimReject(int claim) => _handler.RejectClaim(Context.ToDiscordContext(), claim);
}
