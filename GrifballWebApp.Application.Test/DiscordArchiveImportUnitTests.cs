using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Application.Test;

/// <summary>DiscordArchiveImportTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordArchiveImportUnitTests
{
    private static readonly System.Text.Json.JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };
    internal const long Discord = 386902644670595072;

    [Test]
    public void TheLateLeagueMasterList_ReadsAsPlayers()
    {
        var file = System.Text.Json.JsonSerializer.Deserialize<LateLeagueMasterFile>("""
            { "sourceId": "late-league-aspence-main", "playerMasterlist": [
                { "Gamertag": "ActiveHate", "Discord": "ActiveHate", "Discord ID": "386902644670595072", "Location": "East" },
                { "Gamertag": "  ", "Discord": "nobody" },
                { "Gamertag": "No Id", "Discord": "x", "Discord ID": "n/a", "System": "PC" } ] }
            """, CaseInsensitive)!;

        var players = file.ToPlayers();

        Assert.That(players.Select(p => (p.Gamertag, p.DiscordUserId, p.Region, p.System, p.SourceKey)), Is.EqualTo(new[]
        {
            ("ActiveHate", (long?)Discord, (string?)"East", (string?)null, (string?)"late-league-aspence-main/Player Masterlist"),
            ("No Id", (long?)null, (string?)null, (string?)"PC", (string?)"late-league-aspence-main/Player Masterlist"),
        }));
    }
}
