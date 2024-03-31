using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Teams;

public class TeamService
{
    private readonly GrifballContext _context;
    public TeamService(GrifballContext context)
    {
        _context = context;
    }

    public Task<List<TeamResponseDto>> GetTeams(int seasonID, CancellationToken ct)
    {
        return _context.Teams
            .AsNoTracking()
            .AsSplitQuery()
            .Where(team => team.SeasonID == seasonID)
            .Select(team => new TeamResponseDto()
            {
                TeamID = team.TeamID,
                TeamName = team.TeamName,
                Captain = new CaptainDto()
                {
                    PersonID = team.CaptainID,
                    Name = team.Captain.Person.Name,
                    Order = team.Captain.DraftCaptainOrder,
                },
                Players = team.TeamPlayers
                    .Where(tp => tp.TeamPlayerID != team.CaptainID)
                    .Select(tp => new PlayerDto()
                    {
                        Name = tp.Person.Name,
                        PersonID = tp.PlayerID,
                        Pick = tp.DraftPick,
                        Round = tp.DraftRound,
                    })
                    .ToList(),
            })
            .ToListAsync(ct);
    }

    public Task<List<PlayerDto>> GetPlayerPool(int seasonID, CancellationToken ct)
    {
        return _context.SeasonSignups
            .AsNoTracking()
            .AsSplitQuery()
            // Filter only approved signups this season
            .Where(signup => signup.SeasonID == seasonID && signup.Approved)
            // Only care about the person
            .Select(signup => signup.Person)
            // Filter out any people that are already on a team for this season
            .Where(person => !_context.TeamPlayers.Where(tp => tp.Team.SeasonID == seasonID).Any(tp => tp.PlayerID == person.PersonID))
            .Select(person => new PlayerDto()
            {
                PersonID = person.PersonID,
                Name = person.Name,
            })
            .ToListAsync(ct);
    }
}
