using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server;

/// <summary>
/// The JSON settings of the REST API (on top of ASP.NET's web defaults), in one place so tests
/// can serialize exactly as the API does.
/// </summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateTimeJsonConverter());
        options.Converters.Add(new TimeOnlyJsonConverter());
    }

    /// <summary>Options equal to the API's: web defaults plus <see cref="Configure"/>.</summary>
    public static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
