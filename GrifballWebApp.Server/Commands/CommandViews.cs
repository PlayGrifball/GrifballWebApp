using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure.Discord;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// A reply that can change in place: its embed and what is under it (◀ ▶ to page a long list, a menu to switch season, stat or view).
/// A click re-renders it from the IDs in the component's custom ID and edits the message (<see cref="CommandViews.Show"/>).
/// </summary>
public sealed record CommandView(EmbedProperties Embed, IReadOnlyList<IMessageComponentProperties> Components);

/// <summary>A list reply: its title, the site's page it links to, its lines, what it says with none, and what follows them.</summary>
public sealed record CommandList(string Title, string Url, IReadOnlyList<string> Lines, string Empty, string Tail = "");

/// <summary>Paging a long list reply and showing a <see cref="CommandView"/>.</summary>
public static class CommandViews
{
    /// <summary>How many lines a page shows.</summary>
    public const int PageSize = 10;

    /// <summary>
    /// Page <paramref name="page"/> of <paramref name="lines"/> (kept within the pages there are), the page shown and how many there are; one
    /// page when there are no lines.
    /// </summary>
    public static (IReadOnlyList<string> Lines, int Page, int Pages) Slice(IReadOnlyList<string> lines, int page)
    {
        var pages = Math.Max(1, (lines.Count + PageSize - 1) / PageSize);
        var shown = Math.Clamp(page, 0, pages - 1);
        return ([.. lines.Skip(shown * PageSize).Take(PageSize)], shown, pages);
    }

    /// <summary>The ◀ ▶ row for list <paramref name="view"/> of <paramref name="key"/>; none with one page.</summary>
    public static ActionRowProperties? Pager(string view, string key, int page, int pages)
        => pages <= 1 ? null : new ActionRowProperties(
        [
            new ButtonProperties(DiscordButtonContants.PageWithParams(view, key, page - 1), "◀", ButtonStyle.Secondary) { Disabled = page == 0 },
            new ButtonProperties(DiscordButtonContants.PageWithParams(view, key, page + 1), "▶", ButtonStyle.Secondary) { Disabled = page == pages - 1 },
        ]);

    /// <summary>The footer of page <paramref name="page"/> of <paramref name="pages"/>; none with one page.</summary>
    public static EmbedFooterProperties? Footer(int page, int pages) => pages <= 1 ? null : new EmbedFooterProperties { Text = $"Page {page + 1} of {pages}" };

    /// <summary>Page <paramref name="page"/> of list <paramref name="list"/> (<paramref name="view"/> of <paramref name="key"/>), under it <paramref name="above"/> and the ◀ ▶ row.</summary>
    public static CommandView List(string view, string key, int page, CommandList list, IEnumerable<IMessageComponentProperties>? above = null)
    {
        var (shown, at, pages) = Slice(list.Lines, page);
        var embed = CommandReplies.Embed(list.Title, list.Url, CommandReplies.Lines(shown, list.Empty, tail: list.Tail));
        embed.Footer = Footer(at, pages);
        return new CommandView(embed, [.. above ?? [], .. Pager(view, key, at, pages) is { } pager ? [pager] : Array.Empty<IMessageComponentProperties>()]);
    }

    /// <summary>A view as a deferred command's reply.</summary>
    public static Task Reply(this IDiscordInteractionContext context, CommandView view)
        => context.Interaction.ModifyResponseAsync(x => x.WithEmbeds([view.Embed]).WithComponents(view.Components));

    /// <summary>A view in place of the message whose component was clicked, as that click's response.</summary>
    public static Task Show(this IDiscordInteractionContext context, CommandView view)
        => context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(x => x.WithEmbeds([view.Embed]).WithComponents(view.Components)));
}
