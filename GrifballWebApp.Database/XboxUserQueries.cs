using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

/// <summary>
/// How an <see cref="XboxUser"/>'s past gamertags (<see cref="XboxGamertagHistory"/>) are read. They are recorded and removed
/// only through the account; the readers (the import's matching, profiles, leaderboards, search, claims) read them here,
/// untracked. An account whose past gamertags weren't loaded finds none of them, so every method of the account that changes
/// them gets an account loaded through <see cref="WithPastGamertags"/>.
/// </summary>
public static class XboxUserQueries
{
    /// <summary>Every account's past gamertags, to read.</summary>
    public static IQueryable<XboxGamertagHistory> PastGamertags(this GrifballContext context) => context.XboxGamertagHistory.AsNoTracking();

    /// <summary>The account with its past gamertags: all its gamertag methods need (<see cref="XboxUser.Renamed"/>,
    /// <see cref="XboxUser.RecordPastGamertag"/>, <see cref="XboxUser.RemovePastGamertag"/>).</summary>
    public static IQueryable<XboxUser> WithPastGamertags(this IQueryable<XboxUser> xboxUsers) => xboxUsers.Include(x => x.PastGamertags);
}
