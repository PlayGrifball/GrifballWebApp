using GrifballWebApp.Application.Identity;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>Who runs a chat model: the Infrastructure's client for it (<see cref="IChatClientFactory.Create"/>).</summary>
public enum ChatProvider
{
    /// <summary>An Ollama server (Ollama:Url), the league's own.</summary>
    Ollama,

    /// <summary>Anthropic's API (Anthropic:ApiKey): Claude.</summary>
    Anthropic,
}

/// <summary>
/// A model the chat can answer with (Chat:Models): its ID (what a user's choice keeps), the name people see, who runs it, the
/// provider's model, the roles that may use it (none: anyone), its price (none: free), its output limit, and whether it gets every tool
/// in private (<see cref="EveryTool"/>). The order matters: a caller's default is
/// the first they may use (<see cref="GrifChat.Allowed"/>), and a Discord channel's the first anyone may.
/// </summary>
public sealed record ChatModelOption(string Id, string Name, ChatProvider Provider, string Model)
{
    /// <summary>The roles that may use it; none: anyone, signed in or not.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>What its tokens cost; null: nothing (the chat counts no spend for it).</summary>
    public ChatPrice? Price { get; init; }

    /// <summary>
    /// The most tokens a call to it may write (its thinking included, on a model that thinks); null: the provider's own default
    /// (Anthropic's client's is 1,024, too few for a model that thinks first).
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Whether it picks well from every tool (Claude): then, in private, it gets every tool the caller's roles allow, as the MCP server
    /// gives them; else the chat's short list (<see cref="Tools.GrifTools.PrivateChat"/>), which a small model picks from better.
    /// </summary>
    public bool EveryTool { get; init; }

    /// <summary>Anyone may use it: no roles.</summary>
    public bool IsOpen => Roles.Count == 0;

    /// <summary>Whether <paramref name="roles"/> may use it.</summary>
    public bool AllowsRoles(IReadOnlyCollection<string> roles) => IsOpen || SiteRoles.IsInAnyRole(roles, Roles);
}

/// <summary>
/// A paid model's prices, in US dollars per million tokens: input, output (thinking included), and the prompt cache's writes and
/// reads. A reply's cost is <see cref="CostMicros"/> of its usage.
/// </summary>
public sealed record ChatPrice(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead)
{
    /// <summary>
    /// The usage's count of input tokens written to the prompt cache, as Anthropic's client reports it (<see cref="UsageDetails.AdditionalCounts"/>):
    /// a part of <see cref="UsageDetails.InputTokenCount"/>, as <see cref="UsageDetails.CachedInputTokenCount"/> (the cache's reads) is.
    /// </summary>
    public const string CacheWriteCount = "CacheCreationInputTokens";

    /// <summary>
    /// What <paramref name="usage"/> costs, in millionths of a US dollar, rounded up: the input tokens that were neither read from nor
    /// written to the cache at <see cref="Input"/>, the cache's reads at <see cref="CacheRead"/>, its writes at
    /// <see cref="CacheWrite"/>, and the output at <see cref="Output"/>.
    /// </summary>
    public long CostMicros(UsageDetails usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var input = usage.InputTokenCount ?? 0;
        var cacheRead = usage.CachedInputTokenCount ?? 0;
        long cacheWrite = 0;
        if (usage.AdditionalCounts is { } counts && counts.TryGetValue(CacheWriteCount, out var written))
            cacheWrite = written;
        var plain = Math.Max(0, input - cacheRead - cacheWrite);
        // Dollars a million tokens are micro-dollars a token
        var micros = plain * Input + cacheRead * CacheRead + cacheWrite * CacheWrite + (usage.OutputTokenCount ?? 0) * Output;
        return (long)Math.Ceiling(micros);
    }
}

/// <summary>A model for the site's picker: its ID and the name people see.</summary>
[Tapper.TranspilationSource]
public sealed record ChatModelDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// The site's chat models for the signed-in user: the ones they may use, the one their questions go to, and, when any of theirs is
/// paid, this month's spend on them and its limit (US dollars).
/// </summary>
[Tapper.TranspilationSource]
public sealed record ChatModelsDto
{
    public required List<ChatModelDto> Models { get; init; }

    /// <summary>The model a question without one goes to: the user's choice, else their default. Null when there is none.</summary>
    public string? Selected { get; init; }

    /// <summary>This month's spend on paid models; null when none of theirs is paid.</summary>
    public decimal? Spent { get; init; }

    /// <summary>The monthly limit on it; null when none of theirs is paid.</summary>
    public decimal? Limit { get; init; }

    /// <summary>
    /// Whether a change the chat makes for them runs without asking, by default (<see cref="GrifChat.AutoApproveAsync"/>); null when
    /// they have no change to make (no tool of theirs changes data), so no setting.
    /// </summary>
    public bool? AutoApprove { get; init; }
}
