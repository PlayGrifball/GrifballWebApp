using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Domain;

/// <summary>
/// How an <see cref="XboxUser"/> is loaded with its past gamertags (<see cref="XboxGamertagHistory"/>). They are recorded
/// and removed only through the account; the readers (the import's matching, profiles, leaderboards, search, claims) read
/// them untracked, through the context's <c>PastGamertags()</c> (AggregatePartQueries, in GrifballWebApp.Database). An
/// account whose past gamertags weren't loaded finds none of them, so every method of the account that changes them gets
/// an account loaded through <see cref="WithPastGamertags"/>.
/// </summary>
public static class XboxUserQueries
{
    /// <summary>The account with its past gamertags: all its gamertag methods need (<see cref="XboxUser.Renamed"/>,
    /// <see cref="XboxUser.RecordPastGamertag"/>, <see cref="XboxUser.RemovePastGamertag"/>).</summary>
    public static IQueryable<XboxUser> WithPastGamertags(this IQueryable<XboxUser> xboxUsers) => xboxUsers.Include(x => x.PastGamertags);
}
