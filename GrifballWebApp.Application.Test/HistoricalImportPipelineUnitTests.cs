using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Application.Test;

/// <summary>HistoricalImportPipelineTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportPipelineUnitTests
{
    [Test]
    public void TheGuardrail_MeasuresEachKindOfRow()
    {
        var guardrail = new HistoricalGuardrailOptions { MaxRemovedFraction = 0.5m, StopOnAnyChampionshipRemoved = false };
        HistoricalSeasonRemoval Removal(int statsRemoved = 0, int teamsRemoved = 0, int titlesRemoved = 0, int before = 10)
            => new() { SeasonKey = "s", StatsBefore = before, StatsRemoved = statsRemoved, TeamsBefore = before, TeamsRemoved = teamsRemoved, TitlesBefore = before, TitlesRemoved = titlesRemoved };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(guardrail.Exceeded(Removal(statsRemoved: 5)), Is.False, "Not more than half");
            Assert.That(guardrail.Exceeded(Removal(statsRemoved: 6)), Is.True);
            Assert.That(guardrail.Exceeded(Removal(teamsRemoved: 6)), Is.True);
            Assert.That(guardrail.Exceeded(Removal(titlesRemoved: 1)), Is.False, "Titles by share when any isn't asked for");
            Assert.That(guardrail.Exceeded(Removal(titlesRemoved: 6)), Is.True);
            Assert.That(guardrail.Exceeded(Removal(before: 0)), Is.False, "A new season removes nothing");
            Assert.That(new HistoricalGuardrailOptions().Exceeded(Removal(titlesRemoved: 1)), Is.True, "Any title, by default");
            Assert.That(Removal(statsRemoved: 1).Summary, Is.EqualTo("1 of 10 stat rows, 0 of 10 teams, 0 of 10 titles"));
        }
    }
}
