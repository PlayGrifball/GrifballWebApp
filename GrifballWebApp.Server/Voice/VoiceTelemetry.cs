using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// The voice bot's own spans and metrics, on <see cref="SourceName"/>: a <c>voice.answer</c> span per line that is transcribed, with a
/// child per stage (transcribe, reply, speak, play); the reply's chat spans sit under its <c>reply</c> span. Its meter: how long each
/// stage took (<c>grif.voice.stage.duration</c>) and what became of each line (<c>grif.voice.lines</c>, by outcome). Nothing said is
/// in any of it.
/// </summary>
public static class VoiceTelemetry
{
    /// <summary>The voice bot's ActivitySource and Meter.</summary>
    public const string SourceName = "GrifballWebApp.Voice";

    /// <summary>A line answered.</summary>
    public const string Answered = "answered";

    /// <summary>A line that didn't start with the bot's name: left there.</summary>
    public const string NotAddressed = "not_addressed";

    /// <summary>A line the speech server heard nothing in.</summary>
    public const string NothingHeard = "nothing_heard";

    /// <summary>A line shorter than <see cref="VoiceLines.Shortest"/>: a cough, a click.</summary>
    public const string TooShort = "too_short";

    /// <summary>A line whose speaker opted out before its turn.</summary>
    public const string OptedOut = "opted_out";

    /// <summary>A line the chat answered with nothing to say.</summary>
    public const string EmptyAnswer = "empty_answer";

    /// <summary>A line that couldn't be answered: the speech server or the chat failed.</summary>
    public const string Failed = "failed";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    private static readonly Histogram<double> StageSeconds = Meter.CreateHistogram<double>("grif.voice.stage.duration", "s",
        "How long a stage of answering a voice line took: transcribe, reply, speak or play");

    private static readonly Counter<long> LineCount = Meter.CreateCounter<long>("grif.voice.lines", "{line}",
        "Voice lines, by what became of them");

    /// <summary>One more line that ended in <paramref name="outcome"/>.</summary>
    public static void Line(string outcome, long count = 1)
        => LineCount.Add(count, new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>A span and a timer for <paramref name="name"/>'s stage, ended (and its duration recorded) when disposed.</summary>
    public static Stage StartStage(string name) => new(name);

    /// <summary>One stage of answering a line.</summary>
    public readonly struct Stage : IDisposable
    {
        private readonly string _name;
        private readonly long _started;
        private readonly Activity? _span;

        internal Stage(string name)
        {
            _name = name;
            _started = Stopwatch.GetTimestamp();
            _span = Source.StartActivity(name);
        }

        /// <summary>How long the stage has run.</summary>
        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_started);

        public void Dispose()
        {
            StageSeconds.Record(Elapsed.TotalSeconds, new KeyValuePair<string, object?>("stage", _name));
            _span?.Dispose();
        }
    }
}
