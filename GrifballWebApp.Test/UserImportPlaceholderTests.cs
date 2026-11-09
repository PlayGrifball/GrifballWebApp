using GrifballWebApp.Database;
using GrifballWebApp.Domain.Players;
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

    private static User Make(bool isDummy, string? userName, long? xboxUserID)
        => User.Seed(userName: userName, normalizedUserName: userName?.ToUpperInvariant(), isDummyUser: isDummy, xboxUserID: xboxUserID);

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
        Assert.That(matched, Is.EquivalentTo(new[] { "historical:Old Tag", "HISTORICAL:Upper Tag" }));
        Assert.That(matched, Is.EquivalentTo(loaded.Where(u => u.IsImportPlaceholder).Select(u => u.UserName)));
    }

    [TestCase("historical:Old Tag", "Old Tag", TestName = "PlaceholderGamertagOf_APlainPlaceholderName_IsTheGamertag")]
    [TestCase("HISTORICAL:Old Tag", "Old Tag", TestName = "PlaceholderGamertagOf_IsCaseInsensitiveOnThePrefix")]
    [TestCase("historical:Old Tag:123", "Old Tag", TestName = "PlaceholderGamertagOf_AnXboxAccountsOwnPlaceholder_DropsTheXuidAfterIt")]
    [TestCase("historical:Dr:Who", "Dr:Who", TestName = "PlaceholderGamertagOf_AnArchiveNameWithColons_KeepsThem")]
    [TestCase("historical:::ZERO Logic::", "::ZERO Logic::", TestName = "PlaceholderGamertagOf_AnArchiveNameEndingInAColon_KeepsIt")]
    [TestCase("historical:Dr:Who:2535405291234567", "Dr:Who", TestName = "PlaceholderGamertagOf_AColonNamesXuidSuffix_IsDropped")]
    [TestCase("historical::42", ":42", TestName = "PlaceholderGamertagOf_ANameThatIsOnlyDigitsAfterAColon_IsKept")]
    [TestCase("someone", null, TestName = "PlaceholderGamertagOf_ARealLoginName_IsNull")]
    [TestCase(null, null, TestName = "PlaceholderGamertagOf_NoUserName_IsNull")]
    public void PlaceholderGamertagOf_IsThePrefixedNamesGamertag(string? userName, string? expected)
        => Assert.That(User.PlaceholderGamertagOf(userName), Is.EqualTo(expected));
}
