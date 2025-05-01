using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Seeder;
public class RankSeeder
{
    private readonly GrifballContext _context;
    private readonly IIconLoader _loader;
    public RankSeeder(GrifballContext context, IIconLoader loader)
    {
        _context = context;
        _loader = loader;
    }

    public async Task SeedRanks(CancellationToken ct = default)
    {
        if (await _context.Ranks.AnyAsync(ct))
            return;

        var ranks = new List<Rank>()
        {
            new Rank { Name = "Iron 2", MmrThreshold = 99, Color = "#4D5458", Icon = await _loader.LoadIcon("Iron2.png", ct), Description = "iron" },
            new Rank { Name = "Iron 1", MmrThreshold = 199, Color = "#4D5458", Icon = await _loader.LoadIcon("iron1.png", ct), Description = "iron" },
            new Rank { Name = "Bronze 3", MmrThreshold = 299, Color = "#CD7F32", Icon = await _loader.LoadIcon("Bronze3.png", ct), Description = "bronze" },
            new Rank { Name = "Bronze 2", MmrThreshold = 424, Color = "#CD7F32", Icon = await _loader.LoadIcon("Bronze2.png", ct), Description = "bronze" },
            new Rank { Name = "Bronze 1", MmrThreshold = 549, Color = "#CD7F32", Icon = await _loader.LoadIcon("Bronze1.png", ct), Description = "bronze" },
            new Rank { Name = "Silver 3", MmrThreshold = 699, Color = "#C0C0C0", Icon = await _loader.LoadIcon("Silver3.png", ct), Description = "silver" },
            new Rank { Name = "Silver 2", MmrThreshold = 849, Color = "#C0C0C0", Icon = await _loader.LoadIcon("Silver2.png", ct), Description = "silver" },
            new Rank { Name = "Silver 1", MmrThreshold = 999, Color = "#C0C0C0", Icon = await _loader.LoadIcon("Silver1.png", ct), Description = "silver" },
            new Rank { Name = "Gold 3", MmrThreshold = 1149, Color = "#FFD700", Icon = await _loader.LoadIcon("gold3.png", ct), Description = "gold" },
            new Rank { Name = "Gold 2", MmrThreshold = 1299, Color = "#FFD700", Icon = await _loader.LoadIcon("Gold2.png", ct), Description = "gold" },
            new Rank { Name = "Gold 1", MmrThreshold = 1449, Color = "#FFD700", Icon = await _loader.LoadIcon("Gold1.png", ct), Description = "gold" },
            new Rank { Name = "Platinum 3", MmrThreshold = 1649, Color = "#007f81", Icon = await _loader.LoadIcon("Platinum3.png", ct), Description = "platinum" },
            new Rank { Name = "Platinum 2", MmrThreshold = 1849, Color = "#007f81", Icon = await _loader.LoadIcon("Platinum2.png", ct), Description = "platinum" },
            new Rank { Name = "Platinum 1", MmrThreshold = 2049, Color = "#007f81", Icon = await _loader.LoadIcon("Platinum1.png", ct), Description = "platinum" },
            new Rank { Name = "Diamond 3", MmrThreshold = 2274, Color = "#445fa5", Icon = await _loader.LoadIcon("Diamond3.png", ct), Description = "diamond" },
            new Rank { Name = "Diamond 2", MmrThreshold = 2499, Color = "#445fa5", Icon = await _loader.LoadIcon("Diamond2.png", ct), Description = "diamond" },
            new Rank { Name = "Diamond 1", MmrThreshold = 2749, Color = "#445fa5", Icon = await _loader.LoadIcon("Diamond1.png", ct), Description = "diamond" },
            new Rank { Name = "Masters 3", MmrThreshold = 2999, Color = "#3BA55C", Icon = await _loader.LoadIcon("Masters3.png", ct), Description = "masters" },
            new Rank { Name = "Masters 2", MmrThreshold = 3249, Color = "#3BA55C", Icon = await _loader.LoadIcon("Masters2.png", ct), Description = "masters" },
            new Rank { Name = "Masters 1", MmrThreshold = 3499, Color = "#3BA55C", Icon = await _loader.LoadIcon("Masters1.png", ct), Description = "masters" },
            new Rank { Name = "Challenger", MmrThreshold = 3500, Color = "#3BA55C", Icon = await _loader.LoadIcon("Challenger.png", ct), Description = "challenger" }
        };

        _context.Ranks.AddRange(ranks);
        await _context.SaveChangesAsync(ct);
    }

    
}

public interface IIconLoader
{
    Task<byte[]> LoadIcon(string iconPath, CancellationToken ct = default);
}

public class TestReader : IIconLoader
{
    public async Task<byte[]> LoadIcon(string iconPath, CancellationToken ct = default)
    {
        var base64 = "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";
        return await Task.FromResult(Convert.FromBase64String(base64)); // Dummy data for testing
    }
}

public class FileReader : IIconLoader
{
    private readonly IConfiguration _configuration;
    public FileReader(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public async Task<byte[]> LoadIcon(string iconPath, CancellationToken ct = default)
    {
        var folderPath = _configuration.GetValue<string>("Folder") ?? throw new Exception("Missing Folder in configuration cannot load icon");
        var fullPath = Path.Join(folderPath, iconPath);
        return await File.ReadAllBytesAsync(fullPath, ct);
    }
}
