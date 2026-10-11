using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrifballWebApp.Seeder;

internal class Program
{
    static void Main(string[] args)
    {
        Host.CreateDefaultBuilder(args).ConfigureServices((hostContext, services) =>
        {
            services.AddDbContext<GrifballContext>((services, options) => options.UseGrifballDatabase(services.GetRequiredService<IConfiguration>()));
            services.AddHostedService<HostedService>();
        }).Build().Run();
    }
}
