using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Brackets;

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
    private readonly IApplicationDbContext _grifballContext;
    private readonly TeamStandingsService _teamStandingsService;
    private readonly GrifballWebApp.Application.Championships.ChampionshipService _championshipService;
    public BracketService(IApplicationDbContext grifballContext, TeamStandingsService teamStandingsService, GrifballWebApp.Application.Championships.ChampionshipService championshipService)
    {
        _grifballContext = grifballContext;
        _teamStandingsService = teamStandingsService;
        _championshipService = championshipService;
    }

    public async Task CreateBracket(int participantsCount, int seasonID, bool doubleElimination, int bestOf, CancellationToken ct = default)
    {
        using var t = await _grifballContext.Database.BeginTransactionAsync(ct);
        var bracket = _grifballContext.SeasonMatches.Where(x => x.SeasonID == seasonID && x.BracketMatch != null);
        await _grifballContext.RecordedGames.DeleteManualForAsync(bracket, ct);
        var deletedCount = await bracket.ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            // TODO add guard against user deleting bracket unless they approve
        }

        await _grifballContext.SaveChangesAsync(ct);

        var numberOfRounds = GetNumberOfRounds(participantsCount);
        var matches = WinnerBracket(seasonID, participantsCount, numberOfRounds, bestOf);

        if (doubleElimination)
            AddDoubleElimination(matches, seasonID, numberOfRounds, bestOf);

        _grifballContext.AddRange(matches);
        await _grifballContext.SaveChangesAsync(ct);

        // In double elimination the loser bracket slots fed by a BYE match are BYEs too
        await AdvanceByesAsync(seasonID, ct);
        await _grifballContext.SaveChangesAsync(ct);

        // A bracket made again decides nobody yet: a title the old one gave is taken back
        await _championshipService.AwardFromBracketAsync(seasonID, ct);
        await t.CommitAsync(ct);
    }

    /// <summary>
    /// The winner bracket, its matches numbered from 1. The first round is set up with the seed numbers (its previous
    /// matches are null: these are the very first games in the bracket); a seed past the participant count is a BYE: the
    /// slot keeps its seed number and is marked as a BYE. Each later round is set up from the victors of the one before.
    /// </summary>
    private static List<SeasonMatch> WinnerBracket(int seasonID, int participantsCount, int numberOfRounds, int bestOf)
    {
        var matchNumber = 1;
        var matches = new List<SeasonMatch>();
        foreach (var matchup in GetSeedMatchUps(participantsCount))
        {
            var match = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Seeded(Bracket.Winner, 1, matchNumber, matchup.Home, matchup.Away), bestOf, homeBye: matchup.Home > participantsCount, awayBye: matchup.Away > participantsCount);
            matches.Add(match);
            matchNumber++;
        }

        var amtToSkip = 0;
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
        return matches;
    }

    /// <summary>
    /// Double elimination, after the winner bracket in <paramref name="matches"/> (numbered on from it): the grand final,
    /// the sudden death the losers may force, and the loser bracket, whose winner meets the winner bracket's in the grand final.
    /// </summary>
    private static void AddDoubleElimination(List<SeasonMatch> matches, int seasonID, int numberOfRounds, int bestOf)
    {
        var matchNumber = matches.Count + 1;
        var lastWinnerBracketMatch = matches.LastOrDefault()
            ?? throw new Exception("Failed to find the last winner bracket match");
        var lastWinnerSlot = lastWinnerBracketMatch.BracketMatch
            ?? throw new Exception("Last winner bracket match is missing bracket info");

        // Add grand final
        var grandFinal = SeasonMatch.InBracket(seasonID, MatchBracketInfo.GrandFinal(numberOfRounds + 1, matchNumber, lastWinnerSlot), bestOf);

        matches.Add(grandFinal);
        matchNumber++;

        // Losers may force a second match
        var suddenDeath = SeasonMatch.InBracket(seasonID, MatchBracketInfo.SuddenDeath(numberOfRounds + 2, matchNumber, grandFinal.BracketMatch), bestOf);

        matches.Add(suddenDeath);

        var loserMatches = LoserBracket(matches, seasonID, numberOfRounds, bestOf);

        var finalLoserMatch = loserMatches.LastOrDefault() ?? throw new Exception("Failed to get last loser match");

        grandFinal.BracketMatch!.AwaitLosersFinal(finalLoserMatch.BracketMatch);

        matches.AddRange(loserMatches);
    }

    /// <summary>
    /// The loser bracket, its matches numbered from 1 again, made two rounds at a time: a minor round (odd) has only those
    /// that survived the previous loser round (in its first, those that just dropped from winners R1), and a major round
    /// (even) is half those and half those just knocked from the winner bracket. Example: round 1 has only those that just
    /// dropped from winners R1, round 2 is half and half winner R2, round 3 only losers, and the last two steps repeat until
    /// one match is left to decide the loser bracket's winner. Loser bracket round 1 has as many matches as winner bracket
    /// round 2 (<paramref name="matches"/>: the winner bracket, the grand final and its sudden death).
    /// </summary>
    private static List<SeasonMatch> LoserBracket(List<SeasonMatch> matches, int seasonID, int numberOfRounds, int bestOf)
    {
        var matchNumber = 1;
        var loserMatchCountToCreate = matches.Count(x => x.BracketMatch!.RoundNumber == 2);

        // We need to create X number of loser rounds based on how many winner rounds there are
        var loserRoundCount = (numberOfRounds - 1) * 2;
        var loserMatches = new List<SeasonMatch>();

        for (int loserRoundNumber = 1; loserRoundNumber <= loserRoundCount; loserRoundNumber += 2)
        {
            var minorRound = loserRoundNumber;
            var majorRound = loserRoundNumber + 1;

            // From winner round 1's teams, else from the previous loser round's
            var fedBy = minorRound is 1
                ? matches.Where(m => m.BracketMatch!.RoundNumber == 1 && m.BracketMatch.Bracket is Bracket.Winner).ToList()
                : loserMatches.Where(m => m.BracketMatch!.RoundNumber == minorRound - 1 && m.BracketMatch.Bracket is Bracket.Loser).ToList();
            matchNumber = AddMinorRound(loserMatches, fedBy, seasonID, minorRound, matchNumber, loserMatchCountToCreate, bestOf);

            // Half from the previous loser round, half from the corresponding winner round: L2 fill from W2, L4 from W3,
            // L6 from W4, L8 from W5
            var corrspondingWinnerRound = majorRound / 2 + 1;
            var winners = matches.Where(m => m.BracketMatch!.RoundNumber == corrspondingWinnerRound && m.BracketMatch.Bracket is Bracket.Winner).ToList();
            var losers = loserMatches.Where(m => m.BracketMatch!.RoundNumber == minorRound && m.BracketMatch.Bracket is Bracket.Loser).ToList();
            matchNumber = AddMajorRound(loserMatches, winners, losers, seasonID, majorRound, matchNumber, loserMatchCountToCreate, bestOf);

            loserMatchCountToCreate /= 2;
        }
        return loserMatches;
    }

    /// <summary>A minor loser round: each match from the next two of <paramref name="fedBy"/>.</summary>
    /// <returns>The next match number.</returns>
    private static int AddMinorRound(List<SeasonMatch> loserMatches, List<SeasonMatch> fedBy, int seasonID, int round, int matchNumber, int count, int bestOf)
    {
        var skipCount = 0;
        for (var i = 1; i <= count; i++)
        {
            var dependentMatches = fedBy.Skip(skipCount).Take(2).ToList();
            skipCount += 2;
            if (dependentMatches.Count != 2)
                throw new Exception("Failed to get dependent matches while building loser bracket");

            var loserMatch = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Following(Bracket.Loser, round, matchNumber, dependentMatches[0].BracketMatch, dependentMatches[1].BracketMatch), bestOf);
            matchNumber++;
            loserMatches.Add(loserMatch);
        }
        return matchNumber;
    }

    /// <summary>A major loser round: each match from the next of <paramref name="winners"/> and the next of <paramref name="losers"/>.</summary>
    /// <returns>The next match number.</returns>
    private static int AddMajorRound(List<SeasonMatch> loserMatches, List<SeasonMatch> winners, List<SeasonMatch> losers, int seasonID, int round, int matchNumber, int count, int bestOf)
    {
        for (var i = 1; i <= count; i++)
        {
            var previousWinnerMatch = winners.Skip(i - 1).FirstOrDefault()
                ?? throw new Exception("Failed to get winner match while building loser bracket");
            var previousLoserMatch = losers.Skip(i - 1).FirstOrDefault()
                ?? throw new Exception("Failed to get loser match while building loser bracket");

            var loserMatch = SeasonMatch.InBracket(seasonID, MatchBracketInfo.Following(Bracket.Loser, round, matchNumber, previousWinnerMatch.BracketMatch, previousLoserMatch.BracketMatch), bestOf);
            matchNumber++;
            loserMatches.Add(loserMatch);
        }
        return matchNumber;
    }

    private static int GetNumberOfRounds(int participantsCount)
    {
        return (int)Math.Ceiling(Math.Log(participantsCount) / Math.Log(2));
    }

    private static IEnumerable<MatchUp> GetSeedMatchUps(int participantsCount)
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

            for (var i = 0; i < matches.Count; i++)
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
                HomeTeam = WinnerBracketSide(x, SeasonMatchSide.Home, x.HomeTeamSeedNumber, x.HomeTeamPreviousMatchBracketInfoID, bracket),
                AwayTeam = WinnerBracketSide(x, SeasonMatchSide.Away, x.AwayTeamSeedNumber, x.AwayTeamPreviousMatchBracketInfoID, bracket),
            }).ToArray(),
        }).ToArray();

        var losers = bracket.Where(x => x.Bracket is Bracket.Loser)
            .OrderBy(x => x.RoundNumber).ThenBy(x => x.MatchNumber)
            .GroupBy(x => x.RoundNumber);

        var loserDtos = losers.Select(x => new RoundDto()
        {
            RoundNumber = x.Key,
            Matches = x.Select(x => new MatchDto()
            {
                MatchNumber = $"L{x.MatchNumber}",
                HomeTeam = LoserBracketSide(x, SeasonMatchSide.Home, x.HomeTeamPreviousMatchBracketInfoID, bracket),
                AwayTeam = LoserBracketSide(x, SeasonMatchSide.Away, x.AwayTeamPreviousMatchBracketInfoID, bracket),
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

    /// <summary>A winner bracket slot's side: a BYE, its seed, else the winner of the match before it.</summary>
    private static string WinnerBracketSide(MatchBracketInfo slot, SeasonMatchSide side, int? seed, int? previousID, List<MatchBracketInfo> bracket)
    {
        if (slot.SeasonMatch.IsBye(side))
            return "BYE";
        if (seed is not null)
            return $"Seed {seed}";
        return $"Winner of W{bracket.FirstOrDefault(y => y.MatchBracketInfoID == previousID)?.MatchNumber}";
    }

    /// <summary>A loser bracket slot's side: a BYE, the loser of a winner bracket match, else the winner of a loser bracket one.</summary>
    private static string LoserBracketSide(MatchBracketInfo slot, SeasonMatchSide side, int? previousID, List<MatchBracketInfo> bracket)
    {
        var from = bracket.FirstOrDefault(y => y.MatchBracketInfoID == previousID);
        if (slot.SeasonMatch.IsBye(side))
            return "BYE";
        return from?.Bracket is Bracket.Winner ? $"Loser of W{from.MatchNumber}" : $"Winner of L{from?.MatchNumber}";
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
                .ThenInclude(bm => bm!.HomeTeamPreviousMatchBracketInfo)
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm!.AwayTeamPreviousMatchBracketInfo)
            .Include(sm => sm.HomeTeam)
            .Include(sm => sm.AwayTeam)
            .Include(sm => sm.SeriesGames) // Not sure we even need this
                .ThenInclude(l => l.Match)
            .Where(sm => sm.SeasonID == seasonID && sm.BracketMatch != null)
            .OrderBy(sm => sm.BracketMatch!.MatchNumber)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        var exceptLosers = seasonMatches.Where(x => x.BracketMatch!.Bracket is Bracket.Winner).ToList();
        var losers = seasonMatches.Where(x => x.BracketMatch!.Bracket is Bracket.Loser).ToList();

        var result = new ViewerDataDto();

        // All teams, or if not yet decided then seeds
        result.Participants = new List<Participant>();

        foreach (var seasonMatch in exceptLosers.Where(x => x.BracketMatch!.RoundNumber is 1 && x.BracketMatch.Bracket is Bracket.Winner))
        {
            if (seasonMatch.HomeTeam is null && seasonMatch.HomeTeamID is not null)
                throw new Exception("Home team not included");
            if (seasonMatch.AwayTeam is null && seasonMatch.AwayTeamID is not null)
                throw new Exception("Away team not included");

            AddParticipant(result.Participants, seasonMatch, SeasonMatchSide.Home, seasonMatch.HomeTeam, seasonMatch.BracketMatch!.HomeTeamSeedNumber, "Missing home seed", seasonID);
            AddParticipant(result.Participants, seasonMatch, SeasonMatchSide.Away, seasonMatch.AwayTeam, seasonMatch.BracketMatch.AwayTeamSeedNumber, "Missing away seed", seasonID);
        }

        var stageType = losers.Count > 0 ? StageType.double_elimination : StageType.single_elimination;

        var grandFinalType = seasonMatches.Any(x => x.BracketMatch!.Bracket is Bracket.GrandFinalSuddenDeath) ? GrandFinalType.@double :
                             seasonMatches.Any(x => x.BracketMatch!.Bracket is Bracket.GrandFinal) ? GrandFinalType.simple
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
                    Size = exceptLosers.Count(x => x.BracketMatch!.RoundNumber is 1) * 2, // Every first round slot, byes included
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

        result.Matches = exceptLosers.Select(sm => WinnerBracketMatch(sm, exceptLosers)).ToList();

        var finalRound = exceptLosers.MaxBy(x => x.BracketMatch!.RoundNumber)?.BracketMatch!.RoundNumber ?? 0;

        var mappedLosers = losers.Select(sm => LoserBracketMatch(sm, losers, finalRound, result.Matches)).ToList();
        result.Matches.AddRange(mappedLosers);

        result.Matches.AddRange(GrandFinalMatches(seasonMatches));

        //result.MatchGames - Does not seem to matter
        result.MatchGames = new();

        return result;
    }

    /// <summary>A first round side's participant: its team, else its seed's placeholder. A BYE is not a participant.</summary>
    private static void AddParticipant(List<Participant> participants, SeasonMatch seasonMatch, SeasonMatchSide side, Team? team, int? seed, string missingSeed, int seasonID)
    {
        if (seasonMatch.IsBye(side))
            return;
        if (team is not null)
        {
            participants.Add(new Participant()
            {
                Id = team.TeamID,
                Tournament_id = team.SeasonID,
                Name = team.TeamName,
            });
        }
        else if (seed is not null)
        {
            participants.Add(new Participant()
            {
                Id = SeedPlaceholderID(seed) ?? throw new Exception(missingSeed),
                Tournament_id = seasonID,
                Name = $"Seed {seed}",
            });
        }
        else
        {
            throw new Exception($"Season Match {seasonMatch.SeasonMatchID} is not valid for first round of bracket");
        }
    }

    /// <summary>
    /// A winner bracket match as brackets-viewer shows it. A null opponent is a BYE to it; an undecided first-round slot
    /// shows its seed placeholder participant.
    /// </summary>
    private static Match WinnerBracketMatch(SeasonMatch sm, List<SeasonMatch> exceptLosers)
    {
        var slot = sm.BracketMatch!;
        var firstRound = slot.RoundNumber is 1;
        return new Match()
        {
            Id = sm.SeasonMatchID,
            Stage_id = sm.SeasonID,
            Group_id = 1,
            Round_id = slot.RoundNumber,
            Number = GetMatchNumberWithinRound(sm, exceptLosers), // This is total match number, need to get match number within round
            Child_count = 0,
            Status = GetMatchStatus(sm),
            Opponent1 = OpponentOrBye(sm, SeasonMatchSide.Home, sm.HomeTeamID ?? (firstRound ? SeedPlaceholderID(slot.HomeTeamSeedNumber) : null), slot.HomeTeamSeedNumber),
            Opponent2 = OpponentOrBye(sm, SeasonMatchSide.Away, sm.AwayTeamID ?? (firstRound ? SeedPlaceholderID(slot.AwayTeamSeedNumber) : null), slot.AwayTeamSeedNumber),
        };
    }

    /// <summary>
    /// A loser bracket match as brackets-viewer shows it. The loser bracket's rounds restart at 1 here, but the viewer
    /// expects them to continue after the last winner round (<paramref name="finalRound"/>).
    /// </summary>
    private static Match LoserBracketMatch(SeasonMatch sm, List<SeasonMatch> losers, int finalRound, List<Match> winnerMatches) => new()
    {
        Id = sm.SeasonMatchID,
        Stage_id = sm.SeasonID,
        Group_id = 2,
        Round_id = sm.BracketMatch!.RoundNumber + finalRound,
        Number = GetMatchNumberWithinRound(sm, losers), // This is total match number, need to get match number within round
        Child_count = 0,
        Status = GetMatchStatus(sm),
        Opponent1 = OpponentOrBye(sm, SeasonMatchSide.Home, sm.HomeTeamID,
            GetWinnerMatchPosition(sm.BracketMatch.HomeTeamPreviousMatchBracketInfo?.SeasonMatchID, winnerMatches)),
        Opponent2 = OpponentOrBye(sm, SeasonMatchSide.Away, sm.AwayTeamID,
            GetWinnerMatchPosition(sm.BracketMatch.AwayTeamPreviousMatchBracketInfo?.SeasonMatchID, winnerMatches)),
    };

    /// <summary>The grand final, and its sudden death (only with a grand final), as brackets-viewer shows them.</summary>
    private static List<Match> GrandFinalMatches(List<SeasonMatch> seasonMatches)
    {
        var grandFinal = seasonMatches.FirstOrDefault(x => x.BracketMatch!.Bracket is Bracket.GrandFinal);
        if (grandFinal is null)
            return [];
        var matches = new List<Match>
        {
            new Match()
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
            },
        };

        // Will only be shown if grand file home team id is not null and result does not equal win
        var grandFinalSuddenDeath = seasonMatches.FirstOrDefault(x => x.BracketMatch!.Bracket is Bracket.GrandFinalSuddenDeath);
        if (grandFinalSuddenDeath is not null)
        {
            matches.Add(new Match()
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
            });
        }
        return matches;
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

    private static int GetMatchNumberWithinRound(SeasonMatch sm, List<SeasonMatch> seasonMatches)
    {
        if (sm.BracketMatch!.RoundNumber is 1)
            return sm.BracketMatch.MatchNumber;

        var previous = seasonMatches
            .Select(x => x.BracketMatch!)
            .Where(x => x.RoundNumber == sm.BracketMatch.RoundNumber - 1)
            .OrderByDescending(x => x.MatchNumber)
            .FirstOrDefault();

        if (previous is null)
            throw new Exception("Failed to find previous rounds last match");

        return sm.BracketMatch.MatchNumber - previous.MatchNumber;
    }

    private static int? GetWinnerMatchPosition(int? seasonMatchID, List<Match> winnerMatches)
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
                .ThenInclude(bm => bm!.HomeTeamPreviousMatchBracketInfo)
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm!.AwayTeamPreviousMatchBracketInfo)
            .Where(x => x.SeasonID == seasonID)
            .Where(x => x.BracketMatch != null)
            .Where(x => x.BracketMatch!.HomeTeamSeedNumber != null || x.BracketMatch.AwayTeamSeedNumber != null)
            .ToListAsync(ct);

        if (playoffMatches.Any(x => x.HomeTeamID is not null || x.AwayTeamID is not null))
            throw new Exception("Teams have already been seeded");

        foreach (var match in playoffMatches)
        {
            var homeBye = match.IsBye(SeasonMatchSide.Home);
            var awayBye = match.IsBye(SeasonMatchSide.Away);

            var foundHomeTeam = seeds.TryGetValue(match.BracketMatch!.HomeTeamSeedNumber ?? throw new Exception("Missing home team seed number"), out var homeTeamID);

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
        var slot = seasonMatch.BracketMatch!;
        return slot.Bracket switch
        {
            Bracket.Winner => FromWinnerBracket(slot),
            Bracket.Loser => FromLoserBracket(slot),
            Bracket.GrandFinal => FromGrandFinal(slot),
            Bracket.GrandFinalSuddenDeath => new NextMatchesDto()
            {
                Winner = null,
                Loser = null,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(seasonMatch), slot.Bracket, "Not a bracket a match can be in"),
        };
    }

    /// <summary>The first of a match's next games: as the home team, else as the away team; null when it has none.</summary>
    private static NextGame? NextGameOf(SeasonMatch? home, SeasonMatch? away)
        => home is not null ? new NextGame() { Game = home, IsHomeTeam = true }
            : away is not null ? new NextGame() { Game = away, IsHomeTeam = false }
            : null;

    private static NextMatchesDto FromWinnerBracket(MatchBracketInfo slot)
    {
        var winnerNextHomeGame = slot.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Winner or Bracket.GrandFinal)?.SeasonMatch;
        var winnerNextAwayGame = slot.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Winner or Bracket.GrandFinal)?.SeasonMatch;

        var loserNextHomeGame = slot.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Loser)?.SeasonMatch;
        var loserNextAwayGame = slot.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault(x => x.Bracket is Bracket.Loser)?.SeasonMatch;

        return new NextMatchesDto()
        {
            Winner = NextGameOf(winnerNextHomeGame, winnerNextAwayGame), // None only without a grand final: single elimination
            Loser = NextGameOf(loserNextHomeGame, loserNextAwayGame), // None only in single elimination
        };
    }

    private static NextMatchesDto FromLoserBracket(MatchBracketInfo slot)
    {
        var winnerNextHomeGame = slot.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault()?.SeasonMatch;
        var winnerNextAwayGame = slot.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault()?.SeasonMatch;

        return new NextMatchesDto()
        {
            Winner = NextGameOf(winnerNextHomeGame, winnerNextAwayGame)
                ?? throw new Exception("Failed to determine winners next match from loser bracket"),
            Loser = null,
        };
    }

    private static NextMatchesDto FromGrandFinal(MatchBracketInfo slot)
    {
        var winnerNextHomeGame = slot.InverseHomeTeamPreviousMatchBracketInfo.FirstOrDefault()?.SeasonMatch;
        var loserNextAwayGame = slot.InverseAwayTeamNextMatchBracketInfo.FirstOrDefault()?.SeasonMatch;

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
                changed |= AdvanceBye(match);
        } while (changed);
    }

    /// <summary>One pass of <see cref="AdvanceByesAsync"/> over a match with a BYE (none: nothing).</summary>
    /// <returns>Whether anything changed.</returns>
    private bool AdvanceBye(SeasonMatch match)
    {
        var homeBye = match.IsBye(SeasonMatchSide.Home);
        var awayBye = match.IsBye(SeasonMatchSide.Away);
        if (homeBye is false && awayBye is false)
            return false;

        // A grand final decided by a BYE ends the bracket: the sudden death only replays a grand final
        // the winner bracket champion lost to a team that was actually there
        var next = match.BracketMatch!.Bracket is Bracket.GrandFinal
            ? new NextMatchesDto() { Winner = null, Loser = null }
            : DetermineNextMatches(match);

        var changed = MoveIntoNextMatch(next.Loser, teamID: null);

        if (homeBye && awayBye)
            changed |= MoveIntoNextMatch(next.Winner, teamID: null);
        else if (match.TeamOf(homeBye ? SeasonMatchSide.Away : SeasonMatchSide.Home) is int teamID)
        {
            changed |= match.AwardByeWin();
            changed |= MoveIntoNextMatch(next.Winner, teamID);
        }
        return changed;
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
