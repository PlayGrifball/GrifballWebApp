using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Shared;
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

        var ranks = await BuildRanksAsync(ct);

        _context.Ranks.AddRange(ranks);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Every rank the seeder writes, lowest first, with the icon file each loads. iron1.png and gold3.png are
    /// lowercase, unlike the rest: on Linux the icon folder's files have to match that case exactly.</summary>
    internal static readonly IReadOnlyList<RankDefinition> Ranks =
    [
        new("Iron 2", 99, "#4D5458", "iron", "Iron2.png"),
        new("Iron 1", 199, "#4D5458", "iron", "iron1.png"),
        new("Bronze 3", 299, "#CD7F32", "bronze", "Bronze3.png"),
        new("Bronze 2", 424, "#CD7F32", "bronze", "Bronze2.png"),
        new("Bronze 1", 549, "#CD7F32", "bronze", "Bronze1.png"),
        new("Silver 3", 699, "#C0C0C0", "silver", "Silver3.png"),
        new("Silver 2", 849, "#C0C0C0", "silver", "Silver2.png"),
        new("Silver 1", 999, "#C0C0C0", "silver", "Silver1.png"),
        new("Gold 3", 1149, "#FFD700", "gold", "gold3.png"),
        new("Gold 2", 1299, "#FFD700", "gold", "Gold2.png"),
        new("Gold 1", 1449, "#FFD700", "gold", "Gold1.png"),
        new("Platinum 3", 1649, "#007f81", "platinum", "Platinum3.png"),
        new("Platinum 2", 1849, "#007f81", "platinum", "Platinum2.png"),
        new("Platinum 1", 2049, "#007f81", "platinum", "Platinum1.png"),
        new("Diamond 3", 2274, "#445fa5", "diamond", "Diamond3.png"),
        new("Diamond 2", 2499, "#445fa5", "diamond", "Diamond2.png"),
        new("Diamond 1", 2749, "#445fa5", "diamond", "Diamond1.png"),
        new("Masters 3", 2999, "#3BA55C", "masters", "Masters3.png"),
        new("Masters 2", 3249, "#3BA55C", "masters", "Masters2.png"),
        new("Masters 1", 3499, "#3BA55C", "masters", "Masters1.png"),
        new("Challenger", 3500, "#3BA55C", "challenger", "Challenger.png"),
    ];

    /// <summary>The ranks, with their icons loaded through <see cref="_loader"/> all at once</summary>
    internal async Task<List<Rank>> BuildRanksAsync(CancellationToken ct = default)
    {
        var icons = await Task.WhenAll(Ranks.Select(rank => _loader.LoadIcon(rank.IconFile, ct)));
        return [.. Ranks.Select((rank, i) => Rank.Create(rank.Name, rank.MmrThreshold, rank.Color, rank.Description, icons[i]))];
    }
}

internal sealed record RankDefinition(string Name, int MmrThreshold, string Color, string Description, string IconFile);

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
