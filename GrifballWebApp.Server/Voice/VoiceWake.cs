using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Voice;

/// <summary>Whether a line said in a voice channel is for the bot: it starts with its name ("Grif, …", "Hey Grif …").</summary>
public static partial class VoiceWake
{
    /// <summary>The question in <paramref name="transcript"/> when it is addressed to the bot by name, else null (nothing after the name too).</summary>
    public static string? Question(string transcript)
    {
        var match = Addressed().Match(transcript);
        return match.Success && match.Groups["question"].Value.Trim() is { Length: > 0 } question ? question : null;
    }

    // Its name at the start, after "hey" or "ok" at most, as Whisper spells it (Grif, Griff); not a longer word (Griffin)
    [GeneratedRegex(@"^\s*(?:(?:hey|ok|okay)[,.!]?\s+)?griff?\b[\s,.!?:;-]*(?<question>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Addressed();
}
