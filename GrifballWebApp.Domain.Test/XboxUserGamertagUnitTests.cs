using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>XboxUser.IsPossibleGamertag: the names worth asking Xbox about.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class XboxUserGamertagUnitTests
{
    [TestCase("Old Tag", TestName = "IsPossibleGamertag_LettersAndASpace_Is")]
    [TestCase("Brotherly690", TestName = "IsPossibleGamertag_LettersAndDigits_Is")]
    [TestCase("FatalLeeScotG", TestName = "IsPossibleGamertag_ThirteenCharacters_Is")]
    [TestCase("ABCDEFGHIJKLMNO", TestName = "IsPossibleGamertag_FifteenCharacters_IsTheClassicLimit")]
    [TestCase("Major Nelson#881", TestName = "IsPossibleGamertag_AModernGamertagWithItsSuffix_Is")]
    [TestCase("O'Neill", TestName = "IsPossibleGamertag_AnApostrophe_IsAllowed")]
    [TestCase("Smith, J", TestName = "IsPossibleGamertag_AComma_IsAllowedInClassicOnes")]
    [TestCase("øÜêþЯЂў", TestName = "IsPossibleGamertag_NonLatinLetters_AreAllowedInModernOnes")]
    public void IsPossibleGamertag_True(string name) => Assert.That(XboxUser.IsPossibleGamertag(name), Is.True);

    [TestCase("Bowdery(1)(G)", TestName = "IsPossibleGamertag_AGuestSuffix_IsNot")]
    [TestCase("TBD 357(1)(G)", TestName = "IsPossibleGamertag_ASplitScreenAndGuestSuffix_IsNot")]
    [TestCase("FatalLeeScotGuest(2)", TestName = "IsPossibleGamertag_ANumberedGuest_IsNot")]
    [TestCase("Lord xPathos(G)", TestName = "IsPossibleGamertag_ParenthesesAfterASpace_IsNot")]
    [TestCase("Dr:Who", TestName = "IsPossibleGamertag_AColon_IsNot")]
    [TestCase("a_b", TestName = "IsPossibleGamertag_AnUnderscore_IsNot")]
    [TestCase("x.y", TestName = "IsPossibleGamertag_ADot_IsNot")]
    [TestCase("ABCDEFGHIJKLMNOP", TestName = "IsPossibleGamertag_SixteenCharactersWithNoSuffix_IsNot")]
    [TestCase("ABCDEFGHIJKL#1234", TestName = "IsPossibleGamertag_SeventeenWithTheSuffix_IsNot")]
    [TestCase("Name#", TestName = "IsPossibleGamertag_AHashWithNoSuffix_IsNot")]
    [TestCase("Name#12a", TestName = "IsPossibleGamertag_ASuffixThatIsNotDigits_IsNot")]
    [TestCase("A#123456789012345", TestName = "IsPossibleGamertag_ASuffixOverFourteenDigits_IsNot")]
    [TestCase("Tab\tName", TestName = "IsPossibleGamertag_AControlCharacter_IsNot")]
    [TestCase("Name[1]", TestName = "IsPossibleGamertag_ASquareBracket_IsNot")]
    [TestCase("Name{x}", TestName = "IsPossibleGamertag_ACurlyBrace_IsNot")]
    [TestCase("Name¿", TestName = "IsPossibleGamertag_ALatinSupplementSymbol_IsNot")]
    [TestCase("#1234", TestName = "IsPossibleGamertag_OnlyASuffix_IsNot")]
    [TestCase("", TestName = "IsPossibleGamertag_Empty_IsNot")]
    [TestCase("   ", TestName = "IsPossibleGamertag_Blank_IsNot")]
    [TestCase(null, TestName = "IsPossibleGamertag_Null_IsNot")]
    public void IsPossibleGamertag_False(string? name) => Assert.That(XboxUser.IsPossibleGamertag(name), Is.False);
}
