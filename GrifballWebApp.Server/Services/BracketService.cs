using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Services;

public class BracketService
{
    private readonly GrifballContext _grifballContext;
    public BracketService(GrifballContext grifballContext)
    {
        _grifballContext = grifballContext;
    }

    public async Task CreateBracket(int participantsCount, int seasonID, bool doubleElimination, CancellationToken ct = default)
    {
        using var t = await _grifballContext.Database.BeginTransactionAsync(ct);
        var deletedCount = await _grifballContext.SeasonMatches
            .Where(x => x.SeasonID == seasonID && x.BracketMatch != null)
            .ExecuteDeleteAsync(ct);

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
        foreach (var matchup in matchUps)
        {
            var match = new SeasonMatch()
            {
                SeasonID = seasonID,
                BracketMatch = new MatchBracketInfo()
                {
                    MatchNumber = matchNumber,
                    RoundNumber = 1,
                    HomeTeamSeedNumber = matchup.Home,
                    HomeTeamPreviousMatchBracketInfo = null,
                    AwayTeamSeedNumber = matchup.Away,
                    AwayTeamPreviousMatchBracketInfo = null,
                    Bracket = Bracket.Winner,
                }
            };
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

                var match = new SeasonMatch()
                {
                    SeasonID = seasonID,
                    BracketMatch = new MatchBracketInfo()
                    {
                        MatchNumber = matchNumber,
                        RoundNumber = round,
                        HomeTeamSeedNumber = null,
                        HomeTeamPreviousMatchBracketInfo = dependentMatches[0].BracketMatch,
                        AwayTeamSeedNumber = null,
                        AwayTeamPreviousMatchBracketInfo = dependentMatches[1].BracketMatch,
                        Bracket = Bracket.Winner,
                    }
                };
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
            var grandFinal = new SeasonMatch()
            {
                SeasonID = seasonID,
                BracketMatch = new MatchBracketInfo()
                {
                    MatchNumber = matchNumber,
                    RoundNumber = numberOfRounds + 1,
                    HomeTeamSeedNumber = null,
                    HomeTeamPreviousMatchBracketInfo = lastWinnerBracketMatch.BracketMatch,
                    AwayTeamSeedNumber = null,
                    AwayTeamPreviousMatchBracketInfo = null, // TBD the winner of the loser bracket.
                    Bracket = Bracket.GrandFinal,
                }
            };

            matches.Add(grandFinal);
            matchNumber++;

            // Losers force a second mathc
            var suddenDeath = new SeasonMatch()
            {
                SeasonID = seasonID,
                BracketMatch = new MatchBracketInfo()
                {
                    MatchNumber = matchNumber,
                    RoundNumber = numberOfRounds + 2,
                    HomeTeamSeedNumber = null,
                    HomeTeamPreviousMatchBracketInfo = grandFinal.BracketMatch, // This
                    AwayTeamSeedNumber = null,
                    AwayTeamPreviousMatchBracketInfo = grandFinal.BracketMatch, // is weird... is this how I want to do this?
                    Bracket = Bracket.GrandFinalSuddenDeath,
                }
            };

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

                        var loserMatch = new SeasonMatch()
                        {
                            SeasonID = seasonID,
                            BracketMatch = new MatchBracketInfo()
                            {
                                MatchNumber = matchNumber,
                                RoundNumber = minorRound,
                                HomeTeamSeedNumber = null,
                                HomeTeamPreviousMatchBracketInfo = dependentMatches[0].BracketMatch,
                                AwayTeamSeedNumber = null,
                                AwayTeamPreviousMatchBracketInfo = dependentMatches[1].BracketMatch,
                                Bracket = Bracket.Loser,
                            },
                        };
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

                        var loserMatch = new SeasonMatch()
                        {
                            SeasonID = seasonID,
                            BracketMatch = new MatchBracketInfo()
                            {
                                MatchNumber = matchNumber,
                                RoundNumber = minorRound,
                                HomeTeamSeedNumber = null,
                                HomeTeamPreviousMatchBracketInfo = dependentMatches[0].BracketMatch,
                                AwayTeamSeedNumber = null,
                                AwayTeamPreviousMatchBracketInfo = dependentMatches[1].BracketMatch,
                                Bracket = Bracket.Loser,
                            },
                        };
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

                    var loserMatch = new SeasonMatch()
                    {
                        SeasonID = seasonID,
                        BracketMatch = new MatchBracketInfo()
                        {
                            MatchNumber = matchNumber,
                            RoundNumber = majorRound,
                            HomeTeamSeedNumber = null,
                            HomeTeamPreviousMatchBracketInfo = previousWinnerMatch.BracketMatch,
                            AwayTeamSeedNumber = null,
                            AwayTeamPreviousMatchBracketInfo = previousLoserMatch.BracketMatch,
                            Bracket = Bracket.Loser,
                        },
                    };
                    matchNumber++;
                    loserMatches.Add(loserMatch);
                }

                loserMatchCountToCreate /= 2;
            }

            var finalLoserMatch = loserMatches.LastOrDefault() ?? throw new Exception("Failed to get last loser match");

            grandFinal.BracketMatch.AwayTeamPreviousMatchBracketInfo = finalLoserMatch.BracketMatch;

            matches.AddRange(loserMatches);
        }

        await _grifballContext.AddRangeAsync(matches, ct);
        await _grifballContext.SaveChangesAsync(ct);

        await t.CommitAsync(ct);
    }

    private int GetNumberOfRounds(int participantsCount)
    {
        return (int)Math.Ceiling(Math.Log(participantsCount) / Math.Log(2));
    }

    private IEnumerable<MatchUp> GetSeedMatchUps(int participantsCount)
    {
        var rounds = GetNumberOfRounds(participantsCount);
        var bracketSize = (int)Math.Pow(2, rounds);
        var requiredByes = bracketSize - participantsCount;

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
                var home = ChangeIntoBye(matches[i].Home, participantsCount);
                var away = ChangeIntoBye(sum - matches[i].Home, participantsCount);
                roundMatches.Add(new MatchUp(home, away));
                home = ChangeIntoBye(sum - matches[i].Away, participantsCount);
                away = ChangeIntoBye(matches[i].Away, participantsCount);
                roundMatches.Add(new MatchUp(home, away));
            }
            matches = roundMatches;

        }

        return matches;
    }

    private int? ChangeIntoBye(int? seed, int participantsCount)
    {
        return seed <= participantsCount ? seed : null;
    }

    private struct MatchUp
    {
        public MatchUp(int? home, int? away)
        {
            Home = home;
            Away = away;
        }
        public int? Home { get; private set; }
        public int? Away { get; private set; }
    }

    public async Task<BracketDto> GetBracketsAsync(int seasonID, CancellationToken ct = default)
    {
        var bracket = await _grifballContext.SeasonMatches
            .Where(x => x.SeasonID == seasonID && x.BracketMatch != null)
            .Select(x => x.BracketMatch)
            .ToListAsync(ct);

        var winners = bracket.Where(x => x.Bracket is Bracket.Winner)
            .OrderBy(x => x.RoundNumber).ThenBy(x => x.MatchNumber)
            .GroupBy(x => x.RoundNumber);

        var winnerDtos = winners.Select(x => new RoundDto()
        {
            RoundNumber = x.Key,
            Matches = x.Select(x => new MatchDto()
            {
                MatchNumber = $"W{x.MatchNumber}",
                HomeTeam = x.HomeTeamSeedNumber is not null ? $"Seed {x.HomeTeamSeedNumber}" :
                    $"Winner of W{bracket.FirstOrDefault(y => y.MatchBracketInfoID == x.HomeTeamPreviousMatchBracketInfoID)?.MatchNumber}",
                AwayTeam = x.AwayTeamSeedNumber is not null ? $"Seed {x.AwayTeamSeedNumber}" :
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
                var homeTeamText = homeTeamFrom?.Bracket is Bracket.Winner ? $"Loser of W{homeTeamFrom.MatchNumber}" : $"Winner of L{homeTeamFrom?.MatchNumber}";

                var awayTeamFrom = bracket.FirstOrDefault(y => y.MatchBracketInfoID == x.AwayTeamPreviousMatchBracketInfoID);
                var awayTeamText = awayTeamFrom?.Bracket is Bracket.Winner ? $"Loser of W{awayTeamFrom.MatchNumber}" : $"Winner of L{awayTeamFrom?.MatchNumber}";

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
}
