using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// Decides the layout of saved Halo Infinite matches (<see cref="CustomScoreDecoder.Choose"/>) and stores what it
/// decodes on their players (<see cref="MatchParticipant.Goals"/> and the rest), which the stats read. A game variant
/// version's layout comes from all its saved matches, and is read once per instance.
/// </summary>
public sealed class CustomScoreLayouts
{
    /// <summary>How many matches are decoded and saved at a time.</summary>
    internal const int Chunk = 200;

    private readonly GrifballContext _context;
    private readonly Dictionary<Guid, CustomScoreLayout?> _variantLayouts = [];

    public CustomScoreLayouts(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The layouts of these game variant versions (<see cref="CustomScoreDecoder.VariantLayout"/>), from their saved matches.</summary>
    public async Task<IReadOnlyDictionary<Guid, CustomScoreLayout?>> VariantLayoutsAsync(IEnumerable<Guid> versions, CancellationToken ct = default)
    {
        var wanted = versions.Distinct().Where(v => !_variantLayouts.ContainsKey(v)).ToList();
        if (wanted.Count > 0)
        {
            var teams = await _context.HaloMatchTeams
                .Where(t => t.Match.UgcGameVariantVersionID != null && wanted.Contains(t.Match.UgcGameVariantVersionID.Value))
                .Select(t => new
                {
                    Version = t.Match.UgcGameVariantVersionID!.Value,
                    t.MatchID,
                    t.Score,
                    Players = t.MatchParticipants.Select(p => new { p.Score, p.PersonalScore }).ToList(),
                })
                .ToListAsync(ct);
            foreach (var version in wanted)
            {
                var matches = teams.Where(t => t.Version == version)
                    .GroupBy(t => t.MatchID)
                    .Select(m => (IReadOnlyCollection<TeamScores>)m.Select(t => new TeamScores(t.Score, t.Players.Select(p => new PlayerScores(p.Score, p.PersonalScore)).ToList())).ToList());
                _variantLayouts[version] = CustomScoreDecoder.VariantLayout(matches);
            }
        }
        return versions.Distinct().ToDictionary(v => v, v => _variantLayouts[v]);
    }

    /// <summary>Decides a match being saved and sets what it decodes on its players.</summary>
    public async Task ApplyAsync(Match match, CancellationToken ct = default)
    {
        CustomScoreLayout? variantLayout = null;
        if (match.UgcGameVariantVersionID is { } version)
            variantLayout = (await VariantLayoutsAsync([version], ct))[version];
        Apply(match, variantLayout);
    }

    /// <summary>
    /// Decodes the match (<see cref="Match.Decode"/>): its layout, and its players' decoded stats; true when anything
    /// changed. Needs its teams' players.
    /// </summary>
    public static bool Apply(Match match, CustomScoreLayout? variantLayout)
    {
        var byTeam = match.MatchTeams.OrderBy(t => t.TeamID).ToList();
        var scores = byTeam.Select(t => new TeamScores(t.Score, t.MatchParticipants.Select(p => new PlayerScores(p.Score, p.PersonalScore)).ToList())).ToList();
        var layout = CustomScoreDecoder.Choose(scores, variantLayout);
        var decoded = CustomScoreDecoder.DecodeMatch(layout, scores);

        var players = byTeam.SelectMany(t => t.MatchParticipants).ToList();
        return match.Decode(layout, players.Select((p, i) => (p, decoded[i])).ToDictionary(x => x.p, x => x.Item2));
    }

    /// <summary>
    /// Decodes saved matches again: those not decided yet, and every match of <paramref name="versions"/> (variants whose
    /// layout may have changed, as the backfill gives their matches theirs). Idempotent: a match whose result is the same
    /// isn't written. Reports (done, of) after each chunk; returns how many matches changed.
    /// </summary>
    public async Task<int> DecodeAsync(IReadOnlyCollection<Guid> versions, Action<int, int>? progress = null, CancellationToken ct = default)
    {
        var versionList = versions.ToList();
        var ids = await _context.Matches
            .Where(Match.NotDecoded)
            .Select(m => m.MatchID)
            .Union(_context.Matches
                .Where(m => m.UgcGameVariantVersionID != null && versionList.Contains(m.UgcGameVariantVersionID.Value))
                .Select(m => m.MatchID))
            .OrderBy(id => id)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return 0;

        var matchVersions = await _context.Matches
            .Where(m => m.UgcGameVariantVersionID != null && ids.Contains(m.MatchID))
            .Select(m => m.UgcGameVariantVersionID!.Value)
            .Distinct()
            .ToListAsync(ct);
        var layouts = await VariantLayoutsAsync(matchVersions, ct);

        var changed = 0;
        var done = 0;
        foreach (var chunk in ids.Chunk(Chunk))
        {
            var matches = await _context.Matches
                .Include(m => m.MatchTeams).ThenInclude(t => t.MatchParticipants)
                .Where(m => chunk.Contains(m.MatchID))
                .AsSplitQuery()
                .ToListAsync(ct);
            foreach (var match in matches)
            {
                var variantLayout = match.UgcGameVariantVersionID is { } v ? layouts.GetValueOrDefault(v) : null;
                if (Apply(match, variantLayout))
                    changed++;
            }
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
            done += chunk.Length;
            progress?.Invoke(done, ids.Count);
        }
        return changed;
    }
}
