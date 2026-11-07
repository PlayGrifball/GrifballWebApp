using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

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

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static User Make(bool isDummy, string? userName, long? xboxUserID) => new()
    {
        UserName = userName,
        NormalizedUserName = userName?.ToUpperInvariant(),
        IsDummyUser = isDummy,
        XboxUserID = xboxUserID,
    };

    [TestCase(true, "historical:Old Tag", null, true, TestName = "IsImportPlaceholder_UntiedDummyNamedForTheArchive_IsOne")]
    [TestCase(true, "HISTORICAL:Old Tag", null, true, TestName = "IsImportPlaceholder_IsCaseInsensitiveOnThePrefix")]
    [TestCase(true, "historical:Old Tag", 123L, false, TestName = "IsImportPlaceholder_AlreadyTiedToAnXboxAccount_IsNot")]
    [TestCase(false, "historical:Old Tag", null, false, TestName = "IsImportPlaceholder_NotADummyUser_IsNot")]
    [TestCase(true, "someone", null, false, TestName = "IsImportPlaceholder_ADummyWithNoHistoricalName_IsNot")]
    [TestCase(true, null, null, false, TestName = "IsImportPlaceholder_NoUserName_IsNot")]
    public void IsImportPlaceholder_MatchesTheOneDefinition(bool isDummy, string? userName, long? xboxUserID, bool expected)
        => Assert.That(Make(isDummy, userName, xboxUserID).IsImportPlaceholder, Is.EqualTo(expected));

    [Test]
    public void IsImportPlaceholder_IsNotAColumn()
        => Assert.That(_context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.IsImportPlaceholder)), Is.Null);

    [Test]
    public async Task IsImportPlaceholderExpression_FiltersTheSameWay_InAQuery()
    {
        // The tied placeholder's Xbox account must exist: XboxUserID is a foreign key
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 123L, Gamertag = "Tied" });
        var placeholder = Make(isDummy: true, userName: "historical:Old Tag", xboxUserID: null);
        var tied = Make(isDummy: true, userName: "historical:Tied", xboxUserID: 123L);
        var real = Make(isDummy: false, userName: "signedup", xboxUserID: null);
        var dummy = Make(isDummy: true, userName: "someone", xboxUserID: null);
        _context.Users.AddRange(placeholder, tied, real, dummy);
        await _context.SaveChangesAsync();

        var matched = await _context.Users.Where(User.IsImportPlaceholderExpression).Select(u => u.UserName).ToListAsync();

        Assert.That(matched, Is.EqualTo(new[] { "historical:Old Tag" }));
    }

    [TestCase("historical:Old Tag", "Old Tag", TestName = "PlaceholderGamertagOf_APlainPlaceholderName_IsTheGamertag")]
    [TestCase("HISTORICAL:Old Tag", "Old Tag", TestName = "PlaceholderGamertagOf_IsCaseInsensitiveOnThePrefix")]
    [TestCase("historical:Old Tag:123", "Old Tag", TestName = "PlaceholderGamertagOf_AnXboxAccountsOwnPlaceholder_DropsTheXuidAfterIt")]
    [TestCase("someone", null, TestName = "PlaceholderGamertagOf_ARealLoginName_IsNull")]
    [TestCase(null, null, TestName = "PlaceholderGamertagOf_NoUserName_IsNull")]
    public void PlaceholderGamertagOf_IsThePrefixedNamesGamertag(string? userName, string? expected)
        => Assert.That(User.PlaceholderGamertagOf(userName), Is.EqualTo(expected));
}
