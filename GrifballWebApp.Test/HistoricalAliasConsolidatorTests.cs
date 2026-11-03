using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalAliasConsolidatorTests
{
    private GrifballContext _context = null!;
    private IUserMergeService _merges = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _merges = new UserMergeService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task ConsolidateAsync_MergesPlaceholderIntoExistingCanonicalRegisteredUser()
    {
        var xboxUser = new XboxUser { XboxUserID = 123456789, Gamertag = "MenZ" };
        _context.XboxUsers.Add(xboxUser);

        var canonicalUser = new User
        {
            UserName = "registered_menz",
            NormalizedUserName = "REGISTERED_MENZ",
            DisplayName = "MenZ",
            IsDummyUser = false,
            XboxUser = xboxUser
        };
        var variantUser = new User
        {
            UserName = "historical:Its Rainin MenZ",
            NormalizedUserName = "HISTORICAL:ITS RAININ MENZ",
            DisplayName = "Its Rainin MenZ",
            IsDummyUser = true
        };
        _context.AddRange(canonicalUser, variantUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var merged = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);
        Assert.That(merged, Is.EqualTo(1));

        _context.ChangeTracker.Clear();
        var remainingUsers = await _context.Users.ToListAsync();
        Assert.That(remainingUsers, Has.Count.EqualTo(1));
        Assert.That(remainingUsers[0].Id, Is.EqualTo(canonicalUser.Id));
        Assert.That(remainingUsers[0].XboxUserID, Is.EqualTo(xboxUser.XboxUserID));
    }

    [Test]
    public async Task ConsolidateAsync_RenamesPlaceholder_WhenNoCanonicalAccountExists()
    {
        var variantUser = new User
        {
            UserName = "historical:Major Silva",
            NormalizedUserName = "HISTORICAL:MAJOR SILVA",
            DisplayName = "Major Silva",
            IsDummyUser = true
        };
        _context.Users.Add(variantUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var merged = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);
        Assert.That(merged, Is.EqualTo(1));

        _context.ChangeTracker.Clear();
        var reloaded = await _context.Users.FindAsync(variantUser.Id);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.DisplayName, Is.EqualTo("Silva"));
        Assert.That(reloaded.UserName, Is.EqualTo("historical:Silva"));
        Assert.That(reloaded.NormalizedUserName, Is.EqualTo("HISTORICAL:SILVA"));
    }

    [Test]
    public async Task ConsolidateAsync_MergesPlaceholderIntoExistingCanonicalPlaceholder()
    {
        var canonicalPlaceholder = new User
        {
            UserName = "historical:MenZ",
            NormalizedUserName = "HISTORICAL:MENZ",
            DisplayName = "MenZ",
            IsDummyUser = true
        };
        var variantUser = new User
        {
            UserName = "historical:MenztrualCramps",
            NormalizedUserName = "HISTORICAL:MENZTRUALCRAMPS",
            DisplayName = "MenztrualCramps",
            IsDummyUser = true
        };
        _context.AddRange(canonicalPlaceholder, variantUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var merged = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);
        Assert.That(merged, Is.EqualTo(1));

        _context.ChangeTracker.Clear();
        var remainingUsers = await _context.Users.ToListAsync();
        Assert.That(remainingUsers, Has.Count.EqualTo(1));
        Assert.That(remainingUsers[0].Id, Is.EqualTo(canonicalPlaceholder.Id));
    }

    [Test]
    public async Task ConsolidateAsync_DoesNotMergeIntoUnrelatedRegisteredUserWithMatchingDisplayName()
    {
        // Registered real user who happens to have DisplayName "Silva", but is NOT a dummy user
        // and does NOT have an Xbox gamertag "Silva"
        var realUser = new User
        {
            UserName = "real_silva_account",
            NormalizedUserName = "REAL_SILVA_ACCOUNT",
            DisplayName = "Silva",
            IsDummyUser = false
        };
        var variant = new User
        {
            UserName = "historical:Major Silva",
            NormalizedUserName = "HISTORICAL:MAJOR SILVA",
            DisplayName = "Major Silva",
            IsDummyUser = true
        };
        _context.AddRange(realUser, variant);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        _context.ChangeTracker.Clear();
        // The real user should NOT be merged into or modified
        var preservedReal = await _context.Users.FindAsync(realUser.Id);
        Assert.That(preservedReal, Is.Not.Null);
        Assert.That(preservedReal!.UserName, Is.EqualTo("real_silva_account"));

        // The variant was renamed to canonical dummy handle instead of being merged into the real user
        var reloadedVariant = await _context.Users.FindAsync(variant.Id);
        Assert.That(reloadedVariant, Is.Not.Null);
        Assert.That(reloadedVariant!.DisplayName, Is.EqualTo("Silva"));
        Assert.That(reloadedVariant.UserName, Is.EqualTo("historical:Silva"));
    }

    [Test]
    public async Task ConsolidateAsync_SkipsMerge_WhenBothAccountsHaveXboxLinks()
    {
        var targetXbox = new XboxUser { XboxUserID = 101, Gamertag = "MenZ" };
        var sourceXbox = new XboxUser { XboxUserID = 202, Gamertag = "Its Rainin MenZ" };
        _context.XboxUsers.AddRange(targetXbox, sourceXbox);

        var canonicalUser = new User
        {
            UserName = "historical:MenZ",
            NormalizedUserName = "HISTORICAL:MENZ",
            DisplayName = "MenZ",
            IsDummyUser = true,
            XboxUser = targetXbox
        };
        var variantUser = new User
        {
            UserName = "historical:Its Rainin MenZ",
            NormalizedUserName = "HISTORICAL:ITS RAININ MENZ",
            DisplayName = "Its Rainin MenZ",
            IsDummyUser = true,
            XboxUser = sourceXbox
        };
        _context.AddRange(canonicalUser, variantUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var merged = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);
        Assert.That(merged, Is.EqualTo(0));

        _context.ChangeTracker.Clear();
        // Both users should still exist and retain their respective Xbox IDs
        var reloadedCanonical = await _context.Users.Include(u => u.XboxUser).FirstOrDefaultAsync(u => u.Id == canonicalUser.Id);
        var reloadedVariant = await _context.Users.Include(u => u.XboxUser).FirstOrDefaultAsync(u => u.Id == variantUser.Id);

        Assert.That(reloadedCanonical, Is.Not.Null);
        Assert.That(reloadedCanonical!.XboxUserID, Is.EqualTo(101));
        Assert.That(reloadedVariant, Is.Not.Null);
        Assert.That(reloadedVariant!.XboxUserID, Is.EqualTo(202));
    }

    [Test]
    public async Task ConsolidateAsync_DoesNotOverwriteTargetXbox_WhenTargetHasXboxAndSourceDoesNot()
    {
        var targetXbox = new XboxUser { XboxUserID = 303, Gamertag = "MenZ" };
        _context.XboxUsers.Add(targetXbox);

        var canonicalUser = new User
        {
            UserName = "historical:MenZ",
            NormalizedUserName = "HISTORICAL:MENZ",
            DisplayName = "MenZ",
            IsDummyUser = true,
            XboxUser = targetXbox
        };
        var variantUser = new User
        {
            UserName = "historical:Its Rainin MenZ",
            NormalizedUserName = "HISTORICAL:ITS RAININ MENZ",
            DisplayName = "Its Rainin MenZ",
            IsDummyUser = true,
            XboxUserID = null
        };
        _context.AddRange(canonicalUser, variantUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var merged = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);
        Assert.That(merged, Is.EqualTo(1));

        _context.ChangeTracker.Clear();
        var reloadedCanonical = await _context.Users.Include(u => u.XboxUser).FirstOrDefaultAsync(u => u.Id == canonicalUser.Id);
        Assert.That(reloadedCanonical, Is.Not.Null);
        Assert.That(reloadedCanonical!.XboxUserID, Is.EqualTo(303));
        Assert.That(await _context.Users.AnyAsync(u => u.Id == variantUser.Id), Is.False);
    }
}
