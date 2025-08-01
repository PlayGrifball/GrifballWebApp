﻿using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// Useful for when multiple accounts are mistakely made for the same person
/// </summary>
public interface IUserMergeService
{
    /// <summary>
    /// Useful for when multiple accounts are mistakely made for the same person
    /// </summary>
    /// <param name="mergeToId">The user to transfer owned entities to</param>
    /// <param name="mergeFromId">The user to take owned entities from, this account will be deleted</param>
    /// <param name="options">Options for the merge operation, such as whether to transfer Discord/Xbox IDs, team players, etc. Defaults to transfer all and delete merge from.</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task Merge(int mergeToId, int mergeFromId, MergeOptions? options = null, CancellationToken ct = default);
}

/// <inheritdoc/>
public class UserMergeService : IUserMergeService
{
    private readonly GrifballContext _context;
    public UserMergeService(GrifballContext context)
    {
        _context = context;
    }
    public async Task Merge(int mergeToId, int mergeFromId, MergeOptions? options = null, CancellationToken ct = default)
    {
        await _context.StartTransactionAsync(ct);
        options ??= new MergeOptions();

        var mergeTo = await _context.Users
            .Where(u => u.Id == mergeToId)
            .FirstOrDefaultAsync(ct) ?? throw new Exception("Did not find user " + mergeToId);

        var mergeFrom = await _context.Users
            .Where(u => u.Id == mergeFromId)
            .FirstOrDefaultAsync(ct) ?? throw new Exception("Did not find user " + mergeFromId);

        if (mergeFrom.DiscordUserID is not null && options.TransferDiscord)
        {
            mergeTo.DiscordUserID = mergeFrom.DiscordUserID;
        }

        if (mergeFrom.XboxUserID is not null && options.TransferXbox)
        {
            mergeTo.XboxUserID = mergeFrom.XboxUserID;
        }

        if (options.TransferTeamPlayers)
        {
            var teamPlayers = await _context.TeamPlayers.Where(tp => tp.UserID == mergeFromId).ToListAsync(ct);
            teamPlayers.ForEach(tp => tp.UserID = mergeToId);
        }
        
        if (options.TransferExperiences)
        {
            var experience = await _context.UserExperiences.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            experience.ForEach(x => x.UserID = mergeToId);
        }
        
        if (options.TransferSignups)
        {
            var signups = await _context.SeasonSignups.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            signups.ForEach(x => x.UserID = mergeToId);
        }

        // Delete just in case it doesnt cascade
        var rolesToDelete = await _context.UserRoles.Where(x => x.UserId == mergeFromId).ToArrayAsync(ct);
        _context.UserRoles.RemoveRange(rolesToDelete);
        //ExecuteDeleteAsync does not work in with in memory test
        //await _context.UserRoles.Where(x => x.UserId == existingUserID).ExecuteDeleteAsync(ct);

        // Transfer entities
        await _context.SaveChangesAsync(ct);

        // Delete user
        if (options.DeleteMergeFrom)
        {
            _context.Users.Remove(mergeFrom);
            await _context.SaveChangesAsync(ct);
        }

        await _context.CommitTransactionAsync(ct);
    }
}

public class MergeOptions
{
    public bool TransferDiscord { get; set; } = true;
    public bool TransferXbox { get; set; } = true;
    public bool TransferTeamPlayers { get; set; } = true;
    public bool TransferExperiences { get; set; } = true;
    public bool TransferSignups { get; set; } = true;
    public bool DeleteMergeFrom { get; set; } = true;
}
