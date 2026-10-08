using GrifballWebApp.Server;
using System.Text.Json;

namespace GrifballWebApp.Test;

[TestFixture]
public class TimeOnlyJsonConverterTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimeOnlyJsonConverter());
        return options;
    }

    [Test]
    public void Read_PlainTime_UsesDefaultConverter()
    {
        var result = JsonSerializer.Deserialize<TimeOnly>("\"10:30:15\"", Options());

        Assert.That(result, Is.EqualTo(new TimeOnly(10, 30, 15)));
    }

    [Test]
    public void Read_FullDateTimeWithOffset_TakesClockTimeOfDay()
    {
        // The time of day is taken in the offset supplied by the client, the offset itself is discarded.
        var result = JsonSerializer.Deserialize<TimeOnly>("\"2024-03-01T22:15:00+05:00\"", Options());

        Assert.That(result, Is.EqualTo(new TimeOnly(22, 15)));
    }

    [Test]
    public void Read_Invalid_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TimeOnly>("\"25:99\"", Options()));
    }

    [Test]
    public void Write_UsesDefaultFormat_AndRoundTrips()
    {
        var options = Options();
        var value = new TimeOnly(7, 5, 3);

        var json = JsonSerializer.Serialize(value, options);
        var back = JsonSerializer.Deserialize<TimeOnly>(json, options);

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo("\"07:05:03\""));
            Assert.That(back, Is.EqualTo(value));
        });
    }
}
