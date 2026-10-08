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

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class InteractionResponseExtensionsTests
{
    private readonly List<InteractionCallback> _callbacks = [];
    private FakeRestRequestHandler _rest;
    private ButtonInteractionContext _ctx;

    [SetUp]
    public void Setup()
    {
        _rest = new FakeRestRequestHandler();
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
