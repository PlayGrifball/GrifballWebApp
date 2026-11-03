using GrifballWebApp.Server.Historical;
using NUnit.Framework;

namespace GrifballWebApp.Test;

[TestFixture]
public class HistoricalAliasesTests
{
    [Test]
    public void CanonicalGamertag_MapsKnownVariantsToCanonicalHandles()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalAliases.CanonicalGamertag("MenztrualCramps"), Is.EqualTo("MenZ"));
            Assert.That(HistoricalAliases.CanonicalGamertag("Its Rainin MenZ"), Is.EqualTo("MenZ"));
            Assert.That(HistoricalAliases.CanonicalGamertag("Itz Rainin MenZ"), Is.EqualTo("MenZ"));
            Assert.That(HistoricalAliases.CanonicalGamertag("MenZ"), Is.EqualTo("MenZ"));
            Assert.That(HistoricalAliases.CanonicalGamertag("I Major Silva I"), Is.EqualTo("Silva"));
            Assert.That(HistoricalAliases.CanonicalGamertag("The Silvaa"), Is.EqualTo("Silva"));
            Assert.That(HistoricalAliases.CanonicalGamertag("Oh My Godlxss"), Is.EqualTo("Regresser"));
            Assert.That(HistoricalAliases.CanonicalGamertag("DI Cell4225"), Is.EqualTo("cxrnchip"));
            Assert.That(HistoricalAliases.CanonicalGamertag("Ace B S1entNd1"), Is.EqualTo("ACE B S1lentND1"));
            Assert.That(HistoricalAliases.CanonicalGamertag("oScet"), Is.EqualTo("Scet Shin"));
            Assert.That(HistoricalAliases.CanonicalGamertag("Gingy"), Is.EqualTo("ixGingy"));
            Assert.That(HistoricalAliases.CanonicalGamertag("SilentShadow451"), Is.EqualTo("SilentShadow459"));
            Assert.That(HistoricalAliases.CanonicalGamertag("UnknownPlayer99"), Is.EqualTo("UnknownPlayer99"));
        });
    }

    [Test]
    public void IsKnownAlias_IdentifiesAliasesCorrectly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalAliases.IsKnownAlias("MenztrualCramps"), Is.True);
            Assert.That(HistoricalAliases.IsKnownAlias("Its Rainin MenZ"), Is.True);
            Assert.That(HistoricalAliases.IsKnownAlias("MenZ"), Is.False);
            Assert.That(HistoricalAliases.IsKnownAlias("Silva"), Is.False);
            Assert.That(HistoricalAliases.IsKnownAlias("I Major Silva I"), Is.True);
            Assert.That(HistoricalAliases.IsKnownAlias("UnknownPlayer99"), Is.False);
            Assert.That(HistoricalAliases.IsKnownAlias(""), Is.False);
            Assert.That(HistoricalAliases.IsKnownAlias(null!), Is.False);
        });
    }

    [Test]
    public void GetAliasesFor_ReturnsRegisteredAliases()
    {
        var menzAliases = HistoricalAliases.GetAliasesFor("MenZ");
        Assert.Multiple(() =>
        {
            Assert.That(menzAliases, Contains.Item("MenztrualCramps"));
            Assert.That(menzAliases, Contains.Item("Its Rainin MenZ"));
            Assert.That(menzAliases, Contains.Item("Zerosum33"));
        });

        // Resolves from variant as well
        var silvaAliasesFromVariant = HistoricalAliases.GetAliasesFor("Major Silva");
        Assert.Multiple(() =>
        {
            Assert.That(silvaAliasesFromVariant, Contains.Item("Major Silva"));
            Assert.That(silvaAliasesFromVariant, Contains.Item("The Silvaa"));
        });

        Assert.That(HistoricalAliases.GetAliasesFor("NonExistentPlayer"), Is.Empty);
    }
}
