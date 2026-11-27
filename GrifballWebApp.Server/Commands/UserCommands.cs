using DiscordInterfaces;
using NetCord;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for the right-click user commands (Apps menu on a Discord user): wraps the context and hands off to
/// <see cref="PlayerCommandsHandler"/> and <see cref="UserCommandsHandler"/>. Keep logic out of here.
/// </summary>
public class UserCommands : ApplicationCommandModule<UserCommandContext>
{
    public const string ProfileName = "Profile";
    public const string PullName = "Pull games";
    public const string SetGamertagName = "Set gamertag";

    private readonly PlayerCommandsHandler _players;
    private readonly UserCommandsHandler _handler;

    public UserCommands(PlayerCommandsHandler players, UserCommandsHandler handler)
    {
        _players = players;
        _handler = handler;
    }

    [UserCommand(ProfileName)]
    public Task Profile(User user) => _players.ProfileOfDiscordUser(Context.ToDiscordContext(), user.Id);

    [UserCommand(PullName)]
    public Task Pull(User user) => _players.PullOfDiscordUser(Context.ToDiscordContext(), user.Id);

    [UserCommand(SetGamertagName)]
    public Task SetGamertag(User user) => _handler.OpenSetGamertag(Context.ToDiscordContext(), user.Id);
}
