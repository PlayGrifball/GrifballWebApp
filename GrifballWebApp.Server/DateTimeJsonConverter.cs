using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server;

public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private readonly static JsonConverter<DateTime> _defaultConverter =
        (JsonConverter<DateTime>)JsonSerializerOptions.Default.GetConverter(typeof(DateTime));

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TryGetDateTimeOffset(out var dateTimeOffset))
        {
            return dateTimeOffset.ToUniversalTime().DateTime;
        }
        else
        {
            return _defaultConverter.Read(ref reader, typeToConvert, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        _defaultConverter.Write(writer, new DateTime(value.Ticks, DateTimeKind.Utc), options);
    }
}
