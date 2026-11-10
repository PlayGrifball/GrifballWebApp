using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Dtos;

namespace GrifballWebApp.Application.Profile;

public class UserMergeDto
{
    public int UserMergeID { get; set; }
    public DateTime MergedAt { get; set; }
    public required string Trigger { get; set; }
    public string? Reason { get; set; }
    public int? TriggeredByUserID { get; set; }
    public string? TriggeredByUserName { get; set; }
    public int SourceUserID { get; set; }
    public string? SourceUserName { get; set; }
    public int TargetUserID { get; set; }
    public string? TargetUserName { get; set; }
    public int RowsMoved { get; set; }
    public int RowsRemoved { get; set; }
    public int RowsAdded { get; set; }
    public DateTime? UndoneAt { get; set; }
    public int? UndoneByUserID { get; set; }
    public string? UndoNote { get; set; }
}

/// <summary>A merge and what it changed, table by table.</summary>
public class UserMergeDetailDto : UserMergeDto
{
    public List<MergeTableSummaryDto> Tables { get; set; } = [];
}

public class MergeTableSummaryDto
{
    public required string Table { get; set; }
    public int Moved { get; set; }
    public int Removed { get; set; }
    public int Added { get; set; }
}

public class UndoMergeRequest
{
    [System.ComponentModel.DataAnnotations.MaxLength(UserMerge.UndoNoteMaxLength)]
    public string? Note { get; set; }
}

/// <summary>The merges' audit records (<see cref="UserMerge"/>) for sysadmins, and undoing one.</summary>
public class UserMergeAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    private readonly ICurrentUserService? _currentUser;

    public UserMergeAuditService(IApplicationDbContext context, TimeProvider time, ICurrentUserService? currentUser = null)
    {
        _context = context;
        _time = time;
        _currentUser = currentUser;
    }

    /// <summary>Newest first.</summary>
    public async Task<PaginationResult<UserMergeDto>> List(PaginationFilter filter, CancellationToken ct = default)
    {
        var query = _context.UserMerges.AsNoTracking();
        var total = await query.CountAsync(ct);
        var page = await Project(query.OrderByDescending(m => m.UserMergeID)
                .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize))
            .ToArrayAsync(ct);
        return new PaginationResult<UserMergeDto> { TotalCount = total, Results = page };
    }

    public async Task<UserMergeDetailDto?> Get(int mergeId, CancellationToken ct = default)
    {
        var merge = await _context.UserMerges.AsNoTracking().FirstOrDefaultAsync(m => m.UserMergeID == mergeId, ct);
        if (merge is null)
            return null;
        var summary = (await Project(_context.UserMerges.AsNoTracking().Where(m => m.UserMergeID == mergeId)).FirstAsync(ct));
        var detail = new UserMergeDetailDto
        {
            UserMergeID = summary.UserMergeID,
            MergedAt = summary.MergedAt,
            Trigger = summary.Trigger,
            Reason = summary.Reason,
            TriggeredByUserID = summary.TriggeredByUserID,
            TriggeredByUserName = summary.TriggeredByUserName,
            SourceUserID = summary.SourceUserID,
            SourceUserName = summary.SourceUserName,
            TargetUserID = summary.TargetUserID,
            TargetUserName = summary.TargetUserName,
            RowsMoved = summary.RowsMoved,
            RowsRemoved = summary.RowsRemoved,
            RowsAdded = summary.RowsAdded,
            UndoneAt = summary.UndoneAt,
            UndoneByUserID = summary.UndoneByUserID,
            UndoNote = summary.UndoNote,
        };
        detail.Tables = MergeManifest.Parse(merge.Manifest).Entries.GroupBy(e => e.Table).OrderBy(g => g.Key)
            .Select(g => new MergeTableSummaryDto
            {
                Table = g.Key,
                Moved = g.Count(e => e.Op == MergeManifestOp.Modified),
                Removed = g.Count(e => e.Op == MergeManifestOp.Deleted),
                Added = g.Count(e => e.Op == MergeManifestOp.Added),
            }).ToList();
        return detail;
    }

    private IQueryable<UserMergeDto> Project(IQueryable<UserMerge> merges) => merges.Select(m => new UserMergeDto
    {
        UserMergeID = m.UserMergeID,
        MergedAt = m.MergedAt,
        Trigger = m.Trigger.ToString(),
        Reason = m.Reason,
        TriggeredByUserID = m.TriggeredByUserID,
        TriggeredByUserName = _context.Users.Where(u => u.Id == m.TriggeredByUserID).Select(u => u.UserName).FirstOrDefault(),
        SourceUserID = m.SourceUserID,
        SourceUserName = m.SourceUserName,
        TargetUserID = m.TargetUserID,
        TargetUserName = m.TargetUserName,
        RowsMoved = m.RowsMoved,
        RowsRemoved = m.RowsRemoved,
        RowsAdded = m.RowsAdded,
        UndoneAt = m.UndoneAt,
        UndoneByUserID = m.UndoneByUserID,
        UndoNote = m.UndoNote,
    });

    /// <summary>
    /// Puts back what the merge changed (<see cref="MergeManifestPlayer"/>), in one transaction, and marks it undone.
    /// Refused (<see cref="MergeUndoRefusedException"/>, nothing changed) when it was undone already, when a later merge
    /// not undone touched the same rows (undo that one first), or when a row was changed since, is gone or its key taken.
    /// </summary>
    public async Task Undo(int mergeId, string? note, CancellationToken ct = default)
    {
        var merge = await _context.UserMerges.AsNoTracking().FirstOrDefaultAsync(m => m.UserMergeID == mergeId, ct)
            ?? throw new MergeUndoRefusedException($"There is no merge {mergeId}");
        if (!merge.IsStanding)
            throw new MergeUndoRefusedException($"Merge {mergeId} was undone already");
        var manifest = MergeManifest.Parse(merge.Manifest);
        var rows = manifest.RowKeys();

        // A later merge into or out of either user that touched the same rows depends on this one
        var later = await _context.UserMerges.AsNoTracking()
            .Where(merge.LaterStandingMergesOfItsUsers())
            .OrderByDescending(m => m.UserMergeID)
            .ToListAsync(ct);
        foreach (var other in later)
        {
            if (MergeManifest.Parse(other.Manifest).RowKeys().Overlaps(rows))
                throw new MergeUndoRefusedException($"Merge {other.UserMergeID} ({other.SourceUserName} into {other.TargetUserName}) since changed the same rows: undo it first.");
        }

        _context.ChangeTracker.Clear();
        await _context.StartTransactionAsync(ct);
        try
        {
            await new MergeManifestPlayer(_context).UndoAsync(manifest, ct);
            var record = await _context.UserMerges.FirstAsync(m => m.UserMergeID == mergeId, ct);
            // Undone by someone else since it was read: refused, and nothing is put back
            if (record.Undo(_time.GetUtcNow().UtcDateTime, _currentUser?.GetCurrentUserId(), note) == UserMergeUndo.AlreadyUndone)
                throw new MergeUndoRefusedException($"Merge {mergeId} was undone already");
            await _context.SaveChangesAsync(ct);
            await _context.CommitTransactionAsync(ct);
        }
        catch (Exception e)
        {
            // Nothing is left half undone
            await _context.RollbackTransactionAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            if (e is DbUpdateException)
                throw new MergeUndoRefusedException("Can't undo: a row could not be put back as it was (" + e.GetBaseException().Message + ")");
            throw;
        }
    }
}
