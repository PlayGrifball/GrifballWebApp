using DiscordInterface.Generated;
using DiscordInterfaces;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.Extensions;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A substitute slash command context for the command handlers' tests, from Discord user <paramref name="discordUserId"/>:
/// it records the first responses (<see cref="Callbacks"/>) and each edit of a deferred one (<see cref="Edits"/>).
/// </summary>
public sealed class CommandContextRecorder
{
    public IDiscordApplicationCommandContext Context { get; } = Substitute.For<IDiscordApplicationCommandContext>();
    public List<InteractionCallbackProperties> Callbacks { get; } = [];
    public List<IDiscordMessageOptions> Edits { get; } = [];

    public CommandContextRecorder(ulong discordUserId = 31337)
    {
        Context.User.Id.Returns(discordUserId);
        Context.Interaction.User.Id.Returns(discordUserId);
        Context.Interaction
            .When(x => x.SendResponseAsync(Arg.Any<InteractionCallbackProperties>(), Arg.Any<bool>(), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>()))
            .Do(call => Callbacks.Add(call.Arg<InteractionCallbackProperties>()));
        Context.Interaction
            .When(x => x.ModifyResponseAsync(Arg.Any<Action<IDiscordMessageOptions>>(), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var options = Substitute.For<IDiscordMessageOptions>();
                // The options are fluent (x.WithContent(..).WithComponents(..)): each call returns them, so a chained
                // call is recorded here too, not on an automatic substitute
                options.ReturnsForAll<IDiscordMessageOptions>(options);
                call.Arg<Action<IDiscordMessageOptions>>()(options);
                Edits.Add(options);
            });
    }

    /// <summary>What edit <paramref name="index"/> passed to <paramref name="method"/>.</summary>
    public object? EditArgument(string method, int index = 0)
        => Edits[index].ReceivedCalls().Single(c => c.GetMethodInfo().Name == method).GetArguments()[0];

    /// <summary>The text the only edit set.</summary>
    public string? Content() => (string?)EditArgument(nameof(IDiscordMessageOptions.WithContent), Single());

    /// <summary>The embed edit <paramref name="index"/> set (the only one, by default).</summary>
    public EmbedProperties Embed(int? index = null)
        => ((IEnumerable<EmbedProperties>)EditArgument(nameof(IDiscordMessageOptions.WithEmbeds), index ?? Single())!).Single();

    private int Single()
    {
        Assert.That(Edits, Has.Count.EqualTo(1), "expected one edit of the response");
        return 0;
    }

    /// <summary>The first response was a message (not deferred), its content and flags.</summary>
    public InteractionMessageProperties Message()
    {
        var callback = Callbacks.Single();
        Assert.That(callback.Type, Is.EqualTo(InteractionCallbackType.Message));
        return ((InteractionCallbackProperties<InteractionMessageProperties>)callback).Data!;
    }

    /// <summary>Whether the response was deferred to the channel (true) or ephemerally (false); fails when it wasn't deferred.</summary>
    public bool DeferredPublicly()
    {
        var callback = Callbacks.Single();
        Assert.That(callback.Type, Is.EqualTo(InteractionCallbackType.DeferredMessage));
        var data = (callback as InteractionCallbackProperties<InteractionMessageProperties>)?.Data;
        return data?.Flags is null || !data.Flags.Value.HasFlag(NetCord.MessageFlags.Ephemeral);
    }
}
