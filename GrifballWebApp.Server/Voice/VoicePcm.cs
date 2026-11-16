using System.Buffers.Binary;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// The voice chat's audio, between Discord's (48 kHz) and the speech server's: a line as a 16 kHz mono WAV for Whisper, and a
/// reply's 24 kHz mono speech as 48 kHz stereo for Discord.
/// </summary>
public static class VoicePcm
{
    /// <summary>Discord's sample rate (Opus's).</summary>
    public const int DiscordRate = 48_000;

    /// <summary>What Whisper is sent: 16 kHz.</summary>
    public const int WhisperRate = 16_000;

    /// <summary>
    /// <paramref name="pcm48k"/> (48 kHz mono, 16-bit) as a 16 kHz mono WAV file, each three samples averaged into one (Whisper hears
    /// nothing above 8 kHz).
    /// </summary>
    public static byte[] Wav16kMono(ReadOnlySpan<short> pcm48k)
    {
        const int Ratio = DiscordRate / WhisperRate;
        var samples = pcm48k.Length / Ratio;
        var wav = new byte[44 + samples * 2];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + samples * 2);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], WhisperRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], WhisperRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var sum = 0;
            for (var j = 0; j < Ratio; j++)
                sum += pcm48k[i * Ratio + j];
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + i * 2)..], (short)(sum / Ratio));
        }
        return wav;
    }

    /// <summary>
    /// <paramref name="pcm24k"/> (the speech server's: 24 kHz mono, 16-bit little-endian) as 48 kHz stereo, interleaved: each sample
    /// twice for the rate, and in both channels.
    /// </summary>
    public static short[] Stereo48kFrom24kMono(ReadOnlySpan<byte> pcm24k)
    {
        var samples = pcm24k.Length / 2;
        var stereo = new short[samples * 4];
        for (var i = 0; i < samples; i++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm24k[(i * 2)..]);
            stereo.AsSpan(i * 4, 4).Fill(sample);
        }
        return stereo;
    }
}
