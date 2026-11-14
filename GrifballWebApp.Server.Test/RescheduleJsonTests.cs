using System.Text.Json;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A reschedule's Discord thread ID reaches the browser exactly: a Discord ID (a snowflake, ~1.3e18) is past what a
/// JavaScript number holds (2^53), so as a JSON number the browser would read it rounded, a different thread.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RescheduleJsonTests
{
    [Test]
    public void DiscordThreadID_IsWrittenAsAString_EveryDigitKept()
    {
        var dto = new RescheduleDto { DiscordThreadID = "1300000000000000009" };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, ApiJson.Options()));
        var written = doc.RootElement.GetProperty("discordThreadID");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(written.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(written.GetString(), Is.EqualTo("1300000000000000009"));
        }
    }
}
