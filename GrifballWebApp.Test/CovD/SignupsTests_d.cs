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

namespace GrifballWebApp.Test.CovD;

internal static class SignupSeed_d
{
    internal static async Task<Season> OpenSeason(GrifballContext ctx, bool open = true, string name = "Signup Season")
    {
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = name,
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = open ? now.AddDays(-5) : now.AddDays(-10),
            SignupsClose = open ? now.AddDays(5) : now.AddDays(-1),
        };
        ctx.Seasons.Add(season);
        await ctx.SaveChangesAsync();
        return season;
    }

    /// <summary>Adds availability options (with odd minutes so they never clash with seeded options) and attaches them to the season.</summary>
    internal static async Task<AvailabilityOption[]> Options(GrifballContext ctx, Season season, params (DayOfWeek day, TimeOnly time)[] slots)
    {
        var options = slots.Select(s => new AvailabilityOption { DayOfWeek = s.day, Time = s.time }).ToArray();
        ctx.Availability.AddRange(options);
        await ctx.SaveChangesAsync();
        ctx.SeasonAvailability.AddRange(options.Select(o => new SeasonAvailability { SeasonID = season.SeasonID, AvailabilityOptionID = o.AvailabilityOptionID }));
        await ctx.SaveChangesAsync();
        return options;
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupsServiceTimeslotTests_d
{
    private GrifballContext _context;
    private SignupsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SignupsService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetTimeslots_PositiveOffsetPastMidnight_MovesToNextDay()
    {
        var season = await SignupSeed_d.OpenSeason(_context);
        await SignupSeed_d.Options(_context, season, (DayOfWeek.Saturday, new TimeOnly(23, 17)), (DayOfWeek.Monday, new TimeOnly(10, 17)));

        var slots = await _service.GetTimeslots(season.SeasonID, 120, userID: 0);

        Assert.That(slots, Has.Length.EqualTo(2));
        // Saturday 23:17 + 2h => Sunday 01:17, which sorts first (Sunday = 0)
        Assert.That(slots[0].DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(slots[0].Time, Is.EqualTo(new TimeOnly(1, 17)));
        Assert.That(slots[1].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(slots[1].Time, Is.EqualTo(new TimeOnly(12, 17)));
        Assert.That(slots.All(s => s.IsChecked is false));
    }

    [Test]
    public async Task GetTimeslots_NegativeOffsetBeforeMidnight_CurrentBehaviour()
    {
        var season = await SignupSeed_d.OpenSeason(_context);
        await SignupSeed_d.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(1, 17)), (DayOfWeek.Wednesday, new TimeOnly(15, 17)));

        var slots = await _service.GetTimeslots(season.SeasonID, -120, userID: 0);

        var shifted = slots.Single(s => s.Time == new TimeOnly(23, 17));
        // BUG: SignupsService.GetTimeslots uses TimeSpan.Days on a negative span (01:17 - 2h = -00:43), which truncates
        // to 0, so the slot stays on Monday instead of rolling back to Sunday 23:17.
        Assert.That(shifted.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        var other = slots.Single(s => s.Time == new TimeOnly(13, 17));
        Assert.That(other.DayOfWeek, Is.EqualTo(DayOfWeek.Wednesday));
    }

    [Test]
    public async Task UpsertSignup_AddsAndRemovesAvailability()
    {
        var season = await SignupSeed_d.OpenSeason(_context);
        var opts = await SignupSeed_d.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(1, 17)), (DayOfWeek.Tuesday, new TimeOnly(2, 17)));
        var user = new User { UserName = "signer" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _service.UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID, UserID = user.Id, TeamName = "T",
            Timeslots = [new TimeslotDto { OptionID = opts[0].AvailabilityOptionID, IsChecked = true }, new TimeslotDto { OptionID = opts[1].AvailabilityOptionID, IsChecked = false }],
        });

        var first = await _service.GetTimeslots(season.SeasonID, 0, user.Id);
        Assert.That(first.Where(s => s.IsChecked).Select(s => s.OptionID), Is.EqualTo(new[] { opts[0].AvailabilityOptionID }));

        _context.ChangeTracker.Clear();
        await _service.UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID, UserID = user.Id, TeamName = "T2",
            Timeslots = [new TimeslotDto { OptionID = opts[1].AvailabilityOptionID, IsChecked = true }],
        });

        var second = await _service.GetSignup(season.SeasonID, 0, user.Id);
        Assert.That(second!.TeamName, Is.EqualTo("T2"));
        Assert.That(second.Timeslots.Where(s => s.IsChecked).Select(s => s.OptionID), Is.EqualTo(new[] { opts[1].AvailabilityOptionID }));
        Assert.That(await _context.SignupAvailability.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task UpsertSignup_OptionNotInSeason_Throws()
    {
        var season = await SignupSeed_d.OpenSeason(_context);
        var otherSeason = await SignupSeed_d.OpenSeason(_context, name: "Other Season");
        var foreign = await SignupSeed_d.Options(_context, otherSeason, (DayOfWeek.Friday, new TimeOnly(4, 17)));
        var user = new User { UserName = "signer2" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID, UserID = user.Id,
            Timeslots = [new TimeslotDto { OptionID = foreign[0].AvailabilityOptionID, IsChecked = true }],
        }));
        Assert.That(ex!.Message, Is.EqualTo($"Cannot add options for season {season.SeasonID}. The following are not valid {foreign[0].AvailabilityOptionID}"));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupsControllerTests_d
{
    private GrifballContext _context;
    private SignupsController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new SignupsController(new SignupsService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> AddUser(string name = "player")
    {
        var user = new User { UserName = name, XboxUser = name == "player" ? new XboxUser { XboxUserID = 777, Gamertag = "PlayerGT" } : null };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public void Authorization_SignupAndUpsertRequirePlayerOrSysadmin()
    {
        var t = typeof(SignupsController);
        Assert.Multiple(() =>
        {
            Assert.That(ControllerTestHelpers_d.ClassAuthorize(t), Is.Null);
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(SignupsController.GetSignup))!.Roles, Is.EqualTo("Player,Sysadmin"));
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(SignupsController.UpsertSignup))!.Roles, Is.EqualTo("Player,Sysadmin"));
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(SignupsController.GetSignups)), Is.Null);
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(SignupsController.GetTimeslots)), Is.Null);
        });
    }

    [Test]
    public async Task GetSignupDateInfo_UsesPersonIdClaim()
    {
        var user = await AddUser();
        var season = await SignupSeed_d.OpenSeason(_context);
        _context.SeasonSignups.Add(new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, TeamName = "x", Timestamp = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("PersonID", user.Id.ToString())], "Test")),
            },
        };

        var result = (SignupDateInfoDto[])((OkObjectResult)await _controller.GetSignupDateInfo(CancellationToken.None)).Value!;

        Assert.That(result.Single(r => r.SeasonID == season.SeasonID).IsSignedUp, Is.True);
    }

    [Test]
    public async Task GetSignups_ReturnsSignupsForSeason()
    {
        var user = await AddUser();
        var season = await SignupSeed_d.OpenSeason(_context);
        _context.SeasonSignups.Add(new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, TeamName = "x", Timestamp = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _controller.GetSignups(season.SeasonID, CancellationToken.None);

        Assert.That(result.Single().PersonName, Is.EqualTo("PlayerGT"));
    }

    [Test]
    public async Task UserScopedActions_WithoutUser_Forbid()
    {
        _controller.WithAnonymousUser();

        Assert.That(await _controller.GetTimeslots(1, 0, CancellationToken.None), Is.TypeOf<ForbidResult>());
        Assert.That(await _controller.GetSignup(1, 0, CancellationToken.None), Is.TypeOf<ForbidResult>());
        Assert.That(await _controller.UpsertSignup(new SignupRequestDto { SeasonID = 1 }, CancellationToken.None), Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task UserScopedActions_NonNumericId_Forbid()
    {
        _controller.WithUser("abc");

        Assert.That(await _controller.GetTimeslots(1, 0, CancellationToken.None), Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task UpsertSignup_UsesUserIdFromClaims_NotBody()
    {
        var user = await AddUser();
        var other = await AddUser("other");
        var season = await SignupSeed_d.OpenSeason(_context);
        _controller.WithUser(user.Id.ToString(), "player", "Player");

        var result = await _controller.UpsertSignup(new SignupRequestDto { SeasonID = season.SeasonID, UserID = other.Id, TeamName = "Mine" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        var signup = await _context.SeasonSignups.SingleAsync();
        Assert.That(signup.UserID, Is.EqualTo(user.Id));

        var get = (SignupResponseDto)((OkObjectResult)await _controller.GetSignup(season.SeasonID, 0, CancellationToken.None)).Value!;
        Assert.That(get.TeamName, Is.EqualTo("Mine"));
        var slots = (TimeslotDto[])((OkObjectResult)await _controller.GetTimeslots(season.SeasonID, 0, CancellationToken.None)).Value!;
        Assert.That(slots, Is.Empty);
    }

    [Test]
    public async Task UpsertSignup_SignupsClosed_ReturnsBadRequest()
    {
        var user = await AddUser();
        var season = await SignupSeed_d.OpenSeason(_context, open: false);
        _controller.WithUser(user.Id.ToString());

        var result = await _controller.UpsertSignup(new SignupRequestDto { SeasonID = season.SeasonID }, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Signups are closed for this season"));
    }
}

/// <summary>Records NetCord REST calls and answers 204.</summary>
internal sealed class NoContentRestHandler_d : IRestRequestHandler
{
    public List<(HttpMethod Method, string Path)> Requests { get; } = [];

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        lock (Requests) Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    public void AddDefaultHeader(string name, IEnumerable<string> values) { }
    public void Dispose() { }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupButtonInteractionsTests_d
{
    private GrifballContext _context;
    private SignupButtonInteractions _module;
    private NoContentRestHandler_d _rest;
    private readonly List<InteractionCallback> _callbacks = [];
    private const ulong DiscordId = 9876;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        _module = new SignupButtonInteractions(_context, new SignupsService(_context), urls);
        _rest = new NoContentRestHandler_d();
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
        var season = await SignupSeed_d.OpenSeason(_context);

        await _module.Signup(season.SeasonID);

        Assert.That(await _context.SeasonSignups.AnyAsync(s => s.SeasonID == season.SeasonID && s.UserID == user.Id), Is.True);
        Assert.That(LastMessage(), Does.StartWith("You have signed up for the season.")
            .And.EndWith($"[here](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)"));
    }

    [Test]
    public async Task Signup_AlreadySignedUp_RepliesWithoutDuplicating()
    {
        var user = await AddLinkedUser();
        var season = await SignupSeed_d.OpenSeason(_context);
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
        var season = await SignupSeed_d.OpenSeason(_context);

        await _module.Signup(season.SeasonID);

        Assert.That(LastMessage(), Is.EqualTo("You must set your gamertag first"));
        Assert.That(_rest.Requests, Has.Some.Matches<(HttpMethod Method, string Path)>(r => r.Method == HttpMethod.Delete && r.Path.EndsWith("/messages/@original")));
        Assert.That(await _context.SeasonSignups.AnyAsync(), Is.False);
    }
}
