using Bogus;
using Bogus.DataSets;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrifballWebApp.Seeder;
internal class HostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public HostedService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        await context.Database.MigrateAsync(ct);

        // Seed here
        await new RankSeeder(context, new FileReader(_configuration)).SeedRanks(ct);
    }

    

    protected async Task Old(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        await context.Database.EnsureDeletedAsync(ct);
        await context.Database.MigrateAsync(ct);

        //var p = NewPerson();

        var season = Season.Organized("Test Season 2024", League.DefaultLeagueID, gameVersionID: null);
        season.Schedule(signupsOpen: new DateTime(2024, 3, 1), signupsClose: new DateTime(2024, 4, 15), draftStart: new DateTime(2024, 3, 1),
            seasonStart: new DateTime(2024, 3, 1), seasonEnd: new DateTime(2024, 4, 15));
        await context.Seasons.AddAsync(season, ct);

        var dummySysAdmin = Database.Models.User.Register("seed-sysadmin", displayName: "string");
        dummySysAdmin.TieToXbox(new XboxUser()
        {
            XboxUserID = 2535417961072277,
            Gamertag = "string"
        });

        await context.Users.AddAsync(dummySysAdmin);

        await context.SaveChangesAsync(ct);

        var xboxUserID = 1;
        var faker = new Faker();
        Database.Models.User NewPerson()
        {
            var person = Database.Models.User.Register($"seed-{xboxUserID}", displayName: faker.Name.FirstName());
            person.TieToXbox(new XboxUser()
            {
                XboxUserID = xboxUserID++,
                Gamertag = faker.Name.FirstName(),
            });
            return person;
        }

        for (int i = 0; i < 6; i++)
        {
            var dummyCaptain = NewPerson();
            await context.Users.AddAsync(dummyCaptain);
            await context.SaveChangesAsync(ct);

            var signup = new SeasonSignup()
            {
                Season = season,
                User = dummyCaptain,
                WillCaptain = true,
                TeamName = $"{dummyCaptain.DisplayName}'s Team",
            };
            await context.SeasonSignups.AddAsync(signup);
            await context.SaveChangesAsync(ct);
        }


        for (int i = 0; i < 24; i++)
        {
            var dummyPlayer = NewPerson();
            await context.Users.AddAsync(dummyPlayer);
            await context.SaveChangesAsync(ct);

            var signup = new SeasonSignup()
            {
                Season = season,
                User = dummyPlayer,
                WillCaptain = false,
            };
            await context.SeasonSignups.AddAsync(signup);
            await context.SaveChangesAsync(ct);
        }
    }

    //private Password CreateDummyPassword()
    //{
    //    return new Password()
    //    {
    //        Salt = "R3fE68fek82vNeGtezGUN9BGEXsPgrh8eTuPMOFVzlHzgxqU/VTaWfGEWHLJ+z65lyTx52AZFTtw2U545x7bwQ==",
    //        Hash = "HKcVv1tGRBjwyNXlKJZy/kT2USea8tAelKTkYWHlJfEd4JIVW0A95Behmerw+TFYZxDBDt8aJr9AD0iZNAlSdw=="
    //    };
    //}
}
