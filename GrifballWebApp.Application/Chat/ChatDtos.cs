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

    /// <summary>The model to answer (its ID); null: the user's choice, else their default.</summary>
    public string? Model { get; init; }
}

/// <summary>One message of a conversation with the site's chat.</summary>
[TranspilationSource]
public sealed record ChatTurnDto
{
    /// <summary>Whether the chat wrote it (else the person did).</summary>
    public required bool FromBot { get; init; }

    public required string Text { get; init; }
}

/// <summary>The user's answer to the change waiting for it (<see cref="GrifChat.ConfirmAsync"/>), with the conversation so far.</summary>
[TranspilationSource]
public sealed record ChatConfirmDto
{
    /// <summary>The waiting change's ID (<see cref="ChatActionDto.Id"/>).</summary>
    public required string ActionId { get; init; }

    /// <summary>Yes, do it; or no, cancel it.</summary>
    public required bool Approved { get; init; }

    /// <summary>The conversation so far, oldest first, as with a question.</summary>
    public required List<ChatTurnDto> History { get; init; }
}

/// <summary>A change waiting for the user's answer: its ID, and what it does as they're shown it.</summary>
[TranspilationSource]
public sealed record ChatActionDto
{
    public required string Id { get; init; }
    public required string Description { get; init; }
}
