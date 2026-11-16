namespace GrifballWebApp.Application.Chat;

/// <summary>
/// The voice chat's speech (Infrastructure's, on a speech server): what people said, as text, and the bot's replies, as audio.
/// Registered only when a server is configured (Speech:Url): none, no voice.
/// </summary>
public interface IChatSpeech
{
    /// <summary>The audio <see cref="SpeakAsync"/> gives: raw PCM, 16-bit little-endian samples, mono, at this rate (Hz).</summary>
    const int SpeechSampleRate = 24_000;

    /// <summary>What was said in <paramref name="wav"/> (a WAV file), as text, trimmed.</summary>
    Task<string> TranscribeAsync(ReadOnlyMemory<byte> wav, CancellationToken cancellationToken = default);

    /// <summary><paramref name="text"/> spoken: raw PCM (<see cref="SpeechSampleRate"/>).</summary>
    Task<byte[]> SpeakAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>The speech server's models: Whisper's for what people say, the voice model's, and its voice.</summary>
public sealed record SpeechOptions(string TranscriptionModel, string SpeechModel, string Voice);
