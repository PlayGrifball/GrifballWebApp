using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// Where a personal access token works, chosen when it is made and never changed after (a new token instead of
/// changing one's). A token made before this existed has none stored, which deserializes as <see cref="Mcp"/>: what
/// every token did before scopes existed.
/// </summary>
public enum AccessTokenScope
{
    /// <summary>The MCP endpoint only.</summary>
    Mcp = 0,
    /// <summary>The REST API only.</summary>
    Api = 1,
    /// <summary>Both the REST API and the MCP endpoint.</summary>
    Both = 2,
}

/// <summary>Whether a scope opens a surface, for <see cref="AccessTokenAuthentication"/>'s check of the one the request is on.</summary>
public static class AccessTokenScopes
{
    public static bool AllowsMcp(this AccessTokenScope scope) => scope is AccessTokenScope.Mcp or AccessTokenScope.Both;
    public static bool AllowsApi(this AccessTokenScope scope) => scope is AccessTokenScope.Api or AccessTokenScope.Both;
}

/// <summary>A personal access token, as its owner sees it in their list: its scope and whether it is read-only, never its secret.</summary>
public sealed record AccessTokenDto(string ID, string Name, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, AccessTokenScope Scope, bool ReadOnly);

/// <summary>A token just made: the token itself is shown this once, and only its hash is kept.</summary>
public sealed record CreatedAccessTokenDto(AccessTokenDto AccessToken, string Token);

/// <summary>
/// A request signed in by an access token: the user it acts as, and the token's own ID, name, scope and read-only flag
/// (never its secret), for the scheme's surface check (<see cref="AccessTokenAuthentication"/>) and the request's logs.
/// </summary>
public sealed record AuthenticatedAccessToken(User User, string ID, string Name, AccessTokenScope Scope, bool ReadOnly);

/// <summary>
/// What to make a token for: a name to tell it by, how many days it lasts, where it works and whether it is read-only.
/// <see cref="Scope"/> and <see cref="ReadOnly"/> default to what every token was before they existed (MCP only, not
/// read-only), though the site always sends them: a token's scope can't be changed later, only remade.
/// </summary>
public sealed record CreateAccessTokenRequest(string Name, int Days = AccessTokenService.DefaultDays,
    AccessTokenScope Scope = AccessTokenScope.Mcp, bool ReadOnly = false);

/// <summary>
/// Personal access tokens, which sign a client (an MCP client such as Claude Code, or a script calling the REST API) in as the
/// user who made them, with that user's roles, on the surface its scope allows (<see cref="AccessTokenAuthentication"/>). A
/// token is <c>grif_</c>, its ID, <c>_</c> and its secret. Kept in Identity's user tokens (Auth.UserTokens), one row per token
/// under <see cref="LoginProvider"/>, named by its ID and holding the secret's SHA-256 with its name, scope, read-only flag and
/// dates: the secret itself is never stored.
/// </summary>
public class AccessTokenService
{
    public const string LoginProvider = "Grifball.AccessToken";
    public const string Prefix = "grif_";
    public const int DefaultDays = 90;
    public const int MaxDays = 365;
    public const int MaxNameLength = 100;
    public const int MaxTokensPerUser = 10;

    private const int IdLength = 32;
    private const int SecretBytes = 32;

    private readonly GrifballContext _context;
    private readonly TimeProvider _time;

    public AccessTokenService(GrifballContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    /// <summary>
    /// What a token's row holds: its secret's hash, its name, its dates, its scope and whether it is read-only. A row
    /// made before these existed has neither in its JSON, which deserializes them as the defaults below: no migration
    /// of the stored rows is needed.
    /// </summary>
    private sealed record Stored(string Hash, string Name, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
        AccessTokenScope Scope = AccessTokenScope.Mcp, bool ReadOnly = false);

    private static string HashOf(string secret) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static bool SecretMatches(string secret, string hash)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(HashOf(secret)), Encoding.ASCII.GetBytes(hash));

    private IQueryable<IdentityUserToken<int>> TokensOf(int userID)
        => _context.UserTokens.Where(t => t.UserId == userID && t.LoginProvider == LoginProvider);

    private static AccessTokenDto ToDto(string id, Stored stored) => new(id, stored.Name, stored.CreatedAt, stored.ExpiresAt, stored.Scope, stored.ReadOnly);

    /// <summary>A new token for the user; a refusal's reason instead when the request breaks a rule.</summary>
    public async Task<(CreatedAccessTokenDto? Created, string? Refusal)> Create(int userID, CreateAccessTokenRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength)
            return (null, $"A token needs a name of 1 to {MaxNameLength} characters.");
        if (request.Days is < 1 or > MaxDays)
            return (null, $"A token lasts 1 to {MaxDays} days.");
        if (!Enum.IsDefined(request.Scope))
            return (null, "Scope must be Mcp, Api or Both.");
        if (await TokensOf(userID).CountAsync(ct) >= MaxTokensPerUser)
            return (null, $"You have {MaxTokensPerUser} tokens already: revoke one first.");

        var id = Guid.NewGuid().ToString("N");
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var now = _time.GetUtcNow();
        var stored = new Stored(HashOf(secret), name, now, now.AddDays(request.Days), request.Scope, request.ReadOnly);
        _context.UserTokens.Add(new IdentityUserToken<int>
        {
            UserId = userID,
            LoginProvider = LoginProvider,
            Name = id,
            Value = JsonSerializer.Serialize(stored),
        });
        await _context.SaveChangesAsync(ct);
        return (new CreatedAccessTokenDto(ToDto(id, stored), $"{Prefix}{id}_{secret}"), null);
    }

    /// <summary>The user's tokens, newest first, expired ones included until they are revoked.</summary>
    public async Task<List<AccessTokenDto>> List(int userID, CancellationToken ct = default)
    {
        var rows = await TokensOf(userID).AsNoTracking().Select(t => new { t.Name, t.Value }).ToListAsync(ct);
        return rows
            .Select(r => ToDto(r.Name, JsonSerializer.Deserialize<Stored>(r.Value!)!))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    /// <summary>Deletes one of the user's tokens; false when they have none with that ID. <see cref="Authenticate"/>
    /// makes no copy of the row anywhere, so this takes effect at once, on every pod, with nothing left to evict.</summary>
    public async Task<bool> Revoke(int userID, string id, CancellationToken ct = default)
        => await TokensOf(userID).Where(t => t.Name == id).ExecuteDeleteAsync(ct) > 0;

    /// <summary>
    /// The token's owner and its own facts, when it is well formed, known, unexpired and its secret matches; else null.
    /// No caching: every call is this one query (a join of the token row and its user), so a revoke on any pod takes
    /// effect on the very next call, everywhere, with nothing to race or go stale.
    /// </summary>
    public async Task<AuthenticatedAccessToken?> Authenticate(string token, CancellationToken ct = default)
    {
        // grif_<32 hex>_<secret>: the secret is base64url, which may hold '_' too, so the ID is cut by its length
        if (!token.StartsWith(Prefix, StringComparison.Ordinal) || token.Length <= Prefix.Length + IdLength + 1 || token[Prefix.Length + IdLength] != '_')
            return null;
        var id = token.Substring(Prefix.Length, IdLength);
        var secret = token[(Prefix.Length + IdLength + 1)..];

        var row = await _context.UserTokens.AsNoTracking()
            .Where(t => t.LoginProvider == LoginProvider && t.Name == id)
            .Join(_context.Users.AsNoTracking(), t => t.UserId, u => u.Id, (t, u) => new { t.Value, User = u })
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;

        var stored = JsonSerializer.Deserialize<Stored>(row.Value!)!;
        if (!SecretMatches(secret, stored.Hash) || stored.ExpiresAt <= _time.GetUtcNow())
            return null;

        return new AuthenticatedAccessToken(row.User, id, stored.Name, stored.Scope, stored.ReadOnly);
    }
}
