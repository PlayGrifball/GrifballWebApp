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

        await DiscordInteractionPipeline.SetCurrentUserAsync(4242, new RecordingLogger<DiscordInteractionPipelineLookupUnitTests>(), services);

        using (Assert.EnterMultipleScope())
        {
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

        await DiscordInteractionPipeline.SetCurrentUserAsync(9999, new RecordingLogger<DiscordInteractionPipelineLookupUnitTests>(), services);

        Assert.That(ambientUser.UserId, Is.Null);
    }
}
