using DiscordInterface.Generated;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// How the slash commands answer: defer first (a lookup may take longer than Discord's 3 seconds), then fill the reply in.
/// Lookups anyone can do are posted to the channel; a personal or staff command's reply is ephemeral, seen only by its caller.
/// </summary>
public static class CommandReplies
{
    /// <summary>Discord's limits on an embed's description and a field's value; longer lists are cut with <see cref="More"/>.</summary>
    internal const int DescriptionLimit = 4096;
    internal const int FieldLimit = 1024;

    /// <summary>The colour of the bot's embeds: the queue embed's blue.</summary>
    public static readonly Color Blue = new(88, 101, 242);

    public static Task Defer(this IDiscordInteractionContext context, bool ephemeral)
        => context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(ephemeral ? MessageFlags.Ephemeral : null));

    public static Task Reply(this IDiscordInteractionContext context, EmbedProperties embed)
        => context.Interaction.ModifyResponseAsync(x => x.WithEmbeds([embed]));

    public static Task Reply(this IDiscordInteractionContext context, string content)
        => context.Interaction.ModifyResponseAsync(x => x.WithContent(content));

    /// <summary>Discord's limit on an embed's title.</summary>
    internal const int TitleLimit = 256;

    /// <summary>An embed titled <paramref name="title"/> (cut to Discord's limit) that links to the site's page for it.</summary>
    public static EmbedProperties Embed(string title, string url, string? description = null) => new()
    {
        Title = title.Length <= TitleLimit ? title : title[..(TitleLimit - 1)] + "…",
        Url = url,
        Description = description,
        Color = Blue,
    };

    /// <summary>Lines for an embed field's value, as many as fit, then how many more there are on the site.</summary>
    public static string FieldLines(IReadOnlyCollection<string> lines, string empty) => Lines(lines, empty, FieldLimit);

    /// <summary>
    /// An embed's description: <paramref name="head"/>, then as many lines as fit before <paramref name="tail"/>, then how many
    /// more there are on the site, then the tail; all within Discord's <paramref name="limit"/>. With no lines, <paramref name="empty"/>
    /// takes their place.
    /// </summary>
    public static string Lines(IReadOnlyCollection<string> lines, string empty, int limit = DescriptionLimit, string head = "", string tail = "")
        => head + Body(lines, empty, limit - head.Length - tail.Length) + tail;

    private static string Body(IReadOnlyCollection<string> lines, string empty, int limit)
    {
        if (lines.Count == 0)
            return empty;
        var text = new System.Text.StringBuilder();
        var shown = 0;
        foreach (var line in lines)
        {
            if (text.Length + line.Length + 1 > limit - 40)
                break;
            text.Append(line).Append('\n');
            shown++;
        }
        if (shown < lines.Count)
            text.Append(More(lines.Count - shown));
        return text.ToString().TrimEnd();
    }

    public static string More(int count) => $"…and {count} more on the site";
}
