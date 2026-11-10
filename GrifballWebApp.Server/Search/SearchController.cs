using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Search;

namespace GrifballWebApp.Server.Search;

[Route("[controller]/[action]")]
[ApiController]
public class SearchController : ControllerBase
{
    private readonly QuickSearchService _search;

    public SearchController(QuickSearchService search)
    {
        _search = search;
    }

    /// <summary>The Ctrl+K navigator's hits for <paramref name="q"/>: players (current and past gamertags), teams and seasons.</summary>
    [HttpGet(Name = "QuickSearch")]
    public Task<QuickSearchDto> QuickSearch([FromQuery] string? q, CancellationToken ct) => _search.SearchAsync(q, ct);
}
