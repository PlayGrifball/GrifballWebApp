using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>A Halo Infinite client factory whose probe answers as told. Shared by this project's, Server.Test's and
/// Integration.Test's health check tests.</summary>
internal static class HaloInfiniteHealthCheckTestData
{
    /// <summary>A factory whose probe answers with <paramref name="code"/>.</summary>
    internal static IHaloInfiniteClientFactory Answering(int code, IHaloInfiniteClientFactory? factory = null)
    {
        factory ??= Substitute.For<IHaloInfiniteClientFactory>();
        factory.StatsGetMatchStats(Arg.Any<string>()).Returns(Response(code));
        return factory;
    }

    internal static HaloApiResultContainer<MatchStats, HaloApiErrorContainer> Response(int code) =>
        new(new MatchStats(), new HaloApiErrorContainer { Code = code });
}
