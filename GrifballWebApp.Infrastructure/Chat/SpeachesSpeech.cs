using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Infrastructure.Chat;

/// <summary>
/// The voice chat's speech on speaches (the chart's speech server), through its OpenAI-compatible endpoints: Whisper's
/// transcriptions, and Kokoro's speech as raw PCM.
/// </summary>
public sealed class SpeachesSpeech(HttpClient http, SpeechOptions options) : IChatSpeech
{
    /// <summary>What Whisper is told it may hear: the bot's name, spelled as the wake word expects.</summary>
    public const string Prompt = "Grif";

    public async Task<string> TranscribeAsync(ReadOnlyMemory<byte> wav, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new ReadOnlyMemoryContent(wav), "file", "speech.wav" },
            { new StringContent(options.TranscriptionModel), "model" },
            { new StringContent("en"), "language" },
            { new StringContent(Prompt), "prompt" },
            { new StringContent("json"), "response_format" },
        };
        using var response = await http.PostAsync("v1/audio/transcriptions", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        var transcription = await response.Content.ReadFromJsonAsync<Transcription>(cancellationToken);
        return transcription?.Text?.Trim() ?? "";
    }

    public async Task<byte[]> SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("v1/audio/speech",
            new Speech(options.SpeechModel, text, options.Voice, "pcm"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private sealed record Transcription([property: JsonPropertyName("text")] string? Text);

    private sealed record Speech(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice")] string Voice,
        [property: JsonPropertyName("response_format")] string ResponseFormat);
}

public static class ChatSpeechExtensions
{
    public const string UrlKey = "Speech:Url";
    public const string TranscriptionModelKey = "Speech:TranscriptionModel";
    public const string SpeechModelKey = "Speech:SpeechModel";
    public const string VoiceKey = "Speech:Voice";

    /// <summary>The HTTP client the speech server is called through.</summary>
    public const string HttpClientName = "Speech";

    /// <summary>Kokoro's voice the bot speaks in, unless Speech:Voice says another.</summary>
    public const string DefaultVoice = "af_heart";

    /// <summary>
    /// The voice chat's speech (<see cref="IChatSpeech"/>) on the server at Speech:Url, with its models (Speech:TranscriptionModel,
    /// Speech:SpeechModel, both required with it) and voice (Speech:Voice). No Speech:Url: nothing registered, no voice.
    /// </summary>
    public static IServiceCollection AddChatSpeech(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration[UrlKey] is not { Length: > 0 } url)
            return services;
        var options = new SpeechOptions(Required(configuration, TranscriptionModelKey), Required(configuration, SpeechModelKey),
            configuration[VoiceKey] is { Length: > 0 } voice ? voice : DefaultVoice);
        services.AddHttpClient(HttpClientName, http =>
        {
            http.BaseAddress = new Uri(url.EndsWith('/') ? url : url + "/");
            // A line's transcription or a reply's speech, on the CPU: well within this
            http.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddTransient<IChatSpeech>(provider => new SpeachesSpeech(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName), options));
        return services;
    }

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"{key} is required with {UrlKey}");
}
