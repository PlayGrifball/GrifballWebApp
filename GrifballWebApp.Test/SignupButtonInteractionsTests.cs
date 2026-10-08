using System.Net;
using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Database.Models.User;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using SignupButtonInteractions = GrifballWebApp.Server.Signups.ButtonInteractions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupButtonInteractionsTests
{
    private GrifballContext _context;
    private SignupButtonInteractions _module;
    private FakeRestRequestHandler _rest;
    private readonly List<InteractionCallback> _callbacks = [];
    private const ulong DiscordId = 9876;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        _module = new SignupButtonInteractions(_context, new SignupsService(_context), urls);
        _rest = new FakeRestRequestHandler { NoContentForAll = true };
        SetContext(_module, BuildContext(DiscordId));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static void FillArrays(object o)
    {
        foreach (var p in o.GetType().GetProperties())
            if (p.PropertyType.IsArray && p.CanWrite && p.GetValue(o) is null)
                p.SetValue(o, Array.CreateInstance(p.PropertyType.GetElementType()!, 0));
    }

    private ButtonInteractionContext BuildContext(ulong userId)
    {
        var json = new JsonInteraction
        {
            Id = 1,
            ApplicationId = 2,
            Type = (InteractionType)3, // message component
            Token = "token",
            User = new JsonUser { Id = userId, Username = "discorduser" },
            Data = new JsonInteractionData { CustomId = "signup", ComponentType = ComponentType.Button },
            Message = new JsonMessage { Id = 5, ChannelId = 6, Author = new JsonUser { Id = 7, Username = "bot" } },
            Channel = new JsonChannel { Id = 6, Type = ChannelType.TextGuildChannel },
        };
        FillArrays(json);
        FillArrays(json.Message);
        var restClient = new RestClient(new RestClientConfiguration { RequestHandler = _rest });
        var interaction = new ButtonInteraction(json, null, (_, cb, _, _) =>
        {
            lock (_callbacks) _callbacks.Add(cb);
            return Task.CompletedTask;
        }, restClient);
        return new ButtonInteractionContext(interaction, null!);
    }

    private static void SetContext(object module, ButtonInteractionContext ctx)
    {
        var t = module.GetType();
        FieldInfo? field = null;
        while (t is not null && field is null)
        {
            field = t.GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field is not null && field.FieldType != typeof(ButtonInteractionContext)) field = null;
            t = t.BaseType;
        }
        field!.SetValue(module, ctx);
    }

    private string LastMessage()
    {
        var cb = _callbacks.Last();
        var data = cb.GetType().GetProperty("Data")!.GetValue(cb) as InteractionMessageProperties;
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

        await _module.Signup(season.SeasonID);

        Assert.That(await _context.SeasonSignups.AnyAsync(s => s.SeasonID == season.SeasonID && s.UserID == user.Id), Is.True);
        Assert.That(LastMessage(), Does.StartWith("You have signed up for the season.")
            .And.EndWith($"[here](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)"));
    }

    [Test]
    public async Task Signup_AlreadySignedUp_RepliesWithoutDuplicating()
    {
        var user = await AddLinkedUser();
        var season = await SignupTestData.OpenSeason(_context);
        _context.SeasonSignups.Add(new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, TeamName = "x", Timestamp = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        await _module.Signup(season.SeasonID);

        Assert.That(LastMessage(), Does.StartWith("You are already signed up for this season."));
        Assert.That(await _context.SeasonSignups.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Signup_NoGamertag_SendsTempResponseAndDeletesIt()
    {
        // Note: TempResponse waits 5s (app code) before deleting the response.
        await AddLinkedUser(withGamertag: false);
        var season = await SignupTestData.OpenSeason(_context);

        await _module.Signup(season.SeasonID);

        Assert.That(LastMessage(), Is.EqualTo("You must set your gamertag first"));
        Assert.That(_rest.Requests, Has.Some.Matches<FakeRestRequestHandler.RecordedRequest>(r => r.Method == HttpMethod.Delete && r.Path.EndsWith("/messages/@original")));
        Assert.That(await _context.SeasonSignups.AnyAsync(), Is.False);
    }
}
