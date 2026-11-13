using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Mcp;

/// <summary>A personal access token, as its owner sees it in their list: never its secret.</summary>
public sealed record AccessTokenDto(string ID, string Name, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>A token just made: the token itself is shown this once, and only its hash is kept.</summary>
public sealed record CreatedAccessTokenDto(AccessTokenDto AccessToken, string Token);

/// <summary>What to make a token for: a name to tell it by, and how many days it lasts.</summary>
public sealed record CreateAccessTokenRequest(string Name, int Days = AccessTokenService.DefaultDays);

/// <summary>
/// Personal access tokens, which sign an MCP client (Claude Code, say) in as the user who made them, with that user's roles,
/// on the MCP endpoint only (<see cref="AccessTokenAuthentication"/>). A token is <c>grif_</c>, its ID, <c>_</c> and its secret.
/// Kept in Identity's user tokens (Auth.UserTokens), one row per token under <see cref="LoginProvider"/>, named by its ID and
/// holding the secret's SHA-256 with its name and dates: the secret itself is never stored.
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

    /// <summary>What a token's row holds: its secret's hash, its name and its dates.</summary>
    private sealed record Stored(string Hash, string Name, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

    private static string Hash(string secret) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private IQueryable<IdentityUserToken<int>> TokensOf(int userID)
        => _context.UserTokens.Where(t => t.UserId == userID && t.LoginProvider == LoginProvider);

    private static AccessTokenDto ToDto(string id, Stored stored) => new(id, stored.Name, stored.CreatedAt, stored.ExpiresAt);

    /// <summary>A new token for the user; a refusal's reason instead when the request breaks a rule.</summary>
    public async Task<(CreatedAccessTokenDto? Created, string? Refusal)> Create(int userID, CreateAccessTokenRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength)
            return (null, $"A token needs a name of 1 to {MaxNameLength} characters.");
        if (request.Days is < 1 or > MaxDays)
            return (null, $"A token lasts 1 to {MaxDays} days.");
        if (await TokensOf(userID).CountAsync(ct) >= MaxTokensPerUser)
            return (null, $"You have {MaxTokensPerUser} tokens already: revoke one first.");

        var id = Guid.NewGuid().ToString("N");
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var now = _time.GetUtcNow();
        var stored = new Stored(Hash(secret), name, now, now.AddDays(request.Days));
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

    /// <summary>Deletes one of the user's tokens; false when they have none with that ID.</summary>
    public async Task<bool> Revoke(int userID, string id, CancellationToken ct = default)
        => await TokensOf(userID).Where(t => t.Name == id).ExecuteDeleteAsync(ct) > 0;

    /// <summary>The user a token signs in, when it is well formed, known, unexpired and its secret matches; else null.</summary>
    public async Task<User?> FindUser(string token, CancellationToken ct = default)
    {
        // grif_<32 hex>_<secret>: the secret is base64url, which may hold '_' too, so the ID is cut by its length
        if (!token.StartsWith(Prefix, StringComparison.Ordinal) || token.Length <= Prefix.Length + IdLength + 1 || token[Prefix.Length + IdLength] != '_')
            return null;
        var id = token.Substring(Prefix.Length, IdLength);
        var secret = token[(Prefix.Length + IdLength + 1)..];

        var row = await _context.UserTokens.AsNoTracking()
            .Where(t => t.LoginProvider == LoginProvider && t.Name == id)
            .Select(t => new { t.UserId, t.Value })
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;

        var stored = JsonSerializer.Deserialize<Stored>(row.Value!)!;
        var matches = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(stored.Hash));
        if (!matches || stored.ExpiresAt <= _time.GetUtcNow())
            return null;

        return await _context.Users.FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
    }
}
