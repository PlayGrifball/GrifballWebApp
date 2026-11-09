using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Teams;

public class TeamService
{
    private readonly GrifballContext _context;
    private readonly IPublisher _publisher;
    private readonly IOutbox _outbox;

    public TeamService(GrifballContext context, IPublisher publisher, IOutbox outbox)
    {
        _outbox = outbox;
        _publisher = publisher;
        _context = context;
    }

    public Task<List<TeamResponseDto>> GetTeams(int seasonID, CancellationToken ct = default)
    {
        return _context.Teams
            .AsNoTracking()
            .AsSplitQuery()
            .Where(team => team.SeasonID == seasonID)
            .OrderBy(team => team.Captain.DraftCaptainOrder)
            .Select(Projections.Inline((Team team) => new TeamResponseDto()
            {
                TeamID = team.TeamID,
                TeamName = team.TeamName,
                Captain = new CaptainDto()
                {
                    PersonID = team.Captain.User.Id,
                    Name = UserNames.Pick(team.Captain.User.XboxUser!.Gamertag, team.Captain.User.DiscordUser!.DiscordUsername, team.Captain.User.DisplayName, team.Captain.User.UserName),
                    Order = team.Captain.DraftCaptainOrder,
                    Region = team.Captain.User.Region != null ? team.Captain.User.Region.RegionName : null,
                },
                Players = team.Apply(Team.PicksOf)
                    .Select(tp => new PlayerDto()
                    {
                        Name = UserNames.Pick(tp.User.XboxUser!.Gamertag, tp.User.DiscordUser!.DiscordUsername, tp.User.DisplayName, tp.User.UserName),
                        PersonID = tp.UserID,
                        Pick = tp.DraftPick, // TODO: Investigate removing this prop
                        Round = tp.DraftRound,
                        Region = tp.User.Region != null ? tp.User.Region.RegionName : null,
                    })
                    .OrderBy(tp => tp.Round)
                    .ToList(),
            }))
            .ToListAsync(ct);
    }

    public Task<List<PlayerDto>> GetPlayerPool(int seasonID, CancellationToken ct = default)
    {
        return _context.SeasonSignups
            .AsNoTracking()
            .AsSplitQuery()
            // Filter only approved signups this season
            .Where(signup => signup.SeasonID == seasonID)
            // Only care about the person
            .Select(signup => signup.User)
            // Filter out any people that are already on a team for this season
            .Where(person => !_context.Rosters().Where(tp => tp.Team.SeasonID == seasonID).Any(tp => tp.UserID == person.Id))
            .Select(person => new PlayerDto()
            {
                PersonID = person.Id,
                Name = UserNames.Pick(person.XboxUser!.Gamertag, person.DiscordUser!.DiscordUsername, person.DisplayName, person.UserName),
                Region = person.Region != null ? person.Region.RegionName : null,
            })
            .ToListAsync(ct);
    }

    /// <returns>The new team's name when adding a captain (the signup's team name, or "&lt;name&gt;'s Team"); null for a resort.</returns>
    public async Task<string?> AddCaptain(CaptainPlacementDto dto, bool resortOnly, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await LockSeasonDraft(dto.SeasonID, ct);
        await ThrowIfCaptainsLocked(dto.SeasonID, ct);
        // Grab the season's captained teams, in the captains' order
        var captainedTeams = await CaptainedTeams(dto.SeasonID, ct);

        // also need check that we are not adding after teams are locked in
        var team = captainedTeams.FirstOrDefault(t => t.IsCaptainedBy(dto.PersonID));

        if (resortOnly is false)
        {
            // If we are only doing a resort then the captain should not already exist
            if (team is not null)
                throw new TeamServiceException("Player is already a captain");

            var signup = await _context.SeasonSignups
                .Where(SeasonSignup.Of(dto.SeasonID, dto.PersonID))
                .FirstOrDefaultAsync(ct);

            if (signup is null)
                throw new TeamServiceException("Player is not signed up");

            // Also need to make sure they are not on another team
            var isAlreadyOnTeam = await _context.Rosters().AnyAsync(x => x.Team.SeasonID == dto.SeasonID && x.UserID == dto.PersonID, ct);
            if (isAlreadyOnTeam)
                throw new TeamServiceException("Player is already on a team");

            // The name the signup asked for, else "<name>'s Team"; either way unique in the season (a unique index)
            var teamName = signup.TeamNameAtDraft(await PersonName(dto.PersonID, ct));
            var teamNameTaken = await _context.Teams.AnyAsync(x => x.TeamName == teamName && x.SeasonID == dto.SeasonID, ct);
            if (teamNameTaken)
                teamName = $"{Truncate(teamName, TeamNameMaxLength - 5)} {Guid.NewGuid().ToString()[..4]}"; // Add a random suffix to make it unique

            team = Team.Drafted(dto.SeasonID, Truncate(teamName, TeamNameMaxLength), dto.PersonID, dto.OrderNumber);
            await _context.Teams.AddAsync(team, ct);
            // The captain is one of the roster, which is saved with the team first (each points at the other)
            await _context.SaveChangesAsync(ct);
            team.SetCaptain(dto.PersonID);
            await _context.SaveChangesAsync(ct);

            // TODO: make sure order number is valid
            captainedTeams.Insert(dto.OrderNumber - 1, team);
        }
        else // If we are doing a resort
        {
            if (team is null)
                throw new TeamServiceException("Player is not a captain");

            captainedTeams.Remove(team);
            // TODO: make sure order number is valid
            captainedTeams.Insert(dto.OrderNumber - 1, team);
        }

        foreach (var (index, item) in captainedTeams.Select((item, index) => (index, item)))
        {
            item.OrderCaptain(index + 1);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (resortOnly)
        {
            _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
            return null;
        }

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, new CaptainAddedDto()
        {
            SeasonID = dto.SeasonID,
            PersonID = dto.PersonID,
            TeamID = team.TeamID,
            CaptainName = await PersonName(dto.PersonID, ct),
            TeamName = team.TeamName,
            OrderNumber = dto.OrderNumber,
        }));
        // The broadcast skips the sender's connection, so the sender gets the name here.
        return team.TeamName;
    }

    public async Task RemoveCaptain(RemoveCaptainDto dto, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await LockSeasonDraft(dto.SeasonID, ct);
        await ThrowIfCaptainsLocked(dto.SeasonID, ct);
        // Grab the season's captained teams, in the captains' order
        var captainedTeams = await CaptainedTeams(dto.SeasonID, ct);

        var team = captainedTeams.FirstOrDefault(t => t.IsCaptainedBy(dto.PersonID));

        if (team is null)
            throw new TeamServiceException("Player is not a captain");

        captainedTeams.Remove(team);

        // The captain leaves the roster (no captain then), and the team goes with them
        await _context.Teams.WithRoster().Where(t => t.TeamID == team.TeamID).LoadAsync(ct);
        team.RemovePlayer(dto.PersonID);
        await _context.SaveChangesAsync(ct);
        _context.Teams.Remove(team);

        foreach (var (index, item) in captainedTeams.Select((item, index) => (index, item)))
        {
            item.OrderCaptain(index + 1);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    public async Task RemovePlayerFromTeam(RemovePlayerFromTeamRequestDto dto, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await LockSeasonDraft(dto.SeasonID, ct);
        var team = await CaptainsTeam(seasonID: dto.SeasonID, captainID: dto.CaptainID, ct);

        // The pick goes, and the picks after it move up a round
        if (team?.Undraft(dto.PersonID) is not TeamRosterResult.Done)
            throw new TeamServiceException("Player is not on this team");

        await _context.SaveChangesAsync(ct);
        // The draft channel is told with the change: the outbox sends it once committed.
        await _outbox.EnqueuePlayerRemovedAsync(_context, this, dto, ct);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    public async Task AddPlayerToTeam(AddPlayerToTeamRequestDto dto, int requestorId, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // One pick at a time per season: without it two picks of one player (or two picks in one turn) both pass the
        // checks below before either is saved.
        await LockSeasonDraft(dto.SeasonID, ct);

        var isPlayerAlreadyOnTeam = await _context.Rosters().AnyAsync(x => x.Team.SeasonID == dto.SeasonID && x.UserID == dto.PersonID, ct);
        if (isPlayerAlreadyOnTeam)
            throw new TeamServiceException("This player is already on a team");
        var hasPlayerSignedUp = await _context.SeasonSignups.AnyAsync(SeasonSignup.Of(dto.SeasonID, dto.PersonID), ct);
        if (!hasPlayerSignedUp)
            throw new TeamServiceException("This player has not signed up");

        var team = await CaptainsTeam(seasonID: dto.SeasonID, captainID: dto.CaptainID, ct) ?? throw new TeamServiceException("Team not found");

        // Need check that player is captain and is their turn
        var isCommissioner = await _context.Users
            .Where(u => u.Id == requestorId)
            .Where(u => u.UserRoles.Any(r => r.Role.Name == "Commissioner"))
            .AnyAsync(ct);

        // Make sure it is valid for this person to be making this request
        if (isCommissioner is false)
        {
            var isCaptain = team.IsCaptainedBy(requestorId);

            if (isCaptain is false)
            {
                throw new TeamServiceException("You cannot make picks on behalf of another captain unless you are the commissioner");
            }

            // Captains pick once the draft order is set; a commissioner may place a player any time.
            if (await AreCaptainsLocked(dto.SeasonID, ct) is false)
                throw new TeamServiceException("The draft has not started: the captains are not locked yet");

            Team onDeck = await OnDeck(dto.SeasonID, ct) ?? throw new TeamServiceException("Failed to find on deck team");

            if (!onDeck.IsCaptainedBy(requestorId))
            {
                throw new TeamServiceException($"It is not your turn to be making a draft pick. It is {onDeck.Captain.UserID}'s turn");
            }
        }

        // Always added at the end, or round 1 (a player on a team of the season was refused above)
        team.Draft(dto.PersonID);

        await _context.SaveChangesAsync(ct);
        // The draft channel is told with the change: the outbox sends it once committed.
        await _outbox.EnqueuePlayerAddedAsync(_context, this, dto, ct);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    /// <summary>The id of the team <paramref name="captainID"/> captains in the season; 0 when none.</summary>
    public Task<int> CaptainsTeamID(int seasonID, int captainID, CancellationToken ct = default)
    {
        return _context.Teams
            .Where(t => t.SeasonID == seasonID)
            .Where(Team.CaptainedBy(captainID))
            .Select(t => t.TeamID)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Team?> OnDeck(int seasonId, CancellationToken ct)
    {
        return await _context.Teams
                        .Include(t => t.Captain)
                        .Where(t => t.SeasonID == seasonId)
                        .OrderBy(t => t.TeamPlayers.Count)
                        .ThenBy(t => t.Captain.DraftCaptainOrder)
                        .FirstOrDefaultAsync(ct);
    }

    public async Task MovePlayerToTeam(MovePlayerToTeamRequestDto dto, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await LockSeasonDraft(dto.SeasonID, ct);
        var team = await CaptainsTeam(seasonID: dto.SeasonID, captainID: dto.NewCaptainID, ct);

        TeamRosterResult result;
        // If we are moving between teams
        if (dto.NewCaptainID != dto.PreviousCaptainID)
        {
            var oldTeam = await CaptainsTeam(seasonID: dto.SeasonID, captainID: dto.PreviousCaptainID, ct);
            if (oldTeam is null || !oldTeam.Picks.Any(tp => tp.UserID == dto.PersonID))
                throw new TeamServiceException("Player was not on that team");
            if (team is null)
                throw new TeamServiceException("New team does not exist");

            // The old team's picks after it move up a round, and it goes in at the round given
            result = oldTeam.TradePick(dto.PersonID, team, dto.RoundNumber);
        }
        else // otherwise Inter-team move
        {
            result = team?.MovePick(dto.PersonID, dto.RoundNumber) ?? TeamRosterResult.NotOnTeam;
        }

        if (result is TeamRosterResult.RoundOutOfRange)
            throw new TeamServiceException("Round number is out of range");
        if (result is not TeamRosterResult.Done)
            throw new TeamServiceException("Player is not on that team");

        await _context.SaveChangesAsync(ct);
        // The draft channel is told with the change: the outbox sends it once committed.
        await _outbox.EnqueuePlayerMovedAsync(_context, this, dto, ct);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    /// <summary>
    /// Locks the season's row until the transaction ends, so that changes to one season's draft run one at a time and
    /// each one's checks see the one before it. Reading the row this way writes nothing (no history). SQLite needs no
    /// lock: its transactions start IMMEDIATE (Microsoft.Data.Sqlite's default), holding the whole file's write lock
    /// from the start, so one season's changes, and every other write, already run one at a time.
    /// </summary>
    private Task LockSeasonDraft(int seasonID, CancellationToken ct)
    {
        if (_context.Database.IsSqlite())
            return Task.CompletedTask;
        return _context.Database.IsNpgsql()
            ? _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Event\".\"Seasons\" WHERE \"SeasonID\" = {seasonID} FOR UPDATE", ct)
            : _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM [Event].[Seasons] WITH (UPDLOCK, HOLDLOCK) WHERE [SeasonID] = {seasonID}", ct);
    }

    /// <summary>Captains can't be added, moved or removed once they are locked (the draft order is set): unlock them first.</summary>
    private async Task ThrowIfCaptainsLocked(int seasonID, CancellationToken ct)
    {
        if (await AreCaptainsLocked(seasonID, ct))
            throw new TeamServiceException("Captains are locked for this season. Unlock them to change the captains");
    }

    /// <summary>A person's name as the draft shows it (<see cref="GetTeams"/>, <see cref="UserNames.Pick"/>).</summary>
    private async Task<string> PersonName(int userID, CancellationToken ct)
    {
        return await _context.Users
            .Where(u => u.Id == userID)
            .Select(u => UserNames.Pick(u.XboxUser!.Gamertag, u.DiscordUser!.DiscordUsername, u.DisplayName, u.UserName))
            .FirstOrDefaultAsync(ct) ?? "";
    }

    // Signups can't have a longer team name (SignupsController), so this only cuts a generated "Name's Team", one with
    // the uniqueness suffix, or a name signed up before that check.
    private const int TeamNameMaxLength = global::GrifballWebApp.Database.Models.Team.TeamNameMaxLength;

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    /// <summary>The team <paramref name="captainID"/> captains in the season, with its roster; null when none.</summary>
    private Task<Team?> CaptainsTeam(int seasonID, int captainID, CancellationToken ct = default)
    {
        return _context.Teams
            .WithRoster()
            .Where(t => t.SeasonID == seasonID)
            .Where(Team.CaptainedBy(captainID))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>The season's teams with a captain (their captain's row loaded), in the captains' order.</summary>
    private Task<List<Team>> CaptainedTeams(int seasonID, CancellationToken ct)
    {
        return _context.Teams
            .Include(t => t.Captain)
            .Where(t => t.SeasonID == seasonID && t.Captain != null)
            .OrderBy(t => t.Captain.DraftCaptainOrder)
            .ToListAsync(ct);
    }

    public async Task LockCaptains(int seasonID, bool @lock, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        var season = await _context.Seasons
            .Where(s => s.SeasonID == seasonID)
            .FirstOrDefaultAsync(ct);

        if (season == null)
            throw new TeamServiceException("Season does not exist");

        season.LockCaptains(@lock);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _context.SaveChangesAsync(ct);
        // The draft channel is told with the change: the outbox sends it once committed.
        await _outbox.EnqueueLockChangedAsync(_context, this, seasonID, @lock, ct);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(new LockChanged(@lock, seasonID, signalRConnectionID));
    }

    public async Task<bool> AreCaptainsLocked(int seasonID, CancellationToken ct = default)
    {
        if (!await _context.Seasons.Where(s => s.SeasonID == seasonID).AnyAsync(ct))
            throw new TeamServiceException("Season does not exist");

        return await _context.Seasons
            .Where(s => s.SeasonID == seasonID)
            .Select(s => s.CaptainsLocked)
            .FirstOrDefaultAsync(ct);
    }
}


public class TeamServiceException : Exception
{
    public TeamServiceException(string message) : base(message)
    {
    }
    public TeamServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}