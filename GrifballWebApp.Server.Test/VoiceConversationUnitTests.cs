using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Voice;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Voice;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A voice channel's conversation (<see cref="VoiceConversation"/>): only those who opted in are heard; a line addressed to the
/// bot is answered on the voice host as its speaker, and the answer spoken.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class VoiceConversationUnitTests : SqliteGrifballContextFixture
{
    private const ulong Speaker = 501;
    private static readonly string[] GrifsDid = ["The Grifs did."];
    private static readonly string[] Season3 = ["Season 3."];
    private static readonly string[] Hello = ["Hello."];
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly ChatLimits _limits = new();
    private readonly Speech _speech = new();
    private readonly List<short[]> _played = [];
    private ServiceProvider _services = null!;

    /// <summary>The speech server: each WAV it is sent answered with the next transcript; each text it speaks recorded.</summary>
    private sealed class Speech : IChatSpeech
    {
        public readonly Queue<string> Transcripts = new();
        public readonly List<byte[]> Heard = [];
        public readonly List<string> Spoken = [];
        public Exception? Fails { get; set; }

        public Task<string> TranscribeAsync(ReadOnlyMemory<byte> wav, CancellationToken cancellationToken = default)
        {
            if (Fails is { } fails)
                throw fails;
            Heard.Add(wav.ToArray());
            return Task.FromResult(Transcripts.Dequeue());
        }

        public Task<byte[]> SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            Spoken.Add(text);
            return Task.FromResult<byte[]>([1, 0, 2, 0]);
        }
    }

    [TearDown]
    public async Task TearDownServices()
    {
        _limits.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private VoiceConversation Conversation(IChatClient model, bool modelConfigured = true)
    {
        _services = new ServiceCollection()
            .AddSingleton<GrifballContext>(Db)
            .AddSingleton<IApplicationDbContext>(Db)
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<IChatWaiting, MemoryChatWaiting>()
            .AddTransient<VoiceConsents>()
            .BuildServiceProvider();
        var clients = Substitute.For<IChatClientFactory>();
        clients.Available.Returns(modelConfigured);
        clients.Models.Returns([new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b")]);
        clients.Create(Arg.Any<ChatModelOption>()).Returns(model);
        var scopes = _services.GetRequiredService<IServiceScopeFactory>();
        var chat = new GrifChat(new ChatModel(clients, NullLoggerFactory.Instance), scopes, _limits,
            new ChatAccounts(scopes, _time, NullLogger<ChatAccounts>.Instance));
        return new VoiceConversation(scopes, chat, _speech, _time, NullLogger<VoiceConversation>.Instance, (pcm, _) =>
        {
            _played.Add(pcm);
            return Task.CompletedTask;
        });
    }

    private async Task OptedIn(ulong discordUserId)
    {
        await new VoiceConsents(Db, _time).OptInAsync(discordUserId);
    }

    /// <summary><paramref name="conversation"/> hears a second of <see cref="Speaker"/>, then the silence that ends the line, and answers.</summary>
    private async Task SaysALine(VoiceConversation conversation)
    {
        await conversation.RefreshAsync(Speaker);
        conversation.Hear(Speaker, new short[VoicePcm.DiscordRate]);
        _time.Advance(VoiceLines.Silence);
        await conversation.ProcessEndedAsync(CancellationToken.None);
    }

    [Test]
    public async Task ALineAddressedToTheBot_FromSomeoneWhoOptedIn_IsAnsweredAsThem_OnTheVoiceHost_AndSpoken()
    {
        await OptedIn(Speaker);
        _speech.Transcripts.Enqueue("Grif, who won last season?");
        var model = new ScriptedChatClient((TimeSpan.Zero, "The "), (TimeSpan.Zero, "Grifs did."));

        await SaysALine(Conversation(model));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_speech.Heard.Single(), Has.Length.EqualTo(44 + VoicePcm.WhisperRate * 2), "A second, as a 16 kHz WAV");
            Assert.That(model.Messages.Last().Text, Is.EqualTo("who won last season?"));
            Assert.That(model.Options!.Instructions, Does.Contain(ChatHost.DiscordVoice.About));
            Assert.That(_speech.Spoken, Is.EqualTo(GrifsDid));
            Assert.That(_played.Single(), Is.EqualTo(new short[] { 1, 1, 1, 1, 2, 2, 2, 2 }));
        }
    }

    [Test]
    public async Task SomeoneWhoHasntOptedIn_IsNeverTranscribed()
    {
        var conversation = Conversation(new ScriptedChatClient((TimeSpan.Zero, "Hi")));

        await SaysALine(conversation);

        Assert.That(_speech.Heard, Is.Empty, "Dropped before transcription");
    }

    [Test]
    public async Task SomeoneWhoOptsOut_IsNoLongerHeard()
    {
        await OptedIn(Speaker);
        var conversation = Conversation(new ScriptedChatClient((TimeSpan.Zero, "Hi")));
        await conversation.RefreshAsync(Speaker);
        conversation.Hear(Speaker, new short[VoicePcm.DiscordRate]);

        await new VoiceConsents(Db, _time).OptOutAsync(Speaker);
        await conversation.RefreshAsync(Speaker);
        conversation.Hear(Speaker, new short[VoicePcm.DiscordRate]);
        _time.Advance(VoiceLines.Silence);
        await conversation.ProcessEndedAsync(CancellationToken.None);

        Assert.That(_speech.Heard, Is.Empty, "What they said before opting out is dropped too");
    }

    [Test]
    public async Task ALineNotAddressedToTheBot_IsLeftThere()
    {
        await OptedIn(Speaker);
        _speech.Transcripts.Enqueue("I think we won that one");
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        await SaysALine(Conversation(model));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Messages, Is.Empty);
            Assert.That(_played, Is.Empty);
        }
    }

    [Test]
    public async Task AQuestionTheChatRefuses_HearsWhy()
    {
        await OptedIn(Speaker);
        _speech.Transcripts.Enqueue("Grif, who won?");

        await SaysALine(Conversation(new ScriptedChatClient((TimeSpan.Zero, "Hi")), modelConfigured: false));

        Assert.That(_speech.Spoken, Is.EqualTo(new[] { GrifChat.Unavailable }));
    }

    [Test]
    public async Task ALineTheSpeechServerFails_IsDropped_AndTheNextIsStillAnswered()
    {
        await OptedIn(Speaker);
        var conversation = Conversation(new ScriptedChatClient((TimeSpan.Zero, "Season 3.")));
        _speech.Fails = new HttpRequestException("down");

        await SaysALine(conversation);
        _speech.Fails = null;
        _speech.Transcripts.Enqueue("Grif, which season is on?");
        await SaysALine(conversation);

        Assert.That(_speech.Spoken, Is.EqualTo(Season3));
    }

    [Test]
    public async Task RunAsync_AnswersLinesAsTheyEnd_UntilStopped()
    {
        await OptedIn(Speaker);
        _speech.Transcripts.Enqueue("Grif, hello?");
        var conversation = Conversation(new ScriptedChatClient((TimeSpan.Zero, "Hello.")));
        await conversation.RefreshAsync(Speaker);
        using var stop = new CancellationTokenSource();
        var running = conversation.RunAsync(stop.Token);

        conversation.Hear(Speaker, new short[VoicePcm.DiscordRate]);
        for (var i = 0; i < 20 && _speech.Spoken.Count == 0; i++)
        {
            _time.Advance(VoiceConversation.Tick);
            await Task.Delay(10);
        }
        await stop.CancelAsync();

        Assert.That(_speech.Spoken, Is.EqualTo(Hello));
        Assert.That(async () => await running, Throws.Nothing, "Stopping ends it quietly");
    }
}
