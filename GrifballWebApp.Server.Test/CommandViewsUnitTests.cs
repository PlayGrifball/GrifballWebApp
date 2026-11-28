using GrifballWebApp.Server.Commands;
using NetCord.Rest;

namespace GrifballWebApp.Server.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommandViewsUnitTests
{
    private static readonly string[] TwentyFive = [.. Enumerable.Range(1, 25).Select(i => $"line {i}")];
    private static readonly string[] Three = ["a", "b", "c"];

    [TestCase(0, 0, 10, "line 1")]
    [TestCase(2, 2, 5, "line 21")]
    [TestCase(-1, 0, 10, "line 1")]
    [TestCase(9, 2, 5, "line 21")]
    public void Slice_IsTheLinesOfThePage_KeptWithinThePagesThereAre(int asked, int shown, int count, string first)
    {
        var (lines, page, pages) = CommandViews.Slice(TwentyFive, asked);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((page, pages), Is.EqualTo((shown, 3)));
            Assert.That(lines, Has.Count.EqualTo(count));
            Assert.That(lines[0], Is.EqualTo(first));
        }
    }

    [Test]
    public void Slice_OfNoLines_IsOneEmptyPage()
    {
        var (lines, page, pages) = CommandViews.Slice([], 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines, Is.Empty);
            Assert.That((page, pages), Is.EqualTo((0, 1)));
        }
    }

    [Test]
    public void Pager_IsNone_ForOnePage_AndDisablesTheEnds()
    {
        var first = CommandViews.Pager("standings", "12", 0, 3)!.OfType<ButtonProperties>().ToList();
        var last = CommandViews.Pager("standings", "12", 2, 3)!.OfType<ButtonProperties>().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CommandViews.Pager("standings", "12", 0, 1), Is.Null);
            Assert.That(first.Select(b => (b.CustomId, b.Disabled)), Is.EqualTo(new[] { ("page:standings:12:-1", true), ("page:standings:12:1", false) }));
            Assert.That(last.Select(b => (b.CustomId, b.Disabled)), Is.EqualTo(new[] { ("page:standings:12:1", false), ("page:standings:12:3", true) }));
        }
    }

    [Test]
    public void List_OfOnePage_HasNoFooterOrPager_AndKeepsWhatIsAbove()
    {
        var above = new StringMenuProperties("seasonview:standings", [new StringMenuSelectOptionProperties("Winter", "id:1")]);

        var view = CommandViews.List("standings", "1", 0, new CommandList("Winter standings", "https://grif.test/season/1", Three, "None", "\n\nmore"), [above]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Embed.Description, Is.EqualTo("a\nb\nc\n\nmore"));
            Assert.That(view.Embed.Footer, Is.Null);
            Assert.That(view.Components, Is.EqualTo(new IMessageComponentProperties[] { above }));
        }
    }

    [Test]
    public void List_OfSeveralPages_SaysWhichPage_UnderItsPager()
    {
        var view = CommandViews.List("signups", "1", 1, new CommandList("Winter signups", "https://grif.test/season/1", TwentyFive, "None"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Embed.Footer?.Text, Is.EqualTo("Page 2 of 3"));
            Assert.That(view.Components.Single(), Is.TypeOf<ActionRowProperties>());
        }
    }
}
