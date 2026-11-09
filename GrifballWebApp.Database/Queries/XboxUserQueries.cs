using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database.Queries;

/// <summary>
/// How an <see cref="XboxUser"/> and its past gamertags (<see cref="XboxGamertagHistory"/>) are read. They are recorded
/// and removed only through the account; the readers (the import's matching, profiles, leaderboards, search, claims) read
/// them untracked through <see cref="PastGamertags"/>. An account whose past gamertags weren't loaded finds none of them, so
/// every method of the account that changes them gets an account loaded through <see cref="WithPastGamertags"/>.
/// </summary>
public static class XboxUserQueries
{
    /// <summary>The account with its past gamertags: all its gamertag methods need (<see cref="XboxUser.Renamed"/>,
    /// <see cref="XboxUser.RecordPastGamertag"/>, <see cref="XboxUser.RemovePastGamertag"/>).</summary>
    public static IQueryable<XboxUser> WithPastGamertags(this IQueryable<XboxUser> xboxUsers) => xboxUsers.Include(x => x.PastGamertags);

    /// <summary>Every account's past gamertags, to read: <c>context.XboxUsers.PastGamertags()</c>.</summary>
    public static IQueryable<XboxGamertagHistory> PastGamertags(this IQueryable<XboxUser> xboxUsers) => xboxUsers.SelectMany(x => x.PastGamertags).AsNoTracking();
}
