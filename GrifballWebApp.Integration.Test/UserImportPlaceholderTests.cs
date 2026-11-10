using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using static GrifballWebApp.Domain.Test.UserImportPlaceholderTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>The one definition of an import placeholder, on <see cref="User"/> (<see cref="User.IsImportPlaceholder"/>,
/// <see cref="User.IsImportPlaceholderExpression"/>, <see cref="User.PlaceholderGamertagOf"/>), shared by the historical
/// import's services (XuidLookupService, GamertagClaimService, DiscordAccountLinkService, DuplicateSuggestionService,
/// UserNames) so they never disagree on what one is. #301.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserImportPlaceholderTests
{
    private GrifballContext _context;
    private static readonly string[] expected = new[] { "historical:Old Tag", "HISTORICAL:Upper Tag" };

    [SetUp]
    public async Task Setup() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void IsImportPlaceholder_IsNotAColumn()
        => Assert.That(_context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.IsImportPlaceholder)), Is.Null);

    [Test]
    public async Task IsImportPlaceholderExpression_FiltersTheSameWay_InAQuery()
    {
        // The tied placeholder's Xbox account must exist: XboxUserID is a foreign key
        _context.XboxUsers.Add(XboxUser.FromXbox(123L, "Tied"));
        var placeholder = Make(isDummy: true, userName: "historical:Old Tag", xboxUserID: null);
        var tied = Make(isDummy: true, userName: "historical:Tied", xboxUserID: 123L);
        var real = Make(isDummy: false, userName: "signedup", xboxUserID: null);
        var dummy = Make(isDummy: true, userName: "someone", xboxUserID: null);
        var upper = Make(isDummy: true, userName: "HISTORICAL:Upper Tag", xboxUserID: null);
        _context.Users.AddRange(placeholder, tied, real, dummy, upper);
        await _context.SaveChangesAsync();

        var matched = await _context.Users.Where(User.IsImportPlaceholderExpression).Select(u => u.UserName).ToListAsync();
        var loaded = await _context.Users.AsNoTracking().ToListAsync();

        // The query and the bool agree, case and all, on every database
        Assert.That(matched, Is.EquivalentTo(expected));
        Assert.That(matched, Is.EquivalentTo(loaded.Where(u => u.IsImportPlaceholder).Select(u => u.UserName)));
    }
}
