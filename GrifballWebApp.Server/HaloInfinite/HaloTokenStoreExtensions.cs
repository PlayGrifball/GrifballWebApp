using Microsoft.Extensions.DependencyInjection.Extensions;
using Surprenant.Grunt.Core.Storage;

namespace GrifballWebApp.Server.HaloInfinite;

public static class HaloTokenStoreExtensions
{
    /// <summary>
    /// Grunt's token storage in the database (<see cref="IHaloTokenStore"/>) instead of files in the working directory.
    /// Call before <c>RegisterHaloInfiniteClientFactory</c>, which only adds its file-based ones if none are registered.
    /// With <paramref name="ownsRefresh"/> this process signs in and refreshes the token (<see cref="HaloTokenRefreshService"/>);
    /// without, it only reads it.
    /// </summary>
    public static IServiceCollection AddHaloTokenStore(this IServiceCollection services, bool ownsRefresh)
    {
        services.AddSingleton(new HaloTokenOwnership(ownsRefresh));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IHaloTokenStore, HaloTokenStore>();
        services.AddSingleton<IXboxTokenClient, XboxTokenClient>();
        services.AddSingleton<IOAuthStorage, DatabaseOAuthStorage>();
        services.AddSingleton<IAccountAuthorization, DatabaseAccountAuthorization>();
        if (ownsRefresh)
            services.AddHostedService<HaloTokenRefreshService>();
        return services;
    }
}
