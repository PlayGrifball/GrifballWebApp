using Microsoft.Data.SqlClient;

namespace GrifballWebApp.Integration.Test;

/// <summary>The backups' test data and logins: SQL Server logins as the chart makes them. The fake pg_dump is
/// GrifballWebApp.Infrastructure.Test's PgDumpTestData.FakePgDump.</summary>
internal static class BackupTestData
{
    /// <summary>A compressed .bak's first bytes (the test servers are Developer edition, so they compress); an uncompressed one starts TAPE.</summary>
    internal const string CompressedBakHeader = "MSSQ";

    /// <summary>
    /// SQL Server: a new login that reads and writes the database only, as the chart's app login without nativeBackups,
    /// and its connection string.
    /// </summary>
    internal static async Task<string> ReadOnlyLoginAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var login = $"grif_test_{Guid.NewGuid():N}";
        var password = $"Aa1!{Guid.NewGuid():N}";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"""
            CREATE LOGIN [{login}] WITH PASSWORD = N'{password}', CHECK_POLICY = OFF;
            CREATE USER [{login}] FOR LOGIN [{login}];
            ALTER ROLE db_datareader ADD MEMBER [{login}];
            ALTER ROLE db_datawriter ADD MEMBER [{login}];
            """, connection);
        await command.ExecuteNonQueryAsync();
        builder.UserID = login;
        builder.Password = password;
        builder.Pooling = false;
        return builder.ConnectionString;
    }

    /// <summary>
    /// SQL Server: a new login with what the chart's database.logins.app.nativeBackups gives the app login (reads and
    /// writes, db_backupoperator, ADMINISTER BULK OPERATIONS), and its connection string.
    /// </summary>
    internal static async Task<string> NativeBackupLoginAsync(string connectionString)
    {
        var login = await ReadOnlyLoginAsync(connectionString);
        var user = new SqlConnectionStringBuilder(login).UserID;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"""
            ALTER ROLE db_backupoperator ADD MEMBER [{user}];
            USE [master];
            GRANT ADMINISTER BULK OPERATIONS TO [{user}];
            """, connection);
        await command.ExecuteNonQueryAsync();
        return login;
    }
}
