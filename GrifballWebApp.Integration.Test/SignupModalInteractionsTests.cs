using System.Text.Json;
using DiscordInterfaces;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Domain.Players.User;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;

namespace GrifballWebApp.Integration.Test;

/// <summary>The signup modal's submit: its text input, checkbox groups and the times it carries reach the signup.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupModalInteractionsTests
{
    private static readonly JsonSerializerOptions NumbersFromStrings = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
    private const ulong DiscordId = 9876;

    private GrifballContext _context;
    private SignupButtonHandler _handler;
    private FakeRestRequestHandler _rest;
    private readonly List<InteractionCallbackProperties> _callbacks = [];

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        _handler = new SignupButtonHandler(_context, new SignupsService(_context), urls);
        _rest = new FakeRestRequestHandler();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private string Reply() => _rest.Requests.Last(r => r.Method == HttpMethod.Patch).Json.RootElement.GetProperty("content").GetString()!;

    private static string Quoted(IEnumerable<string> values) => string.Join(",", values.Select(v => $"\"{v}\""));

    private IDiscordModalInteractionContext Submit(int seasonId, string teamName, string[] choices, string[]? times = null)
    {
        var timeGroups = string.Concat((times is null ? Array.Empty<string[]>() : new[] { times }).Select((values, i) =>
            $$$"""
            ,{"type":18,"id":{{{10 + i}}},"component":{"type":22,"id":{{{20 + i}}},"custom_id":"times_{{{i}}}","values":[{{{Quoted(values)}}}]}}
            """));
        var json = $$$"""
        {"id":"1","application_id":"2","type":5,"data":{"custom_id":"signup:{{{seasonId}}}",
           "components":[
             {"type":18,"id":1,"component":{"type":4,"id":2,"custom_id":"team_name","value":"{{{teamName}}}"}},
             {"type":18,"id":3,"component":{"type":22,"id":4,"custom_id":"choices","values":[{{{Quoted(choices)}}}]}}
             {{{timeGroups}}}]},
         "channel_id":"3","channel":{"id":"3","type":0},
         "user":{"id":"{{{DiscordId}}}","username":"discorduser","discriminator":"0"},"token":"tok","version":1,
         "locale":"en-US","entitlements":[],"authorizing_integration_owners":{},"context":1}
        """;
        var model = JsonSerializer.Deserialize<JsonInteraction>(json, NumbersFromStrings)!;
        var restClient = new RestClient(new RestClientConfiguration { RequestHandler = _rest });
        var interaction = new ModalInteraction(model, null!, (_, cb, _, _, _) => { lock (_callbacks) _callbacks.Add(cb); return Task.FromResult<InteractionCallbackResponse?>(null); }, restClient);
        return new ModalInteractionContext(interaction, null!).ToDiscordContext();
    }

    private async Task<User> AddLinkedUser()
    {
        var user = User.Seed(userName: "linked", discordUser: DiscordUser.FromDiscord((long)DiscordId, "discorduser"), xboxUser: XboxUser.FromXbox(4444, "LinkedGT"));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task Submit_SignsUpWithTheChoicesAndTimesPicked()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);
        var options = await SignupTestData.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(19, 1)), (DayOfWeek.Tuesday, new TimeOnly(20, 1)));

        await _handler.SignupDetails(Submit(season.SeasonID, "Grifs", ["captain"], [options[1].AvailabilityOptionID.ToString()]), season.SeasonID);

        var signup = await _context.SeasonSignups.Include(s => s.SignupAvailability).AsNoTracking().SingleAsync(s => s.UserID == user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signup.TeamName, Is.EqualTo("Grifs"));
            Assert.That(signup.WillCaptain, Is.True);
            Assert.That(signup.RequiresAssistanceDrafting, Is.False);
            Assert.That(signup.SignupAvailability.Select(a => a.AvailabilityOptionID), Is.EqualTo(new[] { options[1].AvailabilityOptionID }));
            Assert.That(_callbacks.Single().Type, Is.EqualTo(InteractionCallbackType.DeferredMessage));
            Assert.That(Reply(), Is.EqualTo("Your signup is saved. Times (UTC): Tue 20:01."));
        }
    }

    [Test]
    public async Task Submit_UncheckedTimesAreRemoved_AndAnEmptyTeamNameIsNone()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);
        var options = await SignupTestData.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(19, 1)), (DayOfWeek.Tuesday, new TimeOnly(20, 1)));
        await new SignupsService(_context).UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID,
            UserID = user.Id,
            TeamName = "Old",
            Timeslots = [new TimeslotDto { OptionID = options[0].AvailabilityOptionID, IsChecked = true }],
        });

        await _handler.SignupDetails(Submit(season.SeasonID, "", ["captain", "draft_help"], []), season.SeasonID);

        var signup = await _context.SeasonSignups.Include(s => s.SignupAvailability).AsNoTracking().SingleAsync(s => s.UserID == user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signup.TeamName, Is.Null);
            Assert.That(signup.RequiresAssistanceDrafting, Is.True);
            Assert.That(signup.SignupAvailability, Is.Empty);
        }
    }

    [Test]
    public async Task Submit_DraftHelpWithoutCaptain_IsOffAndSaysWhy()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);

        await _handler.SignupDetails(Submit(season.SeasonID, "", ["draft_help"]), season.SeasonID);

        var signup = await _context.SeasonSignups.AsNoTracking().SingleAsync(s => s.UserID == user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signup.RequiresAssistanceDrafting, Is.False);
            Assert.That(Reply(), Is.EqualTo("Your signup is saved. No times set yet. Draft help is only for captains, so it is off."));
        }
    }

    [Test]
    public async Task Submit_WithoutAnAccount_AsksForAGamertagAndSavesNothing()
    {
        var season = await SignupTestData.OpenSeason(_context);

        await _handler.SignupDetails(Submit(season.SeasonID, "Grifs", ["captain"]), season.SeasonID);

        var reply = ((InteractionCallbackProperties<InteractionMessageProperties>)_callbacks.Single()).Data!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply.Content, Is.EqualTo("You must set your gamertag first"));
            Assert.That(await _context.SeasonSignups.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task Submit_MoreTimesThanFit_KeepsTheRestAndLinksTheForm()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);
        // 31 times: Monday 00:01-23:01, then Tuesday 00:01-06:01; the modal shows the first 30
        var slots = Enumerable.Range(0, 24).Select(h => (DayOfWeek.Monday, new TimeOnly(h, 1)))
            .Concat(Enumerable.Range(0, 7).Select(h => (DayOfWeek.Tuesday, new TimeOnly(h, 1)))).ToArray();
        var options = await SignupTestData.Options(_context, season, slots);
        await new SignupsService(_context).UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID,
            UserID = user.Id,
            Timeslots = [new TimeslotDto { OptionID = options[30].AvailabilityOptionID, IsChecked = true }],
        });

        await _handler.SignupDetails(Submit(season.SeasonID, "", [], [options[0].AvailabilityOptionID.ToString()]), season.SeasonID);

        var signup = await _context.SeasonSignups.Include(s => s.SignupAvailability).AsNoTracking().SingleAsync(s => s.UserID == user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signup.SignupAvailability.Select(a => a.AvailabilityOptionID).Order(),
                Is.EqualTo(new[] { options[0].AvailabilityOptionID, options[30].AvailabilityOptionID }.Order()));
            Assert.That(Reply(), Is.EqualTo("Your signup is saved. Times (UTC): Mon 00:01, Tue 06:01. This season has more times than fit here: set them "
                + $"[on the signup form](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)."));
        }
    }

    [Test]
    public async Task Submit_SignupsClosed_MakesNoSignup()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context, open: false);

        await _handler.SignupDetails(Submit(season.SeasonID, "", []), season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.SeasonSignups.AnyAsync(s => s.UserID == user.Id), Is.False);
            Assert.That(Reply(), Is.EqualTo("Signups for this season are closed."));
        }
    }
}
