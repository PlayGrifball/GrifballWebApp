using Anthropic;
using Anthropic.Core;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Chat;

/// <summary>
/// The chats' models (<see cref="ChatModels"/>, as <see cref="ChatClientsExtensions.AddChatModels"/> checked them) and a client for
/// each: an Ollama server's (Ollama:Url, OllamaSharp) or Anthropic's API (Anthropic:ApiKey, Anthropic's own SDK), over one HTTP client
/// a provider (and one Anthropic client) for the process. A new client each time one is asked for: ChatModel keeps the one it builds
/// for each model. Each HTTP client never times out itself: the host's <see cref="ChatHost.Timeout"/> decides, as a cold model on the
/// CPU can take minutes to start answering. Only the models: GrifChat chooses one and calls the tools for it.
/// </summary>
public sealed class ChatClients : IChatClientFactory
{
    /// <summary>The Ollama model with Ollama:Url set and no Ollama:Model.</summary>
    public const string DefaultOllamaModel = "llama3.1:latest";

    /// <summary>The HTTP client Ollama is called through (<see cref="ChatClientsExtensions.AddChatModels"/>).</summary>
    public const string OllamaHttpClient = "Ollama";

    /// <summary>The HTTP client Anthropic's API is called through.</summary>
    public const string AnthropicHttpClient = "Anthropic";

    private readonly Lazy<HttpClient> _ollama;
    private readonly Lazy<AnthropicClient> _anthropic;
    private readonly ILogger<AnthropicSpendLimit> _spendLimitLog;

    /// <param name="models">The models, checked (<see cref="ChatModels.From"/>).</param>
    /// <param name="configuration">Where the providers are: Ollama:Url, Anthropic:ApiKey.</param>
    /// <param name="httpClients">The providers' HTTP clients (<see cref="OllamaHttpClient"/>, <see cref="AnthropicHttpClient"/>).</param>
    /// <param name="loggers">For Claude's spend limits (<see cref="AnthropicSpendLimit"/>).</param>
    public ChatClients(IReadOnlyList<ChatModelOption> models, IConfiguration configuration, IHttpClientFactory httpClients, ILoggerFactory loggers)
    {
        Models = models;
        _spendLimitLog = loggers.CreateLogger<AnthropicSpendLimit>();
        _ollama = new(() =>
        {
            var http = httpClients.CreateClient(OllamaHttpClient);
            http.BaseAddress = new Uri(configuration[ChatModels.OllamaUrlKey]!);
            return http;
        });
        _anthropic = new(() => new AnthropicClient(new ClientOptions
        {
            ApiKey = configuration[ChatModels.AnthropicApiKeyKey],
            HttpClient = httpClients.CreateClient(AnthropicHttpClient),
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
        return model.Provider == ChatProvider.Anthropic
            ? new AnthropicSpendLimit(new AnthropicPromptCache(_anthropic.Value.AsIChatClient(model.Model), model.Model), _spendLimitLog)
            : new OllamaApiClient(_ollama.Value, model.Model);
    }
}

/// <summary>
/// The chats' models from configuration: Chat:Models, in order, when it lists any; else the defaults, Claude Haiku, Sonnet and Opus
/// for sysadmins with Anthropic:ApiKey set (Haiku first: their default), then the Ollama model (Ollama:Model) for anyone with
/// Ollama:Url set. A model whose provider isn't configured or named, a negative price, or an ID twice, is refused as the app starts
/// (<see cref="ChatClientsExtensions.AddChatModels"/>).
/// </summary>
public static class ChatModels
{
    public const string ModelsKey = "Chat:Models";
    public const string OllamaUrlKey = "Ollama:Url";
    public const string OllamaModelKey = "Ollama:Model";
    public const string AnthropicApiKeyKey = "Anthropic:ApiKey";

    /// <summary>The most a call to Claude may write, its thinking included: room for a long answer after thinking, at most a few cents.</summary>
    public const int ClaudeMaxOutputTokens = 16_000;

    /// <summary>
    /// Claude, for sysadmins, at Anthropic's prices (US dollars per million tokens: input, output, cache writes for five minutes,
    /// cache reads); Haiku's are for prompts up to 100,000 tokens, as a chat's are.
    /// </summary>
    public static readonly IReadOnlyList<ChatModelOption> Claude =
    [
        new("claude-haiku", "Claude Haiku 5.5", ChatProvider.Anthropic, "claude-haiku-5-5")
            { Roles = [SiteRoles.Sysadmin], MaxOutputTokens = ClaudeMaxOutputTokens, Price = new(Input: 0.10m, Output: 0.50m, CacheWrite: 0.125m, CacheRead: 0.01m) },
        new("claude-sonnet", "Claude Sonnet 5.5", ChatProvider.Anthropic, "claude-sonnet-5-5")
            { Roles = [SiteRoles.Sysadmin], MaxOutputTokens = ClaudeMaxOutputTokens, Price = new(Input: 2m, Output: 10m, CacheWrite: 2.50m, CacheRead: 0.10m) },
        new("claude-opus", "Claude Opus 5.5", ChatProvider.Anthropic, "claude-opus-5-5")
            { Roles = [SiteRoles.Sysadmin], MaxOutputTokens = ClaudeMaxOutputTokens, Price = new(Input: 4m, Output: 20m, CacheWrite: 5m, CacheRead: 0.20m) },
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
        public ChatProvider? Provider { get; set; }
        public string Model { get; set; } = "";
        public List<string> Roles { get; set; } = [];
        public PriceSettings? Price { get; set; }
        public int? MaxOutputTokens { get; set; }

        public ChatModelOption ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Model))
                throw new InvalidOperationException($"Each of {ModelsKey} needs an Id and a Model");
            if (Id.Length > Domain.Players.ChatChoice.ModelMaxLength)
                throw new InvalidOperationException($"{ModelsKey}: {Id} is longer than {Domain.Players.ChatChoice.ModelMaxLength} characters");
            if (Provider is not { } provider)
                throw new InvalidOperationException($"{ModelsKey}: {Id} needs a Provider (Ollama or Anthropic)");
            if (Price is { } p && (p.Input < 0 || p.Output < 0 || p.CacheWrite < 0 || p.CacheRead < 0))
                throw new InvalidOperationException($"{ModelsKey}: {Id} has a negative price");
            return new(Id, string.IsNullOrWhiteSpace(Name) ? Model : Name, provider, Model)
            {
                Roles = Roles,
                MaxOutputTokens = MaxOutputTokens,
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
    /// connections renewed every few minutes so a moved server is found again. The models are read and checked here, once, as the app
    /// starts (<see cref="ChatModels.From"/>): a mistake in them stops it, rather than every chat, and the Discord bot, when first asked;
    /// the list checked is the one served.
    /// </summary>
    public static IServiceCollection AddChatModels(this IServiceCollection services, IConfiguration configuration)
    {
        var models = ChatModels.From(configuration);
        foreach (var name in new[] { ChatClients.OllamaHttpClient, ChatClients.AnthropicHttpClient })
        {
            services.AddHttpClient(name, client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
        }
        services.AddSingleton<IChatClientFactory>(provider => new ChatClients(models, configuration, provider.GetRequiredService<IHttpClientFactory>(), provider.GetRequiredService<ILoggerFactory>()));
        return services;
    }
}
