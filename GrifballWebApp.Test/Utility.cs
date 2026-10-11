using DotNet.Testcontainers.Containers;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Test;
internal static class Utility
{
    internal static async Task<GrifballContext> NewGrifballContext(IDatabaseContainer server, params IInterceptor[] interceptors)
    {
        // Create a unique database name per test
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        await TestDatabase.CreateDatabase(server, dbName);
        var options = TestDatabase.Options(TestDatabase.ConnectionString(server, dbName))
            .AddInterceptors(interceptors)
            .Options;

        var context = new GrifballContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    internal static async Task DropDatabaseAndDispose(this GrifballContext context)
    {
        // Disposed first: Postgres's drop ends the database's connections, which the context may still be
        // closing.
        var cs = context.Database.GetConnectionString()!;
        await context.DisposeAsync();
        _ = Task.Run(() => TestDatabase.DropDatabase(cs));
    }
}
