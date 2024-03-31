using Bogus;
using Bogus.DataSets;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
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

    public HostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        await context.Database.EnsureDeletedAsync(ct);
        await context.Database.MigrateAsync(ct);

        //var p = personFaker.Generate();

        var season = new Season()
        {
            SeasonName = "Test Season 2024",
            SignupsOpen = new DateTime(2024, 3, 1),
            SignupsClose = new DateTime(2024, 4, 15),
            DraftStart = new DateTime(2024, 3, 1),
            SeasonStart = new DateTime(2024, 3, 1),
            SeasonEnd = new DateTime(2024, 4, 15),
        };
        await context.Seasons.AddAsync(season, ct);

        var dummySysAdmin = new Database.Models.Person()
        {
            Name = "string",
            XboxUser = new XboxUser()
            {
                XboxUserID = 2535417961072277,
                Gamertag = "string"
            },
            Password = CreateDummyPassword(),
            PersonRoles = new HashSet<PersonRole>()
            {
                new ()
                {
                    RoleID = 1,
                }
            }
        };

        await context.Persons.AddAsync(dummySysAdmin);

        await context.SaveChangesAsync(ct);

        var xboxUserID = 1;
        var personFaker = new Faker<Database.Models.Person>()
            .RuleFor(p => p.Name, faker => faker.Name.FirstName())
            .RuleFor(p => p.Password, f => CreateDummyPassword())
            .RuleFor(p => p.PersonRoles, f => new List<PersonRole>()
            {
                new PersonRole()
                {
                    RoleID = 2,
                }
            })
            .RuleFor(p => p.XboxUser, f => new XboxUser()
            {
                XboxUserID = xboxUserID++,
                Gamertag = f.Name.FirstName(),
            });

        for (int i = 0; i < 6; i++)
        {
            var dummyCaptain = personFaker.Generate();
            await context.Persons.AddAsync(dummyCaptain);
            await context.SaveChangesAsync(ct);

            var signup = new SeasonSignup()
            {
                Season = season,
                Person = dummyCaptain,
                WillCaptain = true,
                TeamName = $"{dummyCaptain.Name}'s Team",
            };
            await context.SeasonSignups.AddAsync(signup);
            await context.SaveChangesAsync(ct);
        }


        for (int i = 0; i < 24; i++)
        {
            var dummyPlayer = personFaker.Generate();
            await context.Persons.AddAsync(dummyPlayer);
            await context.SaveChangesAsync(ct);

            var signup = new SeasonSignup()
            {
                Season = season,
                Person = dummyPlayer,
                WillCaptain = false,
            };
            await context.SeasonSignups.AddAsync(signup);
            await context.SaveChangesAsync(ct);
        }
    }

    private Password CreateDummyPassword()
    {
        return new Password()
        {
            Salt = "R3fE68fek82vNeGtezGUN9BGEXsPgrh8eTuPMOFVzlHzgxqU/VTaWfGEWHLJ+z65lyTx52AZFTtw2U545x7bwQ==",
            Hash = "HKcVv1tGRBjwyNXlKJZy/kT2USea8tAelKTkYWHlJfEd4JIVW0A95Behmerw+TFYZxDBDt8aJr9AD0iZNAlSdw=="
        };
    }
}
