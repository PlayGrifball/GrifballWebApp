using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A link a sysadmin gives a user to set their password. Only the token's hash is kept: the token itself is only in the
/// link given out, so a backup or a read of the table can't reset anyone's password.
/// <list type="bullet">
/// <item>issued (<see cref="Issue"/>): usable until it expires, <see cref="Lifetime"/> later;</item>
/// <item>used (<see cref="Use"/>): once, and only before it expires;</item>
/// <item>expired: when its time is up, or early when a new link replaces it (<see cref="Expire"/>).</item>
/// </list>
/// A used link was used before it expired (CK_Auth_PasswordResetLinks_UsedBeforeExpiry). Spent links (used or expired) are
/// deleted by the cleanup job (<see cref="Spent"/>).
/// </summary>
[AggregateRoot]
public class PasswordResetLink : AuditableEntity
{
    public const int TokenMaxLength = 255;

    /// <summary>How long a link is usable after it is issued.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>For EF. A link begins through <see cref="Issue"/>.</summary>
    private PasswordResetLink() { }

    /// <summary>
    /// A new link for <paramref name="userId"/>, issued by <paramref name="issuedByUserId"/> at <paramref name="now"/>:
    /// a cryptographically random token (64 hex characters), of which the link keeps only the hash. The token is
    /// returned for the link given out, and is nowhere else.
    /// </summary>
    public static (PasswordResetLink Link, string Token) Issue(int userId, int issuedByUserId, DateTime now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var link = new PasswordResetLink
        {
            UserId = userId,
            Token = HashToken(token),
            ExpiresAt = now + Lifetime,
        };
        // Its issuer, as the audit interceptor writes it when the sysadmin saves it: through IAuditable, the audit
        // columns' only way in
        IAuditable audit = link;
        (audit.CreatedByID, audit.ModifiedByID) = (issuedByUserId, issuedByUserId);
        return (link, token);
    }

    /// <summary>
    /// A link of any token, expiry and use, past the rules: for tests, which seed links as each move leaves them (and old
    /// rows for the migration tests).
    /// </summary>
    internal static PasswordResetLink Seed(int userId, string token, DateTime expiresAt, DateTime? usedAt = null) => new()
    {
        UserId = userId,
        Token = HashToken(token),
        ExpiresAt = expiresAt,
        UsedAt = usedAt,
    };

    /// <summary>What a link keeps of its token: its SHA-256, hex.</summary>
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Nothing outside the link sets its fields, its foreign key or its navigation: only its own methods below, and EF
    // (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int Id { get; internal set; }
    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>The SHA-256 of the link's token, hex (<see cref="HashToken"/>); the token itself is only in the link given out.</summary>
    public string Token { get; private set; } = null!;

    public DateTime ExpiresAt { get; private set; }

    /// <summary>When it was used, before it expired; null while unused.</summary>
    public DateTime? UsedAt { get; private set; }

    /// <summary>The link of <paramref name="token"/> (the link given out), found by the token's hash.</summary>
    public static Expression<Func<PasswordResetLink, bool>> OfToken(string token)
    {
        var hash = HashToken(token);
        return l => l.Token == hash;
    }

    /// <summary>Unused and not expired at <paramref name="now"/> (<see cref="IsUsableAt"/> for a loaded link).</summary>
    public static Expression<Func<PasswordResetLink, bool>> Usable(DateTime now) => l => l.UsedAt == null && l.ExpiresAt > now;

    /// <summary>Used, or expired at <paramref name="now"/>: of no more use, so the cleanup deletes it.</summary>
    public static Expression<Func<PasswordResetLink, bool>> Spent(DateTime now) => l => l.UsedAt != null || l.ExpiresAt <= now;

    /// <summary>Whether it can still be used at <paramref name="now"/> (<see cref="Usable"/> for queries).</summary>
    public bool IsUsableAt(DateTime now) => CanUse(now) == PasswordResetLinkUse.Ok;

    /// <summary>Whether it can be used at <paramref name="now"/>, or why not: a used link is used, whether expired or not.</summary>
    public PasswordResetLinkUse CanUse(DateTime now)
        => UsedAt is not null ? PasswordResetLinkUse.AlreadyUsed
            : ExpiresAt <= now ? PasswordResetLinkUse.Expired
            : PasswordResetLinkUse.Ok;

    /// <summary>
    /// Used at <paramref name="now"/>: once, and only before it expires. Refused (the link unchanged) when it can't be
    /// (<see cref="CanUse"/>), with why.
    /// </summary>
    public PasswordResetLinkUse Use(DateTime now)
    {
        var use = CanUse(now);
        if (use == PasswordResetLinkUse.Ok)
            UsedAt = now;
        return use;
    }

    /// <summary>Expired at <paramref name="now"/>, before its time: a new link replaces it. A used or expired link stays as it is.</summary>
    public void Expire(DateTime now)
    {
        if (IsUsableAt(now))
            ExpiresAt = now;
    }
}

/// <summary>Whether a <see cref="PasswordResetLink"/> can be used, or why not.</summary>
public enum PasswordResetLinkUse
{
    /// <summary>Usable (from <see cref="PasswordResetLink.CanUse"/>), or used now (from <see cref="PasswordResetLink.Use"/>).</summary>
    Ok,
    AlreadyUsed,
    Expired,
}
