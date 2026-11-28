using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Application.Chat;

/// <summary>What a field of a chat form takes.</summary>
public enum ChatFormFieldKind
{
    Short,
    Paragraph,
    Choice,
}

/// <summary>One field of a form the chat made (<see cref="ChatForm"/>).</summary>
public sealed record ChatFormField(
    [property: Description("What the field asks, as its label: at most 45 characters.")] string Label,
    [property: Description("Short: one line of text; Paragraph: several lines; Choice: one of Choices.")] ChatFormFieldKind Kind = ChatFormFieldKind.Short,
    [property: Description("Whether it must be answered.")] bool Required = true,
    [property: Description("A hint shown in an empty text field: at most 100 characters.")] string? Placeholder = null,
    [property: Description("A Choice field's options, 2 to 10, each at most 100 characters; none for the others.")] IReadOnlyList<string>? Choices = null);

/// <summary>A form the chat made for an answer no existing form asks for: shown on Discord behind a Fill in button, only to the one who asked.</summary>
public sealed record ChatForm(string Title, IReadOnlyList<ChatFormField> Fields);

/// <summary>A form held for its asker (<see cref="ChatLimits.Asker"/>) until they answer it or it expires; <paramref name="Direct"/> when it was made in a DM.</summary>
public sealed record HeldForm(ChatForm Form, string Asker, bool Direct);

/// <summary>
/// The chat's forms (<c>offer_form</c>): their limits (Discord's for a modal, and what the answers' message holds), the token the tool returns
/// (<see cref="ChatReply.OfferedForm"/> reads it from that tool's result, never from the reply's text), and the answers as the asker's next turn.
/// Turned off with <see cref="EnabledKey"/>.
/// </summary>
public static partial class ChatForms
{
    /// <summary>The setting that turns the chat's forms off (false); on unless it says so.</summary>
    public const string EnabledKey = "Chat:Forms";

    public const int MaxFields = 5;
    public const int MaxTitle = 45;
    public const int MaxLabel = 45;
    public const int MaxPlaceholder = 100;
    public const int MinChoices = 2;
    public const int MaxChoices = 10;
    public const int MaxChoice = 100;

    /// <summary>The longest answer to a short field, and to a paragraph: five of them, with their labels, fit one Discord message.</summary>
    public const int ShortLength = 100;
    public const int ParagraphLength = 300;

    /// <summary>How long a form waits for its answers.</summary>
    public static readonly TimeSpan Lasts = TimeSpan.FromMinutes(10);

    /// <summary>The longest answer <paramref name="field"/> takes.</summary>
    public static int MaxLength(ChatFormField field) => field.Kind is ChatFormFieldKind.Paragraph ? ParagraphLength : ShortLength;

    public const string Incomplete = "A form needs a title and its fields, none of them empty.";

    /// <summary>
    /// The form the model gave, its text trimmed (a choice is picked as it is shown, and an answer is checked trimmed, <see cref="Invalid"/>);
    /// null when it left out the title, the fields or a field.
    /// </summary>
    public static ChatForm? Trimmed(string? title, IReadOnlyList<ChatFormField?>? fields)
    {
        if (title is null || fields is null || fields.Any(f => f is null))
            return null;
        return new(title.Trim(), [.. fields.Select(f => f! with
        {
            Label = f.Label?.Trim()!,
            Placeholder = f.Placeholder?.Trim(),
            Choices = f.Choices?.Select(c => c?.Trim()!).ToList(),
        })]);
    }

    /// <summary>Why <paramref name="form"/> can't be shown, for the model to fix; null when it can.</summary>
    public static string? Refusal(ChatForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Title) || form.Title.Length > MaxTitle)
            return $"A form needs a title of at most {MaxTitle} characters.";
        if (form.Fields.Count is 0 or > MaxFields)
            return $"A form has 1 to {MaxFields} fields.";
        return form.Fields.Select(FieldRefusal).FirstOrDefault(r => r is not null);
    }

    private static string? FieldRefusal(ChatFormField field)
    {
        if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > MaxLabel)
            return $"Each field needs a label of at most {MaxLabel} characters.";
        if (field.Placeholder is { Length: > MaxPlaceholder })
            return $"A placeholder is at most {MaxPlaceholder} characters.";
        if (field.Kind is not ChatFormFieldKind.Choice)
            return field.Choices is { Count: > 0 } ? $"Only a Choice field has choices (\"{field.Label}\")." : null;
        var choices = field.Choices ?? [];
        if (choices.Count is < MinChoices or > MaxChoices || choices.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > MaxChoice) || choices.Distinct().Count() != choices.Count)
            return $"A Choice field (\"{field.Label}\") has {MinChoices} to {MaxChoices} different choices, each at most {MaxChoice} characters.";
        return null;
    }

    [GeneratedRegex(@"\[form:(?<form>[A-Za-z0-9_-]+)\]")]
    private static partial Regex Pattern();

    /// <summary>The token <paramref name="form"/> is written as in <c>offer_form</c>'s result.</summary>
    public static string Token(ChatForm form)
        => $"[form:{Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(form)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}]";

    /// <summary>The form <paramref name="text"/> names (its first), as <see cref="Token"/> writes it; null for none, or one that isn't a form.</summary>
    public static ChatForm? In(string text)
    {
        var match = Pattern().Match(text);
        if (!match.Success)
            return null;
        var encoded = match.Groups["form"].Value.Replace('-', '+').Replace('_', '/');
        try
        {
            var form = JsonSerializer.Deserialize<ChatForm>(Convert.FromBase64String(encoded.PadRight(encoded.Length + ((4 - (encoded.Length % 4)) % 4), '=')));
            return form is not null && Refusal(form) is null ? form : null;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Why <paramref name="answers"/> (one per field, null for none) don't fit <paramref name="form"/>; null when they do.</summary>
    public static string? Invalid(ChatForm form, IReadOnlyList<string?> answers)
    {
        for (var i = 0; i < form.Fields.Count; i++)
        {
            var field = form.Fields[i];
            var answer = answers[i]?.Trim();
            if (string.IsNullOrEmpty(answer))
            {
                if (field.Required)
                    return $"\"{field.Label}\" needs an answer.";
            }
            else if (field.Kind is ChatFormFieldKind.Choice ? !field.Choices!.Contains(answer) : answer.Length > MaxLength(field))
            {
                return $"\"{field.Label}\" takes {(field.Kind is ChatFormFieldKind.Choice ? "one of its choices" : $"at most {MaxLength(field)} characters")}.";
            }
        }
        return null;
    }

    private const string AnswersStart = "My answers to \"";

    /// <summary>Whether <paramref name="text"/> is a form's answers, as <see cref="Answers"/> writes them.</summary>
    public static bool AreAnswers(string text) => text.StartsWith(AnswersStart, StringComparison.Ordinal);

    /// <summary>
    /// The answers as the asker's next turn: what the model reads, and what the channel or DM is shown, posted by the bot. So the asker's text
    /// is shown as typed, never as Discord's markdown (a masked link, a heading under the bot's name), and a line of it stays in its answer.
    /// </summary>
    public static string Answers(ChatForm form, IReadOnlyList<string?> answers)
    {
        var text = new StringBuilder($"{AnswersStart}{form.Title}\":");
        for (var i = 0; i < form.Fields.Count; i++)
            text.Append($"\n- {form.Fields[i].Label}: {(string.IsNullOrWhiteSpace(answers[i]) ? "(no answer)" : AsTyped(answers[i]!.Trim()))}");
        return text.ToString();
    }

    [GeneratedRegex(@"[\\*_~`|>#\[\]()<:-]")]
    private static partial Regex Markdown();

    private static string AsTyped(string answer) => Markdown().Replace(answer, @"\$0").ReplaceLineEndings("\n  ");

    /// <summary>A new form's ID: short, for its button's and modal's custom IDs (Discord's 100 characters).</summary>
    public static string NewId() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
}

/// <summary>The forms waiting for their answers (<see cref="HeldForm"/>), each expiring on its own: in memory, or in Redis to be shared.</summary>
public interface IChatForms
{
    /// <summary><paramref name="held"/> waits under <paramref name="id"/> for <paramref name="expiresIn"/>.</summary>
    Task SaveAsync(string id, HeldForm held, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    /// <summary>The form waiting under <paramref name="id"/>, left waiting; null when none.</summary>
    Task<HeldForm?> PeekAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>The form waiting under <paramref name="id"/>, no longer waiting: of two takes at once, one gets it, the other null.</summary>
    Task<HeldForm?> TakeAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>The waiting forms of one process (no Redis): in its memory, each dropped once it expires.</summary>
public sealed class MemoryChatForms(TimeProvider time) : IChatForms
{
    private readonly Dictionary<string, (HeldForm Held, DateTimeOffset Until)> _forms = [];
    private readonly Lock _lock = new();

    /// <summary>Holds <paramref name="held"/>, dropping the expired ones first: each form has an ID of its own, and most are never opened.</summary>
    public Task SaveAsync(string id, HeldForm held, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            foreach (var expired in _forms.Where(f => f.Value.Until <= now).Select(f => f.Key).ToList())
                _forms.Remove(expired);
            _forms[id] = (held, now + expiresIn);
        }
        return Task.CompletedTask;
    }

    /// <summary>How many forms it holds, the expired ones not dropped yet included.</summary>
    internal int Count
    {
        get
        {
            lock (_lock)
                return _forms.Count;
        }
    }

    public Task<HeldForm?> PeekAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(Live(id));
    }

    public Task<HeldForm?> TakeAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var held = Live(id);
            _forms.Remove(id);
            return Task.FromResult(held);
        }
    }

    /// <summary>The form under <paramref name="id"/> while it waits; an expired one dropped. Under the lock.</summary>
    private HeldForm? Live(string id)
    {
        if (!_forms.TryGetValue(id, out var waiting))
            return null;
        if (waiting.Until > time.GetUtcNow())
            return waiting.Held;
        _forms.Remove(id);
        return null;
    }
}
