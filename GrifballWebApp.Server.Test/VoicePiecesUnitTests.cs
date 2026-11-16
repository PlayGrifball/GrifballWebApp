using GrifballWebApp.Server.Voice;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Server.Test;

/// <summary>The voice chat's pieces that need no Discord: the wake phrase, the audio's conversions and each speaker's lines.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class VoicePiecesUnitTests
{
    [TestCase("Grif, who won last season?", "who won last season?")]
    [TestCase("griff who won last season", "who won last season")]
    [TestCase("Hey Grif. What's on tonight?", "What's on tonight?")]
    [TestCase("  OK, Grif! Who's top of the leaderboard? ", "Who's top of the leaderboard?")]
    [TestCase("Grif", null)]
    [TestCase("Grif,", null)]
    [TestCase("I think Grif won it", null)]
    [TestCase("Griffin, come here", null)]
    [TestCase("", null)]
    public void Question_OnlyALineAddressedToTheBot_WithSomethingAfterItsName(string transcript, string? question)
        => Assert.That(VoiceWake.Question(transcript), Is.EqualTo(question));

    [Test]
    public void Wav_ThreeIn_OneOut_At16kHzMono_WithItsHeader()
    {
        short[] pcm48k = [3, 6, 9, 30, 30, 30, -3, -6, -9];

        var wav = VoicePcm.Wav16kMono(pcm48k);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(System.Text.Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(System.Text.Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(BitConverter.ToInt32(wav, 24), Is.EqualTo(16_000), "Sample rate");
            Assert.That(BitConverter.ToInt16(wav, 22), Is.EqualTo((short)1), "Mono");
            Assert.That(BitConverter.ToInt32(wav, 40), Is.EqualTo(6), "Three samples, two bytes each");
            Assert.That(new[] { BitConverter.ToInt16(wav, 44), BitConverter.ToInt16(wav, 46), BitConverter.ToInt16(wav, 48) },
                Is.EqualTo(new short[] { 6, 30, -6 }), "Each three averaged");
        }
    }

    [Test]
    public void Stereo48k_From24kMono_EachSampleTwiceForTheRate_AndTwiceForTheChannels()
    {
        byte[] pcm24k = [.. BitConverter.GetBytes((short)100), .. BitConverter.GetBytes((short)-200)];

        var stereo = VoicePcm.Stereo48kFrom24kMono(pcm24k);

        Assert.That(stereo, Is.EqualTo(new short[] { 100, 100, 100, 100, -200, -200, -200, -200 }));
    }

    [Test]
    public void Lines_EndAfterASilence_EachSpeakersOwn_AndTooShortOnesAreDropped()
    {
        var time = new FakeTimeProvider();
        var lines = new VoiceLines(time);
        var second = new short[48_000];

        lines.Add(1, second);
        lines.Add(2, new short[4_800]);
        time.Advance(VoiceLines.Silence - TimeSpan.FromMilliseconds(1));
        var early = lines.Ended();
        lines.Add(1, second);
        time.Advance(VoiceLines.Silence);
        var ended = lines.Ended();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(early, Is.Empty, "Still speaking");
            Assert.That(ended.Select(l => (l.Speaker, l.Pcm.Length)), Is.EqualTo(new[] { (1UL, 96_000) }), "Speaker 2's tenth of a second: a cough");
            Assert.That(lines.Ended(), Is.Empty, "Given once");
        }
    }

    [Test]
    public void Lines_ALongOne_IsCutAtTheLimit()
    {
        var time = new FakeTimeProvider();
        var lines = new VoiceLines(time);

        for (var i = 0; i < VoiceLines.MaxSeconds + 5; i++)
            lines.Add(1, new short[48_000]);
        time.Advance(VoiceLines.Silence);

        Assert.That(lines.Ended().Single().Pcm, Has.Length.EqualTo(VoiceLines.MaxSeconds * 48_000));
    }

    [Test]
    public void Lines_ASpeakerForgotten_HasNoLine()
    {
        var time = new FakeTimeProvider();
        var lines = new VoiceLines(time);
        lines.Add(1, new short[48_000]);

        lines.Forget(1);
        time.Advance(VoiceLines.Silence);

        Assert.That(lines.Ended(), Is.Empty);
    }
}
