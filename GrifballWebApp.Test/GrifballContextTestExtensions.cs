using GrifballWebApp.Database;
using GrifballWebApp.Database.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Test;

internal static class GrifballContextTestExtensions
{
    /// <summary>A new, independent context on the same per-test database (no shared change tracker).</summary>
    public static GrifballContext NewContextLike(this GrifballContext context)
    {
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, context.Database.GetConnectionString()!).Options);
    }

    /// <summary>The interceptors <paramref name="context"/> was configured with.</summary>
    public static IEnumerable<IInterceptor> Interceptors(this GrifballContext context)
    {
        // DbContext implements IInfrastructure<IServiceProvider> explicitly, so EF's internal service provider (and from it, the
        // options) is only reachable through that interface.
        var internalProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        return internalProvider.GetService<IDbContextOptions>()?.FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];
    }

    /// <summary>
    /// The real <see cref="GrifballContextFactory"/>, registered like Program.cs, on the same database as <paramref name="context"/>:
    /// its contexts are audited, with the ambient user.
    /// </summary>
    public static IDbContextFactory<GrifballContext> FactoryFor(GrifballContext context)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GrifballWebApp"] = context.Database.GetConnectionString(),
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddHttpContextAccessor()
            .AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>()
            .BuildServiceProvider();
        return new GrifballContextFactory(services);
    }
}
