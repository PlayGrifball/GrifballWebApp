using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Historical.Sources;

/// <summary>
/// The archive's drawn brackets, cell by cell (<see cref="BracketSlotCells"/>), for the catalog's
/// <see cref="HistoricalSourceConfig.Brackets"/>. Each list is in the order the series were played.
/// </summary>
public static class HistoricalBracketLayouts
{
    private static BracketSlotCells Slot(Bracket bracket, int round, string home, string away, string? winner,
        string? score = null, string? homeSeed = null, string? awaySeed = null, string? homeScore = null, string? awayScore = null, int? bestOf = null)
        => new() { Bracket = bracket, Round = round, Home = home, Away = away, Winner = winner, Score = score, HomeSeed = homeSeed, AwaySeed = awaySeed, HomeScore = homeScore, AwayScore = awayScore, BestOf = bestOf };

    private static BracketSlotCells Winners(int round, string home, string away, string? winner, string? score = null, string? homeSeed = null, string? awaySeed = null, int? bestOf = null)
        => Slot(Bracket.Winner, round, home, away, winner, score, homeSeed, awaySeed, bestOf: bestOf);

    private static BracketSlotCells Losers(int round, string home, string away, string? winner, string? homeSeed = null, int? bestOf = null)
        => Slot(Bracket.Loser, round, home, away, winner, homeSeed: homeSeed, bestOf: bestOf);

    /// <summary>
    /// The Infinite seasons' Playoffs template (SL24, FL24, SL25), eight teams: seeds in B, teams in C, "vs (2-1)" (the
    /// winner's first) between each pair, winners on in G, J and L (the champion). A top seed's BYE leaves its
    /// quarter-final's cells empty.
    /// </summary>
    public static List<BracketSlotCells> InfiniteEight() =>
    [
        Winners(1, "C2", "C5", "G5", "C4", "B2", "B5"),
        Winners(1, "C8", "C11", "G8", "C10", "B8", "B11"),
        Winners(1, "C14", "C17", "G17", "C16", "B14", "B17"),
        Winners(1, "C20", "C23", "G20", "C22", "B20", "B23"),
        Winners(2, "G5", "G8", "J11", "G7"),
        Winners(2, "G17", "G20", "J14", "G19"),
        Winners(3, "J11", "J14", "L13", "J13"),
    ];

    /// <summary>The same template cut to six teams (WL25): seeds 1 and 2 wait in the semi-finals (G5, G17).</summary>
    public static List<BracketSlotCells> InfiniteSix() =>
    [
        Winners(1, "C8", "C11", "G8", "C10", "B8", "B11"),
        Winners(1, "C14", "C17", "G14", "C16", "B14", "B17"),
        Winners(2, "G5", "G8", "J11", "G7"),
        Winners(2, "G14", "G17", "J14", "G16"),
        Winners(3, "J11", "J14", "L13", "J13"),
    ];

    /// <summary>H3 Action League 2022's PLAYOFFS: seeds ("1st") left of each team, each side's wins beside it, six teams.</summary>
    public static List<BracketSlotCells> H3Action() =>
    [
        Slot(Bracket.Winner, 1, "D5", "D7", "H7", homeSeed: "C5", awaySeed: "C7", homeScore: "E5", awayScore: "E7", bestOf: 3),
        Slot(Bracket.Winner, 1, "D9", "D11", "H9", homeSeed: "C9", awaySeed: "C11", homeScore: "E9", awayScore: "E11", bestOf: 3),
        Slot(Bracket.Winner, 2, "H5", "H7", "J7", homeSeed: "G5", homeScore: "I5", awayScore: "I7", bestOf: 3),
        Slot(Bracket.Winner, 2, "H9", "H11", "J9", homeSeed: "C9", awaySeed: "G11", homeScore: "I9", awayScore: "I11", bestOf: 3),
        Winners(3, "J7", "J9", "L8", "J8", bestOf: 5),
    ];

    /// <summary>The 2022 H4 Bid Season's PLAYOFFS: "Team - seed N" cells, a play-in, three rounds; its final names no winner.</summary>
    public static List<BracketSlotCells> H4Bid() =>
    [
        Winners(1, "A3", "A5", "B7", "A4", bestOf: 3),
        Winners(2, "B5", "B7", "C5", "B6", bestOf: 3),
        Winners(2, "B9", "B11", "C11", "B10", bestOf: 3),
        Winners(3, "C5", "C7", "D7", "C6", bestOf: 3),
        Winners(3, "C9", "C11", "D9", "C10", bestOf: 3),
        Winners(4, "D7", "D9", null, "D8", bestOf: 3),
    ];

    /// <summary>GWL Spring League 2023's Playoff Bracket: "Rated R (4)" cells, the score under each pair naming its winner.</summary>
    public static List<BracketSlotCells> GwlSpring2023() =>
    [
        Winners(1, "B19", "B23", "E21", "B21", bestOf: 5),
        Winners(1, "B27", "B31", "E29", "B29", bestOf: 5),
        Winners(2, "E13", "E21", "H17", "E15", bestOf: 5),
        Winners(2, "E29", "E37", "H33", "E31", bestOf: 5),
        Winners(3, "H17", "H33", "K25", "H25", bestOf: 7),
    ];

    /// <summary>Summer League 2021 (Pro)'s double-elimination Bracket: seeds in A, the losers' bracket from row 17; never
    /// finished (no losers' final, no champion).</summary>
    public static List<BracketSlotCells> Summer2021Pro() =>
    [
        Winners(1, "B4", "B7", "F5", homeSeed: "A4", awaySeed: "A7"),
        Winners(1, "B10", "B13", "F11", homeSeed: "A10", awaySeed: "A13"),
        Losers(1, "B20", "B23", "F22", "A20"),
        Losers(1, "B26", "B29", "F28", "A26"),
        Winners(2, "F5", "F11", "O8"),
        Losers(2, "F22", "F28", "K27"),
        Losers(3, "K22", "K27", "O24"),
        Slot(Bracket.GrandFinal, 3, "O8", "O24", "S15"),
    ];

    /// <summary>Summer League 2021 (Amateur)'s Bracket: five teams, seeds 4 and 5 playing in; never finished.</summary>
    public static List<BracketSlotCells> Summer2021Amateur() =>
    [
        Winners(1, "B10", "B13", "F16", homeSeed: "A10", awaySeed: "A13"),
        Winners(2, "F6", "F9", "J8", homeSeed: "E6"),
        Winners(2, "F13", "F16", "J14", homeSeed: "E13"),
        Winners(3, "J8", "J14", null),
    ];

    /// <summary>Summer League 2025 (Classic Pro)'s double elimination: four teams in the winners' bracket, two more starting
    /// in the losers'; the losers' team won the grand final (Match 8) and its reset (Match 9).</summary>
    public static List<BracketSlotCells> Summer2025Pro() =>
    [
        Winners(1, "B19", "B23", "E21", bestOf: 3),
        Winners(1, "B27", "B31", "E29", bestOf: 3),
        Losers(1, "B45", "B49", "E47", bestOf: 3),
        Losers(1, "B53", "B57", "E55", bestOf: 3),
        Winners(2, "E21", "E29", "H25", bestOf: 3),
        Losers(2, "E47", "E55", "H51", bestOf: 3),
        Losers(3, "H51", "H57", "H31", bestOf: 3),
        Slot(Bracket.GrandFinal, 3, "H25", "H31", null, bestOf: 5),
        Slot(Bracket.GrandFinalSuddenDeath, 4, "H25", "H31", "N30", bestOf: 5),
    ];

    /// <summary>Summer League 2025 (Classic Legacy)'s double elimination: four teams in the winners' bracket, four starting in
    /// the losers'.</summary>
    public static List<BracketSlotCells> Summer2025Legacy() =>
    [
        Winners(1, "B19", "B23", "E21", bestOf: 3),
        Winners(1, "B27", "B31", "E29", bestOf: 3),
        Losers(1, "B45", "B49", "E47", bestOf: 3),
        Losers(1, "B53", "B57", "E55", bestOf: 3),
        Winners(2, "E21", "E29", "H25", bestOf: 3),
        Losers(2, "E42", "E47", "H46", bestOf: 3),
        Losers(2, "E55", "E60", "H55", bestOf: 3),
        Losers(3, "H46", "H55", "K50", bestOf: 3),
        Losers(4, "K44", "K50", "H31", bestOf: 3),
        Slot(Bracket.GrandFinal, 3, "H25", "H31", "K27", bestOf: 5),
    ];

    /// <summary>The Respawn series' tabs per round: Round 1-3, Semifinals, then Finals (the grand final).</summary>
    public static List<BracketRoundTab> RespawnRounds() =>
    [
        new() { Sheet = "Round 1", Round = 1 },
        new() { Sheet = "Round 2", Round = 2 },
        new() { Sheet = "Round 3", Round = 3 },
        new() { Sheet = "Semifinals", Round = 4 },
        new() { Sheet = "Finals", Round = 5, Bracket = Bracket.GrandFinal },
    ];
}
