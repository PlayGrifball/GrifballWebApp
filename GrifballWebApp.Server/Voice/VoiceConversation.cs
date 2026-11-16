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
public sealed class VoiceConversation(IServiceScopeFactory scopes, GrifChat chat, IChatSpeech speech, TimeProvider time,
    ILogger<VoiceConversation> log, Func<short[], CancellationToken, Task> play)
{
    /// <summary>How often ended lines are looked for.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    /// <summary>What the bot says when a line couldn't be answered for a reason it doesn't know.</summary>
    public const string Failed = "Sorry, I couldn't answer that just now.";

    private readonly VoiceLines _lines = new(time);
    // Who may be heard: true opted in, false not (or not known yet: dropped until it is)
    private readonly ConcurrentDictionary<ulong, bool> _heard = new();

    /// <summary>
    /// <paramref name="pcm"/> (48 kHz mono) from <paramref name="speaker"/>: kept for their line when they opted in, else dropped. A
    /// speaker not known yet is looked up (<see cref="RefreshAsync"/>), their audio dropped meanwhile.
    /// </summary>
    public void Hear(ulong speaker, short[] pcm)
    {
        if (_heard.TryGetValue(speaker, out var heard))
        {
            if (heard)
                _lines.Add(speaker, pcm);
            return;
        }
        _heard[speaker] = false;
        _ = RefreshAsync(speaker);
    }

    /// <summary>Whether <paramref name="speaker"/> may be heard, read again: they opted in or out. Opted out, what they were saying is dropped.</summary>
    public async Task RefreshAsync(ulong speaker)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var heard = (await scope.ServiceProvider.GetRequiredService<VoiceConsents>().OptedInAsync([speaker])).Contains(speaker);
            _heard[speaker] = heard;
            if (!heard)
                _lines.Forget(speaker);
        }
        catch (Exception e)
        {
            // Not known: not heard, and asked again with their next audio
            _heard.TryRemove(speaker, out _);
            _lines.Forget(speaker);
            log.LogWarning(e, "Voice: whether {Speaker} opted in couldn't be read", speaker);
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
        var transcript = await speech.TranscribeAsync(VoicePcm.Wav16kMono(pcm), cancellationToken);
        if (VoiceWake.Question(transcript) is not { } question)
            return;
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
        if (string.IsNullOrWhiteSpace(answer))
            return;
        var spoken = await speech.SpeakAsync(answer, cancellationToken);
        await play(VoicePcm.Stereo48kFrom24kMono(spoken), cancellationToken);
    }

    /// <summary><paramref name="question"/> asked as <paramref name="speaker"/> (their linked account's roles, else none) on the voice host; the whole answer.</summary>
    private async Task<string> AskAsync(ulong speaker, string question, CancellationToken cancellationToken)
    {
        ToolCaller caller;
        using (var scope = scopes.CreateScope())
            caller = await new DiscordCaller(scope.ServiceProvider.GetRequiredService<GrifballContext>()).AsToolCaller(speaker, cancellationToken);
        using var reply = await chat.StartAsync([], question, caller, ChatHost.DiscordVoice, ChatLimits.Asker(caller, ChatLimits.Discord(speaker)),
            cancellationToken: cancellationToken);
        var answer = new StringBuilder();
        await foreach (var text in reply.StreamAsync(cancellationToken))
            answer.Append(text);
        return answer.ToString().Trim();
    }
}
