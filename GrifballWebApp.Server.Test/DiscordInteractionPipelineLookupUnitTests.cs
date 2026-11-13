using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordInteractionPipeline.SetCurrentUserAsync's found-user path, on a real GrifballContext (temp-file SQLite,
/// #402): the lookup-fails path (no GrifballContext registered) is DiscordInteractionPipelineUnitTests'.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordInteractionPipelineLookupUnitTests : SqliteGrifballContextFixture
{
    [Test]
    public async Task SetCurrentUserAsync_SetsTheCurrentUser_AndTheAmbientUser_ForALinkedDiscordAccount()
    {
        var user = await AddUser(4242, "Someone");
        var services = new ServiceCollection()
            .AddSingleton(Db)
            .AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>()
            .AddSingleton(Substitute.For<IHttpContextAccessor>())
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .BuildServiceProvider();
        var ambientUser = services.GetRequiredService<IAmbientCurrentUser>();
        ambientUser.Begin();
        var logger = new RecordingLogger<DiscordInteractionPipelineLookupUnitTests>();

        await DiscordInteractionPipeline.SetCurrentUserAsync(4242, logger, services);

        using (Assert.EnterMultipleScope())
        {
            // SetCurrentUserAsync swallows every exception and only logs it, so without this the test would still pass
            // on a failed lookup (ambientUser.UserId would just stay null, which a different assertion already checks).
            Assert.That(logger.Entries, Is.Empty, "a successful lookup logs nothing");
            Assert.That(ambientUser.UserId, Is.EqualTo(user.Id));
            Assert.That(services.GetRequiredService<ICurrentUserService>().GetCurrentUserId(), Is.EqualTo(user.Id));
        }
    }

    [Test]
    public async Task SetCurrentUserAsync_LeavesTheCurrentUserUnset_ForAnUnlinkedDiscordAccount()
    {
        var services = new ServiceCollection()
            .AddSingleton(Db)
            .AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>()
            .AddSingleton(Substitute.For<IHttpContextAccessor>())
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .BuildServiceProvider();
        var ambientUser = services.GetRequiredService<IAmbientCurrentUser>();
        ambientUser.Begin();
        var logger = new RecordingLogger<DiscordInteractionPipelineLookupUnitTests>();

        await DiscordInteractionPipeline.SetCurrentUserAsync(9999, logger, services);

        using (Assert.EnterMultipleScope())
        {
            // An unlinked account is not a failed lookup: it still logs nothing (a thrown lookup, caught and logged,
            // would also leave UserId null, so the lack of a log entry is what tells the two apart).
            Assert.That(logger.Entries, Is.Empty, "an unlinked account logs nothing: it isn't a failed lookup");
            Assert.That(ambientUser.UserId, Is.Null);
        }
    }
}
