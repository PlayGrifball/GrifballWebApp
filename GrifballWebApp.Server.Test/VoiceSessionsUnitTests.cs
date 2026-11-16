using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Voice;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Voice;
using NetCord.Gateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The bot's voice channels (<see cref="VoiceSessions"/>) and /voice's work (<see cref="VoiceCommandsHandler"/>), against a
/// stand-in for Discord's voice connection: one channel a guild, each feeding its conversation and playing its replies.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class VoiceSessionsUnitTests : SqliteGrifballContextFixture
{
    private const ulong Guild = 30;
    private const ulong Channel = 40;
    private const ulong Speaker = 501;
    private static readonly string[] Hello = ["Hello."];
    private static readonly ulong[] OnlyTheGuild = [Guild];
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly ChatLimits _limits = new();
    private readonly Connector _connector = new();
    private readonly Speech _speech = new();
    private ServiceProvider _services = null!;

    /// <summary>A voice connection: what it was played, and whether it was closed.</summary>
    private sealed class Link : IVoiceLink
    {
        public readonly List<short[]> Played = [];
        public bool Disposed { get; private set; }

        public event Action<ulong, short[]>? Heard;
        public event Action? Closed;

        public void Say(ulong speaker, short[] pcm) => Heard?.Invoke(speaker, pcm);

        public void Close() => Closed?.Invoke();

        public Task PlayAsync(short[] pcm, CancellationToken cancellationToken)
        {
            Played.Add(pcm);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }

    /// <summary>Discord's voice connections: each one opened recorded, and the guilds left.</summary>
    private sealed class Connector : IVoiceConnector
    {
        public readonly List<(ulong Guild, ulong Channel, Link Link)> Opened = [];
        public readonly List<ulong> Disconnected = [];
        public Exception? Fails { get; set; }

        public Task<IVoiceLink> ConnectAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken)
        {
            if (Fails is { } fails)
                throw fails;
            var link = new Link();
            Opened.Add((guildId, channelId, link));
            return Task.FromResult<IVoiceLink>(link);
        }

        public Task DisconnectAsync(ulong guildId)
        {
            Disconnected.Add(guildId);
            return Task.CompletedTask;
        }
    }

    private sealed class Speech : IChatSpeech
    {
        public readonly Queue<string> Transcripts = new();
        public readonly List<string> Spoken = [];

        public Task<string> TranscribeAsync(ReadOnlyMemory<byte> wav, CancellationToken cancellationToken = default)
            => Task.FromResult(Transcripts.Dequeue());

        public Task<byte[]> SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            Spoken.Add(text);
            return Task.FromResult<byte[]>([1, 0]);
        }
    }

    [TearDown]
    public async Task TearDownServices()
    {
        _limits.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private VoiceSessions Sessions(bool speech = true)
    {
        var services = new ServiceCollection()
            .AddSingleton<GrifballContext>(Db)
            .AddSingleton<IApplicationDbContext>(Db)
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<IChatWaiting, MemoryChatWaiting>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddTransient<VoiceConsents>();
        if (speech)
            services.AddSingleton<IChatSpeech>(_speech);
        var clients = Substitute.For<IChatClientFactory>();
        clients.Available.Returns(true);
        clients.Models.Returns([new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b")]);
        clients.Create(Arg.Any<ChatModelOption>()).Returns(new ScriptedChatClient((TimeSpan.Zero, "Hello.")));
        services.AddSingleton(provider => new GrifChat(new ChatModel(clients, NullLoggerFactory.Instance), provider.GetRequiredService<IServiceScopeFactory>(),
            _limits, new ChatAccounts(provider.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<ChatAccounts>.Instance)));
        _services = services.BuildServiceProvider();
        return new VoiceSessions(_connector, _services, NullLogger<VoiceSessions>.Instance);
    }

    private VoiceCommandsHandler Handler(VoiceSessions sessions) => new(sessions, new VoiceConsents(Db, _time));

    /// <summary>The guild's voice states, as the gateway keeps them: <see cref="Speaker"/> in <paramref name="channel"/> (null: in none).</summary>
    private static Dictionary<ulong, VoiceState> In(ulong? channel) => new()
    {
        [Speaker] = new VoiceState(new NetCord.Gateway.JsonModels.JsonVoiceState { UserId = Speaker, ChannelId = channel, SessionId = "s" }, Guild,
            new NetCord.Rest.RestClient()),
    };

    [Test]
    public async Task Join_ListensInTheChannel_ItsSpeakersAnswered_AndItsRepliesPlayedThere()
    {
        await using var sessions = Sessions();
        var handler = Handler(sessions);
        await handler.OptInAsync(Speaker);
        _speech.Transcripts.Enqueue("Grif, hello?");

        var joined = await handler.JoinAsync(Guild, In(Channel), Speaker);
        var link = _connector.Opened.Single().Link;
        await sessions.RefreshAsync(Speaker);
        link.Say(Speaker, new short[VoicePcm.DiscordRate]);
        for (var i = 0; i < 20 && link.Played.Count == 0; i++)
        {
            _time.Advance(VoiceConversation.Tick + VoiceLines.Silence);
            await Task.Delay(10);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(joined, Is.EqualTo(VoiceCommandsHandler.Listening(Channel)));
            Assert.That((_connector.Opened.Single().Guild, _connector.Opened.Single().Channel), Is.EqualTo((Guild, Channel)));
            Assert.That(_speech.Spoken, Is.EqualTo(Hello));
            Assert.That(link.Played, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Join_Twice_InOneGuild_IsRefused_AndLeaveClosesIt()
    {
        await using var sessions = Sessions();
        var handler = Handler(sessions);

        await handler.JoinAsync(Guild, In(Channel), Speaker);
        var again = await handler.JoinAsync(Guild, In(41), Speaker);
        var left = await handler.LeaveAsync(Guild);
        var leftAgain = await handler.LeaveAsync(Guild);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again, Is.EqualTo(VoiceCommandsHandler.AlreadyIn));
            Assert.That(_connector.Opened, Has.Count.EqualTo(1));
            Assert.That((left, leftAgain), Is.EqualTo((VoiceCommandsHandler.Left, VoiceCommandsHandler.NotIn)));
            Assert.That(_connector.Opened.Single().Link.Disposed, Is.True);
            Assert.That(_connector.Disconnected, Is.EqualTo(OnlyTheGuild));
        }
    }

    [Test]
    public async Task AConnectionClosedByDiscord_IsOver_SoTheBotMayJoinAgain()
    {
        await using var sessions = Sessions();
        var handler = Handler(sessions);
        await handler.JoinAsync(Guild, In(Channel), Speaker);

        _connector.Opened.Single().Link.Close();
        var rejoined = await handler.JoinAsync(Guild, In(Channel), Speaker);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rejoined, Is.EqualTo(VoiceCommandsHandler.Listening(Channel)));
            Assert.That(_connector.Opened[0].Link.Disposed, Is.True);
        }
    }

    [Test]
    public async Task AJoinThatFails_LeavesNothingBehind_AndSaysSo()
    {
        await using var sessions = Sessions();
        _connector.Fails = new TimeoutException("no voice server");

        Assert.That(async () => await Handler(sessions).JoinAsync(Guild, In(Channel), Speaker), Throws.TypeOf<TimeoutException>());
        _connector.Fails = null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_connector.Disconnected, Is.EqualTo(OnlyTheGuild), "Out of the channel Discord may have put it in");
            Assert.That(await Handler(sessions).JoinAsync(Guild, In(Channel), Speaker), Is.EqualTo(VoiceCommandsHandler.Listening(Channel)), "Free to try again");
        }
    }

    [Test]
    public async Task Join_WithNoSpeechServer_NoGuild_OrNoVoiceChannel_IsRefused()
    {
        await using var unavailable = Sessions(speech: false);
        var noSpeech = await Handler(unavailable).JoinAsync(Guild, In(Channel), Speaker);
        await using var sessions = Sessions();
        var handler = Handler(sessions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noSpeech, Is.EqualTo(VoiceCommandsHandler.Unavailable));
            Assert.That(await handler.JoinAsync(null, In(Channel), Speaker), Is.EqualTo(VoiceCommandsHandler.NoGuild));
            Assert.That(await handler.JoinAsync(Guild, null, Speaker), Is.EqualTo(VoiceCommandsHandler.NoGuild), "Not in the gateway's cache");
            Assert.That(await handler.JoinAsync(Guild, In(null), Speaker), Is.EqualTo(VoiceCommandsHandler.NotInVoice), "In none");
            Assert.That(await handler.JoinAsync(Guild, In(Channel), 999), Is.EqualTo(VoiceCommandsHandler.NotInVoice), "Someone else is");
            Assert.That(await handler.LeaveAsync(null), Is.EqualTo(VoiceCommandsHandler.NoGuild));
            Assert.That(_connector.Opened, Is.Empty);
        }
    }

    [Test]
    public async Task OptOut_InAChannel_IsNoLongerHeard_ThereAtOnce()
    {
        await using var sessions = Sessions();
        var handler = Handler(sessions);
        await handler.OptInAsync(Speaker);
        await handler.JoinAsync(Guild, In(Channel), Speaker);
        var link = _connector.Opened.Single().Link;
        await sessions.RefreshAsync(Speaker);

        var reply = await handler.OptOutAsync(Speaker);
        link.Say(Speaker, new short[VoicePcm.DiscordRate]);
        _time.Advance(VoiceConversation.Tick + VoiceLines.Silence);
        await Task.Delay(50);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply, Is.EqualTo(VoiceCommandsHandler.OptedOut));
            Assert.That(_speech.Spoken, Is.Empty);
        }
    }

    [Test]
    public async Task AConnectionClosedAfterTheBotLeft_ChangesNothing()
    {
        await using var sessions = Sessions();
        var handler = Handler(sessions);
        await handler.JoinAsync(Guild, In(Channel), Speaker);
        var link = _connector.Opened.Single().Link;
        await handler.LeaveAsync(Guild);
        await handler.JoinAsync(Guild, In(Channel), Speaker);

        link.Close();

        Assert.That(await handler.JoinAsync(Guild, In(Channel), Speaker), Is.EqualTo(VoiceCommandsHandler.AlreadyIn), "The new connection stands");
    }
}
