using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Brackets;

public class BracketService
{
    private readonly GrifballContext _grifballContext;
    private readonly TeamStandingsService _teamStandingsService;
    public BracketService(GrifballContext grifballContext, TeamStandingsService teamStandingsService)
    {
        _grifballContext = grifballContext;
        _teamStandingsService = teamStandingsService;
    }

    public async Task CreateBracket(int participantsCount, int seasonID, bool doubleElimination, int bestOf, CancellationToken ct = default)
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
                },
                BestOf = bestOf,
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
                    },
                    BestOf = bestOf,
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
                },
                BestOf = bestOf,
            };

            matches.Add(grandFinal);
            matchNumber++;

            // Losers may force a second match
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
                },
                BestOf = bestOf,
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
                            BestOf = bestOf,
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
                            BestOf = bestOf,
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
                        BestOf = bestOf,
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

    public async Task<ViewerDataDto> GetViewerDataAsync(int seasonID, CancellationToken ct = default)
    {
        var season = await _grifballContext.Seasons
            .Where(s => s.SeasonID == seasonID)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (season is null)
        {
            throw new Exception("Season does not exist");
        }

        var seasonMatches = await _grifballContext.SeasonMatches
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm.HomeTeamPreviousMatchBracketInfo)
            .Include(sm => sm.BracketMatch)
                .ThenInclude(bm => bm.AwayTeamPreviousMatchBracketInfo)
            .Include(sm => sm.HomeTeam)
            .Include(sm => sm.AwayTeam)
            .Include(sm => sm.MatchLinks) // Not sure we even need this
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

            if (seasonMatch.HomeTeam is not null)
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
                    Id = seasonMatch.BracketMatch.HomeTeamSeedNumber ?? throw new Exception("Missing home seed"),
                    Tournament_id = seasonID,
                    Name = $"Seed {seasonMatch.BracketMatch.HomeTeamSeedNumber}",
                });
            }
            else
            {
                throw new Exception($"Season Match {seasonMatch.SeasonMatchID} is not valid for first round of bracket");
            }

            if (seasonMatch.AwayTeam is not null)
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
                    Id = seasonMatch.BracketMatch.AwayTeamSeedNumber ?? throw new Exception("Missing away seed"),
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
                    Size = result.Participants.Count, // All teams, includes, TODO: should include byes
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
            Status = sm.BracketMatch.RoundNumber is 1 ? Status.Ready : Status.Locked,
            Opponent1 = new ParticpantResult()
            {
                Id = sm.HomeTeamID,
                Position = sm.BracketMatch.HomeTeamSeedNumber ?? null,
                Forfeit = sm.HomeTeamResult is SeasonMatchResult.Forfeit,
                Score = sm.HomeTeamScore,
                Result = MapResult(sm.HomeTeamResult),
            },
            Opponent2 = new ParticpantResult()
            {
                Id = sm.AwayTeamID,
                Position = sm.BracketMatch.AwayTeamSeedNumber ?? null,
                Forfeit = sm.AwayTeamResult is SeasonMatchResult.Forfeit,
                Score = sm.AwayTeamScore,
                Result = MapResult(sm.AwayTeamResult),
            }
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
            Status = Status.Locked,
            Opponent1 = new ParticpantResult()
            {
                Id = sm.HomeTeamID,
                Position = GetWinnerMatchPosition(sm.BracketMatch.HomeTeamPreviousMatchBracketInfo?.SeasonMatchID, result.Matches),
                Forfeit = sm.HomeTeamResult is SeasonMatchResult.Forfeit,
                Score = sm.HomeTeamScore,
                Result = MapResult(sm.HomeTeamResult),
            },
            Opponent2 = new ParticpantResult()
            {
                Id = sm.AwayTeamID,
                Position = GetWinnerMatchPosition(sm.BracketMatch.AwayTeamPreviousMatchBracketInfo?.SeasonMatchID, result.Matches),
                Forfeit = sm.AwayTeamResult is SeasonMatchResult.Forfeit,
                Score = sm.AwayTeamScore,
                Result = MapResult(sm.AwayTeamResult),
            }
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
                Status = Status.Locked,
                Opponent1 = new ParticpantResult()
                {
                    Id = grandFinal.HomeTeamID,
                    Forfeit = grandFinal.HomeTeamResult is SeasonMatchResult.Forfeit,
                    Score = grandFinal.HomeTeamScore,
                    Result = MapResult(grandFinal.HomeTeamResult),
                },
                Opponent2 = new ParticpantResult()
                {
                    Id = grandFinal.AwayTeamID,
                    Position = 1, // There is only 1 game in loser bracket final so this is always 1
                    Forfeit = grandFinal.AwayTeamResult is SeasonMatchResult.Forfeit,
                    Score = grandFinal.AwayTeamScore,
                    Result = MapResult(grandFinal.AwayTeamResult),
                }
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
                    Status = Status.Ready,
                    Opponent1 = new ParticpantResult()
                    {
                        Id = grandFinalSuddenDeath.HomeTeamID,
                        Forfeit = grandFinalSuddenDeath.HomeTeamResult is SeasonMatchResult.Forfeit,
                        Score = grandFinalSuddenDeath.HomeTeamScore,
                        Result = MapResult(grandFinalSuddenDeath.HomeTeamResult),
                    },
                    Opponent2 = new ParticpantResult()
                    {
                        Id = grandFinalSuddenDeath.AwayTeamID,
                        Forfeit = grandFinalSuddenDeath.AwayTeamResult is SeasonMatchResult.Forfeit,
                        Score = grandFinalSuddenDeath.AwayTeamScore,
                        Result = MapResult(grandFinalSuddenDeath.AwayTeamResult),
                    }
                };
                result.Matches.Add(mappedGrandFinalSuddenDeath);
            }
        }

        //result.MatchGames - Does not seem to matter
        result.MatchGames = new();

        return result;
    }

    private Result? MapResult(SeasonMatchResult? r)
    {
        return r switch
        {
            SeasonMatchResult.Won => Result.win,
            SeasonMatchResult.Loss => Result.loss,
            SeasonMatchResult.Forfeit => Result.loss,
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

    public async Task SetSeeds(int seasonID, CancellationToken ct = default)
    {
        var transaction = await _grifballContext.Database.BeginTransactionAsync(ct);
        var standings = await _teamStandingsService.GetTeamStandings(seasonID, ct);

        var seeds = standings.Select((dto, index) => (++index, dto.TeamID)).ToDictionary();

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
            var foundHomeTeam = seeds.TryGetValue(match.BracketMatch.HomeTeamSeedNumber ?? throw new Exception("Missing home team seed number"), out var homeTeamID);

            var foundAwayTeam = seeds.TryGetValue(match.BracketMatch.AwayTeamSeedNumber ?? throw new Exception("Missing away team seed number"), out var awayTeamID);

            if (foundHomeTeam is false || foundAwayTeam is false)
                throw new Exception("Missing home or away team. Byes are currently not supported");

            match.HomeTeamID = homeTeamID;
            match.AwayTeamID = awayTeamID;
        }

        await _grifballContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        //var winner = playoffMatches.Where(x => x.BracketMatch.Bracket is Bracket.Winner).Select(x => x.BracketMatch).ToList();

        //var lsoer = playoffMatches.Where(x => x.BracketMatch.Bracket is Bracket.Loser).Select(x => x.BracketMatch).ToList();

        //var grandfinal = playoffMatches.Select(x => x.BracketMatch).FirstOrDefault(x => x.Bracket is Bracket.GrandFinal);

        //var sd = playoffMatches.Select(x => x.BracketMatch).FirstOrDefault(x => x.Bracket is Bracket.GrandFinalSuddenDeath);

        //var match = playoffMatches.FirstOrDefault();

        //var nextMatch = DetermineNextMatches(match);

        //var foo = playoffMatches.Select(x => new
        //{
        //    Match = x,
        //    NextMatches = DetermineNextMatches(x),
        //}).ToList();

        //var b = standings;
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
}
