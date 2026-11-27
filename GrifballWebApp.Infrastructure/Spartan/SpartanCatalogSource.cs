using GrifballWebApp.Application.Spartan;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayGrifball.InfiniteSpartanRender.Client;

namespace GrifballWebApp.Infrastructure.Spartan;

/// <summary>The render service's catalog, kept for <see cref="Lifetime"/> so a profile edit does not ask it every time.</summary>
public sealed class SpartanCatalogSource(InfiniteSpartanRenderClient client, IMemoryCache cache, ILogger<SpartanCatalogSource> logger)
    : ISpartanCatalogSource
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const string CacheKey = "spartan-render-catalog";

    public async Task<Application.Spartan.SpartanCatalog?> Get(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out Application.Spartan.SpartanCatalog? known))
            return known;
        try
        {
            var catalog = Map(await client.GetCatalogAsync(ct));
            cache.Set(CacheKey, catalog, Lifetime);
            return catalog;
        }
        catch (Exception e) when (e is HttpRequestException or SpartanRenderException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "The Spartan render service's catalog could not be read");
            return null;
        }
    }

    internal static Application.Spartan.SpartanCatalog Map(PlayGrifball.InfiniteSpartanRender.Client.SpartanCatalog catalog) => new(
        [.. catalog.Cores.Select(c => new Application.Spartan.SpartanCatalogCore(c.Name,
            [.. c.Regions.Select(r => new Application.Spartan.SpartanCatalogRegion(r.Kind, r.Key, r.Region,
                [.. r.Pieces.Select(p => new Application.Spartan.SpartanCatalogPiece(p.Permutation, p.Name))]))]))],
        [.. catalog.Coatings],
        [.. catalog.PendingCores],
        [.. catalog.Visors]);
}

public static class SpartanRenderServiceExtensions
{
    public const string BaseAddressKey = "SpartanRender:BaseAddress";

    /// <summary>The key the render service knows this app by (<c>Render:ApiKeys</c> there): its name shows on the service's traces and metrics. Unset: calls go unnamed.</summary>
    public const string ApiKeyKey = "SpartanRender:ApiKey";

    /// <summary>The render service's Service in the cluster it runs in; <c>SpartanRender:BaseAddress</c> overrides it.</summary>
#pragma warning disable S5332 // an in-cluster Service, which serves only HTTP
    public const string DefaultBaseAddress = "http://infinite-spartan-render.ekur-data.svc:8080/";
#pragma warning restore S5332

    public static IServiceCollection AddSpartanRender(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var address = configuration[BaseAddressKey];
        services.AddInfiniteSpartanRenderClient(o =>
        {
            o.BaseAddress = new Uri(string.IsNullOrWhiteSpace(address) ? DefaultBaseAddress : address.Trim());
            o.ApiKey = configuration[ApiKeyKey];
        });
        services.AddMemoryCache();
        services.AddScoped<ISpartanCatalogSource, SpartanCatalogSource>();
        return services;
    }
}
