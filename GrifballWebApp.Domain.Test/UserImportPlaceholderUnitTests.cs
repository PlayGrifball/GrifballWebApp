using GrifballWebApp.Domain.Players;
using static GrifballWebApp.Domain.Test.UserImportPlaceholderTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>UserImportPlaceholderTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserImportPlaceholderUnitTests
{
    [TestCase(true, "historical:Old Tag", null, true, TestName = "IsImportPlaceholder_UntiedDummyNamedForTheArchive_IsOne")]
    [TestCase(true, "HISTORICAL:Old Tag", null, true, TestName = "IsImportPlaceholder_IsCaseInsensitiveOnThePrefix")]
    [TestCase(true, "historical:Old Tag", 123L, false, TestName = "IsImportPlaceholder_AlreadyTiedToAnXboxAccount_IsNot")]
    [TestCase(false, "historical:Old Tag", null, false, TestName = "IsImportPlaceholder_NotADummyUser_IsNot")]
    [TestCase(true, "someone", null, false, TestName = "IsImportPlaceholder_ADummyWithNoHistoricalName_IsNot")]
    [TestCase(true, null, null, false, TestName = "IsImportPlaceholder_NoUserName_IsNot")]
    public void IsImportPlaceholder_MatchesTheOneDefinition(bool isDummy, string? userName, long? xboxUserID, bool expected)
        => Assert.That(Make(isDummy, userName, xboxUserID).IsImportPlaceholder, Is.EqualTo(expected));

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

/// <summary>UserImportPlaceholderTests' helpers, shared with its integration tests.</summary>
internal static class UserImportPlaceholderTestData
{
    internal static User Make(bool isDummy, string? userName, long? xboxUserID)
        => User.Seed(userName: userName, normalizedUserName: userName?.ToUpperInvariant(), isDummyUser: isDummy, xboxUserID: xboxUserID);
}
