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

        if (await context.Ranks.AnyAsync(ct))
            return;

        var ranks = new List<Rank>()
        {
            new Rank { Name = "Iron 2", MmrThreshold = 99, Color = "#4D5458", Icon = await LoadIcon("Iron2.png", ct), Description = "iron" },
            new Rank { Name = "Iron 1", MmrThreshold = 199, Color = "#4D5458", Icon = await LoadIcon("iron1.png", ct), Description = "iron" },
            new Rank { Name = "Bronze 3", MmrThreshold = 299, Color = "#CD7F32", Icon = await LoadIcon("Bronze3.png", ct), Description = "bronze" },
            new Rank { Name = "Bronze 2", MmrThreshold = 424, Color = "#CD7F32", Icon = await LoadIcon("Bronze2.png", ct), Description = "bronze" },
            new Rank { Name = "Bronze 1", MmrThreshold = 549, Color = "#CD7F32", Icon = await LoadIcon("Bronze1.png", ct), Description = "bronze" },
            new Rank { Name = "Silver 3", MmrThreshold = 699, Color = "#C0C0C0", Icon = await LoadIcon("Silver3.png", ct), Description = "silver" },
            new Rank { Name = "Silver 2", MmrThreshold = 849, Color = "#C0C0C0", Icon = await LoadIcon("Silver2.png", ct), Description = "silver" },
            new Rank { Name = "Silver 1", MmrThreshold = 999, Color = "#C0C0C0", Icon = await LoadIcon("Silver1.png", ct), Description = "silver" },
            new Rank { Name = "Gold 3", MmrThreshold = 1149, Color = "#FFD700", Icon = await LoadIcon("gold3.png", ct), Description = "gold" },
            new Rank { Name = "Gold 2", MmrThreshold = 1299, Color = "#FFD700", Icon = await LoadIcon("Gold2.png", ct), Description = "gold" },
            new Rank { Name = "Gold 1", MmrThreshold = 1449, Color = "#FFD700", Icon = await LoadIcon("Gold1.png", ct), Description = "gold" },
            new Rank { Name = "Platinum 3", MmrThreshold = 1649, Color = "#007f81", Icon = await LoadIcon("Platinum3.png", ct), Description = "platinum" },
            new Rank { Name = "Platinum 2", MmrThreshold = 1849, Color = "#007f81", Icon = await LoadIcon("Platinum2.png", ct), Description = "platinum" },
            new Rank { Name = "Platinum 1", MmrThreshold = 2049, Color = "#007f81", Icon = await LoadIcon("Platinum1.png", ct), Description = "platinum" },
            new Rank { Name = "Diamond 3", MmrThreshold = 2274, Color = "#445fa5", Icon = await LoadIcon("Diamond3.png", ct), Description = "diamond" },
            new Rank { Name = "Diamond 2", MmrThreshold = 2499, Color = "#445fa5", Icon = await LoadIcon("Diamond2.png", ct), Description = "diamond" },
            new Rank { Name = "Diamond 1", MmrThreshold = 2749, Color = "#445fa5", Icon = await LoadIcon("Diamond1.png", ct), Description = "diamond" },
            new Rank { Name = "Masters 3", MmrThreshold = 2999, Color = "#3BA55C", Icon = await LoadIcon("Masters3.png", ct), Description = "masters" },
            new Rank { Name = "Masters 2", MmrThreshold = 3249, Color = "#3BA55C", Icon = await LoadIcon("Masters2.png", ct), Description = "masters" },
            new Rank { Name = "Masters 1", MmrThreshold = 3499, Color = "#3BA55C", Icon = await LoadIcon("Masters1.png", ct), Description = "masters" },
            new Rank { Name = "Challenger", MmrThreshold = 3500, Color = "#3BA55C", Icon = await LoadIcon("Challenger.png", ct), Description = "challenger" }
        };

        context.Ranks.AddRange(ranks);
        await context.SaveChangesAsync();
    }

    private async Task<byte[]> LoadIcon(string iconPath, CancellationToken ct = default)
    {
        var folderPath = _configuration.GetValue<string>("Folder") ?? throw new Exception("Missing Folder in configuration cannot load icon");
        var fullPath = Path.Join(folderPath, iconPath);
        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    protected async Task Old(CancellationToken ct)
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

        var dummySysAdmin = new Database.Models.User()
        {
            DisplayName = "string",
            XboxUser = new XboxUser()
            {
                XboxUserID = 2535417961072277,
                Gamertag = "string"
            },
            //Password = CreateDummyPassword(),
            //PersonRoles = new HashSet<PersonRole>()
            //{
            //    new ()
            //    {
            //        RoleID = 1,
            //    }
            //}
        };

        await context.Users.AddAsync(dummySysAdmin);

        await context.SaveChangesAsync(ct);

        var xboxUserID = 1;
        var personFaker = new Faker<Database.Models.User>()
            .RuleFor(p => p.DisplayName, faker => faker.Name.FirstName())
            //.RuleFor(p => p.Password, f => CreateDummyPassword())
            //.RuleFor(p => p.PersonRoles, f => new List<PersonRole>()
            //{
            //    new PersonRole()
            //    {
            //        RoleID = 2,
            //    }
            //})
            .RuleFor(p => p.XboxUser, f => new XboxUser()
            {
                XboxUserID = xboxUserID++,
                Gamertag = f.Name.FirstName(),
            });

        for (int i = 0; i < 6; i++)
        {
            var dummyCaptain = personFaker.Generate();
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
            var dummyPlayer = personFaker.Generate();
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
