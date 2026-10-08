using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

internal static class GrifballContextTestExtensions
{
    /// <summary>A new, independent context on the same per-test database (no shared change tracker).</summary>
    public static GrifballContext NewContextLike(this GrifballContext context)
    {
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseSqlServer(context.Database.GetConnectionString()).Options);
    }

    /// <summary>An <see cref="IDbContextFactory{TContext}"/> that hands out new contexts on the same database as <paramref name="context"/>.</summary>
    public static IDbContextFactory<GrifballContext> FactoryFor(GrifballContext context)
    {
        var factory = Substitute.For<IDbContextFactory<GrifballContext>>();
        factory.CreateDbContext().Returns(_ => context.NewContextLike());
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(context.NewContextLike()));
        return factory;
    }
}
