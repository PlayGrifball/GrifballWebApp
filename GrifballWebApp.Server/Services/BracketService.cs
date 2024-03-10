using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using System.Collections;

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

                matches.Add(new SeasonMatch()
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
                    }
                });
                matchNumber++;
            }
        }

        if (doubleElimination)
        {
            matchNumber = 1;
        }

        //await _grifballContext.AddRangeAsync(matches, ct);
        //await _grifballContext.SaveChangesAsync(ct);
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
}
