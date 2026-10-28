using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class GrifballContextFactoryTests
{
    // Registers AuditInterceptor's own dependency chain (ICurrentUserService, IHttpContextAccessor)
    // scoped, like Program.cs, so GrifballContextFactory.CreateDbContext's scope resolves it.
    private static IServiceProvider Provider(string? connectionString, string? provider = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:GrifballWebApp"] = connectionString, ["Database:Provider"] = provider }).Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddHttpContextAccessor()
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .AddScoped<AuditInterceptor>()
            .BuildServiceProvider();
    }

    [Test]
    public void CreateDbContext_UsesConfiguredConnectionString()
    {
        const string cs = "Server=example;Database=Grif;Trusted_Connection=True;";

        using var context = new GrifballContextFactory(Provider(cs)).CreateDbContext();

        var actual = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Multiple(() =>
        {
            Assert.That(actual.DataSource, Is.EqualTo("example"));
            Assert.That(actual.InitialCatalog, Is.EqualTo("Grif"));
        });
    }

    [Test]
    public void CreateDbContext_Postgres_UsesNpgsql()
    {
        const string cs = "Host=example;Database=Grif;Username=grif;Password=x;";

        using var context = new GrifballContextFactory(Provider(cs, "Postgres")).CreateDbContext();

        var actual = new Npgsql.NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Multiple(() =>
        {
            Assert.That(context.Database.IsNpgsql(), Is.True);
            Assert.That(actual.Host, Is.EqualTo("example"));
            Assert.That(actual.Database, Is.EqualTo("Grif"));
        });
    }

    [Test]
    public void CreateDbContext_MissingConnectionString_Throws()
    {
        var ex = Assert.Throws<Exception>(() => new GrifballContextFactory(Provider(null)).CreateDbContext());

        Assert.That(ex!.Message, Is.EqualTo("GrifballContext failed to configure"));
    }

    // DbContext implements IInfrastructure<IServiceProvider> explicitly, so the EF internal service
    // provider (and from it, the configured interceptors) is only reachable through that interface.
    private static IEnumerable<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>? InterceptorsOf(GrifballContext context)
    {
        var internalProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        return internalProvider.GetService<IDbContextOptions>()?.FindExtension<CoreOptionsExtension>()?.Interceptors;
    }

    [Test]
    public void CreateDbContext_AttachesAuditInterceptor()
    {
        const string cs = "Server=example;Database=Grif;Trusted_Connection=True;";

        using var context = new GrifballContextFactory(Provider(cs)).CreateDbContext();

        Assert.That(InterceptorsOf(context)?.OfType<AuditInterceptor>().Any(), Is.True,
            "Contexts the factory creates (used by Discord handlers) must be audited the same as request-scoped contexts, see #26/#28");
    }

    [Test]
    public void CreateDbContext_CalledTwice_ResolvesInterceptorFromItsOwnScope()
    {
        // Each call must get its own scope, not a captive AuditInterceptor shared by every context
        // this (singleton-registered) factory ever creates.
        var provider = Provider("Server=example;Database=Grif;Trusted_Connection=True;");
        var factory = new GrifballContextFactory(provider);

        using var context1 = factory.CreateDbContext();
        using var context2 = factory.CreateDbContext();

        var interceptor1 = InterceptorsOf(context1)!.OfType<AuditInterceptor>().Single();
        var interceptor2 = InterceptorsOf(context2)!.OfType<AuditInterceptor>().Single();

        Assert.That(interceptor1, Is.Not.SameAs(interceptor2));
    }
}
