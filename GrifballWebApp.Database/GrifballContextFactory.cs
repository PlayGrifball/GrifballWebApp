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
            .UseSqlServer(_serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("GrifballWebApp")
            ?? throw new Exception("GrifballContext failed to configure"))
            .Options;
        return new GrifballContext(options);
    }
}
