using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Database;

/// <summary>
/// Contexts from this factory are audited like the scoped <see cref="GrifballContext"/>, but have no DI scope, so no
/// ICurrentUserService set by the caller: the audit user is the ambient one (<see cref="IAmbientCurrentUser"/>, set for
/// Discord interactions and messages), else the HTTP request's user.
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
        // A CurrentUserService per context, not a shared one: it caches the first user it finds in claims
        var currentUserService = new CurrentUserService(
            _serviceProvider.GetRequiredService<IHttpContextAccessor>(),
            _serviceProvider.GetRequiredService<IAmbientCurrentUser>());
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(_serviceProvider.GetRequiredService<IConfiguration>())
            .AddInterceptors(new AuditInterceptor(currentUserService))
            .Options;
        return new GrifballContext(options);
    }
}
