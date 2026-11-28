using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

/// <summary>
/// The Xbox accounts a run of a per-account job (gamer pictures, emblems, armor) failed for, named in its summary on the Jobs page:
/// the counts say how many, these say whose and why. Accounts that failed for the same reason (a whole Halo call failing) share one
/// line, so one problem doesn't crowd out the others. Read by gamertag in one query, only when the run had a failure.
/// </summary>
public sealed class FailedAccounts
{
    private const int MaxReason = 120;

    private readonly List<(long Xuid, string Reason)> _failed = [];
    private List<string> _lines = [];

    /// <summary>One line per reason, "why: gamertag (xuid), ...", once <see cref="NameAsync"/> has read the gamertags.</summary>
    public IReadOnlyList<string> Lines => _lines;

    public void Add(long xuid, string reason) => _failed.Add((xuid, reason));

    /// <summary>The failed accounts' lines, by their gamertags (the XUID alone for an account without one). Called again, it builds them afresh.</summary>
    public async Task NameAsync(GrifballContext context, CancellationToken ct)
    {
        if (_failed.Count == 0)
            return;
        var xuids = _failed.Select(f => f.Xuid).Distinct().ToList();
        var gamertags = await context.XboxUsers.Where(x => xuids.Contains(x.XboxUserID))
            .Select(x => new { x.XboxUserID, x.Gamertag })
            .ToDictionaryAsync(x => x.XboxUserID, x => x.Gamertag, ct);
        string Who(long xuid) => gamertags.GetValueOrDefault(xuid) is { Length: > 0 } gamertag
            ? $"{gamertag} ({xuid})"
            : xuid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _lines = [.. _failed.GroupBy(f => f.Reason).Select(g => $"{JobSummary.Shorten(g.Key, MaxReason)}: {string.Join(", ", g.Select(f => Who(f.Xuid)))}")];
    }

    /// <summary><paramref name="summary"/> with the failed accounts after it, cut to <see cref="JobSummary.MaxLength"/>.</summary>
    public string After(string summary)
        => _lines.Count == 0 ? summary : JobSummary.Shorten($"{summary}. Failed: {string.Join("; ", _lines)}", JobSummary.MaxLength);
}
