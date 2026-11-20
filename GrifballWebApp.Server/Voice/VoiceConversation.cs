using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.Voice;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// The bot's conversation in one voice channel, without Discord itself (<see cref="VoiceSessions"/> feeds it and plays its
/// replies): it hears only those who opted in (<see cref="VoiceConsents"/>; everyone else's audio is dropped as it arrives), each
/// line ending after a silence (<see cref="VoiceLines"/>). A line addressed to the bot (<see cref="VoiceWake"/>) is answered on the
/// voice host as its speaker (their roles, their models and month), one at a time, and the answer spoken. Nothing said is kept:
/// each line is gone once transcribed, its transcript once answered.
/// </summary>
/// <param name="scopes">Scopes for the speaker's account (their roles) and, by default, their consent.</param>
/// <param name="chat">The chat that answers, on the voice host.</param>
/// <param name="speech">The speech server: what was said, and the answer spoken.</param>
/// <param name="time">The clock lines end by.</param>
/// <param name="log">Where a line that couldn't be answered, or a consent that couldn't be read, is logged.</param>
/// <param name="play">Plays an answer (48 kHz stereo) in the channel.</param>
/// <param name="optedIn">Whether a speaker opted in; null: <see cref="VoiceConsents"/>, in a scope of its own.</param>
public sealed class VoiceConversation(IServiceScopeFactory scopes, GrifChat chat, IChatSpeech speech, TimeProvider time,
    ILogger<VoiceConversation> log, Func<short[], CancellationToken, Task> play, Func<ulong, Task<bool>>? optedIn = null,
    TimeSpan? endOfLine = null)
{
    private readonly Func<ulong, Task<bool>> _optedIn = optedIn ?? (async speaker =>
    {
        using var scope = scopes.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<VoiceConsents>().OptedInAsync([speaker])).Contains(speaker);
    });

    /// <summary>How often ended lines are looked for.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    /// <summary>What the bot says when a line couldn't be answered for a reason it doesn't know.</summary>
    public const string Failed = "Sorry, I couldn't answer that just now.";

    /// <summary>The most audio held for a speaker while they are looked up: past it, the rest is dropped.</summary>
    public static readonly TimeSpan MaxHeld = TimeSpan.FromSeconds(5);

    private static readonly int MaxHeldSamples = (int)(MaxHeld.TotalSeconds * VoicePcm.DiscordRate);

    private readonly VoiceLines _lines = new(time, endOfLine);
    // Who may be heard: true opted in, false not; absent, not known yet (their audio held while they are looked up)
    private readonly ConcurrentDictionary<ulong, bool> _heard = new();
    // Each speaker's lookups, one at a time: the latest (an opt-out's) always writes last
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _lookups = new();
    private readonly ConcurrentDictionary<ulong, byte> _lookingUp = new();
    private readonly Dictionary<ulong, List<short>> _held = [];
    private readonly Lock _heldLock = new();

    /// <summary>
    /// Whether <paramref name="speaker"/>'s audio is worth decoding: they opted in, or aren't known yet (held while they are looked
    /// up). Not someone who hasn't opted in.
    /// </summary>
    public bool Wants(ulong speaker) => !_heard.TryGetValue(speaker, out var heard) || heard;

    /// <summary>
    /// <paramref name="pcm"/> (48 kHz mono) from <paramref name="speaker"/>: kept for their line when they opted in, else dropped. A
    /// speaker not known yet is looked up (<see cref="RefreshAsync"/>), their audio held meanwhile (up to <see cref="MaxHeld"/>), so a
    /// first "Grif, ..." isn't cut.
    /// </summary>
    public void Hear(ulong speaker, short[] pcm)
    {
        // Under the lock their lookup settles under: known, or held for it
        lock (_heldLock)
        {
            if (_heard.TryGetValue(speaker, out var heard))
            {
                if (heard)
                    _lines.Add(speaker, pcm);
                return;
            }
            var held = _held.TryGetValue(speaker, out var some) ? some : _held[speaker] = [];
            var room = MaxHeldSamples - held.Count;
            if (room > 0)
                held.AddRange(pcm.Length > room ? pcm.AsSpan(0, room) : pcm);
        }
        if (_lookingUp.TryAdd(speaker, 0))
            _ = RefreshAsync(speaker);
    }

    /// <summary>
    /// Whether <paramref name="speaker"/> may be heard, read again: they opted in or out. Opted in, what was held for them is theirs;
    /// opted out, what they were saying is dropped, held or not.
    /// </summary>
    public async Task RefreshAsync(ulong speaker)
    {
        var lookup = _lookups.GetOrAdd(speaker, _ => new SemaphoreSlim(1, 1));
        await lookup.WaitAsync();
        try
        {
            Settle(speaker, await _optedIn(speaker));
        }
        catch (Exception e)
        {
            // Not known: not heard, and asked again with their next audio
            Settle(speaker, null);
            log.LogWarning(e, "Voice: whether {Speaker} opted in couldn't be read", speaker);
        }
        finally
        {
            _lookingUp.TryRemove(speaker, out _);
            lookup.Release();
        }
    }

    /// <summary><paramref name="speaker"/> heard or not (null: not known), what was held for them kept or dropped with it.</summary>
    private void Settle(ulong speaker, bool? heard)
    {
        lock (_heldLock)
        {
            if (heard is { } known)
                _heard[speaker] = known;
            else
                _heard.TryRemove(speaker, out _);
            if (_held.Remove(speaker, out var held) && heard == true)
                _lines.Add(speaker, held.ToArray());
            if (heard != true)
                _lines.Forget(speaker);
        }
    }

    /// <summary>Answers the lines that ended, until <paramref name="cancellationToken"/>: one at a time, in the order they ended.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Tick, time);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await ProcessEndedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Left the channel: over. Only this token's cancellation gets here: the timer throws for nothing else, and
            // ProcessEndedAsync logs any other
        }
    }

    /// <summary>The lines that ended since last time, each answered when it is addressed to the bot.</summary>
    public async Task ProcessEndedAsync(CancellationToken cancellationToken)
    {
        foreach (var (speaker, pcm) in _lines.Ended())
        {
            // Opted out since the line ended (while another was answered): dropped, as they were told
            if (!_heard.TryGetValue(speaker, out var heard) || !heard)
                continue;
            try
            {
                await AnswerAsync(speaker, pcm, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                log.LogError(e, "Voice: a line from {Speaker} couldn't be answered", speaker);
            }
        }
    }

    private async Task AnswerAsync(ulong speaker, short[] pcm, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var transcript = await speech.TranscribeAsync(VoicePcm.Wav16kMono(pcm), cancellationToken);
        var heardFor = Stopwatch.GetElapsedTime(started);
        if (VoiceWake.Question(transcript) is not { } question)
            return;
        started = Stopwatch.GetTimestamp();
        string answer;
        try
        {
            answer = await AskAsync(speaker, question, cancellationToken);
        }
        catch (ChatRefusedException refused)
        {
            answer = refused.Message;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            log.LogError(e, "Voice: the chat couldn't answer {Speaker}", speaker);
            answer = Failed;
        }
        var answeredFor = Stopwatch.GetElapsedTime(started);
        if (string.IsNullOrWhiteSpace(answer))
            return;
        started = Stopwatch.GetTimestamp();
        var spoken = await speech.SpeakAsync(answer, cancellationToken);
        var spokenFor = Stopwatch.GetElapsedTime(started);
        started = Stopwatch.GetTimestamp();
        await play(VoicePcm.Stereo48kFrom24kMono(spoken), cancellationToken);
        var playedFor = Stopwatch.GetElapsedTime(started);
        log.LogInformation("Voice: answered {Speaker} in {Transcribe} ms transcribing, {Reply} ms replying, {Speak} ms speaking, {Play} ms playing",
            speaker, (int)heardFor.TotalMilliseconds, (int)answeredFor.TotalMilliseconds, (int)spokenFor.TotalMilliseconds, (int)playedFor.TotalMilliseconds);
    }

    /// <summary><paramref name="question"/> asked as <paramref name="speaker"/> (their linked account's roles, else none) on the voice host; the whole answer.</summary>
    private async Task<string> AskAsync(ulong speaker, string question, CancellationToken cancellationToken)
    {
        ToolCaller caller;
        using (var scope = scopes.CreateScope())
            caller = await new DiscordCaller(scope.ServiceProvider.GetRequiredService<GrifballContext>()).AsToolCaller(speaker, cancellationToken);
        using var reply = await chat.StartAsync([], question, caller, ChatHost.DiscordVoice, ChatLimits.Asker(caller, ChatLimits.Discord(speaker)),
            cancellationToken: cancellationToken);
        var model = reply.Model.Id;
        var signedIn = caller.SignedIn;
        log.LogInformation("Voice: {Speaker} (signed in: {SignedIn}) is answered by {Model}", speaker, signedIn, model);
        var answer = new StringBuilder();
        await foreach (var text in reply.StreamAsync(cancellationToken))
            answer.Append(text);
        return answer.ToString().Trim();
    }
}
