using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Server.Extensions;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class InteractionResponseExtensionsTests
{
    private readonly List<InteractionCallbackProperties> _callbacks = [];
    private FakeRestRequestHandler _rest;
    private IDiscordButtonInteractionContext _ctx;

    [SetUp]
    public void Setup()
    {
        _rest = new FakeRestRequestHandler();
        var json = DiscordInteractionJson.Button("x", 3);
        var interaction = new ButtonInteraction(json, null, (_, cb, _, _, _) => { _callbacks.Add(cb); return Task.FromResult<InteractionCallbackResponse?>(null); },
            new RestClient(new RestClientConfiguration { RequestHandler = _rest }));
        // A real NetCord context behind our wrapper, so these check what actually reaches Discord
        _ctx = new ButtonInteractionContext(interaction, null!).ToDiscordContext();
    }

    private InteractionMessageProperties SentMessage() =>
        ((InteractionCallbackProperties<InteractionMessageProperties>)_callbacks.Single()).Data;

    [Test]
    public async Task EphemeralResponse_WrappedNetCordContext_SendsEphemeralMessage()
    {
        await _ctx.EphemeralResponse("hello");

        Assert.That(SentMessage().Content, Is.EqualTo("hello"));
        Assert.That(SentMessage().Flags, Is.EqualTo(MessageFlags.Ephemeral));
        Assert.That(_rest.Requests, Is.Empty);
    }

    [Test]
    public async Task TempResponse_WrappedNetCordContext_SendsThenDeletesOriginal()
    {
        await _ctx.TempResponse("temp");

        Assert.That(SentMessage().Content, Is.EqualTo("temp"));
        Assert.That(_rest.Requests.Single().Method, Is.EqualTo(HttpMethod.Delete));
        Assert.That(_rest.Requests.Single().Path, Does.EndWith("/webhooks/2/tok/messages/@original"));
    }

    [Test]
    public async Task ModifyTempResponse_WrappedNetCordContext_PatchesThenDeletesOriginal()
    {
        await _ctx.ModifyTempResponse("modified");

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

        await ctx.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(cb =>
            ((InteractionCallbackProperties<InteractionMessageProperties>)cb).Data.Content == "hi there" &&
            ((InteractionCallbackProperties<InteractionMessageProperties>)cb).Data.Flags == MessageFlags.Ephemeral));
    }
}
