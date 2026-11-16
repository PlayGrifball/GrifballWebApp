using System.Net;
using System.Text.Json;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The voice chat's speech (<see cref="SpeachesSpeech"/>): speaches' OpenAI-compatible endpoints, transcription and speech.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SpeachesSpeechUnitTests
{
    /// <summary>The server: each request recorded (its path, content type and body), answered with <paramref name="body"/>.</summary>
    private sealed class Server(HttpStatusCode status, byte[] body) : HttpMessageHandler
    {
        public readonly List<(string Path, string? ContentType, byte[] Body)> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
        }
    }

    private static SpeachesSpeech Speech(Server server)
        => new(new HttpClient(server) { BaseAddress = new Uri("http://grif-speech:8000/") }, new SpeechOptions("whisper-small", "kokoro", "af_heart"));

    [Test]
    public async Task TranscribeAsync_SendsTheWav_AndGivesItsText()
    {
        var server = new Server(HttpStatusCode.OK, JsonSerializer.SerializeToUtf8Bytes(new { text = " Grif, who won last season? " }));
        byte[] wav = [1, 2, 3];

        var text = await Speech(server).TranscribeAsync(wav);

        var (path, contentType, body) = server.Requests.Single();
        var sent = System.Text.Encoding.Latin1.GetString(body);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Is.EqualTo("Grif, who won last season?"));
            Assert.That(path, Is.EqualTo("/v1/audio/transcriptions"));
            Assert.That(contentType, Is.EqualTo("multipart/form-data"));
            Assert.That(sent, Does.Contain("whisper-small").And.Contain("name=file").And.Contain("\u0001\u0002\u0003"));
            Assert.That(sent, Does.Contain(SpeachesSpeech.Prompt), "Its name spelled as the wake word expects");
        }
    }

    [Test]
    public async Task SpeakAsync_AsksForRawPcm_InTheVoice_AndGivesIt()
    {
        byte[] pcm = [9, 8, 7, 6];
        var server = new Server(HttpStatusCode.OK, pcm);

        var spoken = await Speech(server).SpeakAsync("Season 3.");

        var (path, contentType, body) = server.Requests.Single();
        var sent = JsonDocument.Parse(body).RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(spoken, Is.EqualTo(pcm));
            Assert.That(path, Is.EqualTo("/v1/audio/speech"));
            Assert.That(contentType, Is.EqualTo("application/json"));
            Assert.That((sent.GetProperty("model").GetString(), sent.GetProperty("voice").GetString(), sent.GetProperty("input").GetString(),
                sent.GetProperty("response_format").GetString()), Is.EqualTo(("kokoro", "af_heart", "Season 3.", "pcm")));
        }
    }

    [Test]
    public void AFailedCall_Throws()
    {
        var server = new Server(HttpStatusCode.InternalServerError, []);

        Assert.That(async () => await Speech(server).SpeakAsync("Hi"), Throws.InstanceOf<HttpRequestException>());
    }

    [Test]
    public void AddChatSpeech_WithAUrl_RegistersIt_ElseNone()
    {
        using var configured = new ServiceCollection().AddChatSpeech(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Speech:Url"] = "http://grif-speech:8000/",
            ["Speech:TranscriptionModel"] = "whisper-small",
            ["Speech:SpeechModel"] = "kokoro",
        }).Build()).BuildServiceProvider();
        using var none = new ServiceCollection().AddChatSpeech(new ConfigurationBuilder().Build()).BuildServiceProvider();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configured.GetService<IChatSpeech>(), Is.InstanceOf<SpeachesSpeech>());
            Assert.That(none.GetService<IChatSpeech>(), Is.Null, "No speech server: no voice");
            Assert.That(() => new ServiceCollection().AddChatSpeech(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Speech:Url"] = "http://grif-speech:8000/",
            }).Build()), Throws.InvalidOperationException.With.Message.Contain("Speech:TranscriptionModel"));
        }
    }
}
