using System.Net;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Database.Models.User;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupButtonInteractionsTests
{
    private GrifballContext _context;
    private SignupButtonHandler _handler;
    private IDiscordButtonInteractionContext _discordContext;
    private FakeRestRequestHandler _rest;
    private readonly List<InteractionCallbackProperties> _callbacks = [];
    private const ulong DiscordId = 9876;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        _handler = new SignupButtonHandler(_context, new SignupsService(_context), urls);
        _rest = new FakeRestRequestHandler { NoContentForAll = true };
        _discordContext = BuildContext(DiscordId).ToDiscordContext();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private ButtonInteractionContext BuildContext(ulong userId)
    {
        var json = DiscordInteractionJson.Button("signup", userId, "discorduser");
        var restClient = new RestClient(new RestClientConfiguration { RequestHandler = _rest });
        var interaction = new ButtonInteraction(json, null, (_, cb, _, _, _) =>
        {
            lock (_callbacks) _callbacks.Add(cb);
            return Task.FromResult<InteractionCallbackResponse?>(null);
        }, restClient);
        return new ButtonInteractionContext(interaction, null!);
    }

    private string LastMessage()
    {
        var cb = _callbacks.Last();
        var data = (cb as InteractionCallbackProperties<InteractionMessageProperties>)?.Data;
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.Flags, Is.EqualTo(MessageFlags.Ephemeral));
        return data.Content!;
    }

    private async Task<User> AddLinkedUser(bool withGamertag = true)
    {
        var user = new User
        {
            UserName = "linked",
            DiscordUser = new DiscordUser { DiscordUserID = (long)DiscordId, DiscordUsername = "discorduser" },
            XboxUser = withGamertag ? new XboxUser { XboxUserID = 4444, Gamertag = "LinkedGT" } : null,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task Signup_NewSignup_CreatesSignupAndRepliesWithLink()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);

        await _handler.Signup(_discordContext, season.SeasonID);

        Assert.That(await _context.SeasonSignups.AnyAsync(s => s.SeasonID == season.SeasonID && s.UserID == user.Id), Is.True);
        Assert.That(LastMessage(), Does.StartWith("You have signed up for the season.")
            .And.EndWith($"[here](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)"));
    }

    [Test]
    public async Task Signup_SignupsClosed_SaysSo()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context, open: false);

        await _handler.Signup(_discordContext, season.SeasonID);

        Assert.That(LastMessage(), Is.EqualTo("Signups for this season are closed."));
        Assert.That(await _context.SeasonSignups.AnyAsync(s => s.UserID == user.Id), Is.False);
    }

    [Test]
    public async Task Signup_AlreadySignedUp_RepliesWithoutDuplicating()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);
        _context.SeasonSignups.Add(new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, TeamName = "x", Timestamp = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        await _handler.Signup(_discordContext, season.SeasonID);

        Assert.That(LastMessage(), Does.StartWith("You are already signed up for this season."));
        Assert.That(await _context.SeasonSignups.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Signup_NoGamertag_SendsTempResponseAndDeletesIt()
    {
        // Note: TempResponse waits 5s (app code) before deleting the response.
        await AddLinkedUser(withGamertag: false);
        var season = await SignupTestData.OpenSeason(_context);

        await _handler.Signup(_discordContext, season.SeasonID);

        Assert.That(LastMessage(), Is.EqualTo("You must set your gamertag first"));
        Assert.That(_rest.Requests, Has.Some.Matches<FakeRestRequestHandler.RecordedRequest>(r => r.Method == HttpMethod.Delete && r.Path.EndsWith("/messages/@original")));
        Assert.That(await _context.SeasonSignups.AnyAsync(), Is.False);
    }
}
