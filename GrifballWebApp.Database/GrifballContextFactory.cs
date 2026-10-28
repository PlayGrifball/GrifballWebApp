using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Database;

/// <summary>
/// Contexts from this factory have no AuditInterceptor, so they are not audited: use them for reads only.
/// Code that writes should resolve the scoped <see cref="GrifballContext"/> from a DI scope instead, with the
/// current user set on that scope's ICurrentUserService (see MessageCreateHandler).
/// </summary>
public class GrifballContextFactory : IDbContextFactory<GrifballContext>
{
    private readonly IServiceProvider _serviceProvider;

    public GrifballContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public virtual GrifballContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(_serviceProvider.GetRequiredService<IConfiguration>())
            .Options;
        return new GrifballContext(options);
    }
}
