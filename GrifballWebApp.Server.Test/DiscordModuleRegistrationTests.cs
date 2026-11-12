using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// What Discord sees of the server's NetCord modules: every custom ID with its parameters and every slash command with its options,
/// read the way Program.cs registers them (AddModules on the server assembly). Pins them so moving logic out of the modules
/// (issue #42) can't rename or drop an interaction.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DiscordModuleRegistrationTests
{
    // The server's Program by its full name: a top-level-statement app that references ASP.NET Core (GrifballWebApp.Convert,
    // through Infrastructure) gets a public Program in the global namespace, which a bare `Program` here would find first.
    private static readonly System.Reflection.Assembly ServerAssembly = typeof(GrifballWebApp.Server.Program).Assembly;

    private static readonly string[] Buttons =
    [
        "join_queue() preconditions: 0",
        "leave_queue() preconditions: 0",
        "set_gamertag() preconditions: 0",
        "signup(Int32 seasonId) preconditions: 0",
        "voteforwinner(Int32 matchId, String winner) preconditions: 0",
    ];

    private static readonly string[] StringMenus =
    [
        "draftpick(Int32 seasonId, Int32 captainId, Int32 _) preconditions: 0",
        "votetokick(Int32 matchId) preconditions: 0",
    ];

    private static readonly string[] Modals =
    [
        "set_gamertag() preconditions: 0",
    ];

    private static List<string> ComponentInteractions<TContext>() where TContext : IComponentInteractionContext
    {
        var service = new ComponentInteractionService<TContext>();
        service.AddModules(ServerAssembly);
        return service.GetComponentInteractions()
            .Select(x => $"{x.Key}({string.Join(", ", x.Value.Parameters.Select(p => $"{p.Type.Name} {p.Name}{(p.IsOptional ? $" = {p.DefaultValue ?? "null"}" : "")}"))})"
                + $" preconditions: {x.Value.Preconditions.Count}")
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    [Test]
    public void Buttons_Should_KeepTheirCustomIdsAndParameters()
    {
        Assert.That(ComponentInteractions<ButtonInteractionContext>(), Is.EqualTo(Buttons));
    }

    [Test]
    public void StringMenus_Should_KeepTheirCustomIdsAndParameters()
    {
        Assert.That(ComponentInteractions<StringMenuInteractionContext>(), Is.EqualTo(StringMenus));
    }

    [Test]
    public void Modals_Should_KeepTheirCustomIdsAndParameters()
    {
        Assert.That(ComponentInteractions<ModalInteractionContext>(), Is.EqualTo(Modals));
    }

    private static string Describe(string name, string description, IEnumerable<SlashCommandParameter<ApplicationCommandContext>> parameters, string extra)
        => $"{name} \"{description}\" {extra}"
            + string.Concat(parameters.Select(p =>
                $" [{p.Name}: {p.Type.Name}{(p.IsOptional ? $" = {p.DefaultValue ?? "null"}" : "")} \"{p.Description}\""
                + $" autocomplete: {p.AutocompleteProviderType?.Name ?? "none"}, preconditions: {p.Preconditions.Count}]"));

    /// <summary>A group's subcommands, nested groups' too, each as one line with the group's permissions (Discord sets them per top-level command).</summary>
    private static IEnumerable<string> SubCommands(string prefix, string permissions, IReadOnlyDictionary<string, ISubSlashCommandInfo<ApplicationCommandContext>> subCommands)
        => subCommands.Values.SelectMany(IEnumerable<string> (sub) => sub switch
        {
            SubSlashCommandInfo<ApplicationCommandContext> command => [Describe($"{prefix} {command.Name}", command.Description, command.Parameters,
                $"{permissions}, preconditions: {command.Preconditions.Count}")],
            SubSlashCommandGroupInfo<ApplicationCommandContext> group => SubCommands($"{prefix} {group.Name}", permissions, group.SubCommands),
            _ => [$"{prefix} {sub.Name}"],
        });

    [Test]
    public void SlashCommands_Should_KeepTheirNamesAndOptions()
    {
        var service = new ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext>();
        service.AddModules(ServerAssembly);

        var commands = service.GetCommands()
            .SelectMany(IEnumerable<string> (c) => c switch
            {
                SlashCommandInfo<ApplicationCommandContext> slash => [Describe($"/{slash.Name}", slash.Description, slash.Parameters,
                    $"permissions: {slash.DefaultGuildPermissions?.ToString() ?? "default"}, preconditions: {slash.Preconditions.Count}")],
                SlashCommandGroupInfo<ApplicationCommandContext> group => SubCommands($"/{group.Name}",
                    $"permissions: {group.DefaultGuildPermissions?.ToString() ?? "default"}", group.SubCommands),
                _ => [$"{c.Type} {c.Name}"],
            })
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.That(commands, Is.EqualTo(new[]
        {
            "/admin bracket create \"Make the season's bracket\" permissions: default, preconditions: 0"
                + " [participants: Int32 \"Teams in the bracket\" autocomplete: none, preconditions: 0]"
                + " [best_of: Int32 \"Games per match\" autocomplete: none, preconditions: 0]"
                + " [double_elimination: Boolean = False \"Double elimination\" autocomplete: none, preconditions: 0]"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/admin bracket seed \"Seed the bracket by the standings\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/admin claims approve \"Approve a claim\" permissions: default, preconditions: 0"
                + " [claim: Int32 \"The claim's number\" autocomplete: none, preconditions: 0]",
            "/admin claims list \"Pending claims\" permissions: default, preconditions: 0",
            "/admin claims reject \"Reject a claim\" permissions: default, preconditions: 0"
                + " [claim: Int32 \"The claim's number\" autocomplete: none, preconditions: 0]"
                + " [reason: String = null \"Why, for the claimant\" autocomplete: none, preconditions: 0]",
            "/admin dashboard \"Pending reschedules and overdue matches, counted\" permissions: default, preconditions: 0",
            "/admin draft lock \"Lock the captains\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/admin draft unlock \"Unlock the captains\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/admin halo pull \"Pull a Halo game's stats\" permissions: default, preconditions: 0"
                + " [match_id: String \"The game's Halo match ID\" autocomplete: none, preconditions: 0]",
            "/admin jobs list \"The jobs and their state\" permissions: default, preconditions: 0",
            "/admin jobs pause \"Pause or resume a job\" permissions: default, preconditions: 0"
                + " [job: String \"The job, as group/name\" autocomplete: none, preconditions: 0]"
                + " [paused: Boolean = True \"Pause it (false resumes it)\" autocomplete: none, preconditions: 0]",
            "/admin jobs run \"Run a job now\" permissions: default, preconditions: 0"
                + " [job: String \"The job, as group/name\" autocomplete: none, preconditions: 0]",
            "/admin match forfeit \"Forfeit the match\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]"
                + " [side: ForfeitSide \"Who forfeits\" autocomplete: none, preconditions: 0]",
            "/admin match pull \"Pull the players' recent Halo games\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]",
            "/admin match report \"Report a Halo game as one of the match's\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]"
                + " [match_id: String \"The game's Halo match ID\" autocomplete: none, preconditions: 0]"
                + " [replaces: Int32 = 0 \"The series game it replaces (default: none)\" autocomplete: none, preconditions: 0]",
            "/admin match time \"Schedule the match\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]"
                + " [time: String \"The time in UTC, like 2026-11-20 20:00\" autocomplete: none, preconditions: 0]",
            "/admin match undo \"Undo the match's result\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]",
            "/admin medals \"Refresh the Halo medals\" permissions: default, preconditions: 0",
            "/admin overdue \"Matches overdue to be played\" permissions: default, preconditions: 0",
            "/admin schedule apply \"Schedule the matches as recommended\" permissions: default, preconditions: 0"
                + " [recommendation: Int32 \"The recommendation's number\" autocomplete: none, preconditions: 0]",
            "/admin schedule run \"Work out a new recommendation\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/admin schedule show \"The latest recommendation\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/admin season generate \"Make the season's matches\" permissions: default, preconditions: 0"
                + " [home_matches: Int32 \"Home matches per team\" autocomplete: none, preconditions: 0]"
                + " [best_of: Int32 \"Games per match\" autocomplete: none, preconditions: 0]"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/availability show \"A season's match times\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/events \"Upcoming signups, drafts and seasons\" permissions: default, preconditions: 0",
            "/help \"The bot's commands\" permissions: default, preconditions: 0"
                + " [command: String = null \"A command, like season or admin, for its parts\" autocomplete: none, preconditions: 0]",
            "/lateleague season \"A Late League season's top players\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season's name or ID (default: the latest)\" autocomplete: none, preconditions: 0]",
            "/lateleague seasons \"The Late League seasons\" permissions: default, preconditions: 0",
            "/leaderboard rank \"A player's all-time ranks\" permissions: default, preconditions: 0"
                + " [player: String \"The player\" autocomplete: PlayerAutocomplete, preconditions: 0]"
                + " [format: LeaderboardFormat = All \"Count only one format (default: all)\" autocomplete: none, preconditions: 0]",
            "/leaderboard top \"The top players on a board\" permissions: default, preconditions: 0"
                + " [board: LeaderboardBoard = Rings \"The board\" autocomplete: none, preconditions: 0]"
                + " [format: LeaderboardFormat = All \"Count only one format (default: all)\" autocomplete: none, preconditions: 0]",
            "/match game \"A Halo game's teams and players\" permissions: default, preconditions: 0"
                + " [match_id: String \"The game's Halo match ID\" autocomplete: none, preconditions: 0]",
            "/match info \"A season match's time, result and games\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]",
            "/matches \"Get recent matches\" permissions: default, preconditions: 0"
                + " [gamertag: String = null \"Gamertag to search matches for\" autocomplete: GamertagAutocomplete, preconditions: 0]",
            "/me claim \"Claim a gamertag you played under before\" permissions: default, preconditions: 0"
                + " [gamertag: String \"The past gamertag\" autocomplete: none, preconditions: 0]",
            "/me claims \"Your gamertag claims\" permissions: default, preconditions: 0",
            "/me logins \"Link or unlink your sign-ins\" permissions: default, preconditions: 0",
            "/me region \"Set your region\" permissions: default, preconditions: 0"
                + " [region: String \"Your region, or none to clear it\" autocomplete: RegionAutocomplete, preconditions: 0]",
            "/player commendations \"A player's medals\" permissions: default, preconditions: 0"
                + " [player: String = null \"The player (default: you)\" autocomplete: PlayerAutocomplete, preconditions: 0]",
            "/player history \"A player's seasons and teams\" permissions: default, preconditions: 0"
                + " [player: String = null \"The player (default: you)\" autocomplete: PlayerAutocomplete, preconditions: 0]",
            "/player matches \"A player's recent Halo matches\" permissions: default, preconditions: 0"
                + " [player: String = null \"The player (default: you)\" autocomplete: PlayerAutocomplete, preconditions: 0]",
            "/player profile \"A player's region, record and championships\" permissions: default, preconditions: 0"
                + " [player: String = null \"The player (default: you)\" autocomplete: PlayerAutocomplete, preconditions: 0]",
            "/player stats \"A player's career stats by game\" permissions: default, preconditions: 0"
                + " [player: String = null \"The player (default: you)\" autocomplete: PlayerAutocomplete, preconditions: 0]",
            "/player vods \"A player's VODs\" permissions: default, preconditions: 0"
                + " [player: String = null \"The player (default: you)\" autocomplete: PlayerAutocomplete, preconditions: 0]",
            "/queue join \"Join the queue\" permissions: default, preconditions: 0",
            "/queue leave \"Leave the queue\" permissions: default, preconditions: 0",
            "/queue status \"How many are in the queue, and whether you are\" permissions: default, preconditions: 0",
            "/reschedule decide \"Approve or reject a request (staff)\" permissions: default, preconditions: 0"
                + " [request: Int32 \"The request's number, from /reschedule pending\" autocomplete: none, preconditions: 0]"
                + " [approve: Boolean \"Approve it (false rejects it)\" autocomplete: none, preconditions: 0]"
                + " [notes: String = null \"Notes for the players\" autocomplete: none, preconditions: 0]",
            "/reschedule pending \"Requests waiting for a decision (staff)\" permissions: default, preconditions: 0",
            "/reschedule request \"Ask to move one of your matches\" permissions: default, preconditions: 0"
                + " [match: String \"The match (search by team)\" autocomplete: SeasonMatchAutocomplete, preconditions: 0]"
                + " [reason: String \"Why it needs to move\" autocomplete: none, preconditions: 0]"
                + " [new_time: String = null \"The new time in UTC, like 2026-11-20 20:00\" autocomplete: none, preconditions: 0]",
            "/search \"Search players, teams and seasons\" permissions: default, preconditions: 0"
                + " [query: String \"A gamertag, team or season name\" autocomplete: none, preconditions: 0]",
            "/season bracket \"A season's playoff bracket\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season draft \"A link to a season's live draft board\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season grades \"A season's player grades\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season info \"A season's league, dates, teams and champions\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season list \"The latest seasons\" permissions: default, preconditions: 0",
            "/season schedule \"A season's upcoming matches\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season signups \"Who signed up for a season\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season standings \"A season's team standings\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/season stats \"A season's top players\" permissions: default, preconditions: 0"
                + " [stat: SeasonStat = Goals \"Rank players by\" autocomplete: none, preconditions: 0]"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]",
            "/setgamertag \"Set your gamertag\" permissions: default, preconditions: 0"
                + " [gamertag: String \"No description provided.\" autocomplete: none, preconditions: 0]",
            "/signup \"Sign up for a season, or change your signup\" permissions: default, preconditions: 0"
                + " [season: String = null \"The season (default: the current one)\" autocomplete: SeasonAutocomplete, preconditions: 0]"
                + " [team_name: String = null \"The team name you'd use as captain\" autocomplete: none, preconditions: 0]"
                + " [will_captain: Boolean = False \"You'd captain a team\" autocomplete: none, preconditions: 0]"
                + " [draft_help: Boolean = False \"You'd like help drafting\" autocomplete: none, preconditions: 0]",
            "/team history \"A team's seasons across the league's history\" permissions: default, preconditions: 0"
                + " [team: String \"The team\" autocomplete: TeamAutocomplete, preconditions: 0]",
            "/team info \"A team's season, record, captain and titles\" permissions: default, preconditions: 0"
                + " [team: String \"The team\" autocomplete: TeamAutocomplete, preconditions: 0]",
            "/team matches \"A team's matches, unplayed first\" permissions: default, preconditions: 0"
                + " [team: String \"The team\" autocomplete: TeamAutocomplete, preconditions: 0]",
            "/team roster \"A team's players\" permissions: default, preconditions: 0"
                + " [team: String \"The team\" autocomplete: TeamAutocomplete, preconditions: 0]",
            "/team vods \"A team's VODs\" permissions: default, preconditions: 0"
                + " [team: String \"The team\" autocomplete: TeamAutocomplete, preconditions: 0]",
        }));
    }
}
