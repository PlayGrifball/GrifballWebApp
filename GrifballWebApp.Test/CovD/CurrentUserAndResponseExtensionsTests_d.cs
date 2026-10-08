using System.Net;
using System.Security.Claims;
using System.Text;
using DiscordInterface.Generated;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Extensions;
using Microsoft.AspNetCore.Http;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using NSubstitute;

namespace GrifballWebApp.Test.CovD;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CurrentUserServiceTests_d
{
    private IHttpContextAccessor _accessor;
    private CurrentUserService _service;

    [SetUp]
    public void Setup()
    {
        _accessor = Substitute.For<IHttpContextAccessor>();
        _service = new CurrentUserService(_accessor);
    }

    private static ClaimsPrincipal Principal(string? id, bool authenticated = true)
    {
        var claims = id is null ? new List<Claim>() : [new Claim(ClaimTypes.NameIdentifier, id)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
    }

    [Test]
    public void GetCurrentUserId_FromHttpContextClaims()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12") });

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(12));
    }

    [Test]
    public void GetCurrentUserId_NoHttpContext_ReturnsNull()
    {
        _accessor.HttpContext.Returns((HttpContext?)null);

        Assert.That(_service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void GetCurrentUserId_UnauthenticatedOrBadClaim_ReturnsNull()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12", authenticated: false) });
        Assert.That(_service.GetCurrentUserId(), Is.Null);

        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("not-int") });
        Assert.That(_service.GetCurrentUserId(), Is.Null);

        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal(null) });
        Assert.That(_service.GetCurrentUserId(), Is.Null);
    }

    [Test]
    public void SetCurrentUserId_TakesPrecedenceAndIsCached()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = Principal("12") });
        _service.SetCurrentUserId(99);

        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(99));
        _ = _accessor.DidNotReceive().HttpContext;
    }

    [Test]
    public void SetCurrentUserIdFromClaims_NullIsIgnored_ValidIsStored()
    {
        _service.SetCurrentUserIdFromClaims(null);
        _accessor.HttpContext.Returns((HttpContext?)null);
        Assert.That(_service.GetCurrentUserId(), Is.Null);

        _service.SetCurrentUserIdFromClaims(Principal("7"));
        Assert.That(_service.GetCurrentUserId(), Is.EqualTo(7));
    }
}

/// <summary>Records NetCord REST calls; DELETE -> 204, everything else -> a minimal message JSON.</summary>
internal sealed class RecordingRestHandler_d : IRestRequestHandler
{
    public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests) Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
        if (request.Method == HttpMethod.Delete)
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        const string json = """
        {"id":"1000","channel_id":"6","author":{"id":"2000","username":"bot","discriminator":"0","global_name":null,"avatar":null},
         "content":"","timestamp":"2024-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,
         "mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"type":0}
        """;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    public void AddDefaultHeader(string name, IEnumerable<string> values) { }
    public void Dispose() { }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class InteractionResponseExtensionsTests_d
{
    private readonly List<InteractionCallback> _callbacks = [];
    private RecordingRestHandler_d _rest;
    private ButtonInteractionContext _ctx;

    [SetUp]
    public void Setup()
    {
        _rest = new RecordingRestHandler_d();
        var json = new JsonInteraction
        {
            Id = 1,
            ApplicationId = 2,
            Type = (InteractionType)3,
            Token = "tok",
            User = new JsonUser { Id = 3, Username = "u" },
            Data = new JsonInteractionData { CustomId = "x", ComponentType = ComponentType.Button },
            Message = new JsonMessage { Id = 5, ChannelId = 6, Author = new JsonUser { Id = 7, Username = "bot" } },
            Channel = new JsonChannel { Id = 6, Type = ChannelType.TextGuildChannel },
        };
        foreach (var o in new object[] { json, json.Message })
            foreach (var p in o.GetType().GetProperties())
                if (p.PropertyType.IsArray && p.CanWrite && p.GetValue(o) is null)
                    p.SetValue(o, Array.CreateInstance(p.PropertyType.GetElementType()!, 0));
        var interaction = new ButtonInteraction(json, null, (_, cb, _, _) => { _callbacks.Add(cb); return Task.CompletedTask; },
            new RestClient(new RestClientConfiguration { RequestHandler = _rest }));
        _ctx = new ButtonInteractionContext(interaction, null!);
    }

    private InteractionMessageProperties SentMessage() =>
        (InteractionMessageProperties)_callbacks.Single().GetType().GetProperty("Data")!.GetValue(_callbacks.Single())!;

    [Test]
    public async Task EphemeralResponse_NetCordContext_SendsEphemeralMessage()
    {
        await _ctx.EphemeralResponse("hello");

        Assert.That(SentMessage().Content, Is.EqualTo("hello"));
        Assert.That(SentMessage().Flags, Is.EqualTo(MessageFlags.Ephemeral));
        Assert.That(_rest.Requests, Is.Empty);
    }

    [Test]
    public async Task TempResponse_NetCordContext_SendsThenDeletesOriginal()
    {
        await _ctx.TempResponse("temp"); // waits 5s in app code

        Assert.That(SentMessage().Content, Is.EqualTo("temp"));
        Assert.That(_rest.Requests.Single().Method, Is.EqualTo(HttpMethod.Delete));
        Assert.That(_rest.Requests.Single().Path, Does.EndWith("/webhooks/2/tok/messages/@original"));
    }

    [Test]
    public async Task ModifyTempResponse_NetCordContext_PatchesThenDeletesOriginal()
    {
        await _ctx.ModifyTempResponse("modified"); // waits 5s in app code

        Assert.That(_callbacks, Is.Empty);
        Assert.That(_rest.Requests.Select(r => r.Method), Is.EqualTo(new[] { HttpMethod.Patch, HttpMethod.Delete }));
        Assert.That(_rest.Requests[0].Path, Does.EndWith("/webhooks/2/tok/messages/@original"));
        Assert.That(_rest.Requests[0].Body, Does.Contain("\"content\":\"modified\""));
    }

    [Test]
    public async Task EphemeralResponse_DiscordInterfaceContext_SendsEphemeralMessage()
    {
        var ctx = Substitute.For<IDiscordInteractionContext>();

        await ctx.EphemeralResponse("hi there");

        await ctx.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallback>(cb =>
            ((InteractionMessageProperties)cb.GetType().GetProperty("Data")!.GetValue(cb)!).Content == "hi there" &&
            ((InteractionMessageProperties)cb.GetType().GetProperty("Data")!.GetValue(cb)!).Flags == MessageFlags.Ephemeral));
    }
}
