using System.Text.RegularExpressions;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

/// <summary>Halo's errors, fit for a log line or a job's run history.</summary>
public static partial class HaloErrors
{
    /// <summary>The longest part of Halo's error kept.</summary>
    public const int MaxLength = 300;

    /// <summary>
    /// Halo's error message: an HTML error page (its edge's 504, passed on by the gateway) cut to its title, anything else to
    /// <see cref="MaxLength"/> characters; "no error message" without one.
    /// </summary>
    public static string Describe(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "no error message";
        if (message.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            var title = Title().Match(message).Groups[1].Value.Trim();
            return $"an HTML error page ({(title.Length > 0 ? title : "no title")})";
        }
        return message.Length > MaxLength ? message[..MaxLength] + "…" : message;
    }

    [GeneratedRegex("<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex Title();
}
