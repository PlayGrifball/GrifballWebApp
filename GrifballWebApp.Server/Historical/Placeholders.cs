using System.Linq.Expressions;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// The one definition of an import placeholder: a dummy user the historical import stands in for a gamertag with,
/// until it is claimed (<see cref="GamertagClaimService"/>) or matched to the Xbox account that holds the gamertag now
/// (<see cref="XuidLookupService"/>). Never a real account: Identity's allowed user name characters have no colon or
/// space, so no account made by signing up can ever collide with one (<see cref="HistoricalKeys.DummyUserNamePrefix"/>).
/// </summary>
public static class Placeholders
{
    /// <summary>An import placeholder not yet tied to an Xbox account, as an expression a query can translate.</summary>
    public static readonly Expression<Func<User, bool>> IsImportPlaceholderExpression = u =>
        u.IsDummyUser && u.XboxUserID == null && u.UserName != null && u.UserName.StartsWith(HistoricalKeys.DummyUserNamePrefix);

    /// <summary>A loaded user: an import placeholder not yet tied to an Xbox account. Inside a query lambda, use
    /// <see cref="IsImportPlaceholderExpression"/> instead, so it translates to SQL.</summary>
    public static bool IsImportPlaceholder(User user)
        => user.IsDummyUser && user.XboxUserID is null
            && user.UserName is not null && user.UserName.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The gamertag an import placeholder's user name is for: "historical:Name" is "Name", and so is
    /// "historical:Name:123" (an Xbox account's own placeholder, named after its XUID too when the gamertag's plain name
    /// is already taken, <see cref="GamertagMatching.CreatePlaceholderAsync"/>). Null for anyone else, whose user name is
    /// their login name, never shown. Case-insensitive on the prefix.</summary>
    public static string? GamertagOf(string? userName)
        => userName is not null && userName.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.OrdinalIgnoreCase)
            ? userName[HistoricalKeys.DummyUserNamePrefix.Length..].Split(':')[0]
            : null;
}
