using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>The BackfillPlayerRole migration, on whichever database the suite runs on.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackfillPlayerRoleMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp()
    {
        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
            Assert.Ignore("A migration of SQL Server's and Postgres's: SQLite's migrations start after it, from one baseline.");
        _context = await SetUpFixture.NewGrifballContext();
    }

    // Not after SetUp ignored the test (SQLite), which made no database
    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    /// <summary>
    /// Inserts a user as the database is at this migration: only the columns its Users table has yet (later migrations
    /// add columns the model already has, e.g. RegionSource). Returns the new user's ID.
    /// </summary>
    private async Task<int> InsertAsOfNow(User user)
    {
        var entity = _context.Model.FindEntityType(typeof(User))!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var columns = connection.CreateCommand())
        {
            columns.CommandText = $"SELECT column_name FROM information_schema.columns WHERE table_schema = '{entity.GetSchema()}' AND table_name = '{entity.GetTableName()}'";
            await using var reader = await columns.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                existing.Add(reader.GetString(0));
        }
        var entry = _context.Entry(user);
        var properties = entity.GetProperties()
            .Where(p => !p.IsPrimaryKey() && !p.IsShadowProperty() && p.GetColumnName(table) is string c && existing.Contains(c))
            .ToList();
        var sqlServer = TestDatabase.Provider == DatabaseProvider.SqlServer;
        string Quote(string name) => sqlServer ? $"[{name}]" : $"\"{name}\"";
        await using var insert = connection.CreateCommand();
        var names = string.Join(", ", properties.Select(p => Quote(p.GetColumnName(table)!)));
        var values = string.Join(", ", properties.Select((_, i) => $"@p{i}"));
        insert.CommandText = sqlServer
            ? $"INSERT INTO {Quote(entity.GetSchema()!)}.{Quote(entity.GetTableName()!)} ({names}) OUTPUT INSERTED.[Id] VALUES ({values})"
            : $"INSERT INTO {Quote(entity.GetSchema()!)}.{Quote(entity.GetTableName()!)} ({names}) VALUES ({values}) RETURNING \"Id\"";
        for (var i = 0; i < properties.Count; i++)
        {
            var parameter = insert.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            var value = entry.Property(properties[i].Name).CurrentValue;
            if (value is DateTime { Kind: DateTimeKind.Unspecified } unspecified)
                value = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
            var converter = properties[i].GetTypeMapping().Converter;
            parameter.Value = (converter is null || value is null ? value : converter.ConvertToProvider(value)) ?? DBNull.Value;
            // SqlClient sends a DateTime as datetime, which can't hold default(DateTime): the columns are datetime2
            if (sqlServer && parameter.Value is DateTime)
                parameter.DbType = System.Data.DbType.DateTime2;
            insert.Parameters.Add(parameter);
        }
        return System.Convert.ToInt32(await insert.ExecuteScalarAsync());
    }

    [Test]
    public async Task AccountsWithoutAnyRole_BecomePlayers_OthersAreLeftAlone()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var backfill = migrations.IndexOf(migrations.Single(m => m.EndsWith("_BackfillPlayerRole")));
        await migrator.MigrateAsync(migrations[backfill - 1]);

        var noRoles = User.Seed(userName: "botmade");
        var dummy = User.Seed(userName: "dummy", isDummyUser: true);
        var commissioner = User.Seed(userName: "commish");
        foreach (var user in new[] { noRoles, dummy, commissioner })
            user.Id = await InsertAsOfNow(user);
        // UserRoles has no columns newer than this migration: the model can write it
        _context.UserRoles.Add(new UserRole { UserId = commissioner.Id, RoleId = 2 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var roles = await _context.UserRoles
            .Select(x => new { x.UserId, x.Role.Name })
            .ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(roles.Where(x => x.UserId == noRoles.Id).Select(x => x.Name), Is.EqualTo(new[] { "Player" }));
            Assert.That(roles.Where(x => x.UserId == dummy.Id), Is.Empty, "Placeholder accounts don't sign in");
            Assert.That(roles.Where(x => x.UserId == commissioner.Id).Select(x => x.Name), Is.EqualTo(new[] { "Commissioner" }), "Accounts with a role keep just theirs");
        });
    }
}
