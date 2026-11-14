using Tapper;

namespace GrifballWebApp.Application.Chat;

/// <summary>A question for the site's chat, with the conversation so far: the browser keeps it, for its tab's session.</summary>
[TranspilationSource]
public sealed record ChatRequestDto
{
    /// <summary>The question, at most <see cref="GrifChat.MaxQuestionLength"/> characters.</summary>
    public required string Question { get; init; }

    /// <summary>The conversation so far, oldest first: only its last <see cref="GrifChat.HistoryLimit"/> messages are read.</summary>
    public required List<ChatTurnDto> History { get; init; }
}

/// <summary>One message of a conversation with the site's chat.</summary>
[TranspilationSource]
public sealed record ChatTurnDto
{
    /// <summary>Whether the chat wrote it (else the person did).</summary>
    public required bool FromBot { get; init; }

    public required string Text { get; init; }
}
