using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Signups;
using Microsoft.Extensions.Configuration;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SignupButtonHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402). TempResponse's 5-second wait
/// is zeroed assembly-wide (AssemblySetUp).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupButtonHandlerUnitTests : SqliteGrifballContextFixture
{
    private static UrlService Urls() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());

    private SignupButtonHandler Handler() => new(Db, new SignupsService(Db), Urls());

    private static IDiscordButtonInteractionContext ButtonContext(ulong discordUserId)
    {
        var context = Substitute.For<IDiscordButtonInteractionContext>();
        context.User.Id.Returns(discordUserId);
        return context;
    }

    private static void AssertSent(IDiscordButtonInteractionContext context, string expected)
        => context.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(p =>
            ((InteractionCallbackProperties<InteractionMessageProperties>)p).Data.Content == expected));

    [Test]
    public async Task Signup_RepliesToSetAGamertag_ForAnUnknownCaller()
    {
        var context = ButtonContext(909090);

        await Handler().Signup(context, 1);

        AssertSent(context, "You must set your gamertag first");
    }

    [Test]
    public async Task Signup_RepliesAlreadySignedUp_ForAnExistingSignup()
    {
        var season = await AddSeason("Winter");
        var user = await AddUser(42, "Grifter1");
        Db.Set<SeasonSignup>().Add(SeasonSignup.Seed(season.SeasonID, user.Id));
        await Db.SaveChangesAsync();
        var context = ButtonContext(42);

        await Handler().Signup(context, season.SeasonID);

        AssertSent(context, $"You are already signed up for this season. Remember you can do more things like set your availability [here](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)");
    }
}
