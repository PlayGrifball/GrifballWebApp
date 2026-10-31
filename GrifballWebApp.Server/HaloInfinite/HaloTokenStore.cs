using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>The account's OAuth token as stored, with when its access token expires (null: unknown).</summary>
public sealed record StoredOAuthToken(OAuthToken Token, DateTime? ExpiresAt);

/// <summary>
/// The Halo Infinite sign-in's secrets (Grunt's tokens.json and AccountAuthorization.txt), in the database
/// (<see cref="HaloInfiniteCredential"/>) rather than in the working directory, so that any number of backend processes
/// share them and none needs a volume. Encrypted with ASP.NET Data Protection, whose keys are in the database too.
/// </summary>
public interface IHaloTokenStore
{
    Task<StoredOAuthToken?> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="token"/>; its expiry is its <c>expires_in</c> from now, unless <paramref name="expiryKnown"/> is false.</summary>
    Task SetTokenAsync(OAuthToken token, bool expiryKnown = true, CancellationToken cancellationToken = default);

    Task SetCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The authorization code, removed as it is read: of processes taking it at once, one gets it and the others null
    /// (a code can be exchanged for a token only once). Null too if there is none.
    /// </summary>
    Task<string?> TakeCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes the token and any code: the account must be authorized again.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the legacy files (tokens.json, AccountAuthorization.txt) have been imported (<see cref="HaloTokenRefreshService"/>).</summary>
    Task<bool> LegacyFilesImportedAsync(CancellationToken cancellationToken = default);

    Task MarkLegacyFilesImportedAsync(CancellationToken cancellationToken = default);
}

public sealed class HaloTokenStore : IHaloTokenStore
{
    /// <summary>The Data Protection purpose: only this store can read what it wrote.</summary>
    public const string Purpose = "GrifballWebApp.HaloInfinite.Credentials";

    /// <summary>Marks the one-time import of the legacy files; not a secret, and not removed by <see cref="ClearAsync"/>.</summary>
    internal const string LegacyImportName = "LegacyFilesImported";

    private readonly IDbContextFactory<GrifballContext> _contexts;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;
    private readonly ILogger<HaloTokenStore> _logger;

    public HaloTokenStore(IDbContextFactory<GrifballContext> contexts, IDataProtectionProvider dataProtection, TimeProvider time, ILogger<HaloTokenStore> logger)
    {
        _contexts = contexts;
        _protector = dataProtection.CreateProtector(Purpose);
        _time = time;
        _logger = logger;
    }

    public async Task<StoredOAuthToken?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(HaloInfiniteCredential.OAuthTokenName, cancellationToken);
        if (row is null || Unprotect(row) is not { } json)
            return null;
        var token = JsonSerializer.Deserialize<OAuthToken>(json);
        return token is null ? null : new StoredOAuthToken(token, row.ExpiresAt);
    }

    public Task SetTokenAsync(OAuthToken token, bool expiryKnown = true, CancellationToken cancellationToken = default)
    {
        DateTime? expiresAt = expiryKnown && token.ExpiresIn > 0
            ? _time.GetUtcNow().UtcDateTime.AddSeconds(token.ExpiresIn)
            : null;
        return UpsertAsync(HaloInfiniteCredential.OAuthTokenName, JsonSerializer.Serialize(token), expiresAt, cancellationToken);
    }

    public Task SetCodeAsync(string code, CancellationToken cancellationToken = default)
        => UpsertAsync(HaloInfiniteCredential.AuthorizationCodeName, code, null, cancellationToken);

    public async Task<string?> TakeCodeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var row = await context.HaloInfiniteCredentials.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == HaloInfiniteCredential.AuthorizationCodeName, cancellationToken);
        if (row is null)
            return null;

        // Only the process whose delete removed this very value has taken it.
        var deleted = await context.HaloInfiniteCredentials
            .Where(c => c.Name == HaloInfiniteCredential.AuthorizationCodeName && c.ProtectedValue == row.ProtectedValue)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1 ? Unprotect(row) : null;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        await context.HaloInfiniteCredentials
            .Where(c => c.Name == HaloInfiniteCredential.OAuthTokenName || c.Name == HaloInfiniteCredential.AuthorizationCodeName)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> LegacyFilesImportedAsync(CancellationToken cancellationToken = default)
        => await FindAsync(LegacyImportName, cancellationToken) is not null;

    public Task MarkLegacyFilesImportedAsync(CancellationToken cancellationToken = default)
        => UpsertAsync(LegacyImportName, _time.GetUtcNow().ToString("O"), null, cancellationToken);

    private async Task<HaloInfiniteCredential?> FindAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        return await context.HaloInfiniteCredentials.AsNoTracking().SingleOrDefaultAsync(c => c.Name == name, cancellationToken);
    }

    private async Task UpsertAsync(string name, string value, DateTime? expiresAt, CancellationToken cancellationToken)
    {
        var protectedValue = _protector.Protect(value);
        var now = _time.GetUtcNow().UtcDateTime;

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        if (await UpdateAsync() > 0)
            return;

        context.HaloInfiniteCredentials.Add(new HaloInfiniteCredential { Name = name, ProtectedValue = protectedValue, ExpiresAt = expiresAt, UpdatedAt = now });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another process inserted it first: this value is the newer one.
            context.ChangeTracker.Clear();
            if (await UpdateAsync() == 0)
                throw;
        }

        Task<int> UpdateAsync() => context.HaloInfiniteCredentials
            .Where(c => c.Name == name)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ProtectedValue, protectedValue)
                .SetProperty(c => c.ExpiresAt, expiresAt)
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);
    }

    private string? Unprotect(HaloInfiniteCredential row)
    {
        try
        {
            return _protector.Unprotect(row.ProtectedValue);
        }
        catch (CryptographicException ex)
        {
            // The key it was written with is gone (the key ring was reset): as if there were none.
            _logger.LogError(ex, "Could not decrypt the stored Halo Infinite {Name}; the account must be authorized again", row.Name);
            return null;
        }
    }
}
