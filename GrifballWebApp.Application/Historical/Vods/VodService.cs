using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace GrifballWebApp.Application.Historical.Vods;

/// <summary>What a VOD refresh did.</summary>
public record VodSyncResult(int Added, int Updated, int Linked, int NeedsReview, List<string> Failed);

/// <summary>
/// The league's VODs: found by the historical refresh (HistoricalImportRunner, in the worker, daily and on demand) from
/// the YouTube channel sources (their latest snapshot of the channel page, the archive's capture, the uploads feed, and
/// the Data API when YouTube:ApiKey is set), one row per video ID; matched to seasons, teams and matches by title
/// (<see cref="VodMatcher"/>); the unsure ones decided by a sysadmin on the historical import page.
/// </summary>
public partial class VodService
{
    /// <summary>Shorter than this (a Short, a clip) isn't a VOD.</summary>
    public const int MinVodSeconds = 180;
    /// <summary>The most VODs a player's profile lists.</summary>
    public const int PlayerVodLimit = 50;

    private readonly IApplicationDbContext _context;
    private readonly YouTubeClient _youtube;
    private readonly YouTubeOptions _options;
    private readonly ILogger<VodService> _logger;

    public VodService(IApplicationDbContext context, YouTubeClient youtube, IOptions<YouTubeOptions> options, ILogger<VodService> logger)
    {
        _context = context;
        _youtube = youtube;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Finds the channels' videos, adds the new ones, completes the others (an exact date, a duration, a description) and
    /// matches again those not decided by a sysadmin. A source that can't be read is reported and skipped: the others still count.
    /// </summary>
    public async Task<VodSyncResult> Sync(Func<string, Task>? progress = null, CancellationToken ct = default)
    {
        var failed = new List<string>();
        var found = new FoundVideos();

        var channels = await _context.HistoricalSources.AsNoTracking()
            .Where(s => s.Kind == HistoricalSourceKind.YouTubeChannel && !s.IsDead)
            .Select(s => new { s.HistoricalSourceID, s.Url })
            .ToListAsync(ct);
        var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? null : _options.ApiKey.Trim();
        foreach (var channel in channels)
        {
            var channelId = YouTubeFeeds.ChannelIdFromUrl(channel.Url);
            if (channelId is null)
            {
                failed.Add($"{channel.HistoricalSourceID}: no channel ID in {channel.Url}");
                continue;
            }
            if (progress is not null)
                await progress($"Finding the VODs of {channel.HistoricalSourceID}");

            await AddKnownVideos(channel.HistoricalSourceID, channelId, found, ct);
            await FetchChannel(channel.HistoricalSourceID, channelId, apiKey, found, failed, ct);
        }

        if (apiKey is not null)
        {
            var hasArchive = channels.Any(c => YouTubeFeeds.ChannelIdFromUrl(c.Url) == VodArchive.ChannelID);
            await FetchDetails(hasArchive, apiKey, found, failed, ct);
        }

        var (added, updated) = await Upsert(found.Values, ct);
        var (linked, needsReview) = await Rematch(ct);
        LogRefreshed(_logger, found.Count, added, updated, linked, needsReview, failed.Count);
        return new VodSyncResult(added, updated, linked, needsReview, failed);
    }

    /// <summary>The videos found so far, one per video ID: what each source says of a video is merged into what was known.</summary>
    private sealed class FoundVideos
    {
        private readonly Dictionary<string, YouTubeVideo> _byID = new();

        public IEnumerable<YouTubeVideo> Values => _byID.Values;

        public int Count => _byID.Count;

        public void Add(YouTubeVideo video) => _byID[video.VideoID] = _byID.TryGetValue(video.VideoID, out var known) ? known.Merge(video) : video;
    }

    /// <summary>A channel's videos the site has already: the archive's list (for the archive channel), and the channel page
    /// the refresh just downloaded (or its last one).</summary>
    private async Task AddKnownVideos(string sourceID, string channelId, FoundVideos found, CancellationToken ct)
    {
        if (channelId == VodArchive.ChannelID)
        {
            foreach (var video in VodArchive.Videos)
                found.Add(new YouTubeVideo { VideoID = video.VideoID, Title = video.Title, PublishedAt = video.PublishedAt, PublishedAtIsApproximate = true, DurationSeconds = video.DurationSeconds, ChannelID = channelId });
        }

        var snapshot = await _context.HistoricalSources.AsNoTracking()
            .Where(s => s.HistoricalSourceID == sourceID).SelectMany(s => s.Snapshots)
            .OrderByDescending(s => s.FetchedAt).ThenByDescending(s => s.HistoricalSourceSnapshotID)
            .Select(s => new { s.FetchedAt, s.Content })
            .FirstOrDefaultAsync(ct);
        if (snapshot is not null)
            foreach (var video in YouTubeFeeds.ParseChannelPage(Encoding.UTF8.GetString(snapshot.Content), snapshot.FetchedAt, channelId))
                found.Add(video);
    }

    /// <summary>A channel's videos from YouTube: its feed, and its uploads with an API key.</summary>
    private async Task FetchChannel(string sourceID, string channelId, string? apiKey, FoundVideos found, List<string> failed, CancellationToken ct)
    {
        await Try(sourceID + " feed", async () =>
        {
            foreach (var video in await _youtube.Feed(channelId, ct))
                found.Add(video);
        }, failed, ct);

        if (apiKey is not null)
            await Try(sourceID + " uploads", async () =>
            {
                foreach (var video in await _youtube.Uploads(channelId, apiKey, ct))
                    found.Add(video);
            }, failed, ct);
    }

    /// <summary>The details (duration, exact date) of every video still without them, and the archive's untitled ones.</summary>
    private async Task FetchDetails(bool hasArchive, string apiKey, FoundVideos found, List<string> failed, CancellationToken ct)
    {
        var complete = (await _context.Vods.AsNoTracking().Where(Vod.Complete)
            .Select(v => v.YouTubeVideoID).ToListAsync(ct)).ToHashSet();
        var archive = hasArchive ? VodArchive.VideoIDs : Array.Empty<string>();
        var wanted = found.Values.Where(v => !v.IsShort).Select(v => v.VideoID).Concat(archive).Distinct().Where(id => !complete.Contains(id)).ToList();
        if (wanted.Count > 0)
            await Try("YouTube videos", async () =>
            {
                foreach (var video in await _youtube.Videos(wanted, apiKey, ct))
                    found.Add(video);
            }, failed, ct);
    }

    private async Task Try(string what, Func<Task> fetch, List<string> failed, CancellationToken ct)
    {
        try
        {
            await fetch();
        }
        catch (Exception ex) when (ex is YouTubeFetchException or HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            var reason = ex is TaskCanceledException ? "timed out" : ex.Message;
            _logger.LogWarning("VODs: {What} could not be read ({Reason})", what, reason);
            failed.Add($"{what}: {reason}");
        }
    }

    /// <summary>One row per video ID. A new video needs a title and a date and must be longer than a clip; a known one
    /// only gains what it lacked (or a corrected title).</summary>
    private async Task<(int Added, int Updated)> Upsert(IEnumerable<YouTubeVideo> videos, CancellationToken ct)
    {
        var list = videos.ToList();
        var ids = list.Select(v => v.VideoID).ToList();
        var existing = await _context.Vods.Where(v => ids.Contains(v.YouTubeVideoID)).ToDictionaryAsync(v => v.YouTubeVideoID, ct);
        var added = 0;
        var updated = 0;
        foreach (var video in list)
        {
            if (existing.TryGetValue(video.VideoID, out var row))
            {
                if (row.Refresh(video.Title, video.Description, video.PublishedAt, video.PublishedAtIsApproximate, video.DurationSeconds))
                    updated++;
                continue;
            }
            if (video.IsShort || video.DurationSeconds < MinVodSeconds || string.IsNullOrWhiteSpace(video.Title) || video.PublishedAt is null)
                continue;
            // Matched right after (Rematch)
            _context.Vods.Add(Vod.Found(video.VideoID, video.Title, video.Description, video.PublishedAt.Value, video.PublishedAtIsApproximate, video.DurationSeconds, video.ChannelID ?? ""));
            added++;
        }
        await _context.SaveChangesAsync(ct);
        return (added, updated);
    }

    /// <summary>Matches every VOD a sysadmin hasn't decided (Auto and NeedsReview) against today's seasons, teams and
    /// matches: run after each refresh and each import, so a season imported later picks its VODs up. Only what changed is written.</summary>
    public async Task<(int Linked, int NeedsReview)> Rematch(CancellationToken ct = default)
    {
        var matcher = await VodMatcher.Load(_context, ct);
        var rows = await _context.Vods.Include(v => v.Teams)
            .Where(Vod.Undecided)
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            var match = matcher.Match(row.Title, row.PublishedAt);
            row.Rematch(match.Status, match.SeasonID, match.SeasonMatchID, match.TeamIDs, match.Note);
        }
        await _context.SaveChangesAsync(ct);
        return (rows.Count(r => r.MatchStatus == VodMatchStatus.Auto), rows.Count(r => r.MatchStatus == VodMatchStatus.NeedsReview));
    }

    // The pages' VODs: linked by the matcher or a sysadmin, newest first

    private static IQueryable<VodDto> Visible(IQueryable<Vod> vods) => vods
        .Where(Vod.Visible)
        .OrderByDescending(v => v.PublishedAt).ThenByDescending(v => v.VodID)
        .Select(v => new VodDto
        {
            VodID = v.VodID,
            YouTubeVideoID = v.YouTubeVideoID,
            Title = v.Title,
            PublishedAt = v.PublishedAt,
            DurationSeconds = v.DurationSeconds,
            SeasonID = v.SeasonID,
            SeasonName = v.Season == null ? null : v.Season.SeasonName,
            SeasonMatchID = v.SeasonMatchID,
            Teams = v.Teams.OrderBy(t => t.Team.TeamName).Select(t => new VodTeamDto { TeamID = t.TeamID, TeamName = t.Team.TeamName }).ToList(),
        });

    public Task<List<VodDto>> ForSeasonMatch(int seasonMatchId, CancellationToken ct = default)
        => Visible(_context.Vods.AsNoTracking().Where(v => v.SeasonMatchID == seasonMatchId)).ToListAsync(ct);

    public Task<List<VodDto>> ForSeason(int seasonId, CancellationToken ct = default)
        => Visible(_context.Vods.AsNoTracking().Where(v => v.SeasonID == seasonId)).ToListAsync(ct);

    public Task<List<VodDto>> ForTeam(int teamId, CancellationToken ct = default)
        => Visible(_context.Vods.AsNoTracking().Where(v => v.Teams.Any(t => t.TeamID == teamId))).ToListAsync(ct);

    /// <summary>The VODs of the teams the player played on.</summary>
    public Task<List<VodDto>> ForPlayer(int userId, CancellationToken ct = default)
        => Visible(_context.Vods.AsNoTracking().Where(v => v.Teams.Any(t => t.Team.TeamPlayers.Any(p => p.UserID == userId)))).Take(PlayerVodLimit).ToListAsync(ct);

    // The review list (sysadmins)

    public async Task<List<VodReviewDto>> Review(CancellationToken ct = default)
        => await _context.Vods.AsNoTracking()
            .Where(v => v.MatchStatus == VodMatchStatus.NeedsReview)
            .OrderByDescending(v => v.PublishedAt).ThenByDescending(v => v.VodID)
            .Select(v => new VodReviewDto
            {
                VodID = v.VodID,
                YouTubeVideoID = v.YouTubeVideoID,
                Title = v.Title,
                PublishedAt = v.PublishedAt,
                DurationSeconds = v.DurationSeconds,
                SeasonID = v.SeasonID,
                SeasonName = v.Season == null ? null : v.Season.SeasonName,
                SeasonMatchID = v.SeasonMatchID,
                Teams = v.Teams.OrderBy(t => t.Team.TeamName).Select(t => new VodTeamDto { TeamID = t.TeamID, TeamName = t.Team.TeamName }).ToList(),
                Description = v.Description,
                PublishedAtIsApproximate = v.PublishedAtIsApproximate,
                MatchStatus = v.MatchStatus.ToString(),
                MatchNote = v.MatchNote,
            })
            .ToListAsync(ct);

    public Task<List<VodLinkSeasonDto>> LinkSeasons(CancellationToken ct = default)
        => _context.Seasons.AsNoTracking().OrderByDescending(s => s.SeasonStart).ThenBy(s => s.SeasonName)
            .Select(s => new VodLinkSeasonDto { SeasonID = s.SeasonID, SeasonName = s.SeasonName })
            .ToListAsync(ct);

    /// <summary>Null when there is no such season.</summary>
    public async Task<VodLinkOptionsDto?> LinkOptions(int seasonId, CancellationToken ct = default)
    {
        if (!await _context.Seasons.AnyAsync(s => s.SeasonID == seasonId, ct))
            return null;
        var teams = await _context.Teams.AsNoTracking().Where(t => t.SeasonID == seasonId).OrderBy(t => t.TeamName)
            .Select(t => new VodTeamDto { TeamID = t.TeamID, TeamName = t.TeamName }).ToListAsync(ct);
        var matches = await _context.SeasonMatches.AsNoTracking().Where(m => m.SeasonID == seasonId && m.HomeTeamID != null && m.AwayTeamID != null)
            .OrderBy(m => m.ScheduledTime).ThenBy(m => m.SeasonMatchID)
            .Select(m => new { m.SeasonMatchID, m.HomeTeamID, m.AwayTeamID, Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName, m.ScheduledTime })
            .ToListAsync(ct);
        return new VodLinkOptionsDto
        {
            Teams = teams,
            Matches = matches.Select(m => new VodLinkMatchDto
            {
                SeasonMatchID = m.SeasonMatchID,
                Label = $"{m.Home} vs {m.Away}" + (m.ScheduledTime is { } at ? " · " + at.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : ""),
                TeamIDs = [m.HomeTeamID!.Value, m.AwayTeamID!.Value],
            }).ToList(),
        };
    }

    /// <summary>Links the VOD as a sysadmin says (Confirmed: no refresh changes it). Null when done; otherwise why not.
    /// Throws <see cref="KeyNotFoundException"/> when there is no such VOD.</summary>
    public async Task<string?> Link(int vodId, LinkVodRequest request, CancellationToken ct = default)
    {
        var row = await _context.Vods.Include(v => v.Teams).FirstOrDefaultAsync(v => v.VodID == vodId, ct) ?? throw new KeyNotFoundException();
        var seasonId = request.SeasonID;
        var teamIds = request.TeamIDs.Distinct().ToList();
        if (request.SeasonMatchID is { } seasonMatchId)
        {
            var match = await _context.SeasonMatches.AsNoTracking().Where(m => m.SeasonMatchID == seasonMatchId)
                .Select(m => new { m.SeasonID, m.HomeTeamID, m.AwayTeamID }).FirstOrDefaultAsync(ct);
            if (match is null)
                return "There is no such season match.";
            if (seasonId is not null && seasonId != match.SeasonID)
                return "The season match isn't in that season.";
            seasonId = match.SeasonID;
            if (teamIds.Count == 0)
                teamIds = new[] { match.HomeTeamID, match.AwayTeamID }.OfType<int>().ToList();
        }
        if (seasonId is null)
            return "Pick a season or a season match.";
        if (!await _context.Seasons.AnyAsync(s => s.SeasonID == seasonId, ct))
            return "There is no such season.";
        if (teamIds.Count > 0 && await _context.Teams.CountAsync(t => t.SeasonID == seasonId && teamIds.Contains(t.TeamID), ct) != teamIds.Count)
            return "Every team must be one of the season's.";

        row.Confirm(seasonId.Value, request.SeasonMatchID, teamIds);
        await _context.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Hides the VOD for good: a refresh never matches it again. False when there is no such VOD.</summary>
    public async Task<bool> Dismiss(int vodId, CancellationToken ct = default)
    {
        var row = await _context.Vods.FirstOrDefaultAsync(v => v.VodID == vodId, ct);
        if (row is null)
            return false;
        row.Dismiss();
        await _context.SaveChangesAsync(ct);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "VODs refreshed: {Found} found, {Added} added, {Updated} updated, {Linked} linked, {NeedsReview} need review, {Failed} failed")]
    private static partial void LogRefreshed(ILogger logger, int found, int added, int updated, int linked, int needsReview, int failed);
}
