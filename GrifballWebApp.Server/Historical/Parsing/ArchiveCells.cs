using System.Globalization;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>Reading the archive's cells: numbers, durations and gamertags with their notes.</summary>
public static partial class ArchiveCells
{
    /// <summary>A number, or null for empty, "-" and errors (#N/A, #DIV/0!). Thousands separators are ignored.</summary>
    public static decimal? Number(string? value)
    {
        var text = value?.Replace(",", "").Trim();
        if (string.IsNullOrEmpty(text) || text is "-" or "#N/A" or "#VALUE!" or "#DIV/0!" or "#REF!")
            return null;
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>The number a cell starts with ("42.21" of "42.21 min"), like JavaScript's parseFloat.</summary>
    public static decimal? LeadingNumber(string? value)
    {
        var match = LeadingNumberPattern().Match(value?.Trim() ?? "");
        return match.Success && decimal.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>Seconds from "h:mm:ss", "m:ss", a number of seconds, or a number of minutes when the header says minutes.</summary>
    public static decimal? DurationSeconds(string? value, string header)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (header.Contains("minute", StringComparison.OrdinalIgnoreCase) && !text.Contains(':'))
            return LeadingNumber(text) is decimal minutes ? Math.Round(minutes * 60, MidpointRounding.AwayFromZero) : null;
        if (text.Contains(':'))
        {
            var parts = text.Split(':').Select(p => LeadingNumber(p) ?? 0).ToArray();
            if (parts.Length == 3)
                return Math.Round(parts[0] * 3600 + parts[1] * 60 + parts[2], MidpointRounding.AwayFromZero);
            if (parts.Length == 2)
                return Math.Round(parts[0] * 60 + parts[1], MidpointRounding.AwayFromZero);
        }
        return LeadingNumber(text) is decimal seconds ? Math.Round(seconds, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>An average life in seconds: ":25", or a number.</summary>
    public static decimal? AverageLifeSeconds(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        return text.StartsWith(':') ? LeadingNumber(text[1..]) : LeadingNumber(text);
    }

    /// <summary>"Rayzyr (c) (to Tuna)" is Rayzyr, with the trade note "to Tuna" and the role note "c".</summary>
    public static (string Gamertag, string? TradeNote, string? RoleNote) Gamertag(string? raw)
    {
        var text = raw?.Trim() ?? "";
        string? trade = null;
        var tradeMatch = TradePattern().Match(text);
        if (tradeMatch.Success)
        {
            trade = tradeMatch.Value.Trim('(', ')').Trim();
            text = text.Remove(tradeMatch.Index, tradeMatch.Length).Trim();
        }
        string? role = null;
        var roleMatch = RolePattern().Match(text);
        if (roleMatch.Success)
        {
            role = roleMatch.Groups[1].Value.ToLowerInvariant();
            text = text.Remove(roleMatch.Index, roleMatch.Length).Trim();
        }
        return (TrailingPattern().Replace(text, "").Trim(), trade, role);
    }

    /// <summary>A header compared loosely: lower case, without spaces ("Games Played" is "gamesplayed").</summary>
    public static string HeaderKey(string header) => new(header.Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());

    [GeneratedRegex(@"^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?")]
    private static partial Regex LeadingNumberPattern();

    [GeneratedRegex(@"\((?:to|from)\s+[^)]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex TradePattern();

    [GeneratedRegex(@"\(([casub]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex RolePattern();

    [GeneratedRegex(@"[\s,]+$")]
    private static partial Regex TrailingPattern();
}
