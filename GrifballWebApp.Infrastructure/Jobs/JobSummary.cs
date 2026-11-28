namespace GrifballWebApp.Infrastructure.Jobs;

/// <summary>The summaries the jobs' runs give on the Jobs page: one cap for all of them, and text cut to fit.</summary>
public static class JobSummary
{
    /// <summary>The longest summary a run's report gives.</summary>
    public const int MaxLength = 1000;

    /// <summary>
    /// <paramref name="text"/> at most <paramref name="length"/> characters, ending with an ellipsis when cut. Never cut inside a surrogate pair
    /// (an emoji): a lone half isn't text, and a database writing strict UTF-8 refuses it.
    /// </summary>
    public static string Shorten(string text, int length)
    {
        if (text.Length <= length)
            return text;
        var cut = length - 1;
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
            cut--;
        return text[..cut] + "…";
    }
}
