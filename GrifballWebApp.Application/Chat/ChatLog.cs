namespace GrifballWebApp.Application.Chat;

/// <summary>The chats' own log lines (a tool call's is the tools': GrifTools).</summary>
internal static partial class ChatLog
{
    /// <summary>A telemetry switch that isn't one (<see cref="ChatModel.SensitiveDataFrom"/>).</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Key} is neither true nor false: the chats' telemetry leaves out what people wrote")]
    public static partial void NotABoolean(ILogger logger, string key);

    /// <summary>A paid reply's cost that couldn't be added to its user's month (<see cref="ChatAccounts.TryRecordAsync"/>).</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "A paid chat reply's cost ({CostMicros} millionths of a dollar) wasn't added to user {UserId}'s month")]
    public static partial void SpendNotSaved(ILogger logger, Exception exception, int userId, long costMicros);
}
