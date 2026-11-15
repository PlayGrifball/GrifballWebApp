using System.Collections.Concurrent;
using Anthropic;
using Anthropic.Core;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Chat;

/// <summary>
/// The chats' models (<see cref="ChatModels"/>) and a client for each: an Ollama server's (Ollama:Url, OllamaSharp) or Anthropic's API
/// (Anthropic:ApiKey, Anthropic's own SDK), one per model for the process. Each over an HTTP client that never times out itself: the
/// host's <see cref="ChatHost.Timeout"/> decides, as a cold model on the CPU can take minutes to start answering. Only the models:
/// GrifChat chooses one and calls the tools for it.
/// </summary>
public sealed class ChatClients : IChatClientFactory
{
    /// <summary>The Ollama model with Ollama:Url set and no Ollama:Model.</summary>
    public const string DefaultOllamaModel = "llama3.1:latest";

    /// <summary>The HTTP client Ollama is called through (<see cref="ChatClientsExtensions.AddChatModels"/>).</summary>
    public const string OllamaHttpClient = "Ollama";

    /// <summary>The HTTP client Anthropic's API is called through.</summary>
    public const string AnthropicHttpClient = "Anthropic";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _http;
    private readonly ConcurrentDictionary<string, IChatClient> _clients = new(StringComparer.Ordinal);
    private readonly Lazy<HttpClient> _ollama;
    private readonly Lazy<AnthropicClient> _anthropic;

    public ChatClients(IConfiguration configuration, IHttpClientFactory httpClients)
    {
        _configuration = configuration;
        _http = httpClients;
        Models = ChatModels.From(configuration);
        _ollama = new(() =>
        {
            var http = _http.CreateClient(OllamaHttpClient);
            http.BaseAddress = new Uri(_configuration[ChatModels.OllamaUrlKey]!);
            return http;
        });
        _anthropic = new(() => new AnthropicClient(new ClientOptions
        {
            ApiKey = _configuration[ChatModels.AnthropicApiKeyKey],
            HttpClient = _http.CreateClient(AnthropicHttpClient),
            // The host's timeout decides, not the SDK's own ten minutes
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        }));
    }

    public IReadOnlyList<ChatModelOption> Models { get; }

    public bool Available => Models.Count > 0;

    public IChatClient Create(ChatModelOption model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!Models.Contains(model))
            throw new ArgumentException($"{model.Id} is not one of the configured chat models", nameof(model));
        return _clients.GetOrAdd(model.Id, _ => model.Provider == ChatProvider.Anthropic
            ? _anthropic.Value.AsIChatClient(model.Model)
            : new OllamaApiClient(_ollama.Value, model.Model));
    }
}

/// <summary>
/// The chats' models from configuration: Chat:Models, in order, when it lists any; else the defaults, Claude Haiku, Sonnet and Opus
/// for sysadmins with Anthropic:ApiKey set (Haiku first: their default), then the Ollama model (Ollama:Model) for anyone with
/// Ollama:Url set. A model whose provider isn't configured, or an ID twice, is refused at startup.
/// </summary>
public static class ChatModels
{
    public const string ModelsKey = "Chat:Models";
    public const string OllamaUrlKey = "Ollama:Url";
    public const string OllamaModelKey = "Ollama:Model";
    public const string AnthropicApiKeyKey = "Anthropic:ApiKey";

    /// <summary>
    /// Claude, for sysadmins, at Anthropic's prices (US dollars per million tokens: input, output, cache writes for five minutes,
    /// cache reads); Haiku's are for prompts up to 100,000 tokens, as a chat's are.
    /// </summary>
    public static readonly IReadOnlyList<ChatModelOption> Claude =
    [
        new("claude-haiku", "Claude Haiku 5.5", ChatProvider.Anthropic, "claude-haiku-5-5")
            { Roles = [SiteRoles.Sysadmin], Price = new(Input: 0.10m, Output: 0.50m, CacheWrite: 0.125m, CacheRead: 0.01m) },
        new("claude-sonnet", "Claude Sonnet 5.5", ChatProvider.Anthropic, "claude-sonnet-5-5")
            { Roles = [SiteRoles.Sysadmin], Price = new(Input: 2m, Output: 10m, CacheWrite: 2.50m, CacheRead: 0.10m) },
        new("claude-opus", "Claude Opus 5.5", ChatProvider.Anthropic, "claude-opus-5-5")
            { Roles = [SiteRoles.Sysadmin], Price = new(Input: 4m, Output: 20m, CacheWrite: 5m, CacheRead: 0.20m) },
    ];

    public static IReadOnlyList<ChatModelOption> From(IConfiguration configuration)
    {
        var ollama = configuration[OllamaUrlKey] is { Length: > 0 };
        var anthropic = configuration[AnthropicApiKeyKey] is { Length: > 0 };
        var listed = configuration.GetSection(ModelsKey).Get<List<ChatModelSettings>>() ?? [];
        List<ChatModelOption> models = listed.Count > 0
            ? listed.Select(s => s.ToModel()).ToList()
            : [.. anthropic ? Claude : [], .. ollama ? [OllamaModel(configuration)] : Array.Empty<ChatModelOption>()];

        foreach (var model in models)
        {
            if (model.Provider == ChatProvider.Ollama && !ollama)
                throw new InvalidOperationException($"{ModelsKey} lists {model.Id} on Ollama, and {OllamaUrlKey} is not set");
            if (model.Provider == ChatProvider.Anthropic && !anthropic)
                throw new InvalidOperationException($"{ModelsKey} lists {model.Id} on Anthropic, and {AnthropicApiKeyKey} is not set");
        }
        if (models.GroupBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new InvalidOperationException($"{ModelsKey} lists {twice.Key} more than once");
        return models;
    }

    /// <summary>The Ollama model for anyone, named as it is (Ollama:Model, else <see cref="ChatClients.DefaultOllamaModel"/>).</summary>
    private static ChatModelOption OllamaModel(IConfiguration configuration)
    {
        var model = configuration[OllamaModelKey] is { Length: > 0 } configured ? configured : ChatClients.DefaultOllamaModel;
        return new ChatModelOption("ollama", model, ChatProvider.Ollama, model);
    }

    /// <summary>One of Chat:Models, as configuration binds it.</summary>
    private sealed class ChatModelSettings
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public ChatProvider Provider { get; set; }
        public string Model { get; set; } = "";
        public List<string> Roles { get; set; } = [];
        public PriceSettings? Price { get; set; }

        public ChatModelOption ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Model))
                throw new InvalidOperationException($"Each of {ModelsKey} needs an Id and a Model");
            if (Id.Length > Domain.Players.ChatChoice.ModelMaxLength)
                throw new InvalidOperationException($"{ModelsKey}: {Id} is longer than {Domain.Players.ChatChoice.ModelMaxLength} characters");
            return new(Id, string.IsNullOrWhiteSpace(Name) ? Model : Name, Provider, Model)
            {
                Roles = Roles,
                Price = Price is null ? null : new(Price.Input, Price.Output, Price.CacheWrite, Price.CacheRead),
            };
        }
    }

    private sealed class PriceSettings
    {
        public decimal Input { get; set; }
        public decimal Output { get; set; }
        public decimal CacheWrite { get; set; }
        public decimal CacheRead { get; set; }
    }
}

public static class ChatClientsExtensions
{
    /// <summary>
    /// The chats' models (<see cref="ChatClients"/>), and their HTTP clients: no timeout of their own (the hosts' decide), their
    /// connections renewed every few minutes so a moved server is found again.
    /// </summary>
    public static IServiceCollection AddChatModels(this IServiceCollection services)
    {
        foreach (var name in new[] { ChatClients.OllamaHttpClient, ChatClients.AnthropicHttpClient })
        {
            services.AddHttpClient(name, client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
        }
        services.AddSingleton<IChatClientFactory, ChatClients>();
        return services;
    }
}
