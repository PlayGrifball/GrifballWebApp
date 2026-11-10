using System.Text.Json;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Jobs;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The request bodies whose value-type fields are required: a body without one is refused (MVC answers 400) rather than
/// read as false or 0, which would turn a job or a source off, or unpause one, that the caller never asked about.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RequestBodyRequiredTests
{
    private static readonly JsonSerializerOptions Web = JsonSerializerOptions.Web;

    [TestCase(typeof(SetEnabledRequest), "{}")]
    [TestCase(typeof(SetPausedRequest), "{}")]
    [TestCase(typeof(SetJobPausedRequest), """{"group":"g","name":"n"}""")]
    [TestCase(typeof(SetJobIntervalRequest), """{"group":"g","name":"n"}""")]
    public void A_body_without_the_value_is_refused(Type request, string json)
    {
        Assert.That(() => JsonSerializer.Deserialize(json, request, Web), Throws.InstanceOf<JsonException>());
    }

    [Test]
    public void A_body_with_the_value_is_read()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonSerializer.Deserialize<SetEnabledRequest>("""{"enabled":false}""", Web), Is.EqualTo(new SetEnabledRequest(false)));
            Assert.That(JsonSerializer.Deserialize<SetPausedRequest>("""{"paused":true}""", Web), Is.EqualTo(new SetPausedRequest(true)));
            Assert.That(JsonSerializer.Deserialize<SetJobPausedRequest>("""{"group":"g","name":"n","paused":false}""", Web), Is.EqualTo(new SetJobPausedRequest("g", "n", false)));
            Assert.That(JsonSerializer.Deserialize<SetJobIntervalRequest>("""{"group":"g","name":"n","intervalSeconds":60}""", Web), Is.EqualTo(new SetJobIntervalRequest("g", "n", 60)));
        }
    }
}
