using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Test;

/// <summary>CommandReplies' text within Discord's limits: a long list cut, saying how many more the site has; a long title cut.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CommandRepliesTests
{
    private static readonly string[] OneTwo = ["One", "Two"];

    [Test]
    public void Lines_TooManyForTheLimit_AreCut_SayingHowManyMore()
    {
        var lines = Enumerable.Range(1, 30).Select(i => $"Line {i:00} " + new string('x', 90)).ToList();

        var text = CommandReplies.Lines(lines, "None", limit: 1024, head: "Head\n", tail: "\nTail");

        var shown = text.Split('\n').Count(l => l.StartsWith("Line ", StringComparison.Ordinal));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Has.Length.LessThanOrEqualTo(1024));
            Assert.That(text, Does.StartWith("Head\nLine 01 ").And.EndWith($"{CommandReplies.More(30 - shown)}\nTail"));
            Assert.That(shown, Is.EqualTo(9), "Each line is 98 characters and a newline: nine fit under the limit less the room kept for the count");
        }
    }

    [Test]
    public void Lines_ThatFit_AreAllShown()
    {
        var text = CommandReplies.Lines(OneTwo, "None");

        Assert.That(text, Is.EqualTo("One\nTwo"));
    }

    [Test]
    public void Embed_ATitleOverDiscordsLimit_IsCutWithAnEllipsis()
    {
        var embed = CommandReplies.Embed(new string('t', 300), "https://grif.test");

        Assert.That(embed.Title, Is.EqualTo(new string('t', 255) + "…"));
    }
}
