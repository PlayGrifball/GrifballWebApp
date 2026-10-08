using GrifballWebApp.Server;
using System.Text.Json;

namespace GrifballWebApp.Test.CovE;

[TestFixture]
public class DateTimeJsonConverterTests_E
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateTimeJsonConverter());
        return options;
    }

    [Test]
    public void Read_WithOffset_ConvertsToUtc()
    {
        var result = JsonSerializer.Deserialize<DateTime>("\"2024-03-01T10:00:00+02:00\"", Options());

        Assert.That(result, Is.EqualTo(new DateTime(2024, 3, 1, 8, 0, 0)));
    }

    [Test]
    public void Read_WithZuluSuffix_KeepsSameInstant()
    {
        var result = JsonSerializer.Deserialize<DateTime>("\"2024-03-01T23:59:30.5Z\"", Options());

        Assert.That(result, Is.EqualTo(new DateTime(2024, 3, 1, 23, 59, 30, 500)));
    }

    [Test]
    public void Read_InvalidString_FallsBackToDefaultConverterAndThrows()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTime>("\"not a date\"", Options()));
    }

    [Test]
    public void Write_UnspecifiedKind_IsEmittedAsUtc()
    {
        var json = JsonSerializer.Serialize(new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Unspecified), Options());

        Assert.That(json, Is.EqualTo("\"2024-03-01T10:00:00Z\""));
    }

    [Test]
    public void Write_LocalKind_IsReinterpretedAsUtcWithoutShifting()
    {
        // The converter treats every DateTime as UTC (the database stores UTC), it does not convert local times.
        var json = JsonSerializer.Serialize(new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Local), Options());

        Assert.That(json, Is.EqualTo("\"2024-03-01T10:00:00Z\""));
    }

    [Test]
    public void RoundTrip_PreservesValue()
    {
        var value = new DateTime(2025, 12, 31, 18, 45, 12, DateTimeKind.Utc);
        var options = Options();

        var json = JsonSerializer.Serialize(new { When = value }, options);
        using var doc = JsonDocument.Parse(json);
        var back = JsonSerializer.Deserialize<DateTime>(doc.RootElement.GetProperty("When").GetRawText(), options);

        Assert.That(back.Ticks, Is.EqualTo(value.Ticks));
    }
}

[TestFixture]
public class TimeOnlyJsonConverterTests_E
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
