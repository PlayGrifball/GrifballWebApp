using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server;

public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private readonly static JsonConverter<TimeOnly> _defaultConverter =
        (JsonConverter<TimeOnly>)JsonSerializerOptions.Default.GetConverter(typeof(TimeOnly));

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TryGetDateTimeOffset(out var dateTimeOffset))
        {
            return TimeOnly.FromTimeSpan(dateTimeOffset.TimeOfDay);
        }
        else
        {
            return _defaultConverter.Read(ref reader, typeToConvert, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        _defaultConverter.Write(writer, value, options);
    }
}
