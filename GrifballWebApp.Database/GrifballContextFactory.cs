using GrifballWebApp.Database.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Database;
public class GrifballContextFactory : IDbContextFactory<GrifballContext>
{
    private readonly IServiceProvider _serviceProvider;

    public GrifballContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public virtual GrifballContext CreateDbContext()
    {
        // AddDbContextFactory registers this factory as a singleton, so _serviceProvider is the root
        // provider. AuditInterceptor and ICurrentUserService are scoped, so they must be resolved from
        // a scope created here rather than from _serviceProvider directly, which would either throw or
        // capture a root-scope instance shared by every context this factory ever creates (see #28).
        using var scope = _serviceProvider.CreateScope();
        var auditInterceptor = scope.ServiceProvider.GetRequiredService<AuditInterceptor>();

        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(_serviceProvider.GetRequiredService<IConfiguration>())
            .AddInterceptors(auditInterceptor)
            .Options;
        return new GrifballContext(options);
    }
}
