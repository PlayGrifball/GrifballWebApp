using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
internal sealed class ChatFormsUnitTests
{
    private static readonly ChatFormField Name = new("Team name", Placeholder: "The Grifs");
    private static readonly ChatFormField Why = new("Why", ChatFormFieldKind.Paragraph, Required: false);
    private static readonly ChatFormField Side = new("Side", ChatFormFieldKind.Choice, Choices: ["Red", "Blue"]);
    private static readonly ChatForm Form = new("Your team", [Name, Why, Side]);
    private static readonly HeldForm Held = new(Form, "user:1", Direct: true);

    private sealed class Bot : ISlashCommandCatalog
    {
        public IReadOnlyList<SlashCommandDto> Commands() => [new("/help", "Help")];
    }

    [Test]
    public void Refusal_AFormInDiscordsLimits_IsNone_EachLimitPassed_IsNamed()
    {
        var tooManyChoices = Enumerable.Range(0, ChatForms.MaxChoices + 1).Select(i => $"c{i}").ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatForms.Refusal(Form), Is.Null);
            Assert.That(ChatForms.Refusal(Form with { Title = " " }), Does.Contain("title"));
            Assert.That(ChatForms.Refusal(Form with { Title = new string('t', ChatForms.MaxTitle + 1) }), Does.Contain("title"));
            Assert.That(ChatForms.Refusal(Form with { Fields = [] }), Does.Contain("1 to 5 fields"));
            Assert.That(ChatForms.Refusal(Form with { Fields = [Name, Name, Name, Name, Name, Name] }), Does.Contain("1 to 5 fields"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Name with { Label = new string('l', ChatForms.MaxLabel + 1) }])), Does.Contain("label"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Name with { Placeholder = new string('p', ChatForms.MaxPlaceholder + 1) }])), Does.Contain("placeholder"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Name with { Choices = ["a", "b"] }])), Does.Contain("Only a Choice field"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Side with { Choices = null }])), Does.Contain("2 to 10"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Side with { Choices = tooManyChoices }])), Does.Contain("2 to 10"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Side with { Choices = ["Red", "Red"] }])), Does.Contain("different"));
            Assert.That(ChatForms.Refusal(new ChatForm("T", [Side with { Choices = ["Red", " "] }])), Does.Contain("2 to 10"));
        }
    }

    [Test]
    public void Token_ReadsBackAsTheForm_AnythingElseIsNone()
    {
        var read = ChatForms.In($"Shown: {ChatForms.Token(Form)}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(read?.Title, Is.EqualTo(Form.Title));
            Assert.That(read?.Fields, Is.EqualTo(Form.Fields).Using<ChatFormField>(SameField));
            Assert.That(ChatForms.In("no form here"), Is.Null);
            Assert.That(ChatForms.In("[form:bm90IGpzb24]"), Is.Null, "not JSON");
            Assert.That(ChatForms.In("[form:A]"), Is.Null, "not base64");
            Assert.That(ChatForms.In(ChatForms.Token(Form with { Fields = [] })), Is.Null, "a form out of the limits");
        }
    }

    private static bool SameField(ChatFormField a, ChatFormField b)
        => a.Label == b.Label && a.Kind == b.Kind && a.Required == b.Required && a.Placeholder == b.Placeholder
            && (a.Choices ?? []).SequenceEqual(b.Choices ?? []);

    [Test]
    public void Invalid_ChecksRequiredChoicesAndLengths()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatForms.Invalid(Form, ["Grifs", null, "Red"]), Is.Null);
            Assert.That(ChatForms.Invalid(Form, [" ", null, "Red"]), Is.EqualTo("\"Team name\" needs an answer."));
            Assert.That(ChatForms.Invalid(Form, ["Grifs", null, "Green"]), Is.EqualTo("\"Side\" takes one of its choices."));
            Assert.That(ChatForms.Invalid(Form, [new string('a', ChatForms.ShortLength + 1), null, "Red"]), Is.EqualTo("\"Team name\" takes at most 100 characters."));
            Assert.That(ChatForms.Invalid(Form, ["Grifs", new string('a', ChatForms.ParagraphLength), "Red"]), Is.Null);
            Assert.That(ChatForms.Invalid(Form, ["Grifs", new string('a', ChatForms.ParagraphLength + 1), "Red"]), Is.EqualTo("\"Why\" takes at most 300 characters."));
        }
    }

    [Test]
    public void Answers_ListEachField_NoneForAnEmptyOne()
    {
        Assert.That(ChatForms.Answers(Form, [" Grifs ", null, "Red"]),
            Is.EqualTo("My answers to \"Your team\":\n- Team name: Grifs\n- Why: (no answer)\n- Side: Red"));
    }

    [Test]
    public void NewId_IsShortHex_AndNew()
    {
        var id = ChatForms.NewId();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(id, Does.Match("^[0-9a-f]{12}$"));
            Assert.That(ChatForms.NewId(), Is.Not.EqualTo(id));
        }
    }

    [Test]
    public async Task MemoryChatForms_PeekLeavesIt_TakeIsOnce_ItExpires()
    {
        var time = new FakeTimeProvider();
        var forms = new MemoryChatForms(time);
        await forms.SaveAsync("a", Held, TimeSpan.FromMinutes(10));
        await forms.SaveAsync("b", Held, TimeSpan.FromMinutes(10));

        var peeked = await forms.PeekAsync("a");
        var taken = await forms.TakeAsync("a");
        var again = await forms.TakeAsync("a");
        time.Advance(TimeSpan.FromMinutes(10));
        var expired = await forms.PeekAsync("b");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(peeked, Is.SameAs(Held));
            Assert.That(taken, Is.SameAs(Held));
            Assert.That(again, Is.Null);
            Assert.That(expired, Is.Null);
            Assert.That(await forms.PeekAsync("none"), Is.Null);
        }
    }

    [Test]
    public void OfferForm_GivesTheToken_RefusesABadFormAndAHostWithoutTheBot()
    {
        var shown = new FormTools(new Bot()).OfferForm(" Your team ", [Name]);
        var bad = Assert.Throws<ToolException>(() => new FormTools(new Bot()).OfferForm("Your team", []));
        var noBot = Assert.Throws<ToolException>(() => new FormTools(new NoSlashCommands()).OfferForm("Your team", [Name]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatForms.In(shown)?.Title, Is.EqualTo("Your team"));
            Assert.That(bad?.Message, Does.Contain("1 to 5 fields"));
            Assert.That(noBot?.Message, Does.Contain("Discord bot"));
        }
    }

    [Test]
    public void ToolsFor_FormsOff_LeavesOutOfferForm_Only()
    {
        var model = new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b");
        var off = (ChatHost.Discord with { Forms = false }).ToolsFor(model);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatHost.Discord.ToolsFor(model), Does.Contain(GrifTools.OfferForm));
            Assert.That(off, Does.Not.Contain(GrifTools.OfferForm));
            Assert.That(off, Is.EquivalentTo(ChatHost.Discord.ToolsFor(model).Except([GrifTools.OfferForm])));
        }
    }
}
