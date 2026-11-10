using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Integration.Test;

internal static class IdentityTestHelpers
{
    /// <summary>A real UserManager over the test database, as the app registers it (EF Core stores on GrifballContext).</summary>
    internal static UserManager<User> NewUserManager(GrifballContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddIdentityCore<User>()
            .AddRoles<Role>()
            .AddEntityFrameworkStores<GrifballContext>();
        return services.BuildServiceProvider().GetRequiredService<UserManager<User>>();
    }
}
