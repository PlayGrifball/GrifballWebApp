namespace GrifballWebApp.Application.Chat;

/// <summary>The chats' own log lines (a tool call's is the tools': GrifTools).</summary>
internal static partial class ChatLog
{
    /// <summary>A telemetry switch that isn't one (<see cref="ChatModel.SensitiveDataFrom"/>).</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Key} is neither true nor false: the chats' telemetry leaves out what people wrote")]
    public static partial void NotABoolean(ILogger logger, string key);
}
