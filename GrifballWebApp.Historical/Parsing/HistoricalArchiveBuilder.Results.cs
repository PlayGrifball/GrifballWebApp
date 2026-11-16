using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;

namespace GrifballWebApp.Historical.Parsing;

public sealed partial class HistoricalArchiveBuilder
{
    /// <summary>
    /// Each result list's games go under their season's regular season (a season of its own for a
    /// <see cref="ResultsTab.CustomSeason"/>), of its game when the list names none, each side named as the season names
    /// the team (<see cref="SeasonTeamNames.Resolve"/>). A season no other tab names a team of gets the list's teams.
    /// </summary>
    private void AddResults()
    {
        foreach (var (sourceId, year, defaults, tab, games) in _results)
        {
            var workbook = tab.Season ?? sourceId;
            var regular = tab.CustomSeason is { } name
                ? CustomSeason(sourceId, year, defaults, tab, name)
                : _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase)
                    && (tab.Tab is null || string.Equals(s.SourceSheet, tab.Tab, StringComparison.OrdinalIgnoreCase)));
            if (games is null || regular is null)
            {
                _warnings.Add($"{sourceId}: results '{tab.Sheet}' not read ({(games is null ? "no such sheet" : $"no season from {workbook}")})");
                continue;
            }
            var key = HistoricalKeys.SeasonSourceId(regular.SourceWorkbook!, regular.SourceSheet!);
            var names = SeasonTeamNames.Of(SeasonOf(regular), tab.Teams);
            foreach (var game in games)
            {
                game.SeasonSourceId = key;
                if (game.GameVersion.Length == 0)
                    game.GameVersion = regular.GameVersion;
                foreach (var side in game.Teams)
                    side.Name = names.Resolve(side.Name);
            }
            // The list is all a season names its teams by: they are its teams
            if (names.IsEmpty)
                regular.Teams.AddRange(games.SelectMany(g => g.Teams).Select(t => t.Name).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(n => regular.Teams.TrueForAll(t => !string.Equals(t.TeamName, n, StringComparison.OrdinalIgnoreCase)))
                    .Select(n => new ArchiveTeam { TeamName = n }));
            _games.AddRange(games);
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.Add(key);
        }
    }

    /// <summary>A result list's season of its own, made once (by its name and workbook), with the workbook's league and division.</summary>
    private ArchiveSeason CustomSeason(string sourceId, int? year, SeasonDefaults? defaults, ResultsTab tab, string name)
    {
        var existing = _seasons.FirstOrDefault(s => s.SeasonName == name && string.Equals(s.SourceWorkbook, sourceId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;
        var season = new ArchiveSeason
        {
            SeasonName = name,
            GameVersion = tab.Game ?? defaults?.Game ?? "",
            League = defaults?.League ?? "AGLA",
            Division = defaults?.Division ?? "Open",
            Format = defaults?.Format,
            SourceWorkbook = sourceId,
            SourceSheet = tab.Sheet,
            SourceYear = year,
        };
        _seasons.Add(season);
        return season;
    }

    /// <summary>The entries of an entry's season: its workbook's of the same name.</summary>
    private List<ArchiveSeason> SeasonOf(ArchiveSeason entry)
        => _seasons.Where(s => string.Equals(s.SourceWorkbook, entry.SourceWorkbook, StringComparison.OrdinalIgnoreCase) && s.SeasonName == entry.SeasonName).ToList();

    /// <summary>The per-game tabs' games name a side (a block's team, a title's) as their season does: as a result list's.</summary>
    private void NameGameSides()
    {
        var bySeason = _seasons.Where(s => s.SourceWorkbook is not null && s.SourceSheet is not null)
            .GroupBy(s => HistoricalKeys.SeasonSourceId(s.SourceWorkbook!, s.SourceSheet!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, SeasonTeamNames>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in _games.Where(g => g.Series is null && g.Teams.Any(t => t.Name is not null)))
        {
            if (!bySeason.TryGetValue(game.SeasonSourceId, out var entry))
                continue;
            if (!names.TryGetValue(game.SeasonSourceId, out var seasonNames))
                names[game.SeasonSourceId] = seasonNames = SeasonTeamNames.Of(SeasonOf(entry), []);
            foreach (var side in game.Teams)
                side.Name = seasonNames.Resolve(side.Name);
        }
    }

    /// <summary>
    /// A season's names of its teams (its stats' and its standings'), and of its players and captains, which a result list
    /// names a team by: <see cref="Resolve"/> finds the season's name of a team the list spells another way.
    /// </summary>
    private sealed class SeasonTeamNames
    {
        private readonly List<string> _known;
        private readonly Dictionary<string, string> _aliases;
        /// <summary>Each player's (and captain's) gamertag, with their team.</summary>
        private readonly List<(string Gamertag, string Team)> _players;

        private SeasonTeamNames(List<string> known, Dictionary<string, string> aliases, List<(string, string)> players)
            => (_known, _aliases, _players) = (known, aliases, players);

        /// <summary>The season names no team: its result list is all it has.</summary>
        public bool IsEmpty => _known.Count == 0;

        public static SeasonTeamNames Of(List<ArchiveSeason> season, Dictionary<string, string> aliases)
        {
            var known = season.SelectMany(s => s.Players).Select(p => p.TeamName).Concat(season.SelectMany(s => s.Teams).Select(t => t.TeamName))
                .OfType<string>().Select(n => n.Trim()).Where(n => n.Length > 0 && !HistoricalKeys.IsExcludedTeamName(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var players = season.SelectMany(s => s.Teams).Where(t => !string.IsNullOrWhiteSpace(t.Captain)).Select(t => (t.Captain!.Trim(), t.TeamName))
                .Concat(season.SelectMany(s => s.Players).Where(p => !HistoricalKeys.IsExcludedTeamName(p.TeamName) && !string.IsNullOrWhiteSpace(p.TeamName))
                    .Select(p => (p.Gamertag.Trim(), p.TeamName!.Trim())))
                .ToList();
            return new SeasonTeamNames(known, new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase), players);
        }

        /// <summary>
        /// The season's name of the team a list names: the alias its tab gives, else the season's team spelled alike
        /// (<see cref="Closest"/>), else the one it is an abbreviation of, or that abbreviates it (its words' first letters:
        /// "MAB" for Mediocre At Best, "ApoPo" for Apocalypse Ponies), else the team of the player or captain it names
        /// ("Smurf" for Sauted Smurf's team). A name none of these find stays as it is.
        /// </summary>
        public string? Resolve(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;
            var listed = _aliases.GetValueOrDefault(name.Trim()) ?? name.Trim();
            if (Closest(listed, _known) is { } same)
                return same;
            var abbreviated = _known.Where(k => Abbreviates(listed, k) || Abbreviates(k, listed)).ToList();
            if (abbreviated.Count == 1)
                return abbreviated[0];
            return PlayerTeam(listed) ?? listed;
        }

        /// <summary>The team of the one player (or captain) whose gamertag is the name, or has it as a word or its start.</summary>
        private string? PlayerTeam(string name)
        {
            var key = HistoricalKeys.Normalize(name);
            var exact = _players.Where(p => HistoricalKeys.Normalize(p.Gamertag) == key).Select(p => p.Team).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (exact.Count > 0)
                return exact.Count == 1 ? exact[0] : null;
            var named = _players.Where(p => Words(p.Gamertag).Contains(key) || HistoricalKeys.Normalize(p.Gamertag).StartsWith(key, StringComparison.Ordinal))
                .Select(p => p.Team).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return key.Length >= 3 && named.Count == 1 ? named[0] : null;
        }

        private static List<string> Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(HistoricalKeys.Normalize).ToList();
    }

    /// <summary>
    /// Whether <paramref name="abbreviation"/> is <paramref name="name"/>'s words, each cut to its start ("G&amp;tC" of
    /// "Galvin &amp; the Chipmunks", "ApoPo" of "Apocalypse Ponies", "ZMD" of "Zaps MD"): every word gives at least its first
    /// letter, in order, and an ampersand is "and". A name of one word is never abbreviated (that would be any prefix).
    /// </summary>
    internal static bool Abbreviates(string abbreviation, string name)
    {
        var letters = TeamKey(abbreviation.Replace("&", "a", StringComparison.Ordinal));
        var words = NameWords(name.Replace("&", " and ", StringComparison.Ordinal));
        return words.Count > 1 && letters.Length < TeamKey(name).Length && Splits(letters, words, 0, 0);
    }

    /// <summary>Whether the letters from <paramref name="at"/> are the words from <paramref name="word"/> on, each cut to a non-empty start.</summary>
    private static bool Splits(string letters, List<string> words, int at, int word)
    {
        if (word == words.Count)
            return at == letters.Length;
        for (var take = 1; take <= words[word].Length && at + take <= letters.Length; take++)
        {
            if (string.CompareOrdinal(letters, at, words[word], 0, take) != 0)
                break;
            if (Splits(letters, words, at + take, word + 1))
                return true;
        }
        return false;
    }

    /// <summary>A name's words, lower case: split at anything but a letter or digit, and where a capital follows a small
    /// letter ("AJunkyardDog" is "ajunkyard", "dog").</summary>
    private static List<string> NameWords(string name)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var boundary = !char.IsLetterOrDigit(c) || (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]));
            if (boundary && word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
            if (char.IsLetterOrDigit(c))
                word.Append(char.ToLowerInvariant(c));
        }
        if (word.Length > 0)
            words.Add(word.ToString());
        return words;
    }
}
