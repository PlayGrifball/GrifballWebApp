using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Test;

/// <summary>The state and helpers DiscordInteractionPipelineTests's unit and integration tests share.</summary>
public abstract class DiscordInteractionPipelineFixture
{
    protected const ulong DiscordId = 4242;

    protected readonly List<InteractionCallbackProperties> _callbacks = [];
    protected readonly FakeRestRequestHandler _rest = new();
    protected readonly RecordingLogger<DiscordInteractionPipelineFixture> _logger = new();
    protected RestException? _responseError;
    protected readonly IServiceProvider _ambientOnly = new ServiceCollection().AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>().BuildServiceProvider();

    protected Task<InteractionCallbackResponse?> SendResponse(IInteraction _, InteractionCallbackProperties callback, bool __, RestRequestProperties? ___, CancellationToken ____)
    {
        if (_responseError is not null)
            throw _responseError;
        _callbacks.Add(callback);
        return Task.FromResult<InteractionCallbackResponse?>(null);
    }

    protected ButtonInteractionContext Button(string customId = "signup:1", ulong userId = DiscordId)
    {
        var json = DiscordInteractionJson.Button(customId, userId);
        var interaction = new ButtonInteraction(json, null, SendResponse, new RestClient(new RestClientConfiguration { RequestHandler = _rest }));
        return new ButtonInteractionContext(interaction, null!);
    }

    protected ApplicationCommandContext SlashCommand(string name = "setgamertag")
    {
        var json = DiscordInteractionJson.SlashCommand(name, DiscordId);
        var interaction = new SlashCommandInteraction(json, null, SendResponse, new RestClient(new RestClientConfiguration { RequestHandler = _rest }));
        return new ApplicationCommandContext(interaction, null!);
    }

    protected InteractionMessageProperties SentMessage() =>
        ((InteractionCallbackProperties<InteractionMessageProperties>)_callbacks.Single()).Data;
}
