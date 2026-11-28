using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

/// <summary>
/// The Xbox accounts a run of a per-account job (gamer pictures, emblems, armor) failed for, named in its summary on the Jobs page:
/// the counts say how many, these say whose and why. Read by gamertag in one query, only when the run had a failure.
/// </summary>
public sealed class FailedAccounts
{
    /// <summary>The longest summary a run's report gives, its accounts cut off with an ellipsis past it.</summary>
    public const int MaxSummary = 1000;

    private const int MaxReason = 120;

    private readonly List<(long Xuid, string Reason)> _failed = [];

    /// <summary>One line per account, "gamertag (xuid): why", once <see cref="NameAsync"/> has read the gamertags.</summary>
    public List<string> Lines { get; } = [];

    public void Add(long xuid, string reason) => _failed.Add((xuid, reason));

    /// <summary>The failed accounts' lines, by their gamertags (the XUID alone for an account without one).</summary>
    public async Task NameAsync(GrifballContext context, CancellationToken ct)
    {
        if (_failed.Count == 0)
            return;
        var xuids = _failed.Select(f => f.Xuid).Distinct().ToList();
        var gamertags = await context.XboxUsers.Where(x => xuids.Contains(x.XboxUserID))
            .Select(x => new { x.XboxUserID, x.Gamertag })
            .ToDictionaryAsync(x => x.XboxUserID, x => x.Gamertag, ct);
        foreach (var (xuid, reason) in _failed)
        {
            var who = gamertags.GetValueOrDefault(xuid) is { Length: > 0 } gamertag ? $"{gamertag} ({xuid})" : xuid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Lines.Add($"{who}: {Shorten(reason, MaxReason)}");
        }
    }

    /// <summary><paramref name="summary"/> with the failed accounts after it, cut to <see cref="MaxSummary"/>.</summary>
    public string After(string summary)
        => Lines.Count == 0 ? summary : Shorten($"{summary}. Failed: {string.Join("; ", Lines)}", MaxSummary);

    internal static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";
}
