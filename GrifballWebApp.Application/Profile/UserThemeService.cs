using System.Text.Json;
using System.Text.RegularExpressions;
using GrifballWebApp.Application.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Profile;

/// <summary>The colours and font a player chose for the site: the seed colours (#rgb or #rrggbb), the font, and the era
/// of the preset they started from (empty for none).</summary>
public class UserThemeDto
{
    public string Era { get; set; } = "";
    public string Font { get; set; } = "";
    public string Primary { get; set; } = "";
    public string Secondary { get; set; } = "";
    public string Tertiary { get; set; } = "";
    public string Neutral { get; set; } = "";
    public string NeutralVariant { get; set; } = "";
    public string Error { get; set; } = "";
}

public enum SetThemeResult { Set, Invalid, NoSuchUser }

/// <summary>A player's saved theme, which follows them to every device they sign in on.</summary>
public partial class UserThemeService
{
    /// <summary>The fonts the theme editor offers.</summary>
    public static readonly string[] Fonts = ["Rajdhani", "Roboto", "sans-serif"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;

    public UserThemeService(IApplicationDbContext context)
    {
        _context = context;
    }

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z")]
    private static partial Regex Hex();

    [GeneratedRegex("^[a-z0-9-]{0,32}\\z")]
    private static partial Regex Era();

    /// <summary>Whether every part of the theme is one the editor can make (a stored theme is never anything else).</summary>
    public static bool IsValid(UserThemeDto? theme)
        => theme is not null
           && Era().IsMatch(theme.Era ?? "")
           && Fonts.Contains(theme.Font)
           && new[] { theme.Primary, theme.Secondary, theme.Tertiary, theme.Neutral, theme.NeutralVariant, theme.Error }
               .All(c => c is not null && Hex().IsMatch(c));

    /// <summary>The user's saved theme; null when they have none (or there is no such user).</summary>
    public async Task<UserThemeDto?> Get(int userId, CancellationToken ct = default)
    {
        var stored = await _context.Users.Where(u => u.Id == userId).Select(u => u.Theme).FirstOrDefaultAsync(ct);
        if (stored is null)
            return null;
        try
        {
            var theme = JsonSerializer.Deserialize<UserThemeDto>(stored, Json);
            return IsValid(theme) ? theme : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Saves the user's theme; the last one saved is the one every device shows.</summary>
    public async Task<SetThemeResult> Set(int userId, UserThemeDto theme, CancellationToken ct = default)
    {
        if (!IsValid(theme))
            return SetThemeResult.Invalid;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return SetThemeResult.NoSuchUser;
        user.ChooseTheme(JsonSerializer.Serialize(theme, Json));
        await _context.SaveChangesAsync(ct);
        return SetThemeResult.Set;
    }
}
