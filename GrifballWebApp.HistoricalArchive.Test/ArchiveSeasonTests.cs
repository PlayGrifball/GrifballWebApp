using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>Single seasons of the real archive whose sheets once lost teams: what they import now.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveSeasonTests
{
    /// <summary>
    /// The Teams tab's regular-season records, team per column, as the archive names the teams. RDG NHL's team was never
    /// named: the workbook calls it "Team Name" (TeamInfo, the Teams tab's column I), and the config names it after its
    /// captain. The tab has no ranks or final places.
    /// </summary>
    private static readonly (string Team, int? Wins, int? Losses)[] H3ActionRecords =
    [
        ("12 Inches of Tommy", 5, 15), ("42 Dugga’s (Yeah, aight)", 17, 3), ("ALPHA", 14, 6), ("GASLIGHT", 3, 17), ("JUNKYARD DOGS", 12, 8),
        ("LIKE CLOCKWORK", 14, 6), ("RDG NHL", 12, 8), ("sPeCiAl ReAsOnS", 7, 13), ("THE BOYS SZN 3", 4, 16), ("The Smurfs", 14, 6), ("ZEG", 6, 14),
    ];

    /// <summary>RDG NHL's first three picks on the Teams tab's draft board (column I, "Team Name"), as the stats spell them.</summary>
    private static readonly (string Gamertag, int? Round)[] RdgNhlPicks = [("DlGITAL PAIN", 1), ("MysticalNovice", 2), ("Myst the Cold", 3)];

    [Test]
    public async Task H3Action2022_StandingsCoverAllElevenTeams_TheTeamNameSlotToo()
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == "2022 H3 Action League")
            .Select(t => new { t.TeamName, t.RecordedWins, t.RecordedLosses, HasCaptain = t.CaptainID != null })
            .ToListAsync();
        if (teams.Count == 0)
            Assert.Ignore("2022 H3 Action League isn't in this copy of the archive");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.OrderBy(t => t.TeamName, StringComparer.Ordinal).Select(t => (t.TeamName, t.RecordedWins, t.RecordedLosses)),
                Is.EqualTo(H3ActionRecords.OrderBy(t => t.Team, StringComparer.Ordinal)), "Each team's record from the Teams tab");
            Assert.That(teams.Where(t => !t.HasCaptain).Select(t => t.TeamName), Is.Empty, "The Teams tab names every team's captain");
        }
    }

    [Test]
    public async Task H3Action2022_EveryTeamHasItsDraftPicks_RdgNhlsTeamNameColumnToo()
    {
        await using var context = Archive.NewContext();
        // Teams with their players, then the picks in memory: SQLite has no APPLY for a projection of both
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == "2022 H3 Action League")
            .Include(t => t.TeamPlayers).ThenInclude(p => p.User)
            .ToListAsync();
        var picks = teams.SelectMany(t => t.TeamPlayers.Where(p => p.DraftRound != null).Select(p => new { t.TeamName, p.User.DisplayName, p.DraftRound })).ToList();
        if (picks.Count == 0)
            Assert.Ignore("2022 H3 Action League isn't in this copy of the archive, or has no draft");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(picks.Select(p => p.TeamName).Distinct().Count(), Is.EqualTo(11), "Each of the 11 teams' picks from the Teams tab's board");
            Assert.That(picks.Where(p => p.TeamName == "RDG NHL").Select(p => (p.DisplayName!.ToUpperInvariant(), p.DraftRound)),
                Is.SupersetOf(RdgNhlPicks.Select(p => (p.Gamertag.ToUpperInvariant(), p.Round))), "Its board column says \"Team Name\"");
        }
    }

    [Test]
    public async Task H3Action2022_BracketsSixthSeed_IsRdgNhlsTeamName_AndEveryPlayoffSeriesHasItsSlot()
    {
        await using var context = Archive.NewContext();
        var series = await context.SeasonMatches.AsNoTracking()
            .Where(m => m.Season.HistoricalSourceID != null && m.Season.SeasonName == "2022 H3 Action League" && m.BracketMatch != null)
            .Select(m => new { Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName })
            .ToListAsync();
        if (series.Count == 0)
            Assert.Ignore("2022 H3 Action League isn't in this copy of the archive, or has no bracket");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(series.Select(s => (s.Home.ToUpperInvariant(), s.Away.ToUpperInvariant())), Has.Some.EqualTo(("ALPHA", "RDG NHL")),
                "The PLAYOFFS' 6th seed, \"Team Name\", against Alpha");
            Assert.That(series.Where(s => s.Home == "THE BOYS SZN 3" || s.Away == "THE BOYS SZN 3"), Is.Empty, "THE BOYS SZN 3 didn't make the playoffs");
            Assert.That(Archive.Report.Warnings.Where(w => w.StartsWith("2022-h3-action", StringComparison.Ordinal) && w.Contains("in no slot", StringComparison.Ordinal)), Is.Empty);
        }
    }

    /// <summary>
    /// No season has a team named by a number: FL21's "0" and "1" (both SML21 workbooks: game sides of hosts who played no
    /// game, below the stats) and SML20's "4" and "5" (its playoff bracket's seeds, below the playoff stats) were rows outside
    /// the stats tables.
    /// </summary>
    [Test]
    public async Task NoSeason_HasATeamNamedByANumber()
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID != null)
            .Select(t => new { t.Season.SeasonName, t.TeamName })
            .ToListAsync();

        Assert.That(teams.Where(t => t.TeamName.Trim().Length > 0 && t.TeamName.Trim().All(char.IsAsciiDigit)).Select(t => $"{t.SeasonName}: {t.TeamName}"), Is.Empty);
    }

    /// <summary>Winter League 2021 (Legacy)'s playoff teams, whose Playoff Stats tab both SML21 workbooks carry a copy of.</summary>
    private static readonly string[] Wl21LegacyPlayoffTeams = ["Fruit Gang", "Limitless", "Only in Dreams", "Rock Bottom", "Yung Humma"];

    [TestCase("Summer League 2021 (Pro)")]
    [TestCase("Summer League 2021 (Amateur)")]
    public async Task Sml21_HasNoneOfWinterLeague2021LegacysPlayoffTeams(string seasonName)
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == seasonName)
            .Select(t => t.TeamName).ToListAsync();
        if (teams.Count == 0)
            Assert.Ignore($"{seasonName} isn't in this copy of the archive");

        Assert.That(teams.Intersect(Wl21LegacyPlayoffTeams, StringComparer.OrdinalIgnoreCase), Is.Empty, "The hidden Playoff Stats tab is WL21 Legacy's");
    }

    /// <summary>The champions record spells two champions otherwise than their sheets ("Yung Hummu", "Yeji's Iced Toasted
    /// Banana"): the titles go to the team the stats have, not a second team of the record's name.</summary>
    [TestCase("Winter League 2021 (Legacy)", "Yung Humma")]
    [TestCase("Summer League 2021 (Pro)", "Yeji's Toasted Banana Icey")]
    public async Task TheChampion_IsTheStatsTeam_NotASecondTeamOfTheRecordsSpelling(string seasonName, string champion)
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == seasonName)
            .Select(t => new { t.TeamName, Titles = t.Season.PlayerChampionships.Count(c => c.TeamID == t.TeamID), Lines = context.PlayerSeasonStats.Count(p => p.TeamID == t.TeamID) })
            .ToListAsync();
        if (teams.Count == 0)
            Assert.Ignore($"{seasonName} isn't in this copy of the archive");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Where(t => t.Titles > 0).Select(t => t.TeamName), Is.EqualTo(new[] { champion }), "The titles' team");
            Assert.That(teams.Where(t => t.Lines == 0).Select(t => t.TeamName), Is.Empty, "No team of the record's name without stats");
        }
    }

    /// <summary>The champions record's "Summer League 2022" (Junkyard Dogs) is the 2022 H3 Action League's title: its five on
    /// the season's JUNKYARD DOGS, who won its bracket, and no titles-only Summer League 2022 left.</summary>
    [Test]
    public async Task H3Action2022_HasTheSummerLeague2022Title_OnJunkyardDogs()
    {
        await using var context = Archive.NewContext();
        var titles = await context.Set<PlayerChampionship>().AsNoTracking()
            .Where(c => c.Season.HistoricalSourceID != null && (c.Season.SeasonName == "2022 H3 Action League" || c.Season.SeasonName == "Summer League 2022"))
            .Select(c => new { c.Season.SeasonName, c.Team.TeamName })
            .ToListAsync();
        if (titles.Count == 0)
            Assert.Ignore("Neither 2022 H3 Action League nor the record's Summer League 2022 is in this copy of the archive");

        Assert.That(titles.Select(t => $"{t.SeasonName}: {t.TeamName.ToUpperInvariant()}").ToList(), Has.Count.EqualTo(5).And.All.EqualTo("2022 H3 Action League: JUNKYARD DOGS"));
    }

    private static readonly string[] Fall2020Champions = ["Regresser", "Sabasauros Rex", "Icey Tanker", "cxrnchip", "Credit Clown"];

    [Test]
    public async Task FallLeague2020_AscendedTitlesAreOnThePcStatsSeason()
    {
        if (!Archive.Snapshots.Manifest.ContainsKey("2020-fl20-pc-schedule-stats") ||
            !Archive.Snapshots.Manifest.ContainsKey("history-agla-champions"))
            Assert.Ignore("Fall League 2020's stats or champions workbook is missing from this copy of the archive");

        await using var context = Archive.NewContext();
        var seasons = await context.Seasons.AsNoTracking()
            .Where(s => s.League.Slug == "agla" && s.SeasonName.StartsWith("Fall League 2020"))
            .Select(s => new { s.SeasonName, Game = s.GameVersion.Slug })
            .ToListAsync();
        var titles = await context.Set<PlayerChampionship>().AsNoTracking()
            .Where(c => c.Season.League.Slug == "agla" && c.Season.SeasonName.StartsWith("Fall League 2020"))
            .Select(c => new { c.Season.SeasonName, c.Team.TeamName, c.Tier, c.GamertagUsed })
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons, Has.Count.EqualTo(1));
            Assert.That(seasons.Select(s => (s.SeasonName, s.Game)), Is.All.EqualTo(("Fall League 2020 (PC)", "halo3")));
            Assert.That(titles, Has.Count.EqualTo(5));
            Assert.That(titles.Select(c => (c.SeasonName, c.TeamName, c.Tier)), Is.All.EqualTo(("Fall League 2020 (PC)", "Ascended", "Open")));
            Assert.That(titles.Select(c => c.GamertagUsed), Is.EquivalentTo(Fall2020Champions));
        }
    }

    /// <summary>The game histories' sides named after a captain the season knows by another spelling: WL23's SPANKATRON
    /// (USURPER SZN's SPANKATR0N), SL25's nkot (Junkyard Dogs) and Squirel (Murder Incorporated) are their teams.</summary>
    [TestCase("2023-da-wl23-schedule/Game History#")]
    [TestCase("2025-octane-sl25-bid-schedule/Game History#")]
    public async Task AGameHistorysCaptainNamedSide_IsTheirTeam(string keys)
    {
        await using var context = Archive.NewContext();
        var sides = await context.Set<GrifballWebApp.Domain.Archive.RecordedGameTeam>().AsNoTracking()
            .Where(t => t.RecordedGame.SourceKey.StartsWith(keys))
            .Select(t => new { t.Name, HasTeam = t.TeamID != null })
            .ToListAsync();
        if (sides.Count == 0)
            Assert.Ignore($"No {keys} games in this copy of the archive");

        Assert.That(sides.Where(s => !s.HasTeam).Select(s => s.Name).Distinct(), Is.Empty);
    }

    /// <summary>SL25's Team Chart names two captains by a nickname: Junkyard Dogs' "Nkot" (NKOTB4evah, owner-confirmed on
    /// #496) and Cosmic Oblivion's "DaedricKindred" (KindredDaedric). Every SL25 team has its captain.</summary>
    [Test]
    public async Task Sl25_EveryTeamHasItsCaptain_TheTwoTheChartNamesByANicknameToo()
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.Season.HistoricalSourceID == "2025-octane-sl25-bid-stats/Regular Season Stats Sheet stat")
            .Select(t => new { t.TeamName, Captain = t.Captain == null ? null : t.Captain.User.UserName })
            .ToListAsync();
        if (teams.Count == 0)
            Assert.Ignore("Spring League 2025 (Octane Bid) isn't in this copy of the archive");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Where(t => t.Captain is null).Select(t => t.TeamName), Is.Empty);
            var captains = teams.ToDictionary(t => t.TeamName, t => t.Captain);
            Assert.That(captains.GetValueOrDefault("Junkyard Dogs"), Does.EndWith("NKOTB4evah").IgnoreCase);
            Assert.That(captains.GetValueOrDefault("Cosmic Oblivion"), Does.EndWith("KindredDaedric").IgnoreCase);
        }
    }

    /// <summary>H4 Bid 2022's PLAYOFFS writes its final "Beer - Seed 1 vs (2-0) Forklift Certified - Seed 3" without a winner;
    /// the Teams tab's playoff records say it: Beer (4-0) lost no playoff game, Forklift Certified (4-2) two. Beer won it 2-0.</summary>
    [Test]
    public async Task H4Bid2022_TheFinal_IsBeers()
    {
        await using var context = Archive.NewContext();
        var slots = await context.SeasonMatches.AsNoTracking()
            .Where(m => m.Season.HistoricalSourceID != null && m.Season.SeasonName == "2022 H4 Bid Season" && m.BracketMatch != null)
            .Select(m => new
            {
                m.BracketMatch!.RoundNumber,
                Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName, m.HomeTeamResult, m.AwayTeamResult, m.HomeTeamScore, m.AwayTeamScore,
            })
            .ToListAsync();
        if (slots.Count == 0)
            Assert.Ignore("2022 H4 Bid Season isn't in this copy of the archive, or has no bracket");

        var final = slots.MaxBy(s => s.RoundNumber)!;
        string? winner = final.HomeTeamResult == SeasonMatchResult.Won ? final.Home : final.AwayTeamResult == SeasonMatchResult.Won ? final.Away : null;
        string? loser = winner is null ? null : winner == final.Home ? final.Away : final.Home;
        var (won, lost) = winner == final.Home ? (final.HomeTeamScore, final.AwayTeamScore) : (final.AwayTeamScore, final.HomeTeamScore);
        Assert.That((winner, loser, won, lost), Is.EqualTo(((string?)"Beer", (string?)"Forklift Certified", (int?)2, (int?)0)));
    }

    /// <summary>The champions record's "Fall League 2015" is two seasons, Halo 3 and Halo 4: its titles are on those two
    /// (the record writes the game "MCC Halo 3"), and no titles-only "Fall League 2015" is left.</summary>
    [Test]
    public async Task FallLeague2015_TitlesAreOnItsHalo3AndHalo4Seasons_NotOnASeasonOfTheirOwn()
    {
        await using var context = Archive.NewContext();
        var seasons = await context.Seasons.AsNoTracking().Where(s => s.SeasonName.StartsWith("Fall League 2015") && s.League.Slug == "agla")
            .Select(s => new { s.SeasonName, Titles = context.Set<PlayerChampionship>().Count(c => c.SeasonID == s.SeasonID) })
            .ToListAsync();
        if (!seasons.Any(s => s.SeasonName == "Fall League 2015 Halo 3"))
            Assert.Ignore("Fall League 2015's workbook is not in this copy of the archive");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Select(s => s.SeasonName), Is.EquivalentTo(FallLeague2015Seasons), "No third, titles-only season");
            Assert.That(seasons.Select(s => s.Titles), Has.All.GreaterThan(0), "Each game's champions are on its own season");
        }
    }

    private static readonly string[] FallLeague2015Seasons = ["Fall League 2015 Halo 3", "Fall League 2015 Halo 4"];

    /// <summary>Summer League 2011 is one Reach competition in two divisions (grifball.info's agla_* tables): Pro and Amateur,
    /// each with its champions and games, and no Halo 3 or whole-year copy of the same games beside them.</summary>
    [Test]
    public async Task SummerLeague2011_IsItsProAndAmateurSeasons_AllReach_NoCopyOfTheSameGames()
    {
        await using var context = Archive.NewContext();
        var seasons = await context.Seasons.AsNoTracking().Where(s => s.SeasonName.StartsWith("Summer League 2011") && s.League.Slug == "agla")
            .Select(s => new
            {
                s.SeasonName,
                Game = s.GameVersion.Slug,
                Titles = context.Set<PlayerChampionship>().Count(c => c.SeasonID == s.SeasonID),
                TitlesOffTheStatsTeams = context.Set<PlayerChampionship>().Count(c => c.SeasonID == s.SeasonID && !context.PlayerSeasonStats.Any(x => x.TeamID == c.TeamID)),
                Games = context.PlayerSeasonStats.Count(x => x.SeasonID == s.SeasonID),
            })
            .ToListAsync();
        if (seasons.Count == 0)
            Assert.Ignore("The grifball.info database export is not in this copy of the archive");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Select(s => s.SeasonName), Is.EquivalentTo(SummerLeague2011Seasons));
            Assert.That(seasons.Select(s => s.Game), Has.All.EqualTo("reach"));
            Assert.That(seasons.Select(s => s.Titles), Has.All.GreaterThan(0), "The Pro and Amateur champions");
            Assert.That(seasons.Select(s => s.TitlesOffTheStatsTeams), Has.All.EqualTo(0), "Each title is on a team of that season with stats, not a titles-only team");
            Assert.That(seasons.Select(s => s.Games), Has.All.GreaterThan(0));
        }
    }

    private static readonly string[] SummerLeague2011Seasons = ["Summer League 2011 (Amateur)", "Summer League 2011 (Pro)"];

    /// <summary>SML18 Fantasy by Rey's "Total Stats" and "Fixtures & Results" tabs are one season, Summer League 2018: its
    /// player stats and its schedule are on the one season, and no "(Fantasy Rey)" season is left.</summary>
    [Test]
    public async Task SummerLeague2018_TheStatsAndTheScheduleTabsAreOneSeason()
    {
        await using var context = Archive.NewContext();
        var seasons = await context.Seasons.AsNoTracking().Where(s => (s.SeasonName == "Summer League 2018" || s.SeasonName == "Summer League 2018 (Fantasy Rey)") && s.League.Slug == "agla")
            .Select(s => new
            {
                s.SeasonName,
                Stats = context.PlayerSeasonStats.Count(x => x.SeasonID == s.SeasonID),
                Matches = context.SeasonMatches.Count(x => x.SeasonID == s.SeasonID),
            })
            .ToListAsync();
        if (seasons.Count == 0)
            Assert.Ignore("SML18 Fantasy by Rey's workbook is not in this copy of the archive");

        Assert.That(seasons, Has.Count.EqualTo(1), "One season, not a '(Fantasy Rey)' one beside it");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons[0].SeasonName, Is.EqualTo("Summer League 2018"));
            Assert.That(seasons[0].Stats, Is.GreaterThan(0), "The Total Stats tab");
            Assert.That(seasons[0].Matches, Is.GreaterThan(0), "The Fixtures & Results tab");
        }
    }

    /// <summary>Spring League 2026 is two seasons, Major and Minor: its titles are on those, and no titles-only "Spring League
    /// 2026" is left.</summary>
    [Test]
    public async Task SpringLeague2026_TitlesAreOnItsMajorAndMinorSeasons_NotOnASeasonOfTheirOwn()
    {
        await using var context = Archive.NewContext();
        var names = await context.Seasons.AsNoTracking().Where(s => s.SeasonName.StartsWith("Spring League 2026")).Select(s => s.SeasonName).ToListAsync();
        if (!names.Contains("Spring League 2026 (Major)"))
            Assert.Ignore("Spring League 2026's workbooks are not in this copy of the archive");

        Assert.That(names, Does.Not.Contain("Spring League 2026"));
    }

    /// <summary>The 2022 H3 Action League's page links the schedule workbook too, the one its bracket is in.</summary>
    [Test]
    public async Task H3Action2022_LinksItsScheduleWorkbook_TheBracketsToo()
    {
        await using var context = Archive.NewContext();
        var season = await context.Seasons.AsNoTracking().SingleOrDefaultAsync(s => s.HistoricalSourceID == "2022-h3-action-stats/Stats");
        if (season is null)
            Assert.Ignore("The 2022 H3 Action League is not in this copy of the archive");

        Assert.That(season.HistoricalSourceLinks!.Select(l => l.Label), Does.Contain("H3 Action League 22 Schedule"));
    }

    /// <summary>No title is on a season the archive has no stats for while a season of the same name and game has them.</summary>
    [Test]
    public async Task NoTitlesOnlySeason_ShareItsNameWithASeasonOfStats()
    {
        await using var context = Archive.NewContext();
        var seasons = await context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID != null)
            .Select(s => new { s.SeasonName, s.HistoricalSourceID, HasStats = context.PlayerSeasonStats.Any(p => p.SeasonID == s.SeasonID) })
            .ToListAsync();
        var titlesOnly = seasons.Where(s => !s.HasStats && s.HistoricalSourceID!.StartsWith("history-")).Select(s => s.SeasonName).ToList();
        var withStats = seasons.Where(s => s.HasStats).Select(s => s.SeasonName).ToList();

        var shadowed = titlesOnly.Where(t => withStats.Any(n => n.StartsWith(t + " ", StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.That(shadowed, Is.Empty, "A titles-only season whose name begins another's (Fall League 2015 / Fall League 2015 Halo 3) is the same season twice");
    }
}
