using Bogus;
using Bogus.DataSets;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        await context.Database.MigrateAsync(stoppingToken);

        // Seed here
        await new RankSeeder(context, new FileReader(_configuration)).SeedRanks(stoppingToken);
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
        context.Seasons.Add(season);

        var dummySysAdmin = Domain.Players.User.Register("seed-sysadmin", displayName: "string");
        dummySysAdmin.TieToXbox(XboxUser.FromXbox(2535417961072277, "string"));

        context.Users.Add(dummySysAdmin);

        await context.SaveChangesAsync(ct);

        var xboxUserID = 1;
        var faker = new Faker();
        Domain.Players.User NewPerson()
        {
            var person = Domain.Players.User.Register($"seed-{xboxUserID}", displayName: faker.Name.FirstName());
            person.TieToXbox(XboxUser.FromXbox(xboxUserID++, faker.Name.FirstName()));
            return person;
        }

        for (int i = 0; i < 6; i++)
        {
            var dummyCaptain = NewPerson();
            context.Users.Add(dummyCaptain);
            await context.SaveChangesAsync(ct);

            // Signed up as signups open
            var signup = SeasonSignup.SignUp(season, dummyCaptain.Id, season.SignupsOpen, new SignupChoices($"{dummyCaptain.DisplayName}'s Team", WillCaptain: true, RequiresAssistanceDrafting: false, Timeslots: [])).Signup!;
            context.SeasonSignups.Add(signup);
            await context.SaveChangesAsync(ct);
        }


        for (int i = 0; i < 24; i++)
        {
            var dummyPlayer = NewPerson();
            context.Users.Add(dummyPlayer);
            await context.SaveChangesAsync(ct);

            var signup = SeasonSignup.SignUp(season, dummyPlayer.Id, season.SignupsOpen, new SignupChoices(null, WillCaptain: false, RequiresAssistanceDrafting: false, Timeslots: [])).Signup!;
            context.SeasonSignups.Add(signup);
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
