using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Brackets;

public interface IBracketService
{
    Task CreateBracket(int participantsCount, int seasonID, bool doubleElimination, int bestOf, CancellationToken ct = default);
    Task<BracketDto> GetBracketsAsync(int seasonID, CancellationToken ct = default);
    /// <returns>The bracket for the viewer, or null when the season does not exist.</returns>
    Task<ViewerDataDto?> GetViewerDataAsync(int seasonID, CancellationToken ct = default);
    Task SetSeeds(int seasonID, CustomSeedDto[]? customSeeds = null, CancellationToken ct = default);
    NextMatchesDto DetermineNextMatches(SeasonMatch seasonMatch);
    Task AdvanceByesAsync(int seasonID, CancellationToken ct = default);
    void SendByesToNextMatches(SeasonMatch seasonMatch);
    Task UndoAdvancementAsync(SeasonMatch seasonMatch, CancellationToken ct = default);
    Task<string?> UndoAdvancementRefusalAsync(SeasonMatch seasonMatch, CancellationToken ct = default);
}

public class BracketService : IBracketService
{
    private readonly GrifballContext _grifballContext;
    private readonly TeamStandingsService _teamStandingsService;
    private readonly GrifballWebApp.Server.Championships.ChampionshipService _championshipService;
    public BracketService(GrifballContext grifballContext, TeamStandingsService teamStandingsService, GrifballWebApp.Server.Championships.ChampionshipService championshipService)
    {
        _grifballContext = grifballContext;
        _teamStandingsService = teamStandingsService;
        _championshipService = championshipService;
    }

    public async Task CreateBracket(int participantsCount, int seasonID, bool doubleElimination, int bestOf, CancellationToken ct = default)
    {
        using var t = await _grifballContext.Database.BeginTransactionAsync(ct);
        var bracket = _grifballContext.SeasonMatches.Where(x => x.SeasonID == seasonID && x.BracketMatch != null);
        await ManualGames.DeleteForAsync(_grifballContext, bracket, ct);
        var deletedCount = await bracket.ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            // TODO add guard against user deleting bracket unless they approve
        }

        await _grifballContext.SaveChangesAsync(ct);

        var numberOfRounds = GetNumberOfRounds(participantsCount);
        var matchUps = GetSeedMatchUps(participantsCount);
        var matchNumber = 1;

        var matches = new List<SeasonMatch>();
        // The first round is setup with the seed numbers
        // Pervious match is null because these are the very first games in the bracket
        // Seeds past the participant count are byes: the slot keeps its seed number and is marked as a BYE
        foreach (var matchup in matchUps)
        {
            var match = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Seeded(Bracket.Winner, 1, matchNumber, matchup.Home, matchup.Away), bestOf, homeBye: matchup.Home > participantsCount, awayBye: matchup.Away > participantsCount);
            matches.Add(match);
            matchNumber++;
        }

        var amtToSkip = 0;

        // Subsequent rounds are setup based on the victors of the previous round
        for (int round = 2; round <= numberOfRounds; round++)
        {
            var matchesCountForRound = (int)Math.Pow(2, numberOfRounds - round);

            for (int i = 1; i <= matchesCountForRound; i++)
            {
                var dependentMatches = matches.Skip(amtToSkip).Take(2).ToList();
                amtToSkip += 2;
                if (dependentMatches.Count != 2)
                    throw new Exception("Failed to get dependent matches while building bracket");

                var match = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Following(Bracket.Winner, round, matchNumber, dependentMatches[0].BracketMatch, dependentMatches[1].BracketMatch), bestOf);
                matches.Add(match);
                matchNumber++;
            }
        }

        if (doubleElimination)
        {
            var lastWinnerBracketMatch = matches.LastOrDefault()
                ?? throw new Exception("Failed to find the last winner bracket match");

            if (lastWinnerBracketMatch.BracketMatch is null)
                throw new Exception("Last winner bracket match is missing bracket info");

            // Add grand final
            var grandFinal = SeasonMatch.InBracket(seasonID, MatchBracketInfo.GrandFinal(numberOfRounds + 1, matchNumber, lastWinnerBracketMatch.BracketMatch), bestOf);

            matches.Add(grandFinal);
            matchNumber++;

            // Losers may force a second match
            var suddenDeath = SeasonMatch.InBracket(seasonID, MatchBracketInfo.SuddenDeath(numberOfRounds + 2, matchNumber, grandFinal.BracketMatch), bestOf);

            matches.Add(suddenDeath);

            // Start loser match count back at 1
            matchNumber = 1;

            // Loser bracket round 1 will have the same number of matches as winner bracket round 2
            // Partipants are from the losers from winner bracket round 1
            var loserMatchCountToCreate = matches.Count(x => x.BracketMatch.RoundNumber == 2);

            // Loser bracket round 2 will have the same as the first round

            // Example scenerio:
            // Round 1 has only those that just dropped from winners R1
            // Round 2 is half and half winner R2
            // Round 3 only losers
            // Repeat last 2 steps until there is only one match left to determine the winner of the loser bracket

            // We need to create X number of loser rounds based on how many winner rounds there are
            var loserRoundCount = (numberOfRounds - 1) * 2;
            var loserMatches = new List<SeasonMatch>();

            // We will create the loser rounds 2 at a time since each major and minor round as the same number of matches
            for (int loserRoundNumber = 1; loserRoundNumber <= loserRoundCount; loserRoundNumber += 2)
            {
                var minorRound = loserRoundNumber; // Odd number rounds are minor rounds and only have those that survived the previous loser round (Except R1)
                var majorRound = loserRoundNumber + 1; // Even number rounds are major rounds and have half filled by those that just got knocked from the winners bracker

                if (minorRound is 1)
                {
                    // Fill with teams from WR1
                    var skipCount = 0;
                    for (var i = 1; i <= loserMatchCountToCreate; i++)
                    {
                        var dependentMatches = matches
                            .Where(m => m.BracketMatch.RoundNumber == 1 && m.BracketMatch.Bracket is Bracket.Winner)
                            .Skip(skipCount)
                            .Take(2)
                            .ToList();
                        skipCount += 2;
                        if (dependentMatches.Count != 2)
                            throw new Exception("Failed to get dependent matches while building loser bracket");

                        var loserMatch = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Following(Bracket.Loser, minorRound, matchNumber, dependentMatches[0].BracketMatch, dependentMatches[1].BracketMatch), bestOf);
                        matchNumber++;
                        loserMatches.Add(loserMatch);
                    }
                }
                else
                {
                    // Fill with teams from previous loser round
                    // Fill with teams from WR1
                    var skipCount = 0;
                    for (var i = 1; i <= loserMatchCountToCreate; i++)
                    {
                        var dependentMatches = loserMatches
                            .Where(m => m.BracketMatch.RoundNumber == minorRound - 1 && m.BracketMatch.Bracket is Bracket.Loser)
                            .Skip(skipCount)
                            .Take(2)
                            .ToList();
                        skipCount += 2;
                        if (dependentMatches.Count != 2)
                            throw new Exception("Failed to get dependent matches while building loser bracket");

                        var loserMatch = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Following(Bracket.Loser, minorRound, matchNumber, dependentMatches[0].BracketMatch, dependentMatches[1].BracketMatch), bestOf);
                        matchNumber++;
                        loserMatches.Add(loserMatch);
                    }
                }

                // Compute corrspondingWinnerRound:
                // L2 fill from W2
                // L4 fill from W3
                // L6 fill from W4
                // L8 fill from W5
                var corrspondingWinnerRound = majorRound / 2 + 1;
                var previousLoserRound = minorRound;

                // Major round fill half from previous loser round and half from corrspondingWinnerRound
                // Fill with teams from previous loser round
                // Fill with teams from WR1
                var winnerSkipCount = 0;
                var loserSkipCount = 0;
                for (var i = 1; i <= loserMatchCountToCreate; i++)
                {
                    var previousWinnerMatch = matches
                        .Where(m => m.BracketMatch.RoundNumber == corrspondingWinnerRound && m.BracketMatch.Bracket is Bracket.Winner)
                        .Skip(winnerSkipCount)
                        .FirstOrDefault();
                    winnerSkipCount += 1;
                    if (previousWinnerMatch is null)
                        throw new Exception("Failed to get winner match while building loser bracket");

                    var previousLoserMatch = loserMatches
                        .Where(m => m.BracketMatch.RoundNumber == previousLoserRound && m.BracketMatch.Bracket is Bracket.Loser)
                        .Skip(loserSkipCount)
                        .FirstOrDefault();
                    loserSkipCount += 1;
                    if (previousLoserMatch is null)
                        throw new Exception("Failed to get loser match while building loser bracket");

                    var loserMatch = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Following(Bracket.Loser, majorRound, matchNumber, previousWinnerMatch.BracketMatch, previousLoserMatch.BracketMatch), bestOf);
                    matchNumber++;
                    loserMatches.Add(loserMatch);
                }

                loserMatchCountToCreate /= 2;
            }

            var finalLoserMatch = loserMatches.LastOrDefault() ?? throw new Exception("Failed to get last loser match");

            grandFinal.BracketMatch.AwaitLosersFinal(finalLoserMatch.BracketMatch);

            matches.AddRange(loserMatches);
        }

        await _grifballContext.AddRangeAsync(matches, ct);
        await _grifballContext.SaveChangesAsync(ct);

        // In double elimination the loser bracket slots fed by a BYE match are BYEs too
        await AdvanceByesAsync(seasonID, ct);
        await _grifballContext.SaveChangesAsync(ct);

        // A bracket made again decides nobody yet: a title the old one gave is taken back
        await _championshipService.AwardFromBracketAsync(seasonID, ct);
        await t.CommitAsync(ct);
    }

    private int GetNumberOfRounds(int participantsCount)
    {
        return (int)Math.Ceiling(Math.Log(participantsCount) / Math.Log(2));
    }

    private IEnumerable<MatchUp> GetSeedMatchUps(int participantsCount)
    {
        var rounds = GetNumberOfRounds(participantsCount);

        if (participantsCount < 2)
            return Enumerable.Empty<MatchUp>();

        List<MatchUp> matches = new List<MatchUp>()
        {
            new MatchUp(1, 2)
        };


        for (var round = 1; round < rounds; round++)
        {
            List<MatchUp> roundMatches = new();
            var sum = (int)Math.Pow(2, round + 1) + 1;

            for (var i = 0; i < matches.Count(); i++)
            {
                roundMatches.Add(new MatchUp(matches[i].Home, sum - matches[i].Home));
                roundMatches.Add(new MatchUp(sum - matches[i].Away, matches[i].Away));
            }
            matches = roundMatches;

        }

        return matches;
    }

    private struct MatchUp
    {
        public MatchUp(int home, int away)
        {
            Home = home;
            Away = away;
        }
        public int Home { get; private set; }
        public int Away { get; private set; }
    }

    public async Task<BracketDto> GetBracketsAsync(int seasonID, CancellationToken ct = default)
    {
        // The season's bracket slots, through their matches (a slot is a part of its match)
        // (each slot's SeasonMatch is its match, fixed up from the Include)
        var bracket = (await _grifballContext.SeasonMatches
            .Where(m => m.SeasonID == seasonID && m.BracketMatch != null)
            .Include(m => m.BracketMatch)
            .AsNoTracking()
            .ToListAsync(ct))
            .Select(m => m.BracketMatch!)
            .ToList();

        var winners = bracket.Where(x => x.Bracket is Bracket.Winner)
            .OrderBy(x => x.RoundNumber).ThenBy(x => x.MatchNumber)
            .GroupBy(x => x.RoundNumber);

        var winnerDtos = winners.Select(x => new RoundDto()
        {
            RoundNumber = x.Key,
            Matches = x.Select(x => new MatchDto()
            {
                MatchNumber = $"W{x.MatchNumber}",
                HomeTeam = x.SeasonMatch.IsBye(SeasonMatchSide.Home) ? "BYE" :
                    x.HomeTeamSeedNumber is not null ? $"Seed {x.HomeTeamSeedNumber}" :
                    $"Winner of W{bracket.FirstOrDefault(y => y.MatchBracketInfoID == x.HomeTeamPreviousMatchBracketInfoID)?.MatchNumber}",
                AwayTeam = x.SeasonMatch.IsBye(SeasonMatchSide.Away) ? "BYE" :
                    x.AwayTeamSeedNumber is not null ? $"Seed {x.AwayTeamSeedNumber}" :
                    $"Winner of W{bracket.FirstOrDefault(y => y.MatchBracketInfoID == x.AwayTeamPreviousMatchBracketInfoID)?.MatchNumber}",
            }).ToArray(),
        }).ToArray();

        var losers = bracket.Where(x => x.Bracket is Bracket.Loser)
            .OrderBy(x => x.RoundNumber).ThenBy(x => x.MatchNumber)
            .GroupBy(x => x.RoundNumber);

        var loserDtos = losers.Select(x => new RoundDto()
        {
            RoundNumber = x.Key,
            Matches = x.Select(x =>
            {
                var homeTeamFrom = bracket.FirstOrDefault(y => y.MatchBracketInfoID == x.HomeTeamPreviousMatchBracketInfoID);
                var homeTeamText = x.SeasonMatch.IsBye(SeasonMatchSide.Home) ? "BYE" :
                    homeTeamFrom?.Bracket is Bracket.Winner ? $"Loser of W{homeTeamFrom.MatchNumber}" : $"Winner of L{homeTeamFrom?.MatchNumber}";

                var awayTeamFrom = bracket.FirstOrDefault(y => y.MatchBracketInfoID == x.AwayTeamPreviousMatchBracketInfoID);
                var awayTeamText = x.SeasonMatch.IsBye(SeasonMatchSide.Away) ? "BYE" :
                    awayTeamFrom?.Bracket is Bracket.Winner ? $"Loser of W{awayTeamFrom.MatchNumber}" : $"Winner of L{awayTeamFrom?.MatchNumber}";

                return new MatchDto()
                {
                    MatchNumber = $"L{x.MatchNumber}",
                    HomeTeam = homeTeamText,
                    AwayTeam = awayTeamText,
                };
            }).ToArray(),
        }).ToArray();

        var grandFinal = bracket.Where(x => x.Bracket is Bracket.GrandFinal)
            .Select(x => new MatchDto()
            {
                MatchNumber = $"W{x.MatchNumber}",
                HomeTeam = $"Winner of W{x.MatchNumber - 1}",
                AwayTeam = $"Winner of L{bracket.Where(x => x.Bracket is Bracket.Loser).OrderByDescending(x => x.MatchNumber).FirstOrDefault()?.MatchNumber}"
            }).FirstOrDefault();

        var grandFinalSuddenDeath = bracket.Where(x => x.Bracket is Bracket.GrandFinalSuddenDeath)
            .Select(x => new MatchDto()
            {
                MatchNumber = $"W{x.MatchNumber}",
                HomeTeam = $"-",
                AwayTeam = $"-"
            }).FirstOrDefault();

        return new BracketDto()
        {
            WinnerRounds = winnerDtos,
            LoserRounds = loserDtos,
            GrandFinal = grandFinal,
            GrandFinalSuddenDeath = grandFinalSuddenDeath,
        };
    }

    public async Task<ViewerDataDto?> GetViewerDataAsync(int seasonID, CancellationToken ct = default)
    {
        var season = await _grifballContext.Seasons
            .Where(s => s.SeasonID == seasonID)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (season is null)
        {
            return null;
        }

        var seasonMatches = await _grifballContext.SeasonMatches
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm.HomeTeamPreviousMatchBracketInfo)
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm.AwayTeamPreviousMatchBracketInfo)
            .Include(sm => sm.HomeTeam)
            .Include(sm => sm.AwayTeam)
            .Include(sm => sm.SeriesGames) // Not sure we even need this
                .ThenInclude(l => l.Match)
            .Where(sm => sm.SeasonID == seasonID && sm.BracketMatch != null)
            .OrderBy(sm => sm.BracketMatch.MatchNumber)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        var exceptLosers = seasonMatches.Where(x => x.BracketMatch.Bracket is Bracket.Winner).ToList();
        var losers = seasonMatches.Where(x => x.BracketMatch.Bracket is Bracket.Loser).ToList();

        var result = new ViewerDataDto();

        // All teams, or if not yet decided then seeds
        result.Participants = new List<Participant>();

        foreach (var seasonMatch in exceptLosers.Where(x => x.BracketMatch.RoundNumber is 1 && x.BracketMatch.Bracket is Bracket.Winner))
        {
            if (seasonMatch.HomeTeam is null && seasonMatch.HomeTeamID is not null)
                throw new Exception("Home team not included");
            if (seasonMatch.AwayTeam is null && seasonMatch.AwayTeamID is not null)
                throw new Exception("Away team not included");

            if (seasonMatch.IsBye(SeasonMatchSide.Home))
            {
                // A BYE is not a participant
            }
            else if (seasonMatch.HomeTeam is not null)
            {
                result.Participants.Add(new Participant()
                {
                    Id = seasonMatch.HomeTeam.TeamID,
                    Tournament_id = seasonMatch.HomeTeam.SeasonID,
                    Name = seasonMatch.HomeTeam.TeamName,
                });
            }
            else if (seasonMatch.BracketMatch.HomeTeamSeedNumber is not null)
            {
                result.Participants.Add(new Participant()
                {
                    Id = SeedPlaceholderID(seasonMatch.BracketMatch.HomeTeamSeedNumber) ?? throw new Exception("Missing home seed"),
                    Tournament_id = seasonID,
                    Name = $"Seed {seasonMatch.BracketMatch.HomeTeamSeedNumber}",
                });
            }
            else
            {
                throw new Exception($"Season Match {seasonMatch.SeasonMatchID} is not valid for first round of bracket");
            }

            if (seasonMatch.IsBye(SeasonMatchSide.Away))
            {
                // A BYE is not a participant
            }
            else if (seasonMatch.AwayTeam is not null)
            {
                result.Participants.Add(new Participant()
                {
                    Id = seasonMatch.AwayTeam.TeamID,
                    Tournament_id = seasonMatch.AwayTeam.SeasonID,
                    Name = seasonMatch.AwayTeam.TeamName,
                });
            }
            else if (seasonMatch.BracketMatch.AwayTeamSeedNumber is not null)
            {
                result.Participants.Add(new Participant()
                {
                    Id = SeedPlaceholderID(seasonMatch.BracketMatch.AwayTeamSeedNumber) ?? throw new Exception("Missing away seed"),
                    Tournament_id = seasonID,
                    Name = $"Seed {seasonMatch.BracketMatch.AwayTeamSeedNumber}",
                });
            }
            else
            {
                throw new Exception($"Season Match {seasonMatch.SeasonMatchID} is not valid for first round of bracket");
            }
        }

        var stageType = losers.Any() ? StageType.double_elimination : StageType.single_elimination;

        var grandFinalType = seasonMatches.Any(x => x.BracketMatch.Bracket is Bracket.GrandFinalSuddenDeath) ? GrandFinalType.@double :
                             seasonMatches.Any(x => x.BracketMatch.Bracket is Bracket.GrandFinal) ? GrandFinalType.simple
                             : GrandFinalType.none;

        result.Stages = new List<Stage>()
        {
            new Stage()
            {
                Id = season.SeasonID,
                Tournament_id = season.SeasonID,
                Name = season.SeasonName,
                Type = stageType,
                Settings = new StageSettings()
                {
                    Size = exceptLosers.Count(x => x.BracketMatch.RoundNumber is 1) * 2, // Every first round slot, byes included
                    //SeedOrdering = [SeedOrdering.inner_outer],
                    SeedOrdering = [SeedOrdering.inner_outer, SeedOrdering.natural, SeedOrdering.reverse_half_shift, SeedOrdering.reverse],
                    BalanceByes = false,
                    MatchesChildCount = 0,
                    //GroupCount = 1,
                    RoundRobinMode = RoundRobinMode.simple,
                    ManualOrdering = [],
                    ConsolationFinal = false,
                    SkipFirstRound = false,
                    GrandFinal = grandFinalType,
                },
                Number = 1,
            }
        };

        result.Matches = exceptLosers.Select(sm => new Match()
        {
            Id = sm.SeasonMatchID,
            Stage_id = sm.SeasonID,
            Group_id = 1,
            Round_id = sm.BracketMatch.RoundNumber,
            Number = GetMatchNumberWithinRound(sm, exceptLosers), // This is total match number, need to get match number within round
            Child_count = 0,
            Status = GetMatchStatus(sm),
            // brackets-viewer shows a null opponent as a BYE
            // An undecided first-round slot shows its seed placeholder participant
            Opponent1 = OpponentOrBye(sm, SeasonMatchSide.Home,
                sm.HomeTeamID ?? (sm.BracketMatch.RoundNumber is 1 ? SeedPlaceholderID(sm.BracketMatch.HomeTeamSeedNumber) : null),
                sm.BracketMatch.HomeTeamSeedNumber),
            Opponent2 = OpponentOrBye(sm, SeasonMatchSide.Away,
                sm.AwayTeamID ?? (sm.BracketMatch.RoundNumber is 1 ? SeedPlaceholderID(sm.BracketMatch.AwayTeamSeedNumber) : null),
                sm.BracketMatch.AwayTeamSeedNumber),
        }).ToList();

        var finalRound = exceptLosers.MaxBy(x => x.BracketMatch.RoundNumber)?.BracketMatch.RoundNumber ?? 0;

        var mappedLosers = losers.Select(sm => new Match()
        {
            Id = sm.SeasonMatchID,
            Stage_id = sm.SeasonID,
            Group_id = 2,
            // I have loser bracket round restart at 1 but front end expects it to continue after last winner round
            Round_id = sm.BracketMatch.RoundNumber + finalRound,
            Number = GetMatchNumberWithinRound(sm, losers), // This is total match number, need to get match number within round
            Child_count = 0,
            Status = GetMatchStatus(sm),
            Opponent1 = OpponentOrBye(sm, SeasonMatchSide.Home, sm.HomeTeamID,
                GetWinnerMatchPosition(sm.BracketMatch.HomeTeamPreviousMatchBracketInfo?.SeasonMatchID, result.Matches)),
            Opponent2 = OpponentOrBye(sm, SeasonMatchSide.Away, sm.AwayTeamID,
                GetWinnerMatchPosition(sm.BracketMatch.AwayTeamPreviousMatchBracketInfo?.SeasonMatchID, result.Matches)),
        }).ToList();
        result.Matches.AddRange(mappedLosers);

        var grandFinal = seasonMatches.FirstOrDefault(x => x.BracketMatch.Bracket is Bracket.GrandFinal);
        if (grandFinal is not null)
        {
            var mappedGrandFinal = new Match()
            {
                Id = grandFinal.SeasonMatchID,
                Stage_id = grandFinal.SeasonID,
                Group_id = 3, // Always 3
                // I have loser bracket round restart at 1 but front end expects it to continue after last winner round
                Round_id = 0, // Round does not matter, just has to be the same as sudden death
                Number = 1, // Grand Final is always the first match in this round
                Child_count = 0,
                Status = GetMatchStatus(grandFinal),
                Opponent1 = Opponent(grandFinal, SeasonMatchSide.Home, grandFinal.HomeTeamID, null),
                // There is only 1 game in loser bracket final so this is always 1
                Opponent2 = Opponent(grandFinal, SeasonMatchSide.Away, grandFinal.AwayTeamID, 1),
            };
            result.Matches.Add(mappedGrandFinal);

            // Will only be shown if grand file home team id is not null and result does not equal win
            var grandFinalSuddenDeath = seasonMatches.FirstOrDefault(x => x.BracketMatch.Bracket is Bracket.GrandFinalSuddenDeath);
            if (grandFinalSuddenDeath is not null)
            {
                var mappedGrandFinalSuddenDeath = new Match()
                {
                    Id = grandFinalSuddenDeath.SeasonMatchID,
                    Stage_id = grandFinalSuddenDeath.SeasonID,
                    Group_id = 3, // Always 3
                    Round_id = 0, // Round does not matter, just has to be the same as grand final
                    Number = 2, // Sudden death is always the second match, since it is after the grand final
                    Child_count = 0,
                    Status = GetMatchStatus(grandFinalSuddenDeath),
                    Opponent1 = Opponent(grandFinalSuddenDeath, SeasonMatchSide.Home, grandFinalSuddenDeath.HomeTeamID, null),
                    Opponent2 = Opponent(grandFinalSuddenDeath, SeasonMatchSide.Away, grandFinalSuddenDeath.AwayTeamID, null),
                };
                result.Matches.Add(mappedGrandFinalSuddenDeath);
            }
        }

        //result.MatchGames - Does not seem to matter
        result.MatchGames = new();

        return result;
    }

    /// <summary>
    /// The id of the "Seed N" placeholder participant shown until a team is seeded there. Negative, so it
    /// can never be a real TeamID.
    /// </summary>
    public static int? SeedPlaceholderID(int? seed) => -seed;

    /// <summary>A side of a match as brackets-viewer shows it, or null for a BYE (brackets-viewer shows a null opponent as
    /// a BYE).</summary>
    private static ParticpantResult? OpponentOrBye(SeasonMatch sm, SeasonMatchSide side, int? id, int? position)
        => sm.IsBye(side) ? null : Opponent(sm, side, id, position);

    /// <summary>
    /// A side of a match as brackets-viewer shows it: its team (<paramref name="id"/>), its score, and its result, a win or
    /// a loss; a side that lost without playing (a forfeit, a BYE) is its forfeit.
    /// </summary>
    private static ParticpantResult Opponent(SeasonMatch sm, SeasonMatchSide side, int? id, int? position) => new()
    {
        Id = id,
        Position = position,
        Forfeit = sm.ResultOf(side) is SeasonMatchResult.Forfeit or SeasonMatchResult.Bye,
        Score = sm.ScoreOf(side),
        Result = MapResult(sm.ResultOf(side)),
    };

    /// <summary>
    /// The match status as brackets-manager's getMatchStatus derives it (brackets-viewer reads it to
    /// label matches and to clear origin hints from Completed on): Locked while neither team is known,
    /// Waiting with one, Completed once a result or forfeit is in, Running once a score is in, else Ready.
    /// A match with a BYE is Locked. Seed placeholders do not count as known teams.
    /// </summary>
    public static Status GetMatchStatus(SeasonMatch sm)
    {
        // A BYE on either side locks the match (brackets-manager's hasBye), whatever else it has.
        if (sm.HasBye)
            return Status.Locked;
        var home = sm.HomeTeamID is not null;
        var away = sm.AwayTeamID is not null;
        if (!home && !away)
            return Status.Locked;
        if (!home || !away)
            return Status.Waiting;
        if (sm.IsDecided)
            return Status.Completed;
        if (sm.HomeTeamScore is not null || sm.AwayTeamScore is not null)
            return Status.Running;
        return Status.Ready;
    }

    private static Result? MapResult(SeasonMatchResult? r)
    {
        return r switch
        {
            SeasonMatchResult.Won => Result.win,
            SeasonMatchResult.Loss => Result.loss,
            SeasonMatchResult.Forfeit => Result.loss,
            SeasonMatchResult.Bye => Result.loss,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(r), r, "Season match result of range"),
        };
    }

    private int GetMatchNumberWithinRound(SeasonMatch sm, List<SeasonMatch> seasonMatches)
    {
        if (sm.BracketMatch.RoundNumber is 1)
            return sm.BracketMatch.MatchNumber;

        var previous = seasonMatches
            .Select(x => x.BracketMatch)
            .Where(x => x.RoundNumber == sm.BracketMatch.RoundNumber - 1)
            .OrderByDescending(x => x.MatchNumber)
            .FirstOrDefault();

        if (previous is null)
            throw new Exception("Failed to find previous rounds last match");

        return sm.BracketMatch.MatchNumber - previous.MatchNumber;
    }

    private int? GetWinnerMatchPosition(int? seasonMatchID, List<Match> winnerMatches)
    {
        if (seasonMatchID is null)
            return null;

        var match = winnerMatches.FirstOrDefault(x => x.Id == seasonMatchID);

        if (match is null)
            return null;

        return match.Number;
    }

    public async Task SetSeeds(int seasonID, CustomSeedDto[]? customSeeds = null, CancellationToken ct = default)
    {
        await using var transaction = await _grifballContext.Database.BeginTransactionAsync(ct);

        // If custom seeds are not provided, use the current standings
        var standings = await _teamStandingsService.GetTeamStandings(seasonID, ct);

        // Create a dictionary mapping seed numbers to team IDs based on custom ordering, fallback to standings if custom seeds are null
        var seeds = customSeeds?.ToDictionary(cs => cs.Seed, cs => cs.TeamID) ?? standings.Select((dto, index) => (++index, dto.TeamID)).ToDictionary();

        var playoffMatches = await _grifballContext.SeasonMatches
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm.HomeTeamPreviousMatchBracketInfo)
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm.AwayTeamPreviousMatchBracketInfo)
            .Where(x => x.SeasonID == seasonID)
            .Where(x => x.BracketMatch != null)
            .Where(x => x.BracketMatch.HomeTeamSeedNumber != null || x.BracketMatch.AwayTeamSeedNumber != null)
            .ToListAsync(ct);

        if (playoffMatches.Any(x => x.HomeTeamID is not null || x.AwayTeamID is not null))
            throw new Exception("Teams have already been seeded");

        foreach (var match in playoffMatches)
        {
            var homeBye = match.IsBye(SeasonMatchSide.Home);
            var awayBye = match.IsBye(SeasonMatchSide.Away);

            var foundHomeTeam = seeds.TryGetValue(match.BracketMatch.HomeTeamSeedNumber ?? throw new Exception("Missing home team seed number"), out var homeTeamID);

            var foundAwayTeam = seeds.TryGetValue(match.BracketMatch.AwayTeamSeedNumber ?? throw new Exception("Missing away team seed number"), out var awayTeamID);

            if ((foundHomeTeam || homeBye) is false || (foundAwayTeam || awayBye) is false)
                throw new Exception("Missing home or away team");

            if (homeBye is false)
                match.PlaceTeam(SeasonMatchSide.Home, homeTeamID);
            if (awayBye is false)
                match.PlaceTeam(SeasonMatchSide.Away, awayTeamID);
        }

        // Teams facing a BYE go straight to their next match
        await AdvanceByesAsync(seasonID, ct);

        await _grifballContext.SaveChangesAsync(ct);
        // Reseeding starts the bracket over: a title it gave is taken back
        await _championshipService.AwardFromBracketAsync(seasonID, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Detemine the next match for the winner and the loser for a season match. Caller must include BracketMatch.InverseHomeTeamPreviousMatchBracketInfo.SeasonMatch and BracketMatch.InverseAwayTeamNextMatchBracketInfo.SeasonMatch.
    /// </summary>
    /// <param name="seasonMatch"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public NextMatchesDto DetermineNextMatches(SeasonMatch seasonMatch)
    {
        if (seasonMatch.BracketMatch.Bracket is Bracket.Winner)
        {
            var winnerNextHomeGame = seasonMatch.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Winner or Bracket.GrandFinal)?.SeasonMatch;
            var winnerNextAwayGame = seasonMatch.BracketMatch.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Winner or Bracket.GrandFinal)?.SeasonMatch;

            var loserNextHomeGame = seasonMatch.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Loser)?.SeasonMatch;
            var loserNextAwayGame = seasonMatch.BracketMatch.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Loser)?.SeasonMatch;

            return new NextMatchesDto()
            {
                Winner = winnerNextHomeGame is not null ? new NextGame()
                            { Game = winnerNextHomeGame, IsHomeTeam = true } :
                         winnerNextAwayGame is not null ? new NextGame()
                            { Game = winnerNextAwayGame, IsHomeTeam = false } :
                         null, // Should only ever happen if there is no grand final - single elimination
                Loser = loserNextHomeGame is not null ? new NextGame()
                         { Game = loserNextHomeGame, IsHomeTeam = true } :
                         loserNextAwayGame is not null ? new NextGame()
                         { Game = loserNextAwayGame, IsHomeTeam = false } :
                         null, // Should only happen in single elimination
            };
        }
        else if (seasonMatch.BracketMatch.Bracket is Bracket.Loser)
        {
            var winnerNextHomeGame = seasonMatch.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault()?.SeasonMatch;
            var winnerNextAwayGame = seasonMatch.BracketMatch.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault()?.SeasonMatch;

            return new NextMatchesDto()
            {
                Winner = winnerNextHomeGame is not null ? new NextGame()
                         { Game = winnerNextHomeGame, IsHomeTeam = true } :
                         winnerNextAwayGame is not null ? new NextGame()
                         { Game = winnerNextAwayGame, IsHomeTeam = false } :
                         throw new Exception("Failed to determine winners next match from loser bracket"),
                Loser = null,
            };
        }
        else if (seasonMatch.BracketMatch.Bracket is Bracket.GrandFinal)
        {
            var winnerNextHomeGame = seasonMatch.BracketMatch.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault()?.SeasonMatch;
            var loserNextAwayGame = seasonMatch.BracketMatch.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault()?.SeasonMatch;

            if (winnerNextHomeGame is null)
                throw new Exception("Failed to find winner next match after grand final");

            if (loserNextAwayGame is null)
                throw new Exception("Failed to find loser next match after grand final");

            return new NextMatchesDto()
            {
                Winner = new NextGame()
                        { Game = winnerNextHomeGame, IsHomeTeam = true },
                Loser = new NextGame()
                        { Game = loserNextAwayGame, IsHomeTeam = false },
                // Should be null if no sudden death?
            };
        }
        else if (seasonMatch.BracketMatch.Bracket is Bracket.GrandFinalSuddenDeath)
        {
            return new NextMatchesDto()
            {
                Winner = null,
                Loser = null,
            };
        }
        else
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Moves the bracket past its BYEs, as brackets-manager does: a team facing a BYE wins the match (so it
    /// counts in the standings) and goes straight to its next match, a match with a BYE has no loser so the loser bracket slot it feeds becomes a BYE, and a
    /// match with two BYEs sends a BYE on as its winner too. Loads the season's bracket into the context;
    /// the caller saves.
    /// </summary>
    public async Task AdvanceByesAsync(int seasonID, CancellationToken ct = default)
    {
        // Loading every bracket match of the season lets EF fix up the navigations DetermineNextMatches walks. With their
        // reschedule requests: a match a BYE decides closes its pending one
        var bracket = await _grifballContext.SeasonMatches
            .Include(x => x.BracketMatch)
            .Include(x => x.Reschedules)
            .Where(x => x.SeasonID == seasonID && x.BracketMatch != null)
            .ToListAsync(ct);

        bool changed;
        do
        {
            changed = false;
            foreach (var match in bracket)
            {
                var homeBye = match.IsBye(SeasonMatchSide.Home);
                var awayBye = match.IsBye(SeasonMatchSide.Away);
                if (homeBye is false && awayBye is false)
                    continue;

                // A grand final decided by a BYE ends the bracket: the sudden death only replays a grand final
                // the winner bracket champion lost to a team that was actually there
                var next = match.BracketMatch.Bracket is Bracket.GrandFinal
                    ? new NextMatchesDto() { Winner = null, Loser = null }
                    : DetermineNextMatches(match);

                changed |= MoveIntoNextMatch(next.Loser, teamID: null);

                if (homeBye && awayBye)
                    changed |= MoveIntoNextMatch(next.Winner, teamID: null);
                else if (match.TeamOf(homeBye ? SeasonMatchSide.Away : SeasonMatchSide.Home) is int teamID)
                {
                    changed |= match.AwardByeWin();
                    changed |= MoveIntoNextMatch(next.Winner, teamID);
                }
            }
        } while (changed);
    }

    /// <summary>
    /// Puts a BYE in every slot a match feeds, for its winner and its loser, when nobody won it (both teams
    /// forfeited). Run <see cref="AdvanceByesAsync"/> afterwards to move the teams now facing those BYEs on.
    /// Caller must include what <see cref="DetermineNextMatches"/> walks.
    /// </summary>
    public void SendByesToNextMatches(SeasonMatch seasonMatch)
    {
        var next = DetermineNextMatches(seasonMatch);
        MoveIntoNextMatch(next.Winner, teamID: null);
        MoveIntoNextMatch(next.Loser, teamID: null);
    }

    /// <summary>
    /// Takes back everything a decided match sent on through the bracket: the teams, or the BYEs after a double
    /// forfeit, it put in its next matches, and the wins and moves <see cref="AdvanceByesAsync"/> made from there.
    /// Throws <see cref="SeasonMatchActionNotAllowedException"/>, changing nothing, when the bracket refuses it
    /// (<see cref="UndoAdvancementRefusalAsync"/>). Loads the season's bracket into the context; the caller clears
    /// the match itself and saves.
    /// </summary>
    public async Task UndoAdvancementAsync(SeasonMatch seasonMatch, CancellationToken ct = default)
    {
        var slots = await SentOnAsync(seasonMatch, ct);

        // Check everything first so a rejected undo leaves the bracket as it was
        if (Refusal(slots) is string why)
            throw new SeasonMatchActionNotAllowedException(why);
        foreach (var slot in slots)
            TakeBack(slot);
    }

    /// <summary>
    /// Why the bracket refuses to take back what a decided match in it sent on (<see cref="UndoAdvancementAsync"/>), or
    /// null when it can: one of the matches it fed, or one a BYE then sent the team on to, has a result or games of its
    /// own (<see cref="SeasonMatch.BlocksFeederUndo"/>). The same walk as the undo, so the match page offers Undo Result
    /// only when it would be taken. Loads the season's bracket into the context.
    /// </summary>
    public async Task<string?> UndoAdvancementRefusalAsync(SeasonMatch seasonMatch, CancellationToken ct = default)
        => Refusal(await SentOnAsync(seasonMatch, ct));

    /// <summary>
    /// The slots a decided match filled in its next matches. Loading every bracket match of the season lets EF fix up
    /// the navigations <see cref="DetermineNextMatches"/> walks, and their games tell a match that is under way; the
    /// walk starts from the tracked copy of the match (the match page's is read without tracking).
    /// </summary>
    private async Task<List<NextGame>> SentOnAsync(SeasonMatch seasonMatch, CancellationToken ct)
    {
        await _grifballContext.SeasonMatches
            .Include(x => x.BracketMatch)
            .Include(x => x.SeriesGames)
            .Where(x => x.SeasonID == seasonMatch.SeasonID && x.BracketMatch != null)
            .LoadAsync(ct);
        var tracked = _grifballContext.SeasonMatches.Local.First(x => x.SeasonMatchID == seasonMatch.SeasonMatchID);

        // The home team winning the grand final ends the bracket, so it sent nobody on
        var next = tracked.BracketMatch!.Bracket is Bracket.GrandFinal && tracked.Winner is SeasonMatchSide.Home
            ? new NextMatchesDto() { Winner = null, Loser = null }
            : DetermineNextMatches(tracked);
        return new[] { next.Winner, next.Loser }.OfType<NextGame>().ToList();
    }

    /// <summary>The slots <see cref="AdvanceByesAsync"/> filled from the match this slot is in, because this slot was filled (a BYE facing it).</summary>
    private List<NextGame> FurtherSlots(NextGame slot)
    {
        var game = slot.Game;
        var other = SeasonMatch.Other(slot.Side);
        var (teamID, otherTeamID) = (game.TeamOf(slot.Side), game.TeamOf(other));
        var bye = game.IsBye(slot.Side);
        var otherBye = game.IsBye(other);

        // A grand final decided by a BYE sends nobody on (see AdvanceByesAsync)
        if (!(bye || otherBye) || game.BracketMatch!.Bracket is Bracket.GrandFinal)
            return [];
        var next = DetermineNextMatches(game);
        // Before this slot was filled the match sent nothing to its winner slot, and a BYE to its loser slot
        // only if the other side was a BYE already
        var sentWinner = bye ? otherBye || otherTeamID is not null : teamID is not null;
        var sentLoserBye = bye && otherBye is false;
        return new[] { sentWinner ? next.Winner : null, sentLoserBye ? next.Loser : null }.OfType<NextGame>().ToList();
    }

    /// <summary>
    /// Why these slots an earlier match filled can't be emptied, or null when they can: a match one is in, or one
    /// <see cref="AdvanceByesAsync"/> sent the team on to from there, has a result or games of its own.
    /// </summary>
    private string? Refusal(IEnumerable<NextGame> slots)
    {
        foreach (var slot in slots)
        {
            var game = slot.Game;
            // The only results AdvanceByesAsync gives out are a BYE's: the BYEs themselves and the win of a team facing
            // one. Games were played by the team this slot holds: taking the team out would leave them under another team
            switch (game.BlocksFeederUndo)
            {
                case FeederUndoBlock.OwnResult:
                    return $"Season match {game.SeasonMatchID} that this result fed into already has a result of its own. Undo that match first.";
                case FeederUndoBlock.Games:
                    return $"Season match {game.SeasonMatchID} that this result fed into already has games. Undo that match first.";
            }
            if (Refusal(FurtherSlots(slot)) is string why)
                return why;
        }
        return null;
    }

    /// <summary>
    /// Empties a slot an earlier match filled, after taking back what <see cref="AdvanceByesAsync"/> sent on from
    /// the match the slot is in because of it. Only after <see cref="Refusal"/> found nothing.
    /// </summary>
    private void TakeBack(NextGame slot)
    {
        foreach (var further in FurtherSlots(slot))
            TakeBack(further);

        // The other side keeps a BYE, but loses a win it only had because of this slot
        slot.Game.ClearSide(slot.Side);
    }

    /// <summary>
    /// Puts a team, or a BYE when <paramref name="teamID"/> is null, into the next match's slot.
    /// </summary>
    /// <returns>Whether the slot changed.</returns>
    private static bool MoveIntoNextMatch(NextGame? next, int? teamID)
    {
        if (next is null)
            return false;

        return teamID is int team ? next.Game.PlaceTeam(next.Side, team) : next.Game.PlaceBye(next.Side);
    }
}

public record NextMatchesDto
{
    public required NextGame? Winner { get; set; }
    public required NextGame? Loser { get; set; }
}

public record NextGame
{
    //public NextGame(SeasonMatch game, bool isHomeTeam)
    //{
    //    Game = game;
    //    IsHomeTeam = isHomeTeam;
    //}
    public required SeasonMatch Game { get; set; }
    public required bool IsHomeTeam { get; set; }
    /// <summary>The side of <see cref="Game"/> the team goes to.</summary>
    public SeasonMatchSide Side => IsHomeTeam ? SeasonMatchSide.Home : SeasonMatchSide.Away;
}
