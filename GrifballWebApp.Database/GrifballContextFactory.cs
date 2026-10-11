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
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(_serviceProvider.GetRequiredService<IConfiguration>())
            .Options;
        return new GrifballContext(options);
    }
}
