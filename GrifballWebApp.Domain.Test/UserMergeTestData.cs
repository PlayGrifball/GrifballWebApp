using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>A merge as <see cref="UserMergeAggregateTests"/> and the database tests record it.</summary>
internal static class UserMergeTestData
{
    internal static readonly DateTime MergedAt = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static UserMerge Merge(int source = 1, int target = 2, string? reason = "claim 12", int moved = 3, int removed = 2, int added = 1)
        => UserMerge.Record(MergedAt, MergeTrigger.ClaimApproval, 7, reason, source, "historical:Old Tag", target, "real", "{}", moved, removed, added);
}
