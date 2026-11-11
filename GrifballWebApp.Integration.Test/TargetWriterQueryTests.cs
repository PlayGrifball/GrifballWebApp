using System.Data.Common;
using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.Data.Sqlite;

namespace GrifballWebApp.Integration.Test;

/// <summary>The conversion's target writers read their target's catalog through QueryAsync: a NULL comes back as null.</summary>
[TestFixture]
public class TargetWriterQueryTests
{
    [Test]
    public async Task QueryAsync_ReadsNullsAsNull_AndValuesAsValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var writer = new QueryingWriter(connection, transaction);

        var rows = await writer.Query("SELECT NULL, 1, 'text'");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0], Is.EqualTo(new object?[] { null, 1L, "text" }));
    }

    private sealed class QueryingWriter(DbConnection connection, DbTransaction transaction) : TargetWriter(connection, transaction)
    {
        public Task<List<object?[]>> Query(string sql) => QueryAsync(sql, CancellationToken.None);

        public override Task PrepareAsync(IReadOnlyList<TablePlan> tables, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task WriteAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task SetDeferredAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task FinishAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
