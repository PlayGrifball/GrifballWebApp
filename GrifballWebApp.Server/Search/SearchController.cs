using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Search;

namespace GrifballWebApp.Server.Search;

[ApiController]
public class SearchController : ControllerBase
{
    private readonly QuickSearchService _search;

    public SearchController(QuickSearchService search)
    {
        _search = search;
    }

    /// <summary>The Ctrl+K navigator's hits for <paramref name="q"/>: players (current and past gamertags), teams and seasons.</summary>
    [AllowAnonymous]
    [HttpGet("search", Name = "QuickSearch")]
    [ProducesResponseType<QuickSearchDto>(StatusCodes.Status200OK)]
    public Task<QuickSearchDto> QuickSearch([FromQuery] string? q, CancellationToken ct) => _search.SearchAsync(q, ct);
}
