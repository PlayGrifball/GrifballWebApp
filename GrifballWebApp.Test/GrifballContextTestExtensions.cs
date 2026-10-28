using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace GrifballWebApp.Test;

internal static class GrifballContextTestExtensions
{
    /// <summary>A new, independent context on the same per-test database (no shared change tracker).</summary>
    public static GrifballContext NewContextLike(this GrifballContext context, params IInterceptor[] interceptors)
    {
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, context.Database.GetConnectionString()!)
            .AddInterceptors(interceptors)
            .Options);
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> that hands out new contexts on the same database as <paramref name="context"/>.
    /// Like GrifballContextFactory's, they are not audited, and an <see cref="UnauditedWriteGuard"/> fails any write of an audited entity through them.
    /// </summary>
    public static IDbContextFactory<GrifballContext> FactoryFor(GrifballContext context)
    {
        var factory = Substitute.For<IDbContextFactory<GrifballContext>>();
        factory.CreateDbContext().Returns(_ => context.NewContextLike(new UnauditedWriteGuard()));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(context.NewContextLike(new UnauditedWriteGuard())));
        return factory;
    }
}
