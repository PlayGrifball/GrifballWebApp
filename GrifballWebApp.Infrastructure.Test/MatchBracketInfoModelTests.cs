using System.Reflection;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// A bracket slot is a part of its season match's aggregate (#298): the context has no way to it but through the match.
/// Its factories and rules are the Domain's own (GrifballWebApp.Domain.Test's MatchBracketInfoUnitTests).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchBracketInfoModelTests
{
    [Test]
    public void TheContext_HasNoSetOfBracketSlots()
    {
        var sets = typeof(GrifballContext).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.PropertyType == typeof(DbSet<MatchBracketInfo>));

        Assert.That(sets, Is.Empty, "A part: reached through its season match");
    }
}
