using GrifballWebApp.Server.Extensions;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// TempResponse and ModifyTempResponse (InteractionResponseExtensions) wait TempResponseLifetime before deleting the
/// reply; zeroed for every test in this assembly, so one that reaches a temporary response doesn't really wait it out (#402).
/// </summary>
[SetUpFixture]
public class AssemblySetUp
{
    [OneTimeSetUp]
    public void ZeroTempResponseLifetime() => InteractionResponseExtensions.TempResponseLifetime = TimeSpan.Zero;
}
