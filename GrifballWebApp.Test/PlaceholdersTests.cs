using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>The one definition of an import placeholder (<see cref="Placeholders"/>), shared by the historical import's
/// services (XuidLookupService, GamertagClaimService, DiscordAccountLinkService, DuplicateSuggestionService, UserNames)
/// so they never disagree on what one is. #301.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PlaceholdersTests
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
        => Assert.That(Placeholders.IsImportPlaceholder(Make(isDummy, userName, xboxUserID)), Is.EqualTo(expected));

    [Test]
    public async Task IsImportPlaceholderExpression_FiltersTheSameWay_InAQuery()
    {
        var placeholder = Make(isDummy: true, userName: "historical:Old Tag", xboxUserID: null);
        var tied = Make(isDummy: true, userName: "historical:Tied", xboxUserID: 123L);
        var real = Make(isDummy: false, userName: "signedup", xboxUserID: null);
        _context.Users.AddRange(placeholder, tied, real);
        await _context.SaveChangesAsync();

        var matched = await _context.Users.Where(Placeholders.IsImportPlaceholderExpression).Select(u => u.UserName).ToListAsync();

        Assert.That(matched, Is.EqualTo(new[] { "historical:Old Tag" }));
    }

    [TestCase("historical:Old Tag", "Old Tag", TestName = "GamertagOf_APlainPlaceholderName_IsTheGamertag")]
    [TestCase("HISTORICAL:Old Tag", "Old Tag", TestName = "GamertagOf_IsCaseInsensitiveOnThePrefix")]
    [TestCase("historical:Old Tag:123", "Old Tag", TestName = "GamertagOf_AnXboxAccountsOwnPlaceholder_DropsTheXuidAfterIt")]
    [TestCase("someone", null, TestName = "GamertagOf_ARealLoginName_IsNull")]
    [TestCase(null, null, TestName = "GamertagOf_NoUserName_IsNull")]
    public void GamertagOf_IsThePrefixedNamesGamertag(string? userName, string? expected)
        => Assert.That(Placeholders.GamertagOf(userName), Is.EqualTo(expected));
}
