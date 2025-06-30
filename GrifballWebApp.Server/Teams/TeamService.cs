using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Teams.Handlers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Teams;

public class TeamService
{
    private readonly GrifballContext _context;
    private readonly IPublisher _publisher;

    public TeamService(GrifballContext context, IPublisher publisher)
    {
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
            .Select(team => new TeamResponseDto()
            {
                TeamID = team.TeamID,
                TeamName = team.TeamName,
                Captain = new CaptainDto()
                {
                    PersonID = team.Captain.User.Id,
                    Name = team.Captain.User.XboxUser!.Gamertag ?? team.Captain.User.DiscordUser!.DiscordUsername ?? team.Captain.User.DisplayName ?? team.Captain.User.UserName ?? "",
                    Order = team.Captain.DraftCaptainOrder,
                },
                Players = team.TeamPlayers
                    .Where(tp => tp.TeamPlayerID != team.CaptainID)
                    .Select(tp => new PlayerDto()
                    {
                        Name = tp.User.XboxUser!.Gamertag ?? tp.User.DiscordUser!.DiscordUsername ?? tp.User.DisplayName ?? tp.User.UserName ?? "",
                        PersonID = tp.UserID,
                        Pick = tp.DraftPick, // TODO: Investigate removing this prop
                        Round = tp.DraftRound,
                    })
                    .OrderBy(tp => tp.Round)
                    .ToList(),
            })
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
            .Where(person => !_context.TeamPlayers.Where(tp => tp.Team.SeasonID == seasonID).Any(tp => tp.UserID == person.Id))
            .Select(person => new PlayerDto()
            {
                PersonID = person.Id,
                Name = person.XboxUser!.Gamertag ?? person.DiscordUser!.DiscordUsername ?? person.DisplayName ?? person.UserName ?? "",
            })
            .ToListAsync(ct);
    }

    public async Task AddCaptain(CaptainPlacementDto dto, bool resortOnly, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // Grab existing captains for this season
        var existingCaptains = await _context.TeamPlayers
            .Where(tp => tp.Team.SeasonID == dto.SeasonID)
            .Where(tp => tp.CaptainTeam != null)
            .OrderBy(x => x.DraftCaptainOrder)
            .ToListAsync(ct);

        // also need check that we are not adding after teams are locked in
        var captain = existingCaptains.FirstOrDefault(tp => tp.UserID == dto.PersonID);
        
        if (resortOnly is false)
        {
            // If we are only doing a resort then the captain should not already exist
            if (captain is not null)
                throw new TeamServiceException("Player is already a captain");

            var signup = await _context.SeasonSignups
                .Where(x => x.SeasonID == dto.SeasonID && x.UserID == dto.PersonID)
                .FirstOrDefaultAsync(ct);

            if (signup is null)
                throw new TeamServiceException("Player is not signed up");

            // Also need to make sure they are not on another team
            var isAlreadyOnTeam = await _context.TeamPlayers.AnyAsync(x => x.Team.SeasonID == dto.SeasonID && x.UserID == dto.PersonID, ct);
            if (isAlreadyOnTeam)
                throw new TeamServiceException("Player is already on a team");

            var newTeam = new Team()
            {
                SeasonID = dto.SeasonID,
                TeamName = signup.TeamName,
                Captain = null, // Will be set after initial save changes
            };
            captain = new TeamPlayer()
            {
                UserID = dto.PersonID,
                DraftCaptainOrder = dto.OrderNumber,
            };
            newTeam.TeamPlayers.Add(captain);

            if (string.IsNullOrEmpty(newTeam.TeamName))
            {
                var name = await _context.Users.Where(p => p.Id == captain.UserID).Select(x => x.XboxUser.Gamertag).FirstOrDefaultAsync(ct);
                name ??= await _context.Users.Where(p => p.Id == captain.UserID).Select(x => x.DiscordUser.DiscordUsername).FirstOrDefaultAsync(ct);
                name = await _context.Users.Where(p => p.Id == captain.UserID).Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
                name ??= await _context.Users.Where(p => p.Id == captain.UserID).Select(x => x.UserName).FirstOrDefaultAsync(ct);
                newTeam.TeamName = $"{name}'s Team";
                var teamNameTaken = await _context.Teams.AnyAsync(x => x.TeamName == newTeam.TeamName && x.SeasonID == dto.SeasonID, ct);
                if (teamNameTaken)
                    newTeam.TeamName = $"{name}'s Team {Guid.NewGuid().ToString()[..4]}"; // Add a random suffix to make it unique
            }

            await _context.Teams.AddAsync(newTeam, ct);
            await _context.SaveChangesAsync(ct);
            newTeam.Captain = captain;
            await _context.SaveChangesAsync(ct);

            // TODO: make sure order number is valid
            existingCaptains.Insert(dto.OrderNumber - 1, newTeam.Captain);
        }
        else // If we are doing a resort
        {
            if (captain is null)
                throw new TeamServiceException("Player is not a captain");

            existingCaptains.Remove(captain);
            // TODO: make sure order number is valid
            existingCaptains.Insert(dto.OrderNumber - 1, captain);
        }

        foreach (var (index, item) in existingCaptains.Select((item, index) => (index, item)))
        {
            item.DraftCaptainOrder = index + 1;
        }

        var f = _context.ChangeTracker.DebugView.ShortView;
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (resortOnly)
        {
            _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
        }
        else
        {
            _ = _publisher.Publish(Notification.Create(signalRConnectionID, new CaptainAddedDto()
            {
                SeasonID = dto.SeasonID,
                PersonID = dto.PersonID,
                CaptainName = await _context.Users.Where(p => p.Id == captain.UserID).Select(p => p.DisplayName).FirstOrDefaultAsync() ?? "Person",
                TeamName = captain.Team?.TeamName ?? "Team",
                OrderNumber = dto.OrderNumber,
            }));
        }
    }

    public async Task RemoveCaptain(RemoveCaptainDto dto, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // Grab existing captains for this season
        var existingCaptains = await _context.TeamPlayers
            .Where(tp => tp.Team.SeasonID == dto.SeasonID)
            .Where(tp => tp.CaptainTeam != null)
            .OrderBy(x => x.DraftCaptainOrder)
            .ToListAsync(ct);

        var captain = existingCaptains.FirstOrDefault(tp => tp.UserID == dto.PersonID);
        
        if (captain is null)
            throw new TeamServiceException("Player is not a captain");

        existingCaptains.Remove(captain);

        var team = await _context.Teams.Where(x => x.Captain == captain).FirstOrDefaultAsync(ct);

        _context.TeamPlayers.Remove(captain);
        team!.Captain = null;
        await _context.SaveChangesAsync(ct);
        _context.Teams.Remove(team!);

        foreach (var (index, item) in existingCaptains.Select((item, index) => (index, item)))
        {
            item.DraftCaptainOrder = index + 1;
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    public async Task RemovePlayerFromTeam(RemovePlayerFromTeamRequestDto dto, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var players = await GetPlayersForTeam(seasonID: dto.SeasonID, captainID: dto.CaptainID, ct);

        var player = players.FirstOrDefault(tp => tp.UserID == dto.PersonID);

        if (player is null)
            throw new TeamServiceException("Player is not on this team");

        // Mark for deletion
        _context.TeamPlayers.Remove(player);

        // Remove the player from players and fix DraftRound
        players.Remove(player);
        foreach (var (index, item) in players.Select((item, index) => (index, item)))
        {
            item.DraftRound = index + 1;
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    public async Task AddPlayerToTeam(AddPlayerToTeamRequestDto dto, int requestorId, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var isPlayerAlreadyOnTeam = await _context.TeamPlayers.AnyAsync(x => x.Team.SeasonID == dto.SeasonID && x.User.Id == dto.PersonID, ct);
        if (isPlayerAlreadyOnTeam)
            throw new TeamServiceException("This player is already on a team");
        var hasPlayerSignedUp = await _context.SeasonSignups.AnyAsync(x => x.SeasonID == dto.SeasonID && x.UserID == dto.PersonID, ct);
        if (!hasPlayerSignedUp)
            throw new TeamServiceException("This player has not signed up");

        var players = await GetPlayersForTeam(seasonID: dto.SeasonID, captainID: dto.CaptainID, ct);

        var player = players.FirstOrDefault(tp => tp.UserID == dto.PersonID);

        if (player is not null)
            throw new TeamServiceException("Player is already on this team team");

        var team = await _context.Teams
                .Include(t => t.Captain)
                .Where(t => t.SeasonID == dto.SeasonID)
                .Where(t => t.Captain.User.Id == dto.CaptainID)
                .FirstOrDefaultAsync(ct) ?? throw new TeamServiceException("Team not found");

        // Need check that player is captain and is their turn
        var isCommissioner = await _context.Users
            .Where(u => u.Id == requestorId)
            .Where(u => u.UserRoles.Any(r => r.Role.Name == "Commissioner"))
            .AnyAsync(ct);

        // Make sure it is valid for this person to be making this request
        if (isCommissioner is false)
        {
            var isCaptain = team.Captain.UserID == requestorId;

            if (isCaptain is false)
            {
                throw new TeamServiceException("You cannot make picks on behalf of another captain unless you are the commissioner");
            }

            Team onDeck = await OnDeck(dto.SeasonID, ct) ?? throw new TeamServiceException("Failed to find on deck team");

            if (onDeck.Captain.UserID != requestorId)
            {
                throw new TeamServiceException($"It is not your turn to be making a draft pick. It is {onDeck.Captain.UserID}'s turn");
            }
        }

        player = new TeamPlayer()
        {
            TeamID = team.TeamID,
            UserID = dto.PersonID,
            DraftRound = players.OrderByDescending(p => p.DraftRound).Select(p => p.DraftRound + 1).FirstOrDefault() ?? 1, // Always add to end, or round 1
        };
        if (player.TeamID is 0)
            throw new TeamServiceException("Could not find team with that captain and season");
        await _context.TeamPlayers.AddAsync(player);

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
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
        var players = await GetPlayersForTeam(seasonID: dto.SeasonID, captainID: dto.NewCaptainID, ct);

        TeamPlayer? player = null;

        // If we are moving between teams
        if (dto.NewCaptainID != dto.PreviousCaptainID)
        {
            var oldTeam = await GetPlayersForTeam(seasonID: dto.SeasonID, captainID: dto.PreviousCaptainID, ct);

            player = oldTeam.FirstOrDefault(tp => tp.UserID == dto.PersonID);
            if (player is null)
                throw new TeamServiceException("Player was not on that team");

            oldTeam.Remove(player);
            // Then we must fix the round numbers for the old team
            foreach (var (index, item) in oldTeam.Select((item, index) => (index, item)))
            {
                item.DraftRound = index + 1;
            }

            player.TeamID = await _context.Teams
                .Where(t => t.SeasonID == dto.SeasonID)
                .Where(t => t.Captain.UserID == dto.NewCaptainID)
                .Select(t => t.TeamID)
                .FirstOrDefaultAsync(ct);
            if (player.TeamID is 0)
                throw new TeamServiceException("New team does not exist");
        }
        else // otherwise Inter-team move
        {
            player = players.FirstOrDefault(tp => tp.UserID == dto.PersonID);
            if (player is null)
                throw new TeamServiceException("Player is not on that team");

            // We must remove from the current position
            players.Remove(player);
        }

        // Now we reinsert in the correct position
        // TODO: make sure round number is valid
        players.Insert(dto.RoundNumber - 1, player);

        foreach (var (index, item) in players.Select((item, index) => (index, item)))
        {
            item.DraftRound = index + 1;
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _ = _publisher.Publish(Notification.Create(signalRConnectionID, dto));
    }

    private Task<List<TeamPlayer>> GetPlayersForTeam(int seasonID, int captainID, CancellationToken ct = default)
    {
        return _context.TeamPlayers
            .Include(tp => tp.User)
            .Where(tp => tp.Team.SeasonID == seasonID)
            .Where(tp => tp.Team.Captain.User.Id == captainID)
            .Where(tp => tp.CaptainTeam == null)
            .OrderBy(x => x.DraftRound)
            .ToListAsync(ct);
    }

    public async Task LockCaptains(int seasonID, bool @lock, string? signalRConnectionID = null, CancellationToken ct = default)
    {
        var season = await _context.Seasons
            .Where(s => s.SeasonID == seasonID)
            .FirstOrDefaultAsync(ct);

        if (season == null)
            throw new TeamServiceException("Season does not exist");

        season.CaptainsLocked = @lock;

        await _context.SaveChangesAsync(ct);

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