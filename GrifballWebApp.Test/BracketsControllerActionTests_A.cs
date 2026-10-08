using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;
using System.Text.Json;

namespace GrifballWebApp.Test.CovA;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BracketsControllerActionTests_A
{
    private IBracketService _service;
    private BracketsController _controller;

    [SetUp]
    public void Setup()
    {
        _service = Substitute.For<IBracketService>();
        _controller = new BracketsController(Substitute.For<ILogger<BracketsController>>(), _service);
    }

    [TestCase(0, 1, "Please provide participantsCount")]
    [TestCase(-1, 1, "Please provide participantsCount")]
    [TestCase(4, 0, "Please provide seasonID")]
    public async Task CreateBracket_InvalidInput_ReturnsBadRequest(int participants, int seasonID, string message)
    {
        var result = await _controller.CreateBracket(participants, seasonID, true, 3, CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo(message));
        await _service.DidNotReceiveWithAnyArgs().CreateBracket(default, default, default, default, default);
    }

    [Test]
    public async Task CreateBracket_Valid_CallsServiceAndReturnsOk()
    {
        using var cts = new CancellationTokenSource();

        var result = await _controller.CreateBracket(8, 2, true, 3, cts.Token);

        Assert.That(result, Is.TypeOf<OkResult>());
        await _service.Received(1).CreateBracket(8, 2, true, 3, cts.Token);
    }

    [Test]
    public async Task GetBracket_InvalidSeason_ReturnsBadRequest()
    {
        var result = await _controller.GetBracket(0);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        await _service.DidNotReceiveWithAnyArgs().GetBracketsAsync(default, default);
    }

    [Test]
    public async Task GetBracket_Valid_ReturnsDto()
    {
        var dto = new BracketDto
        {
            WinnerRounds = [new RoundDto { RoundNumber = 1, Matches = [new MatchDto { MatchNumber = "W1", HomeTeam = "Seed 1", AwayTeam = "Seed 2" }] }],
            LoserRounds = [],
            GrandFinal = new MatchDto { MatchNumber = "W2", HomeTeam = "a", AwayTeam = "b" },
            GrandFinalSuddenDeath = null!,
        };
        _service.GetBracketsAsync(5, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await _controller.GetBracket(5);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(dto));
        var value = (BracketDto)ok.Value!;
        Assert.That(value.WinnerRounds[0].Matches[0].HomeTeam, Is.EqualTo("Seed 1"));
        Assert.That(value.GrandFinal.MatchNumber, Is.EqualTo("W2"));
    }

    [Test]
    public async Task GetViewerData_InvalidSeason_ReturnsBadRequest()
    {
        var result = await _controller.GetViewerData(-3);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result.Result!).Value, Is.EqualTo("Please provide seasonID"));
    }

    [Test]
    public async Task GetViewerData_Valid_ReturnsDto()
    {
        var dto = new ViewerDataDto();
        _service.GetViewerDataAsync(7, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await _controller.GetViewerData(7);

        Assert.That((result.Result as OkObjectResult)?.Value, Is.SameAs(dto));
    }

    [Test]
    public async Task SetSeeds_PassesNullCustomSeeds()
    {
        using var cts = new CancellationTokenSource();

        await _controller.SetSeeds(9, cts.Token);

        await _service.Received(1).SetSeeds(9, null, cts.Token);
    }

    [TestCase(nameof(BracketsController.CreateBracket), "Commissioner")]
    [TestCase(nameof(BracketsController.SetSeeds), "Commissioner")]
    [TestCase(nameof(BracketsController.SetCustomSeeds), "Commissioner")]
    [TestCase(nameof(BracketsController.GetBracket), null)]
    [TestCase(nameof(BracketsController.GetViewerData), null)]
    public void Actions_HaveExpectedAuthorization(string action, string? roles)
    {
        var attr = typeof(BracketsController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();
        if (roles is null)
            Assert.That(attr, Is.Null, "read endpoints are public");
        else
            Assert.That(attr?.Roles, Is.EqualTo(roles));
    }

    [Test]
    public void ViewerData_SerializesEnumsAsStringsAndOmitsNulls()
    {
        var dto = new ViewerDataDto
        {
            Stages = [new Stage
            {
                Id = 1, Tournament_id = 1, Name = "S", Number = 1, Type = StageType.double_elimination,
                Settings = new StageSettings
                {
                    Size = 4, GroupCount = 2, SeedOrdering = [SeedOrdering.inner_outer], ManualOrdering = [],
                    GrandFinal = GrandFinalType.@double, RoundRobinMode = RoundRobinMode.simple,
                },
            }],
            Matches = [new Match
            {
                Id = 3, Stage_id = 1, Group_id = 1, Round_id = 1, Number = 1, Status = Status.Ready,
                Opponent1 = new ParticpantResult { Id = 10, Position = 1, Score = 2, Result = Result.win },
                Opponent2 = new ParticpantResult { Id = null, Forfeit = true },
            }],
            MatchGames = [new MatchGame { Id = "g", Stage_id = 1, ParentID = 3, Number = 1 }],
            Participants = [new Participant { Id = 10, Tournament_id = 1, Name = "Team" }],
        };

        var json = JsonSerializer.Serialize(dto);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var stage = root.GetProperty("Stages")[0];
        var match = root.GetProperty("Matches")[0];
        var game = root.GetProperty("MatchGames")[0];

        Assert.Multiple(() =>
        {
            Assert.That(stage.GetProperty("Type").GetString(), Is.EqualTo("double_elimination"));
            Assert.That(stage.GetProperty("Settings").GetProperty("GrandFinal").GetString(), Is.EqualTo("double"));
            Assert.That(stage.GetProperty("Settings").GetProperty("SeedOrdering")[0].GetString(), Is.EqualTo("inner_outer"));
            Assert.That(stage.GetProperty("Settings").GetProperty("GroupCount").GetInt32(), Is.EqualTo(2));
            // Status has no string converter
            Assert.That(match.GetProperty("Status").GetInt32(), Is.EqualTo((int)Status.Ready));
            Assert.That(match.GetProperty("Opponent1").GetProperty("Result").GetString(), Is.EqualTo("win"));
            var opp2 = match.GetProperty("Opponent2");
            Assert.That(opp2.TryGetProperty("Position", out _), Is.False);
            Assert.That(opp2.TryGetProperty("Score", out _), Is.False);
            Assert.That(opp2.TryGetProperty("Result", out _), Is.False);
            Assert.That(opp2.GetProperty("Id").ValueKind, Is.EqualTo(JsonValueKind.Null), "Id is written even when null");
            Assert.That(opp2.GetProperty("Forfeit").GetBoolean(), Is.True);
            Assert.That((game.GetProperty("Id").GetString(), game.GetProperty("Stage_id").GetInt32(), game.GetProperty("ParentID").GetInt32(), game.GetProperty("Number").GetInt32()),
                Is.EqualTo(("g", 1, 3, 1)));
        });
    }
}
