using Anthropic;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Infrastructure.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The chats' models as configuration gives them (<see cref="ChatModels"/>) and their clients (<see cref="ChatClients"/>): Ollama's for
/// anyone, Claude's for sysadmins, over one HTTP client and one Anthropic client for all models, no tool calls of their own.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ChatClientsUnitTests
{
    private const string OllamaUrl = "http://ollama.test:11434/";

    private static readonly string[] DefaultIds = ["claude-haiku", "claude-sonnet", "claude-opus", "ollama"];
    private static readonly string[] ClaudeModels = ["claude-haiku-5-5", "claude-sonnet-5-5", "claude-opus-5-5"];
    private static readonly string[] Commissioners = ["Commissioner"];
    private static readonly string[] OllamaModels = ["qwen3:4b", "qwen3:8b"];

    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>The chats' models as AddChatModels registers them, with <paramref name="settings"/>.</summary>
    private static ServiceProvider Services(params (string Key, string? Value)[] settings)
    {
        var configuration = Configuration(settings);
        return new ServiceCollection().AddSingleton(configuration).AddChatModels(configuration).BuildServiceProvider();
    }

    [Test]
    public void Ollama_IsTheConfiguredServer_ForAnyone_WithNoToolCallsOfItsOwn()
    {
        using var services = Services((ChatModels.OllamaUrlKey, OllamaUrl));
        var clients = services.GetRequiredService<IChatClientFactory>();
        var model = clients.Models.Single();
        var client = clients.Create(model);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clients, Is.TypeOf<ChatClients>());
            Assert.That(clients.Available, Is.True);
            Assert.That((model.Id, model.Name, model.Provider, model.IsOpen, model.Price), Is.EqualTo(
                ("ollama", ChatClients.DefaultOllamaModel, ChatProvider.Ollama, true, (ChatPrice?)null)), "Named as Ollama runs it, free");
            // GrifChat calls the tools, whatever the provider
            Assert.That(client, Is.TypeOf<OllamaApiClient>());
            Assert.That(client.GetService<FunctionInvokingChatClient>(), Is.Null);
            Assert.That(client.GetService<OllamaApiClient>()?.Uri, Is.EqualTo(new Uri(OllamaUrl)));
            Assert.That(client.GetService<OllamaApiClient>()?.SelectedModel, Is.EqualTo(ChatClients.DefaultOllamaModel));
        }
    }

    [TestCase("qwen3:8b", "qwen3:8b")]
    [TestCase("", ChatClients.DefaultOllamaModel)]
    public void Ollama_IsTheModelAsked_ElseTheDefault(string model, string asked)
    {
        using var services = Services((ChatModels.OllamaUrlKey, OllamaUrl), (ChatModels.OllamaModelKey, model));
        var clients = services.GetRequiredService<IChatClientFactory>();

        Assert.That(clients.Create(clients.Models.Single()).GetService<OllamaApiClient>()?.SelectedModel, Is.EqualTo(asked));
    }

    [Test]
    public void WithAnApiKey_ClaudeHaikuSonnetAndOpus_ForSysadmins_HaikuFirst_ThenOllama()
    {
        using var services = Services((ChatModels.OllamaUrlKey, OllamaUrl), (ChatModels.AnthropicApiKeyKey, "sk-test"));
        var clients = services.GetRequiredService<IChatClientFactory>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clients.Models.Select(m => m.Id), Is.EqualTo(DefaultIds));
            Assert.That(clients.Models.Take(3).Select(m => (m.Provider, m.Roles.Single(), m.Price is not null)),
                Is.All.EqualTo((ChatProvider.Anthropic, SiteRoles.Sysadmin, true)));
            Assert.That(clients.Models.Take(3).Select(m => m.Model), Is.EqualTo(ClaudeModels));
            Assert.That(clients.Create(clients.Models[0]).GetService<IAnthropicClient>(), Is.Not.Null, "Anthropic's own client");
            Assert.That(clients.Create(clients.Models[0]).GetService<AnthropicSpendLimit>(), Is.Not.Null, "Its spend limits said as the chat's refusal");
            Assert.That(clients.Create(clients.Models[0]).GetService<FunctionInvokingChatClient>(), Is.Null);
        }
    }

    [Test]
    public void ChatModels_WhenListed_AreTheListInItsOrder()
    {
        var configuration = Configuration(
            (ChatModels.OllamaUrlKey, OllamaUrl), (ChatModels.AnthropicApiKeyKey, "sk-test"),
            ("Chat:Models:0:Id", "local"), ("Chat:Models:0:Name", "Grif"), ("Chat:Models:0:Provider", "Ollama"), ("Chat:Models:0:Model", "qwen3:4b"),
            ("Chat:Models:1:Id", "haiku"), ("Chat:Models:1:Provider", "Anthropic"), ("Chat:Models:1:Model", "claude-haiku-5-5"),
            ("Chat:Models:1:Roles:0", "Commissioner"), ("Chat:Models:1:Price:Input", "0.1"), ("Chat:Models:1:Price:Output", "0.5"));

        var models = ChatModels.From(configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(models.Select(m => (m.Id, m.Name, m.Provider, m.Model)), Is.EqualTo(new[]
            {
                ("local", "Grif", ChatProvider.Ollama, "qwen3:4b"),
                ("haiku", "claude-haiku-5-5", ChatProvider.Anthropic, "claude-haiku-5-5"),
            }), "No name: the model's own");
            Assert.That(models[1].Roles, Is.EqualTo(Commissioners));
            Assert.That(models[1].Price, Is.EqualTo(new ChatPrice(0.1m, 0.5m, 0m, 0m)));
            Assert.That(models[0].Price, Is.Null);
        }
    }

    [Test]
    public void ChatModels_RefusesAModelWhoseProviderIsntConfigured_OrAnIdTwice()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => ChatModels.From(Configuration(("Chat:Models:0:Id", "h"), ("Chat:Models:0:Provider", "Anthropic"), ("Chat:Models:0:Model", "m"))),
                Throws.InvalidOperationException.With.Message.Contains(ChatModels.AnthropicApiKeyKey));
            Assert.That(() => ChatModels.From(Configuration(("Chat:Models:0:Id", "o"), ("Chat:Models:0:Provider", "Ollama"), ("Chat:Models:0:Model", "m"))),
                Throws.InvalidOperationException.With.Message.Contains(ChatModels.OllamaUrlKey));
            Assert.That(() => ChatModels.From(Configuration((ChatModels.OllamaUrlKey, OllamaUrl),
                    ("Chat:Models:0:Id", "o"), ("Chat:Models:0:Provider", "Ollama"), ("Chat:Models:0:Model", "a"),
                    ("Chat:Models:1:Id", "o"), ("Chat:Models:1:Provider", "Ollama"), ("Chat:Models:1:Model", "b"))),
                Throws.InvalidOperationException.With.Message.Contains("more than once"));
            Assert.That(() => ChatModels.From(Configuration((ChatModels.OllamaUrlKey, OllamaUrl), ("Chat:Models:0:Provider", "Ollama"))),
                Throws.InvalidOperationException.With.Message.Contains("an Id and a Model"));
            Assert.That(() => ChatModels.From(Configuration((ChatModels.OllamaUrlKey, OllamaUrl),
                    ("Chat:Models:0:Id", new string('i', GrifballWebApp.Domain.Players.ChatChoice.ModelMaxLength + 1)), ("Chat:Models:0:Provider", "Ollama"), ("Chat:Models:0:Model", "m"))),
                Throws.InvalidOperationException.With.Message.Contains("longer than"), "A user's choice keeps the ID");
        }
    }

    [Test]
    public void OneHttpClientAndAnthropicClient_ForAllModels()
    {
        var configuration = Configuration((ChatModels.OllamaUrlKey, OllamaUrl), (ChatModels.AnthropicApiKeyKey, "sk-test"),
            ("Chat:Models:0:Id", "a"), ("Chat:Models:0:Provider", "Ollama"), ("Chat:Models:0:Model", "qwen3:4b"),
            ("Chat:Models:1:Id", "b"), ("Chat:Models:1:Provider", "Ollama"), ("Chat:Models:1:Model", "qwen3:8b"),
            ("Chat:Models:2:Id", "c"), ("Chat:Models:2:Provider", "Anthropic"), ("Chat:Models:2:Model", "claude-haiku-5-5"),
            ("Chat:Models:3:Id", "d"), ("Chat:Models:3:Provider", "Anthropic"), ("Chat:Models:3:Model", "claude-opus-5-5"));
        var http = Substitute.For<IHttpClientFactory>();
        http.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());
        // The test's HTTP clients, in place of AddChatModels' own
        using var services = new ServiceCollection().AddSingleton(configuration).AddChatModels(configuration).AddSingleton(http).BuildServiceProvider();
        var clients = services.GetRequiredService<IChatClientFactory>();

        var created = clients.Models.Select(clients.Create).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Take(2).Select(c => c.GetService<OllamaApiClient>()?.SelectedModel), Is.EqualTo(OllamaModels));
            Assert.That(created.Skip(2).Select(c => c.GetService<IAnthropicClient>()), Is.All.Not.Null);
            http.Received(1).CreateClient(ChatClients.OllamaHttpClient);
            http.Received(1).CreateClient(ChatClients.AnthropicHttpClient);
            Assert.That(() => clients.Create(clients.Models[0] with { Id = "other" }), Throws.ArgumentException, "Only a configured model");
        }
    }

    [Test]
    public void AddChatModels_ServesTheModelsItChecked_NotAListReadAgain()
    {
        var configuration = Configuration((ChatModels.OllamaUrlKey, OllamaUrl));
        using var services = new ServiceCollection().AddSingleton(configuration).AddChatModels(configuration).BuildServiceProvider();

        // Read once, as the app starts: a later change is no other list than the one checked
        configuration[ChatModels.OllamaModelKey] = "unchecked:1b";

        Assert.That(services.GetRequiredService<IChatClientFactory>().Models.Single().Model, Is.EqualTo(ChatClients.DefaultOllamaModel));
    }

    [TestCase(ChatClients.OllamaHttpClient)]
    [TestCase(ChatClients.AnthropicHttpClient)]
    public void HttpClients_HaveNoTimeoutOfTheirOwn_TheHostsDecide(string name)
    {
        using var services = Services((ChatModels.OllamaUrlKey, OllamaUrl));

        // Not HttpClient's default 100 seconds: a cold model on the CPU can take longer to start answering
        using var http = services.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        Assert.That(http.Timeout, Is.EqualTo(Timeout.InfiniteTimeSpan));
    }

    [TestCase(null)]
    [TestCase("")]
    public void NoOllamaUrlNorApiKey_IsNoModel_NotOllamasDefaultPort(string? url)
    {
        using var services = Services((ChatModels.OllamaUrlKey, url));
        var clients = services.GetRequiredService<IChatClientFactory>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clients.Available, Is.False);
            Assert.That(clients.Models, Is.Empty);
        }
    }

    [Test]
    public void ChatModels_RefusesANegativePrice_OrNoProvider()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => ChatModels.From(Configuration((ChatModels.AnthropicApiKeyKey, "sk-test"),
                    ("Chat:Models:0:Id", "h"), ("Chat:Models:0:Provider", "Anthropic"), ("Chat:Models:0:Model", "m"), ("Chat:Models:0:Price:Output", "-1"))),
                Throws.InvalidOperationException.With.Message.Contains("negative"), "A negative cost would never be saved: no limit");
            Assert.That(() => ChatModels.From(Configuration((ChatModels.OllamaUrlKey, OllamaUrl), ("Chat:Models:0:Id", "o"), ("Chat:Models:0:Model", "m"))),
                Throws.InvalidOperationException.With.Message.Contains("Provider"), "Not Ollama by default: an Anthropic model sent there");
        }
    }

    [Test]
    public void AddChatModels_ChecksTheModelsAsTheAppStarts_NotWhenAChatFirstNeedsThem()
    {
        var configuration = Configuration(("Chat:Models:0:Id", "h"), ("Chat:Models:0:Provider", "Anthropic"), ("Chat:Models:0:Model", "m"));

        Assert.That(() => new ServiceCollection().AddChatModels(configuration), Throws.InvalidOperationException.With.Message.Contains(ChatModels.AnthropicApiKeyKey));
    }
}
