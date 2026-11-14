using DiscordInterfaces;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /admin: wraps the context and hands off to <see cref="AdminCommandsHandler"/>, which holds each
/// command to its endpoint's role. Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("admin", "Commissioner and sysadmin actions (only you see the replies)")]
public class AdminCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private const string MatchDescription = "The match (search by team)";
    private const string SeasonDescription = "The season (default: the current one)";
    /// <summary>Making matches or a bracket replaces the ones there: never a season by default.</summary>
    private const string RebuiltSeasonDescription = "The season (its matches not played yet are replaced)";
    private const string UpcomingSeasonDescription = "The season (default: the next to start)";
    private readonly AdminCommandsHandler _handler;

    public AdminCommands(AdminCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("dashboard", "Pending reschedules and overdue matches, counted")]
    public Task Dashboard() => _handler.Dashboard(Context.ToDiscordContext());

    [SubSlashCommand("overdue", "Matches overdue to be played")]
    public Task Overdue() => _handler.Overdue(Context.ToDiscordContext());

    [SubSlashCommand("medals", "Refresh the Halo medals")]
    public Task Medals() => _handler.RefreshMedals(Context.ToDiscordContext());

    [SubSlashCommand("match", "Season match results")]
    public class Match : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly AdminCommandsHandler _handler;

        public Match(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("report", "Report a Halo game as one of the match's")]
        public Task Report(
            [SlashCommandParameter(Description = MatchDescription, AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match,
            [SlashCommandParameter(Name = "match_id", Description = "The game's Halo match ID")] string gameID,
            [SlashCommandParameter(Description = "The series game it replaces (default: none)")] int replaces = 0)
            => _handler.ReportGame(Context.ToDiscordContext(), match, gameID, replaces > 0 ? replaces : null);

        [SubSlashCommand("forfeit", "Forfeit the match")]
        public Task Forfeit(
            [SlashCommandParameter(Description = MatchDescription, AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match,
            [SlashCommandParameter(Description = "Who forfeits")] ForfeitSide side)
            => _handler.Forfeit(Context.ToDiscordContext(), match, side);

        [SubSlashCommand("undo", "Undo the match's result")]
        public Task Undo([SlashCommandParameter(Description = MatchDescription, AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match)
            => _handler.UndoResult(Context.ToDiscordContext(), match);

        [SubSlashCommand("time", "Schedule the match")]
        public Task Time(
            [SlashCommandParameter(Description = MatchDescription, AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match,
            [SlashCommandParameter(Description = "The time in UTC, like 2026-11-20 20:00")] string time)
            => _handler.SetTime(Context.ToDiscordContext(), match, time);
    }

    [SubSlashCommand("season", "Season setup")]
    public class Season : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly AdminCommandsHandler _handler;

        public Season(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("generate", "Make the season's matches (refused once any is played)")]
        public Task Generate(
            [SlashCommandParameter(Description = RebuiltSeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string season,
            [SlashCommandParameter(Name = "home_matches", Description = "Home matches per team")] int homeMatchesPerTeam,
            [SlashCommandParameter(Name = "best_of", Description = "Games per match")] int bestOf)
            => _handler.GenerateMatches(Context.ToDiscordContext(), season, homeMatchesPerTeam, bestOf);
    }

    [SubSlashCommand("bracket", "Playoff brackets")]
    public class Bracket : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly AdminCommandsHandler _handler;

        public Bracket(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("create", "Make the season's bracket (refused once any of it is played)")]
        public Task Create(
            [SlashCommandParameter(Description = RebuiltSeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string season,
            [SlashCommandParameter(Description = "Teams in the bracket")] int participants,
            [SlashCommandParameter(Name = "best_of", Description = "Games per match")] int bestOf,
            [SlashCommandParameter(Name = "double_elimination", Description = "Double elimination")] bool doubleElimination = false)
            => _handler.CreateBracket(Context.ToDiscordContext(), season, participants, doubleElimination, bestOf);

        [SubSlashCommand("seed", "Seed the bracket by the standings")]
        public Task Seed([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
            => _handler.SeedBracket(Context.ToDiscordContext(), season);
    }

    [SubSlashCommand("draft", "The draft's captains")]
    public class Draft : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly AdminCommandsHandler _handler;

        public Draft(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("lock", "Lock the captains")]
        public Task Lock([SlashCommandParameter(Description = UpcomingSeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
            => _handler.LockCaptains(Context.ToDiscordContext(), season, @lock: true);

        [SubSlashCommand("unlock", "Unlock the captains")]
        public Task Unlock([SlashCommandParameter(Description = UpcomingSeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
            => _handler.LockCaptains(Context.ToDiscordContext(), season, @lock: false);
    }

    [SubSlashCommand("schedule", "Schedule recommendations")]
    public class Schedule : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly AdminCommandsHandler _handler;

        public Schedule(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("show", "The latest recommendation")]
        public Task Show([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
            => _handler.ScheduleShow(Context.ToDiscordContext(), season);

        [SubSlashCommand("run", "Work out a new recommendation")]
        public Task Run([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
            => _handler.ScheduleRun(Context.ToDiscordContext(), season);

        [SubSlashCommand("apply", "Schedule the matches as recommended")]
        public Task Apply([SlashCommandParameter(Description = "The recommendation's number")] int recommendation)
            => _handler.ScheduleApply(Context.ToDiscordContext(), recommendation);
    }

    [SubSlashCommand("halo", "Halo data (sysadmins)")]
    public class Halo : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly AdminCommandsHandler _handler;

        public Halo(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("pull", "Pull a Halo game's stats")]
        public Task Pull([SlashCommandParameter(Name = "match_id", Description = "The game's Halo match ID")] string gameID)
            => _handler.HaloPull(Context.ToDiscordContext(), gameID);
    }

    [SubSlashCommand("jobs", "Background jobs (sysadmins)")]
    public class Jobs : ApplicationCommandModule<ApplicationCommandContext>
    {
        private const string JobDescription = "The job, as group/name";
        private readonly AdminCommandsHandler _handler;

        public Jobs(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("list", "The jobs and their state")]
        public Task List() => _handler.Jobs(Context.ToDiscordContext());

        [SubSlashCommand("run", "Run a job now")]
        public Task Run([SlashCommandParameter(Description = JobDescription)] string job)
            => _handler.Job(Context.ToDiscordContext(), job, pause: null);

        [SubSlashCommand("pause", "Pause or resume a job")]
        public Task Pause(
            [SlashCommandParameter(Description = JobDescription)] string job,
            [SlashCommandParameter(Description = "Pause it (false resumes it)")] bool paused = true)
            => _handler.Job(Context.ToDiscordContext(), job, paused);
    }

    [SubSlashCommand("claims", "Gamertag claims (sysadmins)")]
    public class Claims : ApplicationCommandModule<ApplicationCommandContext>
    {
        private const string ClaimDescription = "The claim's number";
        private readonly AdminCommandsHandler _handler;

        public Claims(AdminCommandsHandler handler)
        {
            _handler = handler;
        }

        [SubSlashCommand("list", "Pending claims")]
        public Task List() => _handler.Claims(Context.ToDiscordContext());

        [SubSlashCommand("approve", "Approve a claim")]
        public Task Approve([SlashCommandParameter(Description = ClaimDescription)] int claim)
            => _handler.DecideClaim(Context.ToDiscordContext(), claim, approve: true, reason: null);

        [SubSlashCommand("reject", "Reject a claim")]
        public Task Reject(
            [SlashCommandParameter(Description = ClaimDescription)] int claim,
            [SlashCommandParameter(Description = "Why, for the claimant")] string? reason = null)
            => _handler.DecideClaim(Context.ToDiscordContext(), claim, approve: false, reason);
    }
}
