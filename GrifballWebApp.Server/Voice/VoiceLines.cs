namespace GrifballWebApp.Server.Voice;

/// <summary>
/// What each speaker in a voice channel said, as it arrives (48 kHz mono): a line ends once they have been quiet for
/// <see cref="Silence"/> (Discord sends nothing while someone is silent). Too short a line (a cough, a click) is dropped; too
/// long a one is cut at <see cref="MaxSeconds"/>.
/// </summary>
public sealed class VoiceLines(TimeProvider time, TimeSpan? silence = null)
{
    /// <summary>How long a speaker is quiet before their line is over, unless set otherwise (Speech:EndOfLineMilliseconds).</summary>
    public static readonly TimeSpan Silence = TimeSpan.FromMilliseconds(700);

    /// <summary>The longest line kept: past it, the rest is dropped.</summary>
    public const int MaxSeconds = 30;

    /// <summary>The shortest line kept: shorter, it's dropped.</summary>
    public static readonly TimeSpan Shortest = TimeSpan.FromMilliseconds(300);

    private const int MaxSamples = MaxSeconds * VoicePcm.DiscordRate;
    private static readonly int ShortestSamples = (int)(Shortest.TotalSeconds * VoicePcm.DiscordRate);

    private readonly TimeSpan _silence = silence ?? Silence;
    private readonly Dictionary<ulong, (List<short> Pcm, DateTimeOffset Last)> _speaking = [];
    // Lines a pause ended before anyone asked for them (the bot was answering meanwhile), in the order they ended
    private readonly List<(ulong Speaker, List<short> Pcm)> _ended = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// <paramref name="pcm"/> more of what <paramref name="speaker"/> is saying: after a pause (<see cref="Silence"/>), a new line, the
    /// last one over, though <see cref="Ended"/> hasn't been asked since.
    /// </summary>
    public void Add(ulong speaker, ReadOnlySpan<short> pcm)
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            List<short> line = [];
            if (_speaking.TryGetValue(speaker, out var speaking))
            {
                if (now - speaking.Last >= _silence)
                    _ended.Add((speaker, speaking.Pcm));
                else
                    line = speaking.Pcm;
            }
            var room = MaxSamples - line.Count;
            if (room > 0)
                line.AddRange(pcm.Length > room ? pcm[..room] : pcm);
            _speaking[speaker] = (line, now);
        }
    }

    /// <summary>The lines over now (each given once), the ones long enough to be speech.</summary>
    public IReadOnlyList<(ulong Speaker, short[] Pcm)> Ended()
    {
        var now = time.GetUtcNow();
        var ended = new List<(ulong, short[])>();
        lock (_lock)
        {
            foreach (var (speaker, (pcm, _)) in _speaking.Where(s => now - s.Value.Last >= _silence).ToList())
            {
                _speaking.Remove(speaker);
                _ended.Add((speaker, pcm));
            }
            ended.AddRange(_ended.Where(l => l.Pcm.Count >= ShortestSamples).Select(l => (l.Speaker, l.Pcm.ToArray())));
            if (_ended.Count > ended.Count)
                VoiceTelemetry.Line(VoiceTelemetry.TooShort, _ended.Count - ended.Count);
            _ended.Clear();
        }
        return ended;
    }

    /// <summary>What <paramref name="speaker"/> was saying dropped: they opted out, or left.</summary>
    public void Forget(ulong speaker)
    {
        lock (_lock)
        {
            _speaking.Remove(speaker);
            _ended.RemoveAll(l => l.Speaker == speaker);
        }
    }
}
