using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.Rest;
using NSubstitute;
using DiscordInterfaces;
using RadioGroupProperties = NetCord.Rest.RadioGroupProperties;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// ChatFormHandler's Fill in button, on a substitute context (<see cref="CommandContextRecorder"/>) and a temp-file SQLite GrifballContext for
/// who clicked: only the asker opens the form, while forms are on and it still waits. Its submit runs end to end (DiscordTransportTests).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ChatFormHandlerUnitTests : SqliteGrifballContextFixture
{
    private const ulong Clicker = 31337;
    private static readonly string[] Labels = ["Team name", "Why", "Side"];
    private static readonly string[] OnlyGone = [ChatFormHandler.Gone];
    private static readonly ChatForm Form = new("Your team",
    [
        new ChatFormField("Team name", Placeholder: "The Grifs"),
        new ChatFormField("Why", ChatFormFieldKind.Paragraph, Required: false),
        new ChatFormField("Side", ChatFormFieldKind.Choice, Choices: ["Red", "Blue"]),
    ]);

    private readonly MemoryChatForms _forms = new(TimeProvider.System);

    private ChatFormHandler Handler(bool formsOn = true)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ChatForms.EnabledKey] = formsOn ? null : "false" }).Build();
        // The submit's chat isn't reached by a click
        return new ChatFormHandler(_forms, new DiscordCaller(Db), null!, config);
    }

    private Task Hold(string asker) => _forms.SaveAsync("f1", new HeldForm(Form, asker, Direct: false), ChatForms.Lasts);

    [Test]
    public async Task Open_ForTheAsker_IsTheFormsModal_ATextInputPerTextField_ARadioGroupPerChoice()
    {
        await Hold(ChatLimits.Discord(Clicker));
        var discord = new CommandContextRecorder(Clicker);

        await Handler().Open(discord.Context, "f1");

        var modal = discord.Modal();
        var fields = modal.Components.Cast<LabelProperties>().ToList();
        var name = (TextInputProperties)fields[0].Component;
        var why = (TextInputProperties)fields[1].Component;
        var side = (RadioGroupProperties)fields[2].Component;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(modal.CustomId, Is.EqualTo(DiscordModalsContants.ChatFormAnswersWithParams("f1")));
            Assert.That(modal.Title, Is.EqualTo("Your team"));
            Assert.That(fields.Select(f => f.Label), Is.EqualTo(Labels));
            Assert.That((name.CustomId, name.Style, name.Required, name.MaxLength, name.Placeholder), Is.EqualTo(("field0", TextInputStyle.Short, true, ChatForms.ShortLength, "The Grifs")));
            Assert.That((why.CustomId, why.Style, why.Required, why.MaxLength), Is.EqualTo(("field1", TextInputStyle.Paragraph, false, ChatForms.ParagraphLength)));
            Assert.That(side.CustomId, Is.EqualTo("field2"));
            Assert.That(side.Options.Select(o => (o.Label, o.Value)), Is.EqualTo(new[] { ("Red", "Red"), ("Blue", "Blue") }));
            Assert.That(await _forms.PeekAsync("f1"), Is.Not.Null, "opening it leaves it waiting");
        }
    }

    [TestCase(false, "discord:1", ChatFormHandler.TurnedOff)]
    [TestCase(true, "discord:1", ChatFormHandler.NotYours)]
    [TestCase(true, null, ChatFormHandler.Gone)]
    public async Task Open_IsRefusedEphemerally_WhenFormsAreOff_ForSomeoneElse_OrGone(bool formsOn, string? asker, string refusal)
    {
        if (asker is not null)
            await Hold(asker);
        var discord = new CommandContextRecorder(Clicker);

        await Handler(formsOn).Open(discord.Context, "f1");

        var message = discord.Message();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo(refusal));
            Assert.That(message.Flags, Is.EqualTo(MessageFlags.Ephemeral));
        }
    }

    /// <summary>A submit of a form's modal by <see cref="Clicker"/> with no answers, its responses kept in <paramref name="responses"/>.</summary>
    private static IDiscordModalInteractionContext Submitted(List<InteractionCallbackProperties> responses)
    {
        var context = Substitute.For<IDiscordModalInteractionContext>();
        context.Interaction.User.Id.Returns(Clicker);
        context.Components.Returns([]);
        context.Interaction
            .When(x => x.SendResponseAsync(Arg.Any<InteractionCallbackProperties>(), Arg.Any<bool>(), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>()))
            .Do(call => responses.Add(call.Arg<InteractionCallbackProperties>()));
        return context;
    }

    private static string? Content(InteractionCallbackProperties response) => ((InteractionCallbackProperties<InteractionMessageProperties>)response).Data!.Content;

    [Test]
    public async Task Submit_OfAFormGone_SaysSo()
    {
        var responses = new List<InteractionCallbackProperties>();

        await Handler().Submit(Submitted(responses), "f1");

        Assert.That(responses.Select(Content), Is.EqualTo(OnlyGone));
    }

    [Test]
    public async Task Submit_TakenMeanwhile_ByAnotherSubmit_SaysItIsGone_AndPostsNothing()
    {
        var forms = Substitute.For<IChatForms>();
        var optional = new ChatForm("Notes", [new ChatFormField("Anything else", Required: false)]);
        forms.PeekAsync("f1", Arg.Any<CancellationToken>()).Returns(new HeldForm(optional, ChatLimits.Discord(Clicker), Direct: false));
        forms.TakeAsync("f1", Arg.Any<CancellationToken>()).Returns((HeldForm?)null);
        var responses = new List<InteractionCallbackProperties>();
        var config = new ConfigurationBuilder().Build();

        await new ChatFormHandler(forms, new DiscordCaller(Db), null!, config).Submit(Submitted(responses), "f1");

        Assert.That(responses.Select(Content), Is.EqualTo(OnlyGone));
    }
}
